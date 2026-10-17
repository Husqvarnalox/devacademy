using DevAcademy.Domain;

namespace DevAcademy.Execution;

// Shared contracts of the Code Execution Engine. See RUNTIME_ARCHITECTURE.md.
// This project knows nothing about lessons or exercises: it only compiles and
// runs source files under resource limits. Evaluation lives in DevAcademy.Application.

/// <summary>A source file to materialise in the working directory. Path is relative, '/'-separated.</summary>
public sealed record SourceFile(string Path, string Content);

/// <summary>Resource limits applied to a single process run (compile step or program run).</summary>
public sealed record ResourceLimits(
    TimeSpan WallTimeout,
    long MaxMemoryBytes,
    int MaxOutputBytes,
    bool AllowNetwork = false)
{
    public static ResourceLimits DefaultRun { get; } =
        new(TimeSpan.FromSeconds(5), 256L * 1024 * 1024, 64 * 1024);

    /// <summary>A whole test-tool run (build + tests), e.g. <c>go test</c> or <c>node --test</c>; the wall time is usually replaced per exercise.</summary>
    public static ResourceLimits DefaultCommand { get; } =
        new(TimeSpan.FromSeconds(20), 512L * 1024 * 1024, 1024 * 1024);

    public static ResourceLimits DefaultCompile { get; } =
        new(TimeSpan.FromSeconds(90), 2048L * 1024 * 1024, 256 * 1024);
}

/// <summary>Isolation actually requested. Runners must report what they really applied (see ExecutionResult.AppliedIsolation).</summary>
public enum IsolationLevel
{
    /// <summary>Plain child process: timeout, output cap, memory polling, process-tree kill. NOT a security boundary.</summary>
    ProcessLimits,
    /// <summary>ProcessLimits + OS sandbox (macOS sandbox-exec, Linux bubblewrap): no network, writes only to workdir.</summary>
    OsSandbox,
    /// <summary>Docker container: --network none, memory/pids/cpu limits, non-root, read-only rootfs.</summary>
    Container,
}

public enum ExecutionStatus
{
    Success,
    CompileError,
    RuntimeError,
    Timeout,
    MemoryLimitExceeded,
    OutputLimitExceeded,
    RuntimeMissing,
    Cancelled,
    InternalError,
}

/// <summary>Outcome of one OS process.</summary>
public sealed record ProcessOutcome(
    int? ExitCode,
    string Stdout,
    string Stderr,
    TimeSpan Duration,
    long? PeakMemoryBytes,
    bool TimedOut,
    bool MemoryLimitExceeded,
    bool OutputTruncated,
    bool Cancelled);

public sealed record ExecutionResult(
    ExecutionStatus Status,
    int? ExitCode,
    string Stdout,
    string Stderr,
    string CompileOutput,
    TimeSpan Duration,
    long? PeakMemoryBytes,
    IsolationLevel AppliedIsolation,
    string? Message = null,
    /// <summary>The same message as a translatable value; <see cref="Message"/> is its English fallback. Null when there is no message.</summary>
    LocalizedText? MessageText = null)
{
    public bool IsSuccess => Status == ExecutionStatus.Success;

    /// <summary>Copy with <paramref name="text"/> as the message (both the English string and the translatable value); null clears it.</summary>
    public ExecutionResult WithMessage(LocalizedText? text) =>
        this with { Message = text is { Fallback.Length: > 0 } ? text.Fallback : null, MessageText = text is { Fallback.Length: > 0 } ? text : null };
}

/// <summary>Description of one process to launch. Paths in Args may be relative to WorkingDirectory.</summary>
public sealed record ProcessSpec(
    string FileName,
    IReadOnlyList<string> Args,
    string WorkingDirectory,
    IReadOnlyDictionary<string, string>? Environment = null,
    /// <summary>Docker image to run in when the spec is executed by a container sandbox; FileName is then a bare command inside the image.</summary>
    string? ContainerImage = null,
    /// <summary>Raises the wall timeout of a compile step above ResourceLimits.DefaultCompile (never lowers it).</summary>
    TimeSpan? MinWallTimeout = null,
    /// <summary>Host paths the process may read but never write (a selected Python environment). Sandboxes that restrict writes also deny them explicitly, even if they sit inside an otherwise writable area.</summary>
    IReadOnlyList<string>? ReadOnlyPaths = null);

