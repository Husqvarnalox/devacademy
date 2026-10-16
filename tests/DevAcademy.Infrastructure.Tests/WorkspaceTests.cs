using System.Text;
using DevAcademy.Application.Workspace;
using DevAcademy.Content;
using DevAcademy.Domain;
using DevAcademy.Infrastructure.Workspace;

namespace DevAcademy.Infrastructure.Tests;

public sealed class TempDir : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "da-ws-" + Guid.NewGuid().ToString("N"));
    public TempDir() => Directory.CreateDirectory(Path);
    public string File(string rel, string? text = null)
    {
        var p = System.IO.Path.Combine(Path, rel);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(p)!);
        System.IO.File.WriteAllText(p, text ?? "");
        return p;
    }
    public void Dispose() { try { Directory.Delete(Path, true); } catch (IOException) { } }
}

public class RecentProjectsTests
{
    [Fact]
    public async Task Touch_orders_by_recency_and_stores_only_paths()
    {
        using var t = new TempDb();
        var repo = new SqliteRecentProjectsRepository(t.Db);
        var t0 = DateTimeOffset.UtcNow;
        await repo.TouchAsync("/tmp/a", t0);
        await repo.TouchAsync("/tmp/b", t0.AddMinutes(1));
        await repo.TouchAsync("/tmp/a", t0.AddMinutes(2));
        var list = await repo.GetRecentAsync(10);
        Assert.Equal(["/tmp/a", "/tmp/b"], list.Select(r => r.Path));
        Assert.Equal("a", list[0].Name);
        await repo.RemoveAsync("/tmp/a");
        Assert.Single(await repo.GetRecentAsync(10));
    }

    [Fact]
    public async Task Session_round_trips_and_unknown_project_gives_empty()
    {
        using var t = new TempDb();
        var store = new SqliteProjectSessionStore(t.Db);
        Assert.Equal(ProjectSession.Empty.Groups.Count, (await store.LoadAsync("/x")).Groups.Count);
        var s = new ProjectSession([["a.py", "src/b.py"], ["c.py"]], ["src/b.py", null], 1, "env1");
        await store.SaveAsync("/x", s);
        var back = await store.LoadAsync("/x");
        Assert.Equal(["a.py", "src/b.py"], back.Groups[0]);
        Assert.Equal(1, back.ActiveGroup);
        Assert.Equal("env1", back.PythonEnvironment);
        Assert.Equal("src/b.py", back.ActiveTabs[0]);
    }

    [Fact]
    public void Corrupt_session_json_falls_back_to_empty() =>
        Assert.Same(ProjectSession.Empty, ProjectSession.FromJson("{not json"));
}

public class TextFileStoreTests
{
    [Fact]
    public async Task Save_preserves_crlf_and_bom()
    {
        using var d = new TempDir();
        var path = Path.Combine(d.Path, "f.txt");
        await File.WriteAllBytesAsync(path, [0xEF, 0xBB, 0xBF, .. "a\r\nb\r\n"u8.ToArray()]);
        var read = await TextFileStore.ReadAsync(path);
        Assert.True(read.Ok);
        Assert.True(read.Content!.HasBom);
        Assert.Equal(LineEnding.Crlf, read.Content.LineEnding);

        // The editor hands back LF-only text with an extra line; the file must stay CRLF + BOM.
        await TextFileStore.SaveAsync(path, "a\nb\nc\n", read.Content);
        var bytes = await File.ReadAllBytesAsync(path);
        Assert.Equal([0xEF, 0xBB, 0xBF], bytes[..3]);
        Assert.Equal("a\r\nb\r\nc\r\n", Encoding.UTF8.GetString(bytes[3..]));
        Assert.Empty(Directory.GetFiles(d.Path, "*.tmp"));
    }

