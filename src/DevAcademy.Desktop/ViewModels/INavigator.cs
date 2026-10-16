namespace DevAcademy.Desktop.ViewModels;

public interface INavigator
{
    Task ShowCatalogAsync();
    Task OpenCourseAsync(string courseId);
    /// <summary>Opens a lesson; a Locked lesson needs the learner's explicit "open anyway" confirmation.</summary>
    /// <summary>Opens Settings on the AI providers tab.</summary>
    Task ShowAiSettingsAsync();
    Task OpenLessonAsync(string lessonId);
    Task OpenPathAsync(string pathId);
    Task OpenComparisonAsync(string comparisonId);
}
