namespace DevAcademy.Domain;

/// <summary>
/// Which executables a <see cref="EvaluationKind.Command"/> exercise may run. Content never names arbitrary binaries:
/// the first argv element must be a known tool of the exercise's language (the runner resolves it to the detected path),
/// and flags that would run other programs or code are rejected.
/// </summary>
public static class CommandPolicy
{
    public const int DefaultTimeoutSeconds = 20;
    public const int MaxTimeoutSeconds = 60;

    private static readonly string[] NodeForbidden =
        ["-e", "--eval", "-p", "--print", "-r", "--require", "--import", "--loader", "--experimental-loader", "--env-file", "--inspect", "--inspect-brk"];

    private static readonly string[] GoForbidden =
        ["-exec", "-toolexec", "-vettool", "-C", "-modfile", "-overlay", "-pgo", "-ldflags", "-gcflags", "-asmflags"];

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
}
