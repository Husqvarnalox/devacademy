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
    bool GoBuildPackage = false,
    /// <summary>
    /// The run command after <see cref="Program"/> as produced by the debug build: <c>node [flags] entry</c>, <c>java [options] -cp dirs MainClass</c>.
    /// Its last element is the program (entry file or main class); the elements before it are runtime options.
    /// </summary>
    IReadOnlyList<string>? RunArguments = null);

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
            AdapterKind.JsDebug => JsLaunch(launch, args),
            AdapterKind.JavaJdi => JavaLaunch(launch, args),
            AdapterKind.NetCoreDbg => NetCoreLaunch(launch),
            _ => throw new ArgumentOutOfRangeException(nameof(adapter)),
        };
    }

    /// <summary>vscode-js-debug <c>pwa-node</c>: Node runs the entry file directly (TypeScript through type stripping keeps the line numbers, so no source maps).</summary>
    private static JsonObject JsLaunch(DebugLaunch launch, JsonArray args)
    {
        var run = launch.RunArguments ?? [launch.Program];
        string entry = Path.GetFullPath(run[^1], launch.WorkingDirectory);
        bool hasRuntime = launch.RunArguments is not null;
        var config = new JsonObject
        {
            ["name"] = "DevAcademy",
            ["type"] = "pwa-node",
            ["request"] = "launch",
            ["program"] = entry,
            ["args"] = args,
            ["cwd"] = launch.WorkingDirectory,
            ["runtimeArgs"] = new JsonArray(run.Take(run.Count - 1).Select(a => (JsonNode?)JsonValue.Create(a)).ToArray()),
            ["console"] = "internalConsole",
            ["stopOnEntry"] = false,
            ["sourceMaps"] = false,
            ["skipFiles"] = new JsonArray("<node_internals>/**"),
            ["autoAttachChildProcesses"] = false,
        };
        if (hasRuntime) config["runtimeExecutable"] = launch.Program;
        return config;
    }

    /// <summary>netcoredbg: the debug build's run command is <c>dotnet app.dll</c>; the host is the program and the dll its first argument.</summary>
    private static JsonObject NetCoreLaunch(DebugLaunch launch) => new()
    {
        ["name"] = "DevAcademy",
        ["type"] = "coreclr",
        ["request"] = "launch",
        ["program"] = launch.Program,
        ["args"] = new JsonArray((launch.RunArguments ?? []).Concat(launch.Args).Select(a => (JsonNode?)JsonValue.Create(a)).ToArray()),
        ["cwd"] = launch.WorkingDirectory,
        ["stopAtEntry"] = false,
        ["justMyCode"] = true,
        ["env"] = new JsonObject { ["DOTNET_NOLOGO"] = "1", ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1" },
    };

    /// <summary>
    /// The JDI adapter: main class and class path from the debug build's run command (<c>java [vm options] -cp dir MainClass</c>); the program's
    /// working directory is the adapter's, which the session starts in <see cref="DebugLaunch.WorkingDirectory"/>.
    /// </summary>
    private static JsonObject JavaLaunch(DebugLaunch launch, JsonArray args)
    {
        var run = launch.RunArguments ?? ["-cp", launch.WorkingDirectory, launch.Program];
        string? classpath = null;
        var vmArgs = new JsonArray();
        for (int i = 0; i < run.Count - 1; i++)
        {
            if (run[i] is "-cp" or "-classpath" or "--class-path" && i + 1 < run.Count - 1) classpath = run[++i];
            else vmArgs.Add(run[i]);
        }
        return new JsonObject
        {
            ["name"] = "DevAcademy",
            ["type"] = "java",
            ["request"] = "launch",
            ["mainClass"] = run[^1],
            ["classpath"] = classpath ?? launch.WorkingDirectory,
            ["vmArgs"] = vmArgs,
            ["args"] = args,
            ["cwd"] = launch.WorkingDirectory,
            ["sourcePaths"] = new JsonArray(launch.WorkingDirectory),
        };
    }
}
