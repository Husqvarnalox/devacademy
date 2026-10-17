using DevAcademy.Application.Go;
using DevAcademy.Domain;
using DevAcademy.Desktop.Tests.Support;
using DevAcademy.Desktop.ViewModels;
using DevAcademy.Execution;
using Microsoft.Extensions.DependencyInjection;

namespace DevAcademy.Desktop.Tests;

internal sealed class GoToolRunner : IToolRunner
{
    public List<string> Streaming { get; } = [];
    public List<string?> WorkDirs { get; } = [];
    public string? Find(string name) => name == "go" ? "/fake/go" : null;

    public Task<ToolOutput> RunAsync(string fileName, IReadOnlyList<string> args, ToolRunOptions? options, CancellationToken ct) =>
        Task.FromResult(new ToolOutput(0, "go version go1.26.5 darwin/arm64\n", ""));

    public Task<ToolOutput> RunStreamingAsync(string fileName, IReadOnlyList<string> args, ToolRunOptions? options, Action<string> onLine, CancellationToken ct)
    {
        Streaming.Add(fileName + " " + string.Join(' ', args));
        WorkDirs.Add(options?.WorkingDirectory);
        onLine("go: downloading github.com/gomlx/gomlx v0.28.8");
        // Like go mod download: the pinned zip of every top-level module lands in GOMODCACHE.
        if (options?.Environment is { } env && env.TryGetValue("GOMODCACHE", out var cache))
            foreach (var (module, version) in new[] { ("github.com/gomlx/gomlx", "v0.28.8"), ("github.com/gomlx/compute", "v0.1.6") })
            {
                var dir = Path.Combine(cache, "cache", "download", module.Replace('/', Path.DirectorySeparatorChar), "@v");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, version + ".zip"), "zip");
            }
        return Task.FromResult(new ToolOutput(0, "", ""));
    }
}

[Collection(DevAcademy.Desktop.Tests.Support.BindingCaptureCollection.Name)]
public class GoEnvironmentViewModelTests
{
    private static (TestHost host, GoToolRunner tools, string root) Setup()
    {
        var packages = ContentPackageIndex.TryLoadDefault() ?? throw new InvalidOperationException("tools/content-packages not found");
        var tools = new GoToolRunner();
        var dir = Path.Combine(Path.GetTempPath(), "devacademy-ui-go-" + Guid.NewGuid().ToString("N"));
        var root = Path.Combine(dir, "go-content"); // deliberately absent: nothing installed
        var host = new TestHost(dir, configure: s =>
        {
            s.AddSingleton<IToolRunner>(tools);
            s.AddSingleton(packages);
            s.AddSingleton(new GoEnvironmentService(tools, root, packages));
        });
        return (host, tools, root);
    }

    [Fact]
    public async Task Settings_list_the_go_set_as_not_installed_and_nothing_is_downloaded_on_load()
    {
        var (host, tools, _) = Setup();
        using (host)
        {
            var vm = new GoEnvironmentViewModel(host.Services);
            await vm.LoadAsync();
            var set = Assert.Single(vm.Sets, x => x.Set.Id == "gomlx");
            Assert.False(set.IsInstalled);
            Assert.Empty(tools.Streaming);
        }
    }

    [Fact]
    public async Task Installing_confirms_with_modules_command_and_network_notice_then_runs_go_mod_download_for_that_set_only()
    {
        var (host, tools, root) = Setup();
        using (host)
        {
            var vm = new GoEnvironmentViewModel(host.Services);
            await vm.LoadAsync();
            host.Dialogs.Answer = true;
            await vm.Sets.Single(x => x.Set.Id == "gomlx").InstallCommand.ExecuteAsync(null);

            var msg = Assert.Single(host.Dialogs.ConfirmedMessages);
            Assert.Contains("go mod download", msg);
            Assert.Contains("github.com/gomlx/gomlx", msg);
            Assert.Contains("сеть", msg);
            Assert.Equal("/fake/go mod download", Assert.Single(tools.Streaming));
            Assert.Equal(Path.Combine(root, "gomlx"), Assert.Single(tools.WorkDirs));
            Assert.True(vm.Sets.Single(x => x.Set.Id == "gomlx").IsInstalled);
        }
    }

    [Fact]
    public async Task Declining_the_install_confirmation_runs_nothing()
    {
        var (host, tools, root) = Setup();
        using (host)
        {
            var vm = new GoEnvironmentViewModel(host.Services);
            await vm.LoadAsync();
            host.Dialogs.Answer = false;
            await vm.Sets.Single(x => x.Set.Id == "gomlx").InstallCommand.ExecuteAsync(null);
            Assert.Single(host.Dialogs.Confirmed);
            Assert.Empty(tools.Streaming);
            Assert.False(Directory.Exists(root));
            Assert.False(vm.IsInstalling);
        }
    }

    [Fact]
    public async Task Remove_deletes_the_set_only_after_confirmation()
    {
        var (host, _, root) = Setup();
        using (host)
        {
            var vm = new GoEnvironmentViewModel(host.Services);
            await vm.LoadAsync();
            host.Dialogs.Answer = true;
            await vm.Sets.Single(x => x.Set.Id == "gomlx").InstallCommand.ExecuteAsync(null);
            host.Dialogs.Answer = false;
            await vm.Sets.Single(x => x.Set.Id == "gomlx").RemoveCommand.ExecuteAsync(null);
            Assert.True(Directory.Exists(Path.Combine(root, "gomlx")));
            host.Dialogs.Answer = true;
            await vm.Sets.Single(x => x.Set.Id == "gomlx").RemoveCommand.ExecuteAsync(null);
            Assert.False(Directory.Exists(Path.Combine(root, "gomlx")));
        }
    }

    [Fact]
    public async Task A_blocked_lesson_offers_the_set_install_button_and_declining_runs_nothing()
    {
        var (host, tools, _) = Setup();
        using (host)
        {
            await host.Main.OpenLessonAsync("go.ml.gomlx.tensors-and-backend");
            var ws = Assert.IsType<LessonWorkspaceViewModel>(host.Main.Current);
            var session = Assert.IsType<ExerciseSessionViewModel>(ws.Practice);
            Assert.True(session.HasInstallSet);
            Assert.Equal("gomlx", session.InstallSet!.Id);
            Assert.Contains("GoMLX", session.InstallSetText);
            Assert.NotNull(session.EnvironmentNotice);

            var before = host.Dialogs.Confirmed.Count; // opening a lesson whose prerequisites are open asks to unlock it first
            host.Dialogs.Answer = false;
            await session.InstallSetCommand.ExecuteAsync(null);
            Assert.Equal(before + 1, host.Dialogs.Confirmed.Count);
            Assert.Empty(tools.Streaming);
            Assert.True(session.HasInstallSet);
        }
    }

    [Fact]
    public async Task Accepting_the_lesson_install_runs_go_mod_download_for_the_set_and_clears_the_block()
    {
        var (host, tools, root) = Setup();
        using (host)
        {
            await host.Main.OpenLessonAsync("go.ml.gomlx.tensors-and-backend");
            var ws = Assert.IsType<LessonWorkspaceViewModel>(host.Main.Current);
            var session = Assert.IsType<ExerciseSessionViewModel>(ws.Practice);
            host.Dialogs.Answer = true;
            await session.InstallSetCommand.ExecuteAsync(null);

            Assert.Equal("/fake/go mod download", Assert.Single(tools.Streaming));
            Assert.Equal(Path.Combine(root, "gomlx"), Assert.Single(tools.WorkDirs));
            Assert.False(session.HasInstallSet);
            Assert.Null(session.EnvironmentNotice);
        }
    }
}
