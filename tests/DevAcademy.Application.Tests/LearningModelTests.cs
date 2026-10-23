using DevAcademy.Application.Services;
using DevAcademy.Domain;

namespace DevAcademy.Application.Tests;

/// <summary>
/// Track "go": B (level 0) b1 -> b2 -> b3; P (level 1) p1 credits b1, b2; checks course K: k1 (exercise + quiz) credits
/// b1, b2, k2 credits b3 and comes after k1; F (level 1, requires B) f1 after b3, with a challenge.
/// Route: beginner entry b1, programmer entry p1.
/// </summary>
internal sealed class LevelWorld
{
    public FixedClock Clock { get; } = new(Make.T0);
    public InMemoryProgress Progress { get; } = new();
    public InMemorySubmissions Submissions { get; } = new();
    public InMemorySettings Settings { get; } = new();
    public InMemoryContent Content { get; }

    public LevelWorld(Func<Lesson, Lesson>? tweak = null)
    {
        static Course C(string id, int level, string[] lessons, string[]? requires = null) =>
            new(id, "1.0.0", "go", "go", "Course " + id, "", level, lessons, id + "/course.yaml", requires ?? [], null);
        var lessons = new List<Lesson>
        {
            Make.Lesson("b1", "B"),
            Make.Lesson("b2", "B", ["b1"]),
            Make.Lesson("b3", "B", ["b2"]),
            Make.Lesson("p1", "P", quiz: true, ex: Make.IoExercise("px"), assessmentFor: ["b1", "b2"]),
            Make.Lesson("k1", "K", quiz: true, ex: Make.IoExercise("kx1"), assessmentFor: ["b1", "b2"]),
            Make.Lesson("k2", "K", quiz: true, assessmentFor: ["b3"]),
            Make.Lesson("f1", "F", ["b3"], ex: Make.IoExercise("fx")) with { Challenge = Make.IoExercise("fch") },
        };
        var set = new ContentSet(
            [new Track("go", TrackKind.Language, "Go", "", "go", ContentStatus.Available, ["B", "P", "K", "F"])],
            [C("B", 0, ["b1", "b2", "b3"]), C("P", 1, ["p1"]), C("K", 1, ["k1", "k2"]), C("F", 1, ["f1"], ["B"])],
            tweak is null ? lessons : lessons.Select(tweak),
            routes: [new TrackRoute("go", "b1", "p1",
                [new Competency("basics", "Basics", "", "k1", []), new Competency("more", "More", "", "k2", ["basics"])], "routes/go.yaml")]);
        Content = new InMemoryContent(set);
    }

    public ProgressService ProgressService() => new(Content, Progress, Submissions, Settings, Clock);
    public LearningPathService Path() => new(Content, Progress, Clock, Settings);
    public DiagnosticService Diagnostics() => new(Content, Progress);

    public static LearnerProfile Profile(int level, LearningDepth depth = LearningDepth.Standard) =>
        new("study", level, depth, ["B", "P", "K", "F"], "ru");

    public Task<IReadOnlyList<Recommendation>> RecsAsync(int level, LearningDepth depth = LearningDepth.Standard) =>
        Path().GetRecommendationsAsync(Profile(level, depth));

    public async Task PassExerciseAsync(string lessonId, string exerciseId) =>
        await ProgressService().RecordSubmissionAsync(lessonId, exerciseId, [], Make.Report(Verdict.Passed));

    public async Task FailExerciseAsync(string lessonId, string exerciseId) =>
        await ProgressService().RecordSubmissionAsync(lessonId, exerciseId, [], Make.Report(Verdict.Failed, 0));
}

public class TheoryPresentationTests
{
    private static readonly string[] All = ["simple", "examples", "precise", "mistakes", "summary", "programmer", "deep-dive"];

    [Fact]
    public void BeginnerKeepsContentOrderAndMovesProgrammerSummaryToTheEnd()
    {
        var plan = TheoryPresentation.Plan(["programmer", "simple", "examples", "summary"], PriorKnowledge.AbsoluteBeginner, LearningDepth.Standard);
        Assert.Equal(["simple", "examples", "summary", "programmer"], plan.Sections);
        Assert.Equal("simple", plan.Initial);
    }

