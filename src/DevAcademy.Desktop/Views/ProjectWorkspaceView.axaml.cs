using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using DevAcademy.Desktop.Controls;
using DevAcademy.Desktop.ViewModels;

namespace DevAcademy.Desktop.Views;

public partial class ProjectWorkspaceView : UserControl
{
    private ProjectWorkspaceViewModel? _vm;

    public ProjectWorkspaceView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Attach(DataContext as ProjectWorkspaceViewModel);
        this.FindControl<TreeView>("Tree")!.DoubleTapped += OnTreeDoubleTapped;
        this.FindControl<ListBox>("TextHits")!.DoubleTapped += OnHitDoubleTapped;
        this.FindControl<ListBox>("FileHits")!.DoubleTapped += OnFileHitDoubleTapped;
        this.FindControl<TextBox>("TextQueryBox")!.KeyDown += OnTextQueryKey;
        this.FindControl<TextBox>("FileQueryBox")!.KeyDown += OnFileQueryKey;
    }

    private void Attach(ProjectWorkspaceViewModel? vm)
    {
        if (_vm is not null) { _vm.FindRequested -= OnFind; _vm.GoToLineRequested -= OnGoToLine; }
        _vm = vm;
        if (_vm is not null) { _vm.FindRequested += OnFind; _vm.GoToLineRequested += OnGoToLine; }
    }

    private CodeEditorControl? EditorFor(EditorGroupViewModel group) =>
        this.GetVisualDescendants().OfType<CodeEditorControl>().FirstOrDefault(e => ReferenceEquals(e.DataContext, group));

    private void OnFind(bool replace)
    {
        if (_vm is { } vm) EditorFor(vm.ActiveGroup)?.OpenSearch(replace);
    }

    private void OnGoToLine(EditorTabViewModel tab, int line)
    {
        if (_vm is null) return;
        var group = _vm.Groups.FirstOrDefault(g => g.Tabs.Contains(tab));
        if (group is null) return;
        // The editor shows the group's active tab; make sure the target tab is the visible one first.
        if (!ReferenceEquals(group.ActiveTab, tab)) group.ActiveTab = tab;
        Avalonia.Threading.Dispatcher.UIThread.Post(() => EditorFor(group)?.GoToLine(line), Avalonia.Threading.DispatcherPriority.Background);
    }

    private void OnGroupPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_vm is not null && (sender as Control)?.Tag is EditorGroupViewModel g) _vm.ActivateGroup(g);
    }

    private void OnTabClick(object? sender, RoutedEventArgs e)
    {
        if (_vm is null || (sender as Control)?.DataContext is not EditorTabViewModel tab) return;
        var group = _vm.Groups.FirstOrDefault(g => g.Tabs.Contains(tab));
        if (group is not null) _vm.ActivateTab(group, tab);
    }

    private async void OnTabClose(object? sender, RoutedEventArgs e)
    {
        if (_vm is not null && (sender as Control)?.DataContext is EditorTabViewModel tab) await _vm.CloseTabAsync(tab);
    }

    private async void OnTreeDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (_vm?.Explorer.Selected is { IsDirectory: false, IsPlaceholder: false } node) await _vm.OpenFileAsync(node.FullPath);
    }

    private async void OnHitDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (_vm is not null && (sender as ListBox)?.SelectedItem is SearchHitItem hit) await _vm.OpenFileAsync(hit.Hit.FullPath, hit.Hit.Line);
    }

    private async void OnFileHitDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (_vm is not null && (sender as ListBox)?.SelectedItem is string rel) await _vm.OpenFileAsync(System.IO.Path.Combine(_vm.Root, rel));
    }

    private async void OnTextQueryKey(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || _vm is null) return;
        await _vm.Search.SearchTextAsync();
        e.Handled = true;
    }

    private void OnFileQueryKey(object? sender, KeyEventArgs e)
    {
        if (_vm is null) return;
        if (e.Key == Key.Escape) { _vm.Search.FileQuery = ""; _vm.Search.TextQuery = ""; }
    }

    private void OnCancelSearch(object? sender, RoutedEventArgs e) => _vm?.Search.Cancel();
}
