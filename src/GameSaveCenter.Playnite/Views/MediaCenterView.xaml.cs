using System;
using System.Windows;
using System.Windows.Controls;

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
        public Border MediaInspectorFrameElement => MediaInspectorFrame;

        public void ApplyResponsiveLayout(double width, double height)
        {
            // The inspector contains wrapping controls and media metadata. Give it its own
            // finite scroll channel at low heights so the media table above remains reachable.
            // On wide layouts the inspector is a peer of the media list and must stretch to
            // the same finite Grid row. A hard 300-DIP cap made its border appear to float
            // outside the table frame and left unused space below it.
            MediaInspectorScrollViewer.MaxHeight = width >= 1080
                ? double.PositiveInfinity
                : Math.Max(220, Math.Min(420, height * 0.56));
            MediaSummaryPanel.Columns = width >= 1180 ? 4 : width >= 820 ? 2 : 1;
            // Do not discard summary information at short heights. Local list/inspector
            // surfaces own overflow so the whole workspace does not become a scroll canvas.
            MediaSummaryPanel.Visibility = Visibility.Visible;
            MediaSourceFields.Columns = width >= 820 ? 2 : 1;
            // Match the demo: the media table and its inspector share the main
            // work area on wide hosts; on compact hosts the inspector moves
            // below the table instead of becoming a narrow strip.
            var stack = width < 1080;
            MediaCurrentLayout.ColumnDefinitions[1].Width = stack ? new GridLength(0) : new GridLength(14);
            MediaCurrentLayout.ColumnDefinitions[2].Width = stack ? new GridLength(0) : new GridLength(370);
            MediaCurrentLayout.RowDefinitions[3].Height = stack ? new GridLength(1, GridUnitType.Auto) : new GridLength(0);
            Grid.SetColumn(MediaInspectorFrame, stack ? 0 : 2);
            Grid.SetColumnSpan(MediaInspectorFrame, stack ? 3 : 1);
            Grid.SetRow(MediaInspectorFrame, stack ? 3 : 2);
            MediaInspectorFrame.Margin = stack ? new Thickness(0, 10, 0, 0) : new Thickness(0);
            // The inspector itself always uses the demo's preview-over-details layout.
            // Responsive work only moves that complete inspector beside/below the media list;
            // it never rewrites the inspector's internal visual tree during a resize.
            MediaPreviewPanel.Margin = new Thickness(0, 0, 0, 14);
        }
    }
}
