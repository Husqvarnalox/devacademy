using System.Collections.ObjectModel;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using DevAcademy.Infrastructure.Workspace;

namespace DevAcademy.Desktop.ViewModels;

/// <summary>
/// One open file. The editor edits <see cref="Document"/> directly, so undo history survives switching tabs. The text is saved back
/// with the file's original BOM and line endings. A file is open in at most one tab.
/// </summary>
public sealed partial class EditorTabViewModel : ObservableObject
{
    private TextFileContent _original;

    public EditorTabViewModel(string fullPath, string root, TextFileContent loaded)
    {
        FullPath = fullPath;
        Root = root;
        _original = loaded;
        Document = new TextDocument(loaded.Text);
        Document.UndoStack.ClearAll();
        Document.UndoStack.MarkAsOriginalFile();
        // The undo stack records a change after TextChanged has fired, so re-evaluate when the update batch is finished too.
        Document.TextChanged += (_, _) => UpdateDirty();
        Document.UpdateFinished += (_, _) => UpdateDirty();
    }

    private void UpdateDirty() => IsDirty = !Document.UndoStack.IsOriginalFile;

    public string Root { get; }
    public TextDocument Document { get; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(Title), nameof(RelativePath))] private string _fullPath;
    [ObservableProperty] private bool _isDirty;
    [ObservableProperty] private bool _deletedOnDisk;
    /// <summary>The tab currently shown in its group (drives the tab highlight).</summary>
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private string? _saveError;
    /// <summary>How this tab's editor talks to a language server (null: none for this file).</summary>
    [ObservableProperty] private DevAcademy.Desktop.Controls.Lsp.LspTarget? _lsp;

    /// <summary>Breakpoints and paused line of this file (shared with the debugger).</summary>
    [ObservableProperty] private DevAcademy.Desktop.Controls.Debug.EditorDebugTarget? _debug;

    public string Title => Path.GetFileName(FullPath);
    public string RelativePath => Path.GetRelativePath(Root, FullPath).Replace('\\', '/');
    public string Fingerprint => _original.Fingerprint;
    public LineEnding LineEnding => _original.LineEnding;
    public bool HasBom => _original.HasBom;

    /// <summary>Writes the document atomically (temp + rename), keeping BOM and line endings. False (with <see cref="SaveError"/>) on failure.</summary>
    public async Task<bool> SaveAsync()
    {
        try
        {
            _original = await TextFileStore.SaveAsync(FullPath, Document.Text, _original);
            Document.UndoStack.MarkAsOriginalFile();
            IsDirty = false;
            DeletedOnDisk = false;
            SaveError = null;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SaveError = ex.Message;
            return false;
        }
    }

    /// <summary>Replaces the document text with what is on disk now and clears undo history and the dirty marker.</summary>
    public async Task<bool> ReloadAsync()
    {
        var read = await TextFileStore.ReadAsync(FullPath);
        if (read.Content is not { } content) return false;
        _original = content;
        Document.Text = content.Text;
        Document.UndoStack.ClearAll();
        Document.UndoStack.MarkAsOriginalFile();
        IsDirty = false;
        DeletedOnDisk = false;
        return true;
    }

    /// <summary>Remembers the current disk state as "seen" (the user chose to keep their version).</summary>
    public void AcceptDiskFingerprint(string fingerprint) => _original = _original with { Fingerprint = fingerprint };

    public void MovedTo(string newFullPath) => FullPath = newFullPath;
}

public sealed partial class EditorGroupViewModel : ObservableObject
{
    public ObservableCollection<EditorTabViewModel> Tabs { get; } = [];

    [ObservableProperty] private EditorTabViewModel? _activeTab;
    [ObservableProperty] private bool _isActive;

    public bool HasTabs => Tabs.Count > 0;

    /// <summary>Breakpoint target of the active tab (one hop for the view binding, so a group without tabs binds quietly).</summary>
    public DevAcademy.Desktop.Controls.Debug.EditorDebugTarget? ActiveDebug => ActiveTab?.Debug;
    public DevAcademy.Desktop.Controls.Lsp.LspTarget? ActiveLsp => ActiveTab?.Lsp;
    public AvaloniaEdit.Document.TextDocument? ActiveDocument => ActiveTab?.Document;
    public string? ActiveTitle => ActiveTab?.Title;

    partial void OnActiveTabChanged(EditorTabViewModel? oldValue, EditorTabViewModel? newValue)
    {
        if (oldValue is not null) { oldValue.IsSelected = false; oldValue.PropertyChanged -= OnActiveTabProperty; }
        if (newValue is not null) { newValue.IsSelected = true; newValue.PropertyChanged += OnActiveTabProperty; }
        OnPropertyChanged(nameof(ActiveDebug));
        OnPropertyChanged(nameof(ActiveLsp));
        OnPropertyChanged(nameof(ActiveDocument));
        OnPropertyChanged(nameof(ActiveTitle));
    }

    private void OnActiveTabProperty(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EditorTabViewModel.Debug)) OnPropertyChanged(nameof(ActiveDebug));
        else if (e.PropertyName == nameof(EditorTabViewModel.Lsp)) OnPropertyChanged(nameof(ActiveLsp));
        else if (e.PropertyName == nameof(EditorTabViewModel.Title)) OnPropertyChanged(nameof(ActiveTitle));
    }

    public EditorGroupViewModel() => Tabs.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasTabs));
}
