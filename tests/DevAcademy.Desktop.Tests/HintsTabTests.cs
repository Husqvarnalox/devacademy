using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using DevAcademy.Desktop.Tests.Support;
using DevAcademy.Desktop.ViewModels;
using DevAcademy.Desktop.Views;

namespace DevAcademy.Desktop.Tests;

[Collection(BindingCaptureCollection.Name)]
public class HintsTabTests
{
    private static void Settle() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    [AvaloniaFact]
    public async Task The_hints_tab_opens_hints_one_by_one_without_the_overflow_menu()
    {
        // Regression: the only way to open a hint was the "..." menu, and the tab said the exercise had no hints until one was opened.
        using var errors = new BindingErrors();
        using var host = new TestHost();
        var window = new MainWindow { DataContext = host.Main, Width = 1280, Height = 900 };
        window.Show();
        await host.Main.OpenLessonAsync(Helpers.FirstLessonId(host));
        var ws = Assert.IsType<LessonWorkspaceViewModel>(host.Main.Current);
        var s = ws.Practice!;
        var total = s.Exercise!.Hints.Count;
        Assert.True(total >= 2, "the first lesson needs at least two hints for this test");
        s.OutputTab = ExerciseSessionViewModel.HintsTab;
        Settle();

        var session = window.GetVisualDescendants().OfType<SessionView>().First(v => v.DataContext == s);
        var button = session.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "NextHintButton");
        Assert.True(button.IsEffectivelyVisible);
        Assert.Equal($"Показать подсказку 1 из {total}", s.NextHintButtonText);
        Assert.DoesNotContain(session.GetVisualDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible && t.Text?.Contains("нет") == true && t.Text.Contains("Подсказ"));

        for (var i = 1; i <= total; i++)
        {
            await s.RevealHintCommand.ExecuteAsync(null);
            Settle();
            Assert.Equal(i, s.Hints.Count);
        }
        Assert.False(button.IsVisible);
        Assert.True(errors.Messages.Count == 0, string.Join("\n", errors.Messages.Distinct()));
        window.Close();
    }
}
