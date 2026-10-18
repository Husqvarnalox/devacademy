using System.Text.RegularExpressions;
using DevAcademy.Domain;

namespace DevAcademy.Execution;

internal static class RuntimeFactory
{
    public static IReadOnlyList<ILanguageRuntime> CreateAll(string cacheDirectory) =>
    [
        new PythonRuntime(), new JavaScriptRuntime(), new TypeScriptRuntime(cacheDirectory), new GoRuntime(cacheDirectory),
        new JavaRuntime(), new CppRuntime(), new CSharpRuntime(cacheDirectory), new PhpRuntime(), new RustRuntime(), new KotlinRuntime(),
    ];
}

internal sealed class PythonRuntime : IContainerizableRuntime
{
    public string LanguageId => "python";
    public string DisplayName => "Python";
    public string DefaultEntryFile => "main.py";

    public async Task<RuntimeStatus> DetectAsync(CancellationToken ct)
    {
        LocalizedText hint = EngineMessages.PythonHint();
        foreach (string name in new[] { "python3", "python" })
        {
            if (ToolLocator.Find(name) is not { } path) continue;
            string? v = await ToolLocator.ProbeAsync(path, ["--version"], ct).ConfigureAwait(false); // rejects shims such as the Windows Store stub
            if (v is not null && v.StartsWith("Python 3", StringComparison.Ordinal))
                return new RuntimeStatus(LanguageId, RuntimeAvailability.Available, v["Python ".Length..], path, null, null);
        }
        return ToolLocator.Missing(LanguageId, EngineMessages.ToolNotFound("python3"), hint);
    }

    public BuildPlan CreatePlan(string workingDirectory, string entryFile, RuntimeStatus status) =>
        new([], Spec(status.ExecutablePath!, workingDirectory, entryFile));

    public BuildPlan CreateContainerPlan(string workingDirectory, string entryFile) =>
        new([], Spec("python3", workingDirectory, entryFile));

    // -I would drop the script directory from sys.path, which test harnesses rely on.
    private static ProcessSpec Spec(string exe, string wd, string entry) => new(
        exe, ["-E", "-s", "-B", "-X", "utf8", entry], wd,
        new Dictionary<string, string> { ["PYTHONDONTWRITEBYTECODE"] = "1", ["PYTHONIOENCODING"] = "utf-8" });
}

internal sealed class JavaScriptRuntime : IContainerizableRuntime, ICommandRuntime, IDebugBuildRuntime
{
    public string LanguageId => "javascript";
    public string DisplayName => "JavaScript (Node.js)";
    public string DefaultEntryFile => "main.js";

    public async Task<RuntimeStatus> DetectAsync(CancellationToken ct) => await NodeProbe.DetectAsync(LanguageId, ct).ConfigureAwait(false);

    public BuildPlan CreatePlan(string workingDirectory, string entryFile, RuntimeStatus status) =>
        new([], new ProcessSpec(status.ExecutablePath!, [entryFile], workingDirectory));

    public BuildPlan CreateContainerPlan(string workingDirectory, string entryFile) =>
        new([], new ProcessSpec("node", [entryFile], workingDirectory));

    public string CommandTool => "node";

    public BuildPlan CreateCommandPlan(string workingDirectory, IReadOnlyList<string> argv, RuntimeStatus status) =>
        new([], new ProcessSpec(status.ExecutablePath!, argv.Skip(1).ToList(), workingDirectory,
            new Dictionary<string, string> { ["NO_COLOR"] = "1" }));

    /// <summary>Nothing to build: node runs the entry file, given by absolute path so the debugger's breakpoints match the editor's files.</summary>
    public BuildPlan CreateDebugPlan(string workingDirectory, string entryFile, RuntimeStatus status, string? sourceDirectory = null) =>
        new([], new ProcessSpec(status.ExecutablePath!, [Path.GetFullPath(entryFile, sourceDirectory ?? workingDirectory)], workingDirectory));
}

internal static class NodeProbe
{
    public static LocalizedText Hint => EngineMessages.NodeHint();

    public static async Task<RuntimeStatus> DetectAsync(string languageId, CancellationToken ct)
    {
        if (ToolLocator.Find("node") is not { } node) return ToolLocator.Missing(languageId, EngineMessages.ToolNotFound("node"), Hint);
        string? v = await ToolLocator.ProbeAsync(node, ["--version"], ct).ConfigureAwait(false);
        if (v is null) return ToolLocator.Missing(languageId, EngineMessages.ToolNotWorking("node"), Hint);
        LocalizedText managers = PackageManagerDetector.Describe(await PackageManagerDetector.DetectAsync(ct).ConfigureAwait(false));
        return new RuntimeStatus(languageId, RuntimeAvailability.Available, v.TrimStart('v'), node, null, null).WithNotes(managers);
    }
}

internal sealed class TypeScriptRuntime(string cacheDirectory) : ILanguageRuntime, ICommandRuntime, IDebugBuildRuntime
{

    public string LanguageId => "typescript";
    public string DisplayName => "TypeScript";
    public string DefaultEntryFile => "main.ts";

