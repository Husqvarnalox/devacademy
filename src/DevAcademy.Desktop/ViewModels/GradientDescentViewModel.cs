using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevAcademy.Application.Visualization;
using DevAcademy.Desktop.Localization;

namespace DevAcademy.Desktop.ViewModels;

/// <summary>What one frame shows: the trace up to <see cref="Position"/>. Nothing is drawn that is not a trace step.</summary>
public sealed record GdFrame(GdTrace Trace, int Position)
{
    public GdStep Current => Trace.Steps[Position];
    public bool IsLast => Position >= Trace.Steps.Count - 1;
}

public sealed record GdFunctionOption(string Id, string Label)
{
    public override string ToString() => Label;
}

public sealed record GdOptimizerOption(GdOptimizerKind Kind, string Label)
{
    public override string ToString() => Label;
}

/// <summary>
/// View model of the gradient-descent laboratory. The whole trace is computed by <see cref="GradientDescentLab"/> whenever an input changes;
/// playback only moves <see cref="Position"/> along it, so each frame is exactly one trace step.
/// </summary>
public sealed partial class GradientDescentViewModel : ObservableObject
{
    public const int MinSpeed = 1, MaxSpeed = 60, DefaultSpeed = 12;
    public const double MinLogLr = -4, MaxLogLr = 1;
    public const double MaxCoordinate = 100;

    private GdTrace _trace = null!;
    private bool _suspend;

    public GradientDescentViewModel(string functionId = GdFunctions.QuadraticId, GdOptimizerKind optimizer = GdOptimizerKind.Gd, double? learningRate = null)
    {
        Functions = GdFunctions.Ids.Select(id => new GdFunctionOption(id, Loc.T("Gd_Fn_" + id))).ToList();
        Optimizers = Enum.GetValues<GdOptimizerKind>().Select(k => new GdOptimizerOption(k, Loc.T("Gd_Opt_" + k))).ToList();
        _suspend = true;
        _selectedFunction = Functions.FirstOrDefault(f => f.Id == functionId) ?? Functions[0];
        _selectedOptimizer = Optimizers.First(o => o.Kind == optimizer);
        _logLearningRate = ToLog(learningRate ?? GdDefaults.LearningRate(_selectedFunction.Id, optimizer));
        _steps = GdDefaults.DefaultSteps(_selectedFunction.Id);
        GdFunction fn = CurrentFunction();
        _startX = fn.DefaultStart[0];
        _startY = fn.Dimension > 1 ? fn.DefaultStart[1] : 0;
        _startXText = Format(_startX);
        _startYText = Format(_startY);
        _suspend = false;
        Rebuild();
    }

