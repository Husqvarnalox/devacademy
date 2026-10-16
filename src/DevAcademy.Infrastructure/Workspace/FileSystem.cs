namespace DevAcademy.Infrastructure.Workspace;

/// <summary>Directories hidden from the tree and from searches by default (toggle in the UI).</summary>
public static class IgnoreRules
{
    public static readonly IReadOnlySet<string> DefaultDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".git", "node_modules", ".venv", "target", "bin", "obj", "__pycache__", ".pytest_cache", ".idea", ".vs",
    };

    public static bool IsIgnoredDirectory(string name) => DefaultDirectories.Contains(name);
}

public sealed record FileNode(string Name, string FullPath, bool IsDirectory)
{
    public bool IsIgnored => IsDirectory && IgnoreRules.IsIgnoredDirectory(Name);
}

/// <summary>Lazy directory listing: one level per call, directories first, then files, both in natural case-insensitive order.</summary>
public static class ProjectTree
{
    public static IReadOnlyList<FileNode> ListChildren(string directory, bool showIgnored)
    {
        var result = new List<FileNode>();
        try
        {
            foreach (var entry in new DirectoryInfo(directory).EnumerateFileSystemInfos())
            {
                var isDir = entry.Attributes.HasFlag(FileAttributes.Directory);
                // A symlinked directory is listed but can loop; it is shown as a leaf-like directory and only expanded on demand.
                if (isDir && !showIgnored && IgnoreRules.IsIgnoredDirectory(entry.Name)) continue;
                if (entry.Name == ".DS_Store") continue;
                result.Add(new FileNode(entry.Name, entry.FullName, isDir));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return []; }
        return result
            .OrderBy(n => n.IsDirectory ? 0 : 1)
            .ThenBy(n => n.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(n => n.Name, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Every file under <paramref name="root"/> (relative paths with '/'), skipping ignored directories unless asked.</summary>
    public static IEnumerable<string> EnumerateFiles(string root, bool showIgnored, CancellationToken ct = default)
    {
        var stack = new Stack<string>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var dir = stack.Pop();
            foreach (var node in ListChildren(dir, showIgnored))
            {
                if (node.IsDirectory)
                {
                    if (!IsSymlink(node.FullPath)) stack.Push(node.FullPath);
                }
                else yield return node.FullPath;
            }
        }
    }

    internal static bool IsSymlink(string path)
    {
        try { return new FileInfo(path).LinkTarget is not null; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }
}

public sealed record FileOpResult(bool Ok, string? Error = null, string? Path = null)
{
    public static FileOpResult Success(string path) => new(true, null, path);
    public static FileOpResult Fail(string error) => new(false, error);
}

/// <summary>Create, rename and delete inside a project. Names are validated so nothing can escape the parent directory.</summary>
public sealed class ProjectFileOps(IFileTrash trash)
{
    private static readonly char[] Invalid = ['/', '\\', ':', '*', '?', '"', '<', '>', '|', '\0'];

    public static string? ValidateName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Имя не может быть пустым.";
        if (name != name.Trim() || name.EndsWith('.')) return "Имя не должно начинаться или заканчиваться пробелом, а также заканчиваться точкой.";
        if (name is "." or "..") return "Недопустимое имя.";
        if (name.IndexOfAny(Invalid) >= 0 || name.Any(char.IsControl)) return "Имя содержит недопустимые символы (/ \\ : * ? \" < > |).";
        if (name.Length > 255) return "Имя слишком длинное.";
        return null;
    }

    public FileOpResult CreateFile(string parentDirectory, string name)
    {
        if (ValidateName(name) is { } e) return FileOpResult.Fail(e);
        var path = System.IO.Path.Combine(parentDirectory, name);
        if (File.Exists(path) || Directory.Exists(path)) return FileOpResult.Fail($"«{name}» уже существует.");
        try
        {
            using (new FileStream(path, FileMode.CreateNew, FileAccess.Write)) { }
            return FileOpResult.Success(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return FileOpResult.Fail(ex.Message); }
    }

    public FileOpResult CreateFolder(string parentDirectory, string name)
    {
        if (ValidateName(name) is { } e) return FileOpResult.Fail(e);
        var path = System.IO.Path.Combine(parentDirectory, name);
        if (File.Exists(path) || Directory.Exists(path)) return FileOpResult.Fail($"«{name}» уже существует.");
        try { Directory.CreateDirectory(path); return FileOpResult.Success(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return FileOpResult.Fail(ex.Message); }
    }

    public FileOpResult Rename(string path, string newName)
    {
        if (ValidateName(newName) is { } e) return FileOpResult.Fail(e);
        var parent = System.IO.Path.GetDirectoryName(path);
        if (parent is null) return FileOpResult.Fail("Нельзя переименовать корень.");
        var target = System.IO.Path.Combine(parent, newName);
        var caseOnly = string.Equals(target, path, StringComparison.OrdinalIgnoreCase);
        if (!caseOnly && (File.Exists(target) || Directory.Exists(target))) return FileOpResult.Fail($"«{newName}» уже существует.");
        try
        {
            if (Directory.Exists(path)) Directory.Move(path, target);
            else File.Move(path, target);
            return FileOpResult.Success(target);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return FileOpResult.Fail(ex.Message); }
    }

    /// <summary>True when the OS trash is available on this machine, so Delete is recoverable.</summary>
    public bool CanTrash => trash.IsAvailable;

    /// <summary>Moves to the OS trash; falls back to permanent deletion only when <paramref name="allowPermanent"/> is set.</summary>
    public FileOpResult Delete(string path, bool allowPermanent)
    {
        if (!File.Exists(path) && !Directory.Exists(path)) return FileOpResult.Fail("Файл уже удалён.");
        if (trash.IsAvailable && trash.TryMoveToTrash(path)) return FileOpResult.Success(path);
        if (!allowPermanent) return FileOpResult.Fail("Не удалось переместить в корзину.");
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
            else File.Delete(path);
            return FileOpResult.Success(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return FileOpResult.Fail(ex.Message); }
    }
}
