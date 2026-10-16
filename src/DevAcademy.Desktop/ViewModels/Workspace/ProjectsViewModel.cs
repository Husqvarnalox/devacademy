using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevAcademy.Application.Workspace;
using DevAcademy.Desktop.Composition;
using DevAcademy.Desktop.Localization;
using DevAcademy.Domain;
using DevAcademy.Infrastructure.Workspace;

namespace DevAcademy.Desktop.ViewModels;

/// <summary>The "Projects" screen: a home page (recent projects, templates) that turns into the workspace once a folder is open.</summary>
public sealed partial class ProjectsViewModel(AppServices services) : ObservableObject
{
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasProject), nameof(ShowHome))] private ProjectWorkspaceViewModel? _project;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private ProjectTemplate? _selectedTemplate;

    public ObservableCollection<RecentProject> Recents { get; } = [];
    public IReadOnlyList<ProjectTemplate> Templates => services.Content.Current.Templates;
    public bool HasProject => Project is not null;
    public bool ShowHome => Project is null;
    public bool HasRecents => Recents.Count > 0;

    public async Task LoadAsync()
    {
        Recents.Clear();
        foreach (var r in await services.Workspace.Recents.GetRecentAsync(12)) Recents.Add(r);
        OnPropertyChanged(nameof(HasRecents));
        SelectedTemplate ??= Templates.FirstOrDefault();
    }

    public async Task OpenAsync(string path)
    {
        if (!Directory.Exists(path)) { Error = Loc.F("Ws_FolderMissing", path); return; }
        if (Project is not null && !await CloseProjectAsync()) return;
        Error = null;
        var vm = new ProjectWorkspaceViewModel(services, path);
        vm.CloseHandler = CloseProjectAsync;
        await vm.LoadAsync();
        await services.Workspace.Recents.TouchAsync(vm.Root, services.Workspace.Clock.UtcNow);
        Project = vm;
    }

    [RelayCommand]
    private async Task OpenFolderAsync()
    {
        var path = await services.Files.PickFolderAsync(Loc.T("Ws_PickFolder"));
        if (path is not null) await OpenAsync(path);
    }

    [RelayCommand]
    private Task OpenRecentAsync(RecentProject? recent) => recent is null ? Task.CompletedTask : OpenAsync(recent.Path);

    [RelayCommand]
    private async Task RemoveRecentAsync(RecentProject? recent)
    {
        if (recent is null) return;
        await services.Workspace.Recents.RemoveAsync(recent.Path);
        Recents.Remove(recent);
        OnPropertyChanged(nameof(HasRecents));
    }

    /// <summary>Creates a project from a template in a folder the user picks. Template commands are never run.</summary>
    [RelayCommand]
    private async Task CreateFromTemplateAsync(ProjectTemplate? template)
    {
        template ??= SelectedTemplate;
        if (template is null) return;
        var name = await services.Dialogs.PromptAsync(Loc.F("Ws_NewProjectTitle", template.Title), Loc.T("Ws_ProjectNameHint"), "my-project", Loc.T("Ws_Create"));
        if (name is null) return;
        name = name.Trim();
        if (TemplateInstaller.ValidateProjectName(name) is { } bad) { Error = bad; return; }
        var parent = await services.Files.PickFolderAsync(Loc.T("Ws_PickParentFolder"));
        if (parent is null) return;
        var result = TemplateInstaller.Instantiate(template, parent, name);
        if (!result.Ok) { Error = result.Error; return; }
        await OpenAsync(result.ProjectPath!);
        if (Project is { } p && result.PostCreate is { Count: > 0 } cmds)
        {
            foreach (var c in cmds) p.PostCreateCommands.Add(new PostCreateCommand(c));
            p.NotifyPostCreate();
        }
    }

    /// <summary>Closes the open project after asking about unsaved tabs. False when the user cancelled.</summary>
    [RelayCommand]
    public async Task<bool> CloseProjectAsync()
    {
        if (Project is not { } p) return true;
        if (!await p.ConfirmCloseAllAsync()) return false;
        await p.SaveSessionAsync();
        p.Dispose();
        Project = null;
        await LoadAsync();
        return true;
    }

    public bool HasUnsavedChanges => Project?.HasUnsavedChanges == true;

    public Task SaveSessionAsync() => Project?.SaveSessionAsync() ?? Task.CompletedTask;

    public void Shutdown() => Project?.Dispose();
}
