using DevAcademy.Application.Evaluation;
using DevAcademy.Application.Node;
using DevAcademy.Domain;
using DevAcademy.Desktop.Tests.Support;
using DevAcademy.Desktop.ViewModels;
using DevAcademy.Execution;
using Microsoft.Extensions.DependencyInjection;

namespace DevAcademy.Desktop.Tests;

internal sealed class NodeToolRunner : IToolRunner
{
    public List<string> Streaming { get; } = [];
    public List<string?> WorkDirs { get; } = [];
    public string? Find(string name) => name switch { "node" => "/fake/node", "npm" => "/fake/npm", _ => null };

    public Task<ToolOutput> RunAsync(string fileName, IReadOnlyList<string> args, ToolRunOptions? options, CancellationToken ct) =>
        Task.FromResult(new ToolOutput(0, "v22.15.0\n", ""));

    public Task<ToolOutput> RunStreamingAsync(string fileName, IReadOnlyList<string> args, ToolRunOptions? options, Action<string> onLine, CancellationToken ct)
    {
        Streaming.Add(fileName + " " + string.Join(' ', args));
        WorkDirs.Add(options?.WorkingDirectory);
        onLine("added 95 packages");
        // Like npm ci: materialize node_modules/<dep> for every dependency of the copied package.json.
        if (options?.WorkingDirectory is { } dir && File.Exists(Path.Combine(dir, "package.json")))
        {
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "package.json")));
            foreach (var dep in new[] { "dependencies", "devDependencies" }.Where(k => doc.RootElement.TryGetProperty(k, out _)).SelectMany(k => doc.RootElement.GetProperty(k).EnumerateObject()))
            {
                var d = Path.Combine(dir, "node_modules", dep.Name);
                Directory.CreateDirectory(d);
                File.WriteAllText(Path.Combine(d, "package.json"), $$"""{ "name": "{{dep.Name}}", "version": "{{dep.Value.GetString()!.TrimStart('^', '~')}}" }""");
            }
        }
        return Task.FromResult(new ToolOutput(0, "", ""));
    }
}

[Collection(DevAcademy.Desktop.Tests.Support.BindingCaptureCollection.Name)]
public class NodeEnvironmentViewModelTests
{
    private static (TestHost host, NodeToolRunner tools, string root) Setup()
    {
        var packages = ContentPackageIndex.TryLoadDefault() ?? throw new InvalidOperationException("tools/content-packages not found");
        var tools = new NodeToolRunner();
        var dir = Path.Combine(Path.GetTempPath(), "devacademy-ui-" + Guid.NewGuid().ToString("N"));
        var root = Path.Combine(dir, "node-content"); // deliberately absent: nothing installed
        var host = new TestHost(dir, configure: s =>
        {
            s.AddSingleton<IToolRunner>(tools);
            s.AddSingleton(packages);
            s.AddSingleton(new NodeEnvironmentService(tools, root, packages));
        });
        return (host, tools, root);
    }

    private static NodeSetViewModel SetVm(NodeEnvironmentViewModel vm, string id) => vm.Sets.Single(x => x.Set.Id == id);

    [Fact]
    public async Task Settings_list_every_set_as_not_installed_and_nothing_is_installed_on_load()
    {
        var (host, tools, _) = Setup();
        using (host)
        {
            var vm = new NodeEnvironmentViewModel(host.Services);
            await vm.LoadAsync();
            Assert.Equal(4, vm.Sets.Count);
            Assert.All(vm.Sets, x => Assert.False(x.IsInstalled));
            Assert.Empty(tools.Streaming);
        }
    }

