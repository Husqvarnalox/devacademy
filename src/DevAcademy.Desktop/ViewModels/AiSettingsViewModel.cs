using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevAcademy.AI;
using DevAcademy.Application.Ports;
using DevAcademy.Application.Tutor;
using DevAcademy.Desktop.Composition;
using DevAcademy.Desktop.Localization;

namespace DevAcademy.Desktop.ViewModels;

/// <summary>Provider + model for one scenario (or the global default when <see cref="Scenario"/> is null).</summary>
public sealed partial class AssignmentRowViewModel : ObservableObject
{
    private readonly Func<string?, IReadOnlyList<string>> _suggest;

    public AssignmentRowViewModel(TutorScenario? scenario, IReadOnlyList<Choice<string?>> providers, Func<string?, IReadOnlyList<string>> suggest)
    {
        Scenario = scenario;
        Label = scenario is { } s ? AiMessages.ScenarioName(s) : Loc.T("Ai_DefaultRow");
        ProviderChoices = providers;
        _selectedProvider = providers[0];
        _suggest = suggest;
    }

    public TutorScenario? Scenario { get; }
    public string Label { get; }
    public IReadOnlyList<Choice<string?>> ProviderChoices { get; }
    public IReadOnlyList<string> Suggestions => _suggest(SelectedProvider.Value);

    [ObservableProperty, NotifyPropertyChangedFor(nameof(Suggestions), nameof(HasProvider))] private Choice<string?> _selectedProvider;
    /// <summary>Free text: any model id is accepted.</summary>
    [ObservableProperty] private string _modelId = "";

    public bool HasProvider => SelectedProvider.Value is not null;
    public void RefreshSuggestions() => OnPropertyChanged(nameof(Suggestions));
}

public sealed class HistoryRowViewModel(AiRequestRecord r)
{
    public string Time { get; } = r.StartedAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
    public string Provider { get; } = r.Provider;
    public string Model { get; } = r.Model;
    public string Scenario { get; } = AiMessages.ScenarioName(r.Scenario);
    public string Tokens { get; } = r.InputTokens is null && r.OutputTokens is null ? "—" : $"{r.InputTokens?.ToString() ?? "?"} / {r.OutputTokens?.ToString() ?? "?"}";
    public string Cost { get; } = r.ApproxCostUsd is { } c ? "≈$" + c.ToString("0.#####", CultureInfo.InvariantCulture) : "—";
    public string Status { get; } = r.Status switch
    {
        "ok" => Loc.T("Ai_StatusOk"),
        "cancelled" => Loc.T("Ai_StatusCancelled"),
        _ => r.ErrorKind is { } k ? Loc.F("Ai_StatusError", k) : Loc.T("Ai_StatusErrorPlain"),
    };
}

/// <summary>Settings → AI providers: global switch, providers, per-scenario assignment, context and spend controls, history.</summary>
public sealed partial class AiSettingsViewModel : ObservableObject
{
    private static readonly TutorScenario[] ScenarioOrder = Enum.GetValues<TutorScenario>();
    private static readonly string[] KnownOrder =
        [ProviderIds.OpenAI, ProviderIds.Anthropic, ProviderIds.Gemini, ProviderIds.OpenRouter, ProviderIds.OpenAICompatible];

    private readonly AppServices _s;
    private readonly INavigator _nav;
    private bool _loading;
    private Task _saving = Task.CompletedTask;

    public AiSettingsViewModel(AppServices s, INavigator nav)
    {
        _s = s;
        _nav = nav;
    }

    public ObservableCollection<AiProviderViewModel> Providers { get; } = [];
    public ObservableCollection<AssignmentRowViewModel> Assignments { get; } = [];
    public ObservableCollection<HistoryRowViewModel> History { get; } = [];

    public bool SecretStoreAvailable => _s.Ai.SecretStoreAvailable;
    public string? SecretStoreMessage => SecretStoreAvailable ? null : Loc.F("Ai_SecretStoreUnavailable", _s.Ai.SecretStoreUnavailableReason ?? "");
    public bool HasSecretStoreMessage => !SecretStoreAvailable;
    public string Notice => Loc.T("Ai_Notice");
    public bool HasHistory => History.Count > 0;