    public IReadOnlyList<GdFunctionOption> Functions { get; }
    public IReadOnlyList<GdOptimizerOption> Optimizers { get; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsTwoDimensional), nameof(IsQuadratic), nameof(FormulaText))] private GdFunctionOption _selectedFunction;
    [ObservableProperty] private GdOptimizerOption _selectedOptimizer;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(LearningRate), nameof(LearningRateText))] private double _logLearningRate;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(StepsText))] private double _steps;
    [ObservableProperty] private double _conditioning = 10;
    [ObservableProperty] private double _startX;
    [ObservableProperty] private double _startY;
    [ObservableProperty] private string _startXText;
    [ObservableProperty] private string _startYText;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasError))] private string? _error;
    [ObservableProperty] private GdFrame _frame = null!;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(PositionText), nameof(CanStepForward), nameof(CanStepBack), nameof(IsFinished))]
    [NotifyCanExecuteChangedFor(nameof(StepForwardCommand), nameof(StepBackCommand))]
    private int _position;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(PlayPauseText))] private bool _isPlaying;
    [ObservableProperty] private double _speed = DefaultSpeed;
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private string _detailText = "";
    [ObservableProperty] private string _hintText = "";

    public bool IsTwoDimensional => SelectedFunction.Id != GdFunctions.DoubleWellId;
    public bool IsQuadratic => SelectedFunction.Id == GdFunctions.QuadraticId;
    public bool HasError => Error is not null;
    public string FormulaText => CurrentFunction().Formula;
    public double LearningRate => Math.Pow(10, LogLearningRate);
    public string LearningRateText => Loc.F("Gd_LrValue", LearningRate.ToString("0.#####", CultureInfo.InvariantCulture));
    public string StepsText => Loc.F("Gd_StepsValue", (int)Steps);
    public GdTrace Trace => _trace;
    public int StepCount => _trace.Steps.Count - 1;
    public bool CanStepForward => Position < StepCount;
    public bool CanStepBack => Position > 0;
    public bool IsFinished => Position >= StepCount;
    public string PositionText => Loc.F("Viz_Position", Position, StepCount);
    public string PlayPauseText => Loc.T(IsPlaying ? "Viz_Pause" : "Viz_Play");
    public int IntervalMs => (int)(1000 / Math.Clamp(Speed, MinSpeed, MaxSpeed));
    public GdFunction Function => _trace.Function;

    private GdFunction CurrentFunction() => GdFunctions.ById(SelectedFunction.Id, Conditioning);

    private static double ToLog(double lr) => Math.Clamp(Math.Log10(Math.Max(lr, 1e-12)), MinLogLr, MaxLogLr);

    private static string Format(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    partial void OnSpeedChanged(double value) => OnPropertyChanged(nameof(IntervalMs));

    partial void OnSelectedFunctionChanged(GdFunctionOption value)
    {
        if (_suspend) return;
        GdFunction fn = GdFunctions.ById(value.Id, Conditioning);
        _suspend = true;
        StartX = fn.DefaultStart[0];
        StartY = fn.Dimension > 1 ? fn.DefaultStart[1] : 0;
        StartXText = Format(StartX);
        StartYText = Format(StartY);
        LogLearningRate = ToLog(GdDefaults.LearningRate(value.Id, SelectedOptimizer.Kind));
        Steps = GdDefaults.DefaultSteps(value.Id);
        _suspend = false;
        Rebuild();
    }

    partial void OnSelectedOptimizerChanged(GdOptimizerOption value)
    {
        if (_suspend) return;
        _suspend = true;
        LogLearningRate = ToLog(GdDefaults.LearningRate(SelectedFunction.Id, value.Kind));
        _suspend = false;
        Rebuild();
    }

    partial void OnLogLearningRateChanged(double value) { if (!_suspend) Rebuild(); }
    partial void OnStepsChanged(double value) { if (!_suspend) Rebuild(); }
    partial void OnConditioningChanged(double value) { if (!_suspend) Rebuild(); }
    partial void OnPositionChanged(int value) => Refresh();

    private void Rebuild()
    {
        IsPlaying = false;
        GdFunction fn = CurrentFunction();
        var start = fn.Dimension > 1 ? new[] { StartX, StartY } : [StartX];
        _trace = GradientDescentLab.Run(new GdSettings
        {
            Function = fn,
            Optimizer = SelectedOptimizer.Kind,
            LearningRate = LearningRate,
            Start = start,
            MaxSteps = (int)Steps,
        });
        OnPropertyChanged(nameof(StepCount));
        OnPropertyChanged(nameof(FormulaText));
        OnPropertyChanged(nameof(Function));
        HintText = BuildHint(fn);
        if (Position == 0) Refresh(); else Position = 0;
    }

    private string BuildHint(GdFunction fn)
    {
        if (SelectedOptimizer.Kind == GdOptimizerKind.Gd && GradientDescentLab.StabilityLimit(fn) is { } limit)
            return Loc.F(LearningRate > limit ? "Gd_HintUnstable" : "Gd_HintStable", limit.ToString("0.####", CultureInfo.InvariantCulture));
        return "";
    }

    private void Refresh()
    {
        Frame = new GdFrame(_trace, Position);
        GdStep s = Frame.Current;
        string coords = string.Join("; ", s.Parameters.Select(Format));
        DetailText = Loc.F("Gd_Detail", s.Step, coords, s.Loss.ToString("0.#####e+0", CultureInfo.InvariantCulture), s.GradientNorm.ToString("0.#####e+0", CultureInfo.InvariantCulture));
        StatusText = !Frame.IsLast ? Loc.T("Gd_StatusRunning") : _trace.StopReason switch
        {
            GdStopReason.Converged => Loc.F("Gd_StatusConverged", s.Step),
            GdStopReason.Diverged => Loc.F("Gd_StatusDiverged", s.Step),
            _ => Loc.F("Gd_StatusMaxSteps", s.Step),
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
        if (StepCount > 0) IsPlaying = true;
    }

    [RelayCommand]
    private void Reset() { IsPlaying = false; Position = 0; }

    public void Tick()
    {
        if (!IsPlaying) return;
        StepForward();
    }

    /// <summary>Validates the numeric start fields and restarts from there.</summary>
    [RelayCommand]
    private void ApplyStart()
    {
        if (!TryParseCoordinate(StartXText, out double x) || (IsTwoDimensional && !TryParseCoordinate(StartYText, out _)))
        {
            Error = Loc.F("Gd_ErrStart", MaxCoordinate);
            return;
        }
        TryParseCoordinate(StartYText, out double y);
        Error = null;
        SetStart(x, IsTwoDimensional ? y : 0);
    }

    private static bool TryParseCoordinate(string text, out double value) =>
        double.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
        && double.IsFinite(value) && Math.Abs(value) <= MaxCoordinate;

    /// <summary>Sets the start point (clicking the plot or the numeric fields) and restarts the run.</summary>
    public void SetStart(double x, double y)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y)) return;   // a NaN from a plot click or a bad field must never reach the optimiser
        x = Math.Clamp(x, -MaxCoordinate, MaxCoordinate);
        y = Math.Clamp(y, -MaxCoordinate, MaxCoordinate);
        Error = null;
        _suspend = true;
        StartX = x;
        StartY = IsTwoDimensional ? y : 0;
        StartXText = Format(StartX);
        StartYText = Format(StartY);
        _suspend = false;
        Rebuild();
    }
}
