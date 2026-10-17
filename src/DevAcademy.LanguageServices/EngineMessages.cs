using DevAcademy.Domain;

namespace DevAcademy.LanguageServices;

/// <summary>
/// Catalog of the user-visible messages of the language-server layer (server status texts and install hints). Each <c>const</c> is a resource
/// key a UI translates; the factory beside it holds the English template that stays the fallback. Server output, exception texts and bare install
/// commands are <see cref="LocalizedText.Literal"/> and are never translated.
/// </summary>
public static class EngineMessages
{
    public const string NoServerDefinedKey = "Engine_Lsp_NoServerDefined";
    public const string InitTimeoutKey = "Engine_Lsp_InitTimeout";
    public const string ExitedDuringInitKey = "Engine_Lsp_ExitedDuringInit";
    public const string InitFailedKey = "Engine_Lsp_InitFailed";
    public const string ExitedUnexpectedlyKey = "Engine_Lsp_ExitedUnexpectedly";
    public const string ProcessExitedKey = "Engine_Lsp_ProcessExited";
    public const string NpmTimedOutKey = "Engine_Lsp_NpmTimedOut";
    public const string CrashedKey = "Engine_Lsp_Crashed";

    public const string HintClangdKey = "Engine_Lsp_Hint_clangd";
    public const string HintPyrightKey = "Engine_Lsp_Hint_pyright";
    public const string HintJdtlsKey = "Engine_Lsp_Hint_jdtls";

    public static LocalizedText NoServerDefined() => LocalizedText.Create(NoServerDefinedKey, "no language server is defined");
    public static LocalizedText InitTimeout() => LocalizedText.Create(InitTimeoutKey, "initialize timed out");
    public static LocalizedText ExitedDuringInit() => LocalizedText.Create(ExitedDuringInitKey, "the server exited during initialize");
    public static LocalizedText InitFailed(string reason) => LocalizedText.Create(InitFailedKey, "initialize failed: {0}", LocalizedText.Literal(reason));
    public static LocalizedText ExitedUnexpectedly() => LocalizedText.Create(ExitedUnexpectedlyKey, "the server process exited unexpectedly");
    public static LocalizedText ProcessExited() => LocalizedText.Create(ProcessExitedKey, "the server process exited");
    public static LocalizedText NpmTimedOut() => LocalizedText.Create(NpmTimedOutKey, "npm install timed out.");
    public static LocalizedText Crashed(int times, LocalizedText detail) => LocalizedText.Create(CrashedKey, "crashed {0} times; {1}", times, detail);

    /// <summary>The install hint of a server: prose is translatable, a bare command stays verbatim.</summary>
    public static LocalizedText InstallHint(string definitionId, string hint) => definitionId switch
    {
        "clangd" => LocalizedText.Create(HintClangdKey, hint),
        "pyright" => LocalizedText.Create(HintPyrightKey, hint),
        "jdtls" => LocalizedText.Create(HintJdtlsKey, hint),
        _ => LocalizedText.Literal(hint),
    };
}
