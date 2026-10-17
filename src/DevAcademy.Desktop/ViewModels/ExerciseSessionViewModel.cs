using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevAcademy.Application.Services;
using Avalonia.Threading;
using DevAcademy.Desktop.Composition;
using DevAcademy.Desktop.Controls.Lsp;
using DevAcademy.Desktop.Services;
using DevAcademy.Desktop.ViewModels.Lsp;
using DevAcademy.LanguageServices.Hosting;
using DevAcademy.LanguageServices.Protocol;
using DevAcademy.LanguageServices.Workspace;
using DevAcademy.Desktop.Localization;
using DevAcademy.Domain;
using DevAcademy.Execution;

namespace DevAcademy.Desktop.ViewModels;

/// <summary>Editor + console + tests + hints for one exercise (or a throw-away scratch buffer).</summary>
public sealed partial class ExerciseSessionViewModel : ObservableObject
{
    public const int ConsoleTab = 0, TestsTab = 1, HintsTab = 2, ProblemsTab = 3, DebugTab = 4;

    private readonly AppServices _s;
    private readonly string _lessonId;
    private readonly HashSet<string> _dirty = new(StringComparer.Ordinal);
    private CancellationTokenSource? _draftCts;
    private Task _pendingDraft = Task.CompletedTask;
    private bool _loading;

    private IReadOnlyDictionary<string, RuntimeStatus> _runtimes = new Dictionary<string, RuntimeStatus>();
    private bool _switching;
    private readonly LessonDebugHost _debugHost;

