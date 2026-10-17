using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevAcademy.Desktop.Composition;
using DevAcademy.Desktop.Localization;
using DevAcademy.Desktop.Services;
using DevAcademy.Execution;
using DevAcademy.Infrastructure;

namespace DevAcademy.Desktop.ViewModels;

public sealed partial class RuntimeRowViewModel : ObservableObject
{
    private static readonly Regex Url = new(@"https?://\S+", RegexOptions.Compiled);

    public RuntimeRowViewModel(RuntimeStatus r, Func<string, Task> openUrl)
    {
        Language = r.LanguageId;
        Status = Loc.T("Runtime_" + r.Availability);
        IsAvailable = r.Availability == RuntimeAvailability.Available;
        Version = r.Version ?? "";
        Path = r.ExecutablePath ?? "";
        Notes = string.Join("\n", new[] { Loc.Of(r.NotesMessage), Loc.Of(r.WarningMessage) }.Where(x => !string.IsNullOrWhiteSpace(x)));
        HasWarning = !string.IsNullOrWhiteSpace(r.Warning);
        InstallHint = IsAvailable ? "" : Loc.Of(r.InstallHintMessage);
        var m = Url.Match(InstallHint);
        InstallUrl = m.Success ? m.Value.TrimEnd('.', ',', ')') : null;
        OpenInstallCommand = new AsyncRelayCommand(() => InstallUrl is null ? Task.CompletedTask : openUrl(InstallUrl));
    }

    public string Language { get; }
    public string Status { get; }
    public bool IsAvailable { get; }
    public string Version { get; }
    public string Path { get; }
    public string Notes { get; }
    public bool HasWarning { get; }
    public string InstallHint { get; }
    public string? InstallUrl { get; }
    public bool HasInstallUrl => InstallUrl is not null;
    public IRelayCommand OpenInstallCommand { get; }
}

public sealed partial class SettingsViewModel : ObservableObject
{
    public const string ThemeKey = "ui.theme";
    /// <summary>Index of the AI providers tab.</summary>
    public const int AiTab = 3;
    public const int AboutTab = 4;

    private readonly AppServices _s;
    private readonly INavigator _nav;
    private readonly Func<Task> _afterRestore;

    public SettingsViewModel(AppServices s, INavigator nav, Func<Task> afterRestore)
    {
        _s = s;
        _nav = nav;
        _afterRestore = afterRestore;
        Themes = Enum.GetValues<ThemeChoice>().Select(t => new Choice<ThemeChoice>(t, Loc.T("Theme_" + t))).ToList();
        _selectedTheme = Themes[0];
        Profile = new ProfileEditorViewModel(s.Catalog.Tracks, null);
        Ai = new AiSettingsViewModel(s, nav);
        Python = new PythonEnvironmentsViewModel(s);
        Ml = new MlAccelerationViewModel(s);
        LanguageServers = new LanguageServersViewModel(s);
        Node = new NodeEnvironmentViewModel(s);
        Go = new GoEnvironmentViewModel(s);
        NuGet = new NuGetEnvironmentViewModel(s);
        Maven = new MavenEnvironmentViewModel(s);
        Python.EnvironmentsChanged += () => _ = Ml.RefreshEnvironmentsAsync();
    }

    public AiSettingsViewModel Ai { get; }
    public PythonEnvironmentsViewModel Python { get; }
    public MlAccelerationViewModel Ml { get; }
    public LanguageServersViewModel LanguageServers { get; }
    public NodeEnvironmentViewModel Node { get; }
    public GoEnvironmentViewModel Go { get; }
    public NuGetEnvironmentViewModel NuGet { get; }
    public MavenEnvironmentViewModel Maven { get; }
    [ObservableProperty] private int _selectedTab;

    public IReadOnlyList<Choice<ThemeChoice>> Themes { get; }
    public ProfileEditorViewModel Profile { get; private set; }
    public string DataDir => _s.Paths.DataDir;

    /// <summary>Settings → About.</summary>
    public AppInfo About { get; } = AppInfo.Current;
    public string AboutVersion => About.Version;
    public string AboutCommit => About.Commit ?? Loc.T("About_NoCommit");
    public string AboutNotices => AppInfo.FindNoticesFile(FileContentRepository.FindContentRoot()) ?? Loc.T("About_NoNotices");

