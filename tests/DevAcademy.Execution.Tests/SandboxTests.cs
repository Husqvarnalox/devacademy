using System.Net;
using System.Net.Sockets;
using DevAcademy.Execution;

namespace DevAcademy.Execution.Tests;

public class SandboxTests
{
    [Fact]
    public async Task Mac_sandbox_blocks_network_and_outside_writes_but_allows_workdir_writes()
    {
        if (!OperatingSystem.IsMacOS()) Assert.Skip("macOS only");
        await TestSupport.RequireAsync("python");
        if (!await new MacSandboxExecSandbox(TestSupport.Options.CacheDirectory).IsAvailableAsync(CancellationToken.None))
            Assert.Skip("sandbox-exec unusable here");

        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        string outside = $"devacademy-outside-{Guid.NewGuid():N}.txt";
        string code = $$"""
            import os, socket
            def attempt(f):
                try:
                    f(); return 'ok'
                except OSError as e:
                    return 'denied'
            def connect():
                s = socket.create_connection(('127.0.0.1', {{port}}), timeout=3); s.close()
            def write_outside():
                open(os.path.join(os.environ['HOME'], '..', '{{outside}}'), 'w').write('x')
            def write_inside():
                open('inside.txt', 'w').write('x'); open(os.path.join(os.environ['TMPDIR'], 't'), 'w').write('x')
            print(attempt(connect), attempt(write_outside), attempt(write_inside))
            """;
        ExecutionResult r = await TestSupport.RunAsync(TestSupport.Request("python", "main.py", code, IsolationLevel.OsSandbox));
        try
        {
            Assert.Equal(IsolationLevel.OsSandbox, r.AppliedIsolation);
            Assert.Equal("denied denied ok", r.Stdout.Trim());
        }
        finally
        {
            File.Delete(Path.Combine(Path.GetTempPath(), outside));
        }
    }

    [Fact]
    public async Task Same_program_can_connect_without_sandbox()
    {
        await TestSupport.RequireAsync("python");
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        ExecutionResult r = await TestSupport.RunAsync(TestSupport.Request("python", "main.py",
            $"import socket\nsocket.create_connection(('127.0.0.1', {port}), timeout=3).close()\nprint('connected')\n"));
        Assert.Equal("connected", r.Stdout.Trim());
    }

    [Theory]
    [InlineData("go", "main.go", "package main\nimport \"fmt\"\nfunc main() { fmt.Println(\"ok\") }\n")]
    [InlineData("java", "Main.java", "public class Main { public static void main(String[] a) { System.out.println(\"ok\"); } }\n")]
    [InlineData("cpp", "main.cpp", "#include <iostream>\nint main() { std::cout << \"ok\\n\"; }\n")]
    [InlineData("csharp", "Program.cs", "Console.WriteLine(\"ok\");\n")]
    [InlineData("javascript", "main.js", "console.log('ok')\n")]
    [InlineData("typescript", "main.ts", "const x: string = 'ok'; console.log(x);\n")]
    [InlineData("rust", "main.rs", "fn main() { println!(\"ok\"); }\n")]
    public async Task Toolchains_work_under_OsSandbox(string language, string file, string code)
    {
        if (!OperatingSystem.IsMacOS()) Assert.Skip("macOS sandbox-exec profile verification");
        await TestSupport.RequireAsync(language);
        ExecutionResult r = await TestSupport.RunAsync(TestSupport.Request(language, file, code, IsolationLevel.OsSandbox));
        Assert.True(r.IsSuccess, $"{r.Status}: {r.CompileOutput}{r.Stderr} {r.Message}");
        Assert.Equal(IsolationLevel.OsSandbox, r.AppliedIsolation);
        Assert.Equal("ok", r.Stdout.Trim());
    }

    [Fact]
    public void Profile_escapes_quotes_and_uses_realpaths()
    {
        string profile = MacSandboxExecSandbox.BuildProfile("/tmp/a\"b\\c", "/tmp/cache", allowNetwork: false);
        Assert.Contains("(deny network*)", profile);
        Assert.Contains("(subpath \"/tmp/a\\\"b\\\\c\")", profile);
        Assert.Contains("(literal \"/dev/null\")", profile);
        Assert.DoesNotContain("(deny network*)", MacSandboxExecSandbox.BuildProfile("/tmp/a", "/tmp/c", allowNetwork: true));
        if (OperatingSystem.IsMacOS())
            Assert.Equal("/private/var", MacSandboxExecSandbox.RealPath("/var"));
    }

