using System.Text.Json;
using DevAcademy.LanguageServices.Hosting;
using DevAcademy.LanguageServices.JsonRpc;
using DevAcademy.LanguageServices.Protocol;
using DevAcademy.LanguageServices.Servers;
using DevAcademy.LanguageServices.Workspace;

namespace DevAcademy.LanguageServices.Tests;

public class ClientTests
{
    private static async Task<(LspClient client, FakeServer server)> ConnectAsync(object? caps = null)
    {
        var server = new FakeServer();
        if (caps is not null) server.Capabilities = caps;
        server.Record("initialized", "textDocument/didOpen", "textDocument/didChange", "textDocument/didSave", "textDocument/didClose");
        var rpc = new JsonRpcConnection(server.Output, server.Input);
        var client = new LspClient(rpc, FileUri.FromPath(TestEnv.TempDir("root")));
        await client.InitializeAsync(null, CancellationToken.None);
        return (client, server);
    }

    [Fact]
    public async Task Initialize_parses_capabilities_server_info_and_sends_initialized()
    {
        var (client, server) = await ConnectAsync();
        Assert.Equal("fake", client.Capabilities.ServerName);
        Assert.Equal("9.9", client.Capabilities.ServerVersion);
        Assert.Equal(2, client.Capabilities.TextDocumentSyncKind);
        Assert.Equal(["."], client.Capabilities.CompletionTriggerCharacters);
        Assert.True(client.Capabilities.CompletionResolve);
        Assert.Equal(["(", ","], client.Capabilities.SignatureTriggerCharacters);
        await server.WaitForAsync(() => server.Methods.Contains("initialized"));
        await client.DisposeAsync();
    }

    [Fact]
    public async Task Position_encoding_is_negotiated_and_defaults_to_utf16()
    {
        var (c1, _) = await ConnectAsync(new { positionEncoding = "utf-8", textDocumentSync = new { change = 2, save = true } });
        Assert.Equal(PositionEncoding.Utf8, c1.Encoding);
        Assert.True(c1.Capabilities.SupportsSave);
        var (c2, _) = await ConnectAsync(new { textDocumentSync = 1 });
        Assert.Equal(PositionEncoding.Utf16, c2.Encoding);
        Assert.Equal(1, c2.Capabilities.TextDocumentSyncKind);
        Assert.False(c2.Capabilities.Completion);
    }

