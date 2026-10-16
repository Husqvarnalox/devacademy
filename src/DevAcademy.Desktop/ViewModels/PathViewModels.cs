using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevAcademy.Application.Services;
using DevAcademy.Desktop.Composition;
using DevAcademy.Desktop.Controls;
using DevAcademy.Desktop.Localization;
using DevAcademy.Domain;

namespace DevAcademy.Desktop.ViewModels;

public static class PathDisplay
{
    public static string Goal(string goal) => Loc.Has("PathGoal_" + goal) ? Loc.T("PathGoal_" + goal) : goal;
}

/// <summary>A path in a list (catalog, dashboard): what it is, how far the learner is, and whether it is followed.</summary>
public sealed partial class PathCardViewModel : ObservableObject
{
    private readonly AppServices _s;
    private readonly INavigator _nav;

    public PathCardViewModel(AppServices s, INavigator nav, PathPlan plan, bool followed)
    {
        _s = s;
        _nav = nav;
        Id = plan.Path.Id;
        Title = plan.Path.Title;
        Description = plan.Path.Description;
        GoalText = Loc.F("Path_Goal", PathDisplay.Goal(plan.Path.Goal));
        Meta = Loc.F("Catalog_PathMeta", plan.Courses.Count, plan.Items.Count, plan.SatisfiedCount);
        _isFollowed = followed;
    }

    public string Id { get; }
    public string Title { get; }
    public string Description { get; }
    public string GoalText { get; }
    public string Meta { get; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(FollowText))] private bool _isFollowed;

    public string FollowText => Loc.T(IsFollowed ? "Path_Unfollow" : "Path_Follow");

    [RelayCommand]
    private Task OpenAsync() => _nav.OpenPathAsync(Id);

    [RelayCommand]
    private async Task ToggleFollowAsync()
    {
        await _s.Path.SetFollowedAsync(Id, !IsFollowed);
        IsFollowed = !IsFollowed;
    }
}

public sealed partial class PathLessonViewModel(PathItem item, INavigator nav) : ObservableObject
{
    public string LessonId { get; } = item.Lesson.Id;
    public string Title { get; } = item.Lesson.Title;
    public LessonState State { get; } = item.State;
    public string StateText { get; } = Display.State(item.State);
    public bool IsCompleted { get; } = item.Satisfied;

    /// <summary>"Credited by placement check" or "already known" (done in a prerequisite course or shared with another path); null otherwise.</summary>
    public string? KnownText { get; } = item.CreditedByAssessment ? Loc.T("Path_LessonCredited") : item.AlreadyKnown ? Loc.T("Path_LessonKnown") : null;
    public bool HasKnownText => KnownText is not null;

    public string? SharedText { get; } = item.SharedWithPaths.Count > 0 ? Loc.F("Path_LessonShared", string.Join(", ", item.SharedWithPaths)) : null;
    public bool HasSharedText => SharedText is not null;

    [RelayCommand]
    private Task OpenAsync() => nav.OpenLessonAsync(LessonId);
}

/// <summary>An inline placement check: a quiz whose success credits lessons. Failing records the attempt and credits nothing.</summary>
public sealed partial class PlacementViewModel : ObservableObject
{
    private readonly Func<Task> _changed;

    public PlacementViewModel(AppServices s, PlacementOffer offer, Func<Task> changed)
    {
        _changed = changed;
        Offer = offer;
        Title = Loc.F("Placement_Title", offer.Assessment.Title);
        Quiz = new QuizViewModel(s, offer.Assessment.Id, offer.Assessment.Quiz!);
        Quiz.Attempted += OnAttempted;
    }

    public PlacementOffer Offer { get; }
    public string Title { get; }
    public QuizViewModel Quiz { get; }

    [ObservableProperty] private string? _outcome;
    [ObservableProperty] private bool _credited;

    public event Action? Closed;

    private async void OnAttempted(ProgressUpdate update)
    {
        var count = update.CreditedLessonIds?.Count ?? 0;
        Credited = count > 0;
        Outcome = Quiz.Passed
            ? Loc.F(count > 0 ? "Placement_Passed" : "Placement_PassedNothing", count)
            : Loc.T("Placement_Failed");
        try { await _changed(); }
        catch (Exception ex) when (ex is not OperationCanceledException) { Outcome = Loc.F("Error_Quiz", ex.Message); }
    }

    [RelayCommand]
    private void Close() => Closed?.Invoke();
}

public sealed partial class PathCourseViewModel : ObservableObject
{
    private readonly INavigator _nav;
    private readonly Action<PlacementOffer> _startPlacement;
    private readonly PlacementOffer? _offer;

