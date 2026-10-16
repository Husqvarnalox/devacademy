using DevAcademy.Infrastructure;

namespace DevAcademy.Infrastructure.Tests;

public class ContentRootTests : IDisposable
{
    private readonly string _tmp = Path.Combine(Path.GetTempPath(), "da-root-" + Guid.NewGuid().ToString("N"));

    public ContentRootTests() => Directory.CreateDirectory(_tmp);

    public void Dispose() => Directory.Delete(_tmp, true);

    private string MakeContent(string parent)
    {
        var dir = Path.Combine(_tmp, parent, "content");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "catalog.yaml"), "x");
        return dir;
    }

    [Fact]
    public void Finds_content_next_to_the_executable()
    {
        var content = MakeContent("flat");
        Assert.Equal(content, FileContentRepository.FindContentRoot(Path.Combine(_tmp, "flat"), _tmp));
    }

    [Fact]
    public void Finds_content_in_macos_bundle_resources()
    {
        MakeContent("App.app/Contents/Resources");
        var macos = Path.Combine(_tmp, "App.app", "Contents", "MacOS");
        Directory.CreateDirectory(macos);
        var found = FileContentRepository.FindContentRoot(macos + Path.DirectorySeparatorChar, Path.Combine(_tmp, "elsewhere"));
        Assert.Equal(Path.Combine(_tmp, "App.app", "Contents", "Resources", "content"), found);
    }

    [Fact]
    public void Falls_back_to_walking_up_from_the_working_directory_then_null()
    {
        var content = MakeContent("repo");
        var deep = Path.Combine(_tmp, "repo", "a", "b");
        Directory.CreateDirectory(deep);
        var empty = Path.Combine(_tmp, "empty");
        Directory.CreateDirectory(empty);
        Assert.Equal(content, FileContentRepository.FindContentRoot(empty, deep));
        // A working directory with no content above it (inside the temp dir only) must not match anything unrelated.
        Assert.Null(FileContentRepository.FindContentRoot(empty, empty));
    }
}
