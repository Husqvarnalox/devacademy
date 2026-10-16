using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevAcademy.Application.Visualization;
using DevAcademy.Desktop.Localization;

namespace DevAcademy.Desktop.ViewModels;

/// <summary>What one frame shows: the trace up to <see cref="Position"/>. The centroid trail is the centroid positions of every earlier frame.</summary>
public sealed record KMeansFrame(KMeansTrace Trace, int Position)
{
    public KMeansStep Current => Trace.Steps[Position];
    public bool IsLast => Position >= Trace.Steps.Count - 1;
}

public sealed record KMeansInitOption(KMeansInit Kind, string Label)
{
    public override string ToString() => Label;
}

/// <summary>
/// View model of the k-means visualiser. The whole run is computed by <see cref="KMeansLab"/> whenever k, the initialisation or the seed changes;
/// playback only moves <see cref="Position"/> along the recorded frames.
/// </summary>
public sealed partial class KMeansViewModel : ObservableObject
{
    public const int MinSpeed = 1, MaxSpeed = 10, DefaultSpeed = 2;

    private readonly IReadOnlyList<(double X, double Y)> _points;
    private KMeansTrace _trace = null!;
    private bool _suspend;

    /// <param name="points">Fewer than two points (or null) selects the built-in three-blob dataset.</param>
    public KMeansViewModel(int k = 3, KMeansInit init = KMeansInit.KMeansPlusPlus, int seed = KMeansLab.DefaultSeed, IReadOnlyList<(double X, double Y)>? points = null)
    {
        _points = points is { Count: >= 2 } ? points : PointData.DefaultBlobs();
        Inits = Enum.GetValues<KMeansInit>().Select(i => new KMeansInitOption(i, Loc.T("Km_Init_" + i))).ToList();
        _suspend = true;
        _k = Math.Clamp(k, KMeansLab.MinK, KMeansLab.MaxK);
        _selectedInit = Inits.First(o => o.Kind == init);
        _seed = seed;
        _seedText = seed.ToString(CultureInfo.InvariantCulture);
        _suspend = false;
        Rebuild();
    }

    public IReadOnlyList<KMeansInitOption> Inits { get; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(KText))] private double _k;
    [ObservableProperty] private KMeansInitOption _selectedInit;
    [ObservableProperty] private int _seed;
    [ObservableProperty] private string _seedText;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasError))] private string? _error;
    [ObservableProperty] private KMeansFrame _frame = null!;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(PositionText), nameof(CanStepForward), nameof(CanStepBack), nameof(IsFinished))]
    [NotifyCanExecuteChangedFor(nameof(StepForwardCommand), nameof(StepBackCommand))]
    private int _position;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(PlayPauseText))] private bool _isPlaying;
    [ObservableProperty] private double _speed = DefaultSpeed;
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private string _detailText = "";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasResult))] private string _resultText = "";

    public bool HasError => Error is not null;
    public bool HasResult => ResultText.Length > 0;
    public string KText => Loc.F("Km_KValue", (int)Math.Round(K));
    public KMeansTrace Trace => _trace;
    public int StepCount => _trace.Steps.Count - 1;
    public bool CanStepForward => Position < StepCount;
    public bool CanStepBack => Position > 0;
    public bool IsFinished => Position >= StepCount;
    public string PositionText => Loc.F("Viz_Position", Position, StepCount);
    public string PlayPauseText => Loc.T(IsPlaying ? "Viz_Pause" : "Viz_Play");
    public int IntervalMs => (int)(1000 / Math.Clamp(Speed, MinSpeed, MaxSpeed));

    partial void OnSpeedChanged(double value) => OnPropertyChanged(nameof(IntervalMs));
    partial void OnKChanged(double value) { if (!_suspend) Rebuild(); }
    partial void OnSelectedInitChanged(KMeansInitOption value) { if (!_suspend) Rebuild(); }
    partial void OnPositionChanged(int value) => Refresh();

    private void Rebuild()
    {
        IsPlaying = false;
        int requested = (int)Math.Round(K);
        int k = Math.Min(requested, _points.Count);
        Error = k < requested ? Loc.F("Km_ErrK", k) : null;
        _trace = KMeansLab.Run(_points, Math.Max(k, KMeansLab.MinK), SelectedInit.Kind, Seed);
        OnPropertyChanged(nameof(StepCount));
        if (Position == 0) Refresh(); else Position = 0;
    }

    private void Refresh()
    {
        Frame = new KMeansFrame(_trace, Position);
        KMeansStep s = Frame.Current;
        if (s.Phase == KMeansPhase.Init)
        {
            StatusText = Loc.F("Km_StatusInit", Loc.T(_trace.Init == KMeansInit.Random ? "Km_StatusInitRandom" : "Km_StatusInitPlusPlus"));
            DetailText = "";
        }
        else
        {
            StatusText = Loc.F("Km_Status", s.Iteration, Loc.T("Km_Phase_" + s.Phase), s.Inertia!.Value.ToString("0.###", CultureInfo.InvariantCulture));
            DetailText = s.Phase == KMeansPhase.Assign ? Loc.F("Km_DetailAssign", s.Changed, _trace.Points.Count) : Loc.T("Km_DetailUpdate");
        }
        ResultText = !Frame.IsLast ? ""
            : _trace.Converged ? Loc.F("Km_Converged", _trace.Iterations, IterationWord(_trace.Iterations))
            : Loc.F("Km_NotConverged", _trace.Iterations);
    }

    /// <summary>Russian plural of "итерация" for the accusative after "за": 1 итерацию, 2-4 итерации, otherwise итераций.</summary>
    private static string IterationWord(int n) => (n % 10, n % 100) switch
    {
        (1, not 11) => Loc.T("Km_IterOne"),
        ( >= 2 and <= 4, not (>= 12 and <= 14)) => Loc.T("Km_IterFew"),
        _ => Loc.T("Km_IterMany"),
    };

    [RelayCommand(CanExecute = nameof(CanStepForward))]
    private void StepForward() { if (Position < StepCount) Position++; if (IsFinished) IsPlaying = false; }

    [RelayCommand(CanExecute = nameof(CanStepBack))]
    private void StepBack() { IsPlaying = false; if (Position > 0) Position--; }

    [RelayCommand]
    private void PlayPause()
    {
        if (IsPlaying) { IsPlaying = false; return; }
        if (IsFinished) Position = 0;
        if (StepCount > 0) IsPlaying = true;
    }

    [RelayCommand]
    private void Reset() { IsPlaying = false; Position = 0; }

    public void Tick()
    {
        if (!IsPlaying) return;
        StepForward();
    }

    /// <summary>Validates the seed field and re-runs from the new seed.</summary>
    [RelayCommand]
    private void ApplySeed()
    {
        if (!int.TryParse(SeedText.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int seed))
        {
            Error = Loc.T("Km_ErrSeed");
            return;
        }
        Seed = seed;
        Rebuild();
    }
}
