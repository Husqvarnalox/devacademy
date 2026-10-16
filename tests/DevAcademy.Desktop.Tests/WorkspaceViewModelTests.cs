using Avalonia.Headless.XUnit;
using DevAcademy.Desktop.Tests.Support;
using DevAcademy.Desktop.ViewModels;
using DevAcademy.Infrastructure.Workspace;
using Microsoft.Extensions.DependencyInjection;

namespace DevAcademy.Desktop.Tests;

public class WorkspaceViewModelTests : IDisposable
{
    private readonly string _project = Path.Combine(Path.GetTempPath(), "da-proj-" + Guid.NewGuid().ToString("N"));
    private readonly FakeTerminalFactory _terminals = new();

    public WorkspaceViewModelTests()
    {
        Directory.CreateDirectory(_project);
        File.WriteAllText(Path.Combine(_project, "main.py"), "print('hi')\r\n");
        File.WriteAllText(Path.Combine(_project, "notes.txt"), "one\ntwo\n");
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(_project, true); } catch (IOException) { }
    }

    private TestHost NewHost(string? dataDir = null)
    {
        var host = new TestHost(dataDir, configure: s => s.AddSingleton<DevAcademy.Terminal.ITerminalFactory>(_terminals));
        host.Services.Workspace.WatchFiles = false;
        return host;
    }

    private static async Task<ProjectWorkspaceViewModel> OpenAsync(TestHost host, string path)
    {
        var projects = new ProjectsViewModel(host.Services);
        await projects.LoadAsync();
        await projects.OpenAsync(path);
        return projects.Project!;
    }

    [Fact]
    public async Task Editing_marks_the_tab_dirty_and_save_clears_it_keeping_crlf()
    {
        using var host = NewHost();
        var ws = await OpenAsync(host, _project);
        var tab = await ws.OpenFileAsync(Path.Combine(_project, "main.py"));
        Assert.NotNull(tab);
        Assert.False(tab!.IsDirty);

        tab.Document.Insert(0, "# x\n");
        Assert.True(tab.IsDirty);
        Assert.True(ws.HasUnsavedChanges);

        await ws.SaveAsync();
        Assert.False(tab.IsDirty);
        Assert.Equal("# x\r\nprint('hi')\r\n", File.ReadAllText(Path.Combine(_project, "main.py")));
    }

    [Fact]
    public async Task Closing_a_dirty_tab_asks_and_cancel_keeps_it_open()
    {
        using var host = NewHost();
        var ws = await OpenAsync(host, _project);
        var tab = (await ws.OpenFileAsync(Path.Combine(_project, "notes.txt")))!;
        tab.Document.Insert(0, "X");

        host.Dialogs.ChoiceAnswers.Enqueue(2); // Cancel
        Assert.False(await ws.CloseTabAsync(tab));
        Assert.Contains(tab, ws.AllTabs);
        Assert.Single(host.Dialogs.Choices);

        host.Dialogs.ChoiceAnswers.Enqueue(1); // Don't save
        Assert.True(await ws.CloseTabAsync(tab));
        Assert.Empty(ws.AllTabs);
        Assert.Equal("one\ntwo\n", File.ReadAllText(Path.Combine(_project, "notes.txt")));
    }

    [Fact]
    public async Task Closing_a_dirty_tab_with_save_writes_the_file()
    {
        using var host = NewHost();
        var ws = await OpenAsync(host, _project);
        var tab = (await ws.OpenFileAsync(Path.Combine(_project, "notes.txt")))!;
        tab.Document.Insert(0, "X");
        host.Dialogs.ChoiceAnswers.Enqueue(0);
        Assert.True(await ws.CloseTabAsync(tab));
        Assert.StartsWith("Xone", File.ReadAllText(Path.Combine(_project, "notes.txt")));
    }

    [Fact]
    public async Task Session_is_restored_after_restarting_the_app()
    {
        var dataDir = Path.Combine(Path.GetTempPath(), "devacademy-ui-" + Guid.NewGuid().ToString("N"));
        using (var host = NewHost(dataDir))
        {
            var ws = await OpenAsync(host, _project);
            await ws.OpenFileAsync(Path.Combine(_project, "main.py"));
            await ws.OpenFileAsync(Path.Combine(_project, "notes.txt"));
            ws.ActivateTab(ws.Groups[0], ws.Groups[0].Tabs[0]);
            await ws.SaveSessionAsync();
            ws.Dispose();
        }
        using (var again = NewHost(dataDir))
        {
            var projects = new ProjectsViewModel(again.Services);
            await projects.LoadAsync();
            Assert.Equal(Path.GetFullPath(_project), projects.Recents.Single().Path);
            await projects.OpenRecentCommand.ExecuteAsync(projects.Recents[0]);
            var ws = projects.Project!;
            Assert.Equal(["main.py", "notes.txt"], ws.Groups[0].Tabs.Select(t => t.RelativePath));
            Assert.Equal("main.py", ws.Groups[0].ActiveTab!.RelativePath);
        }
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(dataDir, true); } catch (IOException) { }
    }

    [Fact]
    public async Task Delete_asks_for_confirmation_and_declining_keeps_the_file()
    {
        using var host = NewHost();
        var ws = await OpenAsync(host, _project);
        ws.Explorer.Selected = ws.Explorer.Find(Path.Combine(_project, "notes.txt"));
        Assert.NotNull(ws.Explorer.Selected);

        host.Dialogs.Answer = false;
        await ws.DeleteCommand.ExecuteAsync(null);
        Assert.Single(host.Dialogs.Confirmed);
        Assert.True(File.Exists(Path.Combine(_project, "notes.txt")));
    }

    [Fact]
    public async Task Run_and_Test_type_the_language_command_visibly_into_a_terminal()
    {
        using var host = NewHost();
        var ws = await OpenAsync(host, _project);
        Assert.True(ws.CanRun);
        Assert.Empty(_terminals.Sessions);

        await ws.RunCommand.ExecuteAsync(null);
        var term = Assert.Single(_terminals.Sessions);
        Assert.Equal(Path.GetFullPath(_project), term.Options.Cwd);
        Assert.Equal("python main.py\r", Assert.Single(term.Written));

        await ws.TestCommand.ExecuteAsync(null);
        Assert.Equal("python -m pytest\r", term.Written[1]); // same terminal reused
        Assert.Single(_terminals.Sessions);
    }

    [Fact]
    public async Task Run_saves_dirty_tabs_first()
    {
        using var host = NewHost();
        var ws = await OpenAsync(host, _project);
        var tab = (await ws.OpenFileAsync(Path.Combine(_project, "main.py")))!;
        tab.Document.Insert(0, "# y\n");
        await ws.RunCommand.ExecuteAsync(null);
        Assert.False(tab.IsDirty);
        Assert.StartsWith("# y", File.ReadAllText(Path.Combine(_project, "main.py")));
    }

    // TextDocument is thread-affine, so the reload continuation must return to the UI thread like it does in the app.
    [AvaloniaFact]
    public async Task External_change_prompts_and_reload_replaces_the_text()
    {
        using var host = NewHost();
        var ws = await OpenAsync(host, _project);
        var path = Path.Combine(_project, "notes.txt");
        var tab = (await ws.OpenFileAsync(path))!;

        File.WriteAllText(path, "changed outside\n");
        host.Dialogs.ChoiceAnswers.Enqueue(0); // Reload
        await ws.HandleChangesAsync([new FileChange(ChangeKind.Changed, path)]);
        Assert.Single(host.Dialogs.Choices);
        Assert.Equal("changed outside\n", tab.Document.Text);
        Assert.False(tab.IsDirty);

        // Keep: no second prompt for the same content.
        File.WriteAllText(path, "again\n");
        host.Dialogs.ChoiceAnswers.Enqueue(1);
        await ws.HandleChangesAsync([new FileChange(ChangeKind.Changed, path)]);
        await ws.HandleChangesAsync([new FileChange(ChangeKind.Changed, path)]);
        Assert.Equal(2, host.Dialogs.Choices.Count);
        Assert.Equal("changed outside\n", tab.Document.Text);
    }

    [Fact]
    public async Task Own_saves_do_not_prompt()
    {
        using var host = NewHost();
        var ws = await OpenAsync(host, _project);
        var path = Path.Combine(_project, "notes.txt");
        var tab = (await ws.OpenFileAsync(path))!;
        tab.Document.Insert(0, "Z");
        await ws.SaveAsync();
        await ws.HandleChangesAsync([new FileChange(ChangeKind.Changed, path)]);
        Assert.Empty(host.Dialogs.Choices);
    }

    [Fact]
    public async Task Template_creation_shows_post_create_commands_and_runs_nothing()
    {
        using var host = NewHost();
        var parent = Path.Combine(_project, "out");
        Directory.CreateDirectory(parent);
        host.Files.FolderPath = parent;
        host.Dialogs.PromptAnswers.Enqueue("demo");
        var projects = new ProjectsViewModel(host.Services);
        await projects.LoadAsync();
        var template = projects.Templates.Single(t => t.Id == "python-cli");

        await projects.CreateFromTemplateCommand.ExecuteAsync(template);
        var ws = projects.Project!;
        Assert.True(File.Exists(Path.Combine(parent, "demo", "main.py")));
        Assert.True(ws.HasPostCreate);
        Assert.Empty(_terminals.Sessions);
        Assert.False(Directory.Exists(Path.Combine(parent, "demo", ".venv")));

        // "To terminal" types the command but does not press Enter.
        ws.TypePostCreateCommand.Execute(ws.PostCreateCommands[0]);
        var term = Assert.Single(_terminals.Sessions);
        Assert.DoesNotContain('\r', term.Written[0]);
    }

    [Fact]
    public async Task Split_moves_the_active_tab_and_collapses_when_empty()
    {
        using var host = NewHost();
        var ws = await OpenAsync(host, _project);
        await ws.OpenFileAsync(Path.Combine(_project, "main.py"));
        var second = (await ws.OpenFileAsync(Path.Combine(_project, "notes.txt")))!;
        await ws.ToggleSplitCommand.ExecuteAsync(null);
        Assert.True(ws.IsSplit);
        Assert.Equal([second], ws.Groups[1].Tabs);
        await ws.CloseTabAsync(second);
        Assert.False(ws.IsSplit);
    }
}
