using System.Text.Json.Nodes;
using DevAcademy.Notebooks.Nbformat;
using DevAcademy.Notebooks.Text;

namespace DevAcademy.Notebooks.Tests;

public class NbformatTests
{
    private static string FixturePath => Path.Combine(AppContext.BaseDirectory, "Fixtures", "sample_nbformat45.ipynb");

    [Fact]
    public void A_notebook_written_by_python_round_trips_byte_for_byte()
    {
        // Fixture: tests/.../Fixtures/make_fixture.py (json.dumps indent=1, sort_keys, ensure_ascii=False).
        var original = File.ReadAllText(FixturePath);
        var doc = NotebookSerializer.Parse(original);
        Assert.Equal(original, NotebookSerializer.Serialize(doc));
    }

    [Fact]
    public void The_fixture_is_read_into_the_expected_model()
    {
        var doc = NotebookSerializer.Parse(File.ReadAllText(FixturePath));
        Assert.Equal(4, doc.NbFormat);
        Assert.Equal(5, doc.NbFormatMinor);
        Assert.Equal([CellType.Markdown, CellType.Code, CellType.Code, CellType.Code, CellType.Code, CellType.Code, CellType.Raw], doc.Cells.Select(c => c.Type));

        var md = doc.Cells[0];
        Assert.Equal("intro0001", md.Id);
        Assert.StartsWith("# Заметки\n\nФормула:", md.Source);
        Assert.NotNull(md.Attachments?["pic.png"]);
        Assert.Equal("intro", md.Metadata["tags"]![0]!.GetValue<string>());

        var code = doc.Cells[1];
        Assert.Equal(1, code.ExecutionCount);
        Assert.Equal("import sys\nprint('hello')\nprint('мир')\nprint('warning', file=sys.stderr)", code.Source);
        Assert.Equal(["stdout", "stderr"], code.Outputs.Select(o => o.Name));
        Assert.Equal("hello\nмир\n", code.Outputs[0].Text);

        var result = doc.Cells[2].Outputs.Single();
        Assert.Equal(NotebookOutput.ExecuteResult, result.OutputType);
        Assert.Equal(2, result.ExecutionCount);
        Assert.Equal("   a\n0  1", result.GetText("text/plain"));
        Assert.StartsWith("<table>", result.GetText("text/html"));

        var display = doc.Cells[3].Outputs;
        Assert.NotNull(display[0].GetText("image/png"));
        Assert.Equal(3.14, display[1].Data!["application/json"]!["pi"]!.GetValue<double>());

        var error = doc.Cells[4].Outputs.Single();
        Assert.Equal("ZeroDivisionError", error.ErrorName);
        Assert.Equal(4, error.Traceback.Count);

        Assert.Null(doc.Cells[5].ExecutionCount);
        Assert.Equal("Raw *text*", doc.Cells[6].Source);
        Assert.Equal("python3", doc.Metadata["kernelspec"]!["name"]!.GetValue<string>());
    }

    [Fact]
    public void Edits_survive_a_save_and_unknown_metadata_is_preserved()
    {
        var doc = NotebookSerializer.Parse(File.ReadAllText(FixturePath));
        doc.Cells[5].Source = "x = 1\ny = 2\n";
        doc.Cells[5].ExecutionCount = 7;
        doc.Cells[5].Outputs.Add(NotebookOutput.StreamOutput("stdout", "ok\n"));
        doc.Cells.Add(NotebookCell.NewMarkdown("new"));
        var again = NotebookSerializer.Parse(NotebookSerializer.Serialize(doc));
        Assert.Equal("x = 1\ny = 2\n", again.Cells[5].Source);
        Assert.Equal(7, again.Cells[5].ExecutionCount);
        Assert.Equal("ok\n", again.Cells[5].Outputs.Single().Text);
        Assert.Equal(8, again.Cells.Count);
        Assert.NotNull(again.Cells[^1].Id);
        Assert.Single(again.Metadata["custom_extension"]!["nested"]!.AsObject());
        Assert.Equal(3, again.Metadata["custom_extension"]!["anything"]!.AsArray().Count);
    }

