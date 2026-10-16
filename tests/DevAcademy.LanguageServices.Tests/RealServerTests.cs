using System.Diagnostics;
using DevAcademy.LanguageServices.Hosting;
using DevAcademy.LanguageServices.Protocol;
using DevAcademy.LanguageServices.Servers;
using DevAcademy.LanguageServices.Workspace;

namespace DevAcademy.LanguageServices.Tests;

/// <summary>Talks to the real gopls / clangd found on this machine; each test is skipped when its server is not installed.</summary>
public class RealServerTests
{
    private static readonly ServerLocator Locator = new(null);

    private static LanguageServerManager NewManager(Action<string>? log = null) => new(new LanguageServerManagerOptions
    {
        Locator = Locator,
        IdleTimeout = TimeSpan.FromMinutes(5),
        RestartDelays = [TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(400)],
        Log = log,
    });

    private static void RequireServer(string language)
    {
        var def = LanguageServerRegistry.ForLanguage(language)!;
        Assert.SkipWhen(Locator.Resolve(def) is null, $"{def.Id} is not installed");
        if (language == "go") Assert.SkipWhen(Locator.FindExecutable("go") is null, "go toolchain is not installed");
    }

    private static async Task<T> UntilAsync<T>(Func<T?> probe, string what, int ms = 60000) where T : class
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < ms)
        {
            if (probe() is { } v) return v;
            await Task.Delay(50);
        }
        throw new TimeoutException("timed out waiting for " + what);
    }

    private sealed class Editor(string text)
    {
        public string Text = text;
        public Func<string> Getter => () => Text;
    }

    private static async Task<(WorkspaceMirror mirror, string file)> GoWorkspaceAsync(string source)
    {
        var m = new WorkspaceMirror(TestEnv.TempDir("lsp-real"), "ex-" + Guid.NewGuid().ToString("N")[..6], "go");
        await m.PrepareAsync([("main.go", source)]);
        return (m, m.PathFor("main.go"));
    }

    // ---- gopls ---------------------------------------------------------------------------------------------------------

    private const string GoBroken = "package main\n\nimport \"fmt\"\n\nfunc main() {\n\tvar n int = \"hello\"\n\tfmt.Println(n)\n}\n";

    [Fact]
    public async Task Gopls_reports_a_type_error_with_the_exact_range_and_clears_it_after_the_fix()
    {
        RequireServer("go");
        var (m, file) = await GoWorkspaceAsync(GoBroken);
        await using var mgr = NewManager();
        var ed = new Editor(GoBroken);
        using var doc = mgr.OpenDocument("go", file, m.Root, ed.Getter)!;
        var diags = await UntilAsync(() => doc.Diagnostics.Count > 0 ? doc.Diagnostics : null, "gopls diagnostic");
        var d = diags[0];
        Assert.Equal(DiagnosticSeverity.Error, d.EffectiveSeverity);
        Assert.Equal(new LspRange(new Position(5, 13), new Position(5, 20)), d.Range);          // the "hello" literal
        Assert.Contains("hello", d.Message);
        Assert.Equal("gopls", mgr.GetStatus("go", m.Root).DisplayName);
        Assert.False(string.IsNullOrEmpty(mgr.GetStatus("go", m.Root).Version));

        // fix it through an incremental edit: replace "hello" (with quotes) by 42
        int off = GoBroken.IndexOf("\"hello\"", StringComparison.Ordinal);
        ed.Text = GoBroken.Replace("\"hello\"", "42");
        doc.ApplyEdit(new StringTextBuffer(ed.Text), off, "\"hello\"", "42");
        await UntilAsync(() => doc.Diagnostics.Count == 0 ? doc.Diagnostics : null, "diagnostics cleared");
    }

    [Fact]
    public async Task Gopls_completes_fmt_dot_Println_and_resolves_hover_signature_and_definition()
    {
        RequireServer("go");
        const string src = "package main\n\nimport \"fmt\"\n\nfunc helper(a int, b string) int { return a }\n\nfunc main() {\n\tfmt.\n\thelper(1, \"x\")\n}\n";
        var (m, file) = await GoWorkspaceAsync(src);
        await using var mgr = NewManager();
        var ed = new Editor(src);
        using var doc = mgr.OpenDocument("go", file, m.Root, ed.Getter)!;
        await UntilAsync(() => doc.IsOpenOnServer ? doc : null, "open");

        CompletionList? list = null;
        for (int i = 0; i < 100 && list?.Items.Any(x => x.Label == "Println") != true; i++)
        {
            list = await doc.CompletionAsync(new Position(7, 5), ".", default);
            if (list?.Items.Any(x => x.Label == "Println") != true) await Task.Delay(100);
        }
        var println = list!.Items.First(i => i.Label == "Println");
        Assert.Contains("Println", println.PrimaryEdit()?.NewText ?? println.TextToInsert);

        // accept the completion through an incremental edit; the remaining checks run on the now valid file
        int at = src.IndexOf("fmt.\n", StringComparison.Ordinal) + 4;
        ed.Text = src[..at] + "Println(\"a\")" + src[at..];
        doc.ApplyEdit(new StringTextBuffer(ed.Text), at, "", "Println(\"a\")");

        // hover on helper in the call: markdown with the signature
        var hover = await doc.HoverAsync(new Position(8, 3), default);
        var (text, md) = LspText.Flatten(hover!.Contents);
        Assert.True(md);
        Assert.Contains("func helper(a int, b string) int", text);

        // definition of helper -> line 4 (0-based) of the same file
        var defs = await doc.DefinitionAsync(new Position(8, 3), default);
        Assert.Single(defs);
        Assert.Equal(file, FileUri.ToPath(defs[0].Uri));
        Assert.Equal(4, defs[0].Range.Start.Line);
        Assert.Equal(5, defs[0].Range.Start.Character);

        // signature help inside helper(1, "x") after the comma
        SignatureHelp? sig = null;
        for (int i = 0; i < 50 && sig is null; i++)
        {
            sig = await doc.SignatureHelpAsync(new Position(8, 11), default);
            if (sig is null) await Task.Delay(100);
        }
        Assert.NotNull(sig);
        Assert.Contains("helper", sig!.Signatures[0].Label);
        Assert.Equal(1, sig.ActiveParameter ?? sig.Signatures[0].ActiveParameter);   // after the comma: second parameter
    }

    [Fact]
    public async Task Gopls_definition_in_another_file_of_the_workspace_and_formatting()
    {
        RequireServer("go");
        const string main = "package main\n\nfunc main() {\n\tgreet()\n}\n";
        const string other = "package main\n\nfunc greet()   {\n\tprintln(\"hi\")\n}\n";
        var mirror = new WorkspaceMirror(TestEnv.TempDir("lsp-real"), "multi", "go");
        await mirror.PrepareAsync([("main.go", main), ("greet.go", other)]);
        await using var mgr = NewManager();
        using var doc = mgr.OpenDocument("go", mirror.PathFor("main.go"), mirror.Root, () => main)!;
        await UntilAsync(() => doc.IsOpenOnServer ? doc : null, "open");
        var defs = await UntilAsync(() => doc.DefinitionAsync(new Position(3, 2), default).GetAwaiter().GetResult() is { Count: > 0 } d ? d : null, "definition");
        Assert.Equal(mirror.PathFor("greet.go"), FileUri.ToPath(defs[0].Uri));
        Assert.Equal(2, defs[0].Range.Start.Line);

        using var doc2 = mgr.OpenDocument("go", mirror.PathFor("greet.go"), mirror.Root, () => other)!;
        await UntilAsync(() => doc2.IsOpenOnServer ? doc2 : null, "open2");
        var edits = await doc2.FormatAsync(4, false, default);
        Assert.NotEmpty(edits);
        Assert.Contains(edits, e => e.NewText.Contains("greet() {") || e.NewText.Contains("{") || e.Range.Start.Line == 2);
    }

    [Fact]
    public async Task Gopls_crash_restarts_and_reopens_the_document()
    {
        RequireServer("go");
        var (m, file) = await GoWorkspaceAsync(GoBroken);
        var states = new List<ServerState>();
        await using var mgr = NewManager();
        mgr.StatusChanged += s => { lock (states) states.Add(s.State); };
        using var doc = mgr.OpenDocument("go", file, m.Root, new Editor(GoBroken).Getter)!;
        await UntilAsync(() => doc.Diagnostics.Count > 0 ? doc.Diagnostics : null, "first diagnostics");
        int pid = mgr.ActiveSessions.Single().ProcessId!.Value;
        Process.GetProcessById(pid).Kill(entireProcessTree: true);

        await UntilAsync(() => { lock (states) return states.Contains(ServerState.Restarting) ? states : null; }, "restarting status");
        await UntilAsync(() => mgr.GetStatus("go", m.Root) is { State: ServerState.Ready, RestartCount: 1 } s ? s : null, "ready again");
        Assert.NotEqual(pid, mgr.ActiveSessions.Single().ProcessId);
        // the document was re-sent to the new process: diagnostics arrive again
        await UntilAsync(() => doc.Diagnostics.Count > 0 ? doc.Diagnostics : null, "diagnostics after restart");
    }

    [Fact]
    public async Task Gopls_shutdown_leaves_no_orphan_process()
    {
        RequireServer("go");
        var (m, file) = await GoWorkspaceAsync(GoBroken);
        var mgr = NewManager();
        var doc = mgr.OpenDocument("go", file, m.Root, new Editor(GoBroken).Getter)!;
        await UntilAsync(() => doc.IsOpenOnServer ? doc : null, "open");
        int pid = mgr.ActiveSessions.Single().ProcessId!.Value;
        Assert.False(Process.GetProcessById(pid).HasExited);
        await mgr.StopAllAsync();
        Assert.True(IsGone(pid), "gopls is still running after StopAllAsync");
    }

    [Fact]
    public async Task A_server_that_ignores_shutdown_is_killed_after_the_timeout()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "uses /bin/sh");
        // `cat` never answers a shutdown request: the stop must still end it.
        var def = new LanguageServerDefinition("stuck", "stuck", ["zz"], [new ServerCommand("/bin/cat", [])], [], null, [], "");
        await using var mgr = new LanguageServerManager(new LanguageServerManagerOptions
        {
            Definitions = [def],
            Locator = Locator,
            KillTimeout = TimeSpan.FromMilliseconds(300),
            InitializeTimeout = TimeSpan.FromSeconds(1),
            MaxRestarts = 0,
        });
        string root = TestEnv.TempDir("stuck");
        mgr.OpenDocument("zz", Path.Combine(root, "a.zz"), root, () => "");
        await UntilAsync(() => mgr.ActiveSessions.SingleOrDefault()?.ProcessId is { } ? mgr.ActiveSessions : null, "process");
        int pid = mgr.ActiveSessions.Single().ProcessId!.Value;
        await UntilAsync(() => mgr.GetStatus("zz", root).State == ServerState.Error ? mgr : null, "initialize timeout error", 15000);
        await mgr.StopAllAsync();
        Assert.True(IsGone(pid));
    }

    private static bool IsGone(int pid)
    {
        for (int i = 0; i < 50; i++)
        {
            try { if (Process.GetProcessById(pid).HasExited) return true; }
            catch (ArgumentException) { return true; }
            Thread.Sleep(100);
        }
        return false;
    }

    // ---- clangd --------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Clangd_reports_undeclared_identifier_and_completes_std_vector()
    {
        RequireServer("cpp");
        const string src = "#include <vector>\n\nint main() {\n    undeclared_thing = 1;\n    std::vec\n    return 0;\n}\n";
        var m = new WorkspaceMirror(TestEnv.TempDir("lsp-real"), "cpp-" + Guid.NewGuid().ToString("N")[..6], "cpp");
        await m.PrepareAsync([("main.cpp", src)]);
        await using var mgr = NewManager();
        using var doc = mgr.OpenDocument("cpp", m.PathFor("main.cpp"), m.Root, new Editor(src).Getter)!;
        var diags = await UntilAsync(() => doc.Diagnostics.Any(d => d.Message.Contains("undeclared_thing")) ? doc.Diagnostics : null, "clangd undeclared diagnostic", 90000);
        var undeclared = diags.First(d => d.Message.Contains("undeclared_thing"));
        Assert.Equal(DiagnosticSeverity.Error, undeclared.EffectiveSeverity);
        Assert.Equal(3, undeclared.Range.Start.Line);
        Assert.Equal(4, undeclared.Range.Start.Character);
        Assert.Equal(4 + "undeclared_thing".Length, undeclared.Range.End.Character);

        CompletionList? list = null;
        await UntilAsync(() =>
        {
            list = doc.CompletionAsync(new Position(4, 12), null, default).GetAwaiter().GetResult();
            return list?.Items.Any(i => i.Label.Trim().StartsWith("vector", StringComparison.Ordinal)) == true ? list : null;
        }, "completion with vector", 90000);
        Assert.Contains(list!.Items, i => i.Label.Trim().StartsWith("vector", StringComparison.Ordinal));
        Assert.Equal("clangd", mgr.ActiveSessions.Single().Definition.Id);
    }
}
