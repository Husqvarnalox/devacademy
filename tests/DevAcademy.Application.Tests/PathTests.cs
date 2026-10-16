using DevAcademy.Application.Services;
using DevAcademy.Domain;

namespace DevAcademy.Application.Tests;

/// <summary>Paths with stages and "one of" alternatives, shared progress, followed paths and placement credit.</summary>
internal sealed class PathWorld
{
    public FixedClock Clock { get; } = new(Make.T0);
    public InMemoryProgress Progress { get; } = new();
    public InMemorySubmissions Submissions { get; } = new();
    public InMemorySettings Settings { get; } = new();
    public InMemoryContent Content { get; }

    // A: a1 -> a2 -> a3 (base). D, F: alternatives, each requires A. T: target with a placement quiz crediting a1, a2.
    public PathWorld()
    {
        var set = new ContentSet(
            [],
            [
                Make.Course("A", ["a1", "a2", "a3"]),
                Make.Course("D", ["d1"], ["A"]),
                Make.Course("F", ["f1"], ["A"]),
                Make.Course("T", ["t0", "t1"], ["A"]),
            ],
            [
                Make.Lesson("a1", "A"),
                Make.Lesson("a2", "A", ["a1"]),
                Make.Lesson("a3", "A", ["a2"]),
                Make.Lesson("d1", "D", ["a3"]),
                Make.Lesson("f1", "F", ["a3"]),
                Make.Lesson("t0", "T", quiz: true, assessmentFor: ["a1", "a2"]),
                Make.Lesson("t1", "T", ["a3"]),
            ],
            [
                new LearningPathDefinition("web", "Web", "", "backend", ["A"], "paths/web.yaml",
                    [new PathStage("Base", ["A"]), new PathStage("Framework", ["D", "F"])], [["D", "F"]]),
                new LearningPathDefinition("deep", "Deep", "", "general", ["T"], "paths/deep.yaml"),
                new LearningPathDefinition("plain", "Plain", "", "general", ["D"], "paths/plain.yaml"),
            ]);
        Content = new InMemoryContent(set);
    }

    public Task CompleteAsync(string lessonId) => Progress_().MarkTheoryReadAsync(lessonId);

    public ProgressService Progress_() => new(Content, Progress, Submissions, Settings, Clock);
    public LearningPathService Path() => new(Content, Progress, Clock, Settings);
}

public class PathAlternativesTests
{
    [Fact]
    public async Task DefaultChoiceIsFirstMemberAndOtherIsNotRequired()
    {
        var w = new PathWorld();
        var plan = (await w.Path().BuildPathAsync("web"))!;
        var group = Assert.Single(plan.Alternatives);
        Assert.Equal("D", group.SelectedCourseId);
        Assert.False(group.IsChosen);
        Assert.Contains(plan.Items, i => i.Lesson.Id == "d1");
        Assert.DoesNotContain(plan.Items, i => i.Lesson.Id == "f1");
    }

    [Fact]
    public async Task ChoiceIsPersistedPerPathAndSwitchesTheRequiredCourse()
    {
        var w = new PathWorld();
        await w.Path().SelectAlternativeAsync("web", 0, "F");
        Assert.Equal("F", w.Settings.Items["path.alternative.web.0"]);

        var fresh = w.Path(); // a new service instance reads the persisted choice
        var plan = (await fresh.BuildPathAsync("web"))!;
        Assert.True(plan.Alternatives[0].IsChosen);
        Assert.Equal("F", plan.Alternatives[0].SelectedCourseId);
        Assert.Contains(plan.Items, i => i.Lesson.Id == "f1");
        Assert.DoesNotContain(plan.Items, i => i.Lesson.Id == "d1");

        var d = plan.Stages.SelectMany(s => s.Courses).Single(c => c.Course.Id == "D");
        Assert.False(d.IsSelected);
        Assert.True(d.IsAlternative);
        Assert.Equal(1, d.Total); // shown, with its lessons, but not counted in the plan
        Assert.Equal(plan.Items.Count, plan.Items.Select(i => i.Lesson.Id).Distinct().Count());
    }

    [Fact]
    public async Task SelectingAnOutsiderOrUnknownGroupThrows()
    {
        var w = new PathWorld();
        await Assert.ThrowsAsync<ArgumentException>(() => w.Path().SelectAlternativeAsync("web", 0, "T"));
        await Assert.ThrowsAsync<ArgumentException>(() => w.Path().SelectAlternativeAsync("web", 3, "D"));
        await Assert.ThrowsAsync<ArgumentException>(() => w.Path().SelectAlternativeAsync("nope", 0, "D"));
    }

