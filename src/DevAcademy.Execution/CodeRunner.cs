using System.Collections.Concurrent;
using System.Text;

namespace DevAcademy.Execution;

/// <summary>
/// Default <see cref="ICodeRunner"/>. The global concurrency slot is held while a single process runs
/// (each compile step, each program run), not for the lifetime of a prepared program.
/// <see cref="PrepareAsync"/> throws <see cref="ArgumentException"/> for unsafe file paths.
/// </summary>
public sealed class CodeRunner : ICodeRunner
{
    private readonly ExecutionOptions _options;
    private readonly Dictionary<string, ILanguageRuntime> _runtimes;
    private readonly IReadOnlyList<IProcessSandbox> _sandboxes;
    private readonly SemaphoreSlim _slots;
    private readonly ConcurrentDictionary<string, Task<RuntimeStatus>> _detected = new();

    public CodeRunner(ExecutionOptions options, IEnumerable<ILanguageRuntime> runtimes, IEnumerable<IProcessSandbox> sandboxes)
    {
        if (options.MaxConcurrentRuns < 1) throw new ArgumentOutOfRangeException(nameof(options), "MaxConcurrentRuns must be >= 1.");
        _options = options;
        _runtimes = runtimes.ToDictionary(r => r.LanguageId, StringComparer.Ordinal);
        _sandboxes = sandboxes.ToList();
        _slots = new SemaphoreSlim(options.MaxConcurrentRuns);
    }

    public async Task<IReadOnlyList<RuntimeStatus>> DetectRuntimesAsync(CancellationToken ct)
    {
        _detected.Clear();
        return await Task.WhenAll(_runtimes.Values.Select(r => DetectCachedAsync(r, ct))).ConfigureAwait(false);
    }

    private Task<RuntimeStatus> DetectCachedAsync(ILanguageRuntime runtime, CancellationToken ct)
    {
        // The shared detection task must not die with one caller's token, so it runs uncancelled and the caller just stops waiting.
        Task<RuntimeStatus> task = _detected.GetOrAdd(runtime.LanguageId, _ => Task.Run(() => runtime.DetectAsync(CancellationToken.None)));
        return task.WaitAsync(ct);
    }

    public async Task<ExecutionResult> RunOnceAsync(ExecutionRequest request, string? stdin, ResourceLimits limits, CancellationToken ct)
    {
        await using IPreparedProgram program = await PrepareAsync(request, ct).ConfigureAwait(false);
        return program.PreparationFailure ?? await program.RunAsync(stdin, limits, ct).ConfigureAwait(false);
    }