    [Fact]
    public async Task Installing_a_set_confirms_with_packages_command_and_network_notice_then_runs_npm_ci_in_that_set_only()
    {
        var (host, tools, root) = Setup();
        using (host)
        {
            var vm = new NodeEnvironmentViewModel(host.Services);
            await vm.LoadAsync();
            host.Dialogs.Answer = true;
            await SetVm(vm, "typescript").InstallCommand.ExecuteAsync(null);

            var msg = Assert.Single(host.Dialogs.ConfirmedMessages);
            Assert.Contains("npm ci --ignore-scripts", msg);
            Assert.Contains("typescript", msg);
            Assert.Contains("сеть", msg);
            Assert.Equal("/fake/npm ci --ignore-scripts --no-audit --no-fund", Assert.Single(tools.Streaming));
            Assert.Equal(Path.Combine(root, "typescript"), Assert.Single(tools.WorkDirs));
            Assert.Contains("added 95 packages", vm.InstallLog);
            Assert.True(SetVm(vm, "typescript").IsInstalled);
            Assert.False(SetVm(vm, "react").IsInstalled);
            Assert.False(Directory.Exists(Path.Combine(root, "react")));
        }
    }

    [Fact]
    public async Task Declining_the_install_confirmation_runs_nothing()
    {
        var (host, tools, root) = Setup();
        using (host)
        {
            var vm = new NodeEnvironmentViewModel(host.Services);
            await vm.LoadAsync();
            host.Dialogs.Answer = false;
            await SetVm(vm, "react").InstallCommand.ExecuteAsync(null);
            Assert.Single(host.Dialogs.Confirmed);
            Assert.Empty(tools.Streaming);
            Assert.False(Directory.Exists(root));
            Assert.False(vm.IsInstalling);
        }
    }

    [Fact]
    public async Task Remove_deletes_only_that_set_after_confirmation_and_declining_keeps_it()
    {
        var (host, _, root) = Setup();
        using (host)
        {
            var vm = new NodeEnvironmentViewModel(host.Services);
            await vm.LoadAsync();
            host.Dialogs.Answer = true;
            await SetVm(vm, "react").InstallCommand.ExecuteAsync(null);
            await SetVm(vm, "typescript").InstallCommand.ExecuteAsync(null);

            host.Dialogs.Answer = false;
            await SetVm(vm, "react").RemoveCommand.ExecuteAsync(null);
            Assert.True(Directory.Exists(Path.Combine(root, "react")));

            host.Dialogs.Answer = true;
            await SetVm(vm, "react").RemoveCommand.ExecuteAsync(null);
            Assert.False(Directory.Exists(Path.Combine(root, "react")));
            Assert.True(Directory.Exists(Path.Combine(root, "typescript")));
            Assert.False(SetVm(vm, "react").IsInstalled);
        }
    }

    [Fact]
    public async Task A_blocked_lesson_offers_the_set_install_button_and_declining_runs_nothing()
    {
        var (host, tools, _) = Setup();
        using (host)
        {
            await host.Main.OpenLessonAsync("react.fundamentals.what-is-react");
            var ws = Assert.IsType<LessonWorkspaceViewModel>(host.Main.Current);
            var session = Assert.IsType<ExerciseSessionViewModel>(ws.Practice);
            Assert.True(session.HasInstallSet);
            Assert.Equal("react", session.InstallSet!.Id);
            Assert.Contains("React", session.InstallSetText);
            Assert.NotNull(session.EnvironmentNotice);

            host.Dialogs.Answer = false;
            await session.InstallSetCommand.ExecuteAsync(null);
            Assert.Single(host.Dialogs.Confirmed);
            Assert.Empty(tools.Streaming);
            Assert.True(session.HasInstallSet);
        }
    }

    [Fact]
    public async Task Accepting_the_lesson_install_runs_npm_ci_and_clears_the_block()
    {
        var (host, tools, root) = Setup();
        using (host)
        {
            await host.Main.OpenLessonAsync("react.fundamentals.what-is-react");
            var ws = Assert.IsType<LessonWorkspaceViewModel>(host.Main.Current);
            var session = Assert.IsType<ExerciseSessionViewModel>(ws.Practice);
            host.Dialogs.Answer = true;
            await session.InstallSetCommand.ExecuteAsync(null);

            Assert.Equal(Path.Combine(root, "react"), Assert.Single(tools.WorkDirs));
            Assert.False(session.HasInstallSet);
            Assert.Null(session.EnvironmentNotice);
        }
    }
}
