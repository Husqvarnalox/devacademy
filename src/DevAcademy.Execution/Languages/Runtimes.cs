using System.Text.RegularExpressions;

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
        const string Hint = "Install Python 3 from https://www.python.org/downloads/ (macOS: brew install python; Debian/Ubuntu: apt install python3).";
        foreach (string name in new[] { "python3", "python" })
        {
            if (ToolLocator.Find(name) is not { } path) continue;
            string? v = await ToolLocator.ProbeAsync(path, ["--version"], ct).ConfigureAwait(false); // rejects shims such as the Windows Store stub
            if (v is not null && v.StartsWith("Python 3", StringComparison.Ordinal))
                return new RuntimeStatus(LanguageId, RuntimeAvailability.Available, v["Python ".Length..], path, null, null);
        }
        return ToolLocator.Missing(LanguageId, "python3", Hint);
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

internal sealed class JavaScriptRuntime : IContainerizableRuntime, ICommandRuntime
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
}

internal static class NodeProbe
{
    public const string Hint = "Install Node.js from https://nodejs.org/ (macOS: brew install node; Debian/Ubuntu: apt install nodejs).";

    public static async Task<RuntimeStatus> DetectAsync(string languageId, CancellationToken ct)
    {
        if (ToolLocator.Find("node") is not { } node) return ToolLocator.Missing(languageId, "node", Hint);
        string? v = await ToolLocator.ProbeAsync(node, ["--version"], ct).ConfigureAwait(false);
        if (v is null) return ToolLocator.Missing(languageId, "A working node", Hint);
        string managers = PackageManagerDetector.Describe(await PackageManagerDetector.DetectAsync(ct).ConfigureAwait(false));
        return new RuntimeStatus(languageId, RuntimeAvailability.Available, v.TrimStart('v'), node, managers, null);
    }
}

internal sealed class TypeScriptRuntime(string cacheDirectory) : ILanguageRuntime
{
    private const string TscHint = "Install the TypeScript compiler for type-checking: npm install -g typescript (https://www.typescriptlang.org/download).";

    public string LanguageId => "typescript";
    public string DisplayName => "TypeScript";
    public string DefaultEntryFile => "main.ts";

    public async Task<RuntimeStatus> DetectAsync(CancellationToken ct)
    {
        RuntimeStatus node = await NodeProbe.DetectAsync(LanguageId, ct).ConfigureAwait(false);
        if (node.Availability != RuntimeAvailability.Available) return node;
        if (!Version.TryParse(node.Version, out Version? nv) || nv < new Version(22, 6))
            return node with
            {
                Availability = RuntimeAvailability.Unsupported,
                Notes = $"Node.js {node.Version} cannot run TypeScript directly; Node.js 22.6 or newer is required.",
                InstallHint = NodeProbe.Hint,
            };

        const string Stripping = "Runs via Node type stripping (no enums/namespaces).";
        string? tscVersion = ToolLocator.Find("tsc") is { } tsc
            ? await ToolLocator.ProbeAsync(tsc, ["--version"], ct).ConfigureAwait(false)
            : null;
        if (tscVersion is null)
            return node with
            {
                Notes = $"Type-checking is UNAVAILABLE (tsc not found): type errors will not be reported. {Stripping} {node.Notes}",
                InstallHint = TscHint,
                Warning = "type-checking unavailable: tsc not installed",
            };
        if (!TypeScriptTypes.IsProvisioned(cacheDirectory))
            return node with
            {
                Notes = $"{tscVersion} found, but type-checking unavailable: @types/node not provisioned. {Stripping} {node.Notes}",
                InstallHint = null,
                Warning = "type-checking unavailable: @types/node not provisioned",
            };
        return node with { Notes = $"Type-checking with tsc ({tscVersion}) is enabled. {Stripping} {node.Notes}", InstallHint = null };
    }

    public BuildPlan CreatePlan(string workingDirectory, string entryFile, RuntimeStatus status)
    {
        Version nv = Version.Parse(status.Version!);
        var args = new List<string>();
        if (nv < new Version(23, 6)) args.Add("--experimental-strip-types");
        args.Add("--disable-warning=ExperimentalWarning");
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

internal sealed class GoRuntime(string cacheDirectory) : ICommandRuntime
{
    public string LanguageId => "go";
    public string DisplayName => "Go";
    public string DefaultEntryFile => "main.go";

    public async Task<RuntimeStatus> DetectAsync(CancellationToken ct)
    {
        if (ToolLocator.Find("go") is not { } go)
            return ToolLocator.Missing(LanguageId, "go", "Install Go from https://go.dev/dl/ (macOS: brew install go; Debian/Ubuntu: apt install golang).");
        string? v = await ToolLocator.ProbeAsync(go, ["version"], ct).ConfigureAwait(false);
        var m = v is null ? null : Regex.Match(v, @"go version go(\S+)");
        return m is { Success: true }
            ? new RuntimeStatus(LanguageId, RuntimeAvailability.Available, m.Groups[1].Value, go, null, null)
            : ToolLocator.Missing(LanguageId, "A working go", "Install Go from https://go.dev/dl/.");
    }

    public string CommandTool => "go";

    public BuildPlan CreateCommandPlan(string workingDirectory, IReadOnlyList<string> argv, RuntimeStatus status) =>
        new([], new ProcessSpec(status.ExecutablePath!, argv.Skip(1).ToList(), workingDirectory, BuildEnvironment(workingDirectory)));

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
        var args = new List<string> { "build", "-o", "prog" };
        args.AddRange(ToolLocator.FilesWithExtensions(workingDirectory, ".go").Where(f => !f.EndsWith("_test.go", StringComparison.Ordinal)));
        return new BuildPlan(
            [new ProcessSpec(status.ExecutablePath!, args, workingDirectory, env)],
            new ProcessSpec(Path.Combine(workingDirectory, "prog"), [], workingDirectory));
    }
}

internal sealed class JavaRuntime : ILanguageRuntime
{
    public string LanguageId => "java";
    public string DisplayName => "Java";
    public string DefaultEntryFile => "Main.java";

