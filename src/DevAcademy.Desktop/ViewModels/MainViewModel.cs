using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevAcademy.Desktop.Composition;
using DevAcademy.Desktop.Localization;
using DevAcademy.Desktop.Services;
using DevAcademy.Domain;

namespace DevAcademy.Desktop.ViewModels;

public enum Section { None, Catalog, Problems, Dashboard, Settings, Projects }

public sealed partial class MainViewModel(AppServices services) : ObservableObject, INavigator
{
    [ObservableProperty, NotifyPropertyChangedFor(nameof(ShowNavigation))] private object? _current;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsCatalogSection), nameof(IsProblemsSection), nameof(IsDashboardSection), nameof(IsSettingsSection), nameof(IsProjectsSection))] private Section _section;
    [ObservableProperty] private string? _error;

    // Sidebar: expanded (220 px) or an icon rail (56 px). Collapses by itself in narrow windows and in the lesson
    // workspace (which needs the width); an explicit toggle wins and is remembered.
    public const string SidebarKey = "ui.sidebar.collapsed";
    public const double SidebarExpandedWidth = 220;
    public const double SidebarRailWidth = 56;
    private bool? _userCollapsed;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsSidebarCollapsed), nameof(IsSidebarExpanded), nameof(SidebarWidth))] private bool _narrowWindow;
    public bool IsSidebarCollapsed => _userCollapsed ?? (NarrowWindow || Current is LessonWorkspaceViewModel);
    public bool IsSidebarExpanded => !IsSidebarCollapsed;
    public double SidebarWidth => IsSidebarCollapsed ? SidebarRailWidth : SidebarExpandedWidth;

    [RelayCommand]
    private void ToggleSidebar()
    {
        _userCollapsed = !IsSidebarCollapsed;
        RaiseSidebarChanged();
        _ = services.Settings.SetAsync(SidebarKey, _userCollapsed.Value ? "1" : "0");
    }

    private void RaiseSidebarChanged()
    {
        OnPropertyChanged(nameof(IsSidebarCollapsed));
        OnPropertyChanged(nameof(IsSidebarExpanded));
        OnPropertyChanged(nameof(SidebarWidth));
    }

    public AppServices Services => services;
    public bool IsCatalogSection => Section == Section.Catalog;
    public bool IsProblemsSection => Section == Section.Problems;
    /// <summary>The "Problems" nav item exists only when algorithm content is loaded.</summary>
    public bool HasProblems => services.Problems.HasProblems;
    public bool IsDashboardSection => Section == Section.Dashboard;
    public bool IsSettingsSection => Section == Section.Settings;
    public bool IsProjectsSection => Section == Section.Projects;
    private ProjectsViewModel? _projects;
    /// <summary>True when an open project has unsaved editor tabs.</summary>
    public bool HasUnsavedProjectChanges => _projects?.HasUnsavedChanges == true;
    public bool ShowNavigation => Current is not (null or OnboardingViewModel);

    /// <summary>Applies the saved theme and opens onboarding (first launch) or the catalog.</summary>
    public async Task InitializeAsync()
    {
        try
        {
            if (await services.Settings.GetAsync(SettingsViewModel.ThemeKey) is { } saved
                && Enum.TryParse<ThemeChoice>(saved, true, out var theme))
                services.Theme.Apply(theme);
            if (await services.Settings.GetAsync(SidebarKey) is { } sidebar)
            {
                _userCollapsed = sidebar == "1";
                RaiseSidebarChanged();
            }
            if (await services.Settings.GetProfileAsync() is null)
            {
                await SwitchAsync(new OnboardingViewModel(services, this), Section.None);
            }
            else await ShowCatalogAsync();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Error = Loc.F("Error_Startup", ex.Message);
        }
    }

    private async Task SwitchAsync(object page, Section section)
    {
        if (Current is LessonWorkspaceViewModel w) await w.FlushAsync();
        if (Current is ProjectsViewModel pv) await pv.SaveSessionAsync();
        Current = page;
        Section = section;
        RaiseSidebarChanged();
    }

    public async Task ShowCatalogAsync()
    {
        var vm = new CatalogViewModel(services, this);
        await vm.LoadAsync();
        await SwitchAsync(vm, Section.Catalog);
    }

    public async Task OpenPathAsync(string pathId)
    {
        var vm = new PathViewModel(services, this, pathId);
        await vm.LoadAsync();
        await SwitchAsync(vm, Section.Catalog);
    }

    public async Task OpenComparisonAsync(string comparisonId)
    {
        if (services.Content.Current.FindComparison(comparisonId) is not { } comparison) return;
        await SwitchAsync(new ComparisonViewModel(services, this, comparison), Section.Catalog);
    }

    [RelayCommand]
    private Task Catalog() => ShowCatalogAsync();

    /// <summary>The Projects view model is kept for the app's lifetime so open files and terminals survive navigation.</summary>
    [RelayCommand]
    public async Task ShowProjectsAsync()
    {
        if (_projects is null)
        {
            _projects = new ProjectsViewModel(services);
            await _projects.LoadAsync();
        }
        await SwitchAsync(_projects, Section.Projects);
    }

    public Task<bool> ConfirmProjectCloseAsync() => _projects?.Project?.ConfirmCloseAllAsync() ?? Task.FromResult(true);

    /// <summary>Called when the window closes: stops terminals and file watchers.</summary>
    public void Shutdown() => _projects?.Shutdown();

    [RelayCommand]
    public async Task ShowProblemsAsync()
    {
        var vm = new ProblemsViewModel(services, this);
        await vm.LoadAsync();
        await SwitchAsync(vm, Section.Problems);
    }

    [RelayCommand]
    public async Task ShowDashboardAsync()
    {
        var vm = new DashboardViewModel(services, this);
        await vm.LoadAsync();
        await SwitchAsync(vm, Section.Dashboard);
    }

    [RelayCommand]
    public async Task ShowSettingsAsync()
    {
        var vm = new SettingsViewModel(services, this, InitializeAsync);
        await vm.LoadAsync();
        await SwitchAsync(vm, Section.Settings);
    }

    /// <summary>Settings → About; also the target of the macOS application menu's "About" item.</summary>
    public async Task ShowAboutAsync()
    {
        var vm = new SettingsViewModel(services, this, InitializeAsync) { SelectedTab = SettingsViewModel.AboutTab };
        await vm.LoadAsync();
        await SwitchAsync(vm, Section.Settings);
    }

    public async Task ShowAiSettingsAsync()
    {
        var vm = new SettingsViewModel(services, this, InitializeAsync) { SelectedTab = SettingsViewModel.AiTab };
        await vm.LoadAsync();
        await SwitchAsync(vm, Section.Settings);
    }

    public async Task OpenCourseAsync(string courseId)
    {
        var vm = new RoadmapViewModel(services, this, courseId);
        await vm.LoadAsync();
        await SwitchAsync(vm, Section.Catalog);
    }

    public async Task OpenLessonAsync(string lessonId)
    {
        var status = await services.Path.GetLessonStatusAsync(lessonId);
        if (status is null) return;
        if (status.State == LessonState.Locked &&
            !await services.Dialogs.ConfirmAsync(Loc.T("Locked_Title"),
                Loc.F("Locked_Message", string.Join(", ", status.MissingPrerequisiteTitles)), Loc.T("Roadmap_OpenAnyway")))
            return;
        var vm = new LessonWorkspaceViewModel(services, this, status.Lesson);
        await vm.LoadAsync();
        // A lesson opened from the Problems list keeps "Problems" highlighted in the nav.
        await SwitchAsync(vm, Section == Section.Problems ? Section.Problems : Section.Catalog);
    }
}
