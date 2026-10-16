using DevAcademy.Execution;

namespace DevAcademy.Execution.Tests;

public class DevServerUrlDetectorTests
{
    private static string Log(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "devserver", name + ".log"));

    // Logs captured from real dev servers running in a pseudo-terminal (colors and cursor codes included).
    [Theory]
    [InlineData("vite", "http://localhost:5173/", 5173)]
    [InlineData("express", "http://localhost:3000", 3000)]
    [InlineData("nest", "http://localhost:3000", 3000)]
    [InlineData("next", "http://localhost:3000", 3000)]
    public void Real_captured_logs_yield_the_local_url(string log, string url, int port)
    {
        var text = Log(log);
        Assert.Contains("\u001B", text, StringComparison.Ordinal); // fixtures are raw terminal output
        var found = DevServerUrlDetector.Find(text);
        Assert.Equal(new DevServerEndpoint(url, port), found);
    }

    [Fact]
    public void Network_urls_are_not_mistaken_for_the_local_one()
    {
        // The real Next log has "Network: http://<lan ip>:3000" right after "Local:".
        var found = DevServerUrlDetector.Find(Log("next"));
        Assert.Equal("http://localhost:3000", found!.Url);
        Assert.Null(DevServerUrlDetector.Find("  Network: http://192.168.1.10:3000\n"));
    }

    [Theory]
    [InlineData("Listening on http://127.0.0.1:8080\n", "http://127.0.0.1:8080", 8080)]
    [InlineData("started at http://0.0.0.0:4000/\n", "http://localhost:4000/", 4000)]
    [InlineData("  ➜  Local:   http://[::1]:5174/app/\n", "http://[::1]:5174/app/", 5174)]
    [InlineData("ready - started server on http://localhost:3001, url: http://localhost:3001\n", "http://localhost:3001", 3001)]
    public void Common_hosts_and_forms_are_recognised(string line, string url, int port) =>
        Assert.Equal(new DevServerEndpoint(url, port), DevServerUrlDetector.Find(line));

    [Fact]
    public void Ansi_between_host_and_port_is_stripped()
    {
        var line = "  \u001B[32m➜\u001B[39m  \u001B[1mLocal\u001B[22m:   \u001B[36mhttp://localhost:\u001B[1m5173\u001B[22m/\u001B[39m\r\n";
        Assert.Equal(5173, DevServerUrlDetector.Find(line)!.Port);
    }

    [Fact]
    public void Streaming_chunks_wait_for_a_complete_line_so_a_split_port_is_never_reported()
    {
        var d = new DevServerUrlDetector();
        Assert.Null(d.Feed("  Local:   http://localhost:51"));
        Assert.Null(d.Feed("73"));
        var e = d.Feed("/\r\nnext line");
        Assert.Equal(new DevServerEndpoint("http://localhost:5173/", 5173), e);
        Assert.Null(d.Feed(" without url\n"));
    }

    [Fact]
    public void Replaying_a_real_log_in_small_chunks_finds_the_same_url()
    {
        var text = Log("vite");
        var d = new DevServerUrlDetector();
        DevServerEndpoint? found = null;
        for (var i = 0; i < text.Length; i += 7) found ??= d.Feed(text.Substring(i, Math.Min(7, text.Length - i)));
        Assert.Equal(5173, found!.Port);
    }

    [Theory]
    [InlineData("see https://example.com:443/docs for help\n")]
    [InlineData("port 5173 is in use\n")]
    [InlineData("http://localhost/ no port\n")]
    public void Unrelated_output_has_no_endpoint(string line) => Assert.Null(DevServerUrlDetector.Find(line));
}

public class JsProjectTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "da-js-" + Guid.NewGuid().ToString("N"));

    public JsProjectTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch (IOException) { } }

    private void Pkg(string json = """{ "scripts": { "dev": "vite", "test": "vitest run", "build": "vite build" } }""") =>
        File.WriteAllText(Path.Combine(_dir, "package.json"), json);

    private void Touch(string name) => File.WriteAllText(Path.Combine(_dir, name), "");

    [Fact]
    public void No_package_json_is_not_a_node_project() => Assert.Null(JsProjects.Detect(_dir));

    [Theory]
    [InlineData("package-lock.json", JsPackageManager.Npm, "npm")]
    [InlineData("npm-shrinkwrap.json", JsPackageManager.Npm, "npm")]
    [InlineData("pnpm-lock.yaml", JsPackageManager.Pnpm, "pnpm")]
    [InlineData("yarn.lock", JsPackageManager.Yarn, "yarn")]
    public void Manager_comes_from_the_lockfile(string lockfile, JsPackageManager manager, string name)
    {
        Pkg();
        Touch(lockfile);
        var p = JsProjects.Detect(_dir)!;
        Assert.Equal(manager, p.Manager);
        Assert.True(p.FromLockfile);
        Assert.Equal(name + " install", p.InstallCommand);
        Assert.Equal(name + " run dev", p.ScriptCommand("dev"));
    }

    [Fact]
    public void Without_a_lockfile_npm_is_the_fallback()
    {
        Pkg();
        var p = JsProjects.Detect(_dir)!;
        Assert.Equal(JsPackageManager.Npm, p.Manager);
        Assert.False(p.FromLockfile);
    }

    [Fact]
    public void Package_manager_field_is_used_only_when_there_is_no_lockfile()
    {
        Pkg("""{ "packageManager": "pnpm@9.1.0", "scripts": { "dev": "x" } }""");
        Assert.Equal(JsPackageManager.Pnpm, JsProjects.Detect(_dir)!.Manager);
        Touch("yarn.lock");
        Assert.Equal(JsPackageManager.Yarn, JsProjects.Detect(_dir)!.Manager);
    }

    [Fact]
    public void Lockfile_priority_is_pnpm_then_yarn_then_npm()
    {
        Pkg();
        Touch("package-lock.json");
        Touch("yarn.lock");
        Assert.Equal(JsPackageManager.Yarn, JsProjects.Detect(_dir)!.Manager);
        Touch("pnpm-lock.yaml");
        Assert.Equal(JsPackageManager.Pnpm, JsProjects.Detect(_dir)!.Manager);
    }

    [Fact]
    public void Scripts_are_read_and_dev_falls_back_to_start()
    {
        Pkg("""{ "scripts": { "start": "node s.js", "test": "t" } }""");
        var p = JsProjects.Detect(_dir)!;
        Assert.Equal("start", p.DevScript);
        Assert.True(p.HasScript("test"));
        Assert.False(p.HasScript("build"));
        Pkg("""{ "scripts": { "start": "s", "dev": "d" } }""");
        Assert.Equal("dev", JsProjects.Detect(_dir)!.DevScript);
        Pkg("{}");
        Assert.Null(JsProjects.Detect(_dir)!.DevScript);
    }

    [Fact]
    public void Broken_package_json_still_gives_a_project_without_scripts()
    {
        Pkg("{ not json");
        var p = JsProjects.Detect(_dir)!;
        Assert.Empty(p.Scripts);
        Assert.Equal(JsPackageManager.Npm, p.Manager);
    }
}
