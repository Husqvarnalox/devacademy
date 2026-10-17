using System.Text.Json.Nodes;
using DevAcademy.Domain;
using DevAcademy.Notebooks.Nbformat;

namespace DevAcademy.Notebooks.Kernel;

public enum KernelState { NotStarted, Starting, Idle, Busy, Restarting, Dead, Stopped }

/// <summary>Something the kernel published on IOPub while a request ran.</summary>
public abstract record KernelEvent;

public sealed record OutputEvent(NotebookOutput Output) : KernelEvent;

/// <summary>clear_output: remove the outputs shown so far (wait=true: only when the next output arrives).</summary>
public sealed record ClearOutputEvent(bool Wait) : KernelEvent;

public sealed record ExecuteOptions
{
    /// <summary>Silent requests produce no IOPub output, do not count and are not stored in history (used for the variable inspector).</summary>
    public bool Silent { get; init; }
    public bool StoreHistory { get; init; } = true;
    public bool AllowStdin { get; init; }
    public JsonObject? UserExpressions { get; init; }
}

/// <summary>The execute_reply: "ok", "error" or "aborted"; <see cref="UserExpressions"/> maps name to <c>{status, data, metadata}</c>.</summary>
public sealed record ExecuteResult(string Status, int? ExecutionCount, string? ErrorName, string? ErrorValue, JsonObject? UserExpressions)
{
    public bool IsOk => Status == "ok";
}

public sealed class KernelDeadException(string message, LocalizedText? text = null) : Exception(message), ILocalizedMessage
{
    public KernelDeadException(LocalizedText text) : this(text.Fallback, text) { }

    public LocalizedText Text { get; } = text ?? LocalizedText.Literal(message);
}

/// <summary>A running Jupyter kernel, as seen by the notebook UI. Implemented by <see cref="KernelSession"/>; tests substitute fakes.</summary>
public interface IKernelClient : IAsyncDisposable
{
    KernelState State { get; }
    /// <summary>Why the kernel is dead (process exit code, lost heartbeat...), or null.</summary>
    string? DeadReason { get; }
    /// <summary>Translatable twin of <see cref="DeadReason"/>; null when none was recorded.</summary>
    LocalizedText? DeadReasonText => null;
    event Action<KernelState>? StateChanged;

    Task StartAsync(CancellationToken ct);

    /// <summary>
    /// Runs code. <paramref name="onEvent"/> is called from a background thread for every output as it arrives.
    /// Completes when the reply and the kernel's idle status for this request have both been received.
    /// </summary>
    /// <exception cref="KernelDeadException">The kernel died before the request finished.</exception>
    Task<ExecuteResult> ExecuteAsync(string code, ExecuteOptions? options, Action<KernelEvent>? onEvent, CancellationToken ct);

    /// <summary>Interrupts the running cell (SIGINT on POSIX, interrupt_request on the control channel elsewhere).</summary>
    Task InterruptAsync();

    /// <summary>Stops the kernel process and starts a fresh one (all variables are lost).</summary>
    Task RestartAsync(CancellationToken ct);

    /// <summary>Asks the kernel to exit (shutdown_request) and kills it when it does not.</summary>
    Task ShutdownAsync();
}
