using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevAcademy.AI;
using DevAcademy.Application.Tutor;
using DevAcademy.Desktop.Composition;
using DevAcademy.Desktop.Localization;

namespace DevAcademy.Desktop.ViewModels;

/// <summary>
/// "AI-наставник" panel of the lesson workspace. Always: scenario click -> exact payload preview -> explicit Send.
/// The response is text only; nothing is applied to code or executed.
/// </summary>
public sealed partial class TutorPanelViewModel : ObservableObject
{
    private readonly AppServices _s;
    private readonly INavigator _nav;
    private readonly string _lessonId;
    private readonly Func<ExerciseSessionViewModel?> _session;
    private TutorPayload? _pending;
    private CancellationTokenSource? _cts;

    public TutorPanelViewModel(AppServices s, INavigator nav, string lessonId, Func<ExerciseSessionViewModel?> session)
    {
        _s = s;
        _nav = nav;
        _lessonId = lessonId;
        _session = session;
    }

    public IReadOnlyList<int> HintLevels { get; } = [1, 2, 3, 4];

    [ObservableProperty] private bool _isExpanded;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(StatusText), nameof(HeaderNote))] private bool _isAiEnabled;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CodeBlockedNote), nameof(HasCodeBlockedNote))] private bool _codeAllowed = true;

    [ObservableProperty] private bool _includeLesson = true;
    [ObservableProperty] private bool _includeCode = true;
    [ObservableProperty] private bool _includeResults = true;
    [ObservableProperty] private bool _includePrevious;
    [ObservableProperty] private int _hintLevel = 1;
    [ObservableProperty] private string _question = "";
    /// <summary>"Не спрашивать снова в этой сессии": skips the preview. Off by default.</summary>
    [ObservableProperty] private bool _skipPreview;

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(AskCommand))] private bool _isBusy;
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(AskCommand))] private bool _isStreaming;
    [ObservableProperty] private bool _isPreviewVisible;
    [ObservableProperty] private string _previewSummary = "";
    [ObservableProperty] private string _previewText = "";
    [ObservableProperty] private string _responseText = "";
    /// <summary>Set only when the answer is complete; rendered as Markdown.</summary>
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasMarkdown))] private string? _responseMarkdown;
    [ObservableProperty] private string? _usageText;
    [ObservableProperty] private string? _notice;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasError))] private string? _errorText;
    [ObservableProperty] private bool _showOpenSettings;

    public bool HasMarkdown => !string.IsNullOrEmpty(ResponseMarkdown);
    public bool HasError => !string.IsNullOrEmpty(ErrorText);
    public string StatusText => IsAiEnabled ? Loc.T("Tutor_Ready") : Loc.T("Tutor_Off");
    public string HeaderNote => IsAiEnabled ? "" : Loc.T("Tutor_OffShort");
    public string CodeBlockedNote => CodeAllowed ? "" : Loc.T("Tutor_CodeBlocked");
    public bool HasCodeBlockedNote => !CodeAllowed;
    public bool IsOffNoticeVisible => !IsAiEnabled;

    partial void OnIsAiEnabledChanged(bool value) => OnPropertyChanged(nameof(IsOffNoticeVisible));
    partial void OnIsExpandedChanged(bool value) { if (value) _ = RefreshStatusAsync(); }

    /// <summary>Reads the global switch and the code policy (cheap settings read; no secret store access).</summary>
    public async Task RefreshStatusAsync()
    {
        var settings = await _s.TutorSettings.LoadAsync();
        IsAiEnabled = settings.AiEnabled;
        CodeAllowed = settings.AllowCode;
    }

    [RelayCommand(CanExecute = nameof(CanAsk))]
    private async Task AskAsync(TutorScenario scenario)
    {
        CancelStreaming();
        ClearOutput();
        IsBusy = true;
        try
        {
            await RefreshStatusAsync();
            if (!IsAiEnabled)
            {
                Refuse(new TutorRefusedException(TutorRefusalReason.AiDisabled, ""));
                return;
            }
            var payload = await _s.Tutor.PrepareAsync(BuildRequest(scenario));
            if (PreflightRefusal(payload) is { } refusal) { Refuse(refusal); return; }
            _pending = payload;
            PreviewSummary = Loc.F("Tutor_PreviewSummary", ProviderName(payload.ProviderId), payload.ModelId, payload.MaxOutputTokens,
                string.Join(", ", payload.IncludedSections));
            PreviewText = FormatPayload(payload);
            IsPreviewVisible = true;
        }
        catch (Exception e) when (e is not OperationCanceledException) { Fail(e); return; }
        finally { IsBusy = false; }
        if (SkipPreview) await SendAsync();
    }

    private bool CanAsk() => !IsBusy && !IsStreaming;

    [RelayCommand]
    private async Task SendAsync()
    {
        if (_pending is not { } payload) return;
        _pending = null;
        IsPreviewVisible = false;
        ResponseText = "";
        ResponseMarkdown = null;
        IsStreaming = true;
        var cts = _cts = new CancellationTokenSource();
        TokenUsage? usage = null;
        var sb = new StringBuilder();
        try
        {
            await foreach (var chunk in _s.Tutor.StreamAsync(payload, cts.Token))
            {
                if (chunk.Usage is not null) usage = chunk.Usage;
                if (string.IsNullOrEmpty(chunk.Text)) continue;
                sb.Append(chunk.Text);
                ResponseText = sb.ToString();
            }
            ResponseMarkdown = ResponseText;
            if (usage is not null)
                UsageText = Loc.F("Tutor_Usage", usage.InputTokens?.ToString() ?? "?", usage.OutputTokens?.ToString() ?? "?");
        }
        catch (OperationCanceledException)
        {
            Notice = Loc.T("Tutor_Stopped");
            if (sb.Length > 0) ResponseMarkdown = ResponseText;
        }
        catch (Exception e)
        {
            if (sb.Length > 0) ResponseMarkdown = ResponseText;
            Fail(e);
        }
        finally
        {
            IsStreaming = false;
            _cts = null;
            cts.Dispose();
        }
    }

    [RelayCommand]
    private void CancelPreview()
    {
        _pending = null;
        IsPreviewVisible = false;
    }

    [RelayCommand]
    private void CancelStreaming() => _cts?.Cancel();

    [RelayCommand]
    private Task OpenSettingsAsync() => _nav.ShowAiSettingsAsync();

    [RelayCommand]
    private void Toggle() => IsExpanded = !IsExpanded;

    /// <summary>Stops a running request when leaving the lesson.</summary>
    public void Shutdown()
    {
        _pending = null;
        _cts?.Cancel();
    }

    // ---- internals ----------------------------------------------------------------------------------------------

    private TutorRequest BuildRequest(TutorScenario scenario)
    {
        var session = _session();
        string? runOutput = null;
        if (session?.ConsoleResult is { } c)
            runOutput = string.Join("\n", new[] { c.CompileOutput, c.Stdout, c.Stderr, c.Message ?? "" }.Where(x => !string.IsNullOrWhiteSpace(x)));
        var message = string.IsNullOrWhiteSpace(Question) ? Loc.T("Tutor_DefaultMessage_" + scenario) : Question.Trim();
        return new TutorRequest(
            scenario, message, _lessonId, session?.Exercise?.Id,
            CurrentFiles: session?.SnapshotFiles(),
            LastReport: session?.Report?.Report,
            RunOutput: runOutput,
            HintLevel: HintLevel,
            Options: new TutorContextOptions(IncludeLesson, IncludeCode, IncludeResults, IncludePrevious));
    }

    private TutorRefusedException? PreflightRefusal(TutorPayload payload)
    {
        var provider = _s.Providers.Find(payload.ProviderId);
        if (provider is null) return new TutorRefusedException(TutorRefusalReason.UnknownProvider, payload.ProviderId);
        if (!provider.RequiresApiKey) return null;
        if (!_s.Ai.SecretStoreAvailable)
            return new TutorRefusedException(TutorRefusalReason.SecretStoreUnavailable, _s.Ai.SecretStoreUnavailableReason ?? "");
        try { if (!_s.Ai.HasKey(payload.ProviderId)) return new TutorRefusedException(TutorRefusalReason.NoApiKey, provider.DisplayName); }
        catch (SecretStoreException e) { return new TutorRefusedException(TutorRefusalReason.SecretStoreUnavailable, e.Message); }
        return null;
    }

    private string ProviderName(string id) => _s.Providers.Find(id)?.DisplayName ?? id;

    private static string FormatPayload(TutorPayload p)
    {
        var sb = new StringBuilder();
        foreach (var m in p.Messages)
            sb.Append("── ").Append(m.Role.ToString().ToLowerInvariant()).AppendLine(" ──").AppendLine(m.Content).AppendLine();
        return sb.ToString().TrimEnd();
    }

    private void ClearOutput()
    {
        _pending = null;
        IsPreviewVisible = false;
        ResponseText = "";
        ResponseMarkdown = null;
        UsageText = null;
        Notice = null;
        ErrorText = null;
        ShowOpenSettings = false;
    }

    private void Refuse(TutorRefusedException e)
    {
        ErrorText = AiMessages.Refusal(e);
        ShowOpenSettings = AiMessages.NeedsSettings(e.Reason);
    }

    private void Fail(Exception e)
    {
        if (e is TutorRefusedException t) Refuse(t);
        else ErrorText = AiMessages.Describe(e);
    }
}
