using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using DevAcademy.Desktop.Markdown;
using DevAcademy.Desktop.ViewModels;

namespace DevAcademy.Desktop.Views;

/// <summary>
/// Native comparison grid: criteria as rows, subjects as columns. Wide: every subject. Narrow: one subject at a time,
/// chosen in the picker (the decision lives in <see cref="ComparisonViewModel.IsNarrow"/>).
/// </summary>
public partial class ComparisonView : UserControl
{
    private ComparisonViewModel? _vm;

    public ComparisonView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Attach();
        SizeChanged += (_, e) => _vm?.UpdateWidth(e.NewSize.Width - 48);
    }

    private void Attach()
    {
        if (_vm is not null) _vm.PropertyChanged -= OnChanged;
        _vm = DataContext as ComparisonViewModel;
        if (_vm is null) return;
        _vm.PropertyChanged += OnChanged;
        if (Bounds.Width > 0) _vm.UpdateWidth(Bounds.Width - 48);
        Rebuild();
    }

    private void OnChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ComparisonViewModel.VisibleSubjects)) Rebuild();
    }

    private void Rebuild()
    {
        var grid = this.FindControl<Grid>("TheGrid")!;
        grid.Children.Clear();
        grid.RowDefinitions.Clear();
        grid.ColumnDefinitions.Clear();
        if (_vm is null) return;

        var subjects = _vm.VisibleSubjects;
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(ComparisonViewModel.CriterionColumn * 0.8, GridUnitType.Pixel)));
        foreach (var _ in subjects) grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));

        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        for (var c = 0; c < subjects.Count; c++)
        {
            var s = subjects[c];
            var head = new StackPanel { Spacing = 2, Margin = new Thickness(8, 6) };
            head.Children.Add(new TextBlock { Text = s.Header, FontWeight = Avalonia.Media.FontWeight.SemiBold, TextWrapping = Avalonia.Media.TextWrapping.Wrap });
            head.Children.Add(new TextBlock { Text = s.KindText, Classes = { "caption" } });
            Place(grid, Wrap(head, true), 0, c + 1);
        }

        for (var r = 0; r < _vm.Rows.Count; r++)
        {
            var row = _vm.Rows[r];
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var title = new StackPanel { Spacing = 2, Margin = new Thickness(8, 6) };
            title.Children.Add(new TextBlock { Text = row.Title, FontWeight = Avalonia.Media.FontWeight.SemiBold, TextWrapping = Avalonia.Media.TextWrapping.Wrap });
            title.Children.Add(new TextBlock { Text = row.Description, Classes = { "caption" }, TextWrapping = Avalonia.Media.TextWrapping.Wrap });
            Place(grid, Wrap(title, true), r + 1, 0);
            for (var c = 0; c < subjects.Count; c++)
            {
                var cell = row.Cells.First(x => x.SubjectId == subjects[c].Id);
                var viewer = new MarkdownViewer { Markdown = cell.Markdown, LinkCommand = _vm.OpenLinkCommand, Margin = new Thickness(8, 4) };
                Place(grid, Wrap(viewer, false), r + 1, c + 1);
            }
        }
    }

    private static Border Wrap(Control child, bool subtle)
    {
        var b = new Border { Child = child, BorderThickness = new Thickness(0, 0, 1, 1) };
        b.Classes.Add(subtle ? "gridhead" : "gridcell");
        return b;
    }

    private static void Place(Grid grid, Control c, int row, int col)
    {
        Grid.SetRow(c, row);
        Grid.SetColumn(c, col);
        grid.Children.Add(c);
    }
}
