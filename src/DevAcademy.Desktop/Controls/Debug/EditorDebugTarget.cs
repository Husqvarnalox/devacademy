namespace DevAcademy.Desktop.Controls.Debug;

/// <summary>
/// What one editor shows for debugging: the breakpoint lines of its file and (while stopped) the line the program is paused on.
/// UI-free so it is shared by project tabs and lesson files and can be unit tested. Lines are 1-based.
/// </summary>
public sealed class EditorDebugTarget
{
    private readonly SortedSet<int> _lines = [];

    public EditorDebugTarget(string key) => Key = key;

    /// <summary>Identity of the file in its host (full path for projects, relative path for lessons).</summary>
    public string Key { get; set; }

    public IReadOnlyCollection<int> Breakpoints => _lines;
    public int? ExecutionLine { get; private set; }

    /// <summary>The breakpoint set changed (user toggled one, or an edit moved them). Persist and forward to a running session.</summary>
    public event Action? BreakpointsChanged;
    /// <summary>Anything visible changed; editors repaint their margin and current-line highlight.</summary>
    public event Action? Invalidated;

    public bool Has(int line) => _lines.Contains(line);

    public void Toggle(int line)
    {
        if (line < 1) return;
        if (!_lines.Remove(line)) _lines.Add(line);
        BreakpointsChanged?.Invoke();
        Invalidated?.Invoke();
    }

    /// <summary>Replaces the set silently (loading a saved session): no BreakpointsChanged.</summary>
    public void Load(IEnumerable<int> lines)
    {
        _lines.Clear();
        foreach (int l in lines) if (l >= 1) _lines.Add(l);
        Invalidated?.Invoke();
    }

    public void ClearAll()
    {
        if (_lines.Count == 0) return;
        _lines.Clear();
        BreakpointsChanged?.Invoke();
        Invalidated?.Invoke();
    }

    public void SetExecutionLine(int? line)
    {
        if (ExecutionLine == line) return;
        ExecutionLine = line;
        Invalidated?.Invoke();
    }

    /// <summary>
    /// Keeps breakpoints attached to their code when text is edited. <paramref name="line"/> is the line containing the edit offset,
    /// <paramref name="removedNewlines"/> / <paramref name="insertedNewlines"/> count line breaks in the replaced and new text,
    /// <paramref name="insertedAtLineStartEndsWithNewline"/> is true for Enter pressed at column 1 (the whole line moves down).
    /// Breakpoints on deleted lines disappear.
    /// </summary>
    public void ApplyEdit(int line, int removedNewlines, int insertedNewlines, bool insertedAtLineStartEndsWithNewline)
    {
        if (removedNewlines == 0 && insertedNewlines == 0) return;
        var next = new SortedSet<int>();
        foreach (int bp in _lines)
        {
            if (bp < line) next.Add(bp);
            else if (bp == line)
            {
                // Enter at column 1 pushes the line's content down; anywhere else the breakpoint stays on the first half.
                if (insertedAtLineStartEndsWithNewline && removedNewlines == 0) next.Add(bp + insertedNewlines);
                else next.Add(bp);
            }
            else if (bp <= line + removedNewlines) { /* the line was deleted */ }
            else next.Add(bp + insertedNewlines - removedNewlines);
        }
        if (next.SetEquals(_lines)) return;
        _lines.Clear();
        foreach (int l in next) _lines.Add(l);
        BreakpointsChanged?.Invoke();
        Invalidated?.Invoke();
    }
}
