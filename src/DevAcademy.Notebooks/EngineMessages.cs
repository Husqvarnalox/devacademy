using DevAcademy.Domain;

namespace DevAcademy.Notebooks;

/// <summary>
/// Catalog of the user-visible messages of the notebook layer (file-format errors, kernel failures, kernel installation, output notes). Each
/// <c>const</c> is a resource key a UI translates; the factory beside it holds the English template that stays the fallback. Kernel and pip output,
/// parser texts and file names are <see cref="LocalizedText.Literal"/> or arguments and are never translated.
/// </summary>
public static class EngineMessages
{
    public const string TooLargeKey = "Engine_Nb_TooLarge";
    public const string NotJsonObjectKey = "Engine_Nb_NotJsonObject";
    public const string InvalidJsonKey = "Engine_Nb_InvalidJson";
    public const string NotNotebookKey = "Engine_Nb_NotNotebook";
    public const string NotIpynbKey = "Engine_Nb_NotIpynb";
    public const string OnlyV4Key = "Engine_Nb_OnlyV4";
    public const string CellStructureKey = "Engine_Nb_CellStructure";
    public const string UnknownCellTypeKey = "Engine_Nb_UnknownCellType";
    public const string NoOutputTypeKey = "Engine_Nb_NoOutputType";

    public const string KernelStartExitedKey = "Engine_Nb_KernelStartExited";
    public const string KernelNoResponseKey = "Engine_Nb_KernelNoResponse";
    public const string KernelStoppedKey = "Engine_Nb_KernelStopped";
    public const string KernelExitedKey = "Engine_Nb_KernelExited";
    public const string KernelExitedPlainKey = "Engine_Nb_KernelExitedPlain";
    public const string KernelConnectionLostKey = "Engine_Nb_KernelConnectionLost";
    public const string KernelHeartbeatKey = "Engine_Nb_KernelHeartbeat";

    public const string InstallVenvOnlyKey = "Engine_Nb_InstallVenvOnly";
    public const string InstallTimeoutKey = "Engine_Nb_InstallTimeout";
    public const string PipFailedKey = "Engine_Nb_PipFailed";

    public const string HtmlNoteKey = "Engine_Nb_HtmlNote";
    public const string UnsupportedFormatKey = "Engine_Nb_UnsupportedFormat";

    public static LocalizedText TooLarge(long megabytes) => LocalizedText.Create(TooLargeKey, "The notebook is larger than {0} MB.", megabytes);
    public static LocalizedText NotJsonObject() => LocalizedText.Create(NotJsonObjectKey, "Not a JSON object.");
    public static LocalizedText InvalidJson(string reason) => LocalizedText.Create(InvalidJsonKey, "The file is not valid JSON: {0}", LocalizedText.Literal(reason));
    public static LocalizedText NotNotebook() => LocalizedText.Create(NotNotebookKey, "The file is not a notebook (top level is not an object).");
    public static LocalizedText NotIpynb() => LocalizedText.Create(NotIpynbKey, "Not an .ipynb file: no \"nbformat\" field.");
    public static LocalizedText OnlyV4(int major) =>
        LocalizedText.Create(OnlyV4Key, "Only nbformat 4 is supported (this file is nbformat {0}); convert it with nbconvert first.", major);
    public static LocalizedText CellStructure(string reason) =>
        LocalizedText.Create(CellStructureKey, "A cell has an unexpected structure: {0}", LocalizedText.Literal(reason));
    public static LocalizedText UnknownCellType(string type) => LocalizedText.Create(UnknownCellTypeKey, "Unknown cell_type '{0}'.", type);
    public static LocalizedText NoOutputType() => LocalizedText.Create(NoOutputTypeKey, "An output has no output_type.");

    public static LocalizedText KernelStartExited(int exitCode) =>
        LocalizedText.Create(KernelStartExitedKey, "The kernel process exited during startup (code {0}).", exitCode);
    public static LocalizedText KernelNoResponse() => LocalizedText.Create(KernelNoResponseKey, "The kernel did not respond in time.");
    public static LocalizedText KernelStopped() => LocalizedText.Create(KernelStoppedKey, "The kernel was stopped.");
    public static LocalizedText KernelExited(int exitCode) => LocalizedText.Create(KernelExitedKey, "The kernel process exited (code {0}).", exitCode);
    public static LocalizedText KernelExited() => LocalizedText.Create(KernelExitedPlainKey, "The kernel process exited.");
    public static LocalizedText KernelConnectionLost(string channel, string reason) =>
        LocalizedText.Create(KernelConnectionLostKey, "Connection to the kernel was lost ({0}): {1}", channel, LocalizedText.Literal(reason));
    public static LocalizedText KernelHeartbeat() => LocalizedText.Create(KernelHeartbeatKey, "The kernel is not responding (heartbeat signals lost).");

    public static LocalizedText InstallVenvOnly() => LocalizedText.Create(InstallVenvOnlyKey, "Installation is allowed only into a virtual environment.");
    public static LocalizedText InstallTimeout() => LocalizedText.Create(InstallTimeoutKey, "The installation did not finish within 10 minutes.");
    public static LocalizedText PipFailed(int exitCode, string output) =>
        LocalizedText.Create(PipFailedKey, "pip exited with code {0}.\n{1}", exitCode, LocalizedText.Literal(output));

    public static LocalizedText HtmlNote() => LocalizedText.Create(HtmlNoteKey, "HTML is not rendered; the text is shown instead.");
    public static LocalizedText UnsupportedFormat(string mime) => LocalizedText.Create(UnsupportedFormatKey, "[{0}: format not supported]", mime);
}
