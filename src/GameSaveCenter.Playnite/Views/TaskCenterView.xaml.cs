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
    }
}
