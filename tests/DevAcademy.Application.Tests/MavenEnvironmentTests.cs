using System.Net;
using System.Security.Cryptography;
using System.Text;
using DevAcademy.Application.Maven;
using DevAcademy.Domain;

namespace DevAcademy.Application.Tests;

/// <summary>Serves bytes by request path; anything not registered is a 404. Records what was asked for.</summary>
public sealed class FakeMavenHandler : HttpMessageHandler
{
    public Dictionary<string, byte[]> Files { get; } = [];
    public HashSet<string> Failing { get; } = [];
    public List<string> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;
        Requests.Add(request.RequestUri.Host + path);
        if (Failing.Contains(path)) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        return Task.FromResult(Files.TryGetValue(path, out var bytes)
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) }
            : new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}

/// <summary>A package-set index with a maven set <c>web-test</c> whose lock.json lists two small fake jars.</summary>
public sealed class MavenFixture : IDisposable
{
    public static readonly byte[] JarA = Encoding.UTF8.GetBytes("jar-a-content");
    public static readonly byte[] JarB = Encoding.UTF8.GetBytes("jar-b-content-longer");

    public string Root { get; } = Path.Combine(Path.GetTempPath(), "devacademy-mavenapp-" + Guid.NewGuid().ToString("N"));
    public string Env => Path.Combine(Root, "env");
    public string Packages => Path.Combine(Root, "content-packages");
    public ContentPackageIndex Index { get; }
    public ContentPackageSet Set => Index.Find(PackageSetLanguage.Maven, "web-test")!;

    private static string Sha(byte[] b) => Convert.ToHexStringLower(SHA256.HashData(b));

    public MavenFixture(byte[]? lockedB = null)
    {
        Directory.CreateDirectory(Packages);
        File.WriteAllText(Path.Combine(Packages, "index.json"), """
            [
              { "id": "web-test", "title": "Web test", "language": "maven", "description": "d", "packages": ["org.example:alpha", "org.example:beta"] }
            ]
            """);
        var dir = Path.Combine(Packages, "maven", "web-test");
        Directory.CreateDirectory(dir);
        var b = lockedB ?? JarB;
        File.WriteAllText(Path.Combine(dir, "lock.json"), $$"""
            {
              "set": "web-test",
              "source": "https://repo.example.org/maven2",
              "jars": [
                { "groupId": "org.example", "artifactId": "alpha", "version": "1.0", "file": "alpha-1.0.jar", "sha256": "{{Sha(JarA)}}", "size": {{JarA.Length}} },
                { "groupId": "org.example", "artifactId": "beta", "version": "2.0", "file": "beta-2.0.jar", "sha256": "{{Sha(b)}}", "size": {{b.Length}} }
              ]
            }
            """);
        Assert.True(ContentPackageIndex.TryLoad(Packages, out var index, out _));
        Index = index;
    }

    public FakeMavenHandler Handler(bool bothJars = true)
    {
        var handler = new FakeMavenHandler();
        handler.Files["/maven2/org/example/alpha/1.0/alpha-1.0.jar"] = JarA;
        if (bothJars) handler.Files["/maven2/org/example/beta/2.0/beta-2.0.jar"] = JarB;
        return handler;
    }

    public MavenEnvironmentService Service(HttpMessageHandler handler) => new(new HttpClient(handler), Env, Index);

    public void Dispose()
    {
        try { Directory.Delete(Root, true); }
        catch (IOException) { }
    }
}

public class MavenEnvironmentServiceTests
{
    private static readonly ExerciseRequirements Needs = new(null, [], MavenArtifacts: ["org.example:alpha"]);

    [Fact]
    public async Task InstallDownloadsEveryLockedJarFromTheLockSourceAndVerifiesThem()
    {
        using var fx = new MavenFixture();
        var handler = fx.Handler();
        var service = fx.Service(handler);
        var (plan, error) = service.PlanInstall(fx.Set);
        Assert.Null(error);
        Assert.Equal(2, plan!.JarCount);
        Assert.Equal("repo.example.org", plan.Host);
        Assert.Equal(MavenFixture.JarA.Length + MavenFixture.JarB.Length, plan.TotalBytes);

        var result = await service.InstallAsync(plan, _ => { }, CancellationToken.None);

        Assert.True(result.Ok, result.Error);
        Assert.Equal(["repo.example.org/maven2/org/example/alpha/1.0/alpha-1.0.jar", "repo.example.org/maven2/org/example/beta/2.0/beta-2.0.jar"], handler.Requests);
        Assert.Equal(MavenFixture.JarA, File.ReadAllBytes(Path.Combine(service.LibPath(fx.Set), "alpha-1.0.jar")));
        Assert.Empty(Directory.GetFiles(service.LibPath(fx.Set), "*.part"));
        Assert.True(service.IsInstalled(fx.Set));
    }