    [Fact]
    public async Task Client_requests_declare_what_the_editor_supports()
    {
        var server = new FakeServer();
        JsonElement? seen = null;
        server.Rpc.OnRequest("initialize", (p, _) =>
        {
            seen = p;
            return Task.FromResult<object?>(new { capabilities = new { } });
        });
        var client = new LspClient(new JsonRpcConnection(server.Output, server.Input), FileUri.FromPath(TestEnv.TempDir("r")));
        await client.InitializeAsync(new { foo = "bar" }, CancellationToken.None);
        var p = seen!.Value;
        var caps = p.GetProperty("capabilities");
        Assert.False(caps.GetProperty("textDocument").GetProperty("completion").GetProperty("completionItem").GetProperty("snippetSupport").GetBoolean());
        Assert.True(caps.GetProperty("textDocument").GetProperty("publishDiagnostics").GetProperty("versionSupport").GetBoolean());
        Assert.Contains("markdown", caps.GetProperty("textDocument").GetProperty("hover").GetProperty("contentFormat").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal("utf-16", caps.GetProperty("general").GetProperty("positionEncodings")[0].GetString());
        Assert.Equal("bar", p.GetProperty("initializationOptions").GetProperty("foo").GetString());
        Assert.Equal(Environment.ProcessId, p.GetProperty("processId").GetInt32());
    }

    [Fact]
    public async Task Server_requests_are_answered_properly()
    {
        var (client, server) = await ConnectAsync();
        var cfg = await server.Rpc.RequestAsync("workspace/configuration", new { items = new object[] { new { section = "a" }, new { section = "b" } } });
        Assert.Equal(2, cfg.GetArrayLength());
        var progress = await server.Rpc.RequestAsync("window/workDoneProgress/create", new { token = "t" });
        Assert.Equal(JsonValueKind.Null, progress.ValueKind);
        var reg = await server.Rpc.RequestAsync("client/registerCapability", new { registrations = Array.Empty<object>() });
        Assert.Equal(JsonValueKind.Null, reg.ValueKind);
        var folders = await server.Rpc.RequestAsync("workspace/workspaceFolders", null);
        Assert.Equal(client.RootUri, folders[0].GetProperty("uri").GetString());
        var ex = await Assert.ThrowsAsync<RpcException>(() => server.Rpc.RequestAsync("unknown/thing", null));
        Assert.Equal(RpcException.MethodNotFound, ex.Code);
    }

    [Fact]
    public async Task Diagnostics_stream_parses_ranges_severity_and_codes()
    {
        var (client, server) = await ConnectAsync();
        var got = new TaskCompletionSource<PublishDiagnosticsParams>();
        client.Diagnostics += d => got.TrySetResult(d);
        await server.Rpc.NotifyAsync("textDocument/publishDiagnostics", new
        {
            uri = "file:///x/a.go",
            version = 3,
            diagnostics = new object[]
            {
                new { range = new { start = new { line = 1, character = 2 }, end = new { line = 1, character = 5 } }, severity = 1, code = "UndeclaredName", source = "compiler", message = "undefined: foo" },
                new { range = new { start = new { line = 0, character = 0 }, end = new { line = 0, character = 1 } }, message = "no severity", code = 7 },
            },
        });
        var d = await got.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(3, d.Version);
        Assert.Equal(new Position(1, 2), d.Diagnostics[0].Range.Start);
        Assert.Equal(DiagnosticSeverity.Error, d.Diagnostics[0].EffectiveSeverity);
        Assert.Equal("UndeclaredName", d.Diagnostics[0].CodeText);
        Assert.Equal(DiagnosticSeverity.Error, d.Diagnostics[1].EffectiveSeverity); // absent severity: treat as error
        Assert.Equal("7", d.Diagnostics[1].CodeText);
    }

    [Fact]
    public async Task Completion_accepts_array_list_and_null_and_hover_flattens_markup()
    {
        var (client, server) = await ConnectAsync();
        int n = 0;
        server.Rpc.OnRequest("textDocument/completion", (_, _) => Task.FromResult<object?>(n++ switch
        {
            0 => new object[] { new { label = "Println", kind = 3, detail = "func" } },
            1 => new { isIncomplete = true, items = new object[] { new { label = "Printf", textEdit = new { range = new { start = new { line = 0, character = 4 }, end = new { line = 0, character = 6 } }, newText = "Printf" } } } },
            _ => null,
        }));
        server.Rpc.OnRequest("textDocument/hover", (_, _) => Task.FromResult<object?>(new { contents = new { kind = "markdown", value = "**bold**" } }));
        var pos = new Position(0, 0);
        var l1 = await client.CompletionAsync("file:///a", pos, null, default);
        Assert.Equal("Println", l1!.Items[0].Label);
        Assert.Equal("Println", l1.Items[0].TextToInsert);
        var l2 = await client.CompletionAsync("file:///a", pos, ".", default);
        Assert.True(l2!.IsIncomplete);
        Assert.Equal(("Printf", new Position(0, 6)), (l2.Items[0].PrimaryEdit()!.Value.NewText, l2.Items[0].PrimaryEdit()!.Value.Range.End));
        Assert.Null(await client.CompletionAsync("file:///a", pos, null, default));
        var hover = await client.HoverAsync("file:///a", pos, default);
        Assert.Equal(("**bold**", true), LspText.Flatten(hover!.Contents));
    }

    [Fact]
    public void Text_flattening_and_location_parsing_handle_every_wire_shape()
    {
        static JsonElement J(string s) => JsonDocument.Parse(s).RootElement.Clone();
        Assert.Equal(("plain", false), LspText.Flatten(J("\"plain\"")));
        Assert.Equal(("```go\nfunc f()\n```", true), LspText.Flatten(J("{\"language\":\"go\",\"value\":\"func f()\"}")));
        Assert.Equal(("a\n\nb", false), LspText.Flatten(J("[\"a\",\"b\"]")));
        const string range = "{\"start\":{\"line\":1,\"character\":0},\"end\":{\"line\":1,\"character\":3}}";
        Assert.Single(LspClient.ParseLocations(J($"{{\"uri\":\"file:///a.go\",\"range\":{range}}}")));
        Assert.Equal(2, LspClient.ParseLocations(J($"[{{\"uri\":\"file:///a.go\",\"range\":{range}}},{{\"targetUri\":\"file:///b.go\",\"targetRange\":{range},\"targetSelectionRange\":{range}}}]")).Count);
        Assert.Empty(LspClient.ParseLocations(J("null")));
    }

    [Fact]
    public async Task Shutdown_sends_shutdown_then_exit()
    {
        var (client, server) = await ConnectAsync();
        Assert.True(await client.ShutdownAsync(TimeSpan.FromSeconds(5)));
        await server.WaitForAsync(() => server.ExitSeen);
        Assert.True(server.ShutdownSeen);
    }
}

public class ManagerTests
{
    private static async Task<bool> WaitAsync(Func<bool> c, int ms = 10000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!c()) { if (sw.ElapsedMilliseconds > ms) return false; await Task.Delay(10); }
        return true;
    }

