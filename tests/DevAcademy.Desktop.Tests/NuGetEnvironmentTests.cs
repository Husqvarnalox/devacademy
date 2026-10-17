using DevAcademy.Application.NuGet;
using DevAcademy.Desktop.Tests.Support;
using DevAcademy.Desktop.ViewModels;
using DevAcademy.Domain;
using DevAcademy.Execution;
using Microsoft.Extensions.DependencyInjection;

namespace DevAcademy.Desktop.Tests;

internal sealed class NuGetToolRunner : IToolRunner
{
    public List<string> Streaming { get; } = [];
    public List<string?> WorkDirs { get; } = [];
    public string? Find(string name) => name == "dotnet" ? "/fake/dotnet" : null;

    public Task<ToolOutput> RunAsync(string fileName, IReadOnlyList<string> args, ToolRunOptions? options, CancellationToken ct) =>
        Task.FromResult(new ToolOutput(0, "10.0.300\n", ""));

    public Task<ToolOutput> RunStreamingAsync(string fileName, IReadOnlyList<string> args, ToolRunOptions? options, Action<string> onLine, CancellationToken ct)
    {
        Streaming.Add(fileName + " " + string.Join(' ', args));
        WorkDirs.Add(options?.WorkingDirectory);
        onLine("  Restored restore.csproj");
        // Like dotnet restore --packages: the pinned package lands in the packages folder.
        var packages = args[(args.ToList().IndexOf("--packages") + 1)];
        var dir = Path.Combine(packages, "microsoft.aspnetcore.testhost", "10.0.10");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, ".nupkg.metadata"), "{}");
        return Task.FromResult(new ToolOutput(0, "", ""));
    }
}

[Collection(DevAcademy.Desktop.Tests.Support.BindingCaptureCollection.Name)]
public class NuGetEnvironmentViewModelTests
{
    private static (TestHost host, NuGetToolRunner tools, string root) Setup()
    {
        var packages = ContentPackageIndex.TryLoadDefault() ?? throw new InvalidOperationException("tools/content-packages not found");
        var tools = new NuGetToolRunner();
        var dir = Path.Combine(Path.GetTempPath(), "devacademy-ui-nuget-" + Guid.NewGuid().ToString("N"));
        var root = Path.Combine(dir, "nuget-content"); // deliberately absent: nothing installed
        var host = new TestHost(dir, configure: s =>
        {
            s.AddSingleton<IToolRunner>(tools);
            s.AddSingleton(packages);
            s.AddSingleton(new NuGetEnvironmentService(tools, root, packages));
        });
        return (host, tools, root);
    }

    [Fact]
    public async Task Settings_list_the_nuget_set_as_not_installed_and_nothing_is_downloaded_on_load()
    {
        var (host, tools, _) = Setup();
        using (host)
        {
            var vm = new NuGetEnvironmentViewModel(host.Services);
            await vm.LoadAsync();
            var set = Assert.Single(vm.Sets, x => x.Set.Id == "aspnetcore-testing");
            Assert.False(set.IsInstalled);
            Assert.Empty(tools.Streaming);
        }
    }

    [Fact]
    public async Task Installing_confirms_with_packages_command_and_network_notice_then_restores_that_set_only()
    {
        var (host, tools, root) = Setup();
        using (host)
        {
            var vm = new NuGetEnvironmentViewModel(host.Services);
            await vm.LoadAsync();
            host.Dialogs.Answer = true;
            await vm.Sets.Single(x => x.Set.Id == "aspnetcore-testing").InstallCommand.ExecuteAsync(null);

            var msg = Assert.Single(host.Dialogs.ConfirmedMessages);
            Assert.Contains("dotnet restore", msg);
            Assert.Contains("Microsoft.AspNetCore.TestHost", msg);
            Assert.Contains("сеть", msg);
            Assert.StartsWith("/fake/dotnet restore restore.csproj --packages ", Assert.Single(tools.Streaming));
            Assert.Equal(Path.Combine(root, "aspnetcore-testing"), Assert.Single(tools.WorkDirs));
            Assert.True(vm.Sets.Single(x => x.Set.Id == "aspnetcore-testing").IsInstalled);
        }
    }

    [Fact]
    public async Task Declining_the_install_confirmation_runs_nothing()
    {
        var (host, tools, root) = Setup();
        using (host)
        {
            var vm = new NuGetEnvironmentViewModel(host.Services);
            await vm.LoadAsync();
            host.Dialogs.Answer = false;
            await vm.Sets.Single(x => x.Set.Id == "aspnetcore-testing").InstallCommand.ExecuteAsync(null);
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
            var vm = new NuGetEnvironmentViewModel(host.Services);
            await vm.LoadAsync();
            host.Dialogs.Answer = true;
            await vm.Sets.Single(x => x.Set.Id == "aspnetcore-testing").InstallCommand.ExecuteAsync(null);
            host.Dialogs.Answer = false;
            await vm.Sets.Single(x => x.Set.Id == "aspnetcore-testing").RemoveCommand.ExecuteAsync(null);
            Assert.True(Directory.Exists(Path.Combine(root, "aspnetcore-testing")));
            host.Dialogs.Answer = true;
            await vm.Sets.Single(x => x.Set.Id == "aspnetcore-testing").RemoveCommand.ExecuteAsync(null);
            Assert.False(Directory.Exists(Path.Combine(root, "aspnetcore-testing")));
        }
    }

    [Fact]
    public async Task A_blocked_lesson_offers_the_set_install_button_and_declining_runs_nothing()
    {
        var (host, tools, _) = Setup();
        using (host)
        {
            await host.Main.OpenLessonAsync("csharp.aspnetcore.minimal-apis-routing");
            var ws = Assert.IsType<LessonWorkspaceViewModel>(host.Main.Current);
            var session = Assert.IsType<ExerciseSessionViewModel>(ws.Practice);
            Assert.True(session.HasInstallSet);
            Assert.Equal("aspnetcore-testing", session.InstallSet!.Id);
            Assert.Contains("ASP.NET Core", session.InstallSetText);
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
    public async Task Accepting_the_lesson_install_restores_the_set_and_clears_the_block()
    {
        var (host, tools, root) = Setup();
        using (host)
        {
            await host.Main.OpenLessonAsync("csharp.aspnetcore.minimal-apis-routing");
            var ws = Assert.IsType<LessonWorkspaceViewModel>(host.Main.Current);
            var session = Assert.IsType<ExerciseSessionViewModel>(ws.Practice);
            host.Dialogs.Answer = true;
            await session.InstallSetCommand.ExecuteAsync(null);

            Assert.StartsWith("/fake/dotnet restore restore.csproj --packages ", Assert.Single(tools.Streaming));
            Assert.Equal(Path.Combine(root, "aspnetcore-testing"), Assert.Single(tools.WorkDirs));
            Assert.False(session.HasInstallSet);
            Assert.Null(session.EnvironmentNotice);
        }
    }
}
