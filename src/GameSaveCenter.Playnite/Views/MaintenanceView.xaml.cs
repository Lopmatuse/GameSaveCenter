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
            var stackProcess = width < 1060;
            MaintenanceProcessLayout.ColumnDefinitions[1].Width = stackProcess ? new GridLength(0) : new GridLength(14);
            MaintenanceProcessLayout.ColumnDefinitions[2].Width = stackProcess ? new GridLength(0) : new GridLength(330);
            MaintenanceProcessLayout.RowDefinitions[2].Height = stackProcess ? new GridLength(1, GridUnitType.Auto) : new GridLength(0);
            Grid.SetColumn(MaintenanceProcessInspector, stackProcess ? 0 : 2);
            Grid.SetColumnSpan(MaintenanceProcessInspector, stackProcess ? 3 : 1);
            Grid.SetRow(MaintenanceProcessInspector, stackProcess ? 2 : 1);
            MaintenanceProcessInspector.Margin = stackProcess ? new Thickness(0, 10, 0, 0) : new Thickness(0);
            var stackDevice = width < 1060;
            MaintenanceDeviceLayout.ColumnDefinitions[1].Width = stackDevice ? new GridLength(0) : new GridLength(14);
            MaintenanceDeviceLayout.ColumnDefinitions[2].Width = stackDevice ? new GridLength(0) : new GridLength(330);
            MaintenanceDeviceLayout.RowDefinitions[3].Height = stackDevice ? new GridLength(1, GridUnitType.Auto) : new GridLength(0);
            Grid.SetColumn(MaintenanceDeviceInspector, stackDevice ? 0 : 2);
            Grid.SetColumnSpan(MaintenanceDeviceInspector, stackDevice ? 3 : 1);
            Grid.SetRow(MaintenanceDeviceInspector, stackDevice ? 3 : 2);
            MaintenanceDeviceInspector.Margin = stackDevice ? new Thickness(0, 10, 0, 0) : new Thickness(0, 10, 0, 0);
            MaintenanceDeviceDecisionScrollViewer.MaxHeight = Math.Max(90, Math.Min(150, height * (compact ? 0.16 : 0.20)));
            MaintenanceRemoteRestoreScrollViewer.MaxHeight = Math.Max(120, Math.Min(210, height * (compact ? 0.22 : 0.28)));
        }
    }
}
