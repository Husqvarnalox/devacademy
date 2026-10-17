using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevAcademy.Application.Services;
using DevAcademy.Desktop.Composition;
using DevAcademy.Desktop.Localization;
using DevAcademy.Domain;

namespace DevAcademy.Desktop.ViewModels;

public sealed record CourseRowViewModel(string Title, string CountText, string? CreditedText, double Fraction)
{
    public string LastActivityText { get; init; } = "";
    public IAsyncRelayCommand? OpenCommand { get; init; }
}

public sealed record DifficultyRowViewModel(string Label, int Count, double Fraction = 0);
public sealed record DayBarViewModel(double Height, string Tooltip, bool HasValue);

public sealed partial class RecommendationViewModel(Recommendation r, INavigator nav) : ObservableObject
{
    public string Title { get; } = r.Title;
    public string KindText { get; } = Loc.T("Rec_" + r.Kind);
    public string Reason { get; } = Loc.T("RecReason_" + r.Kind);

    [RelayCommand]
    private Task OpenAsync() => nav.OpenLessonAsync(r.LessonId);
}

public sealed partial class DashboardViewModel(AppServices s, INavigator nav) : ObservableObject
{
    private const double MaxBarHeight = 80;

    [ObservableProperty] private IReadOnlyList<CourseRowViewModel> _courses = [];
    [ObservableProperty] private IReadOnlyList<DifficultyRowViewModel> _solved = [];
    [ObservableProperty] private IReadOnlyList<DayBarViewModel> _days = [];
    [ObservableProperty] private int _dueReviews;
    [ObservableProperty] private int _totalSubmissions;
    [ObservableProperty] private IReadOnlyList<RecommendationViewModel> _recommendations = [];

    [ObservableProperty] private string _heroOverline = "";
    [ObservableProperty] private string _heroTitle = "";
    [ObservableProperty] private string _heroText = "";
    [ObservableProperty] private string _heroPrimaryText = "";
    [ObservableProperty] private IAsyncRelayCommand? _heroPrimaryCommand;
    [ObservableProperty] private IAsyncRelayCommand? _heroCourseCommand;
    [ObservableProperty] private IReadOnlyList<RecommendationViewModel> _reviews = [];
    [ObservableProperty] private IReadOnlyList<RecommendationViewModel> _upNext = [];

    public bool HasCourses => Courses.Count > 0;
    public bool HasActivity => TotalSubmissions > 0;
    public bool HasSolved => Solved.Any(d => d.Count > 0);
    public bool HasReviews => Reviews.Count > 0;
    public bool HasUpNext => UpNext.Count > 0;
    public bool HasHeroCourse => HeroCourseCommand is not null;
    public bool HasRecommendations => Recommendations.Count > 0;

    private static string ActivityText(DateTimeOffset? last)
    {
        if (last is null) return Loc.T("Dashboard_NoActivity");
        var days = (int)(DateTimeOffset.UtcNow.Date - last.Value.UtcDateTime.Date).TotalDays;
        return days <= 0 ? Loc.T("Dashboard_Today") : days == 1 ? Loc.T("Dashboard_Yesterday")
            : days < 14 ? Loc.F("Dashboard_DaysAgo", days) : last.Value.ToLocalTime().ToString("dd.MM.yyyy");
    }