    [Fact]
    public void Unknown_fields_on_cells_outputs_and_the_notebook_are_kept()
    {
        const string json = """
            {"cells":[{"cell_type":"code","execution_count":1,"metadata":{},"outputs":[{"output_type":"stream","name":"stdout","text":["a"],"future":1}],"source":"x","vendor":{"k":true}}],
             "metadata":{},"nbformat":4,"nbformat_minor":4,"top_extra":"yes"}
            """;
        var doc = NotebookSerializer.Parse(json);
        var again = JsonNode.Parse(NotebookSerializer.Serialize(doc))!;
        Assert.Equal("yes", again["top_extra"]!.GetValue<string>());
        Assert.True(again["cells"]![0]!["vendor"]!["k"]!.GetValue<bool>());
        Assert.Equal(1, again["cells"]![0]!["outputs"]![0]!["future"]!.GetValue<int>());
        Assert.Null(again["cells"]![0]!["id"]); // minor 4: no ids are invented
        Assert.Equal(4, again["nbformat_minor"]!.GetValue<int>());
    }

    [Fact]
    public void Notebooks_of_minor_5_get_ids_on_save_and_source_may_be_a_single_string()
    {
        var doc = NotebookSerializer.Parse("""{"cells":[{"cell_type":"markdown","metadata":{},"source":"# t"}],"metadata":{},"nbformat":4,"nbformat_minor":5}""");
        Assert.Equal("# t", doc.Cells[0].Source);
        var json = JsonNode.Parse(NotebookSerializer.Serialize(doc))!;
        Assert.False(string.IsNullOrEmpty(json["cells"]![0]!["id"]!.GetValue<string>()));
    }

    [Theory]
    [InlineData("""{"nbformat":3,"cells":[]}""", "nbformat 3")]
    [InlineData("""{"cells":[]}""", "no \"nbformat\"")]
    [InlineData("not json", "not valid JSON")]
    [InlineData("[]", "not a notebook")]
    [InlineData("""{"nbformat":4,"nbformat_minor":5,"metadata":{},"cells":[{"cell_type":"weird","source":""}]}""", "Unknown cell_type")]
    public void Bad_files_fail_with_a_readable_error(string json, string expected)
    {
        var ex = Assert.Throws<NotebookFormatException>(() => NotebookSerializer.Parse(json));
        Assert.Contains(expected, ex.Message);
    }

