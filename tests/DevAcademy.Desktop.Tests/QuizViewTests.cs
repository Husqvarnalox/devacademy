using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using DevAcademy.Desktop.Tests.Support;
using DevAcademy.Desktop.ViewModels;
using DevAcademy.Desktop.Views;
using DevAcademy.Domain;

namespace DevAcademy.Desktop.Tests;

[Collection(DevAcademy.Desktop.Tests.Support.BindingCaptureCollection.Name)]
public class QuizViewTests
{
    private static void Settle() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    private static QuizQuestion Question(string id, QuestionType type) =>
        new(id, type, "prompt " + id, ["a", "b", "c"], [0, 1], [], null, 0, "");

    [AvaloniaFact]
    public void Checking_several_boxes_of_a_multiple_choice_question_keeps_all_of_them()
    {
        // Regression: each option used to carry a hidden RadioButton bound to the same IsSelected, so checking a box
        // checked the hidden radio and its group cleared every other option.
        using var errors = new BindingErrors();
        using var host = new TestHost(contentRoot: TestHost.ValidFixtureRoot);
        var quiz = new QuizViewModel(host.Services, "lesson", new Quiz(0.5, [Question("m", QuestionType.Multiple), Question("s", QuestionType.Single)]));
        var window = new Window { Content = new QuizView { DataContext = quiz }, Width = 900, Height = 900 };
        window.Show();
        Settle();

        var boxes = window.GetVisualDescendants().OfType<CheckBox>().ToList();
        var radios = window.GetVisualDescendants().OfType<RadioButton>().ToList();
        Assert.Equal(3, boxes.Count); // only the multiple-choice question has check boxes
        Assert.Equal(3, radios.Count); // only the single-choice question has radio buttons

        boxes[0].IsChecked = true;
        boxes[1].IsChecked = true;
        Settle();
        Assert.Equal([true, true, false], quiz.Questions[0].Options.Select(o => o.IsSelected));

        radios[2].IsChecked = true;
        radios[1].IsChecked = true;
        Settle();
        Assert.Equal([false, true, false], quiz.Questions[1].Options.Select(o => o.IsSelected));
        Assert.Equal([true, true, false], quiz.Questions[0].Options.Select(o => o.IsSelected));
        Assert.True(errors.Messages.Count == 0, string.Join("\n", errors.Messages.Distinct()));
    }

    [AvaloniaFact]
    public async Task A_wrong_multiple_choice_answer_marks_each_option_by_its_own_outcome()
    {
        // Regression (go basic-types, q4 on zero values): the row colour followed the whole question, so with float64 left
        // unticked the two correct picks turned red and the missed answer was not marked at all.
        using var errors = new BindingErrors();
        using var host = new TestHost();
        const string lessonId = "go.first-steps.basic-types";
        var quiz = new QuizViewModel(host.Services, lessonId, host.Services.Content.Current.FindLesson(lessonId)!.Quiz!);
        var window = new Window { Content = new QuizView { DataContext = quiz }, Width = 900, Height = 2400 };
        window.Show();
        var q4 = quiz.Questions.Single(q => q.Id == "q4");
        q4.Options[0].IsSelected = true;
        q4.Options[3].IsSelected = true;

        await quiz.SubmitCommand.ExecuteAsync(null);
        Settle();

        Assert.True(q4.IsWrong);
        Assert.Equal([true, false, false, true], q4.Options.Select(o => o.IsRightPick));
        Assert.Equal([false, true, false, false], q4.Options.Select(o => o.IsMissed));
        Assert.DoesNotContain(q4.Options, o => o.IsWrongPick);
        var rows = window.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("option") && b.DataContext is QuizOptionViewModel o && q4.Options.Contains(o)).ToList();
        Assert.Equal(4, rows.Count);
        Assert.DoesNotContain(rows, r => r.Classes.Contains("wrong"));
        Assert.Equal(["correct", "missed", "", "correct"], rows.Select(r => r.Classes.Contains("correct") ? "correct" : r.Classes.Contains("missed") ? "missed" : ""));

        quiz.RetryCommand.Execute(null);
        Settle();
        Assert.DoesNotContain(rows, r => r.Classes.Contains("correct") || r.Classes.Contains("missed"));
        Assert.True(errors.Messages.Count == 0, string.Join("\n", errors.Messages.Distinct()));
    }
}
