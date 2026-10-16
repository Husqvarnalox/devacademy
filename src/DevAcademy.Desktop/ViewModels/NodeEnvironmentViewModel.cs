using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevAcademy.Application.Node;
using DevAcademy.Desktop.Composition;
using DevAcademy.Desktop.Localization;

namespace DevAcademy.Desktop.ViewModels;

/// <summary>Settings → Runtimes → "npm-пакеты курсов": status of the node content environment and an explicit, confirmed <c>npm ci</c>.</summary>
public sealed partial class NodeEnvironmentViewModel : ObservableObject
{
    private const int MaxLogChars = 30_000;
    private readonly AppServices _s;
    private readonly StringBuilder _log = new();
    private CancellationTokenSource? _cts;

    public NodeEnvironmentViewModel(AppServices s) => _s = s;

    public ObservableCollection<string> Packages { get; } = [];
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private string _pathText = "";
    [ObservableProperty] private string? _message;
    [ObservableProperty] private string _installLog = "";
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(InstallCommand), nameof(CancelCommand))] private bool _isInstalling;

    public bool HasPackages => Packages.Count > 0;

    public async Task LoadAsync()
    {
        try
        {
            var status = await _s.Node.InspectAsync(CancellationToken.None);
            PathText = status.Path;
            Packages.Clear();
            foreach (var p in status.Packages) Packages.Add($"{p.Name} {(string.IsNullOrEmpty(p.Version) ? Loc.T("Node_Missing") : p.Version)}");
            OnPropertyChanged(nameof(HasPackages));
            var nodeLine = status.NodeVersion is null ? Loc.T("Node_NoNode") : Loc.F("Node_Version", status.NodeVersion);
            StatusText = (status.Installed ? Loc.T("Node_Installed") : Loc.T("Node_NotInstalled")) + " " + nodeLine;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            StatusText = Loc.F("Py_Error", ex.Message);
        }
    }

    private bool CanInstall() => !IsInstalling;

    /// <summary>Shows the exact command and the network notice; only a confirmation runs it. Declining runs nothing.</summary>
    [RelayCommand(CanExecute = nameof(CanInstall))]
    public async Task InstallAsync()
    {
        var (plan, error) = _s.Node.PlanInstall();
        if (plan is null) { Message = error; return; }

        var text = new StringBuilder();
        text.AppendLine(Loc.T("Node_ConfirmIntro")).AppendLine().AppendLine(plan.CommandLine).AppendLine();
        text.Append(Loc.T("Node_ConfirmNetwork"));
        if (!await _s.Dialogs.ConfirmAsync(Loc.T("Node_ConfirmTitle"), text.ToString(), Loc.T("Py_ConfirmButton")))
        {
            Message = Loc.T("Py_InstallDeclined");
            return;
        }

        _cts = new CancellationTokenSource();
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
        try
        {
            var result = await _s.Node.InstallAsync(plan, OnLine, _cts.Token);
            Message = result.Ok ? Loc.T("Py_InstallDone") : result.Cancelled ? Loc.T("Py_InstallCancelled") : result.Error;
            await LoadAsync();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Message = Loc.F("Py_Error", ex.Message);
        }
        finally
        {
            IsInstalling = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private bool CanCancel() => IsInstalling;

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel() => _cts?.Cancel();
}
