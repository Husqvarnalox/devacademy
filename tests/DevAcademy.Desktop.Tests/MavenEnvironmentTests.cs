using System.Net;
using DevAcademy.Application.Maven;
using DevAcademy.Desktop.Tests.Support;
using DevAcademy.Desktop.ViewModels;
using DevAcademy.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace DevAcademy.Desktop.Tests;

internal sealed class CountingHandler : HttpMessageHandler
{
    public int Requests { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests++;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}

[Collection(DevAcademy.Desktop.Tests.Support.BindingCaptureCollection.Name)]
public class MavenEnvironmentViewModelTests
{
    private static (TestHost host, CountingHandler http, string root) Setup()
    {
        var packages = ContentPackageIndex.TryLoadDefault() ?? throw new InvalidOperationException("tools/content-packages not found");
        var http = new CountingHandler();
        var dir = Path.Combine(Path.GetTempPath(), "devacademy-ui-maven-" + Guid.NewGuid().ToString("N"));
        var root = Path.Combine(dir, "maven-content"); // deliberately absent: nothing installed
        var host = new TestHost(dir, configure: s =>
        {
            s.AddSingleton(packages);
            s.AddSingleton(new MavenEnvironmentService(new HttpClient(http), root, packages));
        });
        return (host, http, root);
    }

    [Fact]
    public async Task Settings_list_the_maven_set_as_not_installed_and_nothing_is_downloaded_on_load()
    {
        var (host, http, _) = Setup();
        using (host)
        {
            var vm = new MavenEnvironmentViewModel(host.Services);
            await vm.LoadAsync();
            var set = Assert.Single(vm.Sets, x => x.Set.Id == "spring-boot-web-test");
            Assert.False(set.IsInstalled);
            Assert.Equal(0, http.Requests);
        }
    }

    [Fact]
    public async Task The_confirmation_shows_count_size_source_and_network_notice()
    {
        var (host, http, root) = Setup();
        using (host)
        {
            var vm = new MavenEnvironmentViewModel(host.Services);
            await vm.LoadAsync();
            host.Dialogs.Answer = false;
            await vm.Sets.Single(x => x.Set.Id == "spring-boot-web-test").InstallCommand.ExecuteAsync(null);

            var msg = Assert.Single(host.Dialogs.ConfirmedMessages);
            Assert.Matches(@"\b\d{2,3}\b", msg); // the jar count of the lock
            Assert.Contains("МБ", msg);
            Assert.Contains("repo1.maven.org", msg);
            Assert.Contains("сеть", msg);
            Assert.Contains("spring-boot-starter-webmvc", msg);
            // Declining runs nothing: no request, no directory.
            Assert.Equal(0, http.Requests);
            Assert.False(Directory.Exists(root));
            Assert.False(vm.IsInstalling);
        }
    }

    [Fact]
    public async Task Accepting_downloads_and_a_failed_download_installs_nothing()
    {
        var (host, http, root) = Setup();
        using (host)
        {
            var vm = new MavenEnvironmentViewModel(host.Services);
            await vm.LoadAsync();
            host.Dialogs.Answer = true;
            await vm.Sets.Single(x => x.Set.Id == "spring-boot-web-test").InstallCommand.ExecuteAsync(null);
            Assert.Equal(1, http.Requests); // stops at the first failing jar
            Assert.False(vm.Sets.Single(x => x.Set.Id == "spring-boot-web-test").IsInstalled);
            Assert.NotNull(vm.Message);
        }
    }
}
