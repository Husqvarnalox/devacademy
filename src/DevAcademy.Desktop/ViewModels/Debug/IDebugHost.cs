using DevAcademy.Debugging.Adapters;

namespace DevAcademy.Desktop.ViewModels;

/// <summary>What a debug run needs from the place it is started in. Paths in <see cref="PathByKey"/> are the real paths the debugger sees.</summary>
public sealed record DebugPreparation(DebugLaunch? Launch, string? Error, string? CleanupDirectory, IReadOnlyDictionary<string, string> PathByKey);

/// <summary>A project workspace or a lesson Practice session, as seen by <see cref="DebugViewModel"/>.</summary>
public interface IDebugHost
{
    string? LanguageId { get; }
    /// <summary>Non-null: debugging is not possible here at all (for example a command-graded exercise), with the reason.</summary>
    string? DisabledReason { get; }
    BreakpointStore Breakpoints { get; }
    /// <summary>The interpreter of the selected Python environment (python only).</summary>
    Task<string?> GetPythonInterpreterAsync(CancellationToken ct);
    /// <summary>Saves files and builds a debug binary where the language needs one.</summary>
    Task<DebugPreparation> PrepareAsync(CancellationToken ct);
    /// <summary>Shows the file at the line (the paused position).</summary>
    Task RevealAsync(string key, int line);
}
