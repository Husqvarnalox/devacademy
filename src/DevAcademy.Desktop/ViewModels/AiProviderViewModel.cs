using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevAcademy.AI;
using DevAcademy.Application.Tutor;
using DevAcademy.Desktop.Composition;
using DevAcademy.Desktop.Localization;

namespace DevAcademy.Desktop.ViewModels;

/// <summary>One row of the model list. Unknown capabilities are shown as "неизвестно", never guessed.</summary>
public sealed class ModelRowViewModel
{
    public ModelRowViewModel(ModelInfo m)
    {
        Model = m;
        Id = m.Id;
        DisplayName = m.DisplayName ?? "";
        ContextText = m.ContextWindow is { } c ? c.ToString("N0", CultureInfo.CurrentCulture) : Loc.T("Ai_Unknown");
        CapabilitiesText = DescribeCapabilities(m.Capabilities);
        PriceText = DescribePrice(m.Pricing);
    }

    public ModelInfo Model { get; }
    public string Id { get; }
    public string DisplayName { get; }
    public string ContextText { get; }
    public string CapabilitiesText { get; }
    public string PriceText { get; }

    public static string DescribeCapabilities(ModelCapabilities c)
    {
        var items = new (string Key, CapabilitySupport Value)[]
        {
            ("Ai_Cap_Text", c.TextGeneration), ("Ai_Cap_Streaming", c.Streaming), ("Ai_Cap_Tools", c.ToolCalling),
            ("Ai_Cap_Structured", c.StructuredOutput), ("Ai_Cap_Multimodal", c.MultimodalInput), ("Ai_Cap_Reasoning", c.ReasoningControls),
        };
        if (items.All(i => i.Value == CapabilitySupport.Unknown)) return Loc.T("Ai_Unknown");
        return string.Join(" · ", items.Select(i => Loc.T(i.Key) + ": " + i.Value switch
        {
            CapabilitySupport.Supported => Loc.T("Ai_Yes"),
            CapabilitySupport.NotSupported => Loc.T("Ai_No"),
            _ => Loc.T("Ai_Unknown"),
        }));
    }

    /// <summary>Only what the provider published, per 1M tokens (approximate, informational).</summary>
    public static string DescribePrice(ModelPricing? p)
    {
        if (p is null || (p.PromptUsdPerToken is null && p.CompletionUsdPerToken is null)) return "—";
        static string F(decimal? v) => v is null ? "?" : "$" + (v.Value * 1_000_000m).ToString("0.####", CultureInfo.InvariantCulture);
        return Loc.F("Ai_Price", F(p.PromptUsdPerToken), F(p.CompletionUsdPerToken));
    }
}

/// <summary>Setup of one provider: key (write-only), validation, model list, manual model test.</summary>
public sealed partial class AiProviderViewModel : ObservableObject
{
    private readonly AppServices _s;

    public AiProviderViewModel(AppServices s, string id, string name, bool requiresKey, bool isRegistered)
    {
        _s = s;
        Id = id;
        Name = name;
        RequiresKey = requiresKey;
        _isRegistered = isRegistered;
    }

    public string Id { get; }
    public string Name { get; }
    public bool RequiresKey { get; }
    public bool IsCompatible => Id == ProviderIds.OpenAICompatible;
    public bool SecretStoreAvailable => _s.Ai.SecretStoreAvailable;
    public ObservableCollection<ModelRowViewModel> Models { get; } = [];
    public bool HasModels => Models.Count > 0;

    /// <summary>Raised after the model list changed (assignment pickers use it for suggestions).</summary>
    public event Action? ModelsChanged;

