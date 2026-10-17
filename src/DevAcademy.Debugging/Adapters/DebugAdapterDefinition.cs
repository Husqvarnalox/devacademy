using System.Text.RegularExpressions;

namespace DevAcademy.Debugging.Adapters;

/// <summary>How the adapter's launch request is shaped and where its executable comes from.</summary>
public enum AdapterKind { DebugPy, LldbDap, Delve, JsDebug, JavaJdi, NetCoreDbg }

/// <summary>
/// One debug adapter as data: which app languages it debugs, how it is found, and what to tell the user when it is missing.
/// Per-kind behaviour (launch configuration, provisioning) is selected by <see cref="Kind"/>.
/// </summary>
public sealed record DebugAdapterDefinition(
    string Id,
    string DisplayName,
    AdapterKind Kind,
    IReadOnlyList<string> Languages,
    /// <summary>Executable names tried in order (PATH + well-known dirs). Empty for python, whose adapter is a module run by the interpreter.</summary>
    IReadOnlyList<string> ExecutableNames,
    string InstallHint)
{
    /// <summary>Pinned Delve release installed by the explicit "install" action (GOBIN = the runner cache).</summary>
    public const string DelvePinnedVersion = "v1.26.3";
    public const string DelvePackage = "github.com/go-delve/delve/cmd/dlv";

    /// <summary>Pinned vscode-js-debug release (standalone DAP server, MIT). The tarball is verified against <see cref="JsDebugSha256"/> before anything is unpacked.</summary>
    public const string JsDebugPinnedVersion = "v1.140.0";
    public const string JsDebugSha256 = "27dab92937ec1ab35821ae955aac867544fe06a1b6307229049f2d789af10968";
    /// <summary>Pinned netcoredbg (Samsung, MIT) release. Only macOS arm64 is installed automatically (it is the one tested end to end); other platforms get manual instructions.</summary>
    public const string NetCoreDbgVersion = "3.2.0-1092";
    public const string NetCoreDbgOsxArm64Sha256 = "f4fa33b3ff874910cc184b4bb3b9c56d0abdf5c6521cee0b144d7c6e4a6e59ea";
    public static string NetCoreDbgOsxArm64Url => $"https://github.com/Samsung/netcoredbg/releases/download/{NetCoreDbgVersion}/netcoredbg-osx-arm64.zip";
    public static string JsDebugUrl => $"https://github.com/microsoft/vscode-js-debug/releases/download/{JsDebugPinnedVersion}/js-debug-dap-{JsDebugPinnedVersion}.tar.gz";
}

/// <summary>An adapter that can be started: executable + arguments + environment, and how to reach it.</summary>
public sealed record ResolvedAdapter(
    DebugAdapterDefinition Definition,
    string Executable,
    IReadOnlyList<string> Args,
    IReadOnlyDictionary<string, string> Environment,
    /// <summary>Null: DAP over stdio. Otherwise the pattern whose first group captures "host:port" printed on stdout (TCP adapters).</summary>
    Regex? Announce = null);

/// <summary>A language the app can debug and the adapter that does it; the registry is data, like the LSP one.</summary>
public static class DebugAdapterRegistry
{
    public static IReadOnlyList<DebugAdapterDefinition> Defaults { get; } =
    [
        new("debugpy", "debugpy", AdapterKind.DebugPy, ["python"], [],
            "pip install debugpy into the selected virtual environment"),
        new("lldb-dap", "lldb-dap", AdapterKind.LldbDap, ["cpp", "rust"], ["lldb-dap", "lldb-vscode"],
            "macOS: xcode-select --install (lldb-dap ships with Xcode / Command Line Tools). Linux: apt install lldb. Windows: install LLVM."),
        new("delve", "Delve (dlv dap)", AdapterKind.Delve, ["go"], ["dlv"],
            $"go install {DebugAdapterDefinition.DelvePackage}@{DebugAdapterDefinition.DelvePinnedVersion}"),
        new("js-debug", "vscode-js-debug", AdapterKind.JsDebug, ["javascript", "typescript"], [],
            $"Node.js plus vscode-js-debug {DebugAdapterDefinition.JsDebugPinnedVersion} from GitHub releases (checksum-verified)"),
        new("java-jdi", "Java (JDI)", AdapterKind.JavaJdi, ["java"], [],
            "A JDK (17+) with the jdk.jdi module: the adapter ships with the app as source and is compiled locally with javac (no download)"),
        new("netcoredbg", "netcoredbg", AdapterKind.NetCoreDbg, ["csharp"], [],
            $"netcoredbg {DebugAdapterDefinition.NetCoreDbgVersion} (Samsung, MIT) from GitHub releases (checksum-verified); installed automatically on macOS arm64 only"),
    ];

    /// <summary>Languages with a registered adapter whose adapter is planned only (no DAP adapter shipped with the app yet).</summary>
    public static IReadOnlyDictionary<string, string> Planned { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    public static DebugAdapterDefinition? ForLanguage(string languageId, IReadOnlyList<DebugAdapterDefinition>? defs = null) =>
        (defs ?? Defaults).FirstOrDefault(d => d.Languages.Contains(languageId, StringComparer.Ordinal));
}