public enum RuntimeAvailability { Available, Missing, Unsupported }

public sealed record RuntimeStatus(
    string LanguageId,
    RuntimeAvailability Availability,
    string? Version,
    string? ExecutablePath,
    string? Notes,
    string? InstallHint,
    /// <summary>Non-null when the runtime works but something degrades the result (e.g. type-checking skipped); surfaced in ExecutionResult.Message.</summary>
    string? Warning = null,
    /// <summary>Translatable forms of <see cref="Notes"/>, <see cref="InstallHint"/> and <see cref="Warning"/>; the strings are their English fallbacks. Set them through the With* methods.</summary>
    LocalizedText? NotesText = null,
    LocalizedText? InstallHintText = null,
    LocalizedText? WarningText = null)
{
    public RuntimeStatus WithNotes(LocalizedText? text) => this with { Notes = text?.Fallback, NotesText = text };
    public RuntimeStatus WithInstallHint(LocalizedText? text) => this with { InstallHint = text?.Fallback, InstallHintText = text };
    public RuntimeStatus WithWarning(LocalizedText? text) => this with { Warning = text?.Fallback, WarningText = text };

    /// <summary><see cref="NotesText"/>, or the plain <see cref="Notes"/> as verbatim text; null when there are none.</summary>
    public LocalizedText? NotesMessage => NotesText ?? (string.IsNullOrWhiteSpace(Notes) ? null : LocalizedText.Literal(Notes));
    public LocalizedText? InstallHintMessage => InstallHintText ?? (string.IsNullOrWhiteSpace(InstallHint) ? null : LocalizedText.Literal(InstallHint));
    public LocalizedText? WarningMessage => WarningText ?? (string.IsNullOrWhiteSpace(Warning) ? null : LocalizedText.Literal(Warning));
}

/// <summary>
/// A language adapter that can run its own test tool. The executable is always the adapter's tool (argv[0] must equal
/// <see cref="CommandTool"/>); it is replaced by the detected path, so content cannot name other binaries.
/// </summary>
public interface ICommandRuntime : ILanguageRuntime
{
    /// <summary>The only accepted argv[0], e.g. "node" or "go".</summary>
    string CommandTool { get; }
    BuildPlan CreateCommandPlan(string workingDirectory, IReadOnlyList<string> argv, RuntimeStatus status);
}

/// <summary>Compile steps (possibly empty) followed by the run command.</summary>
public sealed record BuildPlan(IReadOnlyList<ProcessSpec> CompileSteps, ProcessSpec Run);

/// <summary>Language adapter. Implementations must not execute anything except in DetectAsync (version probe).</summary>
public interface ILanguageRuntime
{
    string LanguageId { get; }
    string DisplayName { get; }
    /// <summary>Default entry file name, e.g. "main.py", "Main.java".</summary>
    string DefaultEntryFile { get; }
    Task<RuntimeStatus> DetectAsync(CancellationToken ct);
    BuildPlan CreatePlan(string workingDirectory, string entryFile, RuntimeStatus status);
}

/// <summary>Launches a single process under limits with a specific isolation mechanism.</summary>
public interface IProcessSandbox
{
    IsolationLevel Level { get; }
    /// <summary>Whether this sandbox can be used on the current machine (e.g. sandbox-exec exists, docker daemon reachable).</summary>
    Task<bool> IsAvailableAsync(CancellationToken ct);
    Task<ProcessOutcome> RunAsync(ProcessSpec spec, ResourceLimits limits, string? stdin, CancellationToken ct);
}

/// <summary>A program whose files are written and compiled; can be run many times (once per test case).</summary>
public interface IPreparedProgram : IAsyncDisposable
{
    string LanguageId { get; }
    string WorkingDirectory { get; }
    /// <summary>Non-null when preparation failed (compile error, runtime missing...). RunAsync must not be called then.</summary>
    ExecutionResult? PreparationFailure { get; }
    string CompileOutput { get; }
    Task<ExecutionResult> RunAsync(string? stdin, ResourceLimits limits, CancellationToken ct,
        IReadOnlyDictionary<string, string>? extraEnvironment = null);

