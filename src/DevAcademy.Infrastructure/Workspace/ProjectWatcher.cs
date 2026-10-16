namespace DevAcademy.Infrastructure.Workspace;

public enum ChangeKind { Created, Changed, Deleted, Renamed }

public sealed record FileChange(ChangeKind Kind, string FullPath, string? OldPath = null);

/// <summary>
/// Recursive file watcher with a short debounce. Changes inside default-ignored directories (.git, node_modules, bin, ...) are dropped so
/// a build does not flood the UI. <see cref="Changes"/> is raised on a thread-pool thread with a de-duplicated batch.
/// </summary>
public sealed class ProjectWatcher : IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private readonly string _root;
    private readonly object _gate = new();
    private readonly Dictionary<string, FileChange> _pending = new(StringComparer.Ordinal);
    private readonly Timer _timer;
    private readonly TimeSpan _debounce;
    private int _disposed;

    public ProjectWatcher(string root, TimeSpan? debounce = null)
    {
        _root = Path.GetFullPath(root);
        _debounce = debounce ?? TimeSpan.FromMilliseconds(150);
        _timer = new Timer(Flush, null, Timeout.Infinite, Timeout.Infinite);
        _watcher = new FileSystemWatcher(_root)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,
            InternalBufferSize = 64 * 1024,
        };
        _watcher.Created += (_, e) => Add(new FileChange(ChangeKind.Created, e.FullPath));
        _watcher.Changed += (_, e) => Add(new FileChange(ChangeKind.Changed, e.FullPath));
        _watcher.Deleted += (_, e) => Add(new FileChange(ChangeKind.Deleted, e.FullPath));
        _watcher.Renamed += (_, e) => Add(new FileChange(ChangeKind.Renamed, e.FullPath, e.OldFullPath));
        _watcher.Error += (_, _) => Add(new FileChange(ChangeKind.Changed, _root));
        _watcher.EnableRaisingEvents = true;
    }

    public event Action<IReadOnlyList<FileChange>>? Changes;

    private bool IsIgnored(string fullPath)
    {
        var rel = Path.GetRelativePath(_root, fullPath);
        if (rel.StartsWith("..", StringComparison.Ordinal)) return false;
        foreach (var part in rel.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            if (IgnoreRules.IsIgnoredDirectory(part)) return true;
        return false;
    }

    private void Add(FileChange change)
    {
        if (Volatile.Read(ref _disposed) == 1) return;
        if (IsIgnored(change.FullPath) && change.FullPath != _root) return;
        lock (_gate)
        {
            // Keep a Created/Deleted/Renamed over a later plain Changed for the same path.
            if (!_pending.TryGetValue(change.FullPath, out var existing) || change.Kind != ChangeKind.Changed || existing.Kind == ChangeKind.Changed)
                _pending[change.FullPath] = change;
        }
        try { _timer.Change(_debounce, Timeout.InfiniteTimeSpan); } catch (ObjectDisposedException) { }
    }

    private void Flush(object? _)
    {
        List<FileChange> batch;
        lock (_gate)
        {
            if (_pending.Count == 0) return;
            batch = [.. _pending.Values];
            _pending.Clear();
        }
        try { Changes?.Invoke(batch); }
        catch (Exception) { /* a faulty subscriber must not kill the timer thread */ }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1) return;
        _watcher.EnableRaisingEvents = false;
        _watcher.Dispose();
        _timer.Dispose();
    }
}
