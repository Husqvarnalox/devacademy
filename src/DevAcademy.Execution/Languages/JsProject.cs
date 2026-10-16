using System.Text.Json;

namespace DevAcademy.Execution;

public enum JsPackageManager { Npm, Pnpm, Yarn }

/// <summary>
/// What the workspace buttons need to know about a JavaScript/TypeScript project: which package manager it uses (from its lockfile, so the
/// user's choice is respected) and which <c>package.json</c> scripts exist. Reading only; nothing is run.
/// </summary>
public sealed record JsProjectInfo(JsPackageManager Manager, bool FromLockfile, IReadOnlySet<string> Scripts)
{
    public string ManagerName => Manager switch { JsPackageManager.Pnpm => "pnpm", JsPackageManager.Yarn => "yarn", _ => "npm" };

    public string InstallCommand => ManagerName + " install";

    public bool HasScript(string name) => Scripts.Contains(name);

    /// <summary>The visible command for a package.json script, e.g. <c>pnpm run dev</c>.</summary>
    public string ScriptCommand(string script) => $"{ManagerName} run {script}";

    /// <summary>"dev", else "start" (Express, Next-like projects without a dev script); null when neither exists.</summary>
    public string? DevScript => HasScript("dev") ? "dev" : HasScript("start") ? "start" : null;
}

public static class JsProjects
{
    /// <summary>Null when <paramref name="projectDir"/> has no readable package.json.</summary>
    public static JsProjectInfo? Detect(string projectDir)
    {
        var pkg = Path.Combine(projectDir, "package.json");
        if (!File.Exists(pkg)) return null;
        var scripts = new HashSet<string>(StringComparer.Ordinal);
        string? declared = null;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(pkg), new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                if (doc.RootElement.TryGetProperty("scripts", out var s) && s.ValueKind == JsonValueKind.Object)
                    foreach (var p in s.EnumerateObject()) scripts.Add(p.Name);
                if (doc.RootElement.TryGetProperty("packageManager", out var pm) && pm.ValueKind == JsonValueKind.String) declared = pm.GetString();
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException) { }

        var (manager, fromLock) = ManagerFromLockfile(projectDir) is { } m ? (m, true) : (ManagerFromField(declared) ?? JsPackageManager.Npm, false);
        return new JsProjectInfo(manager, fromLock, scripts);
    }

    /// <summary>pnpm-lock.yaml gives pnpm, yarn.lock gives yarn, package-lock.json / npm-shrinkwrap.json give npm; null when there is no lockfile.</summary>
    public static JsPackageManager? ManagerFromLockfile(string projectDir)
    {
        bool Has(string n) => File.Exists(Path.Combine(projectDir, n));
        if (Has("pnpm-lock.yaml")) return JsPackageManager.Pnpm;
        if (Has("yarn.lock")) return JsPackageManager.Yarn;
        if (Has("package-lock.json") || Has("npm-shrinkwrap.json")) return JsPackageManager.Npm;
        return null;
    }

    private static JsPackageManager? ManagerFromField(string? packageManager) => packageManager?.Split('@')[0] switch
    {
        "pnpm" => JsPackageManager.Pnpm,
        "yarn" => JsPackageManager.Yarn,
        "npm" => JsPackageManager.Npm,
        _ => null,
    };
}
