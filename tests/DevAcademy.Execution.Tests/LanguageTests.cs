using DevAcademy.Execution;

namespace DevAcademy.Execution.Tests;

public class LanguageTests
{
    private static readonly Dictionary<string, (string File, string Code)> Hello = new()
    {
        ["python"] = ("main.py", "import sys\nprint('hello ' + sys.stdin.readline().strip())\n"),
        ["javascript"] = ("main.js", "const l = require('fs').readFileSync(0, 'utf8').trim();\nconsole.log('hello ' + l);\n"),
        ["typescript"] = ("main.ts", "import { readFileSync } from 'node:fs';\nconst l: string = readFileSync(0, 'utf8').trim();\nconsole.log('hello ' + l);\n"),
        ["go"] = ("main.go", "package main\nimport (\"bufio\";\"fmt\";\"os\";\"strings\")\nfunc main() {\n r := bufio.NewReader(os.Stdin)\n l, _ := r.ReadString('\\n')\n fmt.Println(\"hello \" + strings.TrimSpace(l))\n}\n"),
        ["java"] = ("Main.java", "import java.util.Scanner;\npublic class Main { public static void main(String[] a) { Scanner s = new Scanner(System.in); System.out.println(\"hello \" + s.nextLine().trim()); } }\n"),
        ["cpp"] = ("main.cpp", "#include <iostream>\n#include <string>\nint main() { std::string l; std::getline(std::cin, l); std::cout << \"hello \" << l << std::endl; }\n"),
        ["csharp"] = ("Program.cs", "Console.WriteLine(\"hello \" + Console.ReadLine());\n"),
        ["php"] = ("main.php", "<?php\necho 'hello ' . trim(fgets(STDIN)) . \"\\n\";\n"),
        ["kotlin"] = ("Main.kt", "fun main() { println(\"hello \" + readLine()!!.trim()) }\n"),
        ["rust"] = ("main.rs", "use std::io::BufRead;\nfn main() { let mut l = String::new(); std::io::stdin().lock().read_line(&mut l).unwrap(); println!(\"hello {}\", l.trim()); }\n"),
    };

    public static TheoryData<string> Languages => new(Hello.Keys.ToArray());

    [Theory]
    [MemberData(nameof(Languages))]
    public async Task Hello_world_with_stdin_echo(string language)
    {
        await TestSupport.RequireAsync(language);
        var (file, code) = Hello[language];
        ExecutionResult r = await TestSupport.RunAsync(TestSupport.Request(language, file, code), "world\n");
        Assert.True(r.IsSuccess, $"{r.Status}: {r.CompileOutput}{r.Stderr} {r.Message}");
        Assert.Equal("hello world", r.Stdout.Trim());
        Assert.Equal(0, r.ExitCode);
    }

    [Theory]
    [InlineData("go", "main.go", "package main\nfunc main() { undefinedSymbol() }\n")]
    [InlineData("java", "Main.java", "public class Main { public static void main(String[] a) { int x = \"s\"; } }\n")]
    [InlineData("cpp", "main.cpp", "int main() { return undefined_symbol; }\n")]
    [InlineData("csharp", "Program.cs", "int x = \"not an int\";\n")]
    [InlineData("rust", "main.rs", "fn main() { let x: i32 = \"s\"; }\n")]
    [InlineData("kotlin", "Main.kt", "fun main() { val x: Int = \"s\" }\n")]
    public async Task Compile_errors_are_reported(string language, string file, string code)
    {
        await TestSupport.RequireAsync(language);
        ExecutionResult r = await TestSupport.RunAsync(TestSupport.Request(language, file, code));
        Assert.Equal(ExecutionStatus.CompileError, r.Status);
        Assert.NotEmpty(r.CompileOutput);
    }

    [Fact]
    public async Task Cpp_runs_a_program_that_joins_two_threads()
    {
        await TestSupport.RequireAsync("cpp");
        const string code = "#include <iostream>\n#include <thread>\n#include <atomic>\n" +
            "int main() { std::atomic<int> n{0};\n" +
            "  std::thread a([&] { for (int i = 0; i < 1000; ++i) ++n; });\n" +
            "  std::thread b([&] { for (int i = 0; i < 1000; ++i) ++n; });\n" +
            "  a.join(); b.join(); std::cout << n << std::endl; }\n";
        ExecutionResult r = await TestSupport.RunAsync(TestSupport.Request("cpp", "main.cpp", code));
        Assert.True(r.IsSuccess, $"{r.Status}: {r.CompileOutput}{r.Stderr} {r.Message}");
        Assert.Equal("2000", r.Stdout.Trim());
    }

