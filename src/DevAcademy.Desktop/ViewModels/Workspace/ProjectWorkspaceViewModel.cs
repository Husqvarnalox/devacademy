using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevAcademy.Application.Workspace;
using DevAcademy.Desktop.Composition;
using DevAcademy.Desktop.Controls.Lsp;
using DevAcademy.Desktop.ViewModels.Lsp;
using DevAcademy.LanguageServices.Protocol;
using DevAcademy.LanguageServices.Servers;
using DevAcademy.Desktop.Localization;
using DevAcademy.Desktop.Services;
using DevAcademy.Execution;
using DevAcademy.Infrastructure.Workspace;
using DevAcademy.Terminal;

namespace DevAcademy.Desktop.ViewModels;

public sealed record PythonChoice(string Key, string Label, string? InterpreterForCommand)
{
    public const string SystemKey = "";
    public const string ProjectVenvKey = ".venv";
    public override string ToString() => Label;
}

public sealed record PostCreateCommand(string Text);

/// <summary>
/// An open project: explorer, editor groups, terminals and the Run/Test buttons. Everything the user runs here runs as an ordinary program
/// in their own account (no sandbox). Nothing is ever typed into a terminal except by the user or by their explicit click on Run/Test/Install.
/// </summary>
public sealed partial class ProjectWorkspaceViewModel : ObservableObject, IDisposable
{
    private readonly AppServices _services;
    private readonly SemaphoreSlim _promptGate = new(1, 1);
    private ProjectWatcher? _watcher;
    private int _terminalCounter;
    private bool _restoring;
    private bool _disposed;

