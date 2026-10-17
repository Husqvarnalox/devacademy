using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevAcademy.Desktop.Localization;
using DevAcademy.Desktop.Services;
using DevAcademy.Notebooks.Kernel;
using DevAcademy.Notebooks.Nbformat;

namespace DevAcademy.Desktop.ViewModels.Notebooks;

public sealed record HistoryEntry(int Count, string FirstLine, string Code, bool Ok, string Time)
{
    public string Label => $"[{Count}] {FirstLine}";
}

/// <summary>
/// A notebook open in the project workspace: cells, the Jupyter kernel of the selected Python environment, the variable inspector and the
/// execution history. The kernel is an ordinary Python process with the user's privileges and network access (no sandbox).
/// </summary>
public sealed partial class NotebookViewModel : ObservableObject, IAsyncDisposable
{
    private readonly INotebookKernelService _kernels;
    private readonly IDialogService _dialogs;
    private readonly Func<PythonChoice, string?> _resolve;
    private readonly string _workingDirectory;
    private readonly Action<Action> _post;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _cts = new();
    private IKernelClient? _kernel;
    private bool _stopRunAll;
    private bool _disposed;

    public NotebookViewModel(string path, NotebookDocument document, INotebookKernelService kernels, IDialogService dialogs,
        ObservableCollection<PythonChoice> environments, Func<PythonChoice, string?> resolveInterpreter, string workingDirectory,
        PythonChoice? initialEnvironment = null, Action<Action>? post = null)
    {
        Path = path;
        Document = document;
        _kernels = kernels;
        _dialogs = dialogs;
        Environments = environments;
        _resolve = resolveInterpreter;
        _workingDirectory = workingDirectory;
        _post = post ?? UiPost;
        foreach (var c in document.Cells) Cells.Add(new NotebookCellViewModel(c, MarkDirty, _post));
        if (Cells.Count == 0) AddCell(NotebookCell.NewCode(), -1);
        Select(Cells[0]);
        _selectedEnvironment = initialEnvironment ?? environments.FirstOrDefault(e => _resolve(e) is not null);
    }

