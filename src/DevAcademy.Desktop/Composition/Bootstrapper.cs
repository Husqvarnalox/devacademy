using DevAcademy.AI;
using DevAcademy.AI.Http;
using DevAcademy.Application.Evaluation;
using DevAcademy.Application.Ports;
using DevAcademy.Application.Python;
using DevAcademy.Application.Workspace;
using DevAcademy.Infrastructure.Workspace;
using DevAcademy.Terminal;
using DevAcademy.Application.Services;
using DevAcademy.Application.Tutor;
using DevAcademy.Desktop.Services;
using DevAcademy.Desktop.ViewModels;
using DevAcademy.Domain;
using DevAcademy.Execution;
using DevAcademy.Infrastructure;
using DevAcademy.Infrastructure.Secrets;
using Microsoft.Extensions.DependencyInjection;

namespace DevAcademy.Desktop.Composition;

/// <param name="DataDir">Overrides AppPaths (otherwise DEVACADEMY_DATA_DIR or the per-OS default).</param>
/// <param name="ContentDir">Overrides content discovery (otherwise DEVACADEMY_CONTENT_DIR or FindContentRoot).</param>
public sealed record BootstrapOptions(string? DataDir = null, string? ContentDir = null);

/// <summary>The single composition root of the desktop application.</summary>
public static class Bootstrapper
{
    public const string ContentDirVariable = "DEVACADEMY_CONTENT_DIR";

    public static ServiceProvider Build(BootstrapOptions? options = null, Action<IServiceCollection>? configure = null)
    {
        options ??= new BootstrapOptions();
        var services = new ServiceCollection();

        var paths = new AppPaths(options.DataDir);
        var database = new SqliteDatabase(paths.DatabasePath);
        database.Migrate();

        services.AddSingleton(paths);
        services.AddSingleton(database);
        services.AddSingleton<ISettingsRepository, SqliteSettingsRepository>();
        services.AddSingleton<IProgressRepository, SqliteProgressRepository>();
        services.AddSingleton<ISubmissionRepository, SqliteSubmissionRepository>();
        services.AddSingleton<IDraftRepository, SqliteDraftRepository>();
        services.AddSingleton<BackupService>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IContentRepository>(_ => CreateContent(options.ContentDir));

        services.AddSingleton<ExecutionOptions>(_ => new ExecutionOptions());
        services.AddSingleton<ICodeRunner>(sp => ExecutionEngine.CreateDefault(sp.GetRequiredService<ExecutionOptions>()));

        // Python environments and ML runtimes. Nothing here runs a process until the user acts (or an exercise that declares requirements is opened).
        services.AddSingleton<IToolRunner, SystemToolRunner>();
        services.AddSingleton(sp => new PythonEnvironmentManager(sp.GetRequiredService<AppPaths>().PythonEnvsDir, sp.GetRequiredService<IToolRunner>()));
        services.AddSingleton(sp => new PythonInterpreterDetector(sp.GetRequiredService<IToolRunner>()));
        services.AddSingleton(sp => new MlRuntimeManager(sp.GetRequiredService<IToolRunner>()));
        services.AddSingleton<PythonEnvironmentService>();
        services.AddSingleton<IExerciseEnvironmentResolver>(sp => sp.GetRequiredService<PythonEnvironmentService>());

        services.AddSingleton<CatalogService>();
        services.AddSingleton<LearningPathService>();
        services.AddSingleton(sp => new ExerciseEvaluationService(sp.GetRequiredService<ICodeRunner>(), sp.GetRequiredService<IExerciseEnvironmentResolver>()));
        services.AddSingleton(sp => new RunService(sp.GetRequiredService<ICodeRunner>(), sp.GetRequiredService<IExerciseEnvironmentResolver>()));
        services.AddSingleton<ProgressService>();
        services.AddSingleton<HintService>();
        services.AddSingleton<ProblemService>();
        services.AddSingleton<DraftService>();

        // AI layer (optional, off by default). Keys live in the OS secret store only; nothing is sent before the learner confirms.
        services.AddSingleton<IAiRequestRepository, SqliteAiRequestRepository>();
        services.AddSingleton<ISecretStore>(_ => SecretStoreFactory.CreateForCurrentOs());
        services.AddSingleton(new AiHttp(HttpModelProvider.CreateDefaultHttpClient()));
        services.AddSingleton(sp => CreateRegistry(sp.GetRequiredService<AiHttp>(), sp.GetRequiredService<ISettingsRepository>()));
        services.AddSingleton<IModelCacheStore, SettingsModelCacheStore>();
        services.AddSingleton<ModelCatalog>();
        services.AddSingleton<TutorSettingsService>();
        services.AddSingleton<TutorContextBuilder>();
        services.AddSingleton<TutorService>();
        services.AddSingleton<AiConnectionService>();

        // Project workspace. The terminal is a real shell in the user's account (no sandbox); only paths are stored in SQLite.
        services.AddSingleton<IRecentProjectsRepository, SqliteRecentProjectsRepository>();
        services.AddSingleton<IProjectSessionStore, SqliteProjectSessionStore>();
        services.AddSingleton<ITerminalFactory, PtyTerminalFactory>();
        services.AddSingleton<IFileTrash>(_ => FileTrash.CreateForCurrentOs());
        services.AddSingleton<ProjectFileOps>();
        services.AddSingleton<WorkspaceServices>();
        services.AddSingleton<LanguageIntelligenceService>();

        services.AddSingleton<WindowHolder>();
        services.AddSingleton<IDialogService, AvaloniaDialogService>();
        services.AddSingleton<IFilePickerService, AvaloniaFilePickerService>();
        services.AddSingleton<ILauncherService, AvaloniaLauncherService>();
        services.AddSingleton<IThemeService, AvaloniaThemeService>();

        services.AddSingleton<AppServices>();
        services.AddSingleton<MainViewModel>();

        configure?.Invoke(services);
        return services.BuildServiceProvider();
    }

    private static ProviderRegistry CreateRegistry(AiHttp http, ISettingsRepository settings)
    {
        var registry = ProviderRegistry.CreateDefault(http.Client);
        var url = settings.GetAsync(TutorSettingsKeys.OpenAiCompatibleBaseUrl).GetAwaiter().GetResult();
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
            registry.AddOpenAiCompatible(http.Client, uri);
        return registry;
    }

    private static IContentRepository CreateContent(string? explicitDir)
    {
        var env = Environment.GetEnvironmentVariable(ContentDirVariable);
        var dir = explicitDir ?? (string.IsNullOrWhiteSpace(env) ? FileContentRepository.FindContentRoot() : env);
        return dir is not null && File.Exists(Path.Combine(dir, "catalog.yaml"))
            ? new FileContentRepository(dir)
            : new MissingContentRepository();
    }
}

/// <summary>HttpClient shared by AI providers (no auto-redirects, so keys cannot follow a redirect).</summary>
public sealed record AiHttp(HttpClient Client);

/// <summary>Used when no content directory exists: the app starts and explains the problem instead of crashing.</summary>
public sealed class MissingContentRepository : IContentRepository
{
    public ContentSet Current => ContentSet.Empty;
    public IReadOnlyList<ContentDiagnostic> Diagnostics { get; } =
        [new ContentDiagnostic(DiagnosticSeverity.Error, "content", "content/catalog.yaml not found")];
    public void Reload() { }
}