    /// <summary>
    /// Reads a text file the program produced in the working directory (e.g. a test report). Null when it does not exist,
    /// is a link, escapes the directory or is larger than <paramref name="maxBytes"/>.
    /// </summary>
    string? TryReadFile(string relativePath, int maxBytes);
}

/// <summary>A user-chosen Python virtual environment for a run: its interpreter replaces the auto-detected one and its directory is mounted read-only.</summary>
public sealed record PythonRunSelection(string InterpreterPath, string EnvironmentPath);

/// <summary>
/// The node content environment for a run: a directory that contains <c>node_modules</c> (installed by <c>npm ci</c> from
/// <c>tools/content-packages/node/&lt;set&gt;</c>). The runner exposes it as <c>node_modules</c> in the work directory through a symbolic link, and the OS
/// sandbox may read but never write it. Nothing is ever installed at run time and there is no network.
/// </summary>
public sealed record NodeRunSelection(string EnvironmentPath);

/// <summary>
/// The Go module cache of a go package set for a run (<c>GOMODCACHE</c>, filled by <c>go mod download</c> from
/// <c>tools/content-packages/go/&lt;set&gt;</c>). The build runs offline (<c>GOPROXY=off</c>) against it and the OS sandbox may read but never write it.
/// </summary>
public sealed record GoRunSelection(string ModCachePath);

/// <summary>
/// The NuGet packages folder of a nuget package set for a run (<c>NUGET_PACKAGES</c>, filled by <c>dotnet restore</c> from
/// <c>tools/content-packages/nuget/&lt;set&gt;/restore.csproj</c>). The build restores offline (the folder is the only package source) and the OS sandbox may
/// read but never write it.
/// </summary>
public sealed record NuGetRunSelection(string PackagesPath);

/// <summary>
/// The jar folder of a maven package set for a run: every jar the set's <c>lock.json</c> lists, downloaded and sha256-verified by an explicit install.
/// <c>javac</c> and <c>java</c> put <c>LibPath/*</c> on the class path and the OS sandbox may read but never write it. No Maven runs.
/// </summary>
public sealed record MavenRunSelection(string LibPath);

public sealed record ExecutionRequest(
    string LanguageId,
    IReadOnlyList<SourceFile> Files,
    string? EntryFile,
    IsolationLevel Isolation,
    /// <summary>Only honoured for language "python". A run with an environment never uses a container (the environment lives on the host).</summary>
    PythonRunSelection? Python = null,
    /// <summary>
    /// Run the language's test tool instead of the entry file: argv with the tool name first (see <see cref="ICommandRuntime"/>).
    /// Container isolation is not used for such runs.
    /// </summary>
    IReadOnlyList<string>? Command = null,
    /// <summary>Only honoured for javascript/typescript: exposes the node content environment as a read-only <c>node_modules</c>. Such a run never uses a container.</summary>
    NodeRunSelection? Node = null,
    /// <summary>Only honoured for language "go": builds against the module cache of a go package set, offline and read-only. Such a run never uses a container.</summary>
    GoRunSelection? Go = null,
    /// <summary>Only honoured for language "csharp": restores and builds against the packages folder of a nuget package set, offline and read-only. Such a run never uses a container.</summary>
    NuGetRunSelection? NuGet = null,
    /// <summary>Only honoured for language "java": compiles and runs against the jars of a maven package set, read-only. Such a run never uses a container.</summary>
    MavenRunSelection? Maven = null);

/// <summary>Entry point of the engine. Enforces a global concurrency limit and always cleans up working directories.</summary>
public interface ICodeRunner
{
    Task<IPreparedProgram> PrepareAsync(ExecutionRequest request, CancellationToken ct);

    /// <summary>Convenience: prepare + single run + dispose.</summary>
    Task<ExecutionResult> RunOnceAsync(ExecutionRequest request, string? stdin, ResourceLimits limits, CancellationToken ct);

    Task<IReadOnlyList<RuntimeStatus>> DetectRuntimesAsync(CancellationToken ct);
}
