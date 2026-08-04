using System;
using System.Windows;
using System.Windows.Controls;

namespace GameSaveCenter.Playnite.Views
{
    public partial class SaveCenterView : UserControl
    {
        public SaveCenterView() => InitializeComponent();

        public void ApplyResponsiveLayout(double width, double height)
        {
            // Keep the history/candidate tables in the star-sized row. At short or
            // narrow heights, wrapped metadata/actions must scroll inside their
            // own channels instead of consuming the entire tab page.
            var compact = height < 760 || width < 980;
            SaveHistoryActionsScrollViewer.MaxHeight = Math.Max(130, Math.Min(220, height * (compact ? 0.24 : 0.30)));
            SaveCandidateReasonScrollViewer.MaxHeight = Math.Max(90, Math.Min(180, height * (compact ? 0.18 : 0.22)));
            SaveCandidateActionsScrollViewer.MaxHeight = Math.Max(70, Math.Min(140, height * (compact ? 0.14 : 0.18)));
        }
    }
}
