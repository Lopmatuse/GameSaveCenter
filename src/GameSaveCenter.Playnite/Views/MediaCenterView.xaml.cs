using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace GameSaveCenter.Playnite.Views
{
    public partial class MediaCenterView : UserControl
    {
        public MediaCenterView() => InitializeComponent();

        public UniformGrid MediaSummaryPanelElement => MediaSummaryPanel;
        public UniformGrid MediaSourceFieldsElement => MediaSourceFields;
        public Border MediaInspectorPanelElement => MediaInspectorPanel;
        public Border MediaPreviewPanelElement => MediaPreviewPanel;
        public StackPanel MediaMetadataPanelElement => MediaMetadataPanel;

        public void ApplyResponsiveLayout(double width, double height)
        {
            MediaSummaryPanel.Columns = width >= 1180 ? 4 : width >= 820 ? 2 : 1;
            MediaSummaryPanel.Visibility = height >= 660 ? Visibility.Visible : Visibility.Collapsed;
            MediaSourceFields.Columns = width >= 820 ? 2 : 1;
            var stack = width < 1180;
            Grid.SetColumnSpan(MediaPreviewPanel, stack ? 2 : 1);
            MediaPreviewPanel.Margin = stack ? new Thickness(0, 0, 0, 12) : new Thickness(0, 0, 12, 0);
            Grid.SetColumn(MediaMetadataPanel, stack ? 0 : 1);
            Grid.SetRow(MediaMetadataPanel, stack ? 1 : 0);
            Grid.SetColumnSpan(MediaMetadataPanel, stack ? 2 : 1);
        }
    }
}