    [Fact]
    public async Task Server_starts_lazily_is_shared_between_documents_and_receives_incremental_changes()
    {
        var launcher = new FakeLauncher(() => { var s = new FakeServer(); s.Record("textDocument/didOpen", "textDocument/didChange", "textDocument/didClose", "textDocument/didSave"); return s; });
        await using var mgr = TestEnv.FakeManager(launcher);
        string root = TestEnv.TempDir("p");
        Assert.Empty(launcher.Launched); // nothing before the first document

        string text = "hello";
        var d1 = mgr.OpenDocument("fakelang", Path.Combine(root, "a.fk"), root, () => text);
        var d2 = mgr.OpenDocument("fakelang", Path.Combine(root, "b.fk"), root, () => "other");
        Assert.NotNull(d1);
        Assert.True(await WaitAsync(() => d1!.IsOpenOnServer && d2!.IsOpenOnServer));
        Assert.Single(launcher.Launched);                      // one process for the root, shared
        var server = launcher.Launched[0];
        await server.WaitForAsync(() => server.Methods.Count(m => m == "textDocument/didOpen") == 2);

        text = "hello😀";
        d1!.ApplyEdit(new StringTextBuffer(text), 5, "", "😀");
        await server.WaitForAsync(() => server.Methods.Contains("textDocument/didChange"));
        var p = server.Params[server.Methods.IndexOf("textDocument/didChange")];
        Assert.Equal(2, p.GetProperty("textDocument").GetProperty("version").GetInt32());
        var ch = p.GetProperty("contentChanges")[0];
        Assert.Equal(5, ch.GetProperty("range").GetProperty("start").GetProperty("character").GetInt32());
        Assert.Equal("😀", ch.GetProperty("text").GetString());
        Assert.Equal("fake", mgr.GetStatus("fakelang", root).DisplayName == "Fake LS" ? "fake" : "?");
        Assert.Equal("9.9", mgr.GetStatus("fakelang", root).Version);
    }

    [Fact]
    public async Task Full_sync_servers_get_the_whole_text()
    {
        var launcher = new FakeLauncher(() =>
        {
            var s = new FakeServer { Capabilities = new { textDocumentSync = 1 } };
            s.Record("textDocument/didOpen", "textDocument/didChange");
            return s;
        });
        await using var mgr = TestEnv.FakeManager(launcher);
        string root = TestEnv.TempDir("p"), text = "v1";
        var d = mgr.OpenDocument("fakelang", Path.Combine(root, "a.fk"), root, () => text)!;
        Assert.True(await WaitAsync(() => d.IsOpenOnServer));
        text = "v2 longer";
        d.ApplyEdit(new StringTextBuffer(text), 1, "1", "2 longer");
        var s = launcher.Launched[0];
        await s.WaitForAsync(() => s.Methods.Contains("textDocument/didChange"));
        var change = s.Params[s.Methods.IndexOf("textDocument/didChange")].GetProperty("contentChanges")[0];
        Assert.False(change.TryGetProperty("range", out _));
        Assert.Equal("v2 longer", change.GetProperty("text").GetString());
    }

