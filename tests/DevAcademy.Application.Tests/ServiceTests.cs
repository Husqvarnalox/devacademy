using DevAcademy.Application.Services;
using DevAcademy.Domain;

namespace DevAcademy.Application.Tests;

internal class World
{
    public FixedClock Clock { get; } = new(Make.T0);
    public InMemoryProgress Progress { get; } = new();
    public InMemorySubmissions Submissions { get; } = new();
    public InMemorySettings Settings { get; } = new();
    public ContentSet Set { get; }
    public InMemoryContent Content { get; }

    public World()
    {
        Set = new ContentSet(
            [],
            [Make.Course("A", ["a1", "a2", "a3"]), Make.Course("B", ["b1", "b2"], requires: ["A"])],
            [
                Make.Lesson("a1", "A", next: ["a3"]),
                Make.Lesson("a2", "A", ["a1"], quiz: true, ex: Make.IoExercise("a2.ex"), difficulty: Difficulty.Medium),
                Make.Lesson("a3", "A", ["a2"]),
                Make.Lesson("b1", "B", ["a2"]),
                Make.Lesson("b2", "B", ["b1"], quiz: true, assessmentFor: ["a1", "a2", "b1"]),
            ],
            [new LearningPathDefinition("p", "Path", "", "backend", ["B"], "paths/p.yaml")]);
        Content = new InMemoryContent(Set);
    }

    public ProgressService Progress_() => new(Content, Progress, Submissions, Settings, Clock);
    public LearningPathService Path() => new(Content, Progress, Clock);
}

public class ProgressServiceTests
{
    [Fact]
    public async Task LessonWithExerciseAndQuizCompletesOnlyWhenBothDone()
    {
        var w = new World();
        var svc = w.Progress_();

        var (res, q) = await svc.RecordQuizAttemptAsync("a2", Make.WrongAnswer);
        Assert.False(res.Passed);
        Assert.False(q.JustCompleted);
        (res, q) = await svc.RecordQuizAttemptAsync("a2", Make.RightAnswer);
        Assert.True(res.Passed);
        Assert.False(q.JustCompleted); // exercise still missing
        Assert.Equal(1.0, q.Progress.QuizBestScore);

        var s = await svc.RecordSubmissionAsync("a2", "a2.ex", [], Make.Report(Verdict.Passed));
        Assert.True(s.JustCompleted);
        Assert.Equal(w.Clock.UtcNow, s.Progress.CompletedAt);
        Assert.Equal(CompletionSource.Exercise, s.Progress.CompletionSource);
        Assert.Equal(Make.T0.AddDays(1), s.Progress.NextReviewAt);
        Assert.Equal(2, w.Progress.Attempts.Count);
        Assert.Single(w.Submissions.Items);
    }

    [Fact]
    public async Task FailedAttemptsCountOnlyBeforeFirstPassAndOnlyForLearnerFaults()
    {
        var w = new World();
        var svc = w.Progress_();
        await svc.RecordSubmissionAsync("a2", "a2.ex", [], Make.Report(Verdict.Failed, 0));
        await svc.RecordSubmissionAsync("a2", "a2.ex", [], Make.Report(Verdict.CompileError, 0, 0));
        await svc.RecordSubmissionAsync("a2", "a2.ex", [], Make.Report(Verdict.RuntimeMissing, 0, 0));
        await svc.RecordSubmissionAsync("a2", "a2.ex", [], Make.Report(Verdict.Error, 0, 0));
        var pass = await svc.RecordSubmissionAsync("a2", "a2.ex", [], Make.Report(Verdict.Passed));
        await svc.RecordSubmissionAsync("a2", "a2.ex", [], Make.Report(Verdict.Failed, 0));
        var p = w.Progress.Items["a2"];
        Assert.Equal(4, p.Attempts); // failed + compile error + pass + failed-after; infrastructure verdicts excluded
        Assert.Equal(2, p.FailedAttempts);
        Assert.True(p.ExercisePassed);
        Assert.Equal(6, w.Submissions.Items.Count);
        Assert.Equal(3, pass.Progress.Attempts);
    }

