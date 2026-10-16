using DevAcademy.Desktop.Controls.Debug;
using DevAcademy.Desktop.ViewModels;

namespace DevAcademy.Desktop.Tests;

public class EditorDebugTargetTests
{
    private static EditorDebugTarget With(params int[] lines)
    {
        var t = new EditorDebugTarget("a.py");
        t.Load(lines);
        return t;
    }

    [Fact]
    public void Toggle_adds_and_removes_and_raises_the_events()
    {
        var t = new EditorDebugTarget("a.py");
        int changed = 0, invalid = 0;
        t.BreakpointsChanged += () => changed++;
        t.Invalidated += () => invalid++;
        t.Toggle(3);
        Assert.True(t.Has(3));
        t.Toggle(3);
        Assert.False(t.Has(3));
        t.Toggle(0);   // outside the document
        Assert.Equal((2, 2), (changed, invalid));
    }

    [Fact]
    public void Load_is_silent_for_persistence_but_repaints()
    {
        var t = new EditorDebugTarget("a.py");
        int changed = 0, invalid = 0;
        t.BreakpointsChanged += () => changed++;
        t.Invalidated += () => invalid++;
        t.Load([5, 2, 2, -1]);
        Assert.Equal([2, 5], t.Breakpoints);
        Assert.Equal((0, 1), (changed, invalid));
    }

    [Theory]
    [InlineData(1, 0, 1, false, new[] { 2, 6, 11 }, new[] { 3, 7, 12 })]   // newline typed at the end of line 1 pushes everything below down
    [InlineData(5, 0, 1, false, new[] { 2, 5, 6 }, new[] { 2, 5, 7 })]      // typing Enter inside line 5 keeps its breakpoint on the first half
    [InlineData(5, 0, 1, true, new[] { 2, 5, 6 }, new[] { 2, 6, 7 })]       // Enter at column 1 moves the whole line (and its breakpoint) down
    [InlineData(3, 2, 0, false, new[] { 2, 4, 5, 8 }, new[] { 2, 6 })]      // deleting two line breaks removes the lines swallowed by the edit
    [InlineData(4, 0, 0, false, new[] { 4 }, new[] { 4 })]                  // edits inside one line change nothing
    public void Edits_keep_breakpoints_on_their_statement(int line, int removed, int inserted, bool enterAtStart, int[] before, int[] after)
    {
        var t = With(before);
        t.ApplyEdit(line, removed, inserted, enterAtStart);
        Assert.Equal(after, t.Breakpoints);
    }

    [Fact]
    public void Execution_line_is_independent_of_breakpoints()
    {
        var t = With(3);
        int invalid = 0;
        t.Invalidated += () => invalid++;
        t.SetExecutionLine(3);
        t.SetExecutionLine(3);
        t.SetExecutionLine(null);
        Assert.Equal(2, invalid);
        Assert.True(t.Has(3));
    }

    [Fact]
    public void Store_renames_removes_and_snapshots_by_prefix()
    {
        var store = new BreakpointStore();
        store.Get("src/a.py").Toggle(1);
        store.Get("src/sub/b.py").Toggle(2);
        store.Get("other.py").Toggle(3);
        Assert.Equal(3, store.Snapshot().Count);

        store.RenamePrefix("src", "lib");
        var snap = store.Snapshot();
        Assert.Equal([1], snap["lib/a.py"]);
        Assert.Equal([2], snap["lib/sub/b.py"]);
        Assert.Equal([3], snap["other.py"]);
        Assert.False(snap.ContainsKey("src/a.py"));

        store.RemovePrefix("lib");
        Assert.Equal(["other.py"], store.Snapshot().Keys);
        Assert.True(store.HasAny);
    }
}