    [Fact]
    public async Task Save_keeps_lf_and_no_bom()
    {
        using var d = new TempDir();
        var path = d.File("x.py", "print(1)\n");
        var c = (await TextFileStore.ReadAsync(path)).Content!;
        Assert.False(c.HasBom);
        await TextFileStore.SaveAsync(path, "print(2)\r\nprint(3)\r\n", c);
        Assert.Equal("print(2)\nprint(3)\n", await File.ReadAllTextAsync(path));
        Assert.DoesNotContain((byte)0xEF, await File.ReadAllBytesAsync(path));
    }

    [Theory]
    [InlineData("a\nb\n", LineEnding.Lf)]
    [InlineData("a\r\nb\r\nc\n", LineEnding.Crlf)]
    [InlineData("a\rb\r", LineEnding.Cr)]
    [InlineData("none", LineEnding.None)]
    public void Detects_dominant_line_ending(string text, LineEnding expected) =>
        Assert.Equal(expected, TextFileStore.DetectLineEnding(text));

    [Fact]
    public async Task Rejects_binary_and_non_utf8_and_missing()
    {
        using var d = new TempDir();
        var bin = Path.Combine(d.Path, "b.bin");
        await File.WriteAllBytesAsync(bin, [1, 2, 0, 3]);
        Assert.Equal(TextFileProblem.Binary, (await TextFileStore.ReadAsync(bin)).Problem);
        var latin = Path.Combine(d.Path, "l.txt");
        await File.WriteAllBytesAsync(latin, [0x61, 0xE9, 0x62]);
        Assert.Equal(TextFileProblem.NotUtf8, (await TextFileStore.ReadAsync(latin)).Problem);
        Assert.Equal(TextFileProblem.NotFound, (await TextFileStore.ReadAsync(Path.Combine(d.Path, "nope"))).Problem);
    }

    [Fact]
    public async Task Save_keeps_unix_permissions()
    {
        if (OperatingSystem.IsWindows()) return;
        using var d = new TempDir();
        var path = d.File("run.sh", "echo hi\n");
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        await TextFileStore.SaveAsync(path, "echo bye\n", (await TextFileStore.ReadAsync(path)).Content);
        Assert.True(File.GetUnixFileMode(path).HasFlag(UnixFileMode.UserExecute));
    }
}

public class ProjectTreeTests
{
    [Fact]
    public void Lists_dirs_first_and_hides_ignored_by_default()
    {
        using var d = new TempDir();
        d.File("b.txt"); d.File("A.txt"); d.File("src/x.py"); d.File(".git/HEAD"); d.File("node_modules/m/i.js"); d.File(".venv/pyvenv.cfg");
        d.File("target/debug/x"); d.File("bin/a"); d.File("obj/a");

        var hidden = ProjectTree.ListChildren(d.Path, showIgnored: false).Select(n => n.Name).ToList();
        Assert.Equal(["src", "A.txt", "b.txt"], hidden);

        var all = ProjectTree.ListChildren(d.Path, showIgnored: true).Select(n => n.Name).ToList();
        Assert.Contains(".git", all);
        Assert.Contains("node_modules", all);
        Assert.Contains(".venv", all);
        Assert.Contains("target", all);
        Assert.Contains("bin", all);
        Assert.Contains("obj", all);
    }

    [Fact]
    public void Listing_is_lazy_one_level_only()
    {
        using var d = new TempDir();
        d.File("a/b/c.txt");
        var top = ProjectTree.ListChildren(d.Path, false);
        Assert.Single(top);
        Assert.Equal("c.txt", ProjectTree.ListChildren(Path.Combine(d.Path, "a", "b"), false).Single().Name);
    }
}

public class ProjectFileOpsTests
{
    private sealed class FakeTrash(bool available, bool succeeds) : IFileTrash
    {
        public List<string> Trashed { get; } = [];
        public bool IsAvailable => available;
        public bool TryMoveToTrash(string path)
        {
            if (!succeeds) return false;
            Trashed.Add(path);
            if (Directory.Exists(path)) Directory.Delete(path, true); else File.Delete(path);
            return true;
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("..")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("a:b")]
    [InlineData(" a")]
    [InlineData("a.")]
    public void Rejects_bad_names(string name) => Assert.NotNull(ProjectFileOps.ValidateName(name));