    [Fact]
    public void LanguageBeginnerOpensOnTheProgrammerSummary()
    {
        var plan = TheoryPresentation.Plan(All, PriorKnowledge.LanguageBeginner, LearningDepth.Standard);
        Assert.Equal("programmer", plan.Sections[0]);
        Assert.Equal("programmer", plan.Initial);
        Assert.Equal(All.Length, plan.Sections.Count);
    }

    [Fact]
    public void IntermediateWithoutProgrammerSectionOpensOnPrecise()
    {
        var plan = TheoryPresentation.Plan(["simple", "precise", "summary"], PriorKnowledge.Intermediate, LearningDepth.Standard);
        Assert.Equal("precise", plan.Initial);
        var beginner = TheoryPresentation.Plan(["simple", "precise", "summary"], PriorKnowledge.LanguageBeginner, LearningDepth.Standard);
        Assert.Equal("simple", beginner.Initial);
    }

    [Fact]
    public void QuickDepthHidesTheDeepDiveFromTheSwitchOnly()
    {
        Assert.DoesNotContain("deep-dive", TheoryPresentation.Plan(All, PriorKnowledge.AbsoluteBeginner, LearningDepth.Quick).Sections);
        Assert.Contains("deep-dive", TheoryPresentation.Plan(All, PriorKnowledge.AbsoluteBeginner, LearningDepth.Standard).Sections);
        Assert.Contains("deep-dive", TheoryPresentation.Plan(All, PriorKnowledge.Advanced, LearningDepth.Deep).Sections);
    }

    [Fact]
    public void OutOfRangeLevelsAreClamped()
    {
        Assert.Equal("simple", TheoryPresentation.Plan(All, -5, LearningDepth.Standard).Initial);
        Assert.Equal("programmer", TheoryPresentation.Plan(All, 42, LearningDepth.Standard).Initial);
    }
}

public class ProgressMigrationTests
{
    private static (InMemoryContent Content, InMemoryProgress Progress) World(params Lesson[] lessons) =>
        (new InMemoryContent(new ContentSet([], [Make.Course("A", lessons.Select(l => l.Id).ToArray())], lessons)), new InMemoryProgress());

    [Fact]
    public async Task CopiesProgressOfAFormerIdAndKeepsTheOldRecord()
    {
        var (content, progress) = World(Make.Lesson("new", "A") with { FormerIds = ["old"] });
        var old = new LessonProgress("old", Make.T0, Make.T0.AddHours(1), 0.9, true, Attempts: 3, CompletionSource: CompletionSource.Exercise);
        await progress.SaveAsync(old);

        var report = await new ProgressMigrationService(content, progress).MigrateAsync();

        Assert.Equal([("old", "new")], report.Copied);
        var migrated = (await progress.GetAsync("new"))!;
        Assert.Equal(old with { LessonId = "new" }, migrated);
        Assert.Equal(old, await progress.GetAsync("old"));
    }

    [Fact]
    public async Task IsIdempotentAndNeverOverwritesExistingProgress()
    {
        var (content, progress) = World(Make.Lesson("new", "A") with { FormerIds = ["old"] });
        await progress.SaveAsync(new LessonProgress("old", Make.T0, Make.T0, CompletionSource: CompletionSource.Exercise));
        var service = new ProgressMigrationService(content, progress);

        Assert.True((await service.MigrateAsync()).Changed);
        var second = await service.MigrateAsync();
        Assert.False(second.Changed);
        Assert.Equal([("old", "new")], second.Skipped);

        var (content2, progress2) = World(Make.Lesson("new", "A") with { FormerIds = ["old"] });
        await progress2.SaveAsync(new LessonProgress("old", Make.T0, Make.T0, CompletionSource: CompletionSource.Exercise));
        await progress2.SaveAsync(new LessonProgress("new", Make.T0));
        await new ProgressMigrationService(content2, progress2).MigrateAsync();
        Assert.False((await progress2.GetAsync("new"))!.IsCompleted);
    }

