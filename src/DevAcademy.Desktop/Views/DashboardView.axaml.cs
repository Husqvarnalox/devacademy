using Avalonia.Controls;

namespace DevAcademy.Desktop.Views;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
        SizeChanged += (_, e) => Responsive.SideColumn(Columns, MainPane, SidePane, e.NewSize.Width, 320);
    }
}
