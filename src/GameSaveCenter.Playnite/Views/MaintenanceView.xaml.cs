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
            // Health cards remain useful context even in compact windows. Grid star rows keep
            // diagnostics tables finite while their own controls handle overflow.
            DiagnosticHealthPanel.Visibility = Visibility.Visible;
            var compact = height < 760 || width < 980;
            var stackDiagnostics = width < 1060;
            MaintenanceDiagnosticsLayout.ColumnDefinitions[1].Width = stackDiagnostics ? new GridLength(0) : new GridLength(14);
            MaintenanceDiagnosticsLayout.ColumnDefinitions[2].Width = stackDiagnostics ? new GridLength(0) : new GridLength(330);
            MaintenanceDiagnosticsLayout.RowDefinitions[1].Height = stackDiagnostics ? new GridLength(1, GridUnitType.Auto) : new GridLength(0);
            Grid.SetColumn(MaintenanceDiagnosticDetails, stackDiagnostics ? 0 : 2);
            Grid.SetColumnSpan(MaintenanceDiagnosticDetails, stackDiagnostics ? 3 : 1);
            Grid.SetRow(MaintenanceDiagnosticDetails, stackDiagnostics ? 1 : 0);
            MaintenanceDiagnosticDetails.Margin = stackDiagnostics ? new Thickness(0, 10, 0, 0) : new Thickness(0);
            MaintenanceDeviceDecisionScrollViewer.MaxHeight = Math.Max(90, Math.Min(150, height * (compact ? 0.16 : 0.20)));
            MaintenanceRemoteRestoreScrollViewer.MaxHeight = Math.Max(120, Math.Min(210, height * (compact ? 0.22 : 0.28)));
        }
    }
}