    public async Task<RuntimeStatus> DetectAsync(CancellationToken ct)
    {
        RuntimeStatus node = await NodeProbe.DetectAsync(LanguageId, ct).ConfigureAwait(false);
        if (node.Availability != RuntimeAvailability.Available) return node;
        if (!Version.TryParse(node.Version, out Version? nv) || nv < new Version(22, 6))
            return node.WithNotes(EngineMessages.NodeTooOldForTs(node.Version!)).WithInstallHint(NodeProbe.Hint) with { Availability = RuntimeAvailability.Unsupported };

        LocalizedText stripping = EngineMessages.TsStripping();
        string? tscVersion = ToolLocator.Find("tsc") is { } tsc
            ? await ToolLocator.ProbeAsync(tsc, ["--version"], ct).ConfigureAwait(false)
            : null;
        if (tscVersion is null)
            return node.WithNotes(LocalizedText.Join(" ", EngineMessages.TsNoChecker(), stripping, node.NotesMessage))
                .WithInstallHint(EngineMessages.TscHint())
                .WithWarning(EngineMessages.TsNoCheckerWarning());
        if (!TypeScriptTypes.IsProvisioned(cacheDirectory))
            return node.WithNotes(LocalizedText.Join(" ", EngineMessages.TsNoTypes(tscVersion), stripping, node.NotesMessage))
                .WithInstallHint(null)
                .WithWarning(EngineMessages.TsNoTypesWarning());
        return node.WithNotes(LocalizedText.Join(" ", EngineMessages.TsCheckerEnabled(tscVersion), stripping, node.NotesMessage)).WithInstallHint(null);
    }

    /// <summary>Test commands run on plain node (Vitest from the node content environment); type-checking is part of the tests.</summary>
    public string CommandTool => "node";

    public BuildPlan CreateCommandPlan(string workingDirectory, IReadOnlyList<string> argv, RuntimeStatus status) =>
        new([], new ProcessSpec(status.ExecutablePath!, argv.Skip(1).ToList(), workingDirectory,
            new Dictionary<string, string> { ["NO_COLOR"] = "1" }));

    /// <summary>
    /// Node runs the .ts file through type stripping, which blanks the types instead of moving code, so lines and columns stay those of the
    /// source and breakpoints need no source map. No type-check step: a program with type errors is still debuggable.
    /// </summary>
    public BuildPlan CreateDebugPlan(string workingDirectory, string entryFile, RuntimeStatus status, string? sourceDirectory = null) =>
        new([], new ProcessSpec(status.ExecutablePath!, [.. StripTypesFlags(status), Path.GetFullPath(entryFile, sourceDirectory ?? workingDirectory)], workingDirectory));

    private static List<string> StripTypesFlags(RuntimeStatus status)
    {
        var flags = new List<string>();
        if (Version.Parse(status.Version!) < new Version(23, 6)) flags.Add("--experimental-strip-types");
        flags.Add("--disable-warning=ExperimentalWarning");
        return flags;
    }

    public BuildPlan CreatePlan(string workingDirectory, string entryFile, RuntimeStatus status)
    {
        var args = StripTypesFlags(status);
        args.Add(entryFile);

        var steps = new List<ProcessSpec>();
        // The provisioned check is live, so provisioning takes effect without re-detection.
        if (ToolLocator.Find("tsc") is { } tsc && TypeScriptTypes.IsProvisioned(cacheDirectory))
        {
            var tscArgs = new List<string>
            {
                "--noEmit", "--strict", "--target", "es2022", "--module", "nodenext",
                "--types", "node", "--typeRoots", TypeScriptTypes.TypeRoot(cacheDirectory),
            };
            tscArgs.AddRange(ToolLocator.FilesWithExtensions(workingDirectory, ".ts"));
            steps.Add(new ProcessSpec(tsc, tscArgs, workingDirectory));
        }
        return new BuildPlan(steps, new ProcessSpec(status.ExecutablePath!, args, workingDirectory));
    }
}

internal sealed class GoRuntime(string cacheDirectory) : ICommandRuntime, INativeDebugBuild
{
    public string LanguageId => "go";
    public string DisplayName => "Go";
    public string DefaultEntryFile => "main.go";

    public async Task<RuntimeStatus> DetectAsync(CancellationToken ct)
    {
        if (ToolLocator.Find("go") is not { } go)
            return ToolLocator.Missing(LanguageId, EngineMessages.ToolNotFound("go"), EngineMessages.GoHint());
        string? v = await ToolLocator.ProbeAsync(go, ["version"], ct).ConfigureAwait(false);
        var m = v is null ? null : Regex.Match(v, @"go version go(\S+)");
        return m is { Success: true }
            ? new RuntimeStatus(LanguageId, RuntimeAvailability.Available, m.Groups[1].Value, go, null, null)
            : ToolLocator.Missing(LanguageId, EngineMessages.ToolNotWorking("go"), EngineMessages.GoWorkingHint());
    }

    public string CommandTool => "go";

    public BuildPlan CreateCommandPlan(string workingDirectory, IReadOnlyList<string> argv, RuntimeStatus status) =>
        new([], new ProcessSpec(status.ExecutablePath!, argv.Skip(1).ToList(), workingDirectory, BuildEnvironment(workingDirectory)));

    /// <summary>
    /// Environment that points the toolchain at a go package set's module cache: modules resolve only from there (no proxy, no checksum database).
    /// The cache itself is mounted read-only by the caller; GOCACHE and GOPATH stay in the writable run cache.
    /// </summary>
    internal static IReadOnlyDictionary<string, string> ModCacheEnvironment(string modCache) => new Dictionary<string, string>
    {
        ["GOMODCACHE"] = modCache,
        ["GOPROXY"] = "off",
        ["GOSUMDB"] = "off",
        ["GOFLAGS"] = "-mod=mod",
        ["CGO_ENABLED"] = "0",
    };