    [ObservableProperty] private bool _isRegistered;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(KeyStateText))] private bool _hasKey;
    /// <summary>Write-only: what the learner types. Cleared after saving; the stored key is never read back into the UI.</summary>
    [ObservableProperty] private string _apiKeyInput = "";
    [ObservableProperty] private string _baseUrl = "";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _statusIsError;
    [ObservableProperty] private string? _cacheText;
    [ObservableProperty] private string _customModelId = "";
    [ObservableProperty] private ModelRowViewModel? _selectedModel;

    public string KeyStateText => HasKey ? Loc.T("Ai_KeySaved") : RequiresKey ? Loc.T("Ai_KeyMissing") : Loc.T("Ai_KeyOptional");
    public bool CanSave => SecretStoreAvailable && IsRegistered && !IsBusy && ApiKeyInput.Trim().Length > 0;
    public bool CanUse => IsRegistered && !IsBusy && (HasKey || !RequiresKey);
    public string? ModelIdToTest => !string.IsNullOrWhiteSpace(CustomModelId) ? CustomModelId : SelectedModel?.Id;

    partial void OnApiKeyInputChanged(string value) => RaiseCan();
    partial void OnIsBusyChanged(bool value) => RaiseCan();
    partial void OnHasKeyChanged(bool value) => RaiseCan();
    partial void OnIsRegisteredChanged(bool value) => RaiseCan();
    partial void OnCustomModelIdChanged(string value) => RaiseCan();
    partial void OnSelectedModelChanged(ModelRowViewModel? value) => RaiseCan();

    private void RaiseCan()
    {
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(CanUse));
        SaveKeyCommand.NotifyCanExecuteChanged();
        RemoveKeyCommand.NotifyCanExecuteChanged();
        ValidateCommand.NotifyCanExecuteChanged();
        RefreshModelsCommand.NotifyCanExecuteChanged();
        TestModelCommand.NotifyCanExecuteChanged();
        ApplyBaseUrlCommand.NotifyCanExecuteChanged();
    }

    public async Task LoadAsync()
    {
        IsRegistered = _s.Providers.Find(Id) is not null;
        if (IsCompatible) BaseUrl = await _s.Settings.GetAsync(TutorSettingsKeys.OpenAiCompatibleBaseUrl) ?? "";
        try { HasKey = _s.Ai.HasKey(Id); }
        catch (SecretStoreException) { HasKey = false; }
        if (IsRegistered) ShowModels(await _s.Ai.GetCachedModelsAsync(Id));
    }

    private void ShowModels(CachedModelList? list)
    {
        Models.Clear();
        SelectedModel = null;
        if (list is not null)
        {
            foreach (var m in list.Models) Models.Add(new ModelRowViewModel(m));
            CacheText = Loc.F("Ai_ModelsCache", list.FetchedAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture), list.Models.Count);
        }
        else CacheText = Loc.T("Ai_ModelsNever");
        OnPropertyChanged(nameof(HasModels));
        ModelsChanged?.Invoke();
    }

    private void Ok(string message) => (StatusMessage, StatusIsError) = (message, false);
    private void Fail(Exception e) => (StatusMessage, StatusIsError) = (AiMessages.Describe(e), true);

    private async Task RunAsync(Func<Task> action)
    {
        IsBusy = true;
        try { await action(); }
        catch (Exception e) when (e is not OutOfMemoryException) { Fail(e); }
        finally { IsBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private Task SaveKeyAsync() => RunAsync(() =>
    {
        _s.Ai.SaveKey(Id, ApiKeyInput);
        ApiKeyInput = "";
        HasKey = true;
        Ok(Loc.T("Ai_KeySavedMsg"));
        return Task.CompletedTask;
    });

    [RelayCommand(CanExecute = nameof(CanRemove))]
    private Task RemoveKeyAsync() => RunAsync(() =>
    {
        _s.Ai.RemoveKey(Id);
        HasKey = false;
        Ok(Loc.T("Ai_KeyRemovedMsg"));
        return Task.CompletedTask;
    });

    private bool CanRemove() => HasKey && !IsBusy && SecretStoreAvailable;

    [RelayCommand(CanExecute = nameof(CanUse))]
    private Task ValidateAsync() => RunAsync(async () =>
    {
        var r = await _s.Ai.ValidateAsync(Id);
        if (r.Valid) Ok(Loc.T("Ai_KeyValid"));
        else (StatusMessage, StatusIsError) = (Loc.F("Ai_KeyInvalid", r.Detail ?? ""), true);
    });

    [RelayCommand(CanExecute = nameof(CanUse))]
    private Task RefreshModelsAsync() => RunAsync(async () =>
    {
        var list = await _s.Ai.RefreshModelsAsync(Id);
        ShowModels(list);
        Ok(Loc.F("Ai_ModelsLoaded", list.Models.Count));
    });

    private bool CanTest() => CanUse && !string.IsNullOrWhiteSpace(ModelIdToTest);

    [RelayCommand(CanExecute = nameof(CanTest))]
    private Task TestModelAsync() => RunAsync(async () =>
    {
        var model = ModelIdToTest!; // free text goes through untouched: there is no whitelist
        var r = await _s.Ai.TestModelAsync(Id, model);
        var tokens = r.Usage is null ? "" : Loc.F("Ai_TestTokens", r.Usage.InputTokens?.ToString() ?? "?", r.Usage.OutputTokens?.ToString() ?? "?");
        Ok(Loc.F("Ai_ModelOk", model, (int)r.Latency.TotalMilliseconds, tokens));
    });

    private bool CanApplyUrl() => IsCompatible && !IsBusy && !string.IsNullOrWhiteSpace(BaseUrl);

    /// <summary>Registers (or replaces) the OpenAI-compatible endpoint, e.g. http://localhost:11434/v1 for Ollama.</summary>
    [RelayCommand(CanExecute = nameof(CanApplyUrl))]
    private Task ApplyBaseUrlAsync() => RunAsync(async () =>
    {
        if (!Uri.TryCreate(BaseUrl.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            (StatusMessage, StatusIsError) = (Loc.T("Ai_BaseUrlInvalid"), true);
            return;
        }
        await _s.Settings.SetAsync(TutorSettingsKeys.OpenAiCompatibleBaseUrl, uri.ToString());
        _s.Providers.AddOpenAiCompatible(_s.AiHttp.Client, uri);
        IsRegistered = true;
        ShowModels(await _s.Ai.GetCachedModelsAsync(Id));
        Ok(Loc.F("Ai_BaseUrlSaved", uri.ToString()));
    });
}