    [ObservableProperty] private bool _isEnabled;
    [ObservableProperty] private bool _allowCode = true;
    [ObservableProperty] private AiProviderViewModel? _selectedProvider;
    [ObservableProperty] private string _maxOutputTokensText = "";
    [ObservableProperty] private string _monthlyLimitText = "";
    [ObservableProperty] private string _sessionLimitText = "";
    [ObservableProperty] private string? _generalMessage;
    [ObservableProperty] private string? _limitsMessage;
    [ObservableProperty] private string? _assignmentsMessage;
    [ObservableProperty] private string? _historyMessage;

    /// <summary>Completes when queued toggle saves have been written (used by tests and before navigating away).</summary>
    public Task WhenSavedAsync() => _saving;

    partial void OnIsEnabledChanged(bool value) { if (!_loading) Queue(() => _s.TutorSettings.SetAiEnabledAsync(value)); }
    partial void OnAllowCodeChanged(bool value) { if (!_loading) Queue(() => _s.TutorSettings.SetAllowCodeAsync(value)); }

    private void Queue(Func<Task> work) => _saving = RunAfterAsync(_saving, work);

    private async Task RunAfterAsync(Task previous, Func<Task> work)
    {
        try { await previous; } catch (Exception) { /* reported by the failing save */ }
        try { await work(); }
        catch (Exception e) when (e is not OperationCanceledException) { GeneralMessage = Loc.F("Ai_SaveFailed", e.Message); }
    }

    public async Task LoadAsync()
    {
        _loading = true;
        try
        {
            var settings = await _s.TutorSettings.LoadAsync();
            IsEnabled = settings.AiEnabled;
            AllowCode = settings.AllowCode;
            MaxOutputTokensText = settings.MaxOutputTokens?.ToString(CultureInfo.CurrentCulture) ?? "";
            MonthlyLimitText = settings.MonthlyLimitUsd?.ToString(CultureInfo.CurrentCulture) ?? "";
            SessionLimitText = settings.SessionLimitUsd?.ToString(CultureInfo.CurrentCulture) ?? "";

            Providers.Clear();
            var all = _s.Providers.Providers.ToDictionary(p => p.ProviderId);
            var ids = KnownOrder.Where(id => id == ProviderIds.OpenAICompatible || all.ContainsKey(id))
                .Concat(all.Keys.Except(KnownOrder).OrderBy(k => k, StringComparer.Ordinal)).ToList();
            foreach (var id in ids)
            {
                all.TryGetValue(id, out var st);
                var name = id == ProviderIds.OpenAICompatible ? Loc.T("Ai_Provider_Compatible") : st?.DisplayName ?? id;
                var vm = new AiProviderViewModel(_s, id, name, st?.RequiresApiKey ?? false, st is not null);
                vm.ModelsChanged += RefreshSuggestions;
                await vm.LoadAsync();
                Providers.Add(vm);
            }
            SelectedProvider = Providers.FirstOrDefault();

            var choices = BuildProviderChoices();
            Assignments.Clear();
            var def = new AssignmentRowViewModel(null, choices.Default, SuggestionsFor);
            def.SelectedProvider = choices.Default.FirstOrDefault(c => c.Value == settings.Default?.ProviderId) ?? choices.Default[0];
            def.ModelId = settings.Default?.ModelId ?? "";
            Assignments.Add(def);
            foreach (var sc in ScenarioOrder)
            {
                var row = new AssignmentRowViewModel(sc, choices.Scenario, SuggestionsFor);
                if (settings.PerScenario.TryGetValue(sc, out var a))
                {
                    row.SelectedProvider = choices.Scenario.FirstOrDefault(c => c.Value == a.ProviderId) ?? choices.Scenario[0];
                    row.ModelId = a.ModelId;
                }
                Assignments.Add(row);
            }
        }
        finally { _loading = false; }
        await RefreshHistoryAsync();
    }

