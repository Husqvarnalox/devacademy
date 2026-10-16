using AvaloniaEdit.Document;
using DevAcademy.Desktop.Services;
using DevAcademy.LanguageServices.Protocol;

namespace DevAcademy.Desktop.Controls.Lsp;

/// <summary>A jump to a place in some file (1-based line and column, UTF-16 columns as in the editor).</summary>
public sealed record LspNavigation(string FilePath, int Line, int Column);

/// <summary>
/// Describes what a <see cref="CodeEditorControl"/> is showing so it can talk to a language server: which service, which
/// app language, which workspace root and which on-disk path (for mirrored lesson buffers, the path inside the mirror).
/// </summary>
public sealed class LspTarget
{
    public required LanguageIntelligenceService Service { get; init; }
    /// <summary>The app language id (<c>go</c>, <c>cpp</c>, <c>python</c> ...).</summary>
    public required string LanguageId { get; init; }
    public required string Root { get; init; }
    public required string FilePath { get; init; }
    /// <summary>Set when the editor shows a shared <see cref="TextDocument"/> (project tabs); the binding only attaches when it is the one displayed.</summary>
    public TextDocument? Document { get; init; }
    /// <summary>Receives the diagnostics of this file whenever the server publishes them (called on the UI thread).</summary>
    public Action<LspTarget, IReadOnlyList<Diagnostic>>? DiagnosticsSink { get; init; }
    /// <summary>
    /// Opens another file of the same workspace (a tab, or the next exercise file) and returns true when it handled it;
    /// otherwise the editor shows a read-only peek.
    /// </summary>
    public Func<LspNavigation, bool>? Navigate { get; init; }
}
