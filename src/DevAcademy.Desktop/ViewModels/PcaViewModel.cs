using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevAcademy.Application.Visualization;
using DevAcademy.Desktop.Localization;

namespace DevAcademy.Desktop.ViewModels;

/// <summary>What one frame shows: one of the five PCA stages.</summary>
public sealed record PcaFrame(PcaTrace Trace, int Position)
{
    public PcaStep Current => Trace.Steps[Position];
}

/// <summary>View model of the PCA visualiser: <see cref="PcaLab"/> computes everything once, the user steps through the stages.</summary>
public sealed partial class PcaViewModel : ObservableObject
{
    public const int IntervalMs = 1600;

    private readonly PcaTrace _trace;

    /// <param name="points">Fewer than two points (or null) selects the built-in correlated dataset.</param>
    public PcaViewModel(IReadOnlyList<(double X, double Y)>? points = null)
    {
        _trace = PcaLab.Run(points is { Count: >= 2 } ? points : PointData.DefaultCorrelated());
        _frame = new PcaFrame(_trace, 0);
        Refresh();
    }

    [ObservableProperty] private PcaFrame _frame;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(PositionText), nameof(CanStepForward), nameof(CanStepBack), nameof(IsFinished))]
    [NotifyCanExecuteChangedFor(nameof(StepForwardCommand), nameof(StepBackCommand))]
    private int _position;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(PlayPauseText))] private bool _isPlaying;
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private string _detailText = "";

    public PcaTrace Trace => _trace;
    public int StepCount => _trace.Steps.Count - 1;
    public bool CanStepForward => Position < StepCount;
    public bool CanStepBack => Position > 0;
    public bool IsFinished => Position >= StepCount;
    public bool IsDegenerate => _trace.Result.IsDegenerate;
    public string PositionText => Loc.F("Viz_Position", Position, StepCount);
    public string PlayPauseText => Loc.T(IsPlaying ? "Viz_Pause" : "Viz_Play");
    public string VarianceText => IsDegenerate ? Loc.T("Pca_Degenerate")
        : Loc.F("Pca_Variance", Percent(_trace.Result.Ratio1), Percent(_trace.Result.Ratio2));

    partial void OnPositionChanged(int value) => Refresh();

    private static string Percent(double ratio) => (ratio * 100).ToString("0.0", CultureInfo.InvariantCulture) + " %";

    private static string Num(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    private void Refresh()
    {
        Frame = new PcaFrame(_trace, Position);
        PcaResult r = _trace.Result;
        PcaStage stage = Frame.Current.Stage;
        StatusText = Loc.T("Pca_Stage_" + stage);
        DetailText = stage switch
        {
            PcaStage.Centered => Loc.F("Pca_Explain_Centered", $"{Num(r.Mean.X)}; {Num(r.Mean.Y)}"),
            PcaStage.Axes => Loc.F("Pca_Explain_Axes", Num(r.Eigenvalue1), Num(r.Eigenvalue2)),
            PcaStage.Reconstruction => Loc.F("Pca_Explain_Reconstruction", Num(r.ReconstructionError)),
            _ => Loc.T("Pca_Explain_" + stage),
        };
    }

    [RelayCommand(CanExecute = nameof(CanStepForward))]
    private void StepForward() { if (Position < StepCount) Position++; if (IsFinished) IsPlaying = false; }

    [RelayCommand(CanExecute = nameof(CanStepBack))]
    private void StepBack() { IsPlaying = false; if (Position > 0) Position--; }

    [RelayCommand]
    private void PlayPause()
    {
        if (IsPlaying) { IsPlaying = false; return; }
        if (IsFinished) Position = 0;
        IsPlaying = true;
    }

    [RelayCommand]
    private void Reset() { IsPlaying = false; Position = 0; }

    public void Tick()
    {
        if (!IsPlaying) return;
        StepForward();
    }
}