    [Fact]
    public async Task Diagnostics_are_routed_to_the_document_and_stale_versions_are_dropped()
    {
        var launcher = new FakeLauncher();
        await using var mgr = TestEnv.FakeManager(launcher);
        string root = TestEnv.TempDir("p"), file = Path.Combine(root, "a.fk");
        var d = mgr.OpenDocument("fakelang", file, root, () => "x")!;
        Assert.True(await WaitAsync(() => d.IsOpenOnServer));
        var server = launcher.Launched[0];
        object Diag(string msg) => new { range = new { start = new { line = 0, character = 0 }, end = new { line = 0, character = 1 } }, severity = 2, message = msg };
        await server.Rpc.NotifyAsync("textDocument/publishDiagnostics", new { uri = d.Uri, version = 1, diagnostics = new[] { Diag("first") } });
        Assert.True(await WaitAsync(() => d.Diagnostics.Count == 1));
        d.ApplyEdit(new StringTextBuffer("xy"), 1, "", "y"); // version 2
        await server.Rpc.NotifyAsync("textDocument/publishDiagnostics", new { uri = d.Uri, version = 1, diagnostics = new[] { Diag("stale"), Diag("stale2") } });
        await server.Rpc.NotifyAsync("textDocument/publishDiagnostics", new { uri = d.Uri, version = 2, diagnostics = new[] { Diag("fresh"), Diag("fresh2"), Diag("fresh3") } });
        Assert.True(await WaitAsync(() => d.Diagnostics.Count == 3));
        Assert.All(d.Diagnostics, x => Assert.StartsWith("fresh", x.Message));
    }

    [Fact]
    public async Task Crash_restarts_with_backoff_reopens_documents_and_shows_status()
    {
        var launcher = new FakeLauncher();
        var statuses = new List<ServerState>();
        await using var mgr = TestEnv.FakeManager(launcher);
        mgr.StatusChanged += s => { lock (statuses) statuses.Add(s.State); };
        string root = TestEnv.TempDir("p"), text = "content";
        var d = mgr.OpenDocument("fakelang", Path.Combine(root, "a.fk"), root, () => text)!;
        Assert.True(await WaitAsync(() => d.IsOpenOnServer));
        launcher.Launched[0].Kill();                                   // crash
        Assert.True(await WaitAsync(() => launcher.Launched.Count == 2 && d.IsOpenOnServer));
        Assert.Equal(ServerState.Ready, mgr.GetStatus("fakelang", root).State);
        Assert.Equal(1, mgr.GetStatus("fakelang", root).RestartCount);
        lock (statuses) Assert.Contains(ServerState.Restarting, statuses);
        Assert.Equal(1, d.Version);                                     // re-opened fresh on the new process
    }

    [Fact]
    public async Task After_max_restarts_the_status_is_an_error_and_it_stops_trying()
    {
        var launcher = new FakeLauncher();
        launcher.Factory = () => { var s = new FakeServer(); _ = Task.Run(async () => { await Task.Delay(30); s.Kill(); }); return s; };
        await using var mgr = TestEnv.FakeManager(launcher, maxRestarts: 3);
        string root = TestEnv.TempDir("p");
        mgr.OpenDocument("fakelang", Path.Combine(root, "a.fk"), root, () => "x");
        Assert.True(await WaitAsync(() => mgr.GetStatus("fakelang", root).State == ServerState.Error));
        int launches = launcher.Launched.Count;
        Assert.Equal(4, launches);                                      // first start + 3 restarts
        await Task.Delay(200);
        Assert.Equal(launches, launcher.Launched.Count);
        Assert.Contains("crashed", mgr.GetStatus("fakelang", root).Message);
    }

    [Fact]
    public async Task Failing_initialize_is_reported_not_thrown()
    {
        var launcher = new FakeLauncher(() =>
        {
            var s = new FakeServer();
            s.Rpc.OnRequest("initialize", (_, _) => throw new InvalidOperationException("no license"));
            return s;
        });
        await using var mgr = TestEnv.FakeManager(launcher, maxRestarts: 0);
        string root = TestEnv.TempDir("p");
        mgr.OpenDocument("fakelang", Path.Combine(root, "a.fk"), root, () => "x");
        Assert.True(await WaitAsync(() => mgr.GetStatus("fakelang", root).State == ServerState.Error));
        Assert.Contains("no license", mgr.GetStatus("fakelang", root).Message);
    }

