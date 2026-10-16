namespace DevAcademy.Domain;

/// <summary>
/// Which executables a <see cref="EvaluationKind.Command"/> exercise may run. Content never names arbitrary binaries:
/// the first argv element must be a known tool of the exercise's language (the runner resolves it to the detected path),
/// and flags that would run other programs or code are rejected. Tools that live in the node content environment (<c>vitest</c>) are
/// named by <see cref="CommandSpec.Tool"/> instead; the argv is then built here from a fixed script path inside <c>node_modules</c>
/// and a flag allowlist, never from content-supplied paths.
/// </summary>
public static class CommandPolicy
{
    public const int DefaultTimeoutSeconds = 20;
    public const int MaxTimeoutSeconds = 60;

    private static readonly string[] NodeForbidden =
        ["-e", "--eval", "-p", "--print", "-r", "--require", "--import", "--loader", "--experimental-loader", "--env-file", "--inspect", "--inspect-brk"];

    private static readonly string[] GoForbidden =
        ["-exec", "-toolexec", "-vettool", "-C", "-modfile", "-overlay", "-pgo", "-ldflags", "-gcflags", "-asmflags"];

    /// <summary>The directory the runner exposes (read-only) in the work directory for node-environment tools.</summary>
    public const string NodeModulesDir = "node_modules";

    /// <summary>A tool installed in the node content environment: its npm package, the script `node` runs and the flags content may pass.</summary>
    public sealed record NodeTool(string Name, string Package, string Script, IReadOnlyList<string> LeadingArgs,
        IReadOnlyDictionary<string, Func<string, bool>> ValueFlags, IReadOnlyList<string> BareFlags);

    private static bool Digits(string v) => v.Length is > 0 and <= 6 && v.All(char.IsAsciiDigit);
    private static bool OneOf(string v, params string[] allowed) => allowed.Contains(v, StringComparer.Ordinal);
    private static bool RelativePath(string v) =>
        v.Length > 0 && !Path.IsPathRooted(v) && !v.StartsWith('/') && !v.StartsWith('\\') && !v.Contains('\0') && !v.Contains('\n')
        && !v.Split('/', '\\').Contains("..") && !(v.Length > 1 && v[1] == ':');

    /// <summary>Vitest: <c>node node_modules/vitest/vitest.mjs run [allowed flags] [relative file filters]</c>. No watch/ui/browser/coverage/update flags. <c>--configLoader=runner</c> is fixed: the default loader bundles the config into node_modules/.vite-temp, which is read-only in the sandbox.</summary>
    public static NodeTool Vitest { get; } = new("vitest", "vitest", "vitest/vitest.mjs", ["run", "--configLoader=runner"],
        new Dictionary<string, Func<string, bool>>(StringComparer.Ordinal)
        {
            ["--reporter"] = v => OneOf(v, "junit", "default", "verbose", "dot"),
            ["--outputFile"] = RelativePath,
            ["--config"] = RelativePath,
            ["--pool"] = v => OneOf(v, "threads", "forks"),
            ["--environment"] = v => OneOf(v, "jsdom", "node"),
            ["--testTimeout"] = Digits,
            ["--maxWorkers"] = Digits,
        },
        ["--no-file-parallelism", "--silent", "--passWithNoTests", "--globals"]);

    public static NodeTool? FindNodeTool(string languageId, string? tool) =>
        languageId == "javascript" && string.Equals(tool, Vitest.Name, StringComparison.Ordinal) ? Vitest : null;

    /// <summary>Tool names allowed as argv[0] for the language (empty = the language has no command support).</summary>
    public static IReadOnlyList<string> AllowedTools(string languageId) => languageId switch
    {
        "javascript" => ["node"],
        "go" => ["go"],
        _ => [],
    };

