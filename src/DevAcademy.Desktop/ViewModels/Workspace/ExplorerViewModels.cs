using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DevAcademy.Infrastructure.Workspace;

namespace DevAcademy.Desktop.ViewModels;

/// <summary>A file or directory in the project tree. Directory children are listed lazily, the first time the node is expanded.</summary>
public sealed partial class TreeNodeViewModel : ObservableObject
{
    private readonly ExplorerViewModel? _owner;
    private bool _loaded;

    public TreeNodeViewModel(ExplorerViewModel owner, FileNode node)
    {
        _owner = owner;
        Name = node.Name;
        FullPath = node.FullPath;
        IsDirectory = node.IsDirectory;
        IsIgnored = node.IsIgnored;
        if (IsDirectory) Children.Add(Placeholder());
    }

    private TreeNodeViewModel()
    {
        Name = "";
        FullPath = "";
        IsPlaceholder = true;
    }

    private static TreeNodeViewModel Placeholder() => new();

    [ObservableProperty, NotifyPropertyChangedFor(nameof(Name))] private string _fullPath;
    [ObservableProperty] private bool _isExpanded;

    public string Name { get; private set; }
    public bool IsDirectory { get; }
    public bool IsIgnored { get; }
    public bool IsPlaceholder { get; }
    public ObservableCollection<TreeNodeViewModel> Children { get; } = [];
    public bool IsLoaded => _loaded;

    partial void OnIsExpandedChanged(bool value)
    {
        if (value && IsDirectory && !_loaded) Load();
    }

    internal void Load()
    {
        if (_owner is null || !IsDirectory) return;
        _loaded = true;
        Sync(_owner.List(FullPath));
    }

    /// <summary>Replaces the children with <paramref name="fresh"/>, reusing existing nodes (and their expansion state) for unchanged paths.</summary>
    internal void Sync(IReadOnlyList<FileNode> fresh)
    {
        if (_owner is null) return;
        var existing = Children.Where(c => !c.IsPlaceholder).ToDictionary(c => c.FullPath, StringComparer.Ordinal);
        var next = fresh.Select(f => existing.TryGetValue(f.FullPath, out var n) ? n : new TreeNodeViewModel(_owner, f)).ToList();
        if (Children.Count == next.Count && Children.Select(c => c.FullPath).SequenceEqual(next.Select(n => n.FullPath))) return;
        Children.Clear();
        foreach (var n in next) Children.Add(n);
    }

    internal void Rename(string newName, string newPath)
    {
        Name = newName;
        FullPath = newPath;
    }

    internal TreeNodeViewModel? Find(string path)
    {
        if (string.Equals(FullPath, path, StringComparison.Ordinal)) return this;
        if (!_loaded) return null;
        return Children.Select(c => c.Find(path)).FirstOrDefault(n => n is not null);
    }
}

public sealed partial class ExplorerViewModel(string root) : ObservableObject
{
    public string Root { get; } = root;
    public ObservableCollection<TreeNodeViewModel> Roots { get; } = [];

    [ObservableProperty] private bool _showIgnored;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasSelection))] private TreeNodeViewModel? _selected;

    private TreeNodeViewModel? _rootNode;

    public bool HasSelection => Selected is not null;

    internal IReadOnlyList<FileNode> List(string dir) => ProjectTree.ListChildren(dir, ShowIgnored);

    /// <summary>The directory new files and folders go into: the selected folder, the parent of the selected file, or the project root.</summary>
    public string TargetDirectory =>
        Selected is null ? Root : Selected.IsDirectory ? Selected.FullPath : Path.GetDirectoryName(Selected.FullPath) ?? Root;

    public void Load()
    {
        _rootNode = new TreeNodeViewModel(this, new FileNode(Path.GetFileName(Root.TrimEnd(Path.DirectorySeparatorChar)), Root, true));
        _rootNode.IsExpanded = true;
        Roots.Clear();
        Roots.Add(_rootNode);
    }

    partial void OnShowIgnoredChanged(bool value) => Refresh();

    /// <summary>Re-lists every loaded directory (used when the toggle changes).</summary>
    public void Refresh()
    {
        if (_rootNode is null) return;
        void Walk(TreeNodeViewModel n)
        {
            if (!n.IsDirectory || !n.IsLoaded) return;
            n.Sync(List(n.FullPath));
            foreach (var c in n.Children.ToList()) Walk(c);
        }
        Walk(_rootNode);
    }

    /// <summary>Re-lists the given directories if they are loaded; unknown or collapsed ones are ignored.</summary>
    public void RefreshDirectories(IEnumerable<string> directories)
    {
        if (_rootNode is null) return;
        foreach (var dir in directories.Distinct(StringComparer.Ordinal))
        {
            var node = _rootNode.Find(dir);
            if (node is { IsDirectory: true, IsLoaded: true })
            {
                if (Directory.Exists(dir)) node.Sync(List(dir));
                else if (_rootNode.Find(Path.GetDirectoryName(dir) ?? "") is { IsLoaded: true } parent) parent.Sync(List(parent.FullPath));
            }
        }
    }

    public TreeNodeViewModel? Find(string path) => _rootNode?.Find(path);

    /// <summary>Expands the tree down to <paramref name="path"/> and selects it.</summary>
    public void Reveal(string path)
    {
        if (_rootNode is null) return;
        var rel = Path.GetRelativePath(Root, path);
        if (rel.StartsWith("..", StringComparison.Ordinal)) return;
        var node = _rootNode;
        var current = Root;
        foreach (var part in rel.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            node.IsExpanded = true;
            current = Path.Combine(current, part);
            var next = node.Children.FirstOrDefault(c => c.FullPath == current);
            if (next is null) return;
            node = next;
        }
        Selected = node;
    }
}