    [Fact]
    public async Task Save_is_atomic_and_leaves_no_temp_files_and_creating_does_not_overwrite()
    {
        var dir = Path.Combine(Path.GetTempPath(), "da-nb-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "a.ipynb");
            var created = await NotebookStore.CreateAsync(path);
            Assert.Single(created.Cells);
            await Assert.ThrowsAsync<IOException>(() => NotebookStore.CreateAsync(path));
            created.Cells[0].Source = "print(1)";
            await NotebookStore.SaveAsync(path, created);
            Assert.Equal("print(1)", (await NotebookStore.LoadAsync(path)).Cells[0].Source);
            Assert.Equal(["a.ipynb"], Directory.GetFiles(dir).Select(Path.GetFileName));
            Assert.EndsWith("\n", File.ReadAllText(path));
            Assert.False(File.ReadAllText(path).StartsWith('﻿'));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void New_notebooks_declare_python3_and_nbformat_4_5()
    {
        var json = JsonNode.Parse(NotebookSerializer.Serialize(NotebookDocument.CreateNew()))!;
        Assert.Equal(4, json["nbformat"]!.GetValue<int>());
        Assert.Equal(5, json["nbformat_minor"]!.GetValue<int>());
        Assert.Equal("python3", json["metadata"]!["kernelspec"]!["name"]!.GetValue<string>());
        Assert.Equal("code", json["cells"]![0]!["cell_type"]!.GetValue<string>());
        Assert.Null(json["cells"]![0]!["execution_count"]);
    }
}

public class TextTests
{
    [Fact]
    public void Ansi_strip_removes_colours_and_other_escapes()
    {
        Assert.Equal("a red b", AnsiText.Strip("a \u001b[31mred\u001b[0m b"));
        Assert.Equal("x", AnsiText.Strip("\u001b[0;32;1mx\u001b[39;49m\u001b[2K"));
    }

    [Fact]
    public void Ansi_parse_tracks_colour_and_bold()
    {
        var runs = AnsiText.Parse("plain \u001b[1;31mbold red\u001b[0m \u001b[92mbright\u001b[39m end\u001b[38;5;241mx");
        Assert.Equal("plain ", runs[0].Text);
        Assert.Null(runs[0].Color);
        Assert.Equal(new AnsiRun("bold red", 1, true), runs[1]);
        Assert.Equal(new AnsiRun("bright", 10, false), runs[3]);
        Assert.Equal("plain bold red bright endx", string.Concat(runs.Select(r => r.Text)));
    }

    [Fact]
    public void Carriage_returns_overwrite_the_line_like_progress_bars()
    {
        Assert.Equal("100%\nnext", AnsiText.ApplyCarriageReturns("10%\r50%\r100%\nnext"));
        Assert.Equal("a\nb", AnsiText.ApplyCarriageReturns("a\r\nb"));
    }

    [Fact]
    public void Display_math_becomes_a_latex_code_block_but_code_fences_are_untouched()
    {
        var md = "before $$a^2+b^2$$ after\n\n$$\n\\int_0^1 x\\,dx\n$$\n\n```python\nprice = '$$'\n```\n\ninline $x$ stays";
        var prepared = MarkdownMath.Prepare(md);
        Assert.Contains("```latex\na^2+b^2\n```", prepared);
        Assert.Contains("```latex\n\\int_0^1 x\\,dx\n```", prepared);
        Assert.Contains("price = '$$'", prepared);
        Assert.Contains("inline $x$ stays", prepared);
        Assert.Equal("no math", MarkdownMath.Prepare("no math"));
    }

    [Fact]
    public void Presentation_prefers_image_then_markdown_then_plain_and_never_html()
    {
        var withImage = new NotebookOutput { OutputType = NotebookOutput.DisplayData, Data = new JsonObject { ["image/png"] = Convert.ToBase64String([1, 2, 3]), ["text/plain"] = "<Figure>", ["text/html"] = "<b>x</b>" } };
        var p = OutputPresentation.For(withImage);
        Assert.Equal(PresentationKind.Image, p.Kind);
        Assert.Equal(new byte[] { 1, 2, 3 }, p.ImageBytes);

        var table = new NotebookOutput { OutputType = NotebookOutput.ExecuteResult, Data = new JsonObject { ["text/html"] = "<table><tr><td>1</td></tr></table>", ["text/plain"] = "   a\n0  1" } };
        Assert.Equal(new OutputPresentation(PresentationKind.Text, "   a\n0  1"), OutputPresentation.For(table));

        var htmlOnly = new NotebookOutput { OutputType = NotebookOutput.DisplayData, Data = new JsonObject { ["text/html"] = "<table><tr><th>a</th><th>b</th></tr><tr><td>1</td><td>2</td></tr></table>" } };
        var h = OutputPresentation.For(htmlOnly);
        Assert.Equal("a\tb\t\n1\t2", h.Text);
        Assert.NotNull(h.Note);

        var md = new NotebookOutput { OutputType = NotebookOutput.DisplayData, Data = new JsonObject { ["text/markdown"] = "**b**", ["text/plain"] = "b" } };
        Assert.Equal(PresentationKind.Markdown, OutputPresentation.For(md).Kind);

        var err = new NotebookOutput { OutputType = NotebookOutput.Error, ErrorName = "E", ErrorValue = "v", Traceback = ["t1", "t2"] };
        Assert.Equal("t1\nt2", OutputPresentation.For(err).Text);
        Assert.Equal(PresentationKind.Error, OutputPresentation.For(err).Kind);

        var bad = new NotebookOutput { OutputType = NotebookOutput.DisplayData, Data = new JsonObject { ["image/png"] = "***", ["text/plain"] = "fallback" } };
        Assert.Equal("fallback", OutputPresentation.For(bad).Text);
    }
}