    private ExerciseSessionViewModel(AppServices s, string lessonId, Exercise? exercise, string languageId, string entryFile, string? title)
    {
        _s = s;
        _lessonId = lessonId;
        Exercise = exercise;
        _languageId = languageId;
        _entryFile = entryFile;
        Title = title ?? exercise?.Title ?? "";
        _debugHost = new LessonDebugHost(this, s);
        Debug = new DebugViewModel(s.Debug, s.Dialogs, _debugHost);
        _debugHost.Breakpoints.Changed += key => _ = _debugHost.SaveAsync();
        Debug.Attention += () => OutputTab = DebugTab;
        Files.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasMultipleFiles));
        if (exercise is { Evaluation: EvaluationKind.Command }) _outputTab = TestsTab;
        Problems.CollectionChanged += (_, _) => OnPropertyChanged(nameof(ProblemsHeader));
        var weak = new WeakReference<ExerciseSessionViewModel>(this);
        Action<ServerStatus>? handler = null;
        handler = st =>
        {
            if (weak.TryGetTarget(out var vm)) vm.OnServerStatus(st);
            else s.Language.Manager.StatusChanged -= handler;
        };
        s.Language.Manager.StatusChanged += handler;
    }

    // ------------------------------------------------------------------ language server

    private WorkspaceMirror? _mirror;
    private int _lspGeneration;

    /// <summary>Server diagnostics of every file that has been opened in the editor, last known state per file.</summary>
    public ObservableCollection<ProblemItemViewModel> Problems { get; } = [];
    public string ProblemsHeader => Problems.Count == 0 ? Loc.T("Out_Problems") : Loc.F("Out_ProblemsCount", Problems.Count);
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasLsp))] private LspStatusView _lspStatus = LspStatusView.None;
    public bool HasLsp => LspStatus.IsVisible;

    /// <summary>Asks the view to move the caret of the selected file (1-based line and column).</summary>
    public event Action<int, int>? RevealRequested;

    private void OnServerStatus(ServerStatus st)
    {
        if (st.LanguageId != LanguageId) return;
        Dispatcher.UIThread.Post(RefreshLspStatus);
    }

    private void RefreshLspStatus()
    {
        var m = _s.Language.Manager;
        var def = m.DefinitionFor(LanguageId);
        LspStatus = def is null ? LspStatusView.None : LspStatusView.From(m.GetStatus(LanguageId, _mirror?.Root ?? _s.Language.MirrorBaseDirectory), def);
    }

    private async Task AttachLspAsync(IReadOnlyList<EditorFileViewModel> files)
    {
        var generation = ++_lspGeneration;
        Problems.Clear();
        var svc = _s.Language;
        if (svc.Manager.DefinitionFor(LanguageId) is null || !svc.Enabled)
        {
            _mirror = null;
            RefreshLspStatus();
            return;
        }
        try
        {
            var mirror = svc.MirrorFor(Exercise?.Id ?? "scratch-" + _lessonId, LanguageId);
            await mirror.PrepareAsync(files.Select(f => (f.Path, f.Content)));
            if (generation != _lspGeneration) return;
            _mirror = mirror;
            foreach (var f in files)
                f.Lsp = new LspTarget
                {
                    Service = svc,
                    LanguageId = LanguageId,
                    Root = mirror.Root,
                    FilePath = mirror.PathFor(f.Path),
                    DiagnosticsSink = OnDiagnostics,
                    Navigate = NavigateTo,
                };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Debug.WriteLine($"LSP workspace unavailable: {ex.Message}");
        }
        RefreshLspStatus();
    }

    private void OnDiagnostics(LspTarget target, IReadOnlyList<Diagnostic> diagnostics)
    {
        if (!Files.Any(f => ReferenceEquals(f.Lsp, target))) return;
        var rel = _mirror?.RelativeOf(target.FilePath) ?? Path.GetFileName(target.FilePath);
        for (var i = Problems.Count - 1; i >= 0; i--)
            if (Problems[i].Path == rel) Problems.RemoveAt(i);
        foreach (var d in diagnostics.OrderBy(d => d.Range.Start.Line).ThenBy(d => d.Range.Start.Character))
            Problems.Add(ProblemItemViewModel.From(rel, d));
    }

    private bool NavigateTo(LspNavigation nav)
    {
        var rel = _mirror?.RelativeOf(nav.FilePath);
        var file = rel is null ? null : Files.FirstOrDefault(f => f.Path == rel);
        if (file is null) return false;
        SelectedFile = file;
        RevealRequested?.Invoke(nav.Line, nav.Column);
        return true;
    }

    [RelayCommand]
    private void OpenProblem(ProblemItemViewModel? problem)
    {
        if (problem is null) return;
        var file = Files.FirstOrDefault(f => f.Path == problem.Path);
        if (file is null) return;
        SelectedFile = file;
        RevealRequested?.Invoke(problem.Line, problem.Column);
    }

    [RelayCommand]
    private async Task LspHelpAsync()
    {
        if (LspStatus is not { NeedsHelp: true, Definition: { } def }) return;
        if (await LanguageServerInstaller.OfferAsync(_s, def)) await AttachLspAsync(Files.ToList());
        else RefreshLspStatus();
    }

    /// <summary>The debugger of this editor (Run-like, never part of grading).</summary>
    public DebugViewModel Debug { get; }

    /// <summary>Selects a file by path and moves the caret to a 1-based line (the paused position of the debugger).</summary>
    public void ShowFileAt(string path, int line)
    {
        var file = Files.FirstOrDefault(f => f.Path == path);
        if (file is null) return;
        SelectedFile = file;
        RevealRequested?.Invoke(line, 1);
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
    /// <summary>The package set whose (explicit, confirmed) installation would unblock this exercise; null when nothing is missing.</summary>
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasInstallSet), nameof(InstallSetText), nameof(HasEnvironmentNotice)), NotifyCanExecuteChangedFor(nameof(InstallSetCommand))] private ContentPackageSet? _installSet;
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(InstallSetCommand), nameof(CancelSetInstallCommand))] private bool _isInstallingSet;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasEnvironmentNotice))] private string _installLog = "";
    [ObservableProperty] private EditorFileViewModel? _selectedFile;
    [ObservableProperty] private string _stdin = "";
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(RunCommand), nameof(SubmitCommand))] private bool _isBusy;
    [ObservableProperty] private ConsoleResultViewModel? _consoleResult;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(TestsHeader))] private TestReportViewModel? _report;
    public string TestsHeader => Report is null || Report.Cases.Count == 0 ? Loc.T("Out_Tests") : Loc.F("Out_TestsCount", Report.Cases.Count(c => c.Passed), Report.Cases.Count);
    public string HintsHeader => !HasHints ? Loc.T("Out_Hints") : Loc.F("Out_HintsCount", RevealedCount, Exercise!.Hints.Count);
    [ObservableProperty] private int _outputTab;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _isSolutionVisible;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanRevealHint), nameof(HintButtonText), nameof(HintsHeader))] private int _revealedCount;

    public bool IsAcceleratorBlocked => AcceleratorBlock is not null;
    public bool HasEnvironmentNotice => AcceleratorBlock is not null || EnvironmentNotice is not null || InstallSet is not null || InstallLog.Length > 0;
    public bool HasInstallSet => InstallSet is not null;
    public string InstallSetText => InstallSet is { } set ? Loc.F("Sets_InstallButton", set.Title) : "";
    public string? EnvironmentText => AcceleratorBlock ?? EnvironmentNotice;

    /// <summary>Install hint of the selected language's runtime, shown while it is missing.</summary>
    public string? RuntimeHint => IsRuntimeMissing
        ? _runtimes.TryGetValue(LanguageId, out var r) && !string.IsNullOrWhiteSpace(r.InstallHint)
            ? Loc.F("Runtime_MissingHint", Display.LanguageName(LanguageId), Loc.Of(r.InstallHintMessage))
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
                var vm = new EditorFileViewModel(f.Path, f.Content) { Debug = _debugHost.Breakpoints.Get(f.Path) };
                vm.PropertyChanged += OnFileChanged;
                Files.Add(vm);
                added.Add(vm);
            }
            SelectedFile = added.FirstOrDefault(f => f.Path == EntryFile) ?? added.FirstOrDefault();
            foreach (var f in old) { f.PropertyChanged -= OnFileChanged; Files.Remove(f); }
            _ = AttachLspAsync(added);
            foreach (var (_, t) in _debugHost.Breakpoints.All.ToList()) t.Load([]);
            _ = LoadBreakpointsAsync();
        }
        finally { _loading = false; }
    }

    private async Task LoadBreakpointsAsync()
    {
        await _debugHost.LoadAsync();
        await Debug.RefreshAsync();
    }

    private void OnFileChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (!_loading && e.PropertyName == nameof(EditorFileViewModel.Content) && sender is EditorFileViewModel mirrored)
            _mirror?.Schedule(mirrored.Path, mirrored.Content);
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
    /// Checks the exercise's environment without installing anything. A missing course package set is explained and offered as an install button
    /// (<see cref="InstallSetCommand"/>); an accelerator that is "required" but unavailable disables Run/Submit with the reason; otherwise a short
    /// note says where the exercise will run.
    /// </summary>
    private async Task RefreshAcceleratorAsync()
    {
        AcceleratorBlock = null;
        EnvironmentNotice = null;
        InstallSet = null;
        if (Exercise is not null && LanguageId is "javascript" or "typescript" && Exercise.ForVariant(LanguageId).HasNodeRequirements)
        {
            try
            {
                var plan = await _s.Node.ResolveAsync(Exercise.ForVariant(LanguageId), CancellationToken.None);
                EnvironmentNotice = plan.BlockedMessage;
                InstallSet = plan.InstallSet;
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { EnvironmentNotice = null; }
            return;
        }
        if (Exercise is not null && LanguageId == "go" && Exercise.ForVariant(LanguageId).HasGoRequirements)
        {
            try
            {
                var plan = await _s.Go.ResolveAsync(Exercise.ForVariant(LanguageId), CancellationToken.None);
                EnvironmentNotice = plan.BlockedMessage;
                InstallSet = plan.InstallSet;
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { EnvironmentNotice = null; }
            return;
        }
        if (Exercise is not null && LanguageId == "java" && Exercise.ForVariant(LanguageId).NeedsMavenEnvironment)
        {
            try
            {
                var plan = await _s.Maven.ResolveAsync(Exercise.ForVariant(LanguageId), CancellationToken.None);
                EnvironmentNotice = plan.BlockedMessage;
                InstallSet = plan.InstallSet;
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { EnvironmentNotice = null; }
            return;
        }
        if (Exercise is not null && LanguageId == "csharp" && Exercise.ForVariant(LanguageId).NeedsNuGetEnvironment)
        {
            try
            {
                var plan = await _s.NuGet.ResolveAsync(Exercise.ForVariant(LanguageId), CancellationToken.None);
                EnvironmentNotice = plan.BlockedMessage;
                InstallSet = plan.InstallSet;
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { EnvironmentNotice = null; }
            return;
        }
        if (Exercise is null || LanguageId != "python") return;
        var variant = Exercise.ForVariant(LanguageId);
        if (variant.Accelerator == AcceleratorNeed.None && !variant.HasRequirements) return;
        try
        {
            var plan = await _s.Python.ResolveAsync(variant, CancellationToken.None);
            if (plan.AcceleratorBlocked)
                AcceleratorBlock = plan.BlockedMessage;
            else
            {
                // Missing packages are explained here and again by the run itself; only the accelerator disables the buttons.
                EnvironmentNotice = plan.BlockedMessage ?? plan.Notice;
                InstallSet = plan.InstallSet;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            EnvironmentNotice = null;
        }
    }

    private CancellationTokenSource? _setInstallCts;
    private readonly System.Text.StringBuilder _setInstallLog = new();

    /// <summary>
    /// "Установить пакеты: …": shows the packages, the exact command and the network notice, and installs the exercise's set only after a
    /// confirmation. Declining runs nothing. On success the environment is re-resolved so Run/Submit work.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanInstallSet))]
    private async Task InstallSetAsync()
    {
        if (InstallSet is not { } set) return;
        _setInstallCts = new CancellationTokenSource();
        IsInstallingSet = true;
        _setInstallLog.Clear();
        InstallLog = "";
        var ui = SynchronizationContext.Current;
        void OnLine(string line)
        {
            void Append()
            {
                _setInstallLog.Append(line).Append('\n');
                if (_setInstallLog.Length > 30_000) _setInstallLog.Remove(0, _setInstallLog.Length - 30_000);
                InstallLog = _setInstallLog.ToString();
            }
            if (ui is null) Append(); else ui.Post(_ => Append(), null);
        }
        try
        {
            StatusMessage = Loc.T("Py_Installing");
            var result = await new PackageSetInstallFlow(_s).InstallAsync(set, OnLine, _setInstallCts.Token);
            StatusMessage = result.Ok ? Loc.F("Sets_Done", set.Title) : result.Message;
            if (result.Ok) InstallLog = "";
            await RefreshAcceleratorAsync();
        }
        finally
        {
            IsInstallingSet = false;
            _setInstallCts?.Dispose();
            _setInstallCts = null;
        }
    }

    private bool CanInstallSet() => !IsInstallingSet && InstallSet is not null;

    [RelayCommand(CanExecute = nameof(IsInstallingSet))]
    private void CancelSetInstall() => _setInstallCts?.Cancel();

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
