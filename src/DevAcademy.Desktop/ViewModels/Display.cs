using DevAcademy.Desktop.Localization;
using DevAcademy.Domain;
using DevAcademy.Execution;

namespace DevAcademy.Desktop.ViewModels;

/// <summary>Localized labels for domain enums and editor language metadata.</summary>
public static class Display
{
    public static string Kind(TrackKind k) => Loc.T("Kind_" + k);
    public static string Difficulty(Difficulty d) => Loc.T("Difficulty_" + d);
    public static string State(LessonState s) => Loc.T("State_" + s);
    public static string Source(CompletionSource? s) => s is null ? "" : Loc.T("Source_" + s);
    public static string Fact(FactKind k) => Loc.T("Fact_" + k);
    public static string Verdict(Verdict v) => Loc.T("Verdict_" + v);
    public static string Run(ExecutionStatus s) => Loc.T("Run_" + s);

    public static string Isolation(IsolationLevel level) => level switch
    {
        IsolationLevel.OsSandbox when OperatingSystem.IsMacOS() => Loc.T("Isolation_Mac"),
        IsolationLevel.OsSandbox when OperatingSystem.IsLinux() => Loc.T("Isolation_Linux"),
        IsolationLevel.OsSandbox => Loc.T("Isolation_Os"),
        IsolationLevel.Container => Loc.T("Isolation_Container"),
        _ => Loc.T("Isolation_Limits"),
    };

    public static string Duration(TimeSpan d) =>
        d.TotalSeconds < 1 ? Loc.F("Duration_Ms", (int)d.TotalMilliseconds) : Loc.F("Duration_S", d.TotalSeconds.ToString("0.0", System.Globalization.CultureInfo.CurrentCulture));

    /// <summary>File extension (with dot) used for syntax highlighting and scratch files.</summary>
    public static string Extension(string languageId) => languageId switch
    {
        "python" => ".py",
        "javascript" => ".js",
        "typescript" => ".ts",
        "go" => ".go",
        "java" => ".java",
        "kotlin" => ".kt",
        "cpp" => ".cpp",
        "rust" => ".rs",
        "csharp" => ".cs",
        "php" => ".php",
        _ => ".txt",
    };

    public static string LanguageName(string languageId) => languageId switch
    {
        "python" => "Python",
        "javascript" => "JavaScript",
        "typescript" => "TypeScript",
        "go" => "Go",
        "java" => "Java",
        "kotlin" => "Kotlin",
        "cpp" => "C++",
        "rust" => "Rust",
        "csharp" => "C#",
        "php" => "PHP",
        _ => languageId,
    };

    public static string ScratchFileName(string languageId) =>
        languageId == "java" ? "Main.java" : "scratch" + Extension(languageId);
}
