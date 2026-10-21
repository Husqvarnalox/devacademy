using Avalonia.Controls;

namespace DevAcademy.Desktop.Views;

public partial class ProblemsView : UserControl
{
    public ProblemsView()
    {
        InitializeComponent();
        SizeChanged += (_, e) => Responsive.SideColumn(Columns, TasksPane, SidePane, e.NewSize.Width, 340);
    }
}