    private Dictionary<string, string> BuildEnvironment(string workingDirectory)
    {
        string go = Path.Combine(cacheDirectory, "go");
        return new Dictionary<string, string>
        {
            ["GOCACHE"] = Path.Combine(go, "build"),
            ["GOPATH"] = Path.Combine(go, "path"),
            ["GOTMPDIR"] = Path.Combine(workingDirectory, "tmp"),
            ["GOFLAGS"] = "-mod=mod",
            ["GOTOOLCHAIN"] = "local",
            ["CGO_ENABLED"] = "0",
            ["GOPROXY"] = "off",
        };
    }

    public BuildPlan CreatePlan(string workingDirectory, string entryFile, RuntimeStatus status)
    {
        var env = BuildEnvironment(workingDirectory);
        var args = new List<string> { "build", "-o", ToolLocator.ProgramName };
        args.AddRange(ToolLocator.FilesWithExtensions(workingDirectory, ".go").Where(f => !f.EndsWith("_test.go", StringComparison.Ordinal)));
        return new BuildPlan(
            [new ProcessSpec(status.ExecutablePath!, args, workingDirectory, env)],
            new ProcessSpec(Path.Combine(workingDirectory, ToolLocator.ProgramName), [], workingDirectory));
    }

    /// <summary>Same build with optimisation and inlining off so breakpoints and locals behave. The normal plan is unchanged.</summary>
    public BuildPlan CreateDebugPlan(string workingDirectory, string entryFile, RuntimeStatus status, string? sourceDirectory = null)
    {
        var env = BuildEnvironment(workingDirectory);
        var args = new List<string> { "build", "-gcflags=all=-N -l", "-o", ToolLocator.ProgramName };
        args.AddRange(ToolLocator.FilesWithExtensions(sourceDirectory ?? workingDirectory, ".go").Select(f => sourceDirectory is null ? f : Path.GetFullPath(f, sourceDirectory)).Where(f => !f.EndsWith("_test.go", StringComparison.Ordinal)));
        return new BuildPlan(
            [new ProcessSpec(status.ExecutablePath!, args, workingDirectory, env)],
            new ProcessSpec(Path.Combine(workingDirectory, ToolLocator.ProgramName), [], workingDirectory));
    }
}

internal sealed class JavaRuntime : ILanguageRuntime, IDebugBuildRuntime
{
    public string LanguageId => "java";
    public string DisplayName => "Java";
    public string DefaultEntryFile => "Main.java";

    public async Task<RuntimeStatus> DetectAsync(CancellationToken ct)
    {
        LocalizedText hint = EngineMessages.JdkHint();
        if (ToolLocator.Find("javac") is not { } javac || ToolLocator.Find("java") is not { } java)
            return ToolLocator.Missing(LanguageId, EngineMessages.JdkNotFound(), hint);
        // On macOS /usr/bin/java is a shim that fails without a JDK, hence the probes.
        string? jv = await ToolLocator.ProbeAsync(javac, ["-version"], ct).ConfigureAwait(false);
        string? rv = await ToolLocator.ProbeAsync(java, ["-version"], ct).ConfigureAwait(false);
        if (jv is null || rv is null) return ToolLocator.Missing(LanguageId, EngineMessages.JdkNotWorking(), hint);
        // ExecutablePath is javac; java is resolved again when planning.
        return new RuntimeStatus(LanguageId, RuntimeAvailability.Available, jv.Replace("javac ", ""), javac, null, null);
    }

    public BuildPlan CreatePlan(string workingDirectory, string entryFile, RuntimeStatus status)
    {
        var args = new List<string> { "-encoding", "UTF-8", "-d", "out" };
        args.AddRange(ToolLocator.FilesWithExtensions(workingDirectory, ".java"));
        string java = Path.Combine(Path.GetDirectoryName(status.ExecutablePath!)!, OperatingSystem.IsWindows() ? "java.exe" : "java");
        string mainClass = Path.ChangeExtension(entryFile, null)!.Replace('\\', '.').Replace('/', '.');
        return new BuildPlan(
            [new ProcessSpec(status.ExecutablePath!, args, workingDirectory)],
            new ProcessSpec(java, ["-XX:+UseSerialGC", "-Xss16m", .. ToolLocator.JvmEncoding, "-cp", "out", mainClass], workingDirectory));
    }

    /// <summary>javac -g (line numbers, local variable names) into <c>out</c> with absolute source paths; the run command is the one the JDI adapter launches.</summary>
    public BuildPlan CreateDebugPlan(string workingDirectory, string entryFile, RuntimeStatus status, string? sourceDirectory = null)
    {
        string src = sourceDirectory ?? workingDirectory;
        var args = new List<string> { "-g", "-encoding", "UTF-8", "-d", "out" };
        args.AddRange(ToolLocator.FilesWithExtensions(src, ".java").Select(f => Path.GetFullPath(f, src)));
        string java = Path.Combine(Path.GetDirectoryName(status.ExecutablePath!)!, OperatingSystem.IsWindows() ? "java.exe" : "java");
        string mainClass = Path.ChangeExtension(entryFile, null)!.Replace('\\', '.').Replace('/', '.');
        return new BuildPlan(
            [new ProcessSpec(status.ExecutablePath!, args, workingDirectory)],
            new ProcessSpec(java, ["-XX:+UseSerialGC", "-Xss16m", .. ToolLocator.JvmEncoding, "-cp", Path.Combine(workingDirectory, "out"), mainClass], workingDirectory));
    }