    [Fact]
    public async Task ProgressInAnUnpickedAlternativeIsKeptWhenSwitchingBack()
    {
        var w = new PathWorld();
        foreach (var id in new[] { "a1", "a2", "a3", "d1" }) await w.CompleteAsync(id);
        await w.Path().SelectAlternativeAsync("web", 0, "F");
        await w.Path().SelectAlternativeAsync("web", 0, "D");
        var plan = (await w.Path().BuildPathAsync("web"))!;
        Assert.Equal(plan.Items.Count, plan.SatisfiedCount);
    }

    [Fact]
    public async Task StagesFollowDeclaredOrderAndPlainPathsGetLayerStages()
    {
        var w = new PathWorld();
        var web = (await w.Path().BuildPathAsync("web"))!;
        Assert.Equal(["Base", "Framework"], web.Stages.Select(s => s.Title));
        Assert.Equal(["A"], web.Stages[0].Courses.Select(c => c.Course.Id));
        Assert.Equal(["D", "F"], web.Stages[1].Courses.Select(c => c.Course.Id));

        var plain = (await w.Path().BuildPathAsync("plain"))!;
        Assert.All(plain.Stages, s => Assert.Null(s.Title));
        Assert.Equal(["A", "D"], plain.Stages.SelectMany(s => s.Courses).Select(c => c.Course.Id));
        Assert.Equal(["A"], plain.Graph.Layers[0].Select(c => c.Id));
        Assert.Contains(plain.Graph.Edges, e => e is { FromCourseId: "A", ToCourseId: "D" });
    }
}

public class PathKnowledgeReuseTests
{
    [Fact]
    public async Task PrerequisiteLessonsCompletedEarlierAreMarkedAlreadyKnown()
    {
        var w = new PathWorld();
        foreach (var id in new[] { "a1", "a2" }) await w.CompleteAsync(id);
        var plan = (await w.Path().BuildPathAsync("deep"))!;
        var known = plan.Items.Where(i => i.AlreadyKnown).Select(i => i.Lesson.Id);
        Assert.Equal(["a1", "a2"], known);
        Assert.Equal(2, plan.AlreadyKnownCount);
        Assert.Equal(2, plan.SatisfiedCount);
    }

    [Fact]
    public async Task LessonSharedByTwoPathsIsReportedWithBothPathsAndProgressIsShared()
    {
        var w = new PathWorld();
        await w.CompleteAsync("a1");
        var deep = (await w.Path().BuildPathAsync("deep"))!;
        var plain = (await w.Path().BuildPathAsync("plain"))!;
        var a1Deep = deep.Items.Single(i => i.Lesson.Id == "a1");
        Assert.Contains("Plain", a1Deep.SharedWithPaths);
        Assert.True(a1Deep.Satisfied);
        Assert.True(plain.Items.Single(i => i.Lesson.Id == "a1").Satisfied);
        Assert.DoesNotContain(deep.Items.Single(i => i.Lesson.Id == "t1").SharedWithPaths, p => p == "Deep");
    }

    [Fact]
    public async Task NotCompletedLessonsAreNeverKnown()
    {
        var w = new PathWorld();
        var plan = (await w.Path().BuildPathAsync("deep"))!;
        Assert.Equal(0, plan.AlreadyKnownCount);
        Assert.All(plan.Items, i => Assert.False(i.AlreadyKnown));
    }
}

public class PathPlacementTests
{
    [Fact]
    public async Task PlacementIsOfferedOnlyWhereAnAssessmentExists()
    {
        var w = new PathWorld();
        var withQuiz = await w.Path().GetCoursePlacementOffersAsync("T");
        var offer = Assert.Single(withQuiz);
        Assert.Equal("t0", offer.Assessment.Id);
        Assert.Equal(["a1", "a2"], offer.CreditedLessons.Select(l => l.Id));
        Assert.Empty(await w.Path().GetCoursePlacementOffersAsync("A"));
        Assert.Empty(await w.Path().GetCoursePlacementOffersAsync("D"));
    }

