using Avalonia.Logging;

namespace DevAcademy.Desktop.Tests.Support;

/// <summary>Collects Avalonia binding/XAML warnings so tests can fail on runtime binding errors.</summary>
public sealed class BindingErrors : ILogSink, IDisposable
{
    private readonly ILogSink? _previous = Logger.Sink;
    public List<string> Messages { get; } = [];

    public BindingErrors() => Logger.Sink = this;

    public bool IsEnabled(LogEventLevel level, string area) =>
        level >= LogEventLevel.Warning && area is LogArea.Binding or LogArea.Control or LogArea.Property;

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate) =>
        Messages.Add($"{level} {area}: {messageTemplate}");

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues)
    {
        var text = messageTemplate;
        try
        {
            for (var i = 0; i < propertyValues.Length; i++)
            {
                var idx = text.IndexOf('{');
                var end = idx >= 0 ? text.IndexOf('}', idx) : -1;
                if (idx < 0 || end < 0) break;
                text = text[..idx] + propertyValues[i] + text[(end + 1)..];
            }
        }
        catch { /* best effort */ }
        Messages.Add($"{level} {area}: {text}");
    }

    public void Dispose() => Logger.Sink = _previous;
}

/// <summary>
/// Avalonia's <see cref="Logger.Sink"/> is process-global, so tests that capture binding errors must not
/// run alongside other UI tests (they would swap the sink and log into each other's capture).
/// xUnit runs a collection with parallelization disabled on its own, after the parallel collections.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class BindingCaptureCollection
{
    public const string Name = "BindingCapture";
}