    /// <summary>Heap cap of a run with a jar folder: the JVM would otherwise grow towards a quarter of the machine's memory, past the run's memory limit.</summary>
    internal const string LibraryHeap = "-Xmx256m";

    /// <summary>
    /// The plan with a maven package set's jar folder on the class path (<c>lib/*</c> is expanded by the tools themselves, no shell is involved).
    /// <c>-parameters</c> keeps parameter names for reflection, as Spring Boot's build plugins do, so <c>@PathVariable</c> needs no explicit name.
    /// The folder is read-only for every step.
    /// </summary>
    public BuildPlan CreateLibraryPlan(string workingDirectory, string entryFile, RuntimeStatus status, string libPath)
    {
        string wildcard = Path.Combine(libPath, "*");
        string[] readOnly = [libPath];
        string java = Path.Combine(Path.GetDirectoryName(status.ExecutablePath!)!, OperatingSystem.IsWindows() ? "java.exe" : "java");
        string mainClass = Path.ChangeExtension(entryFile, null)!.Replace('\\', '.').Replace('/', '.');
        var args = new List<string> { "-encoding", "UTF-8", "-parameters", "-d", "out", "-cp", wildcard };
        args.AddRange(ToolLocator.FilesWithExtensions(workingDirectory, ".java"));
        return new BuildPlan(
            [new ProcessSpec(status.ExecutablePath!, args, workingDirectory, ReadOnlyPaths: readOnly)],
            new ProcessSpec(java, ["-XX:+UseSerialGC", "-Xss16m", .. ToolLocator.JvmEncoding, LibraryHeap, "-cp", "out" + Path.PathSeparator + wildcard, mainClass], workingDirectory, ReadOnlyPaths: readOnly));
    }
}

internal sealed class CppRuntime : ILanguageRuntime, INativeDebugBuild
{
    public string LanguageId => "cpp";
    public string DisplayName => "C++";
    public string DefaultEntryFile => "main.cpp";

    public async Task<RuntimeStatus> DetectAsync(CancellationToken ct)
    {
        // Several compilers can be installed, and the first one is not always the best: clang++ on Ubuntu uses libstdc++, whose <expected>
        // needs a concepts feature-test macro that clang 18 does not define, so only g++ builds the std::expected examples there.
        // Prefer a compiler that builds everything; otherwise the best of the rest (C++23 without std::expected, then C++20), the first one on a tie.
        RuntimeStatus? best = null;
        RuntimeStatus? withoutHeaders = null;
        foreach (string name in new[] { "clang++", "g++" })
        {
            if (ToolLocator.Find(name) is not { } path) continue;
            string? v = await ToolLocator.ProbeAsync(path, ["--version"], ct).ConfigureAwait(false); // fails for the Xcode shim without Command Line Tools
            if (v is null) continue;
            // A driver without a standard library cannot build anything: clang++ on Windows finds no <iostream> unless Visual Studio's
            // build tools are installed. Skip it so another installed compiler (MinGW g++) is used, and say why if none works.
            if (!await ToolLocator.SucceedsAsync(path, ["-x", "c++", "-fsyntax-only", "-"], "#include <iostream>\nint main() {}\n", ct).ConfigureAwait(false))
            {
                withoutHeaders ??= new RuntimeStatus(LanguageId, RuntimeAvailability.Unsupported, v, path, null, null).WithNotes(EngineMessages.CppNoStandardLibrary()).WithInstallHint(EngineMessages.CppHint());
                continue;
            }
            // Preprocess-only probes (-dM -E on empty stdin prints the predefined macros): cheap, need no temp file, and fail exactly when the driver rejects the -std flag.
            if (await AcceptsAsync(path, Cpp23, ct).ConfigureAwait(false))
            {
                if (await HasExpectedAsync(path, ct).ConfigureAwait(false))
                    return new RuntimeStatus(LanguageId, RuntimeAvailability.Available, v, path, null, null, LanguageStandard: Cpp23);
                best = Better(best, new RuntimeStatus(LanguageId, RuntimeAvailability.Available, v, path, null, null, LanguageStandard: Cpp23)
                    .WithNotes(EngineMessages.CppNoExpected()).WithWarning(EngineMessages.CppNoExpected()).WithInstallHint(EngineMessages.CppHint()));
                continue;
            }
            // An older compiler still runs most of the courses: build as C++20 and say once per run what that costs (C++23-only examples fail to compile).
            best = Better(best, await AcceptsAsync(path, Cpp20, ct).ConfigureAwait(false)
                ? new RuntimeStatus(LanguageId, RuntimeAvailability.Available, v, path, null, null, LanguageStandard: Cpp20)
                    .WithNotes(EngineMessages.CppFallback20()).WithWarning(EngineMessages.CppFallback20()).WithInstallHint(EngineMessages.CppHint())
                : new RuntimeStatus(LanguageId, RuntimeAvailability.Unsupported, v, path, null, null).WithNotes(EngineMessages.CppUnsupported()).WithInstallHint(EngineMessages.CppHint()));
        }
        return best ?? withoutHeaders ?? ToolLocator.Missing(LanguageId, EngineMessages.CppNotFound(), EngineMessages.CppHint());
    }

    private static RuntimeStatus Better(RuntimeStatus? current, RuntimeStatus candidate) =>
        current is null || Rank(candidate) > Rank(current) ? candidate : current;

