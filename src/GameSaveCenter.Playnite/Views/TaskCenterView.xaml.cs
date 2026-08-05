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
            // Long task diagnostics and wrapped actions are secondary content. Keep them in
            // their own finite scroll channel so the task table's star row remains reachable.
            TaskDetailScrollViewer.MaxHeight = Math.Max(150, Math.Min(260, height * 0.32));
        }
    }
}