    public static void UiPost(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess()) action();
        else Dispatcher.UIThread.Post(action);
    }

    public string Path { get; }
    public string FileName => System.IO.Path.GetFileName(Path);
    public NotebookDocument Document { get; }
    public ObservableCollection<NotebookCellViewModel> Cells { get; } = [];
    public ObservableCollection<PythonChoice> Environments { get; }
    public ObservableCollection<VariableInfo> Variables { get; } = [];
    public ObservableCollection<HistoryEntry> History { get; } = [];
    public IKernelClient? Kernel => _kernel;

    /// <summary>The always-visible statement that the kernel is not sandboxed.</summary>
    public string SandboxWarning => Loc.T("Nb_NoSandbox");
    public string Title => IsDirty ? FileName + " ●" : FileName;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(Title))] private bool _isDirty;
    [ObservableProperty] private NotebookCellViewModel? _selectedCell;
    [ObservableProperty] private PythonChoice? _selectedEnvironment;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(StateText), nameof(CanInterrupt), nameof(IsKernelBusy))] private KernelState _kernelState = KernelState.NotStarted;
    [ObservableProperty] private string? _message;
    [ObservableProperty] private bool _isRunningAll;
    [ObservableProperty] private bool _needsInstall;
    [ObservableProperty] private string _installCommand = "";
    [ObservableProperty] private string _installLog = "";
    [ObservableProperty] private bool _isInstalling;

    public bool HasInstallLog => InstallLog.Length > 0;
    public bool CanInterrupt => KernelState == KernelState.Busy;
    public bool IsKernelBusy => KernelState is KernelState.Busy or KernelState.Starting or KernelState.Restarting;
    public string StateText => Loc.T("Nb_State_" + KernelState);
    public string? EnvironmentText => SelectedEnvironment is { } e ? _resolve(e) : null;

    partial void OnInstallLogChanged(string value) => OnPropertyChanged(nameof(HasInstallLog));

    partial void OnSelectedEnvironmentChanged(PythonChoice? value)
    {
        NeedsInstall = false;
        OnPropertyChanged(nameof(EnvironmentText));
        if (_kernel is not null) _ = StopKernelAsync();
    }

    private void MarkDirty() => IsDirty = true;

    // ---------------------------------------------------------------- cells

    public void Select(NotebookCellViewModel? cell)
    {
        if (SelectedCell is { } old) old.IsSelected = false;
        SelectedCell = cell;
        if (cell is not null) cell.IsSelected = true;
    }

    private NotebookCellViewModel AddCell(NotebookCell model, int after)
    {
        var vm = new NotebookCellViewModel(model, MarkDirty, _post);
        Cells.Insert(after < 0 || after >= Cells.Count ? Cells.Count : after + 1, vm);
        return vm;
    }

    [RelayCommand]
    private void AddCode()
    {
        var cell = AddCell(NotebookCell.NewCode(), SelectedCell is null ? -1 : Cells.IndexOf(SelectedCell));
        Select(cell);
        MarkDirty();
    }

    [RelayCommand]
    private void AddMarkdown()
    {
        var cell = AddCell(NotebookCell.NewMarkdown(), SelectedCell is null ? -1 : Cells.IndexOf(SelectedCell));
        cell.IsEditing = true;
        Select(cell);
        MarkDirty();
    }

    [RelayCommand]
    private void DeleteCell(NotebookCellViewModel? cell)
    {
        cell ??= SelectedCell;
        if (cell is null || cell.IsRunning) return;
        var index = Cells.IndexOf(cell);
        Cells.Remove(cell);
        if (Cells.Count == 0) AddCell(NotebookCell.NewCode(), -1);
        Select(Cells[Math.Min(index, Cells.Count - 1)]);
        MarkDirty();
    }

    [RelayCommand]
    private void MoveUp(NotebookCellViewModel? cell) => Move(cell ?? SelectedCell, -1);

    [RelayCommand]
    private void MoveDown(NotebookCellViewModel? cell) => Move(cell ?? SelectedCell, 1);

    private void Move(NotebookCellViewModel? cell, int delta)
    {
        if (cell is null) return;
        var i = Cells.IndexOf(cell);
        var j = i + delta;
        if (i < 0 || j < 0 || j >= Cells.Count) return;
        Cells.Move(i, j);
        MarkDirty();
    }

    [RelayCommand]
    private void ToggleEdit(NotebookCellViewModel? cell) => (cell ?? SelectedCell)?.ToggleEditing();

    [RelayCommand]
    private void ClearOutputs(NotebookCellViewModel? cell)
    {
        (cell ?? SelectedCell)?.ClearOutputs();
    }

    [RelayCommand]
    private void ClearAllOutputs()
    {
        foreach (var c in Cells.Where(c => !c.IsRunning)) c.ClearOutputs();
    }

    [RelayCommand]
    private void InsertFromHistory(HistoryEntry? entry)
    {
        if (entry is null) return;
        var cell = AddCell(NotebookCell.NewCode(entry.Code), Cells.Count - 1);
        Select(cell);
        MarkDirty();
    }

    // ---------------------------------------------------------------- kernel

    private string? Interpreter() => SelectedEnvironment is { } e ? _resolve(e) : null;

    private async Task<bool> EnsureKernelAsync()
    {
        if (_kernel is { State: not (KernelState.Dead or KernelState.Stopped) }) return true;
        if (_kernel is not null) await StopKernelAsync();

        var interpreter = Interpreter();
        if (interpreter is null)
        {
            Message = Loc.T("Nb_NeedEnvironment");
            return false;
        }
        if (await _kernels.GetInstalledVersionAsync(interpreter, _cts.Token) is null)
        {
            NeedsInstall = true;
            var plan = _kernels.PlanInstall(interpreter);
            InstallCommand = plan?.CommandLine ?? "";
            Message = plan is null ? Loc.F("Nb_NotInVenv", interpreter) : Loc.F("Nb_MissingIpykernel", interpreter);
            return false;
        }

        var kernel = _kernels.Create(interpreter, _workingDirectory);
        kernel.StateChanged += s => _post(() => OnKernelState(kernel, s));
        _kernel = kernel;
        KernelState = KernelState.Starting;
        Message = null;
        try
        {
            await kernel.StartAsync(_cts.Token);
            KernelState = kernel.State;
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Message = Loc.F("Nb_StartFailed", ex.Message);
            _kernel = null;
            KernelState = KernelState.NotStarted;
            try { await kernel.DisposeAsync(); } catch (Exception) { /* already failed */ }
            return false;
        }
    }

    private void OnKernelState(IKernelClient source, KernelState state)
    {
        if (!ReferenceEquals(source, _kernel)) return;
        KernelState = state;
        if (state == KernelState.Dead) Message = Loc.F("Nb_KernelDied", source.DeadReason is null ? "?" : Loc.Of(source.DeadReasonText, source.DeadReason));
    }

    private async Task StopKernelAsync()
    {
        var kernel = _kernel;
        _kernel = null;
        KernelState = KernelState.NotStarted;
        Variables.Clear();
        if (kernel is null) return;
        try { await kernel.ShutdownAsync(); } catch (Exception) { /* best effort: the process is killed below */ }
        try { await kernel.DisposeAsync(); } catch (Exception) { /* nothing more to release */ }
    }

    /// <summary>Runs a code cell and waits for it. Markdown cells are rendered. Returns false when the cell failed or could not run.</summary>
    public async Task<bool> RunCellAsync(NotebookCellViewModel cell)
    {
        if (!cell.IsCode)
        {
            if (cell.IsMarkdown) cell.IsEditing = false;
            return true;
        }
        cell.IsQueued = true;
        try
        {
            await _gate.WaitAsync(_cts.Token);
        }
        catch (OperationCanceledException)
        {
            cell.IsQueued = false;
            return false;
        }
        try
        {
            if (!await EnsureKernelAsync()) return false;
            cell.IsQueued = false;
            cell.ClearOutputs();
            cell.IsRunning = true;
            var kernel = _kernel!;
            ExecuteResult? result = null;
            try
            {
                result = await kernel.ExecuteAsync(cell.Source, new ExecuteOptions { StoreHistory = true }, cell.Receive, _cts.Token);
            }
            catch (KernelDeadException ex)
            {
                Message = Loc.F("Nb_KernelDied", Loc.Msg(ex));
            }
            catch (OperationCanceledException) when (_cts.IsCancellationRequested)
            {
                return false;
            }
            finally
            {
                cell.Flush();
                cell.IsRunning = false;
            }
            if (result is null) return false;
            cell.ExecutionCount = result.ExecutionCount;
            if (result.ExecutionCount is { } n)
                History.Insert(0, new HistoryEntry(n, FirstLine(cell.Source), cell.Source, result.IsOk, DateTime.Now.ToString("HH:mm:ss")));
            MarkDirty();
            if (!IsRunningAll) await RefreshVariablesCoreAsync(kernel);
            return result.IsOk;
        }
        finally
        {
            cell.IsQueued = false;
            _gate.Release();
        }
    }

    private static string FirstLine(string code)
    {
        var line = code.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0) ?? "";
        return line.Length > 70 ? line[..70] + "…" : line;
    }

    /// <summary>Shift+Enter: run, then move to the next cell (a new code cell after the last one).</summary>
    public async Task RunAndAdvanceAsync(NotebookCellViewModel cell)
    {
        var index = Cells.IndexOf(cell);
        if (index == Cells.Count - 1)
        {
            Select(AddCell(NotebookCell.NewCode(), index));
            MarkDirty();
        }
        else Select(Cells[index + 1]);
        await RunCellAsync(cell);
    }

    [RelayCommand]
    private Task RunSelectedAsync(NotebookCellViewModel? cell) => (cell ?? SelectedCell) is { } c ? RunCellAsync(c) : Task.CompletedTask;

    [RelayCommand]
    private Task RunAdvanceAsync(NotebookCellViewModel? cell) => (cell ?? SelectedCell) is { } c ? RunAndAdvanceAsync(c) : Task.CompletedTask;

    [RelayCommand]
    public async Task RunAllAsync()
    {
        if (IsRunningAll) return;
        IsRunningAll = true;
        _stopRunAll = false;
        var all = Cells.ToList();
        var codeCells = all.Where(c => c.IsCode).ToList();
        try
        {
            foreach (var c in codeCells) c.IsQueued = true;
            foreach (var c in all) // markdown cells are rendered on the way, like Jupyter's "Run all"
            {
                if (_stopRunAll || !await RunCellAsync(c)) break;
                c.IsQueued = false;
            }
        }
        finally
        {
            foreach (var c in codeCells) c.IsQueued = false;
            IsRunningAll = false;
        }
        if (_kernel is { } k && await _gate.WaitAsync(0))
        {
            try { await RefreshVariablesCoreAsync(k); }
            finally { _gate.Release(); }
        }
    }

    [RelayCommand]
    private async Task InterruptAsync()
    {
        _stopRunAll = true;
        if (_kernel is { } k)
        {
            try { await k.InterruptAsync(); }
            catch (Exception ex) when (ex is not OperationCanceledException) { Message = ex.Message; }
        }
    }

    [RelayCommand]
    private async Task RestartAsync()
    {
        if (_kernel is not { } k) return;
        if (!await _dialogs.ConfirmAsync(Loc.T("Nb_RestartTitle"), Loc.T("Nb_RestartMessage"), Loc.T("Nb_Restart"))) return;
        _stopRunAll = true;
        Variables.Clear();
        try
        {
            KernelState = KernelState.Restarting;
            await k.RestartAsync(_cts.Token);
            KernelState = k.State;
            Message = Loc.T("Nb_Restarted");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Message = Loc.F("Nb_StartFailed", ex.Message);
        }
    }

    [RelayCommand]
    private async Task RefreshVariablesAsync()
    {
        if (_kernel is not { State: KernelState.Idle } k) return;
        if (!await _gate.WaitAsync(0)) return;
        try { await RefreshVariablesCoreAsync(k); }
        finally { _gate.Release(); }
    }

    private async Task RefreshVariablesCoreAsync(IKernelClient kernel)
    {
        try
        {
            var vars = await VariableInspector.QueryAsync(kernel, _cts.Token);
            Variables.Clear();
            foreach (var v in vars) Variables.Add(v);
        }
        catch (Exception ex) when (ex is KernelDeadException or OperationCanceledException or InvalidOperationException) { /* the kernel went away; the state text says so */ }
    }

    // ---------------------------------------------------------------- ipykernel

    [RelayCommand]
    private async Task InstallKernelAsync()
    {
        if (IsInstalling || Interpreter() is not { } interpreter) return;
        var plan = _kernels.PlanInstall(interpreter);
        if (plan is null) { Message = Loc.F("Nb_NotInVenv", interpreter); return; }
        InstallCommand = plan.CommandLine;
        if (!await _dialogs.ConfirmAsync(Loc.T("Nb_InstallTitle"), Loc.F("Nb_InstallMessage", plan.CommandLine, plan.EnvironmentDirectory), Loc.T("Nb_Install"))) return;

        IsInstalling = true;
        InstallLog = "";
        Message = Loc.T("Nb_Installing");
        try
        {
            var result = await _kernels.InstallAsync(plan, line => _post(() => InstallLog += line + "\n"), _cts.Token);
            if (result.Ok)
            {
                NeedsInstall = false;
                Message = Loc.T("Nb_Installed");
            }
            else Message = Loc.F("Nb_InstallFailed", Loc.Of(result.ErrorText, result.Error));
        }
        catch (OperationCanceledException) { }
        finally { IsInstalling = false; }
    }

    // ---------------------------------------------------------------- file

    [RelayCommand]
    public async Task<bool> SaveAsync()
    {
        Document.Cells = Cells.Select(c => c.Model).ToList();
        try
        {
            await NotebookStore.SaveAsync(Path, Document);
            IsDirty = false;
            Message = Loc.F("Ws_Saved", FileName);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Message = Loc.F("Ws_SaveFailed", FileName, ex.Message);
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        _stopRunAll = true;
        await _cts.CancelAsync();
        await StopKernelAsync();
        _cts.Dispose();
    }
}