    private static int Rank(RuntimeStatus s) => s.Availability != RuntimeAvailability.Available ? 0 : s.LanguageStandard == Cpp23 ? 2 : 1;

    /// <summary>A syntax-only compile of a program that uses <c>std::expected</c> (stdin, no temp file): true when the compiler and its standard library provide it.</summary>
    private static async Task<bool> HasExpectedAsync(string compiler, CancellationToken ct) =>
        await ToolLocator.SucceedsAsync(compiler, ["-x", "c++", "-std=" + Cpp23, "-fsyntax-only", "-"],
            "#include <expected>\nstd::expected<int, int> f() { return std::unexpected(1); }\n", ct).ConfigureAwait(false);

    internal const string Cpp23 = "c++23";
    internal const string Cpp20 = "c++20";

    private static async Task<bool> AcceptsAsync(string compiler, string standard, CancellationToken ct) =>
        await ToolLocator.ProbeAsync(compiler, ["-x", "c++", "-std=" + standard, "-dM", "-E", "-"], ct).ConfigureAwait(false) is not null;

    /// <summary>The detected standard; C++23 when the status does not say (a status built by hand, e.g. in tests).</summary>
    internal static string StdFlag(RuntimeStatus status) => "-std=" + (status.LanguageStandard ?? Cpp23);

    public BuildPlan CreatePlan(string workingDirectory, string entryFile, RuntimeStatus status)
    {
        var args = new List<string> { StdFlag(status), "-pthread", "-O2", "-Wall", "-Wextra", "-o", ToolLocator.ProgramName };
        args.AddRange(ToolLocator.FilesWithExtensions(workingDirectory, ".cpp", ".cc", ".cxx"));
        return new BuildPlan(
            [new ProcessSpec(status.ExecutablePath!, args, workingDirectory)],
            new ProcessSpec(Path.Combine(workingDirectory, ToolLocator.ProgramName), [], workingDirectory));
    }

    /// <summary>-g -O0 and absolute source paths, so DWARF file names match what the editor sends as breakpoint paths.</summary>
    public BuildPlan CreateDebugPlan(string workingDirectory, string entryFile, RuntimeStatus status, string? sourceDirectory = null)
    {
        string src = sourceDirectory ?? workingDirectory;
        var args = new List<string> { StdFlag(status), "-pthread", "-g", "-O0", "-Wall", "-Wextra", "-o", ToolLocator.ProgramName };
        args.AddRange(ToolLocator.FilesWithExtensions(src, ".cpp", ".cc", ".cxx").Select(f => Path.GetFullPath(f, src)));
        return new BuildPlan(
            [new ProcessSpec(status.ExecutablePath!, args, workingDirectory)],
            new ProcessSpec(Path.Combine(workingDirectory, ToolLocator.ProgramName), [], workingDirectory));
    }
}

internal sealed class RustRuntime : ILanguageRuntime, INativeDebugBuild
{
    public string LanguageId => "rust";
    public string DisplayName => "Rust";
    public string DefaultEntryFile => "main.rs";

    public async Task<RuntimeStatus> DetectAsync(CancellationToken ct)
    {
        LocalizedText hint = EngineMessages.RustHint();
        // ToolLocator also searches ~/.cargo/bin, where rustup installs and GUI apps usually have no PATH entry.
        if (ToolLocator.Find("rustc") is not { } rustc) return ToolLocator.Missing(LanguageId, EngineMessages.ToolNotFound("rustc"), hint);
        string? v = await ToolLocator.ProbeAsync(rustc, ["--version"], ct).ConfigureAwait(false);
        return v is null
            ? ToolLocator.Missing(LanguageId, EngineMessages.ToolNotWorking("rustc"), hint)
            : new RuntimeStatus(LanguageId, RuntimeAvailability.Available, v.Replace("rustc ", ""), rustc, null, null);
    }

    // Single rustc invocation on the entry file: `mod x;` resolves next to it. Future cargo projects would use
    // CARGO_HOME/CARGO_TARGET_DIR below the runner cache with CARGO_NET_OFFLINE=true; plain rustc needs no cache.
    public BuildPlan CreatePlan(string workingDirectory, string entryFile, RuntimeStatus status) =>
        new([new ProcessSpec(status.ExecutablePath!, ["--edition", "2021", "-O", "-o", ToolLocator.ProgramName, entryFile], workingDirectory, ToolchainEnvironment())],
            new ProcessSpec(Path.Combine(workingDirectory, ToolLocator.ProgramName), [], workingDirectory));

    // HOME is the workdir, so point the rustup shim at the real toolchain directories (read only use).
    private static Dictionary<string, string> ToolchainEnvironment()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var env = new Dictionary<string, string>();
        if (Directory.Exists(Path.Combine(home, ".rustup"))) env["RUSTUP_HOME"] = Path.Combine(home, ".rustup");
        if (Directory.Exists(Path.Combine(home, ".cargo"))) env["CARGO_HOME"] = Path.Combine(home, ".cargo");
        return env;
    }

    /// <summary>-g -C opt-level=0; on macOS split-debuginfo=packed keeps DWARF in prog.dSYM (the default leaves it in deleted .o files).</summary>
    public BuildPlan CreateDebugPlan(string workingDirectory, string entryFile, RuntimeStatus status, string? sourceDirectory = null)
    {
        var args = new List<string> { "--edition", "2021", "-g", "-C", "opt-level=0" };
        if (OperatingSystem.IsMacOS()) args.AddRange(["-C", "split-debuginfo=packed"]);
        args.AddRange(["-o", ToolLocator.ProgramName, Path.GetFullPath(entryFile, sourceDirectory ?? workingDirectory)]);
        return new BuildPlan([new ProcessSpec(status.ExecutablePath!, args, workingDirectory, ToolchainEnvironment())],
            new ProcessSpec(Path.Combine(workingDirectory, ToolLocator.ProgramName), [], workingDirectory));
    }
}