public sealed record SearchHitItem(SearchHit Hit)
{
    public string Display => $"{Hit.RelativePath}:{Hit.Line}";
    public string Preview => Hit.LineText.Trim();
}

/// <summary>File-name search (as you type) and cancellable text search over the project.</summary>
public sealed partial class WorkspaceSearchViewModel(string root, Func<bool> showIgnored) : ObservableObject
{
    private readonly SynchronizationContext? _ui = SynchronizationContext.Current;
    private CancellationTokenSource? _nameCts;
    private CancellationTokenSource? _textCts;

    [ObservableProperty] private string _fileQuery = "";
    [ObservableProperty] private string _textQuery = "";
    [ObservableProperty] private bool _matchCase;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanCancel))] private bool _isSearching;
    [ObservableProperty] private string _status = "";

    public ObservableCollection<string> FileResults { get; } = [];
    public ObservableCollection<SearchHitItem> TextResults { get; } = [];
    public bool CanCancel => IsSearching;

    partial void OnFileQueryChanged(string value) => _ = RunFileSearchAsync(value);

    public async Task RunFileSearchAsync(string query)
    {
        _nameCts?.Cancel();
        var cts = _nameCts = new CancellationTokenSource();
        if (string.IsNullOrWhiteSpace(query)) { FileResults.Clear(); return; }
        try
        {
            var results = await Task.Run(() => WorkspaceSearch.FindFiles(root, query, showIgnored(), 50, cts.Token), cts.Token);
            if (cts.IsCancellationRequested) return;
            FileResults.Clear();
            foreach (var r in results) FileResults.Add(r);
        }
        catch (OperationCanceledException) { }
    }

    public async Task SearchTextAsync()
    {
        _textCts?.Cancel();
        var cts = _textCts = new CancellationTokenSource();
        TextResults.Clear();
        if (string.IsNullOrEmpty(TextQuery)) { Status = ""; return; }
        IsSearching = true;
        Status = "Поиск…";
        var pending = new List<SearchHit>();
        var gate = new object();
        void Flush()
        {
            List<SearchHit> batch;
            lock (gate) { batch = [.. pending]; pending.Clear(); }
            if (cts.IsCancellationRequested) return;
            foreach (var h in batch) TextResults.Add(new SearchHitItem(h));
        }
        void Post(Action a) { if (_ui is null) a(); else _ui.Post(_ => a(), null); }
        try
        {
            var options = new SearchOptions(CaseSensitive: MatchCase, ShowIgnored: showIgnored());
            var count = await WorkspaceSearch.SearchTextAsync(root, TextQuery, options, hit =>
            {
                bool flush;
                lock (gate) { pending.Add(hit); flush = pending.Count >= 50; }
                if (flush) Post(Flush);
            }, cts.Token);
            Flush();
            Status = count >= options.MaxHits ? $"Показаны первые {count}" : $"Найдено: {count}";
        }
        catch (OperationCanceledException)
        {
            Status = "Поиск отменён";
        }
        finally
        {
            if (ReferenceEquals(_textCts, cts)) IsSearching = false;
        }
    }

    public void Cancel() => _textCts?.Cancel();
}
