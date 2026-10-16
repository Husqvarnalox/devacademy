using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevAcademy.Application.Python;
using DevAcademy.Desktop.Composition;
using DevAcademy.Desktop.Localization;
using DevAcademy.Execution;

namespace DevAcademy.Desktop.ViewModels;

public sealed record InterpreterRow(PythonInterpreter Interpreter)
{
    public string Display => Interpreter.DisplayName;
    public string Path => Interpreter.Path;
    public string Details => $"{Interpreter.Architecture} · {Interpreter.Source}";
    public override string ToString() => $"{Interpreter.DisplayName}  ({Interpreter.Path})";
}

public sealed partial class EnvironmentRow(ManagedPythonEnvironment env, bool isActive) : ObservableObject
{
    public ManagedPythonEnvironment Environment { get; } = env;
    public string Name => Environment.Name;
    public string Version => Environment.Version is null ? "" : "Python " + Environment.Version;
    public string Tool => Environment.CreatedWithUv ? "uv" : "venv";
    public string Path => Environment.Path;
    [ObservableProperty] private bool _isActive = isActive;
    public override string ToString() => Environment.Name;
}

public sealed record PackageRow(string Name, string Version);

public sealed record ToolChoice(PythonEnvTool Tool, string Label)
{
    public override string ToString() => Label;
}

/// <summary>
/// Settings → Runtimes → "Python-окружения". Managing environments is local; the only network action is package installation,
/// which always shows the exact command and asks for confirmation first (declining runs nothing).
/// </summary>
public sealed partial class PythonEnvironmentsViewModel : ObservableObject
{
    private const int MaxLogChars = 30_000;

    private readonly AppServices _s;
    private CancellationTokenSource? _installCts;
    private readonly StringBuilder _log = new();

    public PythonEnvironmentsViewModel(AppServices s)
    {
        _s = s;
        Tools = [new ToolChoice(PythonEnvTool.Venv, "venv"), new ToolChoice(PythonEnvTool.Uv, "uv")];
        _selectedTool = Tools[0];
    }

    public ObservableCollection<InterpreterRow> Interpreters { get; } = [];
    public ObservableCollection<EnvironmentRow> Environments { get; } = [];
    public ObservableCollection<PackageRow> Packages { get; } = [];
    public IReadOnlyList<ToolChoice> Tools { get; }

    /// <summary>Raised after the set of environments changed (create/delete), so the ML section can refresh its choices.</summary>
    public event Action? EnvironmentsChanged;

