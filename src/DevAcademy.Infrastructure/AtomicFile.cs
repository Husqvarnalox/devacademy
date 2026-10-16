namespace DevAcademy.Infrastructure;

public static class AtomicFile
{
    /// <summary>Writes to a temp file in the same directory, then moves it over the target.</summary>
    public static async Task WriteAllTextAsync(string path, string content, CancellationToken ct = default)
    {
        var full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var temp = full + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temp, content, ct);
            File.Move(temp, full, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    /// <summary>
    /// Writes bytes to a temp file in the same directory, then moves it over the target (atomic on one volume).
    /// An existing file's Unix permission bits are kept.
    /// </summary>
    public static async Task WriteAllBytesAsync(string path, byte[] content, CancellationToken ct = default)
    {
        var full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var temp = full + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(temp, content, ct);
            if (!OperatingSystem.IsWindows() && File.Exists(full))
            {
                try { File.SetUnixFileMode(temp, File.GetUnixFileMode(full)); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* keep default mode */ }
            }
            File.Move(temp, full, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }
}
