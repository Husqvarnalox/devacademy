using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevAcademy.Application.Services;
using DevAcademy.Desktop.Composition;
using DevAcademy.Desktop.Localization;
using DevAcademy.Domain;

namespace DevAcademy.Desktop.ViewModels;

public sealed record CourseRowViewModel(string Title, string CountText, string? CreditedText, double Fraction);
public sealed record DifficultyRowViewModel(string Label, int Count);
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

    public bool HasCourses => Courses.Count > 0;
    public bool HasRecommendations => Recommendations.Count > 0;

    public async Task LoadAsync()
    {
        var stats = await s.Progress.GetDashboardAsync();
        var profile = await s.Settings.GetProfileAsync();
        var enrolled = profile?.EnrolledCourseIds.ToHashSet(StringComparer.Ordinal) ?? [];

        Courses = stats.Courses
            .Where(c => enrolled.Contains(c.CourseId) || c.Completed > 0)
            .Select(c => new CourseRowViewModel(c.Title, Loc.F("Dashboard_CourseCount", c.Completed, c.Total),
                c.CreditedByAssessment > 0 ? Loc.F("Dashboard_Credited", c.CreditedByAssessment) : null,
                c.Total == 0 ? 0 : (double)c.Completed / c.Total))
            .ToList();
        Solved = Enum.GetValues<Difficulty>().Select(d => new DifficultyRowViewModel(Display.Difficulty(d), stats.SolvedExercisesByDifficulty.GetValueOrDefault(d))).ToList();
        var max = Math.Max(1, stats.SubmissionsPerDay.Max(d => d.Submissions));
        Days = stats.SubmissionsPerDay
            .Select(d => new DayBarViewModel(d.Submissions == 0 ? 2 : Math.Max(4, MaxBarHeight * d.Submissions / max),
                Loc.F("Dashboard_DayTip", d.Day.ToString("dd.MM"), d.Submissions), d.Submissions > 0))
            .ToList();
        TotalSubmissions = stats.SubmissionsPerDay.Sum(d => d.Submissions);
        DueReviews = stats.DueReviews;
        Recommendations = (await s.Path.GetRecommendationsAsync(profile)).Select(r => new RecommendationViewModel(r, nav)).ToList();
        OnPropertyChanged(nameof(HasCourses));
        OnPropertyChanged(nameof(HasRecommendations));
    }
}