    private const string CoroutineProgram = "import kotlinx.coroutines.*\n" +
        "fun main() = runBlocking {\n" +
        "    val a = launch { delay(30); println(\"slow\") }\n" +
        "    val b = launch { delay(1); println(\"fast\") }\n" +
        "    joinAll(a, b)\n" +
        "    println(\"done\")\n" +
        "}\n";

    [Fact]
    public async Task Kotlin_runs_coroutines_when_the_kotlinc_distribution_ships_the_jar()
    {
        await TestSupport.RequireAsync("kotlin");
        RuntimeStatus s = await TestSupport.StatusAsync("kotlin");
        if (KotlinRuntime.FindCoroutinesJar(s.ExecutablePath!) is null)
            Assert.Skip("kotlinx-coroutines-core-jvm.jar is not shipped with this kotlinc distribution.");
        Assert.Contains("kotlinx.coroutines: available", s.Notes);
        ExecutionResult r = await TestSupport.RunAsync(TestSupport.Request("kotlin", "Main.kt", CoroutineProgram), limits: TestSupport.RunLimits(seconds: 60));
        Assert.True(r.IsSuccess, $"{r.Status}: {r.CompileOutput}{r.Stderr} {r.Message}");
        Assert.Equal("fast\nslow\ndone", r.Stdout.Replace("\r\n", "\n").Trim());
    }

    [Fact]
    public async Task Kotlin_with_a_package_declaration_still_finds_its_main_class()
    {
        await TestSupport.RequireAsync("kotlin");
        const string code = "package demo.app\nfun main() { println(\"pkg\") }\n";
        ExecutionResult r = await TestSupport.RunAsync(TestSupport.Request("kotlin", "Main.kt", code), limits: TestSupport.RunLimits(seconds: 60));
        Assert.True(r.IsSuccess, $"{r.Status}: {r.CompileOutput}{r.Stderr} {r.Message}");
        Assert.Equal("pkg", r.Stdout.Trim());
    }

    private static bool TypesProvisioned =>
        File.Exists(Path.Combine(TestSupport.Options.CacheDirectory, "typescript-types", "node_modules", "@types", "node", "index.d.ts"));

    /// <summary>Provisions @types/node into the real runner cache (network). Run explicitly: dotnet test --filter Provision --explicit only.</summary>
    [Fact(Explicit = true)]
    public async Task Provision_node_types_into_runner_cache()
    {
        ProvisionResult r = await TypeScriptTypesProvisioner.ProvisionAsync(TestSupport.Options.CacheDirectory, CancellationToken.None);
        Assert.True(r.Success, r.Output);
    }

    [Fact]
    public async Task TypeScript_type_errors_are_reported_when_tsc_and_types_exist()
    {
        await TestSupport.RequireAsync("typescript");
        RuntimeStatus s = await TestSupport.StatusAsync("typescript");
        if (s.InstallHint is not null || !TypesProvisioned) Assert.Skip("tsc or @types/node (cache) not provisioned");
        ExecutionResult r = await TestSupport.RunAsync(TestSupport.Request("typescript", "main.ts", "const x: number = 'a';\n"));
        Assert.Equal(ExecutionStatus.CompileError, r.Status);
        Assert.Contains("TS2322", r.CompileOutput);
    }

