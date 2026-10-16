using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevAcademy.Application.Services;
using DevAcademy.Desktop.Composition;
using DevAcademy.Desktop.Localization;
using DevAcademy.Domain;

namespace DevAcademy.Desktop.ViewModels;

public sealed partial class RoadmapLessonViewModel(int number, LessonStatus status, INavigator nav) : ObservableObject
{
    public string LessonId { get; } = status.Lesson.Id;
    public int Number { get; } = number;
    public string Title { get; } = status.Lesson.Title;
    public string Goal { get; } = status.Lesson.Goal;
    public LessonState State { get; } = status.State;
    public string StateText { get; } = Display.State(status.State);
    public string Meta { get; } = Loc.F("Lesson_Meta", Display.Difficulty(status.Lesson.Difficulty), status.Lesson.EstimatedMinutes);
    public bool IsLocked { get; } = status.State == LessonState.Locked;
    public bool IsCompleted { get; } = status.State == LessonState.Completed;
    public string? SourceText { get; } = status.State == LessonState.Completed ? Loc.F("Lesson_CompletedBy", Display.Source(status.Progress?.CompletionSource)) : null;
    public string? WhyLocked { get; } = status.State == LessonState.Locked
        ? Loc.F("Roadmap_WhyLocked", string.Join(", ", status.MissingPrerequisiteTitles)) : null;
    public string OpenText { get; } = Loc.T(status.State switch
    {
        LessonState.Locked => "Roadmap_OpenAnyway",
        LessonState.Completed => "Roadmap_Review",
        LessonState.InProgress => "Roadmap_Continue",
        _ => "Roadmap_Start",
    });

    [RelayCommand]
    private Task OpenAsync() => nav.OpenLessonAsync(LessonId);
}

public sealed record DeprecatedRowViewModel(string Feature, string Status, string Note);

public sealed class FrameworkInfoViewModel
{
    public FrameworkInfoViewModel(Course course, FrameworkInfo f)
    {
        Title = f.Version is null ? f.Id : $"{f.Id} {f.Version}";
        CurriculumText = Loc.F("Framework_Curriculum", course.Version);
        Compatibility = f.Compatibility;
        Deprecated = f.Deprecated.Select(d => new DeprecatedRowViewModel(d.Feature, d.Status, d.Note)).ToList();
        MigrationNotes = f.MigrationNotes;
        Docs = f.Docs;
    }

    public string Title { get; }
    public string CurriculumText { get; }
    public string? Compatibility { get; }
    public bool HasCompatibility => !string.IsNullOrWhiteSpace(Compatibility);
    public IReadOnlyList<DeprecatedRowViewModel> Deprecated { get; }
    public bool HasDeprecated => Deprecated.Count > 0;
    public string? MigrationNotes { get; }
    public bool HasMigrationNotes => !string.IsNullOrWhiteSpace(MigrationNotes);
    public IReadOnlyList<string> Docs { get; }
    public bool HasDocs => Docs.Count > 0;
}

/// <summary>A "skip if you already know it" row on a course page; exists only when the course has a placement quiz.</summary>
public sealed partial class PlacementRowViewModel(PlacementOffer offer, Action<PlacementOffer> start) : ObservableObject
{
    public string Text { get; } = Loc.F("Placement_Offer", offer.Assessment.Title, offer.QuestionCount, (int)Math.Round(offer.PassThreshold * 100), offer.CreditedLessons.Count);

    [RelayCommand]
    private void Take() => start(offer);
}

public sealed partial class RoadmapViewModel(AppServices s, INavigator nav, string courseId) : ObservableObject
{
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _description = "";
    [ObservableProperty] private string _progressText = "";
    [ObservableProperty] private IReadOnlyList<RoadmapLessonViewModel> _lessons = [];

    public string CourseId { get; } = courseId;

    /// <summary>Framework/library metadata block; null (nothing rendered) when course.yaml declares no framework.</summary>
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasFramework))] private FrameworkInfoViewModel? _framework;
    public bool HasFramework => Framework is not null;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasPlacement))] private IReadOnlyList<PlacementRowViewModel> _placements = [];
    public bool HasPlacement => Placements.Count > 0;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasPlacementCheck))] private PlacementViewModel? _placementCheck;
    public bool HasPlacementCheck => PlacementCheck is not null;

    [RelayCommand]
    private Task OpenLinkAsync(string url) => s.Launcher.OpenUrlAsync(url);

    public async Task LoadAsync()
    {
        var path = await s.Path.GetCoursePathAsync(CourseId);
        if (path is null) return;
        Title = path.Course.Title;
        Description = path.Course.Description;
        ProgressText = Loc.F("Roadmap_Progress", path.Completed, path.Total);
        Lessons = path.Lessons.Select((l, i) => new RoadmapLessonViewModel(i + 1, l, nav)).ToList();
        Framework = path.Course.Framework is { } f ? new FrameworkInfoViewModel(path.Course, f) : null;
        Placements = (await s.Path.GetCoursePlacementOffersAsync(CourseId))
            .Select(o => new PlacementRowViewModel(o, StartPlacement)).ToList();
    }

    private void StartPlacement(PlacementOffer offer)
    {
        var check = new PlacementViewModel(s, offer, LoadAsync);
        check.Closed += () => PlacementCheck = null;
        PlacementCheck = check;
    }

    [RelayCommand]
    private Task BackAsync() => nav.ShowCatalogAsync();
}