    [Fact]
    public void Create_rename_and_collisions()
    {
        using var d = new TempDir();
        var ops = new ProjectFileOps(new FakeTrash(true, true));
        var f = ops.CreateFile(d.Path, "main.py");
        Assert.True(f.Ok);
        Assert.False(ops.CreateFile(d.Path, "main.py").Ok);
        Assert.True(ops.CreateFolder(d.Path, "pkg").Ok);
        Assert.False(ops.CreateFolder(d.Path, "pkg").Ok);
        var r = ops.Rename(f.Path!, "app.py");
        Assert.True(r.Ok);
        Assert.True(File.Exists(Path.Combine(d.Path, "app.py")));
        Assert.False(ops.Rename(r.Path!, "pkg").Ok);
        Assert.False(ops.Rename(r.Path!, "../escape.py").Ok);
    }

    [Fact]
    public void Delete_goes_to_trash_when_available()
    {
        using var d = new TempDir();
        var trash = new FakeTrash(true, true);
        var ops = new ProjectFileOps(trash);
        var p = d.File("a.txt");
        Assert.True(ops.Delete(p, allowPermanent: false).Ok);
        Assert.Equal([p], trash.Trashed);
    }

    [Fact]
    public void Delete_without_trash_requires_explicit_permanent_permission()
    {
        using var d = new TempDir();
        var ops = new ProjectFileOps(new NoTrash());
        Assert.False(ops.CanTrash);
        var p = d.File("a.txt");
        Assert.False(ops.Delete(p, allowPermanent: false).Ok);
        Assert.True(File.Exists(p));
        Assert.True(ops.Delete(p, allowPermanent: true).Ok);
        Assert.False(File.Exists(p));
    }

    [Fact]
    public void Mac_trash_moves_into_trash_directory_with_unique_names()
    {
        using var d = new TempDir();
        using var trashDir = new TempDir();
        var trash = new MacTrash(trashDir.Path);
        var a = d.File("x.txt", "1");
        Assert.True(trash.TryMoveToTrash(a));
        var b = d.File("x.txt", "2");
        Assert.True(trash.TryMoveToTrash(b));
        Assert.Equal(2, Directory.GetFiles(trashDir.Path).Length);
        Assert.False(File.Exists(a));
    }

    [Fact]
    public void Freedesktop_trash_writes_trashinfo()
    {
        using var d = new TempDir();
        using var home = new TempDir();
        var trash = new FreedesktopTrash(home.Path);
        var a = d.File("my file.txt");
        Assert.True(trash.TryMoveToTrash(a));
        Assert.Single(Directory.GetFiles(Path.Combine(home.Path, "Trash", "files")));
        var info = Directory.GetFiles(Path.Combine(home.Path, "Trash", "info")).Single();
        var text = File.ReadAllText(info);
        Assert.Contains("[Trash Info]", text);
        Assert.Contains("my%20file.txt", text);
    }
}

public class WorkspaceSearchTests
{
    [Fact]
    public async Task Text_search_finds_lines_and_skips_binary_large_and_ignored()
    {
        using var d = new TempDir();
        d.File("a.py", "x = 1\nNeedle here\n");
        d.File("sub/b.txt", "no\nneedle again\n");
        d.File("node_modules/n.js", "needle");
        File.WriteAllBytes(Path.Combine(d.Path, "bin.dat"), [.. "needle"u8.ToArray(), 0, 1, 2]);
        d.File("big.txt", "needle" + new string('x', 3000));
        var hits = new List<SearchHit>();
        var n = await WorkspaceSearch.SearchTextAsync(d.Path, "needle", new SearchOptions(MaxFileBytes: 2000), hits.Add, CancellationToken.None);
        Assert.Equal(2, n);
        Assert.Equal(["a.py", "sub/b.txt"], hits.Select(h => h.RelativePath).Order());
        Assert.Equal(2, hits.First(h => h.RelativePath == "a.py").Line);

        hits.Clear();
        await WorkspaceSearch.SearchTextAsync(d.Path, "needle", new SearchOptions(CaseSensitive: true, MaxFileBytes: 2000), hits.Add, CancellationToken.None);
        Assert.Single(hits);
    }

