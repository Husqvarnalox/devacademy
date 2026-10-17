using System.Text;

namespace DevAcademy.Notebooks.Nbformat;

/// <summary>Reads and writes .ipynb files; saving is atomic (temp file in the same directory, then rename over the target).</summary>
public static class NotebookStore
{
    public const long MaxBytes = 128L * 1024 * 1024;

    public static async Task<NotebookDocument> LoadAsync(string path, CancellationToken ct = default)
    {
        var info = new FileInfo(path);
        if (info.Length > MaxBytes) throw new NotebookFormatException(EngineMessages.TooLarge(MaxBytes / (1024 * 1024)));
        var text = await File.ReadAllTextAsync(path, new UTF8Encoding(false), ct).ConfigureAwait(false);
        return NotebookSerializer.Parse(text);
    }

    public static async Task SaveAsync(string path, NotebookDocument doc, CancellationToken ct = default)
    {
        var json = NotebookSerializer.Serialize(doc);
        var dir = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(dir);
        var tmp = Path.Combine(dir, "." + Path.GetFileName(path) + "." + Guid.NewGuid().ToString("N")[..8] + ".tmp");
        try
        {
            await File.WriteAllTextAsync(tmp, json, new UTF8Encoding(false), ct).ConfigureAwait(false);
            File.Move(tmp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    /// <summary>Creates a new empty notebook file; fails when the file exists.</summary>
    public static async Task<NotebookDocument> CreateAsync(string path, CancellationToken ct = default)
    {
        if (File.Exists(path)) throw new IOException("The file already exists: " + path);
        var doc = NotebookDocument.CreateNew();
        await SaveAsync(path, doc, ct).ConfigureAwait(false);
        return doc;
    }
}
