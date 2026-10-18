using DevAcademy.Execution;

namespace DevAcademy.Execution.Tests;

public class DebugBuildTests
{
    private static readonly RuntimeStatus Fake = new("x", RuntimeAvailability.Available, "1", "/usr/bin/tool", null, null);

    private static string Dir()
    {
        string d = Path.Combine(Path.GetTempPath(), "da-debugbuild-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(d);
        return d;
    }

    [Fact]
    public void Cpp_debug_plan_is_g_O0_with_absolute_sources_and_the_run_plan_stays_O2()
    {
        string d = Dir();
        File.WriteAllText(Path.Combine(d, "main.cpp"), "int main(){}");
        var rt = new CppRuntime();
        var debug = rt.CreateDebugPlan(d, "main.cpp", Fake).CompileSteps[0].Args;
        Assert.Contains("-g", debug);
        Assert.Contains("-O0", debug);
        Assert.DoesNotContain("-O2", debug);
        Assert.Contains(Path.Combine(d, "main.cpp"), debug);
        var normal = rt.CreatePlan(d, "main.cpp", Fake).CompileSteps[0].Args;
        Assert.Contains("-O2", normal);
        Assert.DoesNotContain("-g", normal);
    }

    [Theory]
    [InlineData(null, "-std=c++23")]
    [InlineData("c++23", "-std=c++23")]
    [InlineData("c++20", "-std=c++20")]
    public void Cpp_plans_build_with_the_detected_standard(string? standard, string flag)
    {
        string d = Dir();
        File.WriteAllText(Path.Combine(d, "main.cpp"), "int main(){}");
        var rt = new CppRuntime();
        var status = Fake with { LanguageStandard = standard };
        Assert.Contains(flag, rt.CreatePlan(d, "main.cpp", status).CompileSteps[0].Args);
        Assert.Contains(flag, rt.CreateDebugPlan(d, "main.cpp", status).CompileSteps[0].Args);
    }

    [Fact]
    public async Task Detected_cpp_compiler_reports_its_standard_and_explains_a_fallback()
    {
        var status = await new CppRuntime().DetectAsync(TestContext.Current.CancellationToken);
        Assert.SkipWhen(status.Availability == RuntimeAvailability.Missing, "No C++ compiler on this machine.");
        if (status.Availability == RuntimeAvailability.Unsupported) { Assert.Null(status.LanguageStandard); return; }
        Assert.Contains(status.LanguageStandard, new[] { "c++23", "c++20" });
        if (status.LanguageStandard == "c++20")
        {
            Assert.Equal(EngineMessages.CppFallback20Key, status.NotesText?.Key);
            Assert.Equal(EngineMessages.CppFallback20Key, status.WarningText?.Key);
        }
        else if (status.Warning is not null) Assert.Equal(EngineMessages.CppNoExpectedKey, status.WarningText?.Key); // C++23 compiler whose library lacks std::expected
    }

    [Fact]
    public void Rust_debug_plan_disables_optimisation_and_keeps_the_normal_plan_optimised()
    {
        var rt = new RustRuntime();
        var debug = rt.CreateDebugPlan("/w", "main.rs", Fake).CompileSteps[0].Args;
        Assert.Contains("-g", debug);
        Assert.Contains("opt-level=0", debug);
        Assert.DoesNotContain("-O", debug);
        Assert.Contains("-O", rt.CreatePlan("/w", "main.rs", Fake).CompileSteps[0].Args);
    }

    [Fact]
    public void Go_debug_plan_passes_N_l_and_the_normal_plan_does_not()
    {
        string d = Dir();
        File.WriteAllText(Path.Combine(d, "main.go"), "package main");
        var rt = new GoRuntime(Path.Combine(d, "cache"));
        Assert.Contains("-gcflags=all=-N -l", rt.CreateDebugPlan(d, "main.go", Fake).CompileSteps[0].Args);
        Assert.DoesNotContain(rt.CreatePlan(d, "main.go", Fake).CompileSteps[0].Args, a => a.StartsWith("-gcflags", StringComparison.Ordinal));
    }

    [Fact]
    public void Builder_supports_exactly_the_native_languages()
    {
        var b = DebugBuilderFactory.Create();
        Assert.Equivalent(new[] { "cpp", "rust", "go" }, b.Languages);
    }

    [Fact]
    public async Task Builder_compiles_a_cpp_program_with_debug_info_when_a_compiler_exists()
    {
        var status = await new CppRuntime().DetectAsync(TestContext.Current.CancellationToken);
        Assert.SkipUnless(status.Availability == RuntimeAvailability.Available, "no C++ compiler");
        string d = Dir();
        File.WriteAllText(Path.Combine(d, "main.cpp"), "int main() { int x = 1; return x - 1; }\n");
        var r = await DebugBuilderFactory.Create().BuildAsync("cpp", d, "main.cpp", TestContext.Current.CancellationToken);
        Assert.True(r.Success, r.Output);
        Assert.True(File.Exists(r.Executable));
    }

    [Fact]
    public async Task Builder_reports_compile_errors_and_unknown_languages()
    {
        var status = await new CppRuntime().DetectAsync(TestContext.Current.CancellationToken);
        Assert.SkipUnless(status.Availability == RuntimeAvailability.Available, "no C++ compiler");
        string d = Dir();
        File.WriteAllText(Path.Combine(d, "main.cpp"), "int main( { }\n");
        var r = await DebugBuilderFactory.Create().BuildAsync("cpp", d, "main.cpp", TestContext.Current.CancellationToken);
        Assert.False(r.Success);
        Assert.Contains("error", r.Output);
        Assert.False((await DebugBuilderFactory.Create().BuildAsync("python", d, "main.py", TestContext.Current.CancellationToken)).Success);
    }
}
