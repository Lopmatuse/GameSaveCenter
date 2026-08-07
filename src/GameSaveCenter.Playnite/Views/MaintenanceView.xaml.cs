using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System;

namespace GameSaveCenter.Playnite.Views
{
    public partial class MaintenanceView : UserControl
    {
        public MaintenanceView()
        {
            InitializeComponent();

            // Loaded is a direct WPF event. Re-assert the explicit local header styles once
            // per grid so generated headers never fall back to a Playnite host default.
            // The XAML HeaderStyle declarations own the theme; no visual-tree scanning here.
            FindingsGrid.Loaded += DataGridLoaded;
            MaintenanceDeviceGrid.Loaded += DataGridLoaded;
            MaintenanceAuditFindingsGrid.Loaded += DataGridLoaded;
            MaintenanceAuditLogGrid.Loaded += DataGridLoaded;
        }

        private void DataGridLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is DataGrid grid)
                ApplyGridHeaderTheme(grid);
        }

        private void ApplyGridHeaderTheme(DataGrid grid)
        {
            var normalStyle = TryFindResource("GscDataGridColumnHeaderStyle") as Style;
            var firstStyle = TryFindResource("MaintenanceFirstColumnHeader") as Style;
            var lastStyle = TryFindResource("GscLastColumnHeader") as Style;

            if (normalStyle != null)
                grid.ColumnHeaderStyle = normalStyle;

            for (var index = 0; index < grid.Columns.Count; index++)
            {
                var column = grid.Columns[index];
                column.HeaderStyle = index == 0 && firstStyle != null
                    ? firstStyle
                    : index == grid.Columns.Count - 1 && lastStyle != null
                        ? lastStyle
                        : normalStyle;
            }
        }
        public UniformGrid DiagnosticHealthPanelElement => DiagnosticHealthPanel;
        public DataGrid FindingsGridElement => FindingsGrid;

        public void ApplyResponsiveLayout(double width, double height)
        {
            DiagnosticHealthPanel.Columns = width >= 1320 ? 4 : width >= 980 ? 2 : 1;
            // Health cards remain useful context even in compact windows. Grid star rows keep
            // diagnostics tables finite while their own controls handle overflow.
            DiagnosticHealthPanel.Visibility = Visibility.Visible;
            var compact = height < 760 || width < 980;
            // The retention page is a left-aligned reading form capped at 1050.
            // Give the StackPanel an explicit viewport width so the cards fill
            // the form instead of collapsing to their content width, mirroring
            // the SaveCenter policy page. The 4 is the right padding of
            // GscPageScrollViewer.
            MaintenanceRetentionStack.Width = Math.Max(0, Math.Min(width - 4, 1050));
            // The findings table has five readable columns plus an inspector. Keep the
            // inspector beside it only when the main table can still show those columns;
            // otherwise stack it before WPF starts compressing the text into a single strip.
            // Keep the findings inspector beside the table only when the table has enough
            // room for readable game/title/detail/action columns.  At common 1280-DIP and
            // high-DPI sizes the inspector must stack instead of forcing ellipses into every
            // column and exposing the host's white fallback header surface.
            var stackDiagnostics = width < 1120;
            MaintenanceDiagnosticsLayout.ColumnDefinitions[1].Width = stackDiagnostics ? new GridLength(0) : new GridLength(14);
            MaintenanceDiagnosticsLayout.ColumnDefinitions[2].Width = stackDiagnostics ? new GridLength(0) : new GridLength(360);
            // The full diagnostic summary owns row 1 as an always-visible full-width strip;
            // the detail inspector stacks into row 2 only in compact windows.
            MaintenanceDiagnosticsLayout.RowDefinitions[2].Height = stackDiagnostics ? new GridLength(1, GridUnitType.Auto) : new GridLength(0);
            Grid.SetColumn(MaintenanceDiagnosticsInspector, stackDiagnostics ? 0 : 2);
            Grid.SetColumnSpan(MaintenanceDiagnosticsInspector, stackDiagnostics ? 3 : 1);
            Grid.SetRow(MaintenanceDiagnosticsInspector, stackDiagnostics ? 2 : 0);
            MaintenanceDiagnosticsInspector.Margin = stackDiagnostics ? new Thickness(0, 10, 0, 0) : new Thickness(0);
            // The detail inspector owns only the selected finding; the full diagnostic
            // summary strip stays below the table. In stacked mode both share a finite
            // vertical budget so the findings table keeps the remaining rows.
            MaintenanceDiagnosticsInspector.MaxHeight = stackDiagnostics ? Math.Max(150, height * 0.34) : double.PositiveInfinity;
            MaintenanceDiagnosticSummaryGrid.MinHeight = stackDiagnostics ? 96 : 140;
            MaintenanceDiagnosticSummaryGrid.MaxHeight = stackDiagnostics ? Math.Max(120, height * 0.20) : 280;
            var stackProcess = width < 1040;
            MaintenanceProcessLayout.ColumnDefinitions[1].Width = stackProcess ? new GridLength(0) : new GridLength(14);
            MaintenanceProcessLayout.ColumnDefinitions[2].Width = stackProcess ? new GridLength(0) : new GridLength(360);
            MaintenanceProcessLayout.RowDefinitions[2].Height = stackProcess ? new GridLength(1, GridUnitType.Auto) : new GridLength(0);
            Grid.SetColumn(MaintenanceProcessInspector, stackProcess ? 0 : 2);
            Grid.SetColumnSpan(MaintenanceProcessInspector, stackProcess ? 3 : 1);
            Grid.SetRow(MaintenanceProcessInspector, stackProcess ? 2 : 1);
            MaintenanceProcessInspector.Margin = stackProcess ? new Thickness(0, 10, 0, 0) : new Thickness(0);
            var stackDevice = width < 1180;
            MaintenanceDeviceLayout.ColumnDefinitions[1].Width = stackDevice ? new GridLength(0) : new GridLength(14);
            MaintenanceDeviceLayout.ColumnDefinitions[2].Width = stackDevice ? new GridLength(0) : new GridLength(360);
            MaintenanceDeviceLayout.RowDefinitions[3].Height = stackDevice ? new GridLength(1, GridUnitType.Auto) : new GridLength(0);
            Grid.SetColumn(MaintenanceDeviceInspector, stackDevice ? 0 : 2);
            Grid.SetColumnSpan(MaintenanceDeviceInspector, stackDevice ? 3 : 1);
            Grid.SetRow(MaintenanceDeviceInspector, stackDevice ? 3 : 2);
            MaintenanceDeviceInspector.Margin = stackDevice ? new Thickness(0, 10, 0, 0) : new Thickness(0, 10, 0, 0);
            MaintenanceDeviceDecisionScrollViewer.MaxHeight = Math.Max(90, Math.Min(150, height * (compact ? 0.16 : 0.20)));
            MaintenanceRemoteRestoreScrollViewer.MaxHeight = Math.Max(120, Math.Min(210, height * (compact ? 0.22 : 0.28)));

            var stackAudit = width < 1120 || height < 700;
            MaintenanceAuditLayout.ColumnDefinitions[1].Width = stackAudit ? new GridLength(0) : new GridLength(14);
            MaintenanceAuditLayout.ColumnDefinitions[2].Width = stackAudit ? new GridLength(0) : new GridLength(370);
            Grid.SetColumn(MaintenanceAuditInspector, stackAudit ? 0 : 2);
            Grid.SetColumnSpan(MaintenanceAuditInspector, stackAudit ? 3 : 1);
            Grid.SetRow(MaintenanceAuditInspector, stackAudit ? 1 : 0);
            MaintenanceAuditInspector.Margin = stackAudit ? new Thickness(0, 10, 0, 0) : new Thickness(0);
            // The detail inspector owns only the selected finding; the recent audit log
            // lives in its own full-width strip. In stacked mode both share a finite
            // vertical budget so the findings table keeps the remaining rows.
            MaintenanceAuditInspector.MaxHeight = stackAudit ? Math.Max(150, height * 0.34) : double.PositiveInfinity;
            MaintenanceAuditLogGrid.MinHeight = stackAudit ? 96 : 140;
            MaintenanceAuditLogGrid.MaxHeight = stackAudit ? Math.Max(120, height * 0.20) : 280;
        }
    }
}
