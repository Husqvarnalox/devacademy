using DevAcademy.Application.Python;
using DevAcademy.Domain;
using DevAcademy.Execution;

namespace DevAcademy.Application.Tests;

public sealed class PythonSetInstallTests : IDisposable
{
    private sealed class UvOnly : IToolRunner
    {
        public string? Find(string name) => name == "uv" ? "/fake/uv" : null;
        public Task<ToolOutput> RunAsync(string fileName, IReadOnlyList<string> args, ToolRunOptions? options, CancellationToken ct) =>
            Task.FromResult(new ToolOutput(1, "", "unexpected"));
        public Task<ToolOutput> RunStreamingAsync(string fileName, IReadOnlyList<string> args, ToolRunOptions? options, Action<string> onLine, CancellationToken ct) =>
            RunAsync(fileName, args, options, ct);
    }

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "da-pyset-" + Guid.NewGuid().ToString("N"));
    private readonly InMemorySettings _settings = new();

    private (PythonEnvironmentService svc, ContentPackageSet set) Create()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "pkgs", "python"));
        File.WriteAllText(Path.Combine(_dir, "pkgs", "index.json"),
            """[{"id":"numpy","title":"NumPy","language":"python","description":"d","packages":["numpy"]}]""");
        File.WriteAllText(Path.Combine(_dir, "pkgs", "python", "numpy.txt"), "numpy==2.5.4\n");
        Assert.True(ContentPackageIndex.TryLoad(Path.Combine(_dir, "pkgs"), out var index, out var loadError), loadError);
        var runner = new UvOnly();
        var svc = new PythonEnvironmentService(new PythonEnvironmentManager(Path.Combine(_dir, "envs"), runner, isWindows: false),
            new PythonInterpreterDetector(runner), new MlRuntimeManager(runner), _settings, index);
        return (svc, index.Sets[0]);
    }

    private void FakeEnv(string name)
    {
        var env = Path.Combine(_dir, "envs", name);
        Directory.CreateDirectory(Path.Combine(env, "bin"));
        File.WriteAllText(Path.Combine(env, "pyvenv.cfg"), "home = /usr/bin\nuv = 0.5\nversion_info = 3.13.0\n");
        File.WriteAllText(Path.Combine(env, "bin", "python"), "");
    }

    [Fact]
    public async Task Without_an_active_environment_the_plan_asks_to_create_one_and_never_creates_it()
    {
        var (svc, set) = Create();
        var (plan, _, error) = await svc.PlanSetInstallAsync(set, CancellationToken.None);
        Assert.Null(plan);
        Assert.Contains("создайте окружение", error);
        Assert.Empty(svc.ListEnvironments());
    }

    [Fact]
    public async Task With_an_active_environment_the_plan_installs_the_sets_pinned_file_into_it()
    {
        var (svc, set) = Create();
        FakeEnv("lab");
        await svc.SetActiveAsync("lab", CancellationToken.None);
        var (plan, _, error) = await svc.PlanSetInstallAsync(set, CancellationToken.None);
        Assert.Null(error);
        Assert.True(plan!.Downloads);
        Assert.Equal(["numpy"], plan.PackageNames);
        Assert.Equal(Path.Combine(_dir, "envs", "lab"), plan.EnvironmentPath);
        Assert.Contains("-r " + Path.Combine(_dir, "pkgs", "python", "numpy.txt"), plan.CommandLine);
    }

    public void Dispose() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }
}