internal sealed class CSharpRuntime(string cacheDirectory) : ILanguageRuntime, IDebugBuildRuntime
{
    public string LanguageId => "csharp";
    public string DisplayName => "C# (.NET file-based app or project)";
    public string DefaultEntryFile => "Program.cs";

    public async Task<RuntimeStatus> DetectAsync(CancellationToken ct)
    {
        LocalizedText hint = EngineMessages.DotNetHint();
        string? dotnet = ToolLocator.Find("dotnet");
        if (dotnet is null) return ToolLocator.Missing(LanguageId, EngineMessages.ToolNotFound("dotnet"), hint);
        string? v = await ToolLocator.ProbeAsync(dotnet, ["--version"], ct).ConfigureAwait(false);
        if (v is null) return ToolLocator.Missing(LanguageId, EngineMessages.ToolNotWorking("dotnet SDK"), hint);
        return Version.TryParse(v.Split('-')[0], out Version? ver) && ver.Major >= 10
            ? new RuntimeStatus(LanguageId, RuntimeAvailability.Available, v, dotnet, null, null).WithNotes(EngineMessages.CSharpNotes())
            : new RuntimeStatus(LanguageId, RuntimeAvailability.Unsupported, v, dotnet, null, null).WithNotes(EngineMessages.CSharpUnsupported()).WithInstallHint(hint);
    }

