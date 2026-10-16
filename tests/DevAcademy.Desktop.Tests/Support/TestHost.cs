using DevAcademy.AI;
using DevAcademy.Application.Services;
using DevAcademy.Desktop.Composition;
using DevAcademy.Desktop.Services;
using DevAcademy.Desktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DevAcademy.Desktop.Tests.Support;

public sealed class FakeDialogs : IDialogService
{
    public bool Answer { get; set; } = true;
    public List<string> Confirmed { get; } = [];
    public List<string> ConfirmedMessages { get; } = [];
    public Task<bool> ConfirmAsync(string title, string message, string confirmText, string? cancelText = null)
    {
        Confirmed.Add(title);
        ConfirmedMessages.Add(message);
        return Task.FromResult(Answer);
    }
    public List<string> Messages { get; } = [];
    public Task ShowMessageAsync(string title, string message) { Messages.Add(message); return Task.CompletedTask; }

    /// <summary>Answers for PromptAsync, consumed in order; null entries (or an empty queue) mean "cancelled".</summary>
    public Queue<string?> PromptAnswers { get; } = new();
    public List<string> Prompts { get; } = [];
    public Task<string?> PromptAsync(string title, string message, string initial, string confirmText)
    {
        Prompts.Add(title);
        return Task.FromResult(PromptAnswers.Count > 0 ? PromptAnswers.Dequeue() : null);
    }

    /// <summary>Answers for ChooseAsync (index of the pressed button), consumed in order; -1 when empty.</summary>
    public Queue<int> ChoiceAnswers { get; } = new();
    public List<(string Title, string Message, IReadOnlyList<string> Options)> Choices { get; } = [];
    public Task<int> ChooseAsync(string title, string message, IReadOnlyList<string> options)
    {
        Choices.Add((title, message, options));
        return Task.FromResult(ChoiceAnswers.Count > 0 ? ChoiceAnswers.Dequeue() : -1);
    }
}

public sealed class FakeFiles : IFilePickerService
{
    public string? SavePath { get; set; }
    public string? OpenPath { get; set; }
    public string? FolderPath { get; set; }
    public Task<string?> PickFolderAsync(string title) => Task.FromResult(FolderPath);
    public Task<string?> PickSaveAsync(string title, string suggestedName, string extension) => Task.FromResult(SavePath);
    public Task<string?> PickOpenAsync(string title, string extension) => Task.FromResult(OpenPath);
}

public sealed class FakeLauncher : ILauncherService
{
    public List<string> Opened { get; } = [];
    public Task<bool> OpenUrlAsync(string url) { Opened.Add(url); return Task.FromResult(true); }
}

public sealed class FakeTheme : IThemeService
{
    public List<ThemeChoice> Applied { get; } = [];
    public void Apply(ThemeChoice choice) => Applied.Add(choice);
}

/// <summary>A full container over a temp data dir and the repository's real content. Dispose deletes the dir.</summary>
public sealed class TestHost : IDisposable
{
    private readonly bool _ownsDir;

