using System.Text.Json.Nodes;

namespace DevAcademy.Debugging.Adapters;

/// <summary>What to run under the debugger. Paths are absolute.</summary>
public sealed record DebugLaunch(
    string Program,
    string WorkingDirectory,
    IReadOnlyList<string> Args,
    /// <summary>Python: the interpreter that runs the debuggee (the selected environment's).</summary>
    string? PythonInterpreter = null,
    /// <summary>Go only: debug a package directory (Delve builds it) instead of running a prebuilt binary.</summary>
    bool GoBuildPackage = false);

/// <summary>Builds the per-adapter <c>launch</c> request arguments.</summary>
public static class LaunchConfigurations
{
    public static JsonObject Create(DebugAdapterDefinition adapter, DebugLaunch launch)
    {
        var args = new JsonArray(launch.Args.Select(a => (JsonNode?)JsonValue.Create(a)).ToArray());
        return adapter.Kind switch
        {
            AdapterKind.DebugPy => new JsonObject
            {
                ["name"] = "DevAcademy",
                ["type"] = "python",
                ["request"] = "launch",
                ["program"] = launch.Program,
                ["args"] = args,
                ["cwd"] = launch.WorkingDirectory,
                ["python"] = new JsonArray(launch.PythonInterpreter ?? "python"),
                ["console"] = "internalConsole",
                ["redirectOutput"] = true,
                ["justMyCode"] = true,
                ["stopOnEntry"] = false,
                ["env"] = new JsonObject { ["PYTHONIOENCODING"] = "utf-8", ["PYTHONUNBUFFERED"] = "1" },
            },
            AdapterKind.LldbDap => new JsonObject
            {
                ["name"] = "DevAcademy",
                ["request"] = "launch",
                ["program"] = launch.Program,
                ["args"] = args,
                ["cwd"] = launch.WorkingDirectory,
                ["stopOnEntry"] = false,
            },
            AdapterKind.Delve => new JsonObject
            {
                ["name"] = "DevAcademy",
                ["type"] = "go",
                ["request"] = "launch",
                ["mode"] = launch.GoBuildPackage ? "debug" : "exec",
                ["program"] = launch.Program,
                ["args"] = args,
                ["cwd"] = launch.WorkingDirectory,
                ["stopOnEntry"] = false,
                ["buildFlags"] = "-gcflags=all=-N -l",
            },
            _ => throw new ArgumentOutOfRangeException(nameof(adapter)),
        };
    }
}
