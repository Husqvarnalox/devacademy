using System.Globalization;
using DevAcademy.Desktop.ViewModels;
using DevAcademy.Domain;
using DevAcademy.Execution;

namespace DevAcademy.Desktop.Tests.Support;

public static class Helpers
{
    public const string CourseId = "python-fundamentals";

    public static bool HasPython3()
    {
        try
        {
            return Probe("python3");
        }
        catch { return false; }
    }

    private static bool Probe(string exe)
    {
        try
        {
            using var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe, "--version")
            { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false })!;
            p.WaitForExit(5000);
            return p.ExitCode == 0;
        }
        catch { return false; }
    }

    public static async Task CompleteOnboardingAsync(TestHost host)
    {
        await host.Main.InitializeAsync();
        var onboarding = Assert.IsType<OnboardingViewModel>(host.Main.Current);
        var python = onboarding.Editor.Languages.Single(l => l.Track.Id == "python");
        onboarding.Editor.SelectLanguage(python);
        await onboarding.FinishCommand.ExecuteAsync(null);
    }

    public static string FirstLessonId(TestHost host) => host.Services.Catalog.GetLessons(CourseId)[0].Id;

    public static void AnswerCorrectly(QuizViewModel quiz)
    {
        foreach (var q in quiz.Questions)
        {
            switch (q.Question.Type)
            {
                case QuestionType.Single:
                case QuestionType.Multiple:
                    foreach (var i in q.Question.CorrectIndices) q.Options[i].IsSelected = true;
                    break;
                case QuestionType.Short:
                    q.Text = q.Question.AcceptedAnswers[0];
                    break;
                default:
                    q.Text = q.Question.NumericAnswer!.Value.ToString(CultureInfo.InvariantCulture);
                    break;
            }
        }
    }

    public static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 5000)
    {
        var until = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition())
        {
            if (DateTime.UtcNow > until) throw new TimeoutException("condition not reached");
            await Task.Delay(25);
        }
    }
}