    [Fact]
    public async Task Text_search_is_cancellable()
    {
        using var d = new TempDir();
        for (var i = 0; i < 50; i++) d.File($"f{i}.txt", "needle\n");
        using var cts = new CancellationTokenSource();
        var seen = 0;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            WorkspaceSearch.SearchTextAsync(d.Path, "needle", new SearchOptions(), _ => { if (++seen == 3) cts.Cancel(); }, cts.Token));
        Assert.True(seen < 50);
    }

    [Fact]
    public async Task Text_search_honours_max_hits()
    {
        using var d = new TempDir();
        d.File("a.txt", string.Join("\n", Enumerable.Repeat("needle", 100)));
        var n = await WorkspaceSearch.SearchTextAsync(d.Path, "needle", new SearchOptions(MaxHits: 10), _ => { }, CancellationToken.None);
        Assert.Equal(10, n);
    }

    [Fact]
    public void Filename_search_ranks_exact_then_prefix_and_skips_ignored()
    {
        using var d = new TempDir();
        d.File("src/main.py"); d.File("main.py"); d.File("domain.py"); d.File("node_modules/main.js"); d.File("readme.md");
        var r = WorkspaceSearch.FindFiles(d.Path, "main", false, 10, CancellationToken.None);
        Assert.Equal(["main.py", "src/main.py", "domain.py"], r);
        Assert.Empty(WorkspaceSearch.FindFiles(d.Path, "", false, 10, CancellationToken.None));
    }
}

public class ProjectWatcherTests
{
    [Fact]
    public async Task Reports_external_modification_but_not_ignored_directories()
    {
        using var d = new TempDir();
        var file = d.File("a.txt", "one");
        Directory.CreateDirectory(Path.Combine(d.Path, "node_modules"));
        using var w = new ProjectWatcher(d.Path, TimeSpan.FromMilliseconds(50));
        var tcs = new TaskCompletionSource<IReadOnlyList<FileChange>>();
        var all = new List<FileChange>();
        w.Changes += batch => { lock (all) all.AddRange(batch); tcs.TrySetResult(batch); };

        await File.WriteAllTextAsync(Path.Combine(d.Path, "node_modules", "x.js"), "ignored");
        await Task.Delay(300);
        lock (all) Assert.Empty(all);

        await File.WriteAllTextAsync(file, "two");
        var done = await Task.WhenAny(tcs.Task, Task.Delay(5000));
        Assert.Same(tcs.Task, done);
        lock (all) Assert.Contains(all, c => c.FullPath == file && c.Kind is ChangeKind.Changed or ChangeKind.Created);
    }
}

public class TemplateTests
{
    private static ContentSet Content()
    {
        var root = FileContentRepository.FindContentRoot() ?? throw new InvalidOperationException("content root not found");
        return ContentLoader.Load(root).Content;
    }

    [Fact]
    public void Shipped_templates_are_valid_and_cover_four_languages()
    {
        var set = Content();
        Assert.Equal(["go-cli", "node-cli", "python-cli", "rust-cli"], set.Templates.Select(t => t.Id).Order());
        Assert.Equal(["go", "javascript", "python", "rust"], set.Templates.Select(t => t.LanguageId).Order());
    }