    [Fact]
    public async Task Idle_session_is_stopped_after_the_last_document_closes_and_stop_is_graceful()
    {
        var launcher = new FakeLauncher();
        await using var mgr = TestEnv.FakeManager(launcher, idle: TimeSpan.FromMilliseconds(100));
        string root = TestEnv.TempDir("p");
        var d = mgr.OpenDocument("fakelang", Path.Combine(root, "a.fk"), root, () => "x")!;
        Assert.True(await WaitAsync(() => d.IsOpenOnServer));
        var server = launcher.Launched[0];
        d.Dispose();
        Assert.True(await WaitAsync(() => server.ExitSeen));              // shutdown + exit were sent
        Assert.True(server.ShutdownSeen);
        Assert.Empty(mgr.ActiveSessions);
        // a new document starts a fresh process
        var d2 = mgr.OpenDocument("fakelang", Path.Combine(root, "a.fk"), root, () => "x")!;
        Assert.True(await WaitAsync(() => d2.IsOpenOnServer));
        Assert.Equal(2, launcher.Launched.Count);
    }

    [Fact]
    public async Task Reopening_before_the_idle_timeout_keeps_the_process()
    {
        var launcher = new FakeLauncher();
        await using var mgr = TestEnv.FakeManager(launcher, idle: TimeSpan.FromMilliseconds(300));
        string root = TestEnv.TempDir("p");
        var d = mgr.OpenDocument("fakelang", Path.Combine(root, "a.fk"), root, () => "x")!;
        Assert.True(await WaitAsync(() => d.IsOpenOnServer));
        d.Dispose();
        var d2 = mgr.OpenDocument("fakelang", Path.Combine(root, "b.fk"), root, () => "y")!;
        await Task.Delay(600);
        Assert.Single(launcher.Launched);
        Assert.True(d2.IsOpenOnServer);
    }

    [Fact]
    public async Task Missing_server_override_and_disabled_state()
    {
        var def = new LanguageServerDefinition("nonexistent", "Nope", ["zz"], [new ServerCommand("definitely-not-installed-xyz", [])], [], null, [], "install me");
        bool enabled = true;
        await using var mgr = new LanguageServerManager(new LanguageServerManagerOptions
        {
            Definitions = [def],
            Locator = new ServerLocator(null, []),
            IsEnabled = () => enabled,
        });
        string root = TestEnv.TempDir("p");
        Assert.Null(mgr.OpenDocument("zz", Path.Combine(root, "a.zz"), root, () => ""));
        var st = mgr.GetStatus("zz", root);
        Assert.Equal(ServerState.NotInstalled, st.State);
        Assert.Equal("install me", st.InstallHint);
        enabled = false;
        Assert.Equal(ServerState.Disabled, mgr.GetStatus("zz", root).State);
        Assert.Equal(ServerState.NotInstalled, mgr.GetStatus("unknownlang", root).State);
    }

    [Fact]
    public async Task Different_roots_get_separate_processes()
    {
        var launcher = new FakeLauncher();
        await using var mgr = TestEnv.FakeManager(launcher);
        string r1 = TestEnv.TempDir("a"), r2 = TestEnv.TempDir("b");
        var d1 = mgr.OpenDocument("fakelang", Path.Combine(r1, "x.fk"), r1, () => "")!;
        var d2 = mgr.OpenDocument("go", Path.Combine(r2, "x.fk"), r2, () => "")!;
        Assert.True(await WaitAsync(() => d1.IsOpenOnServer && d2.IsOpenOnServer));
        Assert.Equal(2, launcher.Launched.Count);
    }
}

public class ServerRegistryTests
{
    [Fact]
    public void Defaults_cover_the_requested_languages()
    {
        foreach (string lang in new[] { "cpp", "go", "python", "typescript", "javascript", "rust", "java", "csharp", "php", "kotlin" })
            Assert.NotNull(LanguageServerRegistry.ForLanguage(lang));
        Assert.Contains("--background-index=false", LanguageServerRegistry.ForLanguage("cpp")!.Candidates[0].Args);
        Assert.Contains("--stdio", LanguageServerRegistry.ForLanguage("python")!.Candidates[0].Args);
        Assert.Equal(["typescript-language-server", "typescript"], LanguageServerRegistry.ForLanguage("typescript")!.NpmPackages);
        Assert.False(LanguageServerRegistry.ForLanguage("go")!.IsNpmInstallable);
    }

