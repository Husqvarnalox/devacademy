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
}