    [Fact]
    public void Bubblewrap_arguments_are_constructed_as_specified()
    {
        var args = LinuxBubblewrapSandbox.BuildArguments("/tmp/w", "/c", "/bin/sh", ["-c", "true"], allowNetwork: false);
        string line = string.Join(' ', args);
        Assert.Equal(
            "--ro-bind / / --dev /dev --proc /proc --tmpfs /tmp --bind /tmp/w /tmp/w --bind /c /c --unshare-all " +
            "--die-with-parent --new-session --chdir /tmp/w -- /bin/sh -c true", line);
        Assert.Contains("--share-net", LinuxBubblewrapSandbox.BuildArguments("/w", "/c", "x", [], allowNetwork: true));
    }

    [Fact]
    public async Task Unavailable_isolation_falls_back_and_reports_real_level()
    {
        await TestSupport.RequireAsync("go");
        // Go has no container image configured, so Container must degrade.
        ExecutionResult r = await TestSupport.RunAsync(TestSupport.Request("go", "main.go",
            "package main\nfunc main() { println(\"x\") }\n", IsolationLevel.Container));
        Assert.True(r.IsSuccess, r.CompileOutput + r.Message);
        Assert.NotEqual(IsolationLevel.Container, r.AppliedIsolation);
        Assert.Contains("Container isolation is not supported", r.Message);
    }

    [Fact]
    public async Task No_fallback_means_failure_instead_of_silent_downgrade()
    {
        await TestSupport.RequireAsync("go");
        ICodeRunner strict = ExecutionEngine.CreateDefault(new ExecutionOptions { AllowIsolationFallback = false, CacheDirectory = TestSupport.Options.CacheDirectory });
        ExecutionResult r = await strict.RunOnceAsync(TestSupport.Request("go", "main.go", "package main\nfunc main() {}\n", IsolationLevel.Container),
            null, TestSupport.RunLimits(), CancellationToken.None);
        Assert.Equal(ExecutionStatus.InternalError, r.Status);
    }

    private static async Task RequireDockerImageAsync(string image)
    {
        if (!await new DockerSandbox().IsAvailableAsync(CancellationToken.None)) Assert.Skip("Docker daemon not reachable");
        var psi = new System.Diagnostics.ProcessStartInfo("docker") { RedirectStandardOutput = true, RedirectStandardError = true };
        psi.ArgumentList.Add("image"); psi.ArgumentList.Add("inspect"); psi.ArgumentList.Add(image);
        using var p = System.Diagnostics.Process.Start(psi)!;
        await p.WaitForExitAsync();
        if (p.ExitCode != 0) Assert.Skip($"Docker image {image} not pulled");
    }

    [Fact]
    public async Task Docker_python_runs_without_network_and_with_readonly_rootfs()
    {
        await RequireDockerImageAsync("python:3.13-slim");
        string code = """
            import socket, sys
            print('hello ' + sys.stdin.readline().strip())
            try:
                socket.create_connection(('1.1.1.1', 80), timeout=2); print('net')
            except OSError:
                print('nonet')
            try:
                open('/etc/x', 'w'); print('rw')
            except OSError:
                print('ro')
            open('inside.txt', 'w').write('x'); print('wd-ok')
            """;
        ExecutionResult r = await TestSupport.RunAsync(TestSupport.Request("python", "main.py", code, IsolationLevel.Container), "docker\n");
        Assert.Equal(IsolationLevel.Container, r.AppliedIsolation);
        Assert.True(r.IsSuccess, r.Stderr + r.Message);
        Assert.Equal(["hello docker", "nonet", "ro", "wd-ok"], r.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public async Task Docker_timeout_kills_the_container_and_memory_limit_applies()
    {
        await RequireDockerImageAsync("python:3.13-slim");
        ExecutionResult t = await TestSupport.RunAsync(TestSupport.Request("python", "main.py", "while True: pass\n", IsolationLevel.Container),
            limits: TestSupport.RunLimits(seconds: 3));
        Assert.Equal(ExecutionStatus.Timeout, t.Status);
        Assert.Equal(IsolationLevel.Container, t.AppliedIsolation);

        ExecutionResult m = await TestSupport.RunAsync(
            TestSupport.Request("python", "main.py", "x = b'a' * (1024 ** 3)\n", IsolationLevel.Container),
            limits: TestSupport.RunLimits(seconds: 20, memoryMb: 128));
        Assert.Equal(ExecutionStatus.MemoryLimitExceeded, m.Status);
    }
}
