using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Styling;
using Avalonia.VisualTree;
using DevAcademy.Application.Visualization;
using DevAcademy.Content;
using DevAcademy.Desktop.Markdown;
using DevAcademy.Desktop.Tests.Support;
using DevAcademy.Desktop.ViewModels;
using DevAcademy.Desktop.Views;

namespace DevAcademy.Desktop.Tests;

[Collection(BindingCaptureCollection.Name)]
public class KMeansPcaViewModelTests
{
    [Fact]
    public void KMeans_playback_walks_every_frame_and_reports_convergence()
    {
        var vm = new KMeansViewModel(3, KMeansInit.KMeansPlusPlus, 1);
        Assert.Equal(0, vm.Position);
        Assert.Equal(KMeansPhase.Init, vm.Frame.Current.Phase);
        Assert.False(vm.HasResult);
        vm.StepForwardCommand.Execute(null);
        Assert.Equal(KMeansPhase.Assign, vm.Frame.Current.Phase);
        Assert.Contains("Назначение", vm.StatusText);
        vm.StepForwardCommand.Execute(null);
        Assert.Contains("Пересчёт центров", vm.StatusText);
        vm.PlayPauseCommand.Execute(null);
        for (int i = 0; i < vm.StepCount + 2; i++) vm.Tick();
        Assert.True(vm.IsFinished);
        Assert.False(vm.IsPlaying);
        Assert.True(vm.Trace.Converged);
        Assert.Contains("Сошлось за", vm.ResultText);
        vm.ResetCommand.Execute(null);
        Assert.Equal(0, vm.Position);
        Assert.False(vm.HasResult);
    }

    [Fact]
    public void KMeans_seed_init_and_k_changes_rerun_the_algorithm()
    {
        var vm = new KMeansViewModel(3, KMeansInit.Random, 1);
        vm.StepForwardCommand.Execute(null);
        vm.SeedText = "42";
        vm.ApplySeedCommand.Execute(null);
        Assert.Equal(42, vm.Trace.Seed);
        Assert.Equal(0, vm.Position);
        vm.SelectedInit = vm.Inits.First(i => i.Kind == KMeansInit.KMeansPlusPlus);
        Assert.Equal(KMeansInit.KMeansPlusPlus, vm.Trace.Init);
        vm.K = 5;
        Assert.Equal(5, vm.Trace.K);
        vm.SeedText = "1.5";
        vm.ApplySeedCommand.Execute(null);
        Assert.True(vm.HasError);
        Assert.Equal(42, vm.Trace.Seed);
    }

    [Fact]
    public void KMeans_k_above_the_point_count_is_clamped_with_a_message()
    {
        var vm = new KMeansViewModel(5, KMeansInit.Random, 1, [(0, 0), (1, 1), (5, 5)]);
        Assert.Equal(3, vm.Trace.K);
        Assert.True(vm.HasError);
        // Fewer than two points falls back to the built-in dataset.
        Assert.Equal(PointData.DefaultBlobs().Count, new KMeansViewModel(3, KMeansInit.Random, 1, [(0, 0)]).Trace.Points.Count);
    }

    [Fact]
    public void Pca_steps_through_the_five_stages_with_explanations()
    {
        var vm = new PcaViewModel();
        var stages = new List<PcaStage>();
        for (int i = 0; i <= vm.StepCount; i++)
        {
            stages.Add(vm.Frame.Current.Stage);
            Assert.False(string.IsNullOrWhiteSpace(vm.StatusText));
            Assert.False(string.IsNullOrWhiteSpace(vm.DetailText));
            vm.StepForwardCommand.Execute(null);
        }
        Assert.Equal(Enum.GetValues<PcaStage>(), stages);
        Assert.True(vm.IsFinished);
        Assert.Contains("PC1", vm.VarianceText);
        Assert.Contains("%", vm.VarianceText);
        vm.ResetCommand.Execute(null);
        Assert.Equal(PcaStage.Raw, vm.Frame.Current.Stage);
    }

