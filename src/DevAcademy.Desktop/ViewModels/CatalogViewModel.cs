using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevAcademy.Desktop.Composition;
using DevAcademy.Desktop.Localization;
using DevAcademy.Domain;

namespace DevAcademy.Desktop.ViewModels;

public sealed partial class CourseCardViewModel(Course course, int lessonCount, INavigator nav) : ObservableObject
{
    public string Id { get; } = course.Id;
    public string Title { get; } = course.Title;
    public string Description { get; } = course.Description;
    public string Meta { get; } = Loc.F("Catalog_CourseMeta", lessonCount, course.Version);

    [RelayCommand]
    private Task OpenAsync() => nav.OpenCourseAsync(Id);
}

public sealed class TrackCardViewModel
{
    public TrackCardViewModel(Track track, IReadOnlyList<CourseCardViewModel> courses)
    {
        Track = track;
        Courses = courses;
    }

    public Track Track { get; }
    public string Title => Track.Title;
    public string Description => Track.Description;
    public bool IsAvailable => Track.Status == ContentStatus.Available;
    public string StatusText => Loc.T(IsAvailable ? "Status_Available" : "Status_Planned");
    public IReadOnlyList<CourseCardViewModel> Courses { get; }
    public bool HasCourses => IsAvailable && Courses.Count > 0;
}

public sealed class TrackGroupViewModel(TrackKind kind, IReadOnlyList<TrackCardViewModel> tracks)
{
    public string Title { get; } = Display.Kind(kind);
    public IReadOnlyList<TrackCardViewModel> Tracks { get; } = tracks;
}

public sealed partial class CatalogViewModel : ObservableObject
{
    private readonly AppServices _s;
    private readonly INavigator _nav;

    public CatalogViewModel(AppServices s, INavigator nav)
    {
        _s = s;
        _nav = nav;
        var errors = s.Content.Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error);
        ContentWarning = errors > 0 ? Loc.F("Catalog_ContentErrors", errors) : null;
        var order = new[] { TrackKind.Language, TrackKind.Framework, TrackKind.Algorithms, TrackKind.ComputerScience, TrackKind.MachineLearning };
        Groups = order
            .Select(kind => new TrackGroupViewModel(kind, s.Catalog.Tracks.Where(t => t.Kind == kind).Select(t =>
                new TrackCardViewModel(t, t.Status == ContentStatus.Available
                    ? s.Catalog.GetCourses(t.Id).Select(c => new CourseCardViewModel(c, s.Catalog.GetLessons(c.Id).Count, nav)).ToList()
                    : [])).ToList()))
            .Where(g => g.Tracks.Count > 0)
            .ToList();
    }

    public string? ContentWarning { get; }
    public bool HasContentWarning => ContentWarning is not null;
    public bool IsEmpty => Groups.Count == 0;
    public IReadOnlyList<TrackGroupViewModel> Groups { get; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasPaths))] private IReadOnlyList<PathCardViewModel> _paths = [];
    public bool HasPaths => Paths.Count > 0;

    public IReadOnlyList<ComparisonCardViewModel> Comparisons { get; private set; } = [];
    public bool HasComparisons => Comparisons.Count > 0;

    /// <summary>Paths need progress to describe, so they are loaded after construction.</summary>
    public async Task LoadAsync()
    {
        var followed = await _s.Path.GetFollowedPathIdsAsync();
        var cards = new List<PathCardViewModel>();
        foreach (var p in _s.Content.Current.Paths)
            if (await _s.Path.BuildPathAsync(p.Id) is { } plan) cards.Add(new PathCardViewModel(_s, _nav, plan, followed.Contains(p.Id)));
        Paths = cards;
        Comparisons = _s.Content.Current.Comparisons.Select(c => new ComparisonCardViewModel(c, _nav)).ToList();
        OnPropertyChanged(nameof(Comparisons));
        OnPropertyChanged(nameof(HasComparisons));
    }
}