    public PathCourseViewModel(PlanCourse plan, PlacementOffer? offer, INavigator nav, Action<PlacementOffer> startPlacement)
    {
        _nav = nav;
        _offer = offer;
        _startPlacement = startPlacement;
        CourseId = plan.Course.Id;
        Title = plan.Course.Title;
        IsAlternative = plan.IsAlternative;
        IsSelected = plan.IsSelected;
        ProgressText = Loc.F("Path_CourseProgress", plan.Completed, plan.Total);
        CreditedText = plan.Credited > 0 ? Loc.F("Path_CourseCredited", plan.Credited) : null;
        Lessons = plan.Items.Select(i => new PathLessonViewModel(i, nav)).ToList();
        if (offer is not null && plan.IsSelected)
            PlacementText = Loc.F("Placement_Offer", offer.Assessment.Title, offer.QuestionCount, (int)Math.Round(offer.PassThreshold * 100), offer.CreditedLessons.Count);
    }

    public string CourseId { get; }
    public string Title { get; }
    public bool IsAlternative { get; }
    public bool IsSelected { get; }
    public bool IsNotChosen => IsAlternative && !IsSelected;
    public string ProgressText { get; }
    public string? CreditedText { get; }
    public bool HasCredited => CreditedText is not null;
    public IReadOnlyList<PathLessonViewModel> Lessons { get; }

    /// <summary>Null (button hidden) unless the course contains a placement quiz that would still credit something.</summary>
    public string? PlacementText { get; }
    public bool HasPlacement => PlacementText is not null;

    [RelayCommand]
    private Task OpenCourseAsync() => _nav.OpenCourseAsync(CourseId);

    [RelayCommand]
    private void TakePlacement()
    {
        if (_offer is not null) _startPlacement(_offer);
    }
}

public sealed record PathStageViewModel(string Title, bool IsPrerequisites, IReadOnlyList<PathCourseViewModel> Courses);

public sealed partial class AlternativeChoiceViewModel : ObservableObject
{
    public AlternativeChoiceViewModel(string courseId, string title, bool selected)
    {
        CourseId = courseId;
        Title = title;
        _isSelected = selected;
    }

    public string CourseId { get; }
    public string Title { get; }
    [ObservableProperty] private bool _isSelected;
}

public sealed partial class AlternativeGroupViewModel(PathViewModel owner, AlternativeGroup group) : ObservableObject
{
    public int Index { get; } = group.Index;
    public string Hint { get; } = group.IsChosen
        ? Loc.T("Path_AlternativesHint")
        : Loc.F("Path_AlternativesDefault", group.Courses.First(c => c.Id == group.SelectedCourseId).Title);
    public IReadOnlyList<AlternativeChoiceViewModel> Choices { get; } =
        group.Courses.Select(c => new AlternativeChoiceViewModel(c.Id, c.Title, c.Id == group.SelectedCourseId)).ToList();

    [RelayCommand]
    private Task ChooseAsync(AlternativeChoiceViewModel choice) => owner.ChooseAlternativeAsync(Index, choice.CourseId);
}

public sealed record OtherPathViewModel(string Title, IAsyncRelayCommand OpenCommand);

