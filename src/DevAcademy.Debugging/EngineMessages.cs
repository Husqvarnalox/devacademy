using DevAcademy.Domain;

namespace DevAcademy.Debugging;

/// <summary>
/// Catalog of the user-visible messages of the debugging layer (adapter statuses and session start-up failures). Each <c>const</c> is a
/// resource key a UI translates; the factory beside it holds the English template that stays the fallback. Paths, versions, URLs, checksums,
/// commands and adapter output are arguments or <see cref="LocalizedText.Literal"/> and are never translated.
/// </summary>
public static class EngineMessages
{
    public const string NoPythonEnvironmentKey = "Engine_Debug_NoPythonEnvironment";
    public const string DebugpyNoVenvKey = "Engine_Debug_DebugpyNoVenv";
    public const string DebugpyNotInstalledKey = "Engine_Debug_DebugpyNotInstalled";
    public const string LldbNotFoundKey = "Engine_Debug_LldbNotFound";
    public const string DelveNoGoKey = "Engine_Debug_DelveNoGo";
    public const string DelveNotInstalledKey = "Engine_Debug_DelveNotInstalled";
    public const string NodeMissingKey = "Engine_Debug_NodeMissing";
    public const string JsDebugWindowsKey = "Engine_Debug_JsDebugWindows";
    public const string JsDebugNotInstalledKey = "Engine_Debug_JsDebugNotInstalled";
    public const string DotNetMissingKey = "Engine_Debug_DotNetMissing";
    public const string NetCoreDbgNotInstalledKey = "Engine_Debug_NetCoreDbgNotInstalled";
    public const string NetCoreDbgManualKey = "Engine_Debug_NetCoreDbgManual";
    public const string JdkNotFoundKey = "Engine_Debug_JdkNotFound";
    public const string NoJdiKey = "Engine_Debug_NoJdi";
    public const string JavaNotBuiltKey = "Engine_Debug_JavaNotBuilt";
    public const string UnknownAdapterKey = "Engine_Debug_UnknownAdapter";
    public const string AdapterFailedKey = "Engine_Debug_AdapterFailed";
    public const string NoDebuggerKey = "Engine_Debug_NoDebugger";
    public const string AdapterStoppedKey = "Engine_Debug_AdapterStopped";
    public const string SessionClosedKey = "Engine_Debug_SessionClosed";

    public const string HintDebugpyKey = "Engine_Debug_Hint_Debugpy";
    public const string HintLldbKey = "Engine_Debug_Hint_Lldb";
    public const string HintJsDebugKey = "Engine_Debug_Hint_JsDebug";
    public const string HintJavaKey = "Engine_Debug_Hint_Java";
    public const string HintNetCoreDbgKey = "Engine_Debug_Hint_NetCoreDbg";