    [Fact]
    public async Task AHashMismatchFailsAndDeletesTheDownloadedFile()
    {
        using var fx = new MavenFixture();
        var handler = fx.Handler();
        // Same length as the locked jar, different content: only the sha256 can tell.
        handler.Files["/maven2/org/example/alpha/1.0/alpha-1.0.jar"] = Encoding.UTF8.GetBytes("jar-a-evil!!!");
        Assert.Equal(MavenFixture.JarA.Length, handler.Files["/maven2/org/example/alpha/1.0/alpha-1.0.jar"].Length);
        var service = fx.Service(handler);

        var result = await service.InstallAsync(service.PlanInstall(fx.Set).Plan!, _ => { }, CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Contains("sha256", result.Error);
        Assert.Empty(Directory.GetFiles(service.LibPath(fx.Set)));
        Assert.Single(handler.Requests); // stopped at the bad jar
        Assert.False(service.IsInstalled(fx.Set));
    }

    [Fact]
    public async Task AJarLargerThanLockedIsRejectedAndRemoved()
    {
        using var fx = new MavenFixture();
        var handler = fx.Handler();
        handler.Files["/maven2/org/example/alpha/1.0/alpha-1.0.jar"] = [.. MavenFixture.JarA, .. MavenFixture.JarA];
        var service = fx.Service(handler);

        var result = await service.InstallAsync(service.PlanInstall(fx.Set).Plan!, _ => { }, CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Empty(Directory.GetFiles(service.LibPath(fx.Set)));
    }

    [Fact]
    public async Task APartialFailureKeepsVerifiedJarsAndARetryFetchesOnlyTheRest()
    {
        using var fx = new MavenFixture();
        var handler = fx.Handler();
        handler.Failing.Add("/maven2/org/example/beta/2.0/beta-2.0.jar");
        var service = fx.Service(handler);
        var plan = service.PlanInstall(fx.Set).Plan!;

        var first = await service.InstallAsync(plan, _ => { }, CancellationToken.None);

        Assert.False(first.Ok);
        Assert.Contains("500", first.Error);
        Assert.Equal(["alpha-1.0.jar"], Directory.GetFiles(service.LibPath(fx.Set)).Select(f => Path.GetFileName(f)!).ToArray());
        Assert.False(service.IsInstalled(fx.Set));

        handler.Failing.Clear();
        handler.Requests.Clear();
        var second = await service.InstallAsync(plan, _ => { }, CancellationToken.None);
        Assert.True(second.Ok, second.Error);
        Assert.Equal(["repo.example.org/maven2/org/example/beta/2.0/beta-2.0.jar"], handler.Requests);
        Assert.True(service.IsInstalled(fx.Set));
    }

    [Fact]
    public async Task ACancelledInstallLeavesNoPartialFile()
    {
        using var fx = new MavenFixture();
        var service = fx.Service(fx.Handler());
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var result = await service.InstallAsync(service.PlanInstall(fx.Set).Plan!, _ => { }, cts.Token);
        Assert.True(result.Cancelled);
        Assert.Empty(Directory.GetFiles(service.LibPath(fx.Set)));
    }

    [Fact]
    public async Task PlanningAndInspectingNeverTouchTheNetwork()
    {
        using var fx = new MavenFixture();
        var handler = fx.Handler();
        var service = fx.Service(handler);
        service.PlanInstall(fx.Set);
        var status = await service.InspectAsync(CancellationToken.None);
        await service.ResolveAsync(new Exercise("e", "E", "", "java", "demo/Main.java", [new("demo/Main.java", "")], [new("demo/Main.java", "")],
            EvaluationKind.Io, [], null, [], ["h"], Requires: Needs), CancellationToken.None);
        Assert.Empty(handler.Requests);
        Assert.False(Assert.Single(status.Sets).Installed);
    }

    [Fact]
    public void AMissingSetIsNamedWithTheFixAndTheLibPathIsPassedOnceComplete()
    {
        using var fx = new MavenFixture();
        var service = fx.Service(fx.Handler());
        var missing = service.Check(Needs);
        Assert.Contains("Web test", missing.Problem);
        Assert.Contains("make maven-env", missing.Problem);

        Directory.CreateDirectory(service.LibPath(fx.Set));
        File.WriteAllBytes(Path.Combine(service.LibPath(fx.Set), "alpha-1.0.jar"), MavenFixture.JarA);
        Assert.Contains("1 из 2", service.Check(Needs).Problem);

        File.WriteAllBytes(Path.Combine(service.LibPath(fx.Set), "beta-2.0.jar"), MavenFixture.JarB);
        Assert.Null(service.Check(Needs).Problem);
    }

    [Fact]
    public void AnArtifactNoSetProvidesIsReported()
    {
        using var fx = new MavenFixture();
        var check = fx.Service(fx.Handler()).Check(new ExerciseRequirements(null, [], MavenArtifacts: ["org.example:nope"]));
        Assert.NotNull(check.Problem);
    }

    [Fact]
    public async Task RemoveDeletesOnlyThatSetDirectory()
    {
        using var fx = new MavenFixture();
        var service = fx.Service(fx.Handler());
        await service.InstallAsync(service.PlanInstall(fx.Set).Plan!, _ => { }, CancellationToken.None);
        var other = Path.Combine(fx.Env, "other");
        Directory.CreateDirectory(other);
        Assert.True(service.RemoveSet(fx.Set).Ok);
        Assert.False(Directory.Exists(service.SetPath(fx.Set)));
        Assert.True(Directory.Exists(other));
    }
}
