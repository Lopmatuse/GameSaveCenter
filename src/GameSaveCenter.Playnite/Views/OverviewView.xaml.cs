using System.Windows;
using System.Windows.Controls;

namespace GameSaveCenter.Playnite.Views
{
    /// <summary>
    /// Physical overview workspace extracted from DashboardView. The public layout
    /// accessors keep the existing responsive coordinator and command bindings intact
    /// while the remaining workspaces are migrated incrementally.
    /// </summary>
    public partial class OverviewView : UserControl
    {
        public OverviewView() => InitializeComponent();

        public GridLength OverviewCompactSecondaryRowHeight
        {
            get => OverviewCompactSecondaryRow.Height;
            set => OverviewCompactSecondaryRow.Height = value;
        }

        public ColumnDefinition OverviewPrimaryColumnDefinition => OverviewPrimaryColumn;
        public ColumnDefinition OverviewGutterColumnDefinition => OverviewGutterColumn;
        public ColumnDefinition OverviewSecondaryColumnDefinition => OverviewSecondaryColumn;
        public UIElement OverviewPrimaryPanelElement => OverviewPrimaryPanel;
        public UIElement OverviewSecondaryPanelElement => OverviewSecondaryPanel;

        public void ApplyResponsiveColumns(bool stack)
        {
            OverviewPrimaryColumn.Width = new GridLength(1.2, GridUnitType.Star);
            OverviewGutterColumn.Width = new GridLength(stack ? 0 : 14);
            OverviewSecondaryColumn.Width = stack
                ? new GridLength(0)
                : new GridLength(0.8, GridUnitType.Star);
            Grid.SetRow(OverviewPrimaryPanel, 0);
            Grid.SetColumn(OverviewPrimaryPanel, 0);
            Grid.SetColumnSpan(OverviewPrimaryPanel, stack ? 3 : 1);
            Grid.SetRow(OverviewSecondaryPanel, stack ? 1 : 0);
            Grid.SetColumn(OverviewSecondaryPanel, stack ? 0 : 2);
            Grid.SetColumnSpan(OverviewSecondaryPanel, stack ? 3 : 1);
            OverviewSecondaryPanel.Margin = stack
                ? new Thickness(0, 14, 0, 0)
                : new Thickness(0);
        }
    }
}