    public ProjectWorkspaceViewModel(AppServices services, string root)
    {
        _services = services;
        Root = Path.GetFullPath(root);
        Name = Path.GetFileName(Root.TrimEnd(Path.DirectorySeparatorChar)) is { Length: > 0 } n ? n : Root;
        Explorer = new ExplorerViewModel(Root);
        _debugHost = new ProjectDebugHost(this, services.Debug);
        Debug = new DebugViewModel(services.Debug, services.Dialogs, _debugHost);
        Debug.Attention += () => ShowDebugPanel = true;
        _debugHost.Breakpoints.Changed += key => { if (!_restoring) _ = SaveSessionAsync(); };
        Search = new WorkspaceSearchViewModel(Root, () => Explorer.ShowIgnored);
        Language = ProjectRunProfiles.DetectLanguage(Root);
        var first = new EditorGroupViewModel { IsActive = true };
        Groups.Add(first);
        _activeGroup = first;
        StartLspTracking();
        Search.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(WorkspaceSearchViewModel.FileQuery) or nameof(WorkspaceSearchViewModel.TextQuery))
            {
                ShowTextHits = !string.IsNullOrEmpty(Search.TextQuery);
                ShowSearch = ShowTextHits || !string.IsNullOrEmpty(Search.FileQuery);
            }
        };
    }

    public string Root { get; }
    public string Name { get; }
    internal DevAcademy.Application.Python.PythonEnvironmentService ServicesPython => _services.Python;
    private readonly ProjectDebugHost _debugHost;
    /// <summary>The debugger of this project (breakpoints, toolbar state, call stack, variables, watches, console).</summary>
    public DebugViewModel Debug { get; }
    public ExplorerViewModel Explorer { get; }
    public WorkspaceSearchViewModel Search { get; }
    public ObservableCollection<EditorGroupViewModel> Groups { get; } = [];
    public ObservableCollection<TerminalTabViewModel> Terminals { get; } = [];
    public ObservableCollection<PythonChoice> PythonChoices { get; } = [];
    public ObservableCollection<PostCreateCommand> PostCreateCommands { get; } = [];

    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsPython), nameof(CanRun), nameof(RunCommandText), nameof(TestCommandText))] private string? _language;
    [ObservableProperty] private EditorGroupViewModel _activeGroup;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(ActiveSession), nameof(HasActiveTerminal), nameof(NoActiveTerminal), nameof(CanKillActive))] private TerminalTabViewModel? _activeTerminal;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(RunCommandText), nameof(TestCommandText))] private PythonChoice? _selectedPython;
    [ObservableProperty] private string? _status;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _showSearch;
    [ObservableProperty] private bool _showTextHits;
    /// <summary>The debug side panel is shown (opens by itself when a debug session starts).</summary>
    [ObservableProperty] private bool _showDebugPanel;

    // ---------------------------------------------------------------- language server

    private Action<DevAcademy.LanguageServices.Hosting.ServerStatus>? _statusHandler;

    /// <summary>Diagnostics of all open files (last known per file), paths relative to the project root.</summary>
    public ObservableCollection<ProblemItemViewModel> Problems { get; } = [];
    public string ProblemsHeader => Problems.Count == 0 ? Loc.T("Out_Problems") : Loc.F("Out_ProblemsCount", Problems.Count);
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasLsp))] private LspStatusView _lspStatus = LspStatusView.None;
    public bool HasLsp => LspStatus.IsVisible;

    private void StartLspTracking()
    {
        Problems.CollectionChanged += (_, _) => OnPropertyChanged(nameof(ProblemsHeader));
        _statusHandler = _ => Avalonia.Threading.Dispatcher.UIThread.Post(RefreshLspStatus);
        _services.Language.Manager.StatusChanged += _statusHandler;
        ActiveGroup.PropertyChanged += OnActiveGroupProperty;
    }

    partial void OnActiveGroupChanged(EditorGroupViewModel? oldValue, EditorGroupViewModel newValue)
    {
        if (oldValue is not null) oldValue.PropertyChanged -= OnActiveGroupProperty;
        newValue.PropertyChanged += OnActiveGroupProperty;
        RefreshLspStatus();
    }

    private void OnActiveGroupProperty(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EditorGroupViewModel.ActiveTab)) { RefreshLspStatus(); _ = Debug.RefreshAsync(); }
    }

    public void RefreshLspStatus()
    {
        if (_disposed) return;
        var m = _services.Language.Manager;
        if (ActiveGroup.ActiveTab?.Lsp is { } t && m.DefinitionFor(t.LanguageId) is { } def)
            LspStatus = LspStatusView.From(m.GetStatus(t.LanguageId, t.Root), def);
        else LspStatus = LspStatusView.None;
    }

    private LspTarget? BuildLspTarget(EditorTabViewModel tab)
    {
        var svc = _services.Language;
        if (!svc.Enabled || LanguageServerRegistry.LanguageForExtension(tab.FullPath) is not { } lang || svc.Manager.DefinitionFor(lang) is not { } def) return null;
        return new LspTarget
        {
            Service = svc,
            LanguageId = lang,
            Root = LanguageServerRegistry.FindRoot(tab.FullPath, Root, def),
            FilePath = tab.FullPath,
            Document = tab.Document,
            DiagnosticsSink = OnDiagnostics,
            Navigate = nav =>
            {
                if (!IsInside(nav.FilePath) || !File.Exists(nav.FilePath)) return false;
                _ = OpenFileAsync(nav.FilePath, nav.Line);
                return true;
            },
        };
    }

    private void OnDiagnostics(LspTarget target, IReadOnlyList<Diagnostic> diagnostics)
    {
        if (_disposed) return;
        var rel = Path.GetRelativePath(Root, target.FilePath).Replace('\\', '/');
        for (var i = Problems.Count - 1; i >= 0; i--)
            if (Problems[i].Path == rel) Problems.RemoveAt(i);
        foreach (var d in diagnostics.OrderBy(d => d.Range.Start.Line).ThenBy(d => d.Range.Start.Character))
            Problems.Add(ProblemItemViewModel.From(rel, d));
    }

    [RelayCommand]
    private async Task OpenProblemAsync(ProblemItemViewModel? problem)
    {
        if (problem is not null) await OpenFileAsync(Path.Combine(Root, problem.Path), problem.Line);
    }

    [RelayCommand]
    private async Task LspHelpAsync()
    {
        if (LspStatus is not { NeedsHelp: true, Definition: { } def }) return;
        if (await LanguageServerInstaller.OfferAsync(_services, def))
            foreach (var t in AllTabs.ToList()) t.Lsp = BuildLspTarget(t);
        RefreshLspStatus();
    }

    public ITerminalSession? ActiveSession => ActiveTerminal?.Session;
    public bool HasActiveTerminal => ActiveTerminal is not null;
    public bool NoActiveTerminal => ActiveTerminal is null;
    public bool CanKillActive => ActiveTerminal?.IsRunning == true;

    /// <summary>Set by the Projects screen: closes this project (after the unsaved-changes prompt).</summary>
    public Func<Task<bool>>? CloseHandler { get; set; }

    [RelayCommand]
    private async Task CloseProjectAsync()
    {
        if (CloseHandler is { } h) await h();
    }

    public bool IsSplit => Groups.Count > 1;
    public bool IsPython => Language == "python";
    public bool CanRun => Profile is not null;
    public string RunCommandText => Profile?.RunCommand ?? "";
    public string TestCommandText => Profile?.TestCommand ?? "";
    public bool HasPostCreate => PostCreateCommands.Count > 0;
    public void NotifyPostCreate() => OnPropertyChanged(nameof(HasPostCreate));

    private RunProfile? Profile => ProjectRunProfiles.For(Language, IsPython ? SelectedPython?.InterpreterForCommand : null);

    public IEnumerable<EditorTabViewModel> AllTabs => Groups.SelectMany(g => g.Tabs);
    public bool HasUnsavedChanges => AllTabs.Any(t => t.IsDirty);

    /// <summary>Raised when the view should show the find / find+replace panel in the active editor.</summary>
    public event Action<bool>? FindRequested;
    /// <summary>Raised with a 1-based line to move the active editor's caret to.</summary>
    public event Action<EditorTabViewModel, int>? GoToLineRequested;

    // ---------------------------------------------------------------- open / restore

    public async Task LoadAsync()
    {
        Explorer.Load();
        // No session writes until the saved one has been read: filling the env list must not overwrite it.
        _restoring = true;
        try
        {
            await RefreshPythonChoicesAsync();
            var session = await _services.Workspace.Sessions.LoadAsync(Root);
            _debugHost.Breakpoints.Load(session.Breakpoints);
            for (var g = 0; g < session.Groups.Count && g < 2; g++)
            {
                var group = g == 0 ? Groups[0] : AddGroup();
                foreach (var rel in session.Groups[g])
                {
                    var full = Path.GetFullPath(Path.Combine(Root, rel));
                    if (IsInside(full) && File.Exists(full)) await OpenFileAsync(full, group: group, activate: false);
                }
                var active = g < session.ActiveTabs.Count ? session.ActiveTabs[g] : null;
                group.ActiveTab = group.Tabs.FirstOrDefault(t => t.RelativePath == active) ?? group.Tabs.FirstOrDefault();
            }
            if (Groups.Count > 1 && Groups[1].Tabs.Count == 0) RemoveGroup(Groups[1]);
            ActivateGroup(Groups[Math.Clamp(session.ActiveGroup, 0, Groups.Count - 1)]);
            if (session.PythonEnvironment is { } key && PythonChoices.FirstOrDefault(c => c.Key == key) is { } choice)
                SelectedPython = choice;
        }
        finally { _restoring = false; }
        await Debug.RefreshAsync();

        if (_services.Workspace.WatchFiles)
        {
            try
            {
                _watcher = new ProjectWatcher(Root);
                var ui = SynchronizationContext.Current;
                _watcher.Changes += batch =>
                {
                    if (_disposed) return;
                    if (ui is null) _ = HandleChangesAsync(batch); else ui.Post(_ => _ = HandleChangesAsync(batch), null);
                };
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or PlatformNotSupportedException)
            {
                Status = Loc.F("Ws_WatcherFailed", ex.Message);
            }
        }
        OnPropertyChanged(nameof(CanRun));
    }

    private bool IsInside(string full)
    {
        var rel = Path.GetRelativePath(Root, full);
        return !rel.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(rel);
    }

    public async Task SaveSessionAsync()
    {
        if (_restoring || _disposed) return;
        var groups = Groups.Select(g => (IReadOnlyList<string>)g.Tabs.Select(t => t.RelativePath).ToList()).ToList();
        var actives = Groups.Select(g => g.ActiveTab?.RelativePath).ToList();
        var session = new ProjectSession(groups, actives, Math.Max(0, Groups.IndexOf(ActiveGroup)), SelectedPython?.Key,
            _debugHost.Breakpoints.Snapshot());
        try { await _services.Workspace.Sessions.SaveAsync(Root, session); }
        catch (Exception ex) when (ex is not OperationCanceledException) { Status = ex.Message; }
    }

    // ---------------------------------------------------------------- editor groups and tabs

    private EditorGroupViewModel AddGroup()
    {
        var g = new EditorGroupViewModel();
        Groups.Add(g);
        OnPropertyChanged(nameof(IsSplit));
        return g;
    }

    private void RemoveGroup(EditorGroupViewModel g)
    {
        if (Groups.Count < 2) return;
        var wasActive = ReferenceEquals(ActiveGroup, g);
        Groups.Remove(g);
        OnPropertyChanged(nameof(IsSplit));
        if (wasActive) ActivateGroup(Groups[0]);
    }

    public void ActivateGroup(EditorGroupViewModel group)
    {
        foreach (var g in Groups) g.IsActive = ReferenceEquals(g, group);
        ActiveGroup = group;
    }

    [RelayCommand]
    private async Task ToggleSplitAsync()
    {
        if (IsSplit)
        {
            // Merge the second group back into the first.
            var second = Groups[1];
            foreach (var t in second.Tabs.ToList()) { second.Tabs.Remove(t); Groups[0].Tabs.Add(t); }
            Groups[0].ActiveTab ??= Groups[0].Tabs.FirstOrDefault();
            RemoveGroup(second);
        }
        else
        {
            var tab = ActiveGroup.ActiveTab;
            var g = AddGroup();
            if (tab is not null && ActiveGroup.Tabs.Count > 1)
            {
                // A file lives in exactly one tab, so splitting moves the active tab to the new group.
                ActiveGroup.Tabs.Remove(tab);
                ActiveGroup.ActiveTab = ActiveGroup.Tabs.LastOrDefault();
                g.Tabs.Add(tab);
                g.ActiveTab = tab;
            }
            ActivateGroup(g);
        }
        await SaveSessionAsync();
    }

    public EditorTabViewModel? FindTab(string fullPath) =>
        AllTabs.FirstOrDefault(t => string.Equals(t.FullPath, fullPath, StringComparison.Ordinal));

    /// <summary>Opens a file in a tab (or activates the tab it is already in). Returns null when it cannot be shown as text.</summary>
    public async Task<EditorTabViewModel?> OpenFileAsync(string fullPath, int? line = null, EditorGroupViewModel? group = null, bool activate = true)
    {
        fullPath = Path.GetFullPath(fullPath);
        if (FindTab(fullPath) is { } existing)
        {
            var owner = Groups.First(g => g.Tabs.Contains(existing));
            owner.ActiveTab = existing;
            if (activate) ActivateGroup(owner);
            if (line is { } l) GoToLineRequested?.Invoke(existing, l);
            await SaveSessionAsync();
            return existing;
        }

        var read = await TextFileStore.ReadAsync(fullPath);
        if (read.Content is not { } content)
        {
            Status = read.Problem switch
            {
                TextFileProblem.Binary => Loc.F("Ws_OpenBinary", Path.GetFileName(fullPath)),
                TextFileProblem.TooLarge => Loc.F("Ws_OpenTooLarge", Path.GetFileName(fullPath)),
                TextFileProblem.NotUtf8 => Loc.F("Ws_OpenNotUtf8", Path.GetFileName(fullPath)),
                _ => Loc.F("Ws_OpenMissing", Path.GetFileName(fullPath)),
            };
            return null;
        }

        var target = group ?? ActiveGroup;
        var tab = new EditorTabViewModel(fullPath, Root, content);
        tab.Lsp = BuildLspTarget(tab);
        tab.Debug = _debugHost.Breakpoints.Get(tab.RelativePath);
        tab.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(EditorTabViewModel.FullPath))
            {
                tab.Lsp = BuildLspTarget(tab);
                tab.Debug = _debugHost.Breakpoints.Get(tab.RelativePath);
                tab.Debug.Key = tab.RelativePath;
            }
        };
        target.Tabs.Add(tab);
        target.ActiveTab = tab;
        if (activate) ActivateGroup(target);
        Status = null;
        if (line is { } ln) GoToLineRequested?.Invoke(tab, ln);
        OnPropertyChanged(nameof(HasUnsavedChanges));
        await SaveSessionAsync();
        return tab;
    }

    [RelayCommand]
    private Task OpenTreeNodeAsync(TreeNodeViewModel? node) =>
        node is { IsDirectory: false, IsPlaceholder: false } ? OpenFileAsync(node.FullPath) : Task.CompletedTask;

    [RelayCommand]
    private Task OpenSearchHitAsync(SearchHitItem? hit) => hit is null ? Task.CompletedTask : OpenFileAsync(hit.Hit.FullPath, hit.Hit.Line);

    [RelayCommand]
    private Task OpenFileResultAsync(string? relative) =>
        string.IsNullOrEmpty(relative) ? Task.CompletedTask : OpenFileAsync(Path.Combine(Root, relative));

    public void ActivateTab(EditorGroupViewModel group, EditorTabViewModel tab)
    {
        group.ActiveTab = tab;
        ActivateGroup(group);
        _ = SaveSessionAsync();
    }

    /// <summary>Closes a tab, asking Save / Don't save / Cancel when it has unsaved changes. False when the user cancelled.</summary>
    public async Task<bool> CloseTabAsync(EditorTabViewModel tab)
    {
        if (tab.IsDirty)
        {
            var choice = await _services.Dialogs.ChooseAsync(Loc.T("Ws_UnsavedTitle"), Loc.F("Ws_UnsavedMessage", tab.Title),
                [Loc.T("Ws_Save"), Loc.T("Ws_DontSave"), Loc.T("Common_Cancel")]);
            if (choice == 0) { if (!await tab.SaveAsync()) { Status = Loc.F("Ws_SaveFailed", tab.Title, tab.SaveError); return false; } }
            else if (choice != 1) return false;
        }
        RemoveTab(tab);
        await SaveSessionAsync();
        return true;
    }

    private void RemoveTab(EditorTabViewModel tab)
    {
        var group = Groups.FirstOrDefault(g => g.Tabs.Contains(tab));
        if (group is null) return;
        var index = group.Tabs.IndexOf(tab);
        group.Tabs.Remove(tab);
        if (ReferenceEquals(group.ActiveTab, tab))
            group.ActiveTab = group.Tabs.Count == 0 ? null : group.Tabs[Math.Min(index, group.Tabs.Count - 1)];
        if (group.Tabs.Count == 0 && Groups.Count > 1) RemoveGroup(group);
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    [RelayCommand]
    private Task CloseTabCommandAsync(EditorTabViewModel? tab) => tab is null ? Task.CompletedTask : CloseTabAsync(tab);

    /// <summary>Asks about every unsaved tab. False when the user cancelled, in which case nothing is closed.</summary>
    public async Task<bool> ConfirmCloseAllAsync()
    {
        foreach (var tab in AllTabs.Where(t => t.IsDirty).ToList())
        {
            var choice = await _services.Dialogs.ChooseAsync(Loc.T("Ws_UnsavedTitle"), Loc.F("Ws_UnsavedMessage", tab.Title),
                [Loc.T("Ws_Save"), Loc.T("Ws_DontSave"), Loc.T("Common_Cancel")]);
            if (choice == 0) { if (!await tab.SaveAsync()) return false; }
            else if (choice != 1) return false;
        }
        return true;
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        if (ActiveGroup.ActiveTab is not { } tab) return;
        if (!await tab.SaveAsync()) Status = Loc.F("Ws_SaveFailed", tab.Title, tab.SaveError);
        else Status = Loc.F("Ws_Saved", tab.Title);
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    [RelayCommand]
    public async Task<bool> SaveAllAsync()
    {
        var ok = true;
        foreach (var tab in AllTabs.Where(t => t.IsDirty).ToList())
            if (!await tab.SaveAsync()) { Status = Loc.F("Ws_SaveFailed", tab.Title, tab.SaveError); ok = false; }
        OnPropertyChanged(nameof(HasUnsavedChanges));
        return ok;
    }

    [RelayCommand]
    private void Find() => FindRequested?.Invoke(false);

    [RelayCommand]
    private void Replace() => FindRequested?.Invoke(true);

    [RelayCommand]
    public async Task GoToLineAsync()
    {
        if (ActiveGroup.ActiveTab is not { } tab) return;
        var answer = await _services.Dialogs.PromptAsync(Loc.T("Ws_GoToLine"), Loc.F("Ws_GoToLineHint", tab.Document.LineCount), "1", Loc.T("Ws_Go"));
        if (answer is not null && int.TryParse(answer.Trim(), out var line)) GoToLineRequested?.Invoke(tab, line);
    }

    // ---------------------------------------------------------------- explorer actions

    [RelayCommand]
    private async Task NewFileAsync()
    {
        var name = await _services.Dialogs.PromptAsync(Loc.T("Ws_NewFile"), Loc.T("Ws_NewFileHint"), "", Loc.T("Ws_Create"));
        if (name is null) return;
        var r = _services.Workspace.FileOps.CreateFile(Explorer.TargetDirectory, name);
        if (!r.Ok) { Status = r.Error; return; }
        Explorer.RefreshDirectories([Explorer.TargetDirectory]);
        Explorer.Reveal(r.Path!);
        await OpenFileAsync(r.Path!);
    }

    [RelayCommand]
    private async Task NewFolderAsync()
    {
        var name = await _services.Dialogs.PromptAsync(Loc.T("Ws_NewFolder"), Loc.T("Ws_NewFolderHint"), "", Loc.T("Ws_Create"));
        if (name is null) return;
        var r = _services.Workspace.FileOps.CreateFolder(Explorer.TargetDirectory, name);
        if (!r.Ok) { Status = r.Error; return; }
        Explorer.RefreshDirectories([Explorer.TargetDirectory]);
        Explorer.Reveal(r.Path!);
    }

    [RelayCommand]
    private async Task RenameAsync()
    {
        if (Explorer.Selected is not { IsPlaceholder: false } node || node.FullPath == Root) return;
        var name = await _services.Dialogs.PromptAsync(Loc.T("Ws_Rename"), Loc.F("Ws_RenameHint", node.Name), node.Name, Loc.T("Ws_Rename"));
        if (name is null || name == node.Name) return;
        var oldPath = node.FullPath;
        var r = _services.Workspace.FileOps.Rename(oldPath, name);
        if (!r.Ok) { Status = r.Error; return; }
        _debugHost.Breakpoints.RenamePrefix(RelativeKey(oldPath), RelativeKey(r.Path!));
        RetargetTabs(oldPath, r.Path!);
        Explorer.RefreshDirectories([Path.GetDirectoryName(oldPath)!]);
        await SaveSessionAsync();
    }

    private string RelativeKey(string full) => Path.GetRelativePath(Root, full).Replace('\\', '/');

    private void RetargetTabs(string oldPath, string newPath)
    {
        foreach (var tab in AllTabs.ToList())
        {
            if (tab.FullPath == oldPath) tab.MovedTo(newPath);
            else if (tab.FullPath.StartsWith(oldPath + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                tab.MovedTo(newPath + tab.FullPath[oldPath.Length..]);
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (Explorer.Selected is not { IsPlaceholder: false } node || node.FullPath == Root) return;
        var ops = _services.Workspace.FileOps;
        var canTrash = ops.CanTrash;
        var dirty = AllTabs.Count(t => (t.FullPath == node.FullPath || t.FullPath.StartsWith(node.FullPath + Path.DirectorySeparatorChar, StringComparison.Ordinal)) && t.IsDirty);
        var message = canTrash ? Loc.F("Ws_DeleteTrashMessage", node.Name) : Loc.F("Ws_DeletePermanentMessage", node.Name);
        if (dirty > 0) message += "\n" + Loc.T("Ws_DeleteDirtyNote");
        if (!await _services.Dialogs.ConfirmAsync(Loc.F("Ws_DeleteTitle", node.Name), message, canTrash ? Loc.T("Ws_ToTrash") : Loc.T("Ws_DeleteForever")))
            return;
        var path = node.FullPath;
        var r = ops.Delete(path, allowPermanent: !canTrash);
        if (!r.Ok && canTrash)
        {
            // The trash refused (for example a different volume): offer permanent deletion explicitly, never silently.
            if (!await _services.Dialogs.ConfirmAsync(Loc.T("Ws_TrashFailedTitle"), Loc.F("Ws_TrashFailedMessage", node.Name), Loc.T("Ws_DeleteForever")))
                return;
            r = ops.Delete(path, allowPermanent: true);
        }
        if (!r.Ok) { Status = r.Error; return; }
        _debugHost.Breakpoints.RemovePrefix(RelativeKey(path));
        foreach (var tab in AllTabs.Where(t => t.FullPath == path || t.FullPath.StartsWith(path + Path.DirectorySeparatorChar, StringComparison.Ordinal)).ToList())
            RemoveTab(tab);
        Explorer.Selected = null;
        Explorer.RefreshDirectories([Path.GetDirectoryName(path)!]);
        await SaveSessionAsync();
    }

    [RelayCommand]
    private void RefreshExplorer() => Explorer.Refresh();

    // ---------------------------------------------------------------- external changes

    /// <summary>Applies a batch of file-system changes: refreshes the tree and asks before touching open files that changed on disk.</summary>
    public async Task HandleChangesAsync(IReadOnlyList<FileChange> changes)
    {
        if (_disposed) return;
        Explorer.RefreshDirectories(changes.SelectMany(c =>
            c.Kind is ChangeKind.Renamed && c.OldPath is not null
                ? new[] { Path.GetDirectoryName(c.FullPath), Path.GetDirectoryName(c.OldPath) }
                : [Path.GetDirectoryName(c.FullPath)]).Where(d => d is not null).Select(d => d!));

        foreach (var change in changes)
        {
            if (change.Kind == ChangeKind.Renamed && change.OldPath is not null && FindTab(change.OldPath) is { } moved && FindTab(change.FullPath) is null)
            {
                moved.MovedTo(change.FullPath);
                continue;
            }
            if (FindTab(change.FullPath) is not { } tab) continue;
            if (change.Kind == ChangeKind.Deleted || !File.Exists(tab.FullPath))
            {
                tab.DeletedOnDisk = true;
                Status = Loc.F("Ws_DeletedOnDisk", tab.Title);
                continue;
            }
            var fingerprint = await TextFileStore.FingerprintOfAsync(tab.FullPath);
            if (fingerprint is null || fingerprint == tab.Fingerprint) continue; // our own save, or an event without a content change
            await PromptReloadAsync(tab, fingerprint);
        }
    }

    private async Task PromptReloadAsync(EditorTabViewModel tab, string fingerprint)
    {
        await _promptGate.WaitAsync();
        try
        {
            if (fingerprint == tab.Fingerprint) return;
            var message = tab.IsDirty ? Loc.F("Ws_ChangedOnDiskDirty", tab.Title) : Loc.F("Ws_ChangedOnDisk", tab.Title);
            var choice = await _services.Dialogs.ChooseAsync(Loc.T("Ws_ChangedOnDiskTitle"), message, [Loc.T("Ws_Reload"), Loc.T("Ws_KeepMine")]);
            if (choice == 0)
            {
                if (!await tab.ReloadAsync()) Status = Loc.F("Ws_OpenMissing", tab.Title);
            }
            else tab.AcceptDiskFingerprint(fingerprint); // keep: do not ask again about this exact content
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }
        finally { _promptGate.Release(); }
    }

    // ---------------------------------------------------------------- terminals

    public TerminalTabViewModel? NewTerminal()
    {
        try
        {
            var session = _services.Workspace.Terminals.Start(new TerminalStartOptions(Root, ExitMessageFormat: Loc.T("Term_ExitFormat")));
            var tab = new TerminalTabViewModel(session, ++_terminalCounter);
            tab.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(TerminalTabViewModel.IsRunning)) OnPropertyChanged(nameof(CanKillActive)); };
            Terminals.Add(tab);
            ActiveTerminal = tab;
            Status = null;
            return tab;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Status = Loc.F("Term_StartFailed", ex.Message);
            return null;
        }
    }

    [RelayCommand]
    private void NewTerminalTab() => NewTerminal();

    [RelayCommand]
    private void KillTerminal(TerminalTabViewModel? tab)
    {
        tab ??= ActiveTerminal;
        tab?.Kill();
    }

    [RelayCommand]
    private void CloseTerminal(TerminalTabViewModel? tab)
    {
        tab ??= ActiveTerminal;
        if (tab is null) return;
        var index = Terminals.IndexOf(tab);
        Terminals.Remove(tab);
        tab.Dispose();
        if (ReferenceEquals(ActiveTerminal, tab))
            ActiveTerminal = Terminals.Count == 0 ? null : Terminals[Math.Min(index, Terminals.Count - 1)];
    }

    /// <summary>Types <paramref name="text"/> into the active running terminal (starting one when needed). The text is visible there like anything the user types.</summary>
    public bool SendToTerminal(string text, bool execute)
    {
        var tab = ActiveTerminal is { IsRunning: true } t ? t : NewTerminal();
        if (tab is null) return false;
        tab.Session.Write(execute ? text + "\r" : text);
        return true;
    }

    [RelayCommand]
    private async Task RunAsync()
    {
        if (Profile is not { } p) return;
        if (!await SaveAllAsync()) return;
        SendToTerminal(p.RunCommand, execute: true);
    }

    [RelayCommand]
    private async Task TestAsync()
    {
        if (Profile is not { } p) return;
        if (!await SaveAllAsync()) return;
        SendToTerminal(p.TestCommand, execute: true);
    }

    [RelayCommand]
    private void TypePostCreate(PostCreateCommand? command)
    {
        // Typed without Enter: the user reads it and presses Enter themselves.
        if (command is not null) SendToTerminal(command.Text, execute: false);
    }

    // ---------------------------------------------------------------- python environment

    private static string VenvInterpreterRelative => OperatingSystem.IsWindows() ? @".venv\Scripts\python.exe" : ".venv/bin/python";

    public string ProjectVenvInterpreter => Path.Combine(Root, VenvInterpreterRelative.Replace('\\', Path.DirectorySeparatorChar));
    public bool HasProjectVenv => File.Exists(ProjectVenvInterpreter);

    public async Task RefreshPythonChoicesAsync()
    {
        var previous = SelectedPython?.Key;
        PythonChoices.Clear();
        PythonChoices.Add(new PythonChoice(PythonChoice.SystemKey, Loc.T("Ws_PythonSystem"), null));
        if (HasProjectVenv) PythonChoices.Add(new PythonChoice(PythonChoice.ProjectVenvKey, Loc.T("Ws_PythonProjectVenv"), VenvInterpreterRelative));
        foreach (var env in _services.Python.ListEnvironments())
            PythonChoices.Add(new PythonChoice("managed:" + env.Name, Loc.F("Ws_PythonManaged", env.Name), env.InterpreterPath));

        var active = await _services.Python.GetActiveNameAsync(CancellationToken.None);
        SelectedPython = PythonChoices.FirstOrDefault(c => c.Key == previous)
            ?? PythonChoices.FirstOrDefault(c => c.Key == PythonChoice.ProjectVenvKey)
            ?? PythonChoices.FirstOrDefault(c => active is not null && c.Key == "managed:" + active)
            ?? PythonChoices[0];
    }

    partial void OnSelectedPythonChanged(PythonChoice? value)
    {
        if (!_restoring && value is not null) { _ = SaveSessionAsync(); _ = Debug.RefreshAsync(); }
    }

    [RelayCommand]
    private async Task CreateVenvAsync()
    {
        if (!IsPython) return;
        if (HasProjectVenv) { Status = Loc.T("Ws_VenvExists"); return; }
        var interpreters = await _services.Python.GetInterpretersAsync(false, CancellationToken.None);
        if (interpreters.Count == 0) { Status = Loc.T("Ws_NoPython"); return; }
        var python = interpreters[0].Path;
        var command = $"{ProjectRunProfiles.Quote(python)} -m venv .venv";
        if (!await _services.Dialogs.ConfirmAsync(Loc.T("Ws_VenvTitle"), Loc.F("Ws_VenvMessage", command, Root), Loc.T("Ws_Create"))) return;

        IsBusy = true;
        Status = Loc.T("Ws_VenvCreating");
        try
        {
            var result = await _services.Workspace.Tools.RunAsync(python, ["-m", "venv", Path.Combine(Root, ".venv")],
                new ToolRunOptions(WorkingDirectory: Root, Timeout: TimeSpan.FromMinutes(3)), CancellationToken.None);
            if (!result.Succeeded) { Status = Loc.F("Ws_VenvFailed", result.FailureText); return; }
            Explorer.RefreshDirectories([Root]);
            await RefreshPythonChoicesAsync();
            SelectedPython = PythonChoices.FirstOrDefault(c => c.Key == PythonChoice.ProjectVenvKey) ?? SelectedPython;
            Status = Loc.T("Ws_VenvCreated");
        }
        finally { IsBusy = false; }
    }

    /// <summary>The visible command the "install requirements" button would type. Null when it cannot run (no requirements file or no environment).</summary>
    public string? InstallRequirementsText =>
        IsPython && File.Exists(Path.Combine(Root, "requirements.txt")) && SelectedPython is { InterpreterForCommand: { } py }
            ? $"{ProjectRunProfiles.Quote(py)} -m pip install -r requirements.txt"
            : null;

    [RelayCommand]
    private async Task InstallRequirementsAsync()
    {
        if (!IsPython) return;
        if (!File.Exists(Path.Combine(Root, "requirements.txt"))) { Status = Loc.T("Ws_NoRequirements"); return; }
        if (InstallRequirementsText is not { } command) { Status = Loc.T("Ws_InstallNeedsEnv"); return; }
        if (!await _services.Dialogs.ConfirmAsync(Loc.T("Ws_InstallTitle"), Loc.F("Ws_InstallMessage", command), Loc.T("Ws_Install"))) return;
        SendToTerminal(command, execute: true);
    }

    // ---------------------------------------------------------------- dispose

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_statusHandler is not null) _services.Language.Manager.StatusChanged -= _statusHandler;
        _watcher?.Dispose();
        Debug.Dispose();
        foreach (var t in Terminals.ToList()) t.Dispose();
        Terminals.Clear();
    }
}
