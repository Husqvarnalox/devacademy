using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevAcademy.Desktop.Composition;
using DevAcademy.Desktop.Localization;

namespace DevAcademy.Desktop.ViewModels;

public sealed partial class OnboardingViewModel : ObservableObject
{
    private readonly AppServices _s;
    private readonly INavigator _nav;

    public OnboardingViewModel(AppServices s, INavigator nav)
    {
        _s = s;
        _nav = nav;
        Editor = new ProfileEditorViewModel(s.Catalog.Tracks, null);
        Editor.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(ProfileEditorViewModel.SelectedLanguage)) return;
            FinishCommand.NotifyCanExecuteChanged();
            RefreshPaths();
        };
    }

    public ProfileEditorViewModel Editor { get; }

    /// <summary>Paths relevant to the chosen language, followed by the "no route" option.</summary>
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasPaths))] private IReadOnlyList<PathChoiceViewModel> _paths = [];
    public bool HasPaths => Paths.Count > 0;
    public PathChoiceViewModel? SelectedPath => Paths.FirstOrDefault(p => p.IsSelected);

    private void RefreshPaths()
    {
        var lang = Editor.SelectedLanguage?.Track.LanguageId;
        var found = lang is null ? [] : _s.Path.GetPathsForLanguage(lang);
        Paths = found.Count == 0 ? [] :
            found.Select(p => new PathChoiceViewModel(p.Id, p.Title, p.Description, false, PickPath))
                .Append(new PathChoiceViewModel(null, Loc.T("Onboarding_NoPath"), Loc.T("Onboarding_NoPathHint"), true, PickPath)).ToList();
    }

    public void PickPath(PathChoiceViewModel choice)
    {
        foreach (var p in Paths) p.IsSelected = ReferenceEquals(p, choice);
    }

    [RelayCommand(CanExecute = nameof(CanFinish))]
    private async Task FinishAsync()
    {
        await _s.Settings.SaveProfileAsync(Editor.Build());
        if (SelectedPath?.PathId is { } pathId)
        {
            await _s.Path.SetFollowedAsync(pathId, true);
            await _nav.OpenPathAsync(pathId);
        }
        else await _nav.ShowCatalogAsync();
    }

    private bool CanFinish() => Editor.HasLanguage;
}

/// <summary>One onboarding option: a path, or (PathId null) "no route".</summary>
public sealed partial class PathChoiceViewModel(string? pathId, string title, string description, bool selected, Action<PathChoiceViewModel> pick) : ObservableObject
{
    public string? PathId { get; } = pathId;
    public string Title { get; } = title;
    public string Description { get; } = description;
    [ObservableProperty] private bool _isSelected = selected;

    [RelayCommand]
    private void Pick() => pick(this);
}