    public async Task<RuntimeStatus> DetectAsync(CancellationToken ct)
    {
        const string Hint = "Install a JDK (17+) from https://adoptium.net/ (macOS: brew install openjdk; Debian/Ubuntu: apt install default-jdk).";
        if (ToolLocator.Find("javac") is not { } javac || ToolLocator.Find("java") is not { } java)
            return ToolLocator.Missing(LanguageId, "java and javac (a JDK)", Hint);
        // On macOS /usr/bin/java is a shim that fails without a JDK, hence the probes.
        string? jv = await ToolLocator.ProbeAsync(javac, ["-version"], ct).ConfigureAwait(false);
        string? rv = await ToolLocator.ProbeAsync(java, ["-version"], ct).ConfigureAwait(false);
        if (jv is null || rv is null) return ToolLocator.Missing(LanguageId, "A working JDK", Hint);
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
            new ProcessSpec(java, ["-XX:+UseSerialGC", "-Xss16m", "-cp", "out", mainClass], workingDirectory));
    }
}

internal sealed class CppRuntime : ILanguageRuntime
{
    public string LanguageId => "cpp";
    public string DisplayName => "C++";
    public string DefaultEntryFile => "main.cpp";

    public async Task<RuntimeStatus> DetectAsync(CancellationToken ct)
    {
        foreach (string name in new[] { "clang++", "g++" })
        {
            if (ToolLocator.Find(name) is not { } path) continue;
            string? v = await ToolLocator.ProbeAsync(path, ["--version"], ct).ConfigureAwait(false); // fails for the Xcode shim without Command Line Tools
            if (v is not null) return new RuntimeStatus(LanguageId, RuntimeAvailability.Available, v, path, null, null);
        }
        return ToolLocator.Missing(LanguageId, "clang++ or g++",
            "Install a C++ compiler: macOS: xcode-select --install; Debian/Ubuntu: apt install g++; Windows: https://visualstudio.microsoft.com/downloads/ or MSYS2.");
    }

    public BuildPlan CreatePlan(string workingDirectory, string entryFile, RuntimeStatus status)
    {
        var args = new List<string> { "-std=c++20", "-O2", "-Wall", "-Wextra", "-o", "prog" };
        args.AddRange(ToolLocator.FilesWithExtensions(workingDirectory, ".cpp", ".cc", ".cxx"));
        return new BuildPlan(
            [new ProcessSpec(status.ExecutablePath!, args, workingDirectory)],
            new ProcessSpec(Path.Combine(workingDirectory, "prog"), [], workingDirectory));
    }
}

internal sealed class RustRuntime : ILanguageRuntime
{
    public string LanguageId => "rust";
    public string DisplayName => "Rust";
    public string DefaultEntryFile => "main.rs";

    public async Task<RuntimeStatus> DetectAsync(CancellationToken ct)
    {
        const string Hint = "https://rustup.rs";
        // ToolLocator also searches ~/.cargo/bin, where rustup installs and GUI apps usually have no PATH entry.
        if (ToolLocator.Find("rustc") is not { } rustc) return ToolLocator.Missing(LanguageId, "rustc", Hint);
        string? v = await ToolLocator.ProbeAsync(rustc, ["--version"], ct).ConfigureAwait(false);
        return v is null
            ? ToolLocator.Missing(LanguageId, "A working rustc", Hint)
            : new RuntimeStatus(LanguageId, RuntimeAvailability.Available, v.Replace("rustc ", ""), rustc, null, null);
    }

    // Single rustc invocation on the entry file: `mod x;` resolves next to it. Future cargo projects would use
    // CARGO_HOME/CARGO_TARGET_DIR below the runner cache with CARGO_NET_OFFLINE=true; plain rustc needs no cache.
    public BuildPlan CreatePlan(string workingDirectory, string entryFile, RuntimeStatus status)
    {
        // HOME is the workdir, so point the rustup shim at the real toolchain directories (read only use).
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var env = new Dictionary<string, string>();
        if (Directory.Exists(Path.Combine(home, ".rustup"))) env["RUSTUP_HOME"] = Path.Combine(home, ".rustup");
        if (Directory.Exists(Path.Combine(home, ".cargo"))) env["CARGO_HOME"] = Path.Combine(home, ".cargo");
        return new BuildPlan(
            [new ProcessSpec(status.ExecutablePath!, ["--edition", "2021", "-O", "-o", "prog", entryFile], workingDirectory, env)],
            new ProcessSpec(Path.Combine(workingDirectory, "prog"), [], workingDirectory));
    }
}