    [Fact]
    public async Task TheMostAdvancedFormerRecordWins()
    {
        var (content, progress) = World(Make.Lesson("new", "A") with { FormerIds = ["a", "b"] });
        await progress.SaveAsync(new LessonProgress("a", Make.T0));
        await progress.SaveAsync(new LessonProgress("b", Make.T0, Make.T0, CompletionSource: CompletionSource.Quiz));
        await new ProgressMigrationService(content, progress).MigrateAsync();
        Assert.Equal(CompletionSource.Quiz, (await progress.GetAsync("new"))!.CompletionSource);
    }

    [Fact]
    public async Task LessonsWithoutFormerIdsAreNeverCredited()
    {
        var (content, progress) = World(Make.Lesson("new", "A"));
        await progress.SaveAsync(new LessonProgress("old", Make.T0, Make.T0, CompletionSource: CompletionSource.Exercise));
        Assert.False((await new ProgressMigrationService(content, progress).MigrateAsync()).Changed);
        Assert.Null(await progress.GetAsync("new"));
    }
}

public class CheckCreditTests
{
    [Fact]
    public async Task ACheckWithAnExerciseCreditsOnlyWhenExerciseAndQuizPass()
    {
        var w = new LevelWorld();
        var svc = w.ProgressService();
        var (_, afterQuiz) = await svc.RecordQuizAttemptAsync("k1", Make.RightAnswer);
        Assert.Null(afterQuiz.CreditedLessonIds);
        Assert.Null(await w.Progress.GetAsync("b1"));

        var update = await svc.RecordSubmissionAsync("k1", "kx1", [], Make.Report(Verdict.Passed));
        Assert.Equal(["b1", "b2"], update.CreditedLessonIds);
        Assert.Equal(CompletionSource.Assessment, (await w.Progress.GetAsync("b2"))!.CompletionSource);
    }

    [Fact]
    public async Task AFailedCheckChangesNothingAndKeepsExistingProgress()
    {
        var w = new LevelWorld();
        await w.ProgressService().MarkTheoryReadAsync("b1");
        await w.FailExerciseAsync("k1", "kx1");
        await w.ProgressService().RecordQuizAttemptAsync("k1", Make.WrongAnswer);
        Assert.Equal(CompletionSource.TheoryRead, (await w.Progress.GetAsync("b1"))!.CompletionSource);
        Assert.Null(await w.Progress.GetAsync("b2"));
    }
}

public class DiagnosticTests
{
    [Fact]
    public async Task WithoutEvidenceEverythingIsNotCheckedAndTheFirstCheckIsNext()
    {
        var report = (await new LevelWorld().Diagnostics().GetReportAsync("go"))!;
        Assert.All(report.Competencies, c => Assert.Equal(CompetencyState.NotChecked, c.State));
        Assert.Equal("basics", report.NextCheck!.Competency.Id);
        Assert.Equal(["basics"], report.Competencies[1].WaitingFor.Select(c => c.Id));
    }

    [Fact]
    public async Task AFailedCheckIsNeedsWorkWithConcreteGaps()
    {
        var w = new LevelWorld();
        await w.ProgressService().MarkTheoryReadAsync("b1");
        await w.FailExerciseAsync("k1", "kx1");
        var basics = (await w.Diagnostics().GetReportAsync("go"))!.Competencies[0];
        Assert.Equal(CompetencyState.NeedsWork, basics.State);
        Assert.True(basics.ExerciseFailed);
        Assert.Equal(["b2"], basics.Gaps.Select(l => l.Id));
    }

    [Fact]
    public async Task PassingTheCheckConfirmsAndUnblocksTheNextOne()
    {
        var w = new LevelWorld();
        await w.ProgressService().RecordQuizAttemptAsync("k1", Make.RightAnswer);
        await w.PassExerciseAsync("k1", "kx1");
        var report = (await w.Diagnostics().GetReportAsync("go"))!;
        Assert.Equal(CompetencyState.Confirmed, report.Competencies[0].State);
        Assert.Equal("more", report.NextCheck!.Competency.Id);
    }

    [Fact]
    public async Task CompletingAllCoveredLessonsByPracticeAlsoConfirms()
    {
        var w = new LevelWorld();
        await w.ProgressService().MarkTheoryReadAsync("b3");
        Assert.Equal(CompetencyState.Confirmed, (await w.Diagnostics().GetReportAsync("go"))!.Competencies[1].State);
    }

