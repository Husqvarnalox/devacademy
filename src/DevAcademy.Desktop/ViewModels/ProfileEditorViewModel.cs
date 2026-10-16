using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DevAcademy.Desktop.Localization;
using DevAcademy.Domain;

namespace DevAcademy.Desktop.ViewModels;

public abstract record ChoiceBase(string Label);

public sealed record Choice<T>(T Value, string Label) : ChoiceBase(Label);

public sealed partial class TrackChoiceViewModel(Track track, Action<TrackChoiceViewModel> pick) : ObservableObject
{
    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void Pick() => pick(this);

    public Track Track { get; } = track;
    public string Title => Track.Title;
    public string Description => Track.Description;
    public bool IsAvailable => Track.Status == ContentStatus.Available;
    public string StatusText => IsAvailable ? "" : Loc.T("Status_Planned");
    [ObservableProperty] private bool _isSelected;
}

/// <summary>Language / goal / level / depth selection shared by Onboarding and Settings.</summary>
public sealed partial class ProfileEditorViewModel : ObservableObject
{
    public static readonly string[] GoalIds = ["career", "study", "hobby", "interview"];

    public ProfileEditorViewModel(IEnumerable<Track> tracks, LearnerProfile? profile)
    {
        Languages = new ObservableCollection<TrackChoiceViewModel>(
            tracks.Where(t => t.Kind == TrackKind.Language).Select(t => new TrackChoiceViewModel(t, PickLanguage)));
        Goals = GoalIds.Select(g => new Choice<string>(g, Loc.T("Goal_" + g))).ToList();
        Levels = Enumerable.Range(0, 4).Select(l => new Choice<int>(l, Loc.T("Level_" + l))).ToList();
        Depths = Enum.GetValues<LearningDepth>().Select(d => new Choice<LearningDepth>(d, Loc.T("Depth_" + d))).ToList();

        SelectedGoal = Goals.FirstOrDefault(g => g.Value == profile?.Goal) ?? Goals[0];
        SelectedLevel = Levels.FirstOrDefault(l => l.Value == profile?.Level) ?? Levels[0];
        SelectedDepth = Depths.FirstOrDefault(d => d.Value == profile?.Depth) ?? Depths.First(d => d.Value == LearningDepth.Standard);
        Existing = profile;
        if (profile is not null)
            SelectLanguage(Languages.FirstOrDefault(l => l.IsAvailable && l.Track.CourseIds.Any(profile.EnrolledCourseIds.Contains)));
    }

    public LearnerProfile? Existing { get; }
    public ObservableCollection<TrackChoiceViewModel> Languages { get; }
    public IReadOnlyList<Choice<string>> Goals { get; }
    public IReadOnlyList<Choice<int>> Levels { get; }
    public IReadOnlyList<Choice<LearningDepth>> Depths { get; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasLanguage))] private TrackChoiceViewModel? _selectedLanguage;
    [ObservableProperty] private Choice<string> _selectedGoal = null!;
    [ObservableProperty] private Choice<int> _selectedLevel = null!;
    [ObservableProperty] private Choice<LearningDepth> _selectedDepth = null!;

    public bool HasLanguage => SelectedLanguage is not null;

    private void PickLanguage(TrackChoiceViewModel choice) => SelectLanguage(choice);

    public void SelectLanguage(TrackChoiceViewModel? choice)
    {
        if (choice is { IsAvailable: false }) return;
        foreach (var l in Languages) l.IsSelected = ReferenceEquals(l, choice);
        SelectedLanguage = choice;
    }

    public LearnerProfile Build()
    {
        var enrolled = SelectedLanguage is { } l
            ? (Existing is { } e && l.Track.CourseIds.Any(e.EnrolledCourseIds.Contains) ? e.EnrolledCourseIds : l.Track.CourseIds)
            : Existing?.EnrolledCourseIds ?? [];
        return new LearnerProfile(SelectedGoal.Value, SelectedLevel.Value, SelectedDepth.Value, enrolled, "ru");
    }
}
