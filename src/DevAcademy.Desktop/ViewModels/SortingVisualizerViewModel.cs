using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevAcademy.Application.Visualization;
using DevAcademy.Desktop.Localization;

namespace DevAcademy.Desktop.ViewModels;

/// <summary>What one frame shows. Every frame is the state after applying the first <c>Position</c> trace steps; nothing else.</summary>
public sealed record SortFrame(
    IReadOnlyList<int> Values,
    IReadOnlyList<int> Compared,
    IReadOnlyList<int> Swapped,
    int? Written,
    int? Pivot,
    (int Lo, int Hi)? Range,
    IReadOnlySet<int> Sorted);

public sealed record SortAlgorithmOption(SortAlgorithm Algorithm, string Label)
{
    public override string ToString() => Label;
}

public sealed partial class SortingVisualizerViewModel : ObservableObject
{
    public static readonly IReadOnlyList<int> SampleArray = [5, 2, 9, 1, 7, 3, 8, 4, 6];
    public const int MinSpeed = 1, MaxSpeed = 30, DefaultSpeed = 4;

    private SortTrace _trace = null!;
    private int[] _comparisons = [], _swaps = [], _writes = [];

    public SortingVisualizerViewModel(SortAlgorithm algorithm = SortAlgorithm.Bubble, IReadOnlyList<int>? input = null)
    {
        Algorithms = Enum.GetValues<SortAlgorithm>().Select(a => new SortAlgorithmOption(a, Loc.T("Viz_Algo_" + a))).ToList();
        _selectedAlgorithm = Algorithms.First(a => a.Algorithm == algorithm);
        var start = input is { Count: >= SortInput.MinLength and <= SortInput.MaxLength } && input.All(v => v is >= SortInput.MinValue and <= SortInput.MaxValue)
            ? input : SampleArray;
        _inputText = string.Join(", ", start);
        Rebuild(start);
    }

    public IReadOnlyList<SortAlgorithmOption> Algorithms { get; }
    public ObservableCollection<string> Log { get; } = [];

    [ObservableProperty, NotifyPropertyChangedFor(nameof(AlgorithmName))] private SortAlgorithmOption _selectedAlgorithm;
    [ObservableProperty] private string _inputText;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasError))] private string? _error;
    [ObservableProperty] private SortFrame _frame = null!;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(PositionText), nameof(CanStepForward), nameof(CanStepBack), nameof(IsFinished))]
    [NotifyCanExecuteChangedFor(nameof(StepForwardCommand), nameof(StepBackCommand), nameof(PlayPauseCommand))]
    private int _position;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(PlayPauseText))] private bool _isPlaying;
    [ObservableProperty] private double _speed = DefaultSpeed;
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private string _comparisonsText = "";
    [ObservableProperty] private string _swapsText = "";
    [ObservableProperty] private string _writesText = "";

    public string AlgorithmName => SelectedAlgorithm.Label;
    public bool HasError => Error is not null;
    public int StepCount => _trace.Steps.Count;
    public SortTrace Trace => _trace;
    public bool CanStepForward => Position < StepCount;
    public bool CanStepBack => Position > 0;
    public bool IsFinished => Position >= StepCount;
    public string PositionText => Loc.F("Viz_Position", Position, StepCount);
    public string PlayPauseText => Loc.T(IsPlaying ? "Viz_Pause" : "Viz_Play");

    /// <summary>Milliseconds between frames while playing.</summary>
    public int IntervalMs => (int)(1000 / Math.Clamp(Speed, MinSpeed, MaxSpeed));

    partial void OnSpeedChanged(double value) => OnPropertyChanged(nameof(IntervalMs));

    partial void OnSelectedAlgorithmChanged(SortAlgorithmOption value) => Rebuild(_trace.Input);

    partial void OnPositionChanged(int value) => Refresh();

    private void Rebuild(IReadOnlyList<int> input)
    {
        IsPlaying = false;
        _trace = SortTraceGenerator.Generate(SelectedAlgorithm.Algorithm, input);
        var n = _trace.Steps.Count;
        _comparisons = new int[n + 1];
        _swaps = new int[n + 1];
        _writes = new int[n + 1];
        for (var k = 0; k < n; k++)
        {
            var kind = _trace.Steps[k].Kind;
            _comparisons[k + 1] = _comparisons[k] + (kind == StepKind.Compare ? 1 : 0);
            _swaps[k + 1] = _swaps[k] + (kind == StepKind.Swap ? 1 : 0);
            _writes[k + 1] = _writes[k] + (kind == StepKind.Write ? 1 : 0);
        }
        OnPropertyChanged(nameof(StepCount));
        if (Position == 0) Refresh(); else Position = 0;
    }

    private void Refresh()
    {
        var steps = _trace.Steps;
        var values = SortTraceReplayer.StateAt(_trace, Position);
        var sorted = new HashSet<int>();
        (int, int)? range = null;
        int? pivot = null;
        for (var k = 0; k < Position; k++)
        {
            var s = steps[k];
            switch (s.Kind)
            {
                case StepKind.MarkSorted: sorted.Add(s.I); if (pivot == s.I) pivot = null; break;
                case StepKind.Range: range = (s.I, s.J); break;
                case StepKind.Pivot: pivot = s.I; break;
            }
        }
        int[] compared = [], swapped = [];
        int? written = null;
        string status;
        if (Position == 0) status = Loc.T("Viz_StatusStart");
        else
        {
            var last = steps[Position - 1];
            switch (last.Kind)
            {
                case StepKind.Compare: compared = [last.I, last.J]; status = Loc.F("Viz_StatusCompare", last.I, last.J); break;
                case StepKind.Swap: swapped = [last.I, last.J]; status = Loc.F("Viz_StatusSwap", last.I, last.J); break;
                case StepKind.Write: written = last.I; status = Loc.F("Viz_StatusWrite", last.I, last.Value); break;
                case StepKind.MarkSorted: status = Loc.F("Viz_StatusSorted", last.I); break;
                case StepKind.Range: status = Loc.F("Viz_StatusRange", last.I, last.J); break;
                default: status = Loc.F("Viz_StatusPivot", last.I); break;
            }
        }
        if (IsFinished) { status = Loc.T("Viz_StatusDone"); for (var i = 0; i < values.Length; i++) sorted.Add(i); }
        Frame = new SortFrame(values, compared, swapped, written, pivot, range, sorted);
        StatusText = status;
        ComparisonsText = Loc.F("Viz_Comparisons", _comparisons[Position]);
        SwapsText = Loc.F("Viz_Swaps", _swaps[Position]);
        WritesText = Loc.F("Viz_Writes", _writes[Position]);
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

    /// <summary>Called by the view's timer; one tick = one trace step.</summary>
    public void Tick()
    {
        if (!IsPlaying) return;
        StepForward();
    }

    /// <summary>Validates the text box and, if valid, restarts the visualization on that array.</summary>
    [RelayCommand]
    private void ApplyInput()
    {
        if (!SortInput.TryParse(InputText, out var values, out var key))
        {
            Error = Loc.F(key ?? "Viz_ErrNotInt", SortInput.MinLength, SortInput.MaxLength, SortInput.MinValue, SortInput.MaxValue);
            return;
        }
        Error = null;
        InputText = string.Join(", ", values);
        Rebuild(values);
    }

    [RelayCommand]
    private void Randomize()
    {
        var values = SortInput.Random(System.Random.Shared, 12);
        InputText = string.Join(", ", values);
        Error = null;
        Rebuild(values);
    }
}
