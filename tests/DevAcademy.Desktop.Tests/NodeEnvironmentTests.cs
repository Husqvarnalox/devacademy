using DevAcademy.Application.Evaluation;
using DevAcademy.Application.Node;
using DevAcademy.Desktop.Tests.Support;
using DevAcademy.Desktop.ViewModels;
using DevAcademy.Execution;
using Microsoft.Extensions.DependencyInjection;

namespace DevAcademy.Desktop.Tests;

internal sealed class NodeToolRunner : IToolRunner
{
    public List<string> Streaming { get; } = [];
    public string? Find(string name) => name switch { "node" => "/fake/node", "npm" => "/fake/npm", _ => null };

    public Task<ToolOutput> RunAsync(string fileName, IReadOnlyList<string> args, ToolRunOptions? options, CancellationToken ct) =>
        Task.FromResult(new ToolOutput(0, "v22.15.0\n", ""));

    public Task<ToolOutput> RunStreamingAsync(string fileName, IReadOnlyList<string> args, ToolRunOptions? options, Action<string> onLine, CancellationToken ct)
    {
        Streaming.Add(fileName + " " + string.Join(' ', args));
        onLine("added 95 packages");
        return Task.FromResult(new ToolOutput(0, "", ""));
    }
}

[Collection(DevAcademy.Desktop.Tests.Support.BindingCaptureCollection.Name)]
public class NodeEnvironmentViewModelTests
{
    private static (TestHost host, NodeToolRunner tools, string env) Setup()
    {
        var tools = new NodeToolRunner();
        var dir = Path.Combine(Path.GetTempPath(), "devacademy-ui-" + Guid.NewGuid().ToString("N"));
        var env = Path.Combine(dir, "my-node-env"); // deliberately absent: nothing installed
        var manifest = Path.Combine(dir, "manifest");
        Directory.CreateDirectory(manifest);
        File.WriteAllText(Path.Combine(manifest, "package.json"), """{ "name": "c", "dependencies": { "react": "19.3.1" } }""");
        File.WriteAllText(Path.Combine(manifest, "package-lock.json"), "{}");
        var host = new TestHost(dir, configure: s =>
        {
            s.AddSingleton<IToolRunner>(tools);
            s.AddSingleton(new NodeEnvironmentService(tools, env, manifest));
        });
        return (host, tools, env);
    }

    [Fact]
    public async Task Missing_environment_is_reported_and_nothing_is_installed_on_load()
    {
        var (host, tools, _) = Setup();
        using (host)
        {
            var vm = new NodeEnvironmentViewModel(host.Services);
            await vm.LoadAsync();
            Assert.Contains("не установлены", vm.StatusText);
            Assert.Empty(tools.Streaming);
        }
    }

    [Fact]
    public async Task Install_confirms_with_the_exact_command_and_the_network_notice_then_runs_npm_ci()
    {
        var (host, tools, env) = Setup();
        using (host)
        {
            var vm = new NodeEnvironmentViewModel(host.Services);
            host.Dialogs.Answer = true;
            await vm.InstallCommand.ExecuteAsync(null);

            var msg = Assert.Single(host.Dialogs.ConfirmedMessages);
            Assert.Contains("npm ci --ignore-scripts", msg);
            Assert.Contains("интернета", msg);
            Assert.Equal("/fake/npm ci --ignore-scripts --no-audit --no-fund", Assert.Single(tools.Streaming));
            Assert.Contains("added 95 packages", vm.InstallLog);
            Assert.True(File.Exists(Path.Combine(env, "package-lock.json")));
        }
    }

    [Fact]
    public async Task Declining_the_confirmation_runs_nothing()
    {
        var (host, tools, env) = Setup();
        using (host)
        {
            var vm = new NodeEnvironmentViewModel(host.Services);
            host.Dialogs.Answer = false;
            await vm.InstallCommand.ExecuteAsync(null);
            Assert.Single(host.Dialogs.Confirmed);
            Assert.Empty(tools.Streaming);
            Assert.False(Directory.Exists(env));
            Assert.False(vm.IsInstalling);
        }
    }

    [Fact]
    public async Task An_exercise_that_needs_npm_packages_is_blocked_with_a_message_pointing_to_settings()
    {
        var (host, _, _) = Setup();
        using (host)
        {
            await host.Main.OpenLessonAsync("react.fundamentals.what-is-react");
            var ws = Assert.IsType<LessonWorkspaceViewModel>(host.Main.Current);
            var session = Assert.IsType<ExerciseSessionViewModel>(ws.Practice);
            var plan = await host.Services.Node.ResolveAsync(session.Exercise!, CancellationToken.None);
            Assert.NotNull(plan.BlockedMessage);
            Assert.Contains("Среды выполнения", plan.BlockedMessage);
        }
    }
}