    [Fact]
    public async Task TheoryReadOnlyForLessonsWithoutExerciseOrQuiz()
    {
        var w = new World();
        var svc = w.Progress_();
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.MarkTheoryReadAsync("a2"));
        var u = await svc.MarkTheoryReadAsync("a1");
        Assert.True(u.JustCompleted);
        Assert.Equal(CompletionSource.TheoryRead, u.Progress.CompletionSource);
    }

    [Fact]
    public async Task UnknownIdsAreExplicitErrors()
    {
        var svc = new World().Progress_();
        await Assert.ThrowsAsync<ArgumentException>(() => svc.StartLessonAsync("nope"));
        await Assert.ThrowsAsync<ArgumentException>(() => svc.RecordSubmissionAsync("a2", "other", [], Make.Report(Verdict.Passed)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.RecordQuizAttemptAsync("a1", Make.RightAnswer));
    }

    [Fact]
    public async Task ReviewAdvancesAndResets()
    {
        var w = new World();
        var svc = w.Progress_();
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.RecordReviewAsync("a1", true));
        await svc.MarkTheoryReadAsync("a1");

        w.Clock.UtcNow = Make.T0.AddDays(2);
        var ok = await svc.RecordReviewAsync("a1", true);
        Assert.Equal(1, ok.ReviewStep);
        Assert.Equal(w.Clock.UtcNow.AddDays(3), ok.NextReviewAt);
        Assert.Equal(w.Clock.UtcNow, ok.LastReviewedAt);

        var bad = await svc.RecordReviewAsync("a1", false);
        Assert.Equal(0, bad.ReviewStep);
        Assert.Equal(w.Clock.UtcNow.AddDays(1), bad.NextReviewAt);
    }

    [Fact]
    public async Task PassingPlacementQuizCreditsLessonsByAssessment()
    {
        var w = new World();
        var svc = w.Progress_();
        var (_, update) = await svc.RecordQuizAttemptAsync("b2", Make.WrongAnswer);
        Assert.Null(update.CreditedLessonIds);
        Assert.DoesNotContain(w.Progress.Items.Values, p => p.IsCompleted);

        (_, update) = await svc.RecordQuizAttemptAsync("b2", Make.RightAnswer);
        Assert.Equal(["a1", "a2", "b1"], update.CreditedLessonIds);
        Assert.All(new[] { "a1", "a2", "b1" }, id =>
        {
            Assert.True(w.Progress.Items[id].IsCompleted);
            Assert.Equal(CompletionSource.Assessment, w.Progress.Items[id].CompletionSource);
        });
        Assert.Equal(CompletionSource.Quiz, w.Progress.Items["b2"].CompletionSource);
    }

    [Fact]
    public async Task AssessmentDoesNotOverwriteCompletedLessonsNorCreditOnFailure()
    {
        var w = new World();
        var svc = w.Progress_();
        await svc.MarkTheoryReadAsync("a1");
        Assert.Empty(await svc.RecordAssessmentAsync(["a2"], passed: false));
        var credited = await svc.RecordAssessmentAsync(["a1", "a2"], passed: true);
        Assert.Equal(["a2"], credited);
        Assert.Equal(CompletionSource.TheoryRead, w.Progress.Items["a1"].CompletionSource);
    }

    [Fact]
    public async Task ExportImportRoundTripIntoEmptyStore()
    {
        var src = new World();
        var svc = src.Progress_();
        src.Settings.Profile = new LearnerProfile("job", 2, LearningDepth.Deep, ["A", "B"], "en");
        src.Settings.Items["theme"] = "dark";
        src.Settings.Items["openai.apiKey"] = "sk-SECRET";
        src.Settings.Items["Auth_Token"] = "tok";
        src.Settings.Items["client-secret"] = "s";
        await svc.MarkTheoryReadAsync("a1");
        await svc.RecordSubmissionAsync("a2", "a2.ex", [new("main.py", "print(1)")], Make.Report(Verdict.Failed, 0));

        var json = await svc.ExportAsync();
        Assert.Contains("\"formatVersion\": 1", json);
        Assert.DoesNotContain("SECRET", json);
        Assert.DoesNotContain("sk-", json);
        Assert.DoesNotContain("tok", json.Replace("token", ""), StringComparison.Ordinal);
        Assert.Contains("\"theme\"", json);

        var dst = new World();
        var summary = await dst.Progress_().ImportAsync(json);
        Assert.Equal(2, summary.LessonsUpdated);
        Assert.Equal(1, summary.SubmissionsAdded);
        Assert.True(summary.ProfileImported);
        Assert.Equal(src.Progress.Items["a1"].CompletedAt, dst.Progress.Items["a1"].CompletedAt);
        Assert.Equal("print(1)", dst.Submissions.Items.Values.Single().Files.Single().Content);
        Assert.Equal(["A", "B"], dst.Settings.Profile!.EnrolledCourseIds);
        Assert.Equal("dark", dst.Settings.Items["theme"]);
        Assert.DoesNotContain("openai.apiKey", dst.Settings.Items.Keys);
    }

    [Fact]
    public async Task ImportMergesKeepingMoreAdvancedStateAndUnionsSubmissions()
    {
        var a = new World();
        await a.Progress_().MarkTheoryReadAsync("a1");                           // a1 completed in A
        await a.Progress_().StartLessonAsync("a2");                               // a2 started only in A
        await a.Progress_().RecordSubmissionAsync("a2", "a2.ex", [], Make.Report(Verdict.Failed, 0));
        var json = await a.Progress_().ExportAsync();

        var b = new World();
        await b.Progress_().StartLessonAsync("a1");                               // a1 only started in B
        await b.Progress_().RecordSubmissionAsync("a2", "a2.ex", [], Make.Report(Verdict.Passed));
        await b.Progress_().RecordSubmissionAsync("a2", "a2.ex", [], Make.Report(Verdict.Failed, 0));
        await b.Progress_().RecordSubmissionAsync("a2", "a2.ex", [], Make.Report(Verdict.Failed, 0));
        b.Settings.Profile = new LearnerProfile("mine", 1, LearningDepth.Quick, ["B"]);
        var localA2 = b.Progress.Items["a2"];

        var summary = await b.Progress_().ImportAsync(json);

        Assert.True(b.Progress.Items["a1"].IsCompleted);                          // incoming more advanced wins
        Assert.Equal(CompletionSource.TheoryRead, b.Progress.Items["a1"].CompletionSource);
        Assert.Equal(localA2, b.Progress.Items["a2"]);                            // local more attempts, kept
        Assert.Equal(1, summary.LessonsUpdated);
        Assert.Equal(1, summary.SubmissionsAdded);
        Assert.Equal(4, b.Submissions.Items.Count);
        Assert.False(summary.ProfileImported);
        Assert.Equal("mine", b.Settings.Profile.Goal);

        var again = await b.Progress_().ImportAsync(json);                        // idempotent
        Assert.Equal(0, again.SubmissionsAdded);
        Assert.Equal(0, again.LessonsUpdated);
    }

    [Fact]
    public async Task ImportedCompletionWithoutSourceIsMarkedImported()
    {
        var w = new World();
        const string json = """{"formatVersion":1,"exportedAt":"2026-01-01T00:00:00Z","progress":[{"lessonId":"a1","completedAt":"2026-01-01T00:00:00Z"}]}""";
        await w.Progress_().ImportAsync(json);
        Assert.Equal(CompletionSource.Imported, w.Progress.Items["a1"].CompletionSource);
    }

    [Theory]
    [InlineData("""{"formatVersion":2,"exportedAt":"2026-01-01T00:00:00Z"}""")]
    [InlineData("not json")]
    [InlineData("""{"exportedAt":"2026-01-01T00:00:00Z"}""")]
    public async Task ImportRejectsBadVersionOrGarbage(string json)
    {
        var w = new World();
        await Assert.ThrowsAsync<InvalidDataException>(() => w.Progress_().ImportAsync(json));
        Assert.Empty(w.Progress.Items);
    }

    [Fact]
    public async Task DashboardUsesOnlyRealRecords()
    {
        var w = new World();
        var svc = w.Progress_();
        var empty = await svc.GetDashboardAsync();
        Assert.All(empty.Courses, c => Assert.Equal(0, c.Completed));
        Assert.All(empty.SolvedExercisesByDifficulty.Values, v => Assert.Equal(0, v));
        Assert.Equal(30, empty.SubmissionsPerDay.Count);
        Assert.All(empty.SubmissionsPerDay, d => Assert.Equal(0, d.Submissions));
        Assert.Equal(0, empty.DueReviews);

        await svc.MarkTheoryReadAsync("a1");
        await svc.RecordSubmissionAsync("a2", "a2.ex", [], Make.Report(Verdict.Passed));
        w.Clock.UtcNow = Make.T0.AddDays(1).AddHours(1);
        await svc.RecordAssessmentAsync(["b1"], true);
        await svc.RecordSubmissionAsync("a2", "a2.ex", [], Make.Report(Verdict.Failed, 0));

        var d = await svc.GetDashboardAsync();
        var courseA = d.Courses.Single(c => c.CourseId == "A");
        Assert.Equal(1, courseA.Completed);
        Assert.Equal(3, courseA.Total);
        var courseB = d.Courses.Single(c => c.CourseId == "B");
        Assert.Equal((1, 1, 0), (courseB.Completed, courseB.CreditedByAssessment, courseB.CompletedByPractice));
        Assert.Equal(1, d.SolvedExercisesByDifficulty[Difficulty.Medium]);
        Assert.Equal(0, d.SolvedExercisesByDifficulty[Difficulty.Easy]);
        Assert.Equal(1, d.SubmissionsPerDay[^1].Submissions);
        Assert.Equal(1, d.SubmissionsPerDay[^2].Submissions);
        Assert.Equal(DateOnly.FromDateTime(w.Clock.UtcNow.UtcDateTime), d.SubmissionsPerDay[^1].Day);
        Assert.Equal(1, d.DueReviews); // a1 was due one day after T0; b1 not yet
    }
}

