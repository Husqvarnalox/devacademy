using DevAcademy.Application.Ports;
using DevAcademy.Content;
using DevAcademy.Domain;

namespace DevAcademy.Infrastructure;

/// <summary>Loads content from a directory containing catalog.yaml.</summary>
public sealed class FileContentRepository : IContentRepository
{
    private readonly string? _explicitRoot;
    private ContentLoadResult? _loaded;

    public FileContentRepository(string? contentRoot = null) => _explicitRoot = contentRoot;

    public string ContentRoot => _explicitRoot ?? FindContentRoot()
        ?? throw new DirectoryNotFoundException("Could not find a 'content' directory with catalog.yaml next to the executable or above the working directory");

    public ContentSet Current => Loaded.Content;

    public IReadOnlyList<ContentDiagnostic> Diagnostics => Loaded.Diagnostics;

    public void Reload() => _loaded = ContentLoader.Load(ContentRoot);

    private ContentLoadResult Loaded => _loaded ??= ContentLoader.Load(ContentRoot);

    /// <summary>`content` next to the executable, else the nearest `content/catalog.yaml` walking up from the working directory.</summary>
    public static string? FindContentRoot()
    {
        var beside = Path.Combine(AppContext.BaseDirectory, "content");
        if (File.Exists(Path.Combine(beside, "catalog.yaml"))) return beside;
        for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "content");
            if (File.Exists(Path.Combine(candidate, "catalog.yaml"))) return candidate;
        }
        return null;
    }
}
