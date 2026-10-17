using DevAcademy.Application.NuGet;
using DevAcademy.Domain;
using DevAcademy.Execution;

namespace DevAcademy.Application.Tests;

internal sealed class ScriptedNuGetToolRunner(string? dotnetVersion = "10.0.300") : IToolRunner
{
    public List<string> Streaming { get; } = [];
    public ToolRunOptions? Options { get; private set; }

    public string? Find(string name) => name == "dotnet" && dotnetVersion is not null ? "/fake/dotnet" : null;

    public Task<ToolOutput> RunAsync(string fileName, IReadOnlyList<string> args, ToolRunOptions? options, CancellationToken ct) =>
        Task.FromResult(args.Count == 1 && args[0] == "--version" ? new ToolOutput(0, dotnetVersion + "\n", "") : new ToolOutput(1, "", "unexpected"));

    public Task<ToolOutput> RunStreamingAsync(string fileName, IReadOnlyList<string> args, ToolRunOptions? options, Action<string> onLine, CancellationToken ct)
    {
        Streaming.Add(fileName + " " + string.Join(' ', args));
        Options = options;
        onLine("  Restored restore.csproj");
        return Task.FromResult(new ToolOutput(0, "", ""));
    }
}

/// <summary>A package-set index with a nuget set <c>aspnetcore-testing</c> plus an environment root with the given packages (set, id, version) restored.</summary>
public sealed class NuGetFixture : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "devacademy-nugetapp-" + Guid.NewGuid().ToString("N"));
    public string Env => Path.Combine(Root, "env");
    public string Packages => Path.Combine(Root, "content-packages");
    public ContentPackageIndex Index { get; }
    public ContentPackageSet Set => Index.Find(PackageSetLanguage.NuGet, "aspnetcore-testing")!;

    public NuGetFixture(params (string Set, string Id, string Version)[] installed)
    {
        Directory.CreateDirectory(Packages);
        File.WriteAllText(Path.Combine(Packages, "index.json"), """
            [
              { "id": "aspnetcore-testing", "title": "ASP.NET Core testing", "language": "nuget", "description": "d", "packages": ["Microsoft.AspNetCore.TestHost"] },
              { "id": "numpy", "title": "NumPy", "language": "python", "description": "d", "packages": ["numpy"] }
            ]
            """);
        var dir = Path.Combine(Packages, "nuget", "aspnetcore-testing");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "restore.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Microsoft.AspNetCore.TestHost" Version="10.0.10" />
              </ItemGroup>
            </Project>
            """);
        Directory.CreateDirectory(Path.Combine(Packages, "python"));
        File.WriteAllText(Path.Combine(Packages, "python", "numpy.txt"), "# c\nnumpy==2.5.4\n");
        Assert.True(ContentPackageIndex.TryLoad(Packages, out var index, out _));
        Index = index;
        Directory.CreateDirectory(Env);
        foreach (var (set, id, version) in installed)
        {
            var dirVersion = Path.Combine(Env, set, "packages", id.ToLowerInvariant(), version);
            Directory.CreateDirectory(dirVersion);
            File.WriteAllText(Path.Combine(dirVersion, ".nupkg.metadata"), "{}");
        }
    }

    public NuGetEnvironmentService Service(IToolRunner? tools = null) => new(tools ?? new ScriptedNuGetToolRunner(), Env, Index);

    public void Dispose()
    {
        try { Directory.Delete(Root, true); }
        catch (IOException) { }
    }
}

public class NuGetEnvironmentServiceTests
{
    private static readonly ExerciseRequirements TestHost = new(null, [], NugetPackages: ["Microsoft.AspNetCore.TestHost"]);

    [Fact]
    public void NotInstalledSetIsNamedAndPointsToSettingsAndTheMakeTarget()
    {
        using var fx = new NuGetFixture();
        var check = fx.Service().Check(TestHost);
        Assert.Contains("ASP.NET Core testing", check.Problem);
        Assert.Contains("make nuget-env", check.Problem);
        Assert.Equal("aspnetcore-testing", check.Set!.Id);
    }

    [Fact]
    public async Task InstalledSetResolvesToItsPackagesFolder()
    {
        using var fx = new NuGetFixture(("aspnetcore-testing", "Microsoft.AspNetCore.TestHost", "10.0.10"));
        var service = fx.Service();
        Assert.Null(service.Check(TestHost).Problem);
        var exercise = new Exercise("e", "E", "", "csharp", "Program.cs", [new("Program.cs", "")], [new("Program.cs", "")],
            EvaluationKind.Io, [], null, [], ["h"], Requires: TestHost);
        var plan = await service.ResolveAsync(exercise, default);
        Assert.False(plan.IsBlocked);
        Assert.Equal(Path.Combine(fx.Env, "aspnetcore-testing", "packages"), plan.NuGet!.PackagesPath);
    }

    [Fact]
    public async Task ExerciseWithoutNuGetPackagesNeedsNoEnvironment()
    {
        using var fx = new NuGetFixture();
        var exercise = new Exercise("e", "E", "", "csharp", "Program.cs", [new("Program.cs", "")], [new("Program.cs", "")],
            EvaluationKind.Io, [], null, [], ["h"]);
        var plan = await fx.Service().ResolveAsync(exercise, default);
        Assert.False(plan.IsBlocked);
        Assert.Null(plan.NuGet);
    }

    [Fact]
    public void WrongRestoredVersionCountsAsMissing()
    {
        using var fx = new NuGetFixture(("aspnetcore-testing", "Microsoft.AspNetCore.TestHost", "10.0.9"));
        Assert.Contains("Microsoft.AspNetCore.TestHost", fx.Service().Check(TestHost).Problem);
    }

    [Fact]
    public void UncoveredPackageIsNotInAnySet()
    {
        using var fx = new NuGetFixture();
        var check = fx.Service().Check(new ExerciseRequirements(null, [], NugetPackages: ["Some.Other.Package"]));
        Assert.NotNull(check.Problem);
        Assert.Null(check.Set);
    }

    [Fact]
    public void PlanInstallShowsTheExactCommandAndIsolatedEnvironment()
    {
        using var fx = new NuGetFixture();
        var (plan, error) = fx.Service().PlanInstall(fx.Set);
        Assert.Null(error);
        Assert.Equal(["restore", "restore.csproj", "--packages", Path.Combine(fx.Env, "aspnetcore-testing", "packages"), "--source", NuGetEnvironmentService.Source], plan!.Args);
        Assert.Contains("dotnet restore restore.csproj", plan.CommandLine);
        Assert.StartsWith(Path.Combine(fx.Env, "aspnetcore-testing"), plan.Environment["DOTNET_CLI_HOME"]);
        Assert.Contains(plan.Packages, p => p.Name == "Microsoft.AspNetCore.TestHost" && p.Version == "10.0.10");
    }

    [Fact]
    public void PlanInstallWithoutDotnetExplainsWhere()
    {
        using var fx = new NuGetFixture();
        var (plan, error) = fx.Service(new ScriptedNuGetToolRunner(null)).PlanInstall(fx.Set);
        Assert.Null(plan);
        Assert.Contains("dotnet", error);
    }

    [Fact]
    public async Task InstallCopiesTheRestoreProjectAndRunsDotnetRestoreInTheSetDirectory()
    {
        using var fx = new NuGetFixture();
        var tools = new ScriptedNuGetToolRunner();
        var service = fx.Service(tools);
        var (plan, _) = service.PlanInstall(fx.Set);
        var lines = new List<string>();
        var result = await service.InstallAsync(plan!, lines.Add, default);
        Assert.True(result.Ok);
        Assert.StartsWith("/fake/dotnet restore restore.csproj --packages ", Assert.Single(tools.Streaming));
        Assert.Equal(Path.Combine(fx.Env, "aspnetcore-testing"), tools.Options!.WorkingDirectory);
        Assert.True(File.Exists(Path.Combine(fx.Env, "aspnetcore-testing", "restore.csproj")));
        Assert.NotEmpty(lines);
    }

    [Fact]
    public async Task InspectListsVersionsOfInstalledPackages()
    {
        using var fx = new NuGetFixture(("aspnetcore-testing", "Microsoft.AspNetCore.TestHost", "10.0.10"));
        var status = await fx.Service().InspectAsync(default);
        Assert.Equal("10.0.300", status.DotnetVersion);
        var set = Assert.Single(status.Sets);
        Assert.True(set.Installed);
        Assert.Contains(set.Packages, p => p.Id == "Microsoft.AspNetCore.TestHost" && p.Version == "10.0.10");
    }

    [Fact]
    public void RemoveSetDeletesOnlyThatSet()
    {
        using var fx = new NuGetFixture(("aspnetcore-testing", "Microsoft.AspNetCore.TestHost", "10.0.10"));
        var other = Path.Combine(fx.Env, "keep");
        Directory.CreateDirectory(other);
        var result = fx.Service().RemoveSet(fx.Set);
        Assert.True(result.Ok, result.Error);
        Assert.False(Directory.Exists(Path.Combine(fx.Env, "aspnetcore-testing")));
        Assert.True(Directory.Exists(other));
    }

    [Fact]
    public void RealIndexCoversTheTestHostPackage()
    {
        var dir = ContentPackageIndex.FindDirectory(AppContext.BaseDirectory);
        Assert.NotNull(dir);
        Assert.True(ContentPackageIndex.TryLoad(dir!, out var index, out var error), error);
        var (set, resolveError) = (index.Resolve(PackageSetLanguage.NuGet, ["Microsoft.AspNetCore.TestHost"]).Set, index.Resolve(PackageSetLanguage.NuGet, ["Microsoft.AspNetCore.TestHost"]).Error);
        Assert.True(set is not null, resolveError);
        Assert.Equal("aspnetcore-testing", set!.Id);
        Assert.Contains(index.ReadPins(set), p => p.Name == "Microsoft.AspNetCore.TestHost" && p.Version == "10.0.10");
    }
}