    public async Task<IPreparedProgram> PrepareAsync(ExecutionRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        foreach (SourceFile f in request.Files) ValidateRelativePath(f.Path);
        string entry = request.EntryFile ?? string.Empty;
        if (request.EntryFile is not null) ValidateRelativePath(request.EntryFile);

        if (!_runtimes.TryGetValue(request.LanguageId, out ILanguageRuntime? runtime))
            return Failed(request.LanguageId, ExecutionStatus.InternalError, $"Unknown language '{request.LanguageId}'.", IsolationLevel.ProcessLimits);
        if (request.EntryFile is null) entry = runtime.DefaultEntryFile;

        RuntimeStatus status = await DetectCachedAsync(runtime, ct).ConfigureAwait(false);
        var notes = new List<string>();
        if (status.Warning is not null) notes.Add(status.Warning);

        IsolationLevel requestedLevel = request.Isolation;
        string[]? readOnly = null;
        if (request.Python is { } py && runtime.LanguageId == "python")
        {
            string? problem = CheckPythonSelection(py);
            if (problem is not null) return Failed(request.LanguageId, ExecutionStatus.InternalError, problem, IsolationLevel.ProcessLimits);
            status = status with { Availability = RuntimeAvailability.Available, ExecutablePath = py.InterpreterPath, Notes = null, InstallHint = null };
            readOnly = [Path.GetFullPath(py.EnvironmentPath)];
            // The environment is on the host, so a container (which has its own Python) cannot use it.
            if (requestedLevel == IsolationLevel.Container)
            {
                requestedLevel = IsolationLevel.OsSandbox;
                notes.Add("A Python environment is selected, so the run uses OsSandbox instead of a container.");
            }
        }
        string? nodeModules = null;
        if (request.Node is { } nodeEnv)
        {
            if (runtime.LanguageId is not ("javascript" or "typescript"))
                return Failed(request.LanguageId, ExecutionStatus.InternalError, "A node environment only applies to JavaScript and TypeScript.", IsolationLevel.ProcessLimits);
            foreach (SourceFile f in request.Files)
                if (f.Path.Split('/', '\\')[0] == "node_modules")
                    throw new ArgumentException($"File path '{f.Path}' is inside node_modules, which is provided by the node environment.", nameof(request));
            string? nodeProblem = CheckNodeSelection(nodeEnv, out nodeModules);
            if (nodeProblem is not null) return Failed(request.LanguageId, ExecutionStatus.InternalError, nodeProblem, IsolationLevel.ProcessLimits);
            if (requestedLevel == IsolationLevel.Container)
            {
                requestedLevel = IsolationLevel.OsSandbox;
                notes.Add("A node environment is selected, so the run uses OsSandbox instead of a container.");
            }
        }
        ICommandRuntime? commandRuntime = null;
        if (request.Command is { } argv)
        {
            commandRuntime = runtime as ICommandRuntime;
            if (commandRuntime is null)
                return Failed(request.LanguageId, ExecutionStatus.InternalError, $"{runtime.DisplayName} has no test command support.", IsolationLevel.ProcessLimits);
            if (argv.Count == 0 || argv[0] != commandRuntime.CommandTool)
                return Failed(request.LanguageId, ExecutionStatus.InternalError,
                    $"Command '{(argv.Count == 0 ? "" : argv[0])}' is not allowed for {runtime.DisplayName} (only '{commandRuntime.CommandTool}').", IsolationLevel.ProcessLimits);
            if (requestedLevel == IsolationLevel.Container)
            {
                requestedLevel = IsolationLevel.OsSandbox;
                notes.Add("A test command runs on the host, so the run uses OsSandbox instead of a container.");
            }
        }
        (IProcessSandbox? sandbox, bool containerised) = await SelectSandboxAsync(requestedLevel, runtime, notes, ct).ConfigureAwait(false);
        IsolationLevel applied = sandbox?.Level ?? IsolationLevel.ProcessLimits;
        if (sandbox is null)
            return Failed(request.LanguageId, ExecutionStatus.InternalError, string.Join(" ", notes), applied);

        if (!containerised && status.Availability != RuntimeAvailability.Available)
        {
            string msg = $"{runtime.DisplayName} is not available: {status.Notes}" + (status.InstallHint is null ? "" : $" {status.InstallHint}");
            return Failed(request.LanguageId, ExecutionStatus.RuntimeMissing, msg.Trim(), applied);
        }

        Directory.CreateDirectory(_options.CacheDirectory);
        string workDir = CreateWorkDirectory(containerised);
        var program = new PreparedProgram(this, runtime.LanguageId, workDir, sandbox, applied, string.Join(" ", notes));
        try
        {
            if (nodeModules is not null)
            {
                try { Directory.CreateSymbolicLink(Path.Combine(workDir, "node_modules"), nodeModules); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
                {
                    await program.DisposeAsync().ConfigureAwait(false);
                    return Failed(request.LanguageId, ExecutionStatus.InternalError, $"Could not link the node environment into the work directory: {ex.Message}", applied);
                }
            }
            WriteFiles(workDir, request.Files);
            BuildPlan plan;
            if (commandRuntime is not null)
                plan = commandRuntime.CreateCommandPlan(workDir, request.Command!, status);
            else if (containerised)
            {
                BuildPlan raw = ((IContainerizableRuntime)runtime).CreateContainerPlan(workDir, entry);
                string image = _options.DockerImages[runtime.LanguageId];
                plan = new BuildPlan(raw.CompileSteps.Select(s => s with { ContainerImage = image }).ToList(), raw.Run with { ContainerImage = image });
            }
            else
            {
                plan = runtime.CreatePlan(workDir, entry, status);
                if (readOnly is not null) plan = plan with { Run = plan.Run with { ReadOnlyPaths = readOnly } };
            }
            if (nodeModules is not null)
            {
                // The link target must stay unwritable even if a sandbox allows writes somewhere above it.
                string[] ro = [.. plan.Run.ReadOnlyPaths ?? [], nodeModules];
                plan = new BuildPlan(plan.CompileSteps.Select(c => c with { ReadOnlyPaths = ro }).ToList(), plan.Run with { ReadOnlyPaths = ro });
            }
            await program.CompileAsync(plan, ct).ConfigureAwait(false);
        }
        catch
        {
            await program.DisposeAsync().ConfigureAwait(false);
            throw;
        }
        return program;
    }

    /// <summary>Walks Container, OsSandbox, ProcessLimits downwards from the requested level. Null sandbox means nothing acceptable exists.</summary>
    private async Task<(IProcessSandbox? Sandbox, bool Containerised)> SelectSandboxAsync(
        IsolationLevel requested, ILanguageRuntime runtime, List<string> notes, CancellationToken ct)
    {
        for (IsolationLevel level = requested; level >= IsolationLevel.ProcessLimits; level--)
        {
            // enum order: ProcessLimits(0) < OsSandbox(1) < Container(2)
            IProcessSandbox? sb = _sandboxes.FirstOrDefault(s => s.Level == level);
            string? problem = null;
            if (sb is null || !await sb.IsAvailableAsync(ct).ConfigureAwait(false))
                problem = $"{level} isolation is not available on this machine.";
            else if (level == IsolationLevel.Container && !(runtime is IContainerizableRuntime && _options.DockerImages.ContainsKey(runtime.LanguageId)))
                problem = $"Container isolation is not supported for {runtime.DisplayName}.";

            if (problem is null) return (sb, level == IsolationLevel.Container);
            if (!_options.AllowIsolationFallback)
            {
                notes.Add(problem + " Fallback is disabled.");
                return (null, false);
            }
            notes.Add(problem);
            if (level == IsolationLevel.ProcessLimits) break;
        }
        notes.Add("No sandbox could be selected.");
        return (null, false);
    }

    /// <summary>The environment must exist and must not be somewhere the sandbox makes writable (the run cache), or "read-only" would be meaningless.</summary>
    private string? CheckPythonSelection(PythonRunSelection py)
    {
        if (!File.Exists(py.InterpreterPath)) return $"The selected Python environment has no interpreter at '{py.InterpreterPath}'.";
        if (!Directory.Exists(py.EnvironmentPath)) return $"The selected Python environment '{py.EnvironmentPath}' does not exist.";
        string env = MacSandboxExecSandbox.RealPath(Path.GetFullPath(py.EnvironmentPath)).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        foreach (string writable in new[] { _options.CacheDirectory })
        {
            string w = MacSandboxExecSandbox.RealPath(Path.GetFullPath(writable)).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (env.StartsWith(w, StringComparison.Ordinal))
                return "The selected Python environment lies inside a writable sandbox area and cannot be mounted read-only.";
        }
        return null;
    }

    /// <summary>The environment must contain node_modules and must not be inside a writable sandbox area. On success <paramref name="nodeModules"/> is its real path.</summary>
    private string? CheckNodeSelection(NodeRunSelection node, out string? nodeModules)
    {
        nodeModules = null;
        string modules = Path.Combine(Path.GetFullPath(node.EnvironmentPath), "node_modules");
        if (!Directory.Exists(modules)) return $"The node environment '{node.EnvironmentPath}' has no node_modules (run `make node-env` or install the course packages in Settings).";
        string real = MacSandboxExecSandbox.RealPath(modules).TrimEnd(Path.DirectorySeparatorChar);
        string w = MacSandboxExecSandbox.RealPath(Path.GetFullPath(_options.CacheDirectory)).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if ((real + Path.DirectorySeparatorChar).StartsWith(w, StringComparison.Ordinal))
            return "The node environment lies inside a writable sandbox area and cannot be mounted read-only.";
        nodeModules = real;
        return null;
    }

    private static IPreparedProgram Failed(string languageId, ExecutionStatus status, string message, IsolationLevel applied) =>
        new FailedProgram(languageId, new ExecutionResult(status, null, "", "", "", TimeSpan.Zero, null, applied, message));

    internal static void ValidateRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Contains('\0'))
            throw new ArgumentException("File path is empty or contains a NUL character.", nameof(path));
        if (Path.IsPathRooted(path) || path.StartsWith('/') || path.StartsWith('\\') || (path.Length > 1 && path[1] == ':'))
            throw new ArgumentException($"File path '{path}' must be relative.", nameof(path));
        if (path.Split('/', '\\').Any(seg => seg == ".."))
            throw new ArgumentException($"File path '{path}' must not contain '..'.", nameof(path));
    }

    private static string CreateWorkDirectory(bool worldAccessible)
    {
        string path = Path.Combine(Path.GetTempPath(), "devacademy-run-" + Guid.NewGuid().ToString("N"));
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(path);
            Directory.CreateDirectory(Path.Combine(path, "tmp"));
            return path;
        }
        // Containers run as uid 65534, so they need to traverse and write the mount; the random name is the barrier there.
        UnixFileMode mode = worldAccessible
            ? UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute
            : UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
        Directory.CreateDirectory(path, mode);
        File.SetUnixFileMode(path, mode); // CreateDirectory applies the umask; this does not
        Directory.CreateDirectory(Path.Combine(path, "tmp"), mode);
        File.SetUnixFileMode(Path.Combine(path, "tmp"), mode);
        return path;
    }

    private static void WriteFiles(string workDir, IReadOnlyList<SourceFile> files)
    {
        string root = Path.GetFullPath(workDir) + Path.DirectorySeparatorChar;
        foreach (SourceFile f in files)
        {
            string full = Path.GetFullPath(Path.Combine(workDir, f.Path));
            if (!full.StartsWith(root, StringComparison.Ordinal))
                throw new ArgumentException($"File path '{f.Path}' escapes the working directory.", nameof(files));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, f.Content, new UTF8Encoding(false));
        }
    }

    private static ExecutionResult Map(ProcessOutcome o, string compileOutput, IsolationLevel applied, string? message)
    {
        ExecutionStatus status =
            o.Cancelled ? ExecutionStatus.Cancelled :
            o.TimedOut ? ExecutionStatus.Timeout :
            o.MemoryLimitExceeded ? ExecutionStatus.MemoryLimitExceeded :
            o.OutputTruncated ? ExecutionStatus.OutputLimitExceeded :
            o.ExitCode == 0 ? ExecutionStatus.Success : ExecutionStatus.RuntimeError;
        return new ExecutionResult(status, o.ExitCode, o.Stdout, o.Stderr, compileOutput, o.Duration, o.PeakMemoryBytes, applied, message);
    }

    private async Task<ProcessOutcome> RunLimitedAsync(IProcessSandbox sandbox, ProcessSpec spec, ResourceLimits limits, string? stdin, CancellationToken ct)
    {
        await _slots.WaitAsync(ct).ConfigureAwait(false);
        try { return await sandbox.RunAsync(spec, limits, stdin, ct).ConfigureAwait(false); }
        finally { _slots.Release(); }
    }

    private sealed class FailedProgram(string languageId, ExecutionResult failure) : IPreparedProgram
    {
        public string LanguageId => languageId;
        public string WorkingDirectory => "";
        public ExecutionResult? PreparationFailure => failure;
        public string CompileOutput => "";
        public Task<ExecutionResult> RunAsync(string? stdin, ResourceLimits limits, CancellationToken ct, IReadOnlyDictionary<string, string>? extraEnvironment = null) =>
            throw new InvalidOperationException("Preparation failed; check PreparationFailure.");
        public string? TryReadFile(string relativePath, int maxBytes) => null;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class PreparedProgram(CodeRunner owner, string languageId, string workDir, IProcessSandbox sandbox, IsolationLevel applied, string notes)
        : IPreparedProgram
    {
        private BuildPlan? _plan;
        private int _disposed;

        public string LanguageId => languageId;
        public string WorkingDirectory => workDir;
        public ExecutionResult? PreparationFailure { get; private set; }
        public string CompileOutput { get; private set; } = "";

        private string? Message => notes.Length == 0 ? null : notes;

        public async Task CompileAsync(BuildPlan plan, CancellationToken ct)
        {
            _plan = plan;
            var output = new StringBuilder();
            foreach (ProcessSpec step in plan.CompileSteps)
            {
                ProcessOutcome o;
                try { o = await owner.RunLimitedAsync(sandbox, step, CompileLimits(step), null, ct).ConfigureAwait(false); }
                catch (OperationCanceledException)
                {
                    Fail(ExecutionStatus.Cancelled, "Cancelled while waiting to compile.");
                    return;
                }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
                {
                    Fail(ExecutionStatus.InternalError, $"Could not start compile step: {ex.Message}");
                    return;
                }
                output.Append(o.Stdout).Append(o.Stderr);
                CompileOutput = output.ToString();
                if (o.Cancelled || o.TimedOut || o.MemoryLimitExceeded || o.OutputTruncated || o.ExitCode != 0)
                {
                    ExecutionResult r = Map(o, CompileOutput, applied, Message);
                    PreparationFailure = r.Status == ExecutionStatus.RuntimeError
                        ? r with { Status = ExecutionStatus.CompileError }
                        : r with { Message = $"Compilation: {r.Status}. {Message}".Trim() };
                    return;
                }
            }
        }

        private static ResourceLimits CompileLimits(ProcessSpec step) =>
            step.MinWallTimeout is { } t && t > ResourceLimits.DefaultCompile.WallTimeout
                ? ResourceLimits.DefaultCompile with { WallTimeout = t }
                : ResourceLimits.DefaultCompile;

        private void Fail(ExecutionStatus status, string message) =>
            PreparationFailure = new ExecutionResult(status, null, "", "", CompileOutput, TimeSpan.Zero, null, applied, $"{message} {Message}".Trim());

        public async Task<ExecutionResult> RunAsync(string? stdin, ResourceLimits limits, CancellationToken ct,
            IReadOnlyDictionary<string, string>? extraEnvironment = null)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            if (PreparationFailure is not null || _plan is null)
                throw new InvalidOperationException("Preparation failed; check PreparationFailure.");

            ProcessSpec spec = _plan.Run;
            if (extraEnvironment is { Count: > 0 })
            {
                var env = new Dictionary<string, string>(spec.Environment ?? new Dictionary<string, string>());
                foreach (var (k, v) in extraEnvironment) env[k] = v;
                spec = spec with { Environment = env };
            }
            try
            {
                ProcessOutcome o = await owner.RunLimitedAsync(sandbox, spec, limits, stdin, ct).ConfigureAwait(false);
                return Map(o, CompileOutput, applied, Message);
            }
            catch (OperationCanceledException)
            {
                return new ExecutionResult(ExecutionStatus.Cancelled, null, "", "", CompileOutput, TimeSpan.Zero, null, applied, Message);
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
            {
                return new ExecutionResult(ExecutionStatus.InternalError, null, "", "", CompileOutput, TimeSpan.Zero, null, applied,
                    $"Could not run program: {ex.Message} {Message}".Trim());
            }
        }

        public string? TryReadFile(string relativePath, int maxBytes)
        {
            try
            {
                ValidateRelativePath(relativePath);
                string root = Path.GetFullPath(workDir) + Path.DirectorySeparatorChar;
                string full = Path.GetFullPath(Path.Combine(workDir, relativePath));
                if (!full.StartsWith(root, StringComparison.Ordinal)) return null;
                var info = new FileInfo(full);
                if (!info.Exists || info.LinkTarget is not null || info.Length > maxBytes) return null;
                return new UTF8Encoding(false).GetString(File.ReadAllBytes(full));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { return null; }
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    if (Directory.Exists(workDir)) Directory.Delete(workDir, recursive: true);
                    return;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    await Task.Delay(100 * (attempt + 1)).ConfigureAwait(false);
                }
            }
            // Last attempt surfaces the problem instead of silently leaking the directory.
            if (Directory.Exists(workDir)) Directory.Delete(workDir, recursive: true);
        }
    }
}