public sealed partial class PathViewModel(AppServices s, INavigator nav, string pathId) : ObservableObject
{
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _description = "";
    [ObservableProperty] private string _goalText = "";
    [ObservableProperty] private string _progressText = "";
    [ObservableProperty] private string? _creditedText;
    [ObservableProperty] private string? _knownText;
    [ObservableProperty] private IReadOnlyList<PathStageViewModel> _stages = [];
    [ObservableProperty] private IReadOnlyList<AlternativeGroupViewModel> _alternatives = [];
    [ObservableProperty] private IReadOnlyList<OtherPathViewModel> _otherPaths = [];
    [ObservableProperty] private CourseGraphModel _graph = CourseGraphModel.Empty;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(FollowText))] private bool _isFollowed;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasNextStep), nameof(NextStepTitle), nameof(NextStepReason), nameof(IsFinished))] private Recommendation? _nextStep;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasPlacementCheck))] private PlacementViewModel? _placementCheck;
    [ObservableProperty] private bool _notFound;

    public string PathId { get; } = pathId;
    public bool HasCredited => CreditedText is not null;
    public bool HasKnown => KnownText is not null;
    public bool HasAlternatives => Alternatives.Count > 0;
    public bool HasOtherPaths => OtherPaths.Count > 0;
    public bool HasGraph => Graph.NodeCount > 1;
    public bool HasNextStep => NextStep is not null;
    public bool IsFinished => NextStep is null && !NotFound;
    public string NextStepTitle => NextStep?.Title ?? "";
    public string NextStepReason => NextStep is null ? "" : Loc.T("RecReason_" + NextStep.Kind);
    public string FollowText => Loc.T(IsFollowed ? "Path_Unfollow" : "Path_Follow");
    public bool HasPlacementCheck => PlacementCheck is not null;

    public async Task LoadAsync()
    {
        var plan = await s.Path.BuildPathAsync(PathId);
        if (plan is null) { NotFound = true; return; }
        var profile = await s.Settings.GetProfileAsync();
        var followed = await s.Path.GetFollowedPathIdsAsync();

        Title = plan.Path.Title;
        Description = plan.Path.Description;
        GoalText = Loc.F("Path_Goal", PathDisplay.Goal(plan.Path.Goal));
        ProgressText = Loc.F("Path_Progress", plan.SatisfiedCount, plan.Items.Count);
        CreditedText = plan.CreditedCount > 0 ? Loc.F("Path_Credited", plan.CreditedCount) : null;
        KnownText = plan.AlreadyKnownCount > 0 ? Loc.F("Path_Known", plan.AlreadyKnownCount) : null;
        IsFollowed = followed.Contains(PathId);
        NextStep = await s.Path.GetPathNextStepAsync(PathId, profile);

        var offers = new Dictionary<string, PlacementOffer>(StringComparer.Ordinal);
        foreach (var course in plan.Courses)
            if ((await s.Path.GetCoursePlacementOffersAsync(course.Id)).FirstOrDefault() is { } offer) offers[course.Id] = offer;

        PathCourseViewModel Course(PlanCourse c) =>
            new(c, offers.GetValueOrDefault(c.Course.Id), nav, StartPlacement);

        var number = 0;
        Stages = plan.Stages.Select(st => new PathStageViewModel(
            st.Title ?? Loc.T(st.IsPrerequisites ? "Path_StagePrerequisites" : "Path_StageN").Replace("{0}", (++number).ToString()),
            st.IsPrerequisites, st.Courses.Select(Course).ToList())).ToList();
        Alternatives = plan.Alternatives.Where(g => g.Courses.Count > 1).Select(g => new AlternativeGroupViewModel(this, g)).ToList();

        var byId = plan.Courses.ToDictionary(c => c.Id, StringComparer.Ordinal);
        PlanCourse? Find(string id) => plan.Stages.SelectMany(st => st.Courses).FirstOrDefault(c => c.Course.Id == id && c.IsSelected);
        Graph = new CourseGraphModel(
            plan.Graph.Layers.Select(layer => (IReadOnlyList<GraphNode>)layer.Select(c =>
            {
                var pc = Find(c.Id);
                var total = pc?.Total ?? 0;
                var done = pc?.Completed ?? 0;
                return new GraphNode(c.Id, c.Title, Loc.F("Path_CourseProgress", done, total), total == 0 ? 0 : (double)done / total);
            }).ToList()).ToList(),
            plan.Graph.Edges.Where(e => byId.ContainsKey(e.FromCourseId) && byId.ContainsKey(e.ToCourseId)).Select(e => new GraphLink(e.FromCourseId, e.ToCourseId)).ToList());

        OtherPaths = followed.Where(id => id != PathId && s.Content.Current.FindPath(id) is not null)
            .Select(id => new OtherPathViewModel(s.Content.Current.FindPath(id)!.Title, new AsyncRelayCommand(() => nav.OpenPathAsync(id)))).ToList();

        OnPropertyChanged(nameof(HasCredited));
        OnPropertyChanged(nameof(HasKnown));
        OnPropertyChanged(nameof(HasAlternatives));
        OnPropertyChanged(nameof(HasOtherPaths));
        OnPropertyChanged(nameof(HasGraph));
    }

    private void StartPlacement(PlacementOffer offer)
    {
        var check = new PlacementViewModel(s, offer, LoadAsync);
        check.Closed += () => PlacementCheck = null;
        PlacementCheck = check;
    }

    public async Task ChooseAlternativeAsync(int groupIndex, string courseId)
    {
        await s.Path.SelectAlternativeAsync(PathId, groupIndex, courseId);
        await LoadAsync();
    }

    [RelayCommand]
    private async Task ToggleFollowAsync()
    {
        await s.Path.SetFollowedAsync(PathId, !IsFollowed);
        await LoadAsync();
    }

    [RelayCommand]
    private Task OpenNextAsync() => NextStep is null ? Task.CompletedTask : nav.OpenLessonAsync(NextStep.LessonId);

    [RelayCommand]
    private Task OpenCourseFromGraphAsync(string courseId) => nav.OpenCourseAsync(courseId);

    [RelayCommand]
    private Task BackAsync() => nav.ShowCatalogAsync();
}
