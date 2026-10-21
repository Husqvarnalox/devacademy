using Avalonia;
using Avalonia.Controls;

namespace DevAcademy.Desktop.Views;

/// <summary>Hub-page layout helper: a main column with a fixed side column that moves below the main one in narrow windows.</summary>
public static class Responsive
{
    /// <summary>Width of the view below which the side column is stacked under the main column.</summary>
    public const double StackWidth = 980;

    /// <summary>Arranges <paramref name="side"/> right of <paramref name="main"/> (wide) or below it (narrow). The grid needs two columns and two rows.</summary>
    public static void SideColumn(Grid grid, Control main, Control side, double viewWidth, double sideWidth)
    {
        var stacked = viewWidth < StackWidth;
        grid.ColumnDefinitions[1].Width = stacked ? new GridLength(0) : new GridLength(sideWidth);
        grid.ColumnSpacing = stacked ? 0 : 32;
        Grid.SetRow(main, 0);
        Grid.SetColumn(main, 0);
        Grid.SetRow(side, stacked ? 1 : 0);
        Grid.SetColumn(side, stacked ? 0 : 1);
    }
}
