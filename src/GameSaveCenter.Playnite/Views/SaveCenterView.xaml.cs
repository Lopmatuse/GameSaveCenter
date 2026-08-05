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
            var compact = height < 760 || width < 980;
            // The demo keeps the history table and the selected-version inspector
            // side by side when there is room. On a compact host, stack the
            // inspector below the table so actions remain reachable without a
            // page-level scrollbar or clipped controls.
            SaveHistoryLayout.ColumnDefinitions[1].Width = compact ? new GridLength(0) : new GridLength(14);
            SaveHistoryLayout.ColumnDefinitions[2].Width = compact ? new GridLength(0) : new GridLength(330);
            SaveHistoryLayout.RowDefinitions[1].Height = compact ? new GridLength(1, GridUnitType.Auto) : new GridLength(0);
            Grid.SetColumn(SaveHistoryActionsScrollViewer, compact ? 0 : 2);
            Grid.SetColumnSpan(SaveHistoryActionsScrollViewer, compact ? 3 : 1);
            Grid.SetRow(SaveHistoryActionsScrollViewer, compact ? 1 : 0);
            SaveHistoryActionsScrollViewer.Margin = compact ? new Thickness(0, 10, 0, 0) : new Thickness(0);
            SaveHistoryActionsScrollViewer.MaxHeight = Math.Max(150, Math.Min(360, height * (compact ? 0.42 : 0.90)));
            SaveCandidateLayout.ColumnDefinitions[1].Width = compact ? new GridLength(0) : new GridLength(14);
            SaveCandidateLayout.ColumnDefinitions[2].Width = compact ? new GridLength(0) : new GridLength(330);
            Grid.SetColumn(SaveCandidateReasonScrollViewer, compact ? 0 : 2);
            Grid.SetColumnSpan(SaveCandidateReasonScrollViewer, compact ? 3 : 1);
            Grid.SetRow(SaveCandidateReasonScrollViewer, compact ? 1 : 0);
            Grid.SetColumn(SaveCandidateActionsScrollViewer, compact ? 0 : 2);
            Grid.SetColumnSpan(SaveCandidateActionsScrollViewer, compact ? 3 : 1);
            Grid.SetRow(SaveCandidateActionsScrollViewer, compact ? 2 : 1);
            Grid.SetRowSpan(SaveCandidateActionsScrollViewer, 1);
            SaveCandidateReasonScrollViewer.Margin = compact ? new Thickness(0, 10, 0, 0) : new Thickness(0);
            SaveCandidateActionsScrollViewer.Margin = new Thickness(0, 10, 0, 0);
            SaveCandidateReasonScrollViewer.MaxHeight = Math.Max(90, Math.Min(180, height * (compact ? 0.18 : 0.22)));
            SaveCandidateActionsScrollViewer.MaxHeight = Math.Max(70, Math.Min(140, height * (compact ? 0.14 : 0.18)));
        }
    }
}