    [ObservableProperty] private Choice<ThemeChoice> _selectedTheme;
    [ObservableProperty] private string? _profileMessage;
    [ObservableProperty] private IReadOnlyList<RuntimeRowViewModel> _runtimes = [];
    [ObservableProperty] private bool _isDetecting;
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(ProvisionTypesCommand))] private bool _isProvisioning;
    [ObservableProperty] private string? _provisionOutput;
    [ObservableProperty] private string? _dataMessage;
    private bool _loaded;

    public async Task LoadAsync()
    {
        var profile = await _s.Settings.GetProfileAsync();
        Profile = new ProfileEditorViewModel(_s.Catalog.Tracks, profile);
        OnPropertyChanged(nameof(Profile));
        var saved = await _s.Settings.GetAsync(ThemeKey);
        SelectedTheme = Themes.FirstOrDefault(t => string.Equals(t.Value.ToString(), saved, StringComparison.OrdinalIgnoreCase)) ?? Themes[0];
        _loaded = true;
        await Ai.LoadAsync();
        await DetectRuntimesAsync();
        await Python.LoadAsync();
        await Node.LoadAsync();
        await Go.LoadAsync();
        await NuGet.LoadAsync();
        await Maven.LoadAsync();
        await Ml.LoadAsync();
        await LanguageServers.LoadAsync();
    }

    partial void OnSelectedThemeChanged(Choice<ThemeChoice> value)
    {
        if (!_loaded) return;
        _s.Theme.Apply(value.Value);
        _ = _s.Settings.SetAsync(ThemeKey, value.Value.ToString());
    }

    [RelayCommand]
    private async Task SaveProfileAsync()
    {
        await _s.Settings.SaveProfileAsync(Profile.Build());
        ProfileMessage = Loc.T("Settings_ProfileSaved");
    }

    [RelayCommand]
    private async Task DetectRuntimesAsync()
    {
        IsDetecting = true;
        try
        {
            var list = await _s.Runner.DetectRuntimesAsync(CancellationToken.None);
            Runtimes = list.OrderBy(r => r.LanguageId, StringComparer.Ordinal)
                .Select(r => new RuntimeRowViewModel(r, async u => { await _s.Launcher.OpenUrlAsync(u); })).ToList();
        }
        finally { IsDetecting = false; }
    }

    [RelayCommand(CanExecute = nameof(CanProvision))]
    private async Task ProvisionTypesAsync()
    {
        if (!await _s.Dialogs.ConfirmAsync(Loc.T("Types_ConfirmTitle"), Loc.T("Types_ConfirmMessage"), Loc.T("Types_ConfirmButton"))) return;
        IsProvisioning = true;
        ProvisionOutput = Loc.T("Types_Running");
        try
        {
            var result = await TypeScriptTypesProvisioner.ProvisionAsync(_s.ExecutionOptions.CacheDirectory, CancellationToken.None);
            ProvisionOutput = Loc.T(result.Success ? "Types_Success" : "Types_Failure") + (result.Output.Length > 0 ? "\n" + Tail(Loc.Of(result.OutputText, result.Output)) : "");
            await DetectRuntimesAsync();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ProvisionOutput = Loc.F("Types_Error", ex.Message);
        }
        finally { IsProvisioning = false; }
    }

    private bool CanProvision() => !IsProvisioning;

    private static string Tail(string s) => s.Length <= 1500 ? s : "..." + s[^1500..];

    [RelayCommand]
    private async Task ExportAsync()
    {
        var path = await _s.Files.PickSaveAsync(Loc.T("Data_ExportTitle"), "devacademy-progress.json", "json");
        if (path is null) return;
        await RunDataAsync(async () =>
        {
            await File.WriteAllTextAsync(path, await _s.Progress.ExportAsync());
            return Loc.F("Data_Exported", path);
        });
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        var path = await _s.Files.PickOpenAsync(Loc.T("Data_ImportTitle"), "json");
        if (path is null) return;
        if (!await _s.Dialogs.ConfirmAsync(Loc.T("Data_ImportTitle"), Loc.T("Data_ImportConfirm"), Loc.T("Data_ImportButton"))) return;
        await RunDataAsync(async () =>
        {
            var summary = await _s.Progress.ImportAsync(await File.ReadAllTextAsync(path));
            return Loc.F("Data_Imported", summary.LessonsUpdated, summary.SubmissionsAdded);
        });
    }

    [RelayCommand]
    private async Task BackupAsync()
    {
        var path = await _s.Files.PickSaveAsync(Loc.T("Data_BackupTitle"), $"devacademy-{DateTime.Now:yyyyMMdd}.db", "db");
        if (path is null) return;
        await RunDataAsync(async () =>
        {
            await _s.Backup.BackupAsync(path);
            return Loc.F("Data_BackedUp", path);
        });
    }

    [RelayCommand]
    private async Task RestoreAsync()
    {
        var path = await _s.Files.PickOpenAsync(Loc.T("Data_RestoreTitle"), "db");
        if (path is null) return;
        if (!await _s.Dialogs.ConfirmAsync(Loc.T("Data_RestoreTitle"), Loc.T("Data_RestoreConfirm"), Loc.T("Data_RestoreButton"))) return;
        await RunDataAsync(async () =>
        {
            await Task.Run(() => _s.Backup.Restore(path));
            await _afterRestore();
            return Loc.T("Data_Restored");
        });
    }

    private async Task RunDataAsync(Func<Task<string>> action)
    {
        try { DataMessage = await action(); }
        catch (Exception ex) when (ex is not OperationCanceledException) { DataMessage = Loc.F("Data_Error", ex.Message); }
    }
}
