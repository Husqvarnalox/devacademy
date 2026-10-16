namespace DevAcademy.Terminal;

/// <summary>The program started inside a new terminal.</summary>
public sealed record ShellSpec(string Path, IReadOnlyList<string> Args, string DisplayName);

/// <summary>Picks the user's own shell: $SHELL (fallback zsh, bash, sh) on Unix; pwsh, powershell, cmd on Windows.</summary>
public static class ShellResolver
{
    public static ShellSpec Resolve(Func<string, string?> getEnv, bool isWindows, Func<string, bool> fileExists, Func<string, string?> findOnPath)
    {
        if (isWindows)
        {
            foreach (var name in new[] { "pwsh.exe", "powershell.exe" })
                if (findOnPath(name) is { } p) return new ShellSpec(p, ["-NoLogo"], System.IO.Path.GetFileNameWithoutExtension(name));
            var comSpec = getEnv("ComSpec");
            return new ShellSpec(string.IsNullOrWhiteSpace(comSpec) ? "cmd.exe" : comSpec, [], "cmd");
        }

        var candidates = new List<string>();
        if (getEnv("SHELL") is { Length: > 0 } fromEnv) candidates.Add(fromEnv);
        candidates.AddRange(["/bin/zsh", "/bin/bash", "/bin/sh"]);
        var shell = candidates.FirstOrDefault(c => System.IO.Path.IsPathRooted(c) && fileExists(c)) ?? "/bin/sh";
        var name2 = System.IO.Path.GetFileName(shell);
        IReadOnlyList<string> args = name2 is "zsh" or "bash" or "fish" ? ["-l"] : [];
        return new ShellSpec(shell, args, name2);
    }

    public static ShellSpec ResolveCurrent() => Resolve(Environment.GetEnvironmentVariable, OperatingSystem.IsWindows(), File.Exists, FindOnPath);

    private static string? FindOnPath(string exe)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path)) return null;
        foreach (var dir in path.Split(System.IO.Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = System.IO.Path.Combine(dir.Trim('"'), exe);
                if (File.Exists(candidate)) return candidate;
            }
            catch (ArgumentException) { /* malformed PATH entry */ }
        }
        return null;
    }
}
