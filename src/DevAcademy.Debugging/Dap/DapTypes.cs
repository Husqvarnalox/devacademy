using System.Text.Json;
using System.Text.Json.Serialization;

namespace DevAcademy.Debugging.Dap;

/// <summary>The subset of the Debug Adapter Protocol this client speaks (https://microsoft.github.io/debug-adapter-protocol/).</summary>
public sealed record Capabilities(
    bool SupportsConfigurationDoneRequest = false,
    bool SupportsConditionalBreakpoints = false,
    bool SupportsHitConditionalBreakpoints = false,
    bool SupportsLogPoints = false,
    bool SupportsEvaluateForHovers = false,
    bool SupportsTerminateRequest = false,
    bool SupportsSetVariable = false);

public sealed record DapSource(string? Name = null, string? Path = null, int? SourceReference = null);

public sealed record DapBreakpoint(int? Id = null, bool Verified = false, int? Line = null, string? Message = null, DapSource? Source = null);

public sealed record DapThread(int Id, string Name);

public sealed record DapStackFrame(int Id, string Name, DapSource? Source, int Line, int Column = 0)
{
    /// <summary>Frames without a file on disk (runtime internals, assembly) cannot be shown in the editor.</summary>
    public bool HasFile => !string.IsNullOrEmpty(Source?.Path);
}

public sealed record DapScope(string Name, int VariablesReference, bool Expensive = false);

public sealed record DapVariable(string Name, string Value, string? Type = null, int VariablesReference = 0, string? EvaluateName = null,
    int? NamedVariables = null, int? IndexedVariables = null)
{
    public bool HasChildren => VariablesReference > 0;
}

public sealed record DapEvaluateResult(string Result, string? Type = null, int VariablesReference = 0);

/// <summary>A breakpoint request from the client side: 1-based line, optional condition / hit count / log message.</summary>
public sealed record SourceBreakpoint(int Line, string? Condition = null, string? HitCondition = null, string? LogMessage = null);

public sealed record StoppedEventArgs(string Reason, int? ThreadId, bool AllThreadsStopped, string? Description, string? Text, IReadOnlyList<int>? HitBreakpointIds);

public sealed record OutputEventArgs(string Category, string Text);

public sealed record BreakpointEventArgs(string Reason, DapBreakpoint Breakpoint);

/// <summary>An incoming DAP event with its raw body.</summary>
public sealed record DapEvent(string Name, JsonElement Body);

/// <summary>Failure of a request (the adapter answered success=false), or of the connection itself.</summary>
public sealed class DapException(string message, string? command = null) : Exception(message)
{
    public string? Command { get; } = command;
}

/// <summary>Reply to a request sent by the adapter (reverse request). Body is serialized as is.</summary>
public sealed record ReverseReply(bool Success, string? Message = null, object? Body = null)
{
    public static ReverseReply NotSupported(string command) => new(false, $"'{command}' is not supported by this client");
}

internal static class DapJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
    };

    public static T? Read<T>(JsonElement? body) =>
        body is { ValueKind: JsonValueKind.Object or JsonValueKind.Array } b ? b.Deserialize<T>(Options) : default;
}