    [Fact]
    public async Task PassingCreditsTheListedLessonsAsAssessmentAndShowsInThePath()
    {
        var w = new PathWorld();
        var (result, update) = await w.Progress_().RecordQuizAttemptAsync("t0", Make.RightAnswer);
        Assert.True(result.Passed);
        Assert.Equal(["a1", "a2"], update.CreditedLessonIds!.Order());
        Assert.All(new[] { "a1", "a2" }, id =>
        {
            Assert.True(w.Progress.Items[id].IsCompleted);
            Assert.Equal(CompletionSource.Assessment, w.Progress.Items[id].CompletionSource);
        });
        Assert.False(w.Progress.Items.ContainsKey("a3"));

        var plan = (await w.Path().BuildPathAsync("deep"))!;
        Assert.Equal(2, plan.Items.Count(i => i.CreditedByAssessment && i.Lesson.Id.StartsWith('a')));
        Assert.Equal(plan.CreditedCount, plan.Items.Count(i => i.CreditedByAssessment));
        Assert.True(plan.CreditedCount >= 2);
        var course = plan.Stages.SelectMany(s => s.Courses).Single(c => c.Course.Id == "A");
        Assert.Equal(2, course.Credited);
        Assert.Equal(2, course.Completed);
        Assert.Equal(3, course.Total);
    }

    [Fact]
    public async Task FailingChangesNoLessonAndOffersNothingNew()
    {
        var w = new PathWorld();
        var (result, update) = await w.Progress_().RecordQuizAttemptAsync("t0", Make.WrongAnswer);
        Assert.False(result.Passed);
        Assert.Empty(update.CreditedLessonIds ?? []);
        Assert.False(w.Progress.Items.ContainsKey("a1"));
        Assert.False(w.Progress.Items.ContainsKey("a2"));
        Assert.Single(await w.Path().GetCoursePlacementOffersAsync("T")); // still offered; can retry
    }

    [Fact]
    public async Task OfferDisappearsOnceEverythingItWouldCreditIsDone()
    {
        var w = new PathWorld();
        await w.CompleteAsync("a1");
        await w.CompleteAsync("a2");
        Assert.Empty(await w.Path().GetCoursePlacementOffersAsync("T"));
    }
}

public class FollowedPathsTests
{
    [Fact]
    public async Task SeveralPathsCanBeFollowedAndSwitchedBetween()
    {
        var w = new PathWorld();
        var svc = w.Path();
        Assert.Empty(await svc.GetFollowedPathIdsAsync());
        await svc.SetFollowedAsync("web", true);
        await svc.SetFollowedAsync("deep", true);
        await svc.SetFollowedAsync("web", true); // idempotent
        Assert.Equal(["web", "deep"], await w.Path().GetFollowedPathIdsAsync());
        await svc.SetFollowedAsync("web", false);
        Assert.Equal(["deep"], await w.Path().GetFollowedPathIdsAsync());
    }

    [Fact]
    public async Task FollowingEnrolsTheCoursesOfThePlanInTheProfile()
    {
        var w = new PathWorld();
        w.Settings.Profile = new LearnerProfile("g", 0, LearningDepth.Standard, ["A"]);
        await w.Path().SetFollowedAsync("deep", true);
        Assert.Contains("T", w.Settings.Profile!.EnrolledCourseIds);
        Assert.Contains("A", w.Settings.Profile.EnrolledCourseIds);
    }

    [Fact]
    public async Task UnknownPathCannotBeFollowed() =>
        await Assert.ThrowsAsync<ArgumentException>(() => new PathWorld().Path().SetFollowedAsync("zzz", true));

    [Fact]
    public async Task NextStepComesFromRecommendationsFilteredToThePath()
    {
        var w = new PathWorld();
        await w.CompleteAsync("a1");
        var profile = new LearnerProfile("g", 0, LearningDepth.Standard, ["A", "T"]);
        var next = (await w.Path().GetPathNextStepAsync("deep", profile))!;
        Assert.Contains(next.LessonId, new[] { "a2", "t0" });
        Assert.NotEqual("d1", next.LessonId);
    }

    [Fact]
    public async Task NextStepIsNullWhenPathIsFinished()
    {
        var w = new PathWorld();
        var plan = (await w.Path().BuildPathAsync("plain"))!;
        foreach (var i in plan.Items) await w.CompleteAsync(i.Lesson.Id);
        Assert.Null(await w.Path().GetPathNextStepAsync("plain", null));
    }

    [Fact]
    public void PathsForLanguageIncludeStageAndAlternativeCourses()
    {
        var w = new PathWorld();
        Assert.Equal(["web", "deep", "plain"], w.Path().GetPathsForLanguage("python").Select(p => p.Id));
        Assert.Empty(w.Path().GetPathsForLanguage("go"));
    }
}
