using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System;

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
            // Health cards remain useful context even in compact windows. MaintenancePageScrollViewer
            // handles overflow and prevents the cards from disappearing during resize.
            DiagnosticHealthPanel.Visibility = Visibility.Visible;
            var compact = height < 760 || width < 980;
            MaintenanceDeviceDecisionScrollViewer.MaxHeight = Math.Max(90, Math.Min(150, height * (compact ? 0.16 : 0.20)));
            MaintenanceRemoteRestoreScrollViewer.MaxHeight = Math.Max(120, Math.Min(210, height * (compact ? 0.22 : 0.28)));
        }
    }
}
