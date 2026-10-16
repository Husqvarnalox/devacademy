using DevAcademy.Domain;

namespace DevAcademy.Content.Tests;

public class TemplateLoaderTests
{
    private static void Write(Fixture f, string rel, string text)
    {
        var p = Path.Combine(f.Root, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(p)!);
        File.WriteAllText(p, text);
    }

    private static void Template(Fixture f, string id, string yaml, bool withFiles = true)
    {
        Write(f, $"templates/{id}/template.yaml", yaml);
        if (withFiles) Write(f, $"templates/{id}/files/main.py", "print('{{name}}')\n");
    }

    [Fact]
    public void Valid_template_is_loaded_with_post_create_as_data()
    {
        using var f = new Fixture();
        Template(f, "t1", "id: t1\ntitle: T\nlanguageId: python\ndescription: d\nfiles: files\npostCreate:\n  - python -m venv .venv\n");
        var r = f.Load();
        var t = Assert.Single(r.Content.Templates);
        Assert.Equal("python", t.LanguageId);
        Assert.Equal(["python -m venv .venv"], t.PostCreate);
        Assert.True(Directory.Exists(t.FilesDirectory));
        Assert.DoesNotContain(r.Diagnostics, d => d.Path.StartsWith("templates"));
    }

    [Fact]
    public void Missing_files_directory_is_an_error()
    {
        using var f = new Fixture();
        Template(f, "t1", "id: t1\ntitle: T\nlanguageId: go\n", withFiles: false);
        Assert.Contains(f.Load().Diagnostics, d => d.Severity == DiagnosticSeverity.Error && d.Path == "templates/t1/template.yaml" && d.Message.Contains("files directory"));
        Assert.Empty(f.Load().Content.Templates);
    }

    [Fact]
    public void Id_must_match_directory_and_required_fields_are_checked()
    {
        using var f = new Fixture();
        Template(f, "t1", "id: other\ntitle: T\nlanguageId: go\n");
        Template(f, "t2", "id: t2\nlanguageId: go\n");
        var d = f.Load().Diagnostics;
        Assert.Contains(d, x => x.Message.Contains("must match the directory name"));
        Assert.Contains(d, x => x.Path == "templates/t2/template.yaml" && x.Message.Contains("'title' is required"));
    }

    [Fact]
    public void Files_path_cannot_escape_the_template_directory()
    {
        using var f = new Fixture();
        Template(f, "t1", "id: t1\ntitle: T\nlanguageId: go\nfiles: ../../courses\n");
        Assert.Contains(f.Load().Diagnostics, d => d.Message.Contains("inside the template directory"));
    }
}
