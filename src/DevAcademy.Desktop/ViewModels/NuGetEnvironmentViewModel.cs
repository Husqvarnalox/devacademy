using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevAcademy.Application.NuGet;
using DevAcademy.Desktop.Composition;
using DevAcademy.Desktop.Localization;
using DevAcademy.Domain;

namespace DevAcademy.Desktop.ViewModels;

/// <summary>One NuGet package set in Settings: its state and the explicit Install / Remove actions (each confirms first).</summary>
public sealed partial class NuGetSetViewModel(NuGetEnvironmentViewModel owner, NuGetSetStatus status) : ObservableObject
{
    public ContentPackageSet Set { get; } = status.Set;
    public string Title => Set.Title;
    public string Description => Set.Description;
    public bool IsInstalled => status.Installed;
    public bool CanRemove => status.Installed;
    public string StatusText => status.Installed
        ? Loc.F("Sets_Installed", string.Join(", ", status.Packages.Select(m => $"{m.Id} {(string.IsNullOrEmpty(m.Version) ? Loc.T("Node_Missing") : m.Version)}")))
        : Loc.T("Sets_NotInstalled");
    public string PathText => status.Path;

    [RelayCommand] private Task InstallAsync() => owner.InstallAsync(Set);
    [RelayCommand] private Task RemoveAsync() => owner.RemoveAsync(Set);
}

/// <summary>
/// Settings → Runtimes → "Пакеты NuGet курсов": the course package sets with their install state. Installing a set is an explicit, confirmed
/// <c>dotnet restore</c> for that set only; removing one deletes its directory after a confirmation. Nothing is installed on load.
/// </summary>
public sealed partial class NuGetEnvironmentViewModel : ObservableObject
{
    private const int MaxLogChars = 30_000;
    private readonly AppServices _s;
    private readonly StringBuilder _log = new();
    private CancellationTokenSource? _cts;

    public NuGetEnvironmentViewModel(AppServices s) => _s = s;

    public ObservableCollection<NuGetSetViewModel> Sets { get; } = [];
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private string _pathText = "";
    [ObservableProperty] private string? _message;
    [ObservableProperty] private string _installLog = "";
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(CancelCommand))] private bool _isInstalling;

    public bool HasSets => Sets.Count > 0;

    public async Task LoadAsync()
    {
        try
        {
            var status = await _s.NuGet.InspectAsync(CancellationToken.None);
            PathText = status.Root;
            Sets.Clear();
            foreach (var set in status.Sets) Sets.Add(new NuGetSetViewModel(this, set));
            OnPropertyChanged(nameof(HasSets));
            var goLine = status.DotnetVersion is null ? Loc.T("NuGet_NoDotnet") : Loc.F("NuGet_Version", status.DotnetVersion);
            StatusText = (Sets.Count == 0 ? Loc.T("Node_NoSets") + " " : "") + goLine;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            StatusText = Loc.F("Py_Error", ex.Message);
        }
    }

    /// <summary>Confirmation (packages, exact command, network notice), then <c>dotnet restore</c> for this set only. Declining runs nothing.</summary>
    public async Task InstallAsync(ContentPackageSet set)
    {
        if (IsInstalling) return;
        _cts = new CancellationTokenSource();
        _log.Clear();
        InstallLog = "";
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
        IsInstalling = true;
        try
        {
            Message = Loc.T("Py_Installing");
            var result = await new PackageSetInstallFlow(_s).InstallAsync(set, OnLine, _cts.Token);
            Message = result.Ok ? Loc.F("Sets_Done", set.Title) : result.Message;
            await LoadAsync();
        }
        finally
        {
            IsInstalling = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    /// <summary>Deletes one set's directory after a confirmation. No network.</summary>
    public async Task RemoveAsync(ContentPackageSet set)
    {
        if (IsInstalling) return;
        var path = _s.NuGet.SetPath(set);
        if (!await _s.Dialogs.ConfirmAsync(Loc.F("Sets_RemoveTitle", set.Title), Loc.F("Sets_RemoveMessage", path), Loc.T("Sets_RemoveConfirm")))
        {
            Message = Loc.T("Py_InstallDeclined");
            return;
        }
        var result = _s.NuGet.RemoveSet(set);
        Message = result.Ok ? Loc.F("Sets_Removed", set.Title) : result.Error;
        await LoadAsync();
    }

    private bool CanCancel() => IsInstalling;

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel() => _cts?.Cancel();
}