    [Fact]
    public async Task ARetakeAfterAFailureCanStillConfirm()
    {
        var w = new LevelWorld();
        await w.ProgressService().RecordQuizAttemptAsync("k1", Make.WrongAnswer);
        Assert.Equal(CompetencyState.NeedsWork, (await w.Diagnostics().GetReportAsync("go"))!.Competencies[0].State);
        await w.ProgressService().RecordQuizAttemptAsync("k1", Make.RightAnswer);
        await w.PassExerciseAsync("k1", "kx1");
        Assert.Equal(CompetencyState.Confirmed, (await w.Diagnostics().GetReportAsync("go"))!.Competencies[0].State);
    }

    [Fact]
    public async Task TracksWithoutCompetenciesHaveNoReport()
    {
        Assert.Null(await new LevelWorld().Diagnostics().GetReportAsync("nope"));
    }
}

public class LevelRoutingTests
{
    [Fact]
    public async Task AbsoluteBeginnerStartsAtTheBeginnerEntryAndGetsNoChecks()
    {
        var recs = await new LevelWorld().RecsAsync(PriorKnowledge.AbsoluteBeginner);
        Assert.Equal("b1", recs[0].LessonId);
        Assert.DoesNotContain(recs, r => r.Kind == RecommendationKind.Diagnose);
        Assert.DoesNotContain(recs, r => r.LessonId is "k1" or "k2" or "p1");
    }

    [Fact]
    public async Task LanguageBeginnerStartsAtTheProgrammerEntryNotAtLessonOne()
    {
        var recs = await new LevelWorld().RecsAsync(PriorKnowledge.LanguageBeginner);
        Assert.Equal("p1", recs[0].LessonId);
        Assert.DoesNotContain(recs, r => r.LessonId == "b1");
    }

    [Fact]
    public async Task IntermediateAndAdvancedAreOfferedTheNextCheck()
    {
        foreach (var level in new[] { PriorKnowledge.Intermediate, PriorKnowledge.Advanced })
        {
            var recs = await new LevelWorld().RecsAsync(level);
            Assert.Equal(RecommendationKind.Diagnose, recs[0].Kind);
            Assert.Equal("k1", recs[0].LessonId);
            Assert.DoesNotContain(recs, r => r.LessonId == "b1");
        }
    }

    [Fact]
    public async Task AFailedCheckLeadsToItsGapRegardlessOfLevel()
    {
        var w = new LevelWorld();
        await w.FailExerciseAsync("k1", "kx1");
        var recs = await w.RecsAsync(PriorKnowledge.Advanced);
        Assert.Contains(recs, r => r.Kind == RecommendationKind.Remediate && r.LessonId == "b1");
    }

    [Fact]
    public async Task CreditFromTheProgrammerCourseMovesTheBeginnerCourseForward()
    {
        var w = new LevelWorld();
        await w.ProgressService().RecordQuizAttemptAsync("p1", Make.RightAnswer);
        await w.PassExerciseAsync("p1", "px");
        var recs = await w.RecsAsync(PriorKnowledge.LanguageBeginner);
        Assert.Contains(recs, r => r.LessonId == "b3");
        Assert.DoesNotContain(recs, r => r.LessonId is "b1" or "b2");
    }

    [Fact]
    public async Task DeepDepthSuggestsTheChallengeOfTheLastLesson()
    {
        var w = new LevelWorld();
        foreach (var id in new[] { "b1", "b2", "b3" }) await w.ProgressService().MarkTheoryReadAsync(id);
        w.Clock.UtcNow = Make.T0.AddMinutes(5);
        await w.PassExerciseAsync("f1", "fx");
        Assert.Contains(await w.RecsAsync(PriorKnowledge.AbsoluteBeginner, LearningDepth.Deep), r => r.Kind == RecommendationKind.Challenge && r.LessonId == "f1");
        Assert.DoesNotContain(await w.RecsAsync(PriorKnowledge.AbsoluteBeginner), r => r.Kind == RecommendationKind.Challenge);
    }
}