public class HintServiceTests
{
    private static HintService Make_(World w) => new(w.Content, w.Settings);

    [Fact]
    public async Task HintsRevealProgressivelyAndPersist()
    {
        var w = new World();
        var svc = Make_(w);
        // World's exercise has hints [h1,h2,h3] only if it is the IoExercise helper
        Assert.Empty(await svc.GetRevealedAsync("a2.ex"));
        Assert.Equal("h1", (await svc.RevealNextAsync("a2.ex"))!.Text);
        Assert.Equal("h2", (await svc.RevealNextAsync("a2.ex"))!.Text);
        Assert.Equal(["h1", "h2"], (await Make_(w).GetRevealedAsync("a2.ex")).Select(h => h.Text));
        var last = await svc.RevealNextAsync("a2.ex");
        Assert.Equal((3, 3), (last!.Level, last.Total));
        Assert.Null(await svc.RevealNextAsync("a2.ex"));
        Assert.Equal(3, w.Settings.Hints["a2.ex"]);
    }

    [Fact]
    public void SolutionIsSeparateExplicitRequest()
    {
        var w = new World();
        Assert.Equal("print(input())", Make_(w).GetSolution("a2.ex").Single().Content);
        Assert.Throws<ArgumentException>(() => Make_(w).GetSolution("nope"));
    }
}

