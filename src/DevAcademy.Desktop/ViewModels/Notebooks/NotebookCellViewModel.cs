using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DevAcademy.Desktop.Localization;
using DevAcademy.Notebooks.Kernel;
using DevAcademy.Notebooks.Nbformat;
using DevAcademy.Notebooks.Text;

namespace DevAcademy.Desktop.ViewModels.Notebooks;

/// <summary>One cell: the editable source, the execution counter and the outputs the kernel has produced so far.</summary>
public sealed partial class NotebookCellViewModel : ObservableObject
{
    /// <summary>Outputs beyond this many characters per cell are dropped (a runaway print loop must not freeze the UI).</summary>
    public const int MaxOutputChars = 1_000_000;

    private readonly Action _changed;
    private readonly Action<Action> _post;
    private readonly ConcurrentQueue<KernelEvent> _pending = new();
    private int _flushScheduled;
    private int _chars;
    private bool _clearOnNext;
    private bool _truncated;

    public NotebookCellViewModel(NotebookCell model, Action changed, Action<Action> post)
    {
        Model = model;
        _changed = changed;
        _post = post;
        _source = model.Source;
        _executionCount = model.ExecutionCount;
        _isEditing = model.Type == CellType.Markdown && string.IsNullOrWhiteSpace(model.Source);
        foreach (var o in model.Outputs) Outputs.Add(Present(o));
        _chars = model.Outputs.Sum(o => (o.Text?.Length ?? 0) + (o.Traceback.Sum(t => t.Length)));
    }

    public NotebookCell Model { get; }
    public ObservableCollection<NotebookOutputViewModel> Outputs { get; } = [];

    public bool IsCode => Model.Type == CellType.Code;
    public bool IsMarkdown => Model.Type == CellType.Markdown;
    public bool IsRaw => Model.Type == CellType.Raw;
    public string TypeLabel => Model.Type switch { CellType.Code => "Python", CellType.Markdown => "Markdown", _ => "Raw" };
    public string EditorFileName => IsRaw ? "cell.txt" : "cell.py";

    [ObservableProperty, NotifyPropertyChangedFor(nameof(EditorHeight), nameof(RenderedMarkdown), nameof(HasMath))] private string _source;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(Label))] private int? _executionCount;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(Label))] private bool _isRunning;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(Label))] private bool _isQueued;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(ShowEditor), nameof(ShowRendered), nameof(IsEditingMarkdown))] private bool _isEditing;
    [ObservableProperty] private bool _isSelected;

    /// <summary>"In [3]:", "In [*]:" while running, "In [ ]:" when it never ran; empty for markdown and raw cells.</summary>
    public string Label => !IsCode ? "" : IsRunning ? "[*]" : IsQueued ? "[…]" : ExecutionCount is { } n ? $"[{n}]" : "[ ]";
    public bool ShowEditor => !IsMarkdown || IsEditing;
    public bool IsEditingMarkdown => IsMarkdown && IsEditing;
    public bool ShowRendered => IsMarkdown && !IsEditing;
    public string RenderedMarkdown => IsMarkdown ? MarkdownMath.Prepare(Source) : "";
    public bool HasMath => IsMarkdown && Source.Contains("$", StringComparison.Ordinal);
    public bool HasOutputs => Outputs.Count > 0;

    /// <summary>The editor grows with its text (clamped) so that the cell list scrolls instead of every editor.</summary>
    public double EditorHeight => Math.Clamp((Source.Count(c => c == '\n') + 1) * 19.5 + 14, 54, 460);

    partial void OnSourceChanged(string value)
    {
        Model.Source = value;
        _changed();
    }

    partial void OnExecutionCountChanged(int? value) => Model.ExecutionCount = IsCode ? value : null;

    public void ToggleEditing() { if (IsMarkdown) IsEditing = !IsEditing; }

    // ---------------------------------------------------------------- outputs

    public void ClearOutputs()
    {
        Model.Outputs.Clear();
        Outputs.Clear();
        _chars = 0;
        _truncated = false;
        _clearOnNext = false;
        OnPropertyChanged(nameof(HasOutputs));
        _changed();
    }

    /// <summary>Called from the kernel's event thread; events are applied in batches on the UI thread.</summary>
    public void Receive(KernelEvent e)
    {
        _pending.Enqueue(e);
        if (Interlocked.Exchange(ref _flushScheduled, 1) == 0) _post(Flush);
    }

    /// <summary>Applies everything received so far (UI thread).</summary>
    public void Flush()
    {
        Volatile.Write(ref _flushScheduled, 0);
        while (_pending.TryDequeue(out var e)) Apply(e);
        // An event may have been enqueued after the last TryDequeue and before the flag reset; schedule again.
        if (!_pending.IsEmpty && Interlocked.Exchange(ref _flushScheduled, 1) == 0) _post(Flush);
    }

    private void Apply(KernelEvent e)
    {
        switch (e)
        {
            case ClearOutputEvent c:
                if (c.Wait) _clearOnNext = true;
                else ClearOutputs();
                break;
            case OutputEvent o:
                if (_clearOnNext) ClearOutputs();
                AddOutput(o.Output);
                break;
        }
    }

    private void AddOutput(NotebookOutput output)
    {
        if (_truncated) return;
        var size = (output.Text?.Length ?? 0) + output.Traceback.Sum(t => t.Length) + (output.GetText("text/plain")?.Length ?? 0);
        _chars += size;
        if (_chars > MaxOutputChars)
        {
            _truncated = true;
            var notice = NotebookOutput.StreamOutput("stderr", "\n" + Loc.T("Nb_OutputTruncated") + "\n");
            Model.Outputs.Add(notice);
            Outputs.Add(Present(notice));
            OnPropertyChanged(nameof(HasOutputs));
            _changed();
            return;
        }

        // nbformat convention: consecutive output of the same stream is one stream output.
        if (output.OutputType == NotebookOutput.Stream && Model.Outputs.Count > 0 && Model.Outputs[^1] is { OutputType: NotebookOutput.Stream } last && last.Name == output.Name)
        {
            last.Text += output.Text;
            Outputs[^1] = Present(last);
        }
        else
        {
            Model.Outputs.Add(output);
            Outputs.Add(Present(output));
        }
        OnPropertyChanged(nameof(HasOutputs));
        _changed();
    }

    private static NotebookOutputViewModel Present(NotebookOutput o) => new(OutputPresentation.For(o), o.Name);
}
