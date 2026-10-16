using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DevAcademy.Debugging.Dap;

namespace DevAcademy.Desktop.ViewModels;

/// <summary>One row of the call stack.</summary>
public sealed record StackFrameItem(DapStackFrame Frame, string? HostKey)
{
    public string Name => Frame.Name;
    public string Location => Frame.Source is { } s
        ? $"{(s.Name ?? System.IO.Path.GetFileName(s.Path ?? "") ?? "?")}:{Frame.Line}"
        : "";
    /// <summary>Frames in the learner's own files are shown normally, library / runtime frames dimmed.</summary>
    public bool IsUserCode => HostKey is not null;
}

/// <summary>
/// A variable (or scope, or evaluated expression). Children are fetched from the adapter the first time the node is expanded,
/// through <c>variablesReference</c>; until then one placeholder child makes the tree show an expander.
/// </summary>
public partial class VariableNode : ObservableObject
{
    private readonly Func<int, Task<IReadOnlyList<DapVariable>>>? _loader;
    private int _reference;
    private bool _loaded;

    public VariableNode(string name, string value, string? type, int reference, Func<int, Task<IReadOnlyList<DapVariable>>>? loader)
    {
        _name = name;
        _value = value;
        Type = type;
        _reference = reference;
        _loader = loader;
        if (reference > 0 && loader is not null) Children.Add(Placeholder());
    }

    private VariableNode() { _name = ""; _value = ""; IsPlaceholder = true; }
    private static VariableNode Placeholder() => new();

    [ObservableProperty] private string _name;
    [ObservableProperty] private string _value;
    [ObservableProperty] private bool _isExpanded;
    [ObservableProperty] private bool _isError;

    public string? Type { get; private set; }
    public bool IsPlaceholder { get; }
    public ObservableCollection<VariableNode> Children { get; } = [];
    public string Tooltip => Type is { Length: > 0 } ? $"{Name}: {Type}" : Name;
    public bool IsLoaded => _loaded;

    /// <summary>Re-targets the node to a new value (a watch re-evaluated at the next stop); children are fetched again when expanded.</summary>
    public void Update(string value, string? type, int reference, bool isError = false)
    {
        Value = value;
        Type = type;
        IsError = isError;
        _reference = reference;
        _loaded = false;
        Children.Clear();
        if (reference > 0 && _loader is not null) Children.Add(Placeholder());
    }

    partial void OnIsExpandedChanged(bool value)
    {
        if (value && !_loaded && _reference > 0 && _loader is not null) _ = LoadChildrenAsync();
    }

    public async Task LoadChildrenAsync()
    {
        if (_loaded || _loader is null || _reference <= 0) return;
        _loaded = true;
        try
        {
            var vars = await _loader(_reference).ConfigureAwait(true);
            Children.Clear();
            foreach (var v in vars) Children.Add(new VariableNode(v.Name, v.Value, v.Type, v.VariablesReference, _loader));
        }
        catch (Exception ex) when (ex is DapException or OperationCanceledException or ObjectDisposedException or InvalidOperationException)
        {
            Children.Clear();
            Children.Add(new VariableNode("", ex.Message, null, 0, null) { IsError = true });
        }
    }
}

public enum ConsoleKind { Output, Error, Echo, Result, Info }

public sealed record ConsoleLine(string Text, ConsoleKind Kind)
{
    public bool IsError => Kind == ConsoleKind.Error;
    public bool IsEcho => Kind == ConsoleKind.Echo;
    public bool IsInfo => Kind == ConsoleKind.Info;
}
