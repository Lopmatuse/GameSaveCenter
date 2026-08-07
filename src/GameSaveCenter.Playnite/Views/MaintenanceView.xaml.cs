using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using System.Collections.Generic;
using System;

namespace GameSaveCenter.Playnite.Views
{
    public partial class MaintenanceView : UserControl
    {
        private readonly HashSet<DataGrid> _headerThemeGrids = new HashSet<DataGrid>();

        public MaintenanceView()
        {
            InitializeComponent();

            // Loaded is a direct WPF event. Subscribe to each concrete DataGrid so the
            // deterministic header/scrolling fallback actually runs for generated headers.
            FindingsGrid.Loaded += DataGridLoaded;
            MaintenanceDeviceGrid.Loaded += DataGridLoaded;
            MaintenanceAuditFindingsGrid.Loaded += DataGridLoaded;
            MaintenanceAuditLogGrid.Loaded += DataGridLoaded;
        }

        private void DataGridLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is DataGrid grid)
            {
                ApplyGridHeaderTheme(grid);
                QueueGridHeaderTheme(grid);
            }
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

                if (string.Equals(column.Header as string, "建议处理", StringComparison.Ordinal))
                {
                    // Keep the action column readable without allowing it to consume the
                    // majority of the diagnostic table at the initial layout pass.
                    column.Width = new DataGridLength(0.75, DataGridLengthUnitType.Star);
                    column.MinWidth = 180;
                }
            }

            if (_headerThemeGrids.Add(grid))
            {
                grid.SizeChanged += GridSizeChanged;
                grid.Unloaded += GridUnloaded;
            }
        }

        private void GridSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is DataGrid grid)
                QueueGridHeaderTheme(grid);
        }

        private void GridUnloaded(object sender, RoutedEventArgs e)
        {
            if (sender is DataGrid grid)
            {
                grid.SizeChanged -= GridSizeChanged;
                grid.Unloaded -= GridUnloaded;
                _headerThemeGrids.Remove(grid);
            }
        }

        private void QueueGridHeaderTheme(DataGrid grid)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
            {
                if (!grid.IsLoaded)
                    return;

                ApplyGridHeaderTheme(grid);
                foreach (var header in FindVisualChildren<DataGridColumnHeader>(grid))
                    ApplyColumnHeaderTheme(header);
            }));
        }

        private void ApplyColumnHeaderTheme(DataGridColumnHeader header)
        {
            if (TryFindResource("GscDataGridColumnHeaderStyle") is Style themedHeaderStyle)
                header.Style = themedHeaderStyle;
            header.OverridesDefaultStyle = true;
            header.SetResourceReference(Control.BackgroundProperty, "GscTableHeaderBrush");
            header.SetResourceReference(Control.ForegroundProperty, "GscPrimaryTextBrush");
            header.SetResourceReference(Control.BorderBrushProperty, "GscTableDividerBrush");
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject
        {
            if (root == null)
                yield break;

            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            {
                var child = VisualTreeHelper.GetChild(root, index);
                if (child is T match)
                    yield return match;

                foreach (var nested in FindVisualChildren<T>(child))
                    yield return nested;
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
            // The findings table has five readable columns plus an inspector. Keep the
            // inspector beside it only when the main table can still show those columns;
            // otherwise stack it before WPF starts compressing the text into a single strip.
            // Keep the findings inspector beside the table only when the table has enough
            // room for readable game/title/detail/action columns.  At common 1280-DIP and
            // high-DPI sizes the inspector must stack instead of forcing ellipses into every
            // column and exposing the host's white fallback header surface.
            var stackDiagnostics = width < 1360;
            MaintenanceDiagnosticsLayout.ColumnDefinitions[1].Width = stackDiagnostics ? new GridLength(0) : new GridLength(14);
            MaintenanceDiagnosticsLayout.ColumnDefinitions[2].Width = stackDiagnostics ? new GridLength(0) : new GridLength(330);
            MaintenanceDiagnosticsLayout.RowDefinitions[1].Height = stackDiagnostics ? new GridLength(1, GridUnitType.Auto) : new GridLength(0);
            Grid.SetColumn(MaintenanceDiagnosticDetails, stackDiagnostics ? 0 : 2);
            Grid.SetColumnSpan(MaintenanceDiagnosticDetails, stackDiagnostics ? 3 : 1);
            Grid.SetRow(MaintenanceDiagnosticDetails, stackDiagnostics ? 1 : 0);
            MaintenanceDiagnosticDetails.Margin = stackDiagnostics ? new Thickness(0, 10, 0, 0) : new Thickness(0);
            var stackProcess = width < 1360;
            MaintenanceProcessLayout.ColumnDefinitions[1].Width = stackProcess ? new GridLength(0) : new GridLength(14);
            MaintenanceProcessLayout.ColumnDefinitions[2].Width = stackProcess ? new GridLength(0) : new GridLength(330);
            MaintenanceProcessLayout.RowDefinitions[2].Height = stackProcess ? new GridLength(1, GridUnitType.Auto) : new GridLength(0);
            Grid.SetColumn(MaintenanceProcessInspector, stackProcess ? 0 : 2);
            Grid.SetColumnSpan(MaintenanceProcessInspector, stackProcess ? 3 : 1);
            Grid.SetRow(MaintenanceProcessInspector, stackProcess ? 2 : 1);
            MaintenanceProcessInspector.Margin = stackProcess ? new Thickness(0, 10, 0, 0) : new Thickness(0);
            var stackDevice = width < 1360;
            MaintenanceDeviceLayout.ColumnDefinitions[1].Width = stackDevice ? new GridLength(0) : new GridLength(14);
            MaintenanceDeviceLayout.ColumnDefinitions[2].Width = stackDevice ? new GridLength(0) : new GridLength(330);
            MaintenanceDeviceLayout.RowDefinitions[3].Height = stackDevice ? new GridLength(1, GridUnitType.Auto) : new GridLength(0);
            Grid.SetColumn(MaintenanceDeviceInspector, stackDevice ? 0 : 2);
            Grid.SetColumnSpan(MaintenanceDeviceInspector, stackDevice ? 3 : 1);
            Grid.SetRow(MaintenanceDeviceInspector, stackDevice ? 3 : 2);
            MaintenanceDeviceInspector.Margin = stackDevice ? new Thickness(0, 10, 0, 0) : new Thickness(0, 10, 0, 0);
            MaintenanceDeviceDecisionScrollViewer.MaxHeight = Math.Max(90, Math.Min(150, height * (compact ? 0.16 : 0.20)));
            MaintenanceRemoteRestoreScrollViewer.MaxHeight = Math.Max(120, Math.Min(210, height * (compact ? 0.22 : 0.28)));

            var stackAudit = width < 1360 || height < 760;
            MaintenanceAuditLayout.ColumnDefinitions[1].Width = stackAudit ? new GridLength(0) : new GridLength(14);
            MaintenanceAuditLayout.ColumnDefinitions[2].Width = stackAudit ? new GridLength(0) : new GridLength(350);
            Grid.SetColumn(MaintenanceAuditInspector, stackAudit ? 0 : 2);
            Grid.SetColumnSpan(MaintenanceAuditInspector, stackAudit ? 3 : 1);
            Grid.SetRow(MaintenanceAuditInspector, stackAudit ? 1 : 0);
            MaintenanceAuditInspector.Margin = stackAudit ? new Thickness(0, 10, 0, 0) : new Thickness(0);
            MaintenanceAuditInspector.MaxHeight = Math.Max(180, Math.Min(560, height * (stackAudit ? 0.55 : 0.90)));
        }
    }
}