    /// <summary>Null when the argv is acceptable, otherwise the reason it is not.</summary>
    public static string? Check(string languageId, IReadOnlyList<string> run)
    {
        var tools = AllowedTools(languageId);
        if (tools.Count == 0) return $"command evaluation is not supported for language '{languageId}'";
        if (run.Count == 0 || string.IsNullOrWhiteSpace(run[0])) return "command.run is empty";
        if (!tools.Contains(run[0], StringComparer.Ordinal))
            return $"command.run executable '{run[0]}' is not allowed for '{languageId}' (allowed: {string.Join(", ", tools)})";
        if (run.Any(string.IsNullOrWhiteSpace) || run.Any(a => a.Contains('\0') || a.Contains('\n'))) return "command.run has an empty or invalid argument";
        foreach (var arg in run.Skip(1))
        {
            var value = arg.StartsWith('-') && arg.Contains('=') ? arg[(arg.IndexOf('=') + 1)..] : arg;
            if (Path.IsPathRooted(value) || value.StartsWith('/') || value.StartsWith('\\') || value.Split('/', '\\').Contains(".."))
                return $"command.run argument '{arg}' must be a relative path without '..'";
        }
        switch (languageId)
        {
            case "javascript":
                if (run.Skip(1).Any(a => a.Split('/', '\\').Contains(NodeModulesDir)))
                    return "command.run must not reach into node_modules; name an installed tool with command.tool (vitest)";
                if (!run.Contains("--test", StringComparer.Ordinal)) return "a javascript command must use the built-in runner (node --test ...)";
                var badNode = run.Skip(1).FirstOrDefault(a => NodeForbidden.Contains(a.Split('=')[0], StringComparer.Ordinal));
                return badNode is null ? null : $"node flag '{badNode}' is not allowed";
            case "go":
                if (run.Count < 2 || run[1] != "test") return "a go command must be 'go test ...'";
                var badGo = run.Skip(2).FirstOrDefault(a => GoForbidden.Contains(a.Split('=')[0], StringComparer.Ordinal));
                return badGo is null ? null : $"go flag '{badGo}' is not allowed";
        }
        return null;
    }

    /// <summary>Null when <c>command.tool</c> + <c>command.args</c> are acceptable, otherwise the reason they are not.</summary>
    public static string? CheckTool(string languageId, string? toolName, IReadOnlyList<string> args)
    {
        if (FindNodeTool(languageId, toolName) is not { } tool)
            return $"command.tool '{toolName}' is not allowed for '{languageId}' (allowed: {(languageId == "javascript" ? Vitest.Name : "none")})";
        foreach (var arg in args)
        {
            if (string.IsNullOrWhiteSpace(arg) || arg.Contains('\0') || arg.Contains('\n')) return "command.args has an empty or invalid argument";
            if (!arg.StartsWith('-'))
            {
                if (!RelativePath(arg)) return $"command.args '{arg}' must be a relative path filter without '..'";
                continue;
            }
            var eq = arg.IndexOf('=');
            var name = eq < 0 ? arg : arg[..eq];
            if (eq < 0)
            {
                if (!tool.BareFlags.Contains(name, StringComparer.Ordinal))
                    return tool.ValueFlags.ContainsKey(name) ? $"{tool.Name} flag '{name}' needs a value: write {name}=<value>" : $"{tool.Name} flag '{name}' is not allowed";
                continue;
            }
            if (!tool.ValueFlags.TryGetValue(name, out var accepts)) return $"{tool.Name} flag '{name}' is not allowed";
            if (!accepts(arg[(eq + 1)..])) return $"{tool.Name} flag value in '{arg}' is not allowed";
        }
        return null;
    }

    /// <summary>The argv for a node-environment tool: <c>node node_modules/&lt;script&gt; &lt;leading&gt; &lt;args&gt;</c>. Call <see cref="CheckTool"/> first.</summary>
    public static IReadOnlyList<string> NodeToolArgv(NodeTool tool, IReadOnlyList<string> args) =>
        ["node", NodeModulesDir + "/" + tool.Script, .. tool.LeadingArgs, .. args];

    /// <summary>
    /// Validates the command of an exercise and yields the argv to run: <c>run</c> as written, or the argv built from <c>tool</c>/<c>args</c>.
    /// Exactly one of the two forms may be used.
    /// </summary>
    public static string? Resolve(string languageId, CommandSpec spec, out IReadOnlyList<string> argv)
    {
        argv = spec.Run;
        if (spec.Tool is null)
            return spec.Args is { Count: > 0 } ? "command.args is only valid together with command.tool" : Check(languageId, spec.Run);
        if (spec.Run.Count > 0) return "command declares both run and tool; use one of them";
        var problem = CheckTool(languageId, spec.Tool, spec.Args ?? []);
        if (problem is not null) return problem;
        argv = NodeToolArgv(FindNodeTool(languageId, spec.Tool)!, spec.Args ?? []);
        return null;
    }
}