    [ObservableProperty] private bool _isDetecting;
    [ObservableProperty] private InterpreterRow? _selectedInterpreter;
    [ObservableProperty] private ToolChoice _selectedTool;
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(CreateCommand))] private string _newName = "";
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(DeleteCommand), nameof(SetActiveCommand), nameof(InstallPackagesCommand), nameof(InstallFromFileCommand))]
    private EnvironmentRow? _selectedEnvironment;
    [ObservableProperty] private string? _activeName;
    [ObservableProperty] private string? _message;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasPackages))] private bool _packagesLoaded;
    [ObservableProperty] private string? _packagesError;
    [ObservableProperty] private string _packageText = "";
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(CreateCommand), nameof(DeleteCommand), nameof(InstallPackagesCommand), nameof(InstallFromFileCommand), nameof(CancelInstallCommand))]
    private bool _isBusy;
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(CancelInstallCommand))] private bool _isInstalling;
    [ObservableProperty] private string _installLog = "";

    public bool HasEnvironments => Environments.Count > 0;
    public bool HasPackages => Packages.Count > 0;
    public bool IsUvAvailable => _s.Python.Environments.IsUvAvailable;
    public string EnvironmentsRoot => _s.Python.Environments.Root;
    public string ActiveText => ActiveName is null ? Loc.T("Py_ActiveNone") : Loc.F("Py_Active", ActiveName);

    partial void OnActiveNameChanged(string? value) => OnPropertyChanged(nameof(ActiveText));

    partial void OnSelectedEnvironmentChanged(EnvironmentRow? value) => _ = LoadPackagesAsync();

    public async Task LoadAsync()
    {
        await DetectInterpretersAsync();
        await RefreshEnvironmentsAsync();
    }

    [RelayCommand]
    private async Task DetectInterpretersAsync()
    {
        IsDetecting = true;
        try
        {
            var list = await _s.Python.GetInterpretersAsync(refresh: true, CancellationToken.None);
            Interpreters.Clear();
            foreach (var i in list) Interpreters.Add(new InterpreterRow(i));
            SelectedInterpreter ??= Interpreters.FirstOrDefault();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Message = Loc.F("Py_Error", ex.Message);
        }
        finally { IsDetecting = false; }
    }

    public async Task RefreshEnvironmentsAsync()
    {
        string? selected = SelectedEnvironment?.Name;
        ActiveName = await _s.Python.GetActiveNameAsync(CancellationToken.None);
        var envs = _s.Python.ListEnvironments();
        if (ActiveName is not null && envs.All(e => e.Name != ActiveName)) ActiveName = null;
        Environments.Clear();
        foreach (var e in envs) Environments.Add(new EnvironmentRow(e, e.Name == ActiveName));
        OnPropertyChanged(nameof(HasEnvironments));
        OnPropertyChanged(nameof(IsUvAvailable));
        SelectedEnvironment = Environments.FirstOrDefault(e => e.Name == selected) ?? Environments.FirstOrDefault();
    }

    private bool CanCreate() => !IsBusy && !string.IsNullOrWhiteSpace(NewName);

    [RelayCommand(CanExecute = nameof(CanCreate))]
    private async Task CreateAsync()
    {
        if (SelectedInterpreter is null) { Message = Loc.T("Py_NoInterpreter"); return; }
        IsBusy = true;
        Message = Loc.T("Py_Creating");
        try
        {
            var r = await _s.Python.CreateEnvironmentAsync(NewName.Trim(), SelectedInterpreter.Path, SelectedTool.Tool, CancellationToken.None);
            if (r.Ok)
            {
                Message = Loc.F("Py_Created", NewName.Trim());
                NewName = "";
                await RefreshEnvironmentsAsync();
                EnvironmentsChanged?.Invoke();
            }
            else Message = r.Error;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Message = Loc.F("Py_Error", ex.Message);
        }
        finally { IsBusy = false; }
    }

    private bool CanDelete() => !IsBusy && SelectedEnvironment is not null;

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private async Task DeleteAsync()
    {
        var env = SelectedEnvironment!;
        if (!await _s.Dialogs.ConfirmAsync(Loc.T("Py_DeleteTitle"), Loc.F("Py_DeleteMessage", env.Name, env.Path), Loc.T("Py_DeleteButton"))) return;
        IsBusy = true;
        try
        {
            var r = await _s.Python.DeleteEnvironmentAsync(env.Name, CancellationToken.None);
            Message = r.Ok ? Loc.F("Py_Deleted", env.Name) : r.Error;
            await RefreshEnvironmentsAsync();
            if (r.Ok) EnvironmentsChanged?.Invoke();
        }
        finally { IsBusy = false; }
    }

    private bool CanSetActive() => SelectedEnvironment is not null;

    [RelayCommand(CanExecute = nameof(CanSetActive))]
    private async Task SetActiveAsync()
    {
        await _s.Python.SetActiveAsync(SelectedEnvironment!.Name, CancellationToken.None);
        Message = Loc.F("Py_ActiveSet", SelectedEnvironment.Name);
        await RefreshEnvironmentsAsync();
    }

    [RelayCommand]
    private async Task ClearActiveAsync()
    {
        await _s.Python.SetActiveAsync(null, CancellationToken.None);
        Message = Loc.T("Py_ActiveCleared");
        await RefreshEnvironmentsAsync();
    }

    private async Task LoadPackagesAsync()
    {
        Packages.Clear();
        PackagesError = null;
        PackagesLoaded = false;
        if (SelectedEnvironment is not { } env) { OnPropertyChanged(nameof(HasPackages)); return; }
        try
        {
            var (list, error) = await _s.Python.ListPackagesAsync(env.Name, CancellationToken.None);
            if (SelectedEnvironment?.Name != env.Name) return; // the selection moved on while pip was listing
            foreach (var p in list) Packages.Add(new PackageRow(p.Name, p.Version));
            PackagesError = error;
            PackagesLoaded = error is null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            PackagesError = ex.Message;
        }
        OnPropertyChanged(nameof(HasPackages));
    }

    [RelayCommand]
    private Task RefreshPackagesAsync() => LoadPackagesAsync();

    // ---------------------------------------------------------------- install

    private bool CanInstall() => !IsBusy && SelectedEnvironment is not null;

    /// <summary>"Установить пакеты…" for the names typed in the box (one per line or separated by spaces/commas).</summary>
    [RelayCommand(CanExecute = nameof(CanInstall))]
    private async Task InstallPackagesAsync()
    {
        var specs = PackageText.Split(['\n', '\r', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => x.Length > 0).ToList();
        if (specs.Count == 0) { Message = Loc.T("Py_NoPackages"); return; }
        await InstallAsync(PythonInstallRequest.FromPackages(specs));
    }

    /// <summary>Install from a requirements.txt or pyproject.toml chosen with the file picker.</summary>
    [RelayCommand(CanExecute = nameof(CanInstall))]
    private async Task InstallFromFileAsync()
    {
        string? path = await _s.Files.PickOpenAsync(Loc.T("Py_PickFile"), "txt");
        if (path is null) return;
        bool pyproject = string.Equals(System.IO.Path.GetExtension(path), ".toml", StringComparison.OrdinalIgnoreCase);
        await InstallAsync(pyproject ? PythonInstallRequest.FromPyproject(path) : PythonInstallRequest.FromRequirements(path));
    }

    /// <summary>Builds the plan, shows the exact command and the PyPI notice, and only then runs it. Declining runs nothing.</summary>
    public async Task InstallAsync(PythonInstallRequest request)
    {
        var env = SelectedEnvironment;
        if (env is null) return;
        IsBusy = true;
        try
        {
            var (plan, notes, error) = await _s.Python.PlanInstallAsync(env.Name, request, CancellationToken.None);
            if (plan is null) { Message = error; return; }

            var text = new StringBuilder();
            text.AppendLine(Loc.F("Py_ConfirmIntro", env.Name));
            text.AppendLine().AppendLine(plan.CommandLine).AppendLine();
            text.AppendLine(Loc.T("Py_ConfirmNetwork"));
            if (notes.Count > 0)
            {
                text.AppendLine().AppendLine(Loc.T("Py_ConfirmNotes"));
                foreach (var n in notes) text.AppendLine("• " + n.Text);
            }
            if (!await _s.Dialogs.ConfirmAsync(Loc.T("Py_ConfirmTitle"), text.ToString().TrimEnd(), Loc.T("Py_ConfirmButton")))
            {
                Message = Loc.T("Py_InstallDeclined");
                return;
            }

            _installCts = new CancellationTokenSource();
            IsInstalling = true;
            _log.Clear();
            InstallLog = "$ " + plan.CommandLine + "\n";
            _log.Append(InstallLog);
            Message = Loc.T("Py_Installing");
            var ui = SynchronizationContext.Current;
            void OnLine(string line)
            {
                void Append()
                {
                    _log.Append(line).Append('\n');
                    if (_log.Length > MaxLogChars) _log.Remove(0, _log.Length - MaxLogChars);
                    InstallLog = _log.ToString();
                }
                if (ui is null) Append(); else ui.Post(_ => Append(), null);
            }
            var result = await _s.Python.InstallAsync(plan, OnLine, _installCts.Token);
            Message = result.Ok ? Loc.T("Py_InstallDone") : result.Cancelled ? Loc.T("Py_InstallCancelled") : result.Error;
            await LoadPackagesAsync();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Message = Loc.F("Py_Error", ex.Message);
        }
        finally
        {
            IsInstalling = false;
            _installCts?.Dispose();
            _installCts = null;
            IsBusy = false;
        }
    }

    private bool CanCancel() => IsInstalling;

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void CancelInstall() => _installCts?.Cancel();
}
