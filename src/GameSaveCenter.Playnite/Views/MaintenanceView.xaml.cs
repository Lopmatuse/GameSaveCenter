using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace GameSaveCenter.Playnite.Views
{
    public partial class MaintenanceView : UserControl
    {
        public MaintenanceView() => InitializeComponent();
        public UniformGrid DiagnosticHealthPanelElement => DiagnosticHealthPanel;
        public DataGrid FindingsGridElement => FindingsGrid;

        public void ApplyResponsiveLayout(double width, double height)
        {
            DiagnosticHealthPanel.Columns = width >= 1320 ? 4 : width >= 980 ? 2 : 1;
            DiagnosticHealthPanel.Visibility = height >= 620 ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
