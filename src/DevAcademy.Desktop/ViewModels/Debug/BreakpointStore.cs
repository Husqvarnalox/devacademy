using DevAcademy.Desktop.Controls.Debug;

namespace DevAcademy.Desktop.ViewModels;

/// <summary>
/// Breakpoints of one host (a project or a lesson), one <see cref="EditorDebugTarget"/> per file key, whether or not the file is open.
/// Keys are project-relative paths with '/'. Changes are reported with the key so a running session can resend that file.
/// </summary>
public sealed class BreakpointStore
{
    private readonly Dictionary<string, EditorDebugTarget> _targets = new(StringComparer.Ordinal);

    public event Action<string>? Changed;

    public EditorDebugTarget Get(string key)
    {
        if (_targets.TryGetValue(key, out var t)) return t;
        t = new EditorDebugTarget(key);
        t.BreakpointsChanged += () => Changed?.Invoke(key);
        _targets[key] = t;
        return t;
    }

    public IEnumerable<KeyValuePair<string, EditorDebugTarget>> All => _targets;

    public bool HasAny => _targets.Values.Any(t => t.Breakpoints.Count > 0);

    /// <summary>Non-empty breakpoint sets, for persisting.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<int>> Snapshot() =>
        _targets.Where(kv => kv.Value.Breakpoints.Count > 0)
            .ToDictionary(kv => kv.Key, kv => (IReadOnlyList<int>)kv.Value.Breakpoints.ToList(), StringComparer.Ordinal);

    /// <summary>Loads saved breakpoints silently (no <see cref="Changed"/>).</summary>
    public void Load(IReadOnlyDictionary<string, IReadOnlyList<int>>? saved)
    {
        if (saved is null) return;
        foreach (var (key, lines) in saved) Get(key).Load(lines);
    }

    /// <summary>A file or folder was renamed: breakpoints of it (and of files below it) follow to the new key.</summary>
    public void RenamePrefix(string oldKey, string newKey)
    {
        foreach (var key in _targets.Keys.Where(k => k == oldKey || k.StartsWith(oldKey + "/", StringComparison.Ordinal)).ToList())
        {
            var t = _targets[key];
            var lines = t.Breakpoints.ToList();
            string next = newKey + key[oldKey.Length..];
            t.Load([]);
            _targets.Remove(key);
            if (lines.Count == 0) continue;
            Get(next).Load(lines);
            Changed?.Invoke(next);
        }
    }

    /// <summary>A file or folder was deleted.</summary>
    public void RemovePrefix(string key)
    {
        bool any = false;
        foreach (var k in _targets.Keys.Where(k => k == key || k.StartsWith(key + "/", StringComparison.Ordinal)).ToList())
        {
            any |= _targets[k].Breakpoints.Count > 0;
            _targets[k].Load([]);
            _targets.Remove(k);
        }
        if (any) Changed?.Invoke(key);
    }

    public void ClearExecution()
    {
        foreach (var t in _targets.Values) t.SetExecutionLine(null);
    }
}
