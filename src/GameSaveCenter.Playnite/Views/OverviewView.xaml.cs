using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

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
        public ScrollViewer OverviewSecondaryScrollViewerElement => OverviewSecondaryScrollViewer;
        public ScrollViewer OverviewRiskScrollViewerElement => OverviewRiskScrollViewer;
        public Panel OverviewMetricPanelElement => OverviewMetricPanel;

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
            Grid.SetRow(OverviewSecondaryScrollViewer, stack ? 1 : 0);
            Grid.SetColumn(OverviewSecondaryScrollViewer, stack ? 0 : 2);
            Grid.SetColumnSpan(OverviewSecondaryScrollViewer, stack ? 3 : 1);
            OverviewSecondaryPanel.Margin = stack
                ? new Thickness(0, 14, 0, 0)
                : new Thickness(0);
        }

        public void ApplyResponsiveWidth(double width)
        {
            // The metric pills size to their content and wrap naturally, so a compact
            // window no longer needs to force fixed column counts that clip the values.

            // The Demo keeps the Home workbench actions in the card header.  At the
            // narrowest widths let that action group become a vertical stack instead of
            // allowing the buttons to push the title column out of the viewport.
            if (OverviewHomeToolbarActions != null)
            {
                var stackActions = width < 720;
                OverviewHomeToolbarActions.Orientation = stackActions
                    ? Orientation.Vertical
                    : Orientation.Horizontal;
                OverviewHomeToolbarActions.HorizontalAlignment = stackActions
                    ? HorizontalAlignment.Left
                    : HorizontalAlignment.Right;
            }
        }

        public void ApplyResponsiveHeight(double height, bool stack)
        {
            // In stacked layouts the risk card is secondary content. Give it a bounded
            // scroll channel so a long attention list cannot push recent activity away.
            var compactHeight = height < 760;
            OverviewSecondaryScrollViewer.MaxHeight = stack || compactHeight
                ? Math.Max(260, Math.Min(480, height * 0.58))
                : double.PositiveInfinity;
            OverviewSecondaryScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
            OverviewRiskScrollViewer.MaxHeight = stack || compactHeight
                ? Math.Max(180, Math.Min(360, height * 0.42))
                : double.PositiveInfinity;
            OverviewRiskScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
        }
    }
}
