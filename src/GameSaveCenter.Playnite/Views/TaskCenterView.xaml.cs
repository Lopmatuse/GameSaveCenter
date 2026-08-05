using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace GameSaveCenter.Playnite.Views
{
    /// <summary>Physical task-center workspace; it deliberately has no current-game picker.</summary>
    public partial class TaskCenterView : UserControl
    {
        public TaskCenterView() => InitializeComponent();
        public UniformGrid TaskSummaryPanelElement => TaskSummaryPanel;
        public Border TaskDetailCardElement => TaskDetailCard;
        public ScrollViewer TaskDetailScrollViewerElement => TaskDetailScrollViewer;

        public void ApplyResponsiveLayout(double width, double height)
        {
            TaskSummaryPanel.Columns = width >= 1120 ? 3 : width >= 760 ? 2 : 1;
            // Keep task summary metrics available at every height; the table and inspector
            // own their finite scroll surfaces instead of scrolling the whole workspace.
            TaskSummaryPanel.Visibility = Visibility.Visible;
            TaskDetailActions.Orientation = width < 760 ? Orientation.Vertical : Orientation.Horizontal;
            // Match the demo's task workspace: the queue remains the primary
            // surface and the selected task is a right-side inspector. At
            // compact widths the inspector drops below the table instead of
            // compressing the task columns into an unreadable strip.
            var stack = width < 1060;
            TaskWorkspaceLayout.ColumnDefinitions[1].Width = stack ? new GridLength(0) : new GridLength(14);
            TaskWorkspaceLayout.ColumnDefinitions[2].Width = stack ? new GridLength(0) : new GridLength(330);
            TaskWorkspaceLayout.RowDefinitions[3].Height = stack ? new GridLength(1, GridUnitType.Auto) : new GridLength(0);
            Grid.SetColumn(TaskDetailScrollViewer, stack ? 0 : 2);
            Grid.SetColumnSpan(TaskDetailScrollViewer, stack ? 3 : 1);
            Grid.SetRow(TaskDetailScrollViewer, stack ? 3 : 2);
            TaskDetailScrollViewer.Margin = stack ? new Thickness(0, 10, 0, 0) : new Thickness(0);
            TaskDetailScrollViewer.MaxHeight = Math.Max(180, Math.Min(520, height * (stack ? 0.42 : 0.90)));
        }
    }
}