    [Fact]
    public void Pca_degenerate_data_shows_a_message_instead_of_percentages()
    {
        var vm = new PcaViewModel([(1, 1), (1, 1), (1, 1)]);
        Assert.True(vm.IsDegenerate);
        Assert.DoesNotContain("%", vm.VarianceText);
    }

    [Fact]
    public void Directive_limits_in_the_validator_match_the_lab()
    {
        Assert.Equal(KMeansLab.MinK, TheoryParser.KMeansMinK);
        Assert.Equal(KMeansLab.MaxK, TheoryParser.KMeansMaxK);
        Assert.Equal(PointData.MaxPoints, TheoryParser.VisualizationMaxPoints);
        Assert.Equal(PointData.MaxCoordinate, TheoryParser.VisualizationMaxCoordinate);
        foreach (string i in TheoryParser.KMeansInits) Assert.True(KMeansLab.TryParseInit(i, out _), i);
        foreach (var i in Enum.GetValues<KMeansInit>()) Assert.Contains(KMeansLab.InitId(i), TheoryParser.KMeansInits);
    }
}

[Collection(BindingCaptureCollection.Name)]
public class KMeansPcaRenderTests
{
    private static void Settle() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void KMeans_and_pca_render_in_both_themes_without_binding_errors(string theme)
    {
        using var errors = new BindingErrors();
        var md = MarkdownRenderer.Render("```visualize kmeans k=4 init=random seed=7\n```\n\n```visualize pca\n1 2\n2 3.9\n3 6.1\n4 8\n```\n\n```visualize kmeans\n0 0\n1 1\n5 5\n6 6\n```\n");
        var kms = md.GetLogicalDescendants().OfType<KMeansView>().ToList();
        Assert.Equal(2, kms.Count);
        Assert.Contains("md-visualizer", kms[0].Classes);
        Assert.Equal(4, kms[0].ViewModel.Trace.K);
        Assert.Equal(KMeansInit.Random, kms[0].ViewModel.Trace.Init);
        Assert.Equal(7, kms[0].ViewModel.Trace.Seed);
        Assert.Equal(4, kms[1].ViewModel.Trace.Points.Count);
        var pca = Assert.Single(md.GetLogicalDescendants().OfType<PcaView>());
        Assert.Equal(4, pca.ViewModel.Trace.Result.Centered.Length);

        var variant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        var panel = new StackPanel();
        panel.Children.Add(new KMeansView());
        panel.Children.Add(new PcaView());
        var window = new Window { Width = 1000, Height = 1400, RequestedThemeVariant = variant, Content = new ScrollViewer { Content = panel } };
        window.Show();
        Settle();
        var km = panel.Children.OfType<KMeansView>().Single();
        var pv = panel.Children.OfType<PcaView>().Single();
        Assert.NotNull(km.GetVisualDescendants().OfType<Controls.KMeansPlotControl>().Single());
        Assert.NotNull(pv.GetVisualDescendants().OfType<Controls.PcaPlotControl>().Single());

        for (int i = 0; i <= km.ViewModel.StepCount; i++) { km.ViewModel.StepForwardCommand.Execute(null); Settle(); }
        km.ViewModel.K = 6; Settle();
        km.ViewModel.SelectedInit = km.ViewModel.Inits[0]; Settle();
        km.ViewModel.SeedText = "9"; km.ViewModel.ApplySeedCommand.Execute(null); Settle();
        km.ViewModel.StepForwardCommand.Execute(null); Settle();
        for (int i = 0; i <= pv.ViewModel.StepCount; i++) { pv.ViewModel.StepForwardCommand.Execute(null); Settle(); }
        pv.ViewModel.ResetCommand.Execute(null); Settle();
        window.Close();
        Assert.True(errors.Messages.Count == 0, string.Join("\n", errors.Messages.Distinct()));
    }
}
