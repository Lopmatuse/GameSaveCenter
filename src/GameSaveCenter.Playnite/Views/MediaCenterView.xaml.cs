using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

namespace GameSaveCenter.Playnite.Views
{
    public partial class MediaCenterView : UserControl
    {
        private bool _inboxGridHooksAttached;

        public MediaCenterView()
        {
            InitializeComponent();
            if (MediaInspectorPanel.Child is Grid inspectorGrid && inspectorGrid.RowDefinitions.Count == 0)
            {
                // The inspector is a two-column surface on wide layouts and becomes a
                // preview-over-details surface when the host is narrow. Explicit rows keep
                // that transition finite instead of placing both children in the same row.
                inspectorGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                inspectorGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            // Loaded is a direct event in WPF; subscribe to the concrete grid instead of
            // attempting to catch child Loaded events from the UserControl.
            MediaInboxGrid.Loaded += InboxGridLoaded;
            MediaInboxGrid.Unloaded += InboxGridUnloaded;
        }

        private void InboxGridLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is not DataGrid grid)
                return;

            ConfigureInboxGrid(grid);
            AttachInboxGridHooks(grid);
            QueueInboxGridRefresh(grid);
            foreach (var header in FindVisualChildren<DataGridColumnHeader>(grid))
                ApplyColumnHeaderTheme(header);
        }

        private void AttachInboxGridHooks(DataGrid grid)
        {
            if (_inboxGridHooksAttached)
                return;

            _inboxGridHooksAttached = true;
            grid.SizeChanged += InboxGridSizeChanged;
        }

        private void InboxGridSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is DataGrid grid)
                QueueInboxGridRefresh(grid);
        }

        private void InboxGridUnloaded(object sender, RoutedEventArgs e)
        {
            if (sender is DataGrid grid)
            {
                grid.SizeChanged -= InboxGridSizeChanged;
                grid.Loaded -= InboxGridLoaded;
                grid.Unloaded -= InboxGridUnloaded;
            }

            _inboxGridHooksAttached = false;
        }

        private void QueueInboxGridRefresh(DataGrid grid)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
            {
                if (!grid.IsLoaded)
                    return;

                ConfigureInboxGrid(grid);
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

        private void ConfigureInboxGrid(DataGrid grid)
        {
            grid.VerticalContentAlignment = VerticalAlignment.Top;
            grid.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            grid.ClipToBounds = true;
            if (TryFindResource("GscGlassStrongBrush") is Brush surface)
                grid.Background = surface;

            var scrollViewer = FindVisualChild<ScrollViewer>(grid);
            if (scrollViewer == null)
                return;

            scrollViewer.VerticalContentAlignment = VerticalAlignment.Top;
            scrollViewer.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            scrollViewer.ClipToBounds = true;
            if (TryFindResource("GscGlassStrongBrush") is Brush scrollSurface)
                scrollViewer.Background = scrollSurface;

            var itemsPresenter = FindVisualChild<ItemsPresenter>(scrollViewer);
            if (itemsPresenter != null)
                itemsPresenter.VerticalAlignment = VerticalAlignment.Top;
        }

        private static T? FindVisualChild<T>(DependencyObject root) where T : DependencyObject
        {
            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            {
                var child = VisualTreeHelper.GetChild(root, index);
                if (child is T match)
                    return match;

                var nested = FindVisualChild<T>(child);
                if (nested != null)
                    return nested;
            }

            return null;
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
            MediaInspectorScrollViewer.MaxHeight = width >= 1240
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
            var stack = width < 1240;
            MediaCurrentLayout.ColumnDefinitions[1].Width = stack ? new GridLength(0) : new GridLength(14);
            MediaCurrentLayout.ColumnDefinitions[2].Width = stack ? new GridLength(0) : new GridLength(360);
            MediaCurrentLayout.RowDefinitions[3].Height = stack ? new GridLength(1, GridUnitType.Auto) : new GridLength(0);
            Grid.SetColumn(MediaInspectorFrame, stack ? 0 : 2);
            Grid.SetColumnSpan(MediaInspectorFrame, stack ? 3 : 1);
            Grid.SetRow(MediaInspectorFrame, stack ? 3 : 2);
            MediaInspectorFrame.Margin = stack ? new Thickness(0, 10, 0, 0) : new Thickness(0);
            Grid.SetColumnSpan(MediaPreviewPanel, stack ? 2 : 1);
            MediaPreviewPanel.Margin = stack ? new Thickness(0, 0, 0, 12) : new Thickness(0, 0, 12, 0);
            Grid.SetColumn(MediaMetadataPanel, stack ? 0 : 1);
            Grid.SetRow(MediaMetadataPanel, stack ? 1 : 0);
            Grid.SetColumnSpan(MediaMetadataPanel, stack ? 2 : 1);
        }
    }
}