    private (IReadOnlyList<Choice<string?>> Default, IReadOnlyList<Choice<string?>> Scenario) BuildProviderChoices()
    {
        var real = Providers.Where(p => p.IsRegistered).Select(p => new Choice<string?>(p.Id, p.Name)).ToList();
        var def = new List<Choice<string?>> { new(null, Loc.T("Ai_NotSelected")) };
        def.AddRange(real);
        var sc = new List<Choice<string?>> { new(null, Loc.T("Ai_UseDefault")) };
        sc.AddRange(real);
        return (def, sc);
    }

    private IReadOnlyList<string> SuggestionsFor(string? providerId) =>
        providerId is null ? [] : Providers.FirstOrDefault(p => p.Id == providerId)?.Models.Select(m => m.Id).ToList() ?? [];

    private void RefreshSuggestions()
    {
        foreach (var a in Assignments) a.RefreshSuggestions();
    }

    [RelayCommand]
    private async Task SaveLimitsAsync()
    {
        if (!TryParseLimit(MonthlyLimitText, out var monthly) || !TryParseLimit(SessionLimitText, out var session))
        {
            LimitsMessage = Loc.T("Ai_LimitInvalid");
            return;
        }
        int? tokens = null;
        if (!string.IsNullOrWhiteSpace(MaxOutputTokensText))
        {
            if (!int.TryParse(MaxOutputTokensText.Trim(), NumberStyles.None, CultureInfo.CurrentCulture, out var n) || n <= 0 || n > 200_000)
            {
                LimitsMessage = Loc.T("Ai_TokensInvalid");
                return;
            }
            tokens = n;
        }
        await _s.TutorSettings.SetMonthlyLimitAsync(monthly);
        await _s.TutorSettings.SetSessionLimitAsync(session);
        await _s.Settings.SetAsync(TutorSettingsKeys.MaxOutputTokens, tokens?.ToString(CultureInfo.InvariantCulture) ?? "");
        LimitsMessage = Loc.T("Ai_LimitsSaved");
    }

    private static bool TryParseLimit(string text, out decimal? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text)) return true;
        var t = text.Trim().Replace(',', '.');
        if (!decimal.TryParse(t, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) || d <= 0) return false;
        value = d;
        return true;
    }

    [RelayCommand]
    private async Task SaveAssignmentsAsync()
    {
        foreach (var row in Assignments.Where(r => r.HasProvider))
            if (string.IsNullOrWhiteSpace(row.ModelId))
            {
                AssignmentsMessage = Loc.F("Ai_AssignNoModel", row.Label);
                return;
            }
        foreach (var row in Assignments)
        {
            var pair = row.SelectedProvider.Value is { } p ? new ModelAssignment(p, row.ModelId) : null;
            if (row.Scenario is { } sc) await _s.TutorSettings.SetScenarioAsync(sc, pair);
            else if (pair is not null) await _s.TutorSettings.SetDefaultAsync(pair);
            else
            {
                await _s.Settings.SetAsync(TutorSettingsKeys.DefaultProvider, "");
                await _s.Settings.SetAsync(TutorSettingsKeys.DefaultModel, "");
            }
        }
        AssignmentsMessage = Loc.T("Ai_AssignSaved");
    }

    [RelayCommand]
    private async Task RefreshHistoryAsync()
    {
        History.Clear();
        foreach (var r in await _s.Tutor.GetHistoryAsync(100)) History.Add(new HistoryRowViewModel(r));
        OnPropertyChanged(nameof(HasHistory));
    }

    [RelayCommand]
    private async Task ClearHistoryAsync()
    {
        if (!await _s.Dialogs.ConfirmAsync(Loc.T("Ai_ClearTitle"), Loc.T("Ai_ClearMessage"), Loc.T("Ai_ClearConfirm"))) return;
        var n = await _s.Tutor.DeleteHistoryAsync();
        HistoryMessage = Loc.F("Ai_Cleared", n);
        await RefreshHistoryAsync();
    }
}