public class LearningPathTests
{
    [Fact]
    public async Task CoursePathShowsStatesAndWhyLocked()
    {
        var w = new World();
        await w.Progress_().MarkTheoryReadAsync("a1");
        var path = (await w.Path().GetCoursePathAsync("A"))!;
        Assert.Equal((1, 3), (path.Completed, path.Total));
        Assert.Equal(LessonState.Completed, path.Lessons[0].State);
        Assert.Equal(LessonState.Available, path.Lessons[1].State);
        Assert.Equal(LessonState.Locked, path.Lessons[2].State);
        Assert.Equal(["Title a2"], path.Lessons[2].MissingPrerequisiteTitles);
        Assert.Null(await w.Path().GetCoursePathAsync("zzz"));
    }

    [Fact]
    public async Task RecommendationsFollowFixedOrder()
    {
        var w = new World();
        var svc = w.Progress_();
        await svc.MarkTheoryReadAsync("a1");                    // completed, review due after 1 day, next = a3
        await svc.StartLessonAsync("a2");                       // in progress
        w.Clock.UtcNow = Make.T0.AddDays(2);
        var profile = new LearnerProfile("g", 0, LearningDepth.Standard, ["B", "A"]);

        var recs = await w.Path().GetRecommendationsAsync(profile);

        Assert.Equal(
            [("a1", RecommendationKind.Review), ("a2", RecommendationKind.Continue)],
            recs.Take(2).Select(r => (r.LessonId, r.Kind)));
        // B has no Available lesson (b1 needs a2), A's first Available lesson a2 is already listed;
        // a3 is suggested through the `next` link of the last completed lesson (a1) even though it is locked.
        Assert.Equal(["a1", "a2", "a3"], recs.Select(r => r.LessonId));
        Assert.Equal(RecommendationKind.Next, recs[2].Kind);
        Assert.All(recs, r => Assert.False(string.IsNullOrWhiteSpace(r.Reason)));
    }