    [Fact]
    public async Task TypeScript_without_provisioned_types_skips_checking_and_says_so()
    {
        string empty = Path.Combine(Path.GetTempPath(), "devacademy-empty-" + Guid.NewGuid().ToString("N"));
        try
        {
            ICodeRunner runner = ExecutionEngine.CreateDefault(new ExecutionOptions { CacheDirectory = empty });
            RuntimeStatus s = (await runner.DetectRuntimesAsync(CancellationToken.None)).Single(x => x.LanguageId == "typescript");
            if (s.Availability != RuntimeAvailability.Available) Assert.Skip("node not available");
            Assert.Contains("type-checking unavailable", s.Warning);

            // A type error must NOT fail the run; the program still executes and the result says checking was skipped.
            ExecutionResult r = await runner.RunOnceAsync(
                TestSupport.Request("typescript", "main.ts", "const x: number = 'a' as any;\nconsole.log('ran', x);\n"),
                null, TestSupport.RunLimits(), CancellationToken.None);
            Assert.True(r.IsSuccess, r.CompileOutput + r.Stderr + r.Message);
            Assert.Equal("ran a", r.Stdout.Trim());
            Assert.Contains("type-checking unavailable", r.Message);
        }
        finally { if (Directory.Exists(empty)) Directory.Delete(empty, true); }
    }

    [Theory]
    [InlineData("php")]
    [InlineData("rust")]
    [InlineData("kotlin")]
    [InlineData("go")]
    [InlineData("java")]
    [InlineData("cpp")]
    [InlineData("csharp")]
    [InlineData("python")]
    [InlineData("javascript")]
    [InlineData("typescript")]
    public async Task Missing_runtime_yields_RuntimeMissing_with_hint(string language)
    {
        // Simulates a machine without any tools: lookups see no directories at all.
        using IDisposable none = ToolLocator.OverrideSearchDirs([]);
        ICodeRunner runner = ExecutionEngine.CreateDefault();
        RuntimeStatus s = (await runner.DetectRuntimesAsync(CancellationToken.None)).Single(x => x.LanguageId == language);
        Assert.Equal(RuntimeAvailability.Missing, s.Availability);
        Assert.False(string.IsNullOrEmpty(s.InstallHint));

        string file = language switch { "php" => "main.php", "rust" => "main.rs", "kotlin" => "Main.kt", _ => "main.x" };
        ExecutionResult r = await runner.RunOnceAsync(TestSupport.Request(language, file, "x"), null, TestSupport.RunLimits(), CancellationToken.None);
        Assert.Equal(ExecutionStatus.RuntimeMissing, r.Status);
        Assert.Contains(s.InstallHint!, r.Message);
    }

    [Fact]
    public async Task Node_status_lists_package_managers()
    {
        await TestSupport.RequireAsync("javascript");
        Assert.Contains("Package managers: npm", (await TestSupport.StatusAsync("javascript")).Notes);
        Assert.Contains("Package managers:", (await TestSupport.StatusAsync("typescript")).Notes);
    }

    [Fact]
    public async Task Runtime_error_has_exit_code_and_stderr()
    {
        await TestSupport.RequireAsync("python");
        ExecutionResult r = await TestSupport.RunAsync(TestSupport.Request("python", "main.py", "import sys\nsys.stderr.write('boom')\nsys.exit(3)\n"));
        Assert.Equal(ExecutionStatus.RuntimeError, r.Status);
        Assert.Equal(3, r.ExitCode);
        Assert.Equal("boom", r.Stderr);
    }

    [Fact]
    public async Task Prepared_program_can_run_many_times_with_extra_environment()
    {
        await TestSupport.RequireAsync("python");
        await using IPreparedProgram p = await TestSupport.Runner.PrepareAsync(
            TestSupport.Request("python", "main.py", "import os,sys\nprint(os.environ.get('NONCE'), sys.stdin.read().strip())\n"), CancellationToken.None);
        Assert.Null(p.PreparationFailure);
        for (int i = 0; i < 3; i++)
        {
            ExecutionResult r = await p.RunAsync($"in{i}", TestSupport.RunLimits(), CancellationToken.None,
                new Dictionary<string, string> { ["NONCE"] = $"n{i}" });
            Assert.Equal($"n{i} in{i}", r.Stdout.Trim());
        }
    }

    [Fact]
    public async Task Python_harness_can_import_learner_module()
    {
        await TestSupport.RequireAsync("python");
        var req = new ExecutionRequest("python",
            [new SourceFile("solution.py", "def f(): return 41 + 1\n"), new SourceFile("pkg/test_main.py", "import sys; sys.path.insert(0, '.')\nimport solution\nprint(solution.f())\n")],
            "pkg/test_main.py", IsolationLevel.ProcessLimits);
        ExecutionResult r = await TestSupport.RunAsync(req);
        Assert.Equal("42", r.Stdout.Trim());
    }
}