    [Fact]
    public void Instantiate_writes_files_substitutes_name_and_runs_nothing()
    {
        var set = Content();
        using var parent = new TempDir();
        foreach (var t in set.Templates)
        {
            var r = TemplateInstaller.Instantiate(t, parent.Path, "demo-" + t.Id);
            Assert.True(r.Ok, r.Error);
            Assert.True(Directory.Exists(r.ProjectPath));
            Assert.NotEmpty(Directory.GetFiles(r.ProjectPath!, "*", SearchOption.AllDirectories));
            foreach (var f in Directory.GetFiles(r.ProjectPath!, "*", SearchOption.AllDirectories))
                Assert.DoesNotContain("{{name}}", File.ReadAllText(f));
            // nothing was executed: no venv, no build output, no lock files created by tools
            Assert.False(Directory.Exists(Path.Combine(r.ProjectPath!, ".venv")));
            Assert.False(Directory.Exists(Path.Combine(r.ProjectPath!, "target")));
            Assert.False(File.Exists(Path.Combine(r.ProjectPath!, "go.sum")));
        }
        var py = set.Templates.Single(t => t.Id == "python-cli");
        Assert.NotEmpty(py.PostCreate);
        Assert.Contains("demo-go-cli", File.ReadAllText(Path.Combine(parent.Path, "demo-go-cli", "go.mod")));
        Assert.Contains("demo-rust-cli", File.ReadAllText(Path.Combine(parent.Path, "demo-rust-cli", "Cargo.toml")));
    }

    [Fact]
    public void Instantiate_refuses_bad_names_and_existing_non_empty_directory()
    {
        var t = Content().Templates.First();
        using var parent = new TempDir();
        Assert.False(TemplateInstaller.Instantiate(t, parent.Path, "../evil").Ok);
        Assert.False(TemplateInstaller.Instantiate(t, parent.Path, "1abc").Ok);
        parent.File("taken/file.txt");
        Assert.False(TemplateInstaller.Instantiate(t, parent.Path, "taken").Ok);
    }

    [Fact]
    public void Run_profiles_match_the_language()
    {
        Assert.Equal("python main.py", ProjectRunProfiles.For("python")!.RunCommand);
        Assert.Equal("python -m pytest", ProjectRunProfiles.For("python")!.TestCommand);
        Assert.Equal("/opt/v/bin/python main.py", ProjectRunProfiles.For("python", "/opt/v/bin/python")!.RunCommand);
        Assert.Contains("'", ProjectRunProfiles.For("python", "/my dir/python")!.RunCommand);
        Assert.Equal(("node main.js", "node --test"), (ProjectRunProfiles.For("javascript")!.RunCommand, ProjectRunProfiles.For("javascript")!.TestCommand));
        Assert.Equal(("go run .", "go test ./..."), (ProjectRunProfiles.For("go")!.RunCommand, ProjectRunProfiles.For("go")!.TestCommand));
        Assert.Equal(("cargo run", "cargo test"), (ProjectRunProfiles.For("rust")!.RunCommand, ProjectRunProfiles.For("rust")!.TestCommand));
        Assert.Null(ProjectRunProfiles.For("cobol"));
    }

    [Fact]
    public void Language_detection_uses_marker_files()
    {
        using var d = new TempDir();
        Assert.Null(ProjectRunProfiles.DetectLanguage(d.Path));
        d.File("go.mod");
        Assert.Equal("go", ProjectRunProfiles.DetectLanguage(d.Path));
        using var r = new TempDir(); r.File("Cargo.toml");
        Assert.Equal("rust", ProjectRunProfiles.DetectLanguage(r.Path));
        using var n = new TempDir(); n.File("package.json");
        Assert.Equal("javascript", ProjectRunProfiles.DetectLanguage(n.Path));
        using var p = new TempDir(); p.File("x.py");
        Assert.Equal("python", ProjectRunProfiles.DetectLanguage(p.Path));
    }
}