    [Fact]
    public async Task StartRecommendationForFirstAvailableLessonOfEnrolledCourse()
    {
        var w = new World();
        var recs = await w.Path().GetRecommendationsAsync(new LearnerProfile("g", 0, LearningDepth.Quick, ["A"]));
        var only = Assert.Single(recs);
        Assert.Equal(("a1", RecommendationKind.Start), (only.LessonId, only.Kind));
        Assert.Empty(await w.Path().GetRecommendationsAsync(null));
    }

    [Fact]
    public async Task BuildPathIncludesCrossCourseClosureInTopologicalOrder()
    {
        var w = new World();
        var plan = (await w.Path().BuildPathAsync("p"))!;
        // Target B requires A: a1, a2, a3 are pulled in before b1, b2 (a3 via course requires).
        Assert.Equal(["a1", "a2", "a3", "b1", "b2"], plan.Items.Select(i => i.Lesson.Id));
        Assert.Equal(["A", "B"], plan.Courses.Select(c => c.Id));
        Assert.Equal([false, false, false, true, true], plan.Items.Select(i => i.InTargetCourse));
        Assert.Equal(0, plan.SatisfiedCount);
        Assert.Null(await w.Path().BuildPathAsync("missing"));
    }

    [Fact]
    public async Task BuildPathMarksKnowledgeFromOtherCoursesAsSatisfied()
    {
        var w = new World();
        await w.Progress_().RecordAssessmentAsync(["a1", "a2"], true);
        var plan = (await w.Path().BuildPathAsync("p"))!;
        Assert.Equal(["a1", "a2"], plan.Items.Where(i => i.Satisfied).Select(i => i.Lesson.Id));
        Assert.Equal(3, plan.RemainingCount);
        Assert.Equal(LessonState.Available, plan.Items.Single(i => i.Lesson.Id == "b1").State);
    }

    [Fact]
    public async Task BuildPathPullsInPrerequisiteLessonsOfCoursesNotListedInRequires()
    {
        var set = new ContentSet([], [Make.Course("X", ["x1"]), Make.Course("Y", ["y1"])],
            [Make.Lesson("x1", "X"), Make.Lesson("y1", "Y", ["x1"])],
            [new LearningPathDefinition("p", "P", "", "g", ["Y"], "p.yaml")]);
        var svc = new LearningPathService(new InMemoryContent(set), new InMemoryProgress(), new FixedClock(Make.T0));
        var plan = (await svc.BuildPathAsync("p"))!;
        Assert.Equal(["x1", "y1"], plan.Items.Select(i => i.Lesson.Id));
    }
}
