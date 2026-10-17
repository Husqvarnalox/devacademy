using DevAcademy.Application.NuGet;
using DevAcademy.Execution;

namespace DevAcademy.Execution.Tests;

/// <summary>C# project mode and the read-only NuGet packages folder of a package set.</summary>
public class NuGetEnvironmentTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "devacademy-nuget-" + Guid.NewGuid().ToString("N"));

    public NuGetEnvironmentTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch (IOException) { }
    }

    private static RuntimeStatus Status() => new("csharp", RuntimeAvailability.Available, "10.0.300", "/fake/dotnet", null, null);

    [Fact]
    public void WithoutAProjectTheEntryFileIsBuiltAsAFileBasedApp()
    {
        File.WriteAllText(Path.Combine(_dir, "Program.cs"), "");
        var plan = new CSharpRuntime(Path.Combine(_dir, "cache")).CreatePlan(_dir, "Program.cs", Status());
        Assert.Equal("Program.cs", plan.CompileSteps.Single().Args[1]);
        Assert.Equal(Path.Combine("out", "Program.dll"), plan.Run.Args.Single());
    }

    [Fact]
    public void ASingleProjectIsBuiltAndItsAssemblyNameIsRun()
    {
        File.WriteAllText(Path.Combine(_dir, "App.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><AssemblyName>Demo</AssemblyName></PropertyGroup></Project>");
        File.WriteAllText(Path.Combine(_dir, "Program.cs"), "");
        var plan = new CSharpRuntime(Path.Combine(_dir, "cache")).CreatePlan(_dir, "Program.cs", Status());
        var build = plan.CompileSteps.Single();
        Assert.Equal(["build", "App.csproj"], build.Args.Take(2));
        Assert.Contains("-o", build.Args);
        Assert.Equal(Path.Combine("out", "Demo.dll"), plan.Run.Args.Single());
    }

    [Fact]
    public void ProjectWithoutAssemblyNameUsesTheFileName()
    {
        File.WriteAllText(Path.Combine(_dir, "Shop.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        var plan = new CSharpRuntime(Path.Combine(_dir, "cache")).CreatePlan(_dir, "Program.cs", Status());
        Assert.Equal(Path.Combine("out", "Shop.dll"), plan.Run.Args.Single());
    }

    [Fact]
    public void TwoProjectsFallBackToTheFileBasedApp()
    {
        File.WriteAllText(Path.Combine(_dir, "A.csproj"), "<Project />");
        File.WriteAllText(Path.Combine(_dir, "B.csproj"), "<Project />");
        var plan = new CSharpRuntime(Path.Combine(_dir, "cache")).CreatePlan(_dir, "Program.cs", Status());
        Assert.Equal("Program.cs", plan.CompileSteps.Single().Args[1]);
    }

    [Fact]
    public void PackagesEnvironmentIsOfflineAndPointsAtTheFolder()
    {
        Assert.Equal("/x/packages", CSharpRuntime.PackagesEnvironment("/x/packages")["NUGET_PACKAGES"]);
        Assert.Equal(["-p:RestoreSources=/x/packages"], CSharpRuntime.OfflineRestoreArguments("/x/packages"));
    }

    [Fact]
    public async Task EmptyPackagesFolderIsAReportedFailure()
    {
        await TestSupport.RequireAsync("csharp");
        var request = TestSupport.Request("csharp", "Program.cs", "System.Console.WriteLine(1);", IsolationLevel.OsSandbox) with { NuGet = new NuGetRunSelection(_dir) };
        await using var program = await TestSupport.Runner.PrepareAsync(request, CancellationToken.None);
        Assert.Contains("make nuget-env", program.PreparationFailure!.Message);
    }

    [Fact]
    public async Task PackagesFolderOnlyAppliesToCSharp()
    {
        await TestSupport.RequireAsync("python");
        var request = TestSupport.Request("python", "main.py", "print(1)", IsolationLevel.OsSandbox) with { NuGet = new NuGetRunSelection(_dir) };
        await using var program = await TestSupport.Runner.PrepareAsync(request, CancellationToken.None);
        Assert.Equal(ExecutionStatus.InternalError, program.PreparationFailure!.Status);
    }

    private const string Csproj = """
        <Project Sdk="Microsoft.NET.Sdk.Web">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
            <Nullable>enable</Nullable>
            <ImplicitUsings>enable</ImplicitUsings>
            <AssemblyName>Probe</AssemblyName>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="Microsoft.AspNetCore.TestHost" Version="10.0.10" />
          </ItemGroup>
        </Project>
        """;

    private const string Program = """
        using Microsoft.AspNetCore.Builder;
        using Microsoft.AspNetCore.Hosting;
        using Microsoft.AspNetCore.TestHost;
        using Microsoft.Extensions.Hosting;
        using Microsoft.Extensions.Logging;

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        var app = builder.Build();
        app.MapGet("/hello", () => "world");
        await app.StartAsync();
        var client = app.GetTestClient();
        Console.WriteLine(await client.GetStringAsync("/hello"));
        try { File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("NUGET_PACKAGES")!, "evil.txt"), "x"); Console.WriteLine("WROTE"); } catch (Exception) { }
        await app.StopAsync();
        """;

    [Fact]
    public async Task TestServerProjectBuildsAndRunsOfflineFromTheRepositoryEnvironmentUnderTheSandbox()
    {
        await TestSupport.RequireAsync("csharp");
        var env = NuGetEnvironmentService.FindRepositoryEnvironment();
        if (env is null) Assert.Skip("no .nuget-content in the repository (run `make nuget-env`)");
        var packages = Path.Combine(env, "aspnetcore-testing", "packages");
        if (!Directory.Exists(packages)) Assert.Skip(".nuget-content/aspnetcore-testing is not installed");
        var request = new ExecutionRequest("csharp", [new SourceFile("Probe.csproj", Csproj), new SourceFile("Program.cs", Program)],
            "Program.cs", IsolationLevel.OsSandbox, NuGet: new NuGetRunSelection(packages));
        var limits = new ResourceLimits(TimeSpan.FromSeconds(30), 1024L * 1024 * 1024, 64 * 1024);
        var result = await TestSupport.RunAsync(request, null, limits);
        if (result.AppliedIsolation != IsolationLevel.OsSandbox) Assert.Skip("no OS sandbox on this machine");

        Assert.True(result.IsSuccess, result.CompileOutput + result.Stderr + result.Message);
        Assert.Equal("world", result.Stdout.Trim());
        Assert.False(File.Exists(Path.Combine(packages, "evil.txt")));
    }
}
