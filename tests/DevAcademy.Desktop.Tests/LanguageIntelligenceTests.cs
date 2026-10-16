using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DevAcademy.Desktop.Controls;
using DevAcademy.Desktop.Services;
using DevAcademy.Desktop.Tests.Support;
using DevAcademy.Desktop.ViewModels;
using DevAcademy.Desktop.Views;
using DevAcademy.LanguageServices.Servers;
using Microsoft.Extensions.DependencyInjection;

namespace DevAcademy.Desktop.Tests;

/// <summary>
/// The lesson editor wired to a (scripted, in-process) language server: sync, diagnostics, completion, hover, definition,
/// problems list, status and the mirrored workspace on disk. Rendered headless in both themes with binding errors captured.
/// </summary>
[Collection(BindingCaptureCollection.Name)]
public class LanguageIntelligenceTests
{
    private const string Code = "package main\n\nimport \"fmt\"\n\nfunc main() {\n\tfmt.Println(\"привет 😀\")\n}\n";

    private static TestHost NewHost(ScriptedLauncher launcher, ServerLocator? locator = null, IReadOnlyList<LanguageServerDefinition>? defs = null) =>
        new(configure: s => s.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<DevAcademy.Application.Ports.ISettingsRepository>();
            settings.SetAsync(LanguageIntelligenceService.EnabledKey, "true").GetAwaiter().GetResult();
            return new LanguageIntelligenceService(sp.GetRequiredService<DevAcademy.Infrastructure.AppPaths>(),
                sp.GetRequiredService<DevAcademy.Execution.ExecutionOptions>(), settings,
                launcher, locator ?? ScriptedLauncher.Locator, defs ?? ScriptedLauncher.Definitions);
        }));

    private static async Task<bool> UntilAsync(Func<bool> condition, int ms = 15000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!condition())
        {
            if (sw.ElapsedMilliseconds > ms) return false;
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(15);
        }
        return true;
    }

    private static async Task<(Window Window, ExerciseSessionViewModel Vm, CodeEditorControl Editor)> ShowAsync(TestHost host, string theme)
    {
        var vm = ExerciseSessionViewModel.CreateScratch(host.Services, "lsp-lesson", "go", Code);
        var window = new Window
        {
            Width = 1100,
            Height = 760,
            RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light,
            Content = new SessionView { DataContext = vm },
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var editor = window.GetVisualDescendants().OfType<CodeEditorControl>().Single();
        Assert.True(await UntilAsync(() => editor.Lsp is not null), "editor never attached to the language server");
        return (window, vm, editor);
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task Editor_talks_to_the_server_and_renders_without_binding_errors(string theme)
    {
        using var errors = new BindingErrors();
        var launcher = new ScriptedLauncher();
        using var host = NewHost(launcher);
        var (window, vm, editor) = await ShowAsync(host, theme);
        try
        {
            // Workspace mirror on disk, with the Go scaffolding, before the server sees the file.
            var mirrorFile = vm.Files[0].Lsp!.FilePath;
            Assert.StartsWith(Path.Combine(host.DataDir, "lsp-workspaces"), mirrorFile);
            Assert.Equal(Code, await File.ReadAllTextAsync(mirrorFile));
            Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(mirrorFile)!, "go.mod")));

            Assert.True(await UntilAsync(() => launcher.Last?.OpenedUri is not null), "didOpen not received");
            var server = launcher.Last!;
            Assert.True(await UntilAsync(() => vm.LspStatus.Text.Contains('✓')), "status: " + vm.LspStatus.Text);
            Assert.Contains("scripted-ls", vm.LspStatus.Text);

            // Diagnostics: squiggle source + Problems list (1-based).
            await server.PublishDiagnosticsAsync(5, 1, 4, "undefined: fmtt");
            Assert.True(await UntilAsync(() => vm.Problems.Count == 1));
            var problem = vm.Problems[0];
            Assert.Equal((6, 2, "undefined: fmtt"), (problem.Line, problem.Column, problem.Message));
            Assert.Single(editor.Lsp!.Diagnostics);
            Assert.Contains("(1)", vm.ProblemsHeader);

            // Incremental sync: typing sends a didChange.
            var before = server.Count("textDocument/didChange");
            editor.Editor.Document.Insert(editor.Editor.Document.TextLength, "// x\n");
            Assert.True(await UntilAsync(() => server.Count("textDocument/didChange") > before));

            // Completion after '.' (a trigger character) shows the server's items.
            var line = editor.Editor.Document.GetLineByNumber(6);
            editor.Editor.TextArea.Caret.Offset = line.Offset + 4; // right after "\tfmt"
            editor.Editor.TextArea.PerformTextInput(".");
            Assert.True(await UntilAsync(() => editor.Lsp.IsCompletionOpen), "completion window did not open");
            Assert.Contains("Println", editor.Lsp.CompletionTexts);
            Assert.Contains("Printf", editor.Lsp.CompletionTexts);
            Dispatcher.UIThread.RunJobs();
            SaveFrame(window, $"lsp-completion-{theme}");
        }
        finally { window.Close(); }
        Assert.Empty(errors.Messages);
    }

    [AvaloniaFact]
    public async Task Hover_shows_diagnostic_and_server_text_and_definition_moves_the_caret()
    {
        var launcher = new ScriptedLauncher();
        using var host = NewHost(launcher);
        var (window, _, editor) = await ShowAsync(host, "Light");
        try
        {
            Assert.True(await UntilAsync(() => launcher.Last?.OpenedUri is not null));
            await launcher.Last!.PublishDiagnosticsAsync(4, 5, 9, "declared and not used: main");
            Assert.True(await UntilAsync(() => editor.Lsp!.Diagnostics.Count == 1));
            Dispatcher.UIThread.RunJobs();

            var view = editor.Editor.TextArea.TextView;
            var doc = editor.Editor.Document;
            var loc = doc.GetLocation(doc.GetLineByNumber(5).Offset + 6); // inside "main"
            var p = view.GetVisualPosition(new AvaloniaEdit.TextViewPosition(loc.Line, loc.Column), AvaloniaEdit.Rendering.VisualYPosition.TextMiddle) - view.ScrollOffset;
            await editor.Lsp!.ShowHoverAtAsync(new Point(p.X, p.Y));
            Assert.True(editor.Popups.Hover.IsOpen);
            var texts = editor.Popups.Hover.Child!.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
            Assert.Contains(texts, t => t is not null && t.Contains("declared and not used"));
            Assert.NotEmpty(editor.Popups.Hover.Child!.GetVisualDescendants().OfType<DevAcademy.Desktop.Markdown.MarkdownViewer>());
            SaveFrame(window, "lsp-hover-Light");

            editor.Editor.TextArea.Caret.Offset = 0;
            await editor.Lsp.GoToDefinitionAtAsync(doc.GetLineByNumber(6).Offset + 2);
            var def = doc.GetLocation(editor.Editor.TextArea.Caret.Offset);
            Assert.Equal((1, 6), (def.Line, def.Column)); // the server answered line 0, character 5
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task Missing_server_shows_install_help_and_never_starts_anything()
    {
        var launcher = new ScriptedLauncher();
        using var host = NewHost(launcher,
            new ServerLocator(null, [Path.Combine(Path.GetTempPath(), "no-such-dir-" + Guid.NewGuid().ToString("N"))]),
            [new LanguageServerDefinition("nope", "nope-ls", ["go"], [new ServerCommand("definitely-not-installed-ls", [])], [], null, [], "install nope-ls")]);
        var vm = ExerciseSessionViewModel.CreateScratch(host.Services, "lsp-lesson", "go", Code);
        Assert.True(await UntilAsync(() => vm.LspStatus.IsVisible));
        Assert.True(vm.LspStatus.NeedsHelp);
        Assert.Contains("не установлен", vm.LspStatus.Text);
        Assert.Contains("install nope-ls", vm.LspStatus.Tooltip);
        await vm.LspHelpCommand.ExecuteAsync(null);
        Assert.Contains(host.Dialogs.Messages, m => m.Contains("install nope-ls"));
        Assert.Empty(launcher.Launched);
    }

    [AvaloniaFact]
    public async Task Disabled_in_settings_means_no_workspace_and_no_server()
    {
        using var host = new TestHost(); // the default test host has lsp.enabled=false
        var vm = ExerciseSessionViewModel.CreateScratch(host.Services, "lsp-lesson", "go", Code);
        await Task.Delay(150);
        Assert.False(Directory.Exists(Path.Combine(host.DataDir, "lsp-workspaces")));
        Assert.Contains("выключен", vm.LspStatus.Text);
        Assert.Null(vm.Files[0].Lsp);
    }

    [AvaloniaFact]
    public async Task Real_gopls_through_the_editor_reports_type_errors_and_completes_after_typing()
    {
        var gopls = new ServerLocator(null).Resolve(LanguageServerRegistry.Defaults.First(d => d.Id == "gopls"), null);
        Assert.SkipWhen(gopls is null, "gopls is not installed");
        using var host = new TestHost(configure: s => s.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<DevAcademy.Application.Ports.ISettingsRepository>();
            settings.SetAsync(LanguageIntelligenceService.EnabledKey, "true").GetAwaiter().GetResult();
            return new LanguageIntelligenceService(sp.GetRequiredService<DevAcademy.Infrastructure.AppPaths>(),
                sp.GetRequiredService<DevAcademy.Execution.ExecutionOptions>(), settings);
        }));
        var vm = ExerciseSessionViewModel.CreateScratch(host.Services, "gopls-lesson", "go",
            "package main\n\nimport \"fmt\"\n\nfunc main() {\n\tvar n int = \"привет 😀\"\n\tfmt.Println(n)\n}\n");
        var window = new Window { Width = 1100, Height = 760, Content = new SessionView { DataContext = vm } };
        window.Show();
        try
        {
            var editor = window.GetVisualDescendants().OfType<CodeEditorControl>().Single();
            Assert.True(await UntilAsync(() => editor.Lsp is not null), "no binding");
            Assert.True(await UntilAsync(() => vm.Problems.Count > 0, 60000), "gopls reported nothing: " + vm.LspStatus.Text);
            Assert.Equal(6, vm.Problems[0].Line);
            Assert.Contains("string", vm.Problems[0].Message);

            // Type "fmt." on a new line (through the document, like the editor does) and complete.
            var doc = editor.Editor.Document;
            var at = doc.GetLineByNumber(7).Offset;
            doc.Insert(at, "\tfmt\n");
            editor.Editor.TextArea.Caret.Offset = at + 4;
            editor.Editor.TextArea.PerformTextInput(".");
            Assert.True(await UntilAsync(() => editor.Lsp!.IsCompletionOpen, 30000), "no completion window");
            Assert.Contains("Println", editor.Lsp!.CompletionTexts);
        }
        finally { window.Close(); }
    }

    private static void SaveFrame(Window window, string name)
    {
        if (Environment.GetEnvironmentVariable("DEVACADEMY_SCREENSHOTS") is not { Length: > 0 } dir) return;
        Directory.CreateDirectory(dir);
        using var frame = window.CaptureRenderedFrame();
        frame?.Save(Path.Combine(dir, name + ".png"), new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
    }
}
