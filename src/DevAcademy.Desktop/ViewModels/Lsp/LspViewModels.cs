using DevAcademy.Desktop.Localization;
using DevAcademy.Desktop.Services;
using DevAcademy.LanguageServices.Hosting;
using DevAcademy.LanguageServices.Protocol;
using DevAcademy.LanguageServices.Servers;

namespace DevAcademy.Desktop.ViewModels.Lsp;

/// <summary>One row of the "Problems" list: a server diagnostic with 1-based line/column.</summary>
public sealed record ProblemItemViewModel(string Path, int Line, int Column, DiagnosticSeverity Severity, string Message, string? Source)
{
    public string Location => $"{Path}:{Line}:{Column}";
    public string Glyph => Severity == DiagnosticSeverity.Error ? "●" : "▲";
    public bool IsError => Severity == DiagnosticSeverity.Error;
    public bool IsWarning => Severity != DiagnosticSeverity.Error;

    public static ProblemItemViewModel From(string path, Diagnostic d) =>
        new(path, d.Range.Start.Line + 1, d.Range.Start.Character + 1, d.EffectiveSeverity, d.Message, d.Source);
}

/// <summary>What the "LSP: gopls ✓" status item shows for a server state.</summary>
public sealed record LspStatusView(string Text, string Tooltip, bool IsVisible, bool NeedsHelp, bool IsError, LanguageServerDefinition? Definition)
{
    public static LspStatusView None { get; } = new("", "", false, false, false, null);

    public static LspStatusView From(ServerStatus s, LanguageServerDefinition? def)
    {
        if (def is null || string.IsNullOrEmpty(s.DefinitionId)) return None;
        var name = s.DisplayName;
        var tip = s.Message is null ? Loc.T("Lsp_StatusTip") : Loc.Of(s.MessageText, s.Message);
        return s.State switch
        {
            ServerState.Ready => new(Loc.F("Lsp_Ready", name), tip, true, false, false, def),
            ServerState.Starting => new(Loc.F("Lsp_Starting", name), tip, true, false, false, def),
            ServerState.Restarting => new(Loc.F("Lsp_Restarting", name), tip, true, false, true, def),
            ServerState.Error => new(Loc.F("Lsp_Error", name), tip, true, false, true, def),
            ServerState.NotInstalled => new(Loc.F("Lsp_NotInstalled", name), Loc.Of(s.InstallHintText), true, true, false, def),
            ServerState.Disabled => new(Loc.T("Lsp_Disabled"), tip, true, false, false, def),
            _ => new(Loc.F("Lsp_Stopped", name), tip, true, false, false, def),
        };
    }
}
