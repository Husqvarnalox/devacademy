using DevAcademy.AI;
using DevAcademy.Application.Tutor;
using DevAcademy.Desktop.Localization;

namespace DevAcademy.Desktop.ViewModels;

/// <summary>Maps AI-layer failures and refusals to clear Russian messages. The provider's own message is always appended.</summary>
public static class AiMessages
{
    public static string Describe(Exception e) => e switch
    {
        AiProviderException a => Loc.F("AiError_" + a.Kind, Loc.Msg(a)),
        TutorRefusedException t => Refusal(t),
        SecretStoreException s => Loc.F("AiError_SecretStore", s.Message),
        OperationCanceledException => Loc.T("AiError_Cancelled"),
        _ => Loc.F("AiError_Unexpected", e.Message),
    };

    public static string Refusal(TutorRefusedException t) => Loc.F("AiRefusal_" + t.Reason, t.Message);

    /// <summary>Refusals the learner fixes in Settings → AI.</summary>
    public static bool NeedsSettings(TutorRefusalReason r) =>
        r is TutorRefusalReason.AiDisabled or TutorRefusalReason.NoModelConfigured or TutorRefusalReason.UnknownProvider
            or TutorRefusalReason.SecretStoreUnavailable or TutorRefusalReason.NoApiKey or TutorRefusalReason.SpendLimit;

    public static string ScenarioName(TutorScenario s) => Loc.T("AiScenario_" + s);

    public static string ScenarioName(string scenario) =>
        Enum.TryParse<TutorScenario>(scenario, out var s) ? ScenarioName(s) : scenario == "ModelTest" ? Loc.T("AiScenario_ModelTest") : scenario;
}
