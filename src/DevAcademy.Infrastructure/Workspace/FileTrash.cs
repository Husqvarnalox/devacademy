using System.Globalization;
using System.Runtime.InteropServices;

namespace DevAcademy.Infrastructure.Workspace;

/// <summary>Moves a file or directory to the operating system's trash so deletion is recoverable.</summary>
public interface IFileTrash
{
    /// <summary>False when there is no known trash on this platform; callers must then warn about permanent deletion.</summary>
    bool IsAvailable { get; }
    bool TryMoveToTrash(string path);
}

public static class FileTrash
{
    public static IFileTrash CreateForCurrentOs() =>
        OperatingSystem.IsMacOS() ? new MacTrash()
        : OperatingSystem.IsWindows() ? new WindowsTrash()
        : OperatingSystem.IsLinux() ? new FreedesktopTrash()
        : new NoTrash();
}

public sealed class NoTrash : IFileTrash
{
    public bool IsAvailable => false;
    public bool TryMoveToTrash(string path) => false;
}

/// <summary>macOS: moves into ~/.Trash under a unique name (Finder's "Put Back" is not available, the item is recoverable by hand).</summary>
public sealed class MacTrash(string? trashDirectory = null) : IFileTrash
{
    private readonly string _dir = trashDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".Trash");
    public bool IsAvailable => Directory.Exists(_dir);

    public bool TryMoveToTrash(string path)
    {
        try
        {
            var target = TrashNaming.Unique(_dir, Path.GetFileName(path.TrimEnd('/')));
            if (Directory.Exists(path)) Directory.Move(path, target); else File.Move(path, target);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }
}

/// <summary>Linux: the freedesktop.org trash specification for the home trash (files/ + info/*.trashinfo).</summary>
public sealed class FreedesktopTrash(string? dataHome = null) : IFileTrash
{
    private readonly string _root = Path.Combine(ResolveDataHome(dataHome), "Trash");

    private static string ResolveDataHome(string? explicitDir)
    {
        if (!string.IsNullOrEmpty(explicitDir)) return explicitDir;
        var xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        return !string.IsNullOrEmpty(xdg) ? xdg : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
    }

    public bool IsAvailable => true;

    public bool TryMoveToTrash(string path)
    {
        try
        {
            var files = Path.Combine(_root, "files");
            var info = Path.Combine(_root, "info");
            Directory.CreateDirectory(files);
            Directory.CreateDirectory(info);
            var full = Path.GetFullPath(path).TrimEnd('/');
            var target = TrashNaming.Unique(files, Path.GetFileName(full));
            var infoPath = Path.Combine(info, Path.GetFileName(target) + ".trashinfo");
            File.WriteAllText(infoPath,
                "[Trash Info]\nPath=" + Uri.EscapeDataString(full).Replace("%2F", "/") + "\nDeletionDate=" + DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture) + "\n");
            if (Directory.Exists(full)) Directory.Move(full, target); else File.Move(full, target);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }
}

/// <summary>Windows: SHFileOperation with FOF_ALLOWUNDO (Recycle Bin). Unverified on a real Windows machine in this repository's CI.</summary>
public sealed class WindowsTrash : IFileTrash
{
    public bool IsAvailable => OperatingSystem.IsWindows();

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 8)]
    private struct ShFileOpStruct
    {
        public IntPtr Hwnd;
        public uint Func;
        public string From;
        public string? To;
        public ushort Flags;
        public int AnyOperationsAborted;
        public IntPtr NameMappings;
        public string? ProgressTitle;
    }

    [DllImport("shell32.dll", EntryPoint = "SHFileOperationW", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref ShFileOpStruct op);

    public bool TryMoveToTrash(string path)
    {
        if (!OperatingSystem.IsWindows()) return false;
        const uint FoDelete = 3;
        const ushort FofSilent = 0x4, FofNoConfirmation = 0x10, FofAllowUndo = 0x40, FofNoErrorUi = 0x400;
        try
        {
            var op = new ShFileOpStruct
            {
                Func = FoDelete,
                From = Path.GetFullPath(path) + "\0\0",
                Flags = (ushort)(FofAllowUndo | FofNoConfirmation | FofSilent | FofNoErrorUi),
            };
            return SHFileOperation(ref op) == 0 && op.AnyOperationsAborted == 0;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or MarshalDirectiveException) { return false; }
    }
}

internal static class TrashNaming
{
    public static string Unique(string directory, string name)
    {
        var candidate = Path.Combine(directory, name);
        if (!File.Exists(candidate) && !Directory.Exists(candidate)) return candidate;
        var stem = Path.GetFileNameWithoutExtension(name);
        var ext = Path.GetExtension(name);
        for (var i = 2; ; i++)
        {
            candidate = Path.Combine(directory, $"{stem} {i}{ext}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate)) return candidate;
        }
    }
}