    public static LocalizedText NoPythonEnvironment() => LocalizedText.Create(NoPythonEnvironmentKey,
        "Select a Python environment (a project virtual environment or a managed one): the debugpy debugger is installed only into it.");
    public static LocalizedText DebugpyNoVenv() => LocalizedText.Create(DebugpyNoVenvKey,
        "debugpy is not installed, and the selected interpreter is not in a virtual environment. The app installs nothing into the system Python: create a .venv or a managed environment.");
    public static LocalizedText DebugpyNotInstalled() => LocalizedText.Create(DebugpyNotInstalledKey, "debugpy is not installed in the selected environment.");
    public static LocalizedText LldbNotFound(LocalizedText hint) => LocalizedText.Create(LldbNotFoundKey, "lldb-dap not found. {0}", hint);
    public static LocalizedText DelveNoGo() => LocalizedText.Create(DelveNoGoKey, "dlv not found, and Go is not installed: install Go (https://go.dev/dl/), then Delve.");
    public static LocalizedText DelveNotInstalled() => LocalizedText.Create(DelveNotInstalledKey, "Delve (dlv) is not installed.");
    public static LocalizedText NodeMissing() => LocalizedText.Create(NodeMissingKey,
        "Node.js not found: it is needed both to run the program and for the debugger (https://nodejs.org/).");
    public static LocalizedText JsDebugWindows(string url, string sha256, string directory) => LocalizedText.Create(JsDebugWindowsKey,
        "vscode-js-debug is not installed. Automatic installation on Windows has not been tested: unpack {0} (sha256 {1}) into {2}.", url, sha256, directory);
    public static LocalizedText JsDebugNotInstalled(string version) => LocalizedText.Create(JsDebugNotInstalledKey, "vscode-js-debug {0} is not installed.", version);
    public static LocalizedText DotNetMissing() => LocalizedText.Create(DotNetMissingKey,
        ".NET SDK not found: it is needed both to build the program and for the debugger (https://dotnet.microsoft.com/download).");
    public static LocalizedText NetCoreDbgNotInstalled(string version) => LocalizedText.Create(NetCoreDbgNotInstalledKey, "netcoredbg {0} is not installed.", version);
    public static LocalizedText NetCoreDbgManual(string version, string directory, string executable) => LocalizedText.Create(NetCoreDbgManualKey,
        "Automatic installation of netcoredbg has been verified only on macOS arm64. On this platform download a release from https://github.com/Samsung/netcoredbg/releases "
        + "(Linux x64/arm64 and Windows x64 are in {0}; macOS x64 is in 3.1.3-1062) and unpack it into {1} so that {2} appears. This has not been tested.",
        version, directory, executable);
    public static LocalizedText JdkNotFound(LocalizedText hint) => LocalizedText.Create(JdkNotFoundKey, "JDK not found. {0}", hint);
    public static LocalizedText NoJdi() => LocalizedText.Create(NoJdiKey,
        "The installed Java has no jdk.jdi module (a full JDK is needed, not a JRE): without it the Java debugger cannot be built.");
    public static LocalizedText JavaNotBuilt() => LocalizedText.Create(JavaNotBuiltKey, "The Java debugger has not been built yet.");
    public static LocalizedText UnknownAdapter() => LocalizedText.Create(UnknownAdapterKey, "Unknown adapter.");
    public static LocalizedText AdapterFailed(string name, Exception error) =>
        LocalizedText.Create(AdapterFailedKey, "{0}: {1}", name, error is ILocalizedMessage l ? l.Text : LocalizedText.Literal(error.Message));
    public static LocalizedText NoDebugger() => LocalizedText.Create(NoDebuggerKey, "Debugging is not supported for this language yet.");
    public static LocalizedText AdapterStopped(string logTail) => LocalizedText.Create(AdapterStoppedKey, "The debug adapter stopped during startup. {0}", LocalizedText.Literal(logTail));
    public static LocalizedText SessionClosed() => LocalizedText.Create(SessionClosedKey, "The debug session closed during startup.");

    /// <summary>The install hint of an adapter: prose is translatable, a bare command (Delve) stays verbatim.</summary>
    public static LocalizedText InstallHint(Adapters.DebugAdapterDefinition def) => def.Kind switch
    {
        Adapters.AdapterKind.DebugPy => LocalizedText.Create(HintDebugpyKey, def.InstallHint),
        Adapters.AdapterKind.LldbDap => LocalizedText.Create(HintLldbKey, def.InstallHint),
        Adapters.AdapterKind.JsDebug => LocalizedText.Create(HintJsDebugKey, def.InstallHint, Adapters.DebugAdapterDefinition.JsDebugPinnedVersion),
        Adapters.AdapterKind.JavaJdi => LocalizedText.Create(HintJavaKey, def.InstallHint),
        Adapters.AdapterKind.NetCoreDbg => LocalizedText.Create(HintNetCoreDbgKey, def.InstallHint, Adapters.DebugAdapterDefinition.NetCoreDbgVersion),
        _ => LocalizedText.Literal(def.InstallHint),
    };
}
