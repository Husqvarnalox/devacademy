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
    public string LevelText { get; } = Loc.Has("Level_" + course.Level) ? Loc.T("Level_" + course.Level) : "";
    public string LessonsText { get; } = Loc.F("Catalog_Lessons", lessonCount);
    public string Meta { get; } = (Loc.Has("Level_" + course.Level) ? Loc.T("Level_" + course.Level) + " · " : "") + Loc.F("Catalog_Lessons", lessonCount);
    public int LessonCount { get; } = lessonCount;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasProgress))] private double _fraction;
    [ObservableProperty] private string _progressText = "";
    public bool HasProgress => Fraction > 0;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsCompleted), nameof(IsStarted), nameof(IsNotStarted))] private bool _isDone;
    [ObservableProperty] private string _shortProgressText = "";
    public bool IsCompleted => IsDone;
    public bool IsStarted => HasProgress && !IsDone;
    public bool IsNotStarted => !HasProgress && !IsDone;

    partial void OnFractionChanged(double value)
    {
        OnPropertyChanged(nameof(IsStarted));
        OnPropertyChanged(nameof(IsNotStarted));
    }

    /// <summary>Case-insensitive match on title or description.</summary>
    public bool Matches(string query) =>
        Title.Contains(query, StringComparison.CurrentCultureIgnoreCase) || Description.Contains(query, StringComparison.CurrentCultureIgnoreCase);

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
    public bool IsPlanned => !IsAvailable;
}

public sealed class TrackGroupViewModel(TrackKind kind, IReadOnlyList<TrackCardViewModel> tracks)
{
    public TrackKind Kind { get; } = kind;
    public string Title { get; } = Display.Kind(kind);
    public IReadOnlyList<TrackCardViewModel> Tracks { get; } = tracks;
}

public enum CatalogFilter { All, Started, Completed }

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
        _visibleGroups = Groups;
    }

    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private CatalogFilter _filter = CatalogFilter.All;
    [ObservableProperty] private IReadOnlyList<TrackGroupViewModel> _visibleGroups;

    public bool HasQuery => !string.IsNullOrWhiteSpace(SearchText);
    public bool IsAllFilter => Filter == CatalogFilter.All;
    public bool IsStartedFilter => Filter == CatalogFilter.Started;
    public bool IsCompletedFilter => Filter == CatalogFilter.Completed;
    public bool HasVisible => VisibleGroups.Count > 0;
    /// <summary>Courses exist but the search or status filter hides all of them.</summary>
    public bool ShowNoMatches => !IsEmpty && !HasVisible;
    public string NoMatchesText => HasQuery ? Loc.F("Catalog_NoMatches", SearchText.Trim()) : Loc.T("Catalog_NoFilterMatches");
    public bool ShowFeaturedPath => HasFeaturedPath && !HasQuery;
    public bool ShowComparisons => HasComparisons && !HasQuery;
    public bool ShowPathsExpander => HasPaths && !HasQuery;
    public bool ShowPathsIntroHint => ShowPathsIntro && !HasQuery;

    partial void OnSearchTextChanged(string value) => Refresh();
    partial void OnFilterChanged(CatalogFilter value) => Refresh();

    [RelayCommand] private void SetFilter(CatalogFilter filter) => Filter = filter;
    [RelayCommand] private void ClearSearch() => SearchText = "";

    private void Refresh()
    {
        var query = SearchText?.Trim() ?? "";
        var filtering = query.Length > 0 || Filter != CatalogFilter.All;
        bool Keep(CourseCardViewModel c) =>
            (query.Length == 0 || c.Matches(query)) &&
            Filter switch { CatalogFilter.Started => c.IsStarted, CatalogFilter.Completed => c.IsCompleted, _ => true };
        var result = new List<TrackGroupViewModel>();
        foreach (var g in Groups)
        {
            var tracks = new List<TrackCardViewModel>();
            foreach (var t in g.Tracks)
            {
                if (t.IsPlanned)
                {
                    // Planned content only appears in the unfiltered list (or when its own text matches the query).
                    if (Filter == CatalogFilter.All && (query.Length == 0 ||
                        t.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                        t.Description.Contains(query, StringComparison.CurrentCultureIgnoreCase))) tracks.Add(t);
                    continue;
                }
                if (!filtering) { tracks.Add(t); continue; }
                var courses = t.Courses.Where(Keep).ToList();
                if (courses.Count > 0) tracks.Add(new TrackCardViewModel(t.Track, courses));
            }
            if (tracks.Count > 0) result.Add(new TrackGroupViewModel(g.Kind, tracks));
        }
        VisibleGroups = result;
        OnPropertyChanged(nameof(HasQuery));
        OnPropertyChanged(nameof(IsAllFilter));
        OnPropertyChanged(nameof(IsStartedFilter));
        OnPropertyChanged(nameof(IsCompletedFilter));
        OnPropertyChanged(nameof(HasVisible));
        OnPropertyChanged(nameof(ShowNoMatches));
        OnPropertyChanged(nameof(NoMatchesText));
        NotifyVisibility();
    }

    private void NotifyVisibility()
    {
        OnPropertyChanged(nameof(ShowFeaturedPath));
        OnPropertyChanged(nameof(ShowComparisons));
        OnPropertyChanged(nameof(ShowPathsExpander));
        OnPropertyChanged(nameof(ShowPathsIntroHint));
    }

    public string? ContentWarning { get; }
    public bool HasContentWarning => ContentWarning is not null;
    public bool IsEmpty => Groups.Count == 0;
    public IReadOnlyList<TrackGroupViewModel> Groups { get; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasPaths))] private IReadOnlyList<PathCardViewModel> _paths = [];
    public bool HasPaths => Paths.Count > 0;

    /// <summary>The path the learner follows. Nothing is featured otherwise: no path is recommended without a real reason.</summary>
    public PathCardViewModel? FeaturedPath => Paths.FirstOrDefault(p => p.IsFollowed);
    public bool HasFeaturedPath => FeaturedPath is not null;
    /// <summary>Without a followed path the catalog explains what a path is instead.</summary>
    public bool ShowPathsIntro => !HasFeaturedPath && HasPaths;
    public string AllPathsTitle => Loc.F("Catalog_AllPaths", Paths.Count);

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
        OnPropertyChanged(nameof(FeaturedPath));
        OnPropertyChanged(nameof(HasFeaturedPath));
        OnPropertyChanged(nameof(ShowPathsIntro));
        OnPropertyChanged(nameof(AllPathsTitle));
        foreach (var card in Groups.SelectMany(g => g.Tracks).SelectMany(t => t.Courses))
            if (await _s.Path.GetCoursePathAsync(card.Id) is { Total: > 0 } cp && cp.Completed > 0)
            {
                card.Fraction = (double)cp.Completed / cp.Total;
                card.ProgressText = Loc.F("Catalog_Progress", cp.Completed, cp.Total);
                card.ShortProgressText = Loc.F("Catalog_ProgressShort", cp.Completed, cp.Total);
                card.IsDone = cp.Completed >= cp.Total;
            }
        Comparisons = _s.Content.Current.Comparisons.Select(c => new ComparisonCardViewModel(c, _nav)).ToList();
        OnPropertyChanged(nameof(Comparisons));
        OnPropertyChanged(nameof(HasComparisons));
        NotifyVisibility();
        Refresh();
    }
}