    public TestHost(string? dataDir = null, string? contentRoot = null, Action<IServiceCollection>? configure = null)
    {
        _ownsDir = dataDir is null;
        DataDir = dataDir ?? Path.Combine(Path.GetTempPath(), "devacademy-ui-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(DataDir);
        Dialogs = new FakeDialogs();
        Files = new FakeFiles();
        Launcher = new FakeLauncher();
        Theme = new FakeTheme();
        Secrets = new InMemorySecretStore();
        Provider = Bootstrapper.Build(new BootstrapOptions(DataDir, contentRoot ?? ContentRoot), s =>
        {
            s.AddSingleton<IDialogService>(Dialogs);
            s.AddSingleton<IFilePickerService>(Files);
            s.AddSingleton<ILauncherService>(Launcher);
            s.AddSingleton<IThemeService>(Theme);
            s.AddSingleton<ISecretStore>(Secrets); // never the real OS keychain in tests
            // Never start real language servers from unrelated tests; LSP tests register their own service over this one.
            s.AddSingleton(sp =>
            {
                var settings = sp.GetRequiredService<DevAcademy.Application.Ports.ISettingsRepository>();
                settings.SetAsync(LanguageIntelligenceService.EnabledKey, "false").GetAwaiter().GetResult();
                return new LanguageIntelligenceService(sp.GetRequiredService<DevAcademy.Infrastructure.AppPaths>(),
                    sp.GetRequiredService<DevAcademy.Execution.ExecutionOptions>(), settings);
            });
            configure?.Invoke(s);
        });
    }

    public static string ContentRoot { get; } = FindContent();

    /// <summary>Algorithm fixture (python + javascript variants, visualize block), linked from Content.Tests into the output dir.</summary>
    public static string AlgorithmsRoot { get; } = System.IO.Path.Combine(AppContext.BaseDirectory, "Fixtures", "algorithms");

    /// <summary>Small stable fixture (one available + one planned language, no algorithms) for tests that assert on catalog shape rather than on the real course.</summary>
    /// <summary>Paths fixture: stages, an alternatives group, a placement quiz (course py-adv), framework metadata (web-django) and a comparison.</summary>
    public static string PathsRoot { get; } = System.IO.Path.Combine(AppContext.BaseDirectory, "Fixtures", "paths");

    public static string ValidFixtureRoot { get; } = System.IO.Path.Combine(AppContext.BaseDirectory, "Fixtures", "valid");

    public string DataDir { get; }
    public ServiceProvider Provider { get; }
    public FakeDialogs Dialogs { get; }
    public FakeFiles Files { get; }
    public FakeLauncher Launcher { get; }
    public FakeTheme Theme { get; }
    public InMemorySecretStore Secrets { get; }
    public AppServices Services => Provider.GetRequiredService<AppServices>();
    public MainViewModel Main => Provider.GetRequiredService<MainViewModel>();
    public LearningPathService LearningPath => Provider.GetRequiredService<LearningPathService>();

    private static string FindContent()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
        {
            var c = System.IO.Path.Combine(d.FullName, "content");
            if (File.Exists(System.IO.Path.Combine(c, "catalog.yaml")) && Directory.Exists(System.IO.Path.Combine(d.FullName, "src"))) return c;
        }
        throw new DirectoryNotFoundException("repository content/ not found above " + AppContext.BaseDirectory);
    }

    public void Dispose()
    {
        Provider.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (_ownsDir) try { Directory.Delete(DataDir, true); } catch (IOException) { }
    }
}

/// <summary>Runner whose runtimes are configurable; RunOnce echoes the language so tests can see what was requested.</summary>
public sealed class FakeRunner(params string[] missing) : DevAcademy.Execution.ICodeRunner
{
    public List<string> RunLanguages { get; } = [];

    public Task<DevAcademy.Execution.IPreparedProgram> PrepareAsync(DevAcademy.Execution.ExecutionRequest request, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task<DevAcademy.Execution.ExecutionResult> RunOnceAsync(DevAcademy.Execution.ExecutionRequest request, string? stdin,
        DevAcademy.Execution.ResourceLimits limits, CancellationToken ct)
    {
        RunLanguages.Add(request.LanguageId);
        return Task.FromResult(new DevAcademy.Execution.ExecutionResult(DevAcademy.Execution.ExecutionStatus.Success, 0,
            "ran " + request.LanguageId, "", "", TimeSpan.FromMilliseconds(5), null, DevAcademy.Execution.IsolationLevel.ProcessLimits));
    }

    public Task<IReadOnlyList<DevAcademy.Execution.RuntimeStatus>> DetectRuntimesAsync(CancellationToken ct)
    {
        IReadOnlyList<DevAcademy.Execution.RuntimeStatus> all = new[] { "python", "javascript" }.Select(l =>
            missing.Contains(l)
                ? new DevAcademy.Execution.RuntimeStatus(l, DevAcademy.Execution.RuntimeAvailability.Missing, null, null, null, "Install " + l)
                : new DevAcademy.Execution.RuntimeStatus(l, DevAcademy.Execution.RuntimeAvailability.Available, "1", "/bin/" + l, null, null)).ToList();
        return Task.FromResult(all);
    }
}