    [Theory]
    [InlineData("a.go", "go")]
    [InlineData("x/y.tsx", "typescript")]
    [InlineData("m.mjs", "javascript")]
    [InlineData("m.hpp", "cpp")]
    [InlineData("README.md", null)]
    public void Extension_to_language(string path, string? lang) => Assert.Equal(lang, LanguageServerRegistry.LanguageForExtension(path));

    [Fact]
    public void Lsp_language_ids_are_specific()
    {
        Assert.Equal("typescriptreact", LanguageServerRegistry.LspLanguageId("typescript", "a.tsx"));
        Assert.Equal("c", LanguageServerRegistry.LspLanguageId("cpp", "a.c"));
        Assert.Equal("cpp", LanguageServerRegistry.LspLanguageId("cpp", "a.cpp"));
    }

    [Fact]
    public void Root_is_found_by_markers_between_the_file_and_the_project_boundary()
    {
        string project = TestEnv.TempDir("proj");
        Directory.CreateDirectory(Path.Combine(project, "svc", "internal"));
        File.WriteAllText(Path.Combine(project, "svc", "go.mod"), "module x");
        var go = LanguageServerRegistry.ForLanguage("go")!;
        Assert.Equal(Path.Combine(project, "svc"), LanguageServerRegistry.FindRoot(Path.Combine(project, "svc", "internal", "a.go"), project, go));
        Assert.Equal(project, LanguageServerRegistry.FindRoot(Path.Combine(project, "other.go"), project, go));
        var cs = LanguageServerRegistry.ForLanguage("csharp")!;
        File.WriteAllText(Path.Combine(project, "svc", "App.csproj"), "<Project/>");
        Assert.Equal(Path.Combine(project, "svc"), LanguageServerRegistry.FindRoot(Path.Combine(project, "svc", "internal", "a.cs"), project, cs));
    }

    [Fact]
    public void Locator_prefers_override_then_runner_cache_then_system()
    {
        string cache = TestEnv.TempDir("cache"), sys = TestEnv.TempDir("sys");
        string bin = ServerLocator.NpmBinDirectory(cache);
        Directory.CreateDirectory(bin);
        var py = LanguageServerRegistry.ForLanguage("python")!;
        var loc = new ServerLocator(cache, [sys]);
        Assert.Null(loc.Resolve(py));
        File.WriteAllText(Path.Combine(sys, "basedpyright-langserver"), "");
        Assert.Equal("basedpyright-langserver", Path.GetFileName(loc.Resolve(py)!.Executable));  // second candidate
        File.WriteAllText(Path.Combine(bin, "pyright-langserver"), "");
        var r = loc.Resolve(py)!;
        Assert.Equal(ServerSource.RunnerCache, r.Source);
        Assert.Equal(["--stdio"], r.Args);

        string custom = Path.Combine(sys, "my pyright");
        File.WriteAllText(custom, "");
        var o = loc.Resolve(py, $"\"{custom}\" --stdio --verbose")!;
        Assert.Equal(ServerSource.UserOverride, o.Source);
        Assert.Equal(["--stdio", "--verbose"], o.Args);
        Assert.Null(loc.Resolve(py, "/nonexistent/binary --x"));
    }

    [Fact]
    public void Command_line_splitting_respects_quotes()
    {
        Assert.Equal(["a", "b c", "d"], CommandLine.Split("a 'b c'   d"));
        Assert.Equal(["x", ""], CommandLine.Split("x \"\""));
        Assert.Empty(CommandLine.Split("   "));
    }

