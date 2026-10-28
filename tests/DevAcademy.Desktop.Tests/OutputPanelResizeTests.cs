using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DevAcademy.Desktop.Tests.Support;
using DevAcademy.Desktop.ViewModels;
using DevAcademy.Desktop.Views;

namespace DevAcademy.Desktop.Tests;

/// <summary>Dragging the splitter above the lesson's output tabs resizes the tabs and the debug panel inside them.</summary>
public class OutputPanelResizeTests
{
    [AvaloniaFact]
    public void Output_tabs_and_debug_sections_follow_the_splitter_row()
    {
        using var host = new TestHost();
        var vm = ExerciseSessionViewModel.CreateScratch(host.Services, "resize-lesson", "go", "package main\n\nfunc main() {}\n");
        vm.OutputTab = ExerciseSessionViewModel.DebugTab;
        var window = new Window { Width = 1100, Height = 900, Content = new SessionView { DataContext = vm } };
        window.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();
            var tabs = window.GetVisualDescendants().OfType<TabControl>().Single(t => Grid.GetRow(t) == 5);
            var panel = window.GetVisualDescendants().OfType<DebugPanelView>().Single();
            var scroll = panel.GetVisualDescendants().OfType<ScrollViewer>().Single(s => s.Name == "SectionsScroll");
            var sections = panel.GetVisualDescendants().OfType<Grid>().Single(g => g.Name == "Sections");
            var before = scroll.Bounds.Height;

            // What GridSplitter does on a drag: the output row gets a new pixel height.
            ((Grid)tabs.Parent!).RowDefinitions[5].Height = new GridLength(500);
            Dispatcher.UIThread.RunJobs();

            // Measured on the scroll area, not the sections: without a debugger installed an install notice takes room,
            // and the sections then sit at their minimum height until the area outgrows it.
            Assert.Equal(500, tabs.Bounds.Height, 1);
            Assert.True(scroll.Bounds.Height >= before + 200, $"debug area stayed at {scroll.Bounds.Height}px (was {before}px)");
            Assert.True(sections.Bounds.Height >= scroll.Bounds.Height - 0.5, $"debug sections {sections.Bounds.Height}px do not fill the {scroll.Bounds.Height}px area");
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task Output_tabs_stay_on_one_line_in_a_narrow_pane_and_scroll_the_selected_tab_into_view()
    {
        // Regression: the strip was a WrapPanel, so in a narrow window the debug tab moved onto a second line.
        using var host = new TestHost();
        await host.Main.OpenLessonAsync(Helpers.FirstLessonId(host));
        var vm = ((LessonWorkspaceViewModel)host.Main.Current!).Practice!;
        var window = new Window { Width = 300, Height = 700, Content = new SessionView { DataContext = vm } };
        window.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();
            var tabs = window.GetVisualDescendants().OfType<TabControl>().Single(t => t.Name == "OutputTabs");
            var headers = tabs.GetVisualDescendants().OfType<TabItem>().Where(t => t.IsVisible).ToList();
            Assert.True(headers.Count >= 3);
            Assert.Single(headers.Select(h => h.Bounds.Y).Distinct());
            var strip = tabs.GetVisualDescendants().OfType<ScrollViewer>().Single(s => s.Name == "PART_TabStrip");
            Assert.True(strip.Extent.Width > strip.Viewport.Width, "the strip should overflow at this width");

            vm.OutputTab = ExerciseSessionViewModel.DebugTab;
            for (var i = 0; i < 3; i++) Dispatcher.UIThread.RunJobs();
            var debug = (TabItem)tabs.ContainerFromIndex(ExerciseSessionViewModel.DebugTab)!;
            var x = Avalonia.VisualExtensions.TranslatePoint(debug, new Avalonia.Point(0, 0), strip)!.Value.X;
            Assert.True(x >= 0 && x + debug.Bounds.Width <= strip.Viewport.Width + 0.5, $"debug tab at {x}..{x + debug.Bounds.Width}, viewport {strip.Viewport.Width}");
        }
        finally { window.Close(); }
    }
}