    public async Task LoadAsync()
    {
        var stats = await s.Progress.GetDashboardAsync();
        var profile = await s.Settings.GetProfileAsync();
        var enrolled = profile?.EnrolledCourseIds.ToHashSet(StringComparer.Ordinal) ?? [];

        var rows = new List<CourseRowViewModel>();
        foreach (var c in stats.Courses.Where(c => enrolled.Contains(c.CourseId) || c.Completed > 0))
        {
            var path = await s.Path.GetCoursePathAsync(c.CourseId);
            var stamps = (path?.Lessons ?? []).Select(l => l.Progress).Where(p => p is not null)
                .SelectMany(p => new[] { p!.StartedAt, p.CompletedAt, p.LastReviewedAt }).Where(t => t is not null).Select(t => t!.Value).ToList();
            var courseId = c.CourseId;
            rows.Add(new CourseRowViewModel(c.Title, Loc.F("Dashboard_CourseCount", c.Completed, c.Total),
                c.CreditedByAssessment > 0 ? Loc.F("Dashboard_Credited", c.CreditedByAssessment) : null,
                c.Total == 0 ? 0 : (double)c.Completed / c.Total)
            {
                LastActivityText = ActivityText(stamps.Count == 0 ? null : stamps.Max()),
                OpenCommand = new AsyncRelayCommand(() => nav.OpenCourseAsync(courseId)),
            });
        }
        Courses = rows;
        var solvedMax = Math.Max(1, Enum.GetValues<Difficulty>().Max(d => stats.SolvedExercisesByDifficulty.GetValueOrDefault(d)));
        Solved = Enum.GetValues<Difficulty>().Select(d => new DifficultyRowViewModel(Display.Difficulty(d),
            stats.SolvedExercisesByDifficulty.GetValueOrDefault(d), (double)stats.SolvedExercisesByDifficulty.GetValueOrDefault(d) / solvedMax)).ToList();
        var max = Math.Max(1, stats.SubmissionsPerDay.Max(d => d.Submissions));
        Days = stats.SubmissionsPerDay
            .Select(d => new DayBarViewModel(d.Submissions == 0 ? 2 : Math.Max(4, MaxBarHeight * d.Submissions / max),
                Loc.F("Dashboard_DayTip", d.Day.ToString("dd.MM"), d.Submissions), d.Submissions > 0))
            .ToList();
        TotalSubmissions = stats.SubmissionsPerDay.Sum(d => d.Submissions);
        DueReviews = stats.DueReviews;
        Recommendations = (await s.Path.GetRecommendationsAsync(profile)).Select(r => new RecommendationViewModel(r, nav)).ToList();
        var recs = await s.Path.GetRecommendationsAsync(profile);
        Reviews = recs.Where(r => r.Kind == RecommendationKind.Review).Select(r => new RecommendationViewModel(r, nav)).ToList();
        var forward = recs.Where(r => r.Kind != RecommendationKind.Review).ToList();
        UpNext = forward.Skip(1).Select(r => new RecommendationViewModel(r, nav)).ToList();
        var started = stats.Courses.Any(c => c.Completed > 0);
        Recommendation? hero = forward.FirstOrDefault();
        if (hero is null || (!started && hero.Kind != RecommendationKind.Continue))
        {
            // Nothing started: the learner's followed path is a better first step than a generic lesson.
            foreach (var pathId in await s.Path.GetFollowedPathIdsAsync())
                if (await s.Path.GetPathNextStepAsync(pathId, profile) is { } step) { hero = step; break; }
        }
        if (hero is null)
        {
            HeroOverline = Loc.T("Dashboard_Start");
            HeroTitle = Loc.T("Dashboard_StartTitle");
            HeroText = Loc.T("Dashboard_StartText");
            HeroPrimaryText = Loc.T("Dashboard_OpenCatalog");
            HeroPrimaryCommand = new AsyncRelayCommand(nav.ShowCatalogAsync);
            HeroCourseCommand = null;
        }
        else
        {
            var lesson = s.Content.Current.FindLesson(hero.LessonId);
            var course = lesson is null ? null : s.Content.Current.FindCourse(lesson.CourseId);
            HeroOverline = course?.Title ?? Loc.T("Dashboard_Continue");
            HeroTitle = hero.Title;
            HeroText = string.IsNullOrWhiteSpace(lesson?.Goal) ? Loc.T("RecReason_" + hero.Kind) : lesson!.Goal;
            HeroPrimaryText = Loc.T("Dashboard_ContinueLesson");
            HeroPrimaryCommand = new AsyncRelayCommand(() => nav.OpenLessonAsync(hero.LessonId));
            HeroCourseCommand = course is null ? null : new AsyncRelayCommand(() => nav.OpenCourseAsync(course.Id));
        }
        OnPropertyChanged(nameof(HasActivity));
        OnPropertyChanged(nameof(HasSolved));
        OnPropertyChanged(nameof(HasReviews));
        OnPropertyChanged(nameof(HasUpNext));
        OnPropertyChanged(nameof(HasHeroCourse));
        OnPropertyChanged(nameof(HasCourses));
        OnPropertyChanged(nameof(HasRecommendations));
    }
}
