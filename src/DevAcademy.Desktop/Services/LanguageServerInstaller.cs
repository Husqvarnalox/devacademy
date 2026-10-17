using DevAcademy.Desktop.Composition;
using DevAcademy.Desktop.Localization;
using DevAcademy.LanguageServices.Servers;

namespace DevAcademy.Desktop.Services;

/// <summary>
/// The "how do I install this server?" flow shared by the status bar item and Settings. Nothing is downloaded unless the user
/// confirms a dialog that shows the exact command; servers that are not npm packages only get instructions.
/// </summary>
public static class LanguageServerInstaller
{
    /// <returns>True when a server was installed.</returns>
    public static async Task<bool> OfferAsync(AppServices s, LanguageServerDefinition def)
    {
        var plan = s.Language.PlanInstall(def);
        if (plan is null)
        {
            var key = def.NpmPackages.Count > 0 ? "Lsp_ManualInstallNoNpm" : "Lsp_ManualInstall";
            await s.Dialogs.ShowMessageAsync(Loc.T("Lsp_HowToInstallTitle"), Loc.F(key, def.DisplayName, Loc.Of(def.InstallHintText)));
            return false;
        }
        var ok = await s.Dialogs.ConfirmAsync(Loc.T("Lsp_HowToInstallTitle"), Loc.F("Lsp_InstallConfirm", plan.Display), Loc.T("Lsp_InstallButton"));
        if (!ok) return false;
        var result = await s.Language.InstallAsync(def, plan);
        await s.Dialogs.ShowMessageAsync(Loc.T("Lsp_HowToInstallTitle"),
            result.Success ? Loc.F("Lsp_InstallDone", def.DisplayName) : Loc.F("Lsp_InstallFailed", def.DisplayName, Tail(Loc.Of(result.OutputText, result.Output))));
        return result.Success;
    }

    private static string Tail(string s) => s.Length <= 600 ? s : "…" + s[^600..];
}
