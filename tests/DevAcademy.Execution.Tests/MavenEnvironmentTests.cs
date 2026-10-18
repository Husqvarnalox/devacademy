using DevAcademy.Application.Maven;
using DevAcademy.Execution;

namespace DevAcademy.Execution.Tests;

/// <summary>Java against the read-only jar folder of a maven package set.</summary>
public class MavenEnvironmentTests : IDisposable
{
    private readonly string _work = Path.Combine(Path.GetTempPath(), "devacademy-mvn-plan-" + Guid.NewGuid().ToString("N"));

    public MavenEnvironmentTests()
    {
        Directory.CreateDirectory(Path.Combine(_work, "demo"));
        File.WriteAllText(Path.Combine(_work, "demo", "Main.java"), "");
    }

    public void Dispose()
    {
        try { Directory.Delete(_work, true); } catch (IOException) { }
    }

    private static RuntimeStatus Status() => new("java", RuntimeAvailability.Available, "22", "/fake/jdk/bin/javac", null, null);

    [Fact]
    public void WithoutALibraryFolderTheClassPathIsOnlyTheCompiledClasses()
    {
        var plan = new JavaRuntime().CreatePlan(_work, "demo/Main.java", Status());
        Assert.DoesNotContain("-parameters", plan.CompileSteps.Single().Args);
        Assert.Empty(plan.CompileSteps.Single().ReadOnlyPaths ?? []);
        Assert.Equal(["-XX:+UseSerialGC", "-Xss16m", .. ToolLocator.JvmEncoding, "-cp", "out", "demo.Main"], plan.Run.Args);
    }

    [Fact]
    public void WithALibraryFolderBothStepsPutItsJarsOnTheClassPathReadOnlyAndCapTheHeap()
    {
        var lib = Path.Combine(Path.GetTempPath(), "maven-lib");
        var plan = new JavaRuntime().CreateLibraryPlan(_work, "demo/Main.java", Status(), lib);
        var compile = plan.CompileSteps.Single();
        var wildcard = Path.Combine(lib, "*");
        Assert.Contains("-parameters", compile.Args);
        Assert.Equal(wildcard, compile.Args[compile.Args.ToList().IndexOf("-cp") + 1]);
        Assert.Equal([lib], compile.ReadOnlyPaths);
        Assert.Contains(JavaRuntime.LibraryHeap, plan.Run.Args);
        Assert.Equal("out" + Path.PathSeparator + wildcard, plan.Run.Args[plan.Run.Args.ToList().IndexOf("-cp") + 1]);
        Assert.Equal("demo.Main", plan.Run.Args[^1]);
        Assert.Equal([lib], plan.Run.ReadOnlyPaths);
    }

    [Fact]
    public async Task AnEmptyLibraryFolderIsAReportedFailure()
    {
        await TestSupport.RequireAsync("java");
        var dir = Path.Combine(Path.GetTempPath(), "devacademy-mvn-empty-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var request = TestSupport.Request("java", "Main.java", "class Main {}", IsolationLevel.OsSandbox) with { Maven = new MavenRunSelection(dir) };
            await using var program = await TestSupport.Runner.PrepareAsync(request, CancellationToken.None);
            Assert.Contains("make maven-env", program.PreparationFailure!.Message);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task LibraryFolderOnlyAppliesToJava()
    {
        await TestSupport.RequireAsync("python");
        var request = TestSupport.Request("python", "main.py", "print(1)", IsolationLevel.OsSandbox) with { Maven = new MavenRunSelection(Path.GetTempPath()) };
        await using var program = await TestSupport.Runner.PrepareAsync(request, CancellationToken.None);
        Assert.Equal(ExecutionStatus.InternalError, program.PreparationFailure!.Status);
    }

    private const string Program = """
        package demo;

        import org.springframework.boot.ApplicationContextFactory;
        import org.springframework.boot.Banner;
        import org.springframework.boot.SpringBootConfiguration;
        import org.springframework.boot.WebApplicationType;
        import org.springframework.boot.autoconfigure.EnableAutoConfiguration;
        import org.springframework.boot.builder.SpringApplicationBuilder;
        import org.springframework.context.ConfigurableApplicationContext;
        import org.springframework.context.annotation.Import;
        import org.springframework.mock.web.MockServletContext;
        import org.springframework.test.web.servlet.MockMvc;
        import org.springframework.test.web.servlet.setup.MockMvcBuilders;
        import org.springframework.web.bind.annotation.*;
        import org.springframework.web.context.WebApplicationContext;
        import org.springframework.web.context.support.GenericWebApplicationContext;
        import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;

        public class Main {
            @RestController
            static class Hello {
                @GetMapping("/hello/{name}")
                String hello(@PathVariable String name) { return "world " + name; }
            }

            @SpringBootConfiguration
            @EnableAutoConfiguration
            @Import(Hello.class)
            static class Config {}

            public static void main(String[] args) throws Exception {
                try (ConfigurableApplicationContext context = new SpringApplicationBuilder(Config.class)
                        .web(WebApplicationType.SERVLET).bannerMode(Banner.Mode.OFF).logStartupInfo(false)
                        .contextFactory(ApplicationContextFactory.of(() -> new GenericWebApplicationContext(new MockServletContext())))
                        .run("--logging.level.root=OFF")) {
                    MockMvc mvc = MockMvcBuilders.webAppContextSetup((WebApplicationContext) context).build();
                    System.out.println(mvc.perform(get("/hello/x")).andReturn().getResponse().getContentAsString());
                    try {
                        java.nio.file.Files.writeString(java.nio.file.Path.of("LIBPATH", "evil.txt"), "x");
                        System.out.println("WROTE");
                    } catch (Exception e) { }
                }
            }
        }
        """;

    [Fact]
    public async Task MockMvcProgramCompilesAndRunsOfflineFromTheRepositoryEnvironmentUnderTheSandbox()
    {
        await TestSupport.RequireAsync("java");
        var env = MavenEnvironmentService.FindRepositoryEnvironment();
        if (env is null) Assert.Skip("no .maven-content in the repository (run `make maven-env`)");
        var lib = Path.Combine(env, "spring-boot-web-test", "lib");
        if (!Directory.Exists(lib) || Directory.GetFiles(lib, "*.jar").Length == 0) Assert.Skip(".maven-content/spring-boot-web-test is not installed");
        var request = new ExecutionRequest("java", [new SourceFile("demo/Main.java", Program.Replace("LIBPATH", lib.Replace('\\', '/')))], "demo/Main.java", IsolationLevel.OsSandbox, Maven: new MavenRunSelection(lib));
        var limits = new ResourceLimits(TimeSpan.FromSeconds(20), 512L * 1024 * 1024, 64 * 1024);
        var result = await TestSupport.RunAsync(request, null, limits);
        if (result.AppliedIsolation != IsolationLevel.OsSandbox) Assert.Skip("no OS sandbox on this machine");

        Assert.True(result.IsSuccess, result.CompileOutput + result.Stderr + result.Message);
        Assert.Equal("world x", result.Stdout.Trim());
        Assert.False(File.Exists(Path.Combine(lib, "evil.txt")));
    }
}