    public BuildPlan CreatePlan(string workingDirectory, string entryFile, RuntimeStatus status)
    {
        string home = Path.Combine(cacheDirectory, "dotnet");
        var env = new Dictionary<string, string>
        {
            ["DOTNET_CLI_HOME"] = Path.Combine(home, "cli"),
            ["NUGET_PACKAGES"] = Path.Combine(home, "nuget"),
            ["DOTNET_NOLOGO"] = "1",
            ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
            ["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1",
            ["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0",
            ["MSBUILDDISABLENODEREUSE"] = "1",
            ["DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE"] = "1",
        };
        // Project mode: exactly one .csproj at the root builds as a project (all its .cs files) and runs <AssemblyName>.dll.
        // Otherwise the entry file alone is built as a file-based app, exactly as before.
        string[] flags = ["-o", "out", "--nologo", "-v", "q", "--disable-build-servers", "-p:UseSharedCompilation=false", .. OfflineFlags, .. Utf8ConsoleFlags(home)];
        if (FindProject(workingDirectory) is { } project)
            return new BuildPlan(
                [new ProcessSpec(status.ExecutablePath!, ["build", project, .. flags], workingDirectory, env)],
                new ProcessSpec(status.ExecutablePath!, [Path.Combine("out", AssemblyName(workingDirectory, project) + ".dll")], workingDirectory, env));
        string name = Path.GetFileNameWithoutExtension(entryFile);
        return new BuildPlan(
            [new ProcessSpec(status.ExecutablePath!, ["build", entryFile, .. flags], workingDirectory, env)],
            new ProcessSpec(status.ExecutablePath!, [Path.Combine("out", name + ".dll")], workingDirectory, env));
    }

    /// <summary>
    /// .NET 10 file-based apps default to <c>PublishAot=true</c>, which makes restore download ILLink/ILCompiler and runtime packs
    /// from nuget.org (about 100 MB; fails on a fresh machine and under a no-network sandbox). We only build and run, never publish
    /// AOT, so turn it off: a plain program then needs no package at all. NuGetAudit would also contact nuget.org.
    /// </summary>
    private static readonly string[] OfflineFlags = ["-p:PublishAot=false", "-p:NuGetAudit=false"];

    /// <summary>
    /// With redirected pipes .NET on Windows reads and writes the console streams in the ANSI code page (Cyrillic prints as "?"). A module initializer compiled
    /// into the learner's program (through a targets file imported after Microsoft.Common.targets, so neither the sources nor the project change) switches
    /// stdin/stdout/stderr to UTF-8, which is what the engine decodes. Other platforms already use UTF-8.
    /// </summary>
    private static string[] Utf8ConsoleFlags(string home)
    {
        if (!OperatingSystem.IsWindows()) return [];
        string dir = Path.Combine(home, "utf8");
        Directory.CreateDirectory(dir);
        string source = Path.Combine(dir, "Utf8Console.cs");
        string targets = Path.Combine(dir, "Utf8Console.targets");
        WriteIfChanged(source, """
            internal static class DevAcademyUtf8Console
            {
                [System.Runtime.CompilerServices.ModuleInitializer]
                internal static void Init()
                {
                    var utf8 = new System.Text.UTF8Encoding(false);
                    System.Console.SetOut(new System.IO.StreamWriter(System.Console.OpenStandardOutput(), utf8) { AutoFlush = true });
                    System.Console.SetError(new System.IO.StreamWriter(System.Console.OpenStandardError(), utf8) { AutoFlush = true });
                    System.Console.SetIn(new System.IO.StreamReader(System.Console.OpenStandardInput(), utf8));
                }
            }
            """);
        WriteIfChanged(targets, $"""
            <Project>
              <ItemGroup>
                <Compile Include="{source}" />
              </ItemGroup>
            </Project>
            """);
        return [$"-p:CustomAfterMicrosoftCommonTargets={targets}"];
    }

    private static void WriteIfChanged(string path, string content)
    {
        if (!File.Exists(path) || File.ReadAllText(path) != content) File.WriteAllText(path, content);
    }

    /// <summary>Debug build (portable pdb, no optimisation) into <c>out</c> of the working directory; the run command is <c>dotnet &lt;absolute dll&gt;</c>.</summary>
    public BuildPlan CreateDebugPlan(string workingDirectory, string entryFile, RuntimeStatus status, string? sourceDirectory = null)
    {
        string src = sourceDirectory ?? workingDirectory;
        string home = Path.Combine(cacheDirectory, "dotnet");
        var env = new Dictionary<string, string>
        {
            ["DOTNET_CLI_HOME"] = Path.Combine(home, "cli"),
            ["NUGET_PACKAGES"] = Path.Combine(home, "nuget"),
            ["DOTNET_NOLOGO"] = "1",
            ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
            ["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1",
            ["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0",
            ["MSBUILDDISABLENODEREUSE"] = "1",
            ["DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE"] = "1",
        };
        string outDir = Path.Combine(workingDirectory, "out");
        string[] flags = ["-c", "Debug", "-o", outDir, "--nologo", "-v", "q", "--disable-build-servers", "-p:UseSharedCompilation=false", .. OfflineFlags, "-p:Optimize=false", "-p:DebugType=portable", "-p:DebugSymbols=true", .. Utf8ConsoleFlags(home)];
        string target; string assembly;
        if (FindProject(src) is { } project)
        {
            target = Path.Combine(src, project);
            assembly = AssemblyName(src, project);
        }
        else
        {
            target = Path.GetFullPath(entryFile, src);
            assembly = Path.GetFileNameWithoutExtension(entryFile);
        }
        return new BuildPlan(
            [new ProcessSpec(status.ExecutablePath!, ["build", target, .. flags], workingDirectory, env)],
            new ProcessSpec(status.ExecutablePath!, [Path.Combine(outDir, assembly + ".dll")], workingDirectory, env));
    }

    /// <summary>The only project file in the root of the working directory (relative name), or null when there is none or several (then the entry file is built as a file-based app).</summary>
    internal static string? FindProject(string workingDirectory)
    {
        string[] projects = Directory.GetFiles(workingDirectory, "*.csproj", SearchOption.TopDirectoryOnly);
        return projects.Length == 1 ? Path.GetFileName(projects[0]) : null;
    }

    /// <summary>The assembly the project builds: its <c>AssemblyName</c> property when set, else the project file name without the extension.</summary>
    internal static string AssemblyName(string workingDirectory, string project)
    {
        string fallback = Path.GetFileNameWithoutExtension(project);
        try
        {
            var doc = System.Xml.Linq.XDocument.Load(Path.Combine(workingDirectory, project));
            string? name = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "AssemblyName")?.Value.Trim();
            return string.IsNullOrEmpty(name) || name.Contains('$') || name.IndexOfAny(['/', '\\']) >= 0 ? fallback : name;
        }
        catch (Exception ex) when (ex is System.Xml.XmlException or IOException or UnauthorizedAccessException) { return fallback; }
    }

    /// <summary>Environment that points NuGet at a nuget package set's packages folder, which the caller mounts read-only.</summary>
    internal static IReadOnlyDictionary<string, string> PackagesEnvironment(string packages) => new Dictionary<string, string> { ["NUGET_PACKAGES"] = packages };

    /// <summary>Restore resolves only from the set's packages folder: no other package source is consulted, so a build never tries the network.</summary>
    internal static IReadOnlyList<string> OfflineRestoreArguments(string packages) => [$"-p:RestoreSources={packages}"];
}

internal sealed class KotlinRuntime : ILanguageRuntime
{
    public string LanguageId => "kotlin";
    public string DisplayName => "Kotlin (JVM)";
    public string DefaultEntryFile => "Main.kt";

    public async Task<RuntimeStatus> DetectAsync(CancellationToken ct)
    {
        LocalizedText hint = EngineMessages.KotlinHint();
        if (ToolLocator.Find("kotlinc") is not { } kotlinc) return ToolLocator.Missing(LanguageId, EngineMessages.ToolNotFound("kotlinc"), hint);
        if (ToolLocator.Find("java") is null || await ToolLocator.ProbeAsync(ToolLocator.Find("java")!, ["-version"], ct).ConfigureAwait(false) is null)
            return ToolLocator.Missing(LanguageId, EngineMessages.KotlinNeedsJdk(), hint);
        string? v = await ToolLocator.ProbeAsync(kotlinc, ["-version"], ct).ConfigureAwait(false); // prints to stderr
        return v is null
            ? ToolLocator.Missing(LanguageId, EngineMessages.ToolNotWorking("kotlinc"), hint)
            : new RuntimeStatus(LanguageId, RuntimeAvailability.Available, v.Replace("info: ", ""), kotlinc, null, null)
                .WithNotes(FindCoroutinesJar(kotlinc) is null ? EngineMessages.KotlinNoCoroutines() : EngineMessages.KotlinCoroutines());
    }

    /// <summary>
    /// kotlinx-coroutines-core-jvm.jar from the kotlinc distribution (zip: kotlinc/lib; Homebrew: libexec/lib next to the
    /// bin/kotlinc wrapper), looked up beside the symlink-resolved kotlinc. Null when the distribution does not ship it.
    /// </summary>
    internal static string? FindCoroutinesJar(string kotlinc)
    {
        try
        {
            string resolved = File.ResolveLinkTarget(kotlinc, true)?.FullName ?? kotlinc;
            if (Path.GetDirectoryName(resolved) is not { } bin) return null;
            foreach (string rel in new[] { "lib", Path.Combine("..", "lib"), Path.Combine("..", "libexec", "lib") })
            {
                string jar = Path.GetFullPath(Path.Combine(bin, rel, "kotlinx-coroutines-core-jvm.jar"));
                if (File.Exists(jar)) return jar;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        return null;
    }

    public BuildPlan CreatePlan(string workingDirectory, string entryFile, RuntimeStatus status)
    {
        var args = new List<string>();
        args.AddRange(ToolLocator.FilesWithExtensions(workingDirectory, ".kt"));
        string? coroutines = FindCoroutinesJar(status.ExecutablePath!);
        if (coroutines is not null) args.AddRange(["-cp", coroutines]);
        args.AddRange(["-include-runtime", "-d", "prog.jar"]);
        string java = ToolLocator.Find("java")!;
        string[] jvm = ["-XX:+UseSerialGC", "-Xss16m", .. ToolLocator.JvmEncoding];
        // `java -jar` ignores -cp, so with the coroutines jar the main class is named explicitly: <package.>FileKt of the entry file.
        ProcessSpec run = coroutines is null
            ? new ProcessSpec(java, [.. jvm, "-jar", "prog.jar"], workingDirectory)
            : new ProcessSpec(java, [.. jvm, "-cp", "prog.jar" + Path.PathSeparator + coroutines, MainClassName(workingDirectory, entryFile)], workingDirectory,
                ReadOnlyPaths: [Path.GetDirectoryName(coroutines)!]);
        return new BuildPlan([new ProcessSpec(status.ExecutablePath!, args, workingDirectory, MinWallTimeout: TimeSpan.FromSeconds(120))], run);
    }

    private static string MainClassName(string workingDirectory, string entryFile)
    {
        string name = Path.GetFileNameWithoutExtension(entryFile);
        string cls = char.ToUpperInvariant(name[0]) + name[1..] + "Kt";
        string path = Path.Combine(workingDirectory, entryFile);
        string? package = File.Exists(path)
            ? File.ReadLines(path).Select(l => Regex.Match(l, @"^\s*package\s+([\w.]+)")).FirstOrDefault(m => m.Success)?.Groups[1].Value
            : null;
        return package is null ? cls : package + "." + cls;
    }
}

internal sealed class PhpRuntime : ILanguageRuntime
{
    public string LanguageId => "php";
    public string DisplayName => "PHP";
    public string DefaultEntryFile => "main.php";

    public async Task<RuntimeStatus> DetectAsync(CancellationToken ct)
    {
        LocalizedText hint = EngineMessages.PhpHint();
        if (ToolLocator.Find("php") is not { } php) return ToolLocator.Missing(LanguageId, EngineMessages.ToolNotFound("php"), hint);
        string? v = await ToolLocator.ProbeAsync(php, ["--version"], ct).ConfigureAwait(false);
        if (v is null) return ToolLocator.Missing(LanguageId, EngineMessages.ToolNotWorking("php"), hint);
        var status = new RuntimeStatus(LanguageId, RuntimeAvailability.Available, v, php, null, null);
        // The first line is "PHP 8.5.11 (cli) ..."; the courses use 8.4+ functions (mb_trim, array_find) and were verified on 8.5.
        Match m = Regex.Match(v, @"PHP (\d+)\.(\d+)");
        return m.Success && new Version(int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), int.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture)) < new Version(8, 4)
            ? status.WithNotes(EngineMessages.PhpOld()).WithWarning(EngineMessages.PhpOld()).WithInstallHint(hint)
            : status;
    }

    /// <summary>
    /// Settings that make a run the same on every machine, whatever php.ini and conf.d the distribution ships (Ubuntu: display_errors off,
    /// zend.assertions -1, memory_limit -1, and Xdebug in develop mode, which replaces var_dump's output and prefixes it with file and line).
    /// Errors go to stderr so stdout is only what the program prints; precision values are PHP's own defaults, stated so a php.ini cannot change float output.
    /// </summary>
    internal static readonly string[] Settings =
    [
        "-d", "xdebug.mode=off",
        "-d", "display_errors=stderr", "-d", "display_startup_errors=1", "-d", "log_errors=0", "-d", "html_errors=0",
        "-d", "error_reporting=32767",
        "-d", "zend.assertions=1", "-d", "assert.exception=1",
        // Production php.ini sets it to On: exception messages then name the type instead of the value ("Unhandled match case of type int").
        "-d", "zend.exception_ignore_args=0",
        "-d", "precision=14", "-d", "serialize_precision=-1",
        "-d", "memory_limit=128M", "-d", "default_charset=UTF-8",
        "-d", "opcache.enable_cli=0", "-d", "auto_prepend_file=", "-d", "auto_append_file=",
    ];

    public BuildPlan CreatePlan(string workingDirectory, string entryFile, RuntimeStatus status) =>
        new([], new ProcessSpec(status.ExecutablePath!, [.. Settings, entryFile], workingDirectory));
}
