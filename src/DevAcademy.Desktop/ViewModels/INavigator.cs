namespace DevAcademy.Desktop.ViewModels;

public interface INavigator
{
    Task ShowCatalogAsync();
    Task ShowDashboardAsync();
    /// <summary>Opens the practice page (problems and placement checks).</summary>
    Task ShowProblemsAsync();
    Task OpenCourseAsync(string courseId);
    /// <summary>Opens a lesson; a Locked lesson needs the learner's explicit "open anyway" confirmation.</summary>
    /// <summary>Opens Settings on the AI providers tab.</summary>
    Task ShowAiSettingsAsync();
    Task OpenLessonAsync(string lessonId);
    Task OpenPathAsync(string pathId);
    Task OpenComparisonAsync(string comparisonId);
    /// <summary>Opens the knowledge-check page (per-competency evidence) of a track that defines competencies.</summary>
    Task OpenDiagnosticAsync(string trackId);
}
