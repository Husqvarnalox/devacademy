using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using DevAcademy.Desktop.Tests.Support;
using DevAcademy.Desktop.ViewModels;
using DevAcademy.Desktop.Views;

namespace DevAcademy.Desktop.Tests;

public class LessonScrollTests
{
    private static void Settle() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    [AvaloniaFact]
    public async Task The_next_lesson_opens_at_the_top_of_its_theory()
    {
        // Regression: with the whole-page theory view the section never changes, and the reused view kept the previous lesson's offset.
        using var host = new TestHost();
        await Helpers.CompleteOnboardingAsync(host);
        var window = new MainWindow { DataContext = host.Main, Width = 1280, Height = 800 };
        window.Show();
        var lessons = host.Services.Catalog.GetLessons(Helpers.CourseId);
        await host.Main.OpenLessonAsync(lessons[0].Id);
        Settle();
        var ws = Assert.IsType<LessonWorkspaceViewModel>(host.Main.Current);
        ws.TheorySections[^1].SelectCommand.Execute(null);
        Settle();
        var view = window.GetVisualDescendants().OfType<LessonWorkspaceView>().Single();
        var scroll = view.GetVisualDescendants().OfType<ScrollViewer>().Single(s => s.Name == "TheoryScroll");
        scroll.ScrollToEnd();
        Settle();
        Assert.True(scroll.Offset.Y > 0, "the theory page is too short to scroll");

        await host.Main.OpenLessonAsync(lessons[1].Id);
        Settle();

        Assert.Same(view, window.GetVisualDescendants().OfType<LessonWorkspaceView>().Single());
        Assert.Equal(0, scroll.Offset.Y);
        window.Close();
    }
}
