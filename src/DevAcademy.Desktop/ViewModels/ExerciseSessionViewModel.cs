using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevAcademy.Application.Services;
using DevAcademy.Desktop.Composition;
using DevAcademy.Desktop.Localization;
using DevAcademy.Domain;
using DevAcademy.Execution;

namespace DevAcademy.Desktop.ViewModels;

/// <summary>Editor + console + tests + hints for one exercise (or a throw-away scratch buffer).</summary>
public sealed partial class ExerciseSessionViewModel : ObservableObject
{
    public const int ConsoleTab = 0, TestsTab = 1, HintsTab = 2;

    private readonly AppServices _s;
    private readonly string _lessonId;
    private readonly HashSet<string> _dirty = new(StringComparer.Ordinal);
    private CancellationTokenSource? _draftCts;
    private Task _pendingDraft = Task.CompletedTask;
    private bool _loading;

    private IReadOnlyDictionary<string, RuntimeStatus> _runtimes = new Dictionary<string, RuntimeStatus>();
    private bool _switching;

    private ExerciseSessionViewModel(AppServices s, string lessonId, Exercise? exercise, string languageId, string entryFile, string? title)
    {
        _s = s;
        _lessonId = lessonId;
        Exercise = exercise;
        _languageId = languageId;
        _entryFile = entryFile;
        Title = title ?? exercise?.Title ?? "";
        Files.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasMultipleFiles));
        if (exercise is { Evaluation: EvaluationKind.Command }) _outputTab = TestsTab;
    }

    public Exercise? Exercise { get; }
    public bool IsScratch => Exercise is null;
    public ObservableCollection<LanguageOptionViewModel> Languages { get; } = [];
    public bool IsMultiLanguage => Languages.Count > 1;
    public string Title { get; }
    public string Prompt => Exercise?.Prompt ?? "";
    public ObservableCollection<EditorFileViewModel> Files { get; } = [];
    public ObservableCollection<HintItemViewModel> Hints { get; } = [];
    public ObservableCollection<EditorFileViewModel> SolutionFiles { get; } = [];
    public bool HasMultipleFiles => Files.Count > 1;
    public bool CanSubmit => !IsScratch;
    /// <summary>
    /// False for command-graded exercises: their files are modules and tests, not a program, so only Submit makes sense
    /// (the hidden test files exist only at grading time).
    /// </summary>
    public bool CanRun => IsScratch || Exercise!.Evaluation != EvaluationKind.Command;
    /// <summary>The exercise is graded by a real test tool over several files.</summary>
    public bool IsCommandGraded => Exercise is { Evaluation: EvaluationKind.Command };
    public bool HasHints => Exercise is { Hints.Count: > 0 };
    public bool HasSolution => Exercise is { Solution.Count: > 0 };

    [ObservableProperty] private string _languageId;
    [ObservableProperty] private string _entryFile;
    [ObservableProperty] private LanguageOptionViewModel? _selectedLanguage;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(RuntimeHint)), NotifyCanExecuteChangedFor(nameof(RunCommand), nameof(SubmitCommand))] private bool _isRuntimeMissing;
    /// <summary>Why Run/Submit are disabled: the exercise needs an accelerator that this machine/environment does not provide.</summary>
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsAcceleratorBlocked), nameof(HasEnvironmentNotice), nameof(EnvironmentText)), NotifyCanExecuteChangedFor(nameof(RunCommand), nameof(SubmitCommand))] private string? _acceleratorBlock;
    /// <summary>Informational: runs on CPU / accelerator available (accelerator: optional).</summary>
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasEnvironmentNotice), nameof(EnvironmentText))] private string? _environmentNotice;
    [ObservableProperty] private EditorFileViewModel? _selectedFile;
    [ObservableProperty] private string _stdin = "";
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(RunCommand), nameof(SubmitCommand))] private bool _isBusy;
    [ObservableProperty] private ConsoleResultViewModel? _consoleResult;
    [ObservableProperty] private TestReportViewModel? _report;
    [ObservableProperty] private int _outputTab;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _isSolutionVisible;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanRevealHint), nameof(HintButtonText))] private int _revealedCount;

    public bool IsAcceleratorBlocked => AcceleratorBlock is not null;
    public bool HasEnvironmentNotice => AcceleratorBlock is not null || EnvironmentNotice is not null;
    public string? EnvironmentText => AcceleratorBlock ?? EnvironmentNotice;

    /// <summary>Install hint of the selected language's runtime, shown while it is missing.</summary>
    public string? RuntimeHint => IsRuntimeMissing
        ? _runtimes.TryGetValue(LanguageId, out var r) && !string.IsNullOrWhiteSpace(r.InstallHint)
            ? Loc.F("Runtime_MissingHint", Display.LanguageName(LanguageId), r.InstallHint)
            : Loc.F("Runtime_Missing", Display.LanguageName(LanguageId))
        : null;

    public bool CanRevealHint => Exercise is not null && RevealedCount < Exercise.Hints.Count;
    public string HintButtonText => Exercise is null ? "" : Loc.F("Hints_Button", RevealedCount, Exercise.Hints.Count);

    /// <summary>Raised after a submission has been persisted.</summary>
    public event Action<ProgressUpdate>? Submitted;

    public static async Task<ExerciseSessionViewModel> CreateAsync(AppServices s, string lessonId, Exercise exercise)
    {
        var initial = exercise.IsMultiLanguage ? await s.DraftFiles.GetLanguageAsync(exercise) : exercise.AllVariants[0].LanguageId;
        var variant = exercise.ForVariant(initial);
        var vm = new ExerciseSessionViewModel(s, lessonId, exercise, variant.LanguageId, variant.EntryFile, null);
        await vm.LoadAsync();
        return vm;
    }

    public static ExerciseSessionViewModel CreateScratch(AppServices s, string lessonId, string languageId, string code)
    {
        var name = Display.ScratchFileName(languageId);
        var vm = new ExerciseSessionViewModel(s, lessonId, null, languageId, name, Loc.T("Scratch_Title"));
        vm.SetFiles([new ExerciseFile(name, code)]);
        return vm;
    }

    private async Task LoadAsync()
    {
        var ex = Exercise!;
        if (ex.IsMultiLanguage)
        {
            _runtimes = await DetectRuntimesAsync();
            foreach (var v in ex.AllVariants)
                Languages.Add(new LanguageOptionViewModel(v.LanguageId, Display.LanguageName(v.LanguageId), IsAvailable(v.LanguageId)));
            _switching = true;
            SelectedLanguage = Languages.First(l => l.Id == LanguageId);
            _switching = false;
            OnPropertyChanged(nameof(IsMultiLanguage));
            IsRuntimeMissing = !IsAvailable(LanguageId);
        }
        SetFiles(await _s.DraftFiles.LoadFilesAsync(ex, LanguageId));
        await RefreshAcceleratorAsync();
        var revealed = await _s.Hints.GetRevealedAsync(ex.Id);
        foreach (var h in revealed) Hints.Add(new HintItemViewModel(h.Level, h.Total, h.Text));
        RevealedCount = revealed.Count;
    }

    private async Task<IReadOnlyDictionary<string, RuntimeStatus>> DetectRuntimesAsync()
    {
        try
        {
            var all = await _s.Runner.DetectRuntimesAsync(CancellationToken.None);
            return all.GroupBy(r => r.LanguageId, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new Dictionary<string, RuntimeStatus>();
        }
    }

    // Unknown status (detection failed) is treated as available: the run itself then reports the real problem.
    private bool IsAvailable(string languageId) =>
        !_runtimes.TryGetValue(languageId, out var r) || r.Availability == RuntimeAvailability.Available;

    partial void OnSelectedLanguageChanged(LanguageOptionViewModel? value)
    {
        if (_switching || value is null || value.Id == LanguageId) return;
        _ = SelectLanguageAsync(value.Id);
    }

    /// <summary>Switches the editor to another variant; drafts of both languages are kept.</summary>
    public async Task SelectLanguageAsync(string languageId)
    {
        if (Exercise is null || !Exercise.IsMultiLanguage || languageId == LanguageId) return;
        var variant = Exercise.ForVariant(languageId);
        await FlushDraftsAsync();
        _switching = true;
        try
        {
            LanguageId = variant.LanguageId;
            EntryFile = variant.EntryFile;
            SelectedLanguage = Languages.First(l => l.Id == variant.LanguageId);
            Report = null;
            ConsoleResult = null;
            IsSolutionVisible = false;
            SolutionFiles.Clear();
            IsRuntimeMissing = !IsAvailable(variant.LanguageId);
            SetFiles(await _s.DraftFiles.LoadFilesAsync(Exercise, variant.LanguageId));
            await _s.DraftFiles.SetLanguageAsync(Exercise, variant.LanguageId);
            await RefreshAcceleratorAsync();
        }
        finally { _switching = false; }
        RunCommand.NotifyCanExecuteChanged();
        SubmitCommand.NotifyCanExecuteChanged();
    }

    private void SetFiles(IReadOnlyList<ExerciseFile> files)
    {
        _loading = true;
        try
        {
            // New items are appended and selected BEFORE the old ones are removed, so the editor binding never sees a null SelectedFile.
            var old = Files.ToList();
            var list = files.ToList();
            if (list.All(f => f.Path != EntryFile)) list.Insert(0, new ExerciseFile(EntryFile, ""));
            var added = new List<EditorFileViewModel>();
            foreach (var f in list)
            {
                var vm = new EditorFileViewModel(f.Path, f.Content);
                vm.PropertyChanged += OnFileChanged;
                Files.Add(vm);
                added.Add(vm);
            }
            SelectedFile = added.FirstOrDefault(f => f.Path == EntryFile) ?? added.FirstOrDefault();
            foreach (var f in old) { f.PropertyChanged -= OnFileChanged; Files.Remove(f); }
        }
        finally { _loading = false; }
    }

    private void OnFileChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_loading || IsScratch || e.PropertyName != nameof(EditorFileViewModel.Content) || sender is not EditorFileViewModel f) return;
        _dirty.Add(f.Path);
        StatusMessage = null;
        _draftCts?.Cancel();
        var cts = _draftCts = new CancellationTokenSource();
        _pendingDraft = DebouncedSaveAsync(cts.Token);
    }

    private async Task DebouncedSaveAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(_s.DraftDelay, ct);
            await SaveDraftsCoreAsync();
        }
        catch (OperationCanceledException) { }
    }

    /// <summary>Writes pending drafts now (Ctrl+S, before Run/Submit, on leaving the lesson).</summary>
    public async Task FlushDraftsAsync()
    {
        _draftCts?.Cancel();
        try { await _pendingDraft; } catch (OperationCanceledException) { }
        await SaveDraftsCoreAsync();
    }

    private async Task SaveDraftsCoreAsync()
    {
        if (IsScratch || _dirty.Count == 0) return;
        var paths = _dirty.ToList();
        var saveLang = LanguageId;
        _dirty.Clear();
        foreach (var p in paths)
            if (Files.FirstOrDefault(f => f.Path == p) is { } f)
                await _s.DraftFiles.SaveAsync(Exercise!, saveLang, p, f.Content);
        StatusMessage = Loc.T("Editor_DraftSaved");
    }

    [RelayCommand]
    private Task SaveDraftAsync() => FlushDraftsAsync();

    /// <summary>Snapshot of the editor files (used by Run/Submit and, only on explicit confirmation, by the AI tutor).</summary>
    public IReadOnlyList<ExerciseFile> SnapshotFiles() => CurrentFiles();

    /// <summary>
    /// For exercises that declare an accelerator: checks the machine and the active Python environment. "required" and unavailable disables
    /// Run/Submit with the reason; otherwise a short note says where the exercise will run. Requirements (packages) are checked at run time.
    /// </summary>
    private async Task RefreshAcceleratorAsync()
    {
        AcceleratorBlock = null;
        EnvironmentNotice = null;
        if (Exercise is null || LanguageId != "python") return;
        var variant = Exercise.ForVariant(LanguageId);
        if (variant.Accelerator == AcceleratorNeed.None) return;
        try
        {
            var plan = await _s.Python.ResolveAsync(variant, CancellationToken.None);
            // A missing package is reported by the run itself with install instructions; only the accelerator disables the buttons here.
            if (plan.AcceleratorBlocked)
                AcceleratorBlock = plan.BlockedMessage;
            else EnvironmentNotice = plan.Notice;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            EnvironmentNotice = null;
        }
    }

    private IReadOnlyList<ExerciseFile> CurrentFiles() => Files.Select(f => new ExerciseFile(f.Path, f.Content)).ToList();

    [RelayCommand(CanExecute = nameof(CanExecuteBusy))]
    private async Task RunAsync()
    {
        IsBusy = true;
        OutputTab = ConsoleTab;
        try
        {
            await FlushDraftsAsync();
            var result = await _s.Run.RunAsync(LanguageId, CurrentFiles(), EntryFile, Stdin.Length == 0 ? null : Stdin, exercise: Exercise);
            ConsoleResult = new ConsoleResultViewModel(result);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            StatusMessage = Loc.F("Error_Run", ex.Message);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(CanExecuteSubmit))]
    private async Task SubmitAsync()
    {
        IsBusy = true;
        OutputTab = TestsTab;
        try
        {
            await FlushDraftsAsync();
            var files = CurrentFiles();
            var report = await _s.Evaluation.EvaluateAsync(Exercise!, files, languageId: LanguageId);
            var update = await _s.Progress.RecordSubmissionAsync(_lessonId, Exercise!.Id, files, report, languageId: LanguageId);
            Report = new TestReportViewModel(report);
            Submitted?.Invoke(update);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            StatusMessage = Loc.F("Error_Submit", ex.Message);
        }
        finally { IsBusy = false; }
    }

    private bool CanExecuteBusy() => !IsBusy && CanRun && !IsRuntimeMissing && !IsAcceleratorBlocked;
    private bool CanExecuteSubmit() => !IsBusy && CanSubmit && !IsRuntimeMissing && !IsAcceleratorBlocked;

    [RelayCommand]
    private async Task ResetAsync()
    {
        if (IsScratch || Exercise is null) return;
        if (!await _s.Dialogs.ConfirmAsync(Loc.T("Reset_Title"), Loc.T("Reset_Message"), Loc.T("Reset_Confirm"))) return;
        _draftCts?.Cancel();
        _dirty.Clear();
        await _s.DraftFiles.ResetAsync(Exercise, LanguageId);
        SetFiles(Exercise.ForVariant(LanguageId).Starter);
        StatusMessage = Loc.T("Editor_ResetDone");
    }

    [RelayCommand]
    private async Task RevealHintAsync()
    {
        if (Exercise is null) return;
        var hint = await _s.Hints.RevealNextAsync(Exercise.Id);
        if (hint is not null)
        {
            Hints.Add(new HintItemViewModel(hint.Level, hint.Total, hint.Text));
            RevealedCount = hint.Level;
        }
        OutputTab = HintsTab;
    }

    [RelayCommand]
    private async Task ShowSolutionAsync()
    {
        if (Exercise is null) return;
        if (!await _s.Dialogs.ConfirmAsync(Loc.T("Solution_Title"), Loc.T("Solution_Message"), Loc.T("Solution_Confirm"))) return;
        SolutionFiles.Clear();
        foreach (var f in _s.Hints.GetSolution(Exercise.Id, LanguageId)) SolutionFiles.Add(new EditorFileViewModel(f.Path, f.Content));
        IsSolutionVisible = true;
        OutputTab = HintsTab;
    }
}

/// <summary>One entry of the language selector. A language whose runtime is missing is still selectable.</summary>
public sealed record LanguageOptionViewModel(string Id, string Name, bool IsAvailable)
{
    public string Label => IsAvailable ? Name : Loc.F("Runtime_MissingLabel", Name);
    public override string ToString() => Label;
}