    [Fact]
    public void Npm_install_plan_shows_the_exact_command_and_requires_npm()
    {
        string cache = TestEnv.TempDir("cache"), sys = TestEnv.TempDir("sys");
        var ts = LanguageServerRegistry.ForLanguage("typescript")!;
        Assert.Null(NpmServerProvisioner.Plan(ts, cache, new ServerLocator(cache, [sys])));       // no npm
        File.WriteAllText(Path.Combine(sys, OperatingSystem.IsWindows() ? "npm.cmd" : "npm"), "");
        var plan = NpmServerProvisioner.Plan(ts, cache, new ServerLocator(cache, [sys]))!;
        Assert.Equal(["install", "--no-audit", "--no-fund", "--prefix", ServerLocator.InstallDirectory(cache), "typescript-language-server", "typescript"], plan.Args);
        Assert.Contains("--prefix", plan.Display);
        Assert.False(Directory.Exists(ServerLocator.InstallDirectory(cache)));                    // planning downloads/creates nothing
        Assert.Null(NpmServerProvisioner.Plan(LanguageServerRegistry.ForLanguage("go")!, cache, new ServerLocator(cache, [sys])));
    }
}

public class WorkspaceMirrorTests
{
    [Fact]
    public async Task Prepare_writes_files_and_scaffolding_and_removes_stale_files()
    {
        string baseDir = TestEnv.TempDir("lsp-ws");
        await using var m = new WorkspaceMirror(baseDir, "ex/../1", "go");
        Assert.StartsWith(baseDir, m.Root);
        Assert.Equal(baseDir, Path.GetDirectoryName(Path.GetDirectoryName(m.Root)));
        await m.PrepareAsync([("main.go", "package main\n"), ("util/u.go", "package util\n")]);
        Assert.True(File.Exists(Path.Combine(m.Root, "go.mod")));
        Assert.True(File.Exists(Path.Combine(m.Root, "util", "u.go")));
        await m.PrepareAsync([("main.go", "package main // v2\n")]);
        Assert.False(File.Exists(Path.Combine(m.Root, "util", "u.go")));
        Assert.Equal("package main // v2\n", File.ReadAllText(Path.Combine(m.Root, "main.go")));
        Assert.Equal("main.go", m.RelativeOf(m.PathFor("main.go")));
        Assert.Null(m.RelativeOf(Path.Combine(baseDir, "elsewhere.go")));
    }

    [Fact]
    public async Task Exercise_supplied_scaffold_file_wins_and_clangd_gets_cpp20_flags()
    {
        string baseDir = TestEnv.TempDir("lsp-ws");
        await using var go = new WorkspaceMirror(baseDir, "e", "go");
        await go.PrepareAsync([("go.mod", "module custom\n\ngo 1.21\n"), ("main.go", "package main\n")]);
        Assert.Contains("module custom", File.ReadAllText(Path.Combine(go.Root, "go.mod")));
        await using var cpp = new WorkspaceMirror(baseDir, "e", "cpp");
        await cpp.PrepareAsync([("main.cpp", "int main(){}\n")]);
        Assert.Contains("-std=c++20", File.ReadAllText(Path.Combine(cpp.Root, "compile_flags.txt")));
    }

    [Fact]
    public async Task Changes_are_debounced_and_flush_forces_them()
    {
        string baseDir = TestEnv.TempDir("lsp-ws");
        await using var m = new WorkspaceMirror(baseDir, "e", "go", TimeSpan.FromMilliseconds(80));
        await m.PrepareAsync([("main.go", "v0")]);
        for (int i = 1; i <= 5; i++) m.Schedule("main.go", "v" + i);
        Assert.Equal("v0", File.ReadAllText(Path.Combine(m.Root, "main.go")));          // not yet
        await Task.Delay(400);
        Assert.Equal("v5", File.ReadAllText(Path.Combine(m.Root, "main.go")));
        m.Schedule("main.go", "v6");
        await m.FlushAsync();
        Assert.Equal("v6", File.ReadAllText(Path.Combine(m.Root, "main.go")));
    }

    [Fact]
    public async Task Paths_escaping_the_root_are_not_written()
    {
        string baseDir = TestEnv.TempDir("lsp-ws");
        await using var m = new WorkspaceMirror(baseDir, "e", "go");
        await m.PrepareAsync([("../../evil.go", "x"), ("ok.go", "y")]);
        Assert.False(File.Exists(Path.Combine(baseDir, "evil.go")));
        Assert.True(File.Exists(Path.Combine(m.Root, "ok.go")));
    }
}
