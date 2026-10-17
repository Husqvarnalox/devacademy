using DevAcademy.AI;
using DevAcademy.Application.Evaluation;
using DevAcademy.Application.Node;
using DevAcademy.Application.Ports;
using DevAcademy.Application.Python;
using DevAcademy.Application.Services;
using DevAcademy.Application.Tutor;
using DevAcademy.Desktop.Services;
using DevAcademy.Domain;
using DevAcademy.Execution;
using DevAcademy.Infrastructure;

namespace DevAcademy.Desktop.Composition;

/// <summary>Everything view models need, resolved once by the composition root.</summary>
public sealed class AppServices(
    AppPaths paths,
    IContentRepository content,
    CatalogService catalog,
    LearningPathService path,
    ExerciseEvaluationService evaluation,
    RunService run,
    ProgressService progress,
    HintService hints,
    ProblemService problems,
    DraftService draftService,
    ISettingsRepository settings,
    IDraftRepository drafts,
    BackupService backup,
    ICodeRunner runner,
    ExecutionOptions executionOptions,
    IDialogService dialogs,
    IFilePickerService files,
    ILauncherService launcher,
    IThemeService theme,
    WindowHolder holder,
    ProviderRegistry providers,
    AiHttp aiHttp,
    AiConnectionService ai,
    TutorSettingsService tutorSettings,
    TutorService tutor,
    PythonEnvironmentService python,
    WorkspaceServices workspace,
    LanguageIntelligenceService language,
    DebugService debug,
    NodeEnvironmentService node,
    ContentPackageIndex packageSets)
{
    public AppPaths Paths { get; } = paths;
    public IContentRepository Content { get; } = content;
    public CatalogService Catalog { get; } = catalog;
    public LearningPathService Path { get; } = path;
    public ExerciseEvaluationService Evaluation { get; } = evaluation;
    public RunService Run { get; } = run;
    public ProgressService Progress { get; } = progress;
    public HintService Hints { get; } = hints;
    public ProblemService Problems { get; } = problems;
    public DraftService DraftFiles { get; } = draftService;
    public ISettingsRepository Settings { get; } = settings;
    public IDraftRepository Drafts { get; } = drafts;
    public BackupService Backup { get; } = backup;
    public ICodeRunner Runner { get; } = runner;
    public ExecutionOptions ExecutionOptions { get; } = executionOptions;
    public IDialogService Dialogs { get; } = dialogs;
    public IFilePickerService Files { get; } = files;
    public ILauncherService Launcher { get; } = launcher;
    public IThemeService Theme { get; } = theme;
    public WindowHolder Holder { get; } = holder;
    public ProviderRegistry Providers { get; } = providers;
    public AiHttp AiHttp { get; } = aiHttp;
    public AiConnectionService Ai { get; } = ai;
    public TutorSettingsService TutorSettings { get; } = tutorSettings;
    public TutorService Tutor { get; } = tutor;
    public PythonEnvironmentService Python { get; } = python;
    public WorkspaceServices Workspace { get; } = workspace;
    public LanguageIntelligenceService Language { get; } = language;
    public DebugService Debug { get; } = debug;
    public NodeEnvironmentService Node { get; } = node;
    public ContentPackageIndex PackageSets { get; } = packageSets;

    /// <summary>Delay between the last keystroke and the draft being written.</summary>
    public TimeSpan DraftDelay { get; init; } = TimeSpan.FromMilliseconds(800);
}
