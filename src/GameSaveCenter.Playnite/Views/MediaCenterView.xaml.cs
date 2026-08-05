using System;
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
        public ScrollViewer MediaInspectorScrollViewerElement => MediaInspectorScrollViewer;

        public void ApplyResponsiveLayout(double width, double height)
        {
            // The inspector contains wrapping controls and media metadata. Give it its own
            // finite scroll channel at low heights so the media table above remains reachable.
            MediaInspectorScrollViewer.MaxHeight = Math.Max(190, Math.Min(300, height * 0.42));
            MediaSummaryPanel.Columns = width >= 1180 ? 4 : width >= 820 ? 2 : 1;
            // Do not discard summary information at short heights. Local list/inspector
            // surfaces own overflow so the whole workspace does not become a scroll canvas.
            MediaSummaryPanel.Visibility = Visibility.Visible;
            MediaSourceFields.Columns = width >= 820 ? 2 : 1;
            // Match the demo: the media table and its inspector share the main
            // work area on wide hosts; on compact hosts the inspector moves
            // below the table instead of becoming a narrow strip.
            var stack = width < 1100;
            MediaCurrentLayout.ColumnDefinitions[1].Width = stack ? new GridLength(0) : new GridLength(14);
            MediaCurrentLayout.ColumnDefinitions[2].Width = stack ? new GridLength(0) : new GridLength(330);
            MediaCurrentLayout.RowDefinitions[3].Height = stack ? new GridLength(1, GridUnitType.Auto) : new GridLength(0);
            Grid.SetColumn(MediaInspectorScrollViewer, stack ? 0 : 2);
            Grid.SetColumnSpan(MediaInspectorScrollViewer, stack ? 3 : 1);
            Grid.SetRow(MediaInspectorScrollViewer, stack ? 3 : 2);
            MediaInspectorScrollViewer.Margin = stack ? new Thickness(0, 10, 0, 0) : new Thickness(0);
            Grid.SetColumnSpan(MediaPreviewPanel, stack ? 2 : 1);
            MediaPreviewPanel.Margin = stack ? new Thickness(0, 0, 0, 12) : new Thickness(0, 0, 12, 0);
            Grid.SetColumn(MediaMetadataPanel, stack ? 0 : 1);
            Grid.SetRow(MediaMetadataPanel, stack ? 1 : 0);
            Grid.SetColumnSpan(MediaMetadataPanel, stack ? 2 : 1);
        }
    }
}