internal sealed class CSharpRuntime(string cacheDirectory) : ILanguageRuntime
{
    public string LanguageId => "csharp";
    public string DisplayName => "C# (.NET file-based app)";
    public string DefaultEntryFile => "Program.cs";

    public async Task<RuntimeStatus> DetectAsync(CancellationToken ct)
    {
        const string Hint = "Install the .NET 10 SDK from https://dotnet.microsoft.com/download (macOS: brew install --cask dotnet-sdk; Debian/Ubuntu: apt install dotnet-sdk-10.0).";
        string? dotnet = ToolLocator.Find("dotnet");
        if (dotnet is null) return ToolLocator.Missing(LanguageId, "dotnet", Hint);
        string? v = await ToolLocator.ProbeAsync(dotnet, ["--version"], ct).ConfigureAwait(false);
        if (v is null) return ToolLocator.Missing(LanguageId, "A working dotnet SDK", Hint);
        return Version.TryParse(v.Split('-')[0], out Version? ver) && ver.Major >= 10
            ? new RuntimeStatus(LanguageId, RuntimeAvailability.Available, v, dotnet, "Only the entry file is compiled (file-based app).", null)
            : new RuntimeStatus(LanguageId, RuntimeAvailability.Unsupported, v, dotnet, ".NET SDK 10 or newer is required for file-based apps.", Hint);
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
        };
        string name = Path.GetFileNameWithoutExtension(entryFile);
        return new BuildPlan(
            [new ProcessSpec(status.ExecutablePath!, ["build", entryFile, "-o", "out", "--nologo", "-v", "q", "--disable-build-servers", "-p:UseSharedCompilation=false"], workingDirectory, env)],
            new ProcessSpec(status.ExecutablePath!, [Path.Combine("out", name + ".dll")], workingDirectory, env));
    }
}

internal sealed class KotlinRuntime : ILanguageRuntime
{
    public string LanguageId => "kotlin";
    public string DisplayName => "Kotlin (JVM)";
    public string DefaultEntryFile => "Main.kt";

    public async Task<RuntimeStatus> DetectAsync(CancellationToken ct)
    {
        const string Hint = "https://kotlinlang.org/docs/command-line.html (also requires a JDK).";
        if (ToolLocator.Find("kotlinc") is not { } kotlinc) return ToolLocator.Missing(LanguageId, "kotlinc", Hint);
        if (ToolLocator.Find("java") is null || await ToolLocator.ProbeAsync(ToolLocator.Find("java")!, ["-version"], ct).ConfigureAwait(false) is null)
            return ToolLocator.Missing(LanguageId, "A working java (kotlinc needs a JDK)", Hint);
        string? v = await ToolLocator.ProbeAsync(kotlinc, ["-version"], ct).ConfigureAwait(false); // prints to stderr
        return v is null
            ? ToolLocator.Missing(LanguageId, "A working kotlinc", Hint)
            : new RuntimeStatus(LanguageId, RuntimeAvailability.Available, v.Replace("info: ", ""), kotlinc, null, null);
    }

    public BuildPlan CreatePlan(string workingDirectory, string entryFile, RuntimeStatus status)
    {
        var args = new List<string>();
        args.AddRange(ToolLocator.FilesWithExtensions(workingDirectory, ".kt"));
        args.AddRange(["-include-runtime", "-d", "prog.jar"]);
        return new BuildPlan(
            [new ProcessSpec(status.ExecutablePath!, args, workingDirectory, MinWallTimeout: TimeSpan.FromSeconds(120))],
            new ProcessSpec(ToolLocator.Find("java")!, ["-XX:+UseSerialGC", "-Xss16m", "-jar", "prog.jar"], workingDirectory));
    }
}

internal sealed class PhpRuntime : ILanguageRuntime
{
    public string LanguageId => "php";
    public string DisplayName => "PHP";
    public string DefaultEntryFile => "main.php";

    public async Task<RuntimeStatus> DetectAsync(CancellationToken ct)
    {
        const string Hint = "Install PHP 8 from https://www.php.net/downloads (macOS: brew install php; Debian/Ubuntu: apt install php-cli).";
        if (ToolLocator.Find("php") is not { } php) return ToolLocator.Missing(LanguageId, "php", Hint);
        string? v = await ToolLocator.ProbeAsync(php, ["--version"], ct).ConfigureAwait(false);
        return v is null
            ? ToolLocator.Missing(LanguageId, "A working php", Hint)
            : new RuntimeStatus(LanguageId, RuntimeAvailability.Available, v, php, null, null);
    }

    public BuildPlan CreatePlan(string workingDirectory, string entryFile, RuntimeStatus status) =>
        new([], new ProcessSpec(status.ExecutablePath!, [entryFile], workingDirectory));
}
