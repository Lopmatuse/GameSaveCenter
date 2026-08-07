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
            var inspectorWidth = SaveHistoryLayout.TryFindResource("GscInspectorWidth") is GridLength gl ? gl : new GridLength(360);
            // The demo keeps the history table and the selected-version inspector
            // side by side when there is room. On a compact host, stack the
            // inspector below the table so actions remain reachable without a
            // page-level scrollbar or clipped controls.
            SaveHistoryLayout.ColumnDefinitions[1].Width = compact ? new GridLength(0) : new GridLength(14);
            SaveHistoryLayout.ColumnDefinitions[2].Width = compact ? new GridLength(0) : inspectorWidth;
            SaveHistoryLayout.RowDefinitions[1].Height = compact ? new GridLength(1, GridUnitType.Auto) : new GridLength(0);
            Grid.SetColumn(SaveHistoryActionsScrollViewer, compact ? 0 : 2);
            Grid.SetColumnSpan(SaveHistoryActionsScrollViewer, compact ? 3 : 1);
            Grid.SetRow(SaveHistoryActionsScrollViewer, compact ? 1 : 0);
            SaveHistoryActionsScrollViewer.Margin = compact ? new Thickness(0, 10, 0, 0) : new Thickness(0);
            SaveHistoryActionsScrollViewer.MaxHeight = compact ? Math.Max(150, Math.Min(360, height * 0.42)) : double.PositiveInfinity;
            SaveCandidateLayout.ColumnDefinitions[1].Width = compact ? new GridLength(0) : new GridLength(14);
            SaveCandidateLayout.ColumnDefinitions[2].Width = compact ? new GridLength(0) : inspectorWidth;
            SaveCandidateLayout.RowDefinitions[1].Height = compact ? new GridLength(1, GridUnitType.Auto) : new GridLength(0);
            Grid.SetColumn(SaveCandidateInspectorScrollViewer, compact ? 0 : 2);
            Grid.SetColumnSpan(SaveCandidateInspectorScrollViewer, compact ? 3 : 1);
            Grid.SetRow(SaveCandidateInspectorScrollViewer, compact ? 1 : 0);
            SaveCandidateInspectorScrollViewer.Margin = compact ? new Thickness(0, 10, 0, 0) : new Thickness(0);
            SaveCandidateInspectorScrollViewer.MaxHeight = compact ? Math.Max(150, Math.Min(360, height * 0.42)) : double.PositiveInfinity;
            var stackPolicy = width < 980;
            // The policy page is a left-aligned form capped by the shared
            // GscFormMaxWidth token (1120). Give the StackPanel an explicit
            // viewport width so the reading cards fill the form instead of
            // collapsing to their content width, and so WPF never centers the
            // capped form inside the page scroll channel. The 4 is the right
            // padding of GscPageScrollViewer.
            SavePolicyStack.Width = Math.Max(0, Math.Min(width - 4, 1120));
            SavePolicyCardsLayout.ColumnDefinitions[1].Width = stackPolicy ? new GridLength(0) : new GridLength(14);
            SavePolicyCardsLayout.ColumnDefinitions[2].Width = stackPolicy ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
            SavePolicyCardsLayout.RowDefinitions[2].Height = stackPolicy ? new GridLength(1, GridUnitType.Auto) : new GridLength(0);
            Grid.SetColumn(SavePolicyMediaCard, stackPolicy ? 0 : 2);
            Grid.SetColumnSpan(SavePolicyMediaCard, stackPolicy ? 3 : 1);
            Grid.SetRow(SavePolicyMediaCard, stackPolicy ? 1 : 0);
            Grid.SetColumn(SavePolicySafetyCard, 0);
            Grid.SetColumnSpan(SavePolicySafetyCard, 3);
            Grid.SetRow(SavePolicySafetyCard, stackPolicy ? 2 : 1);
            SavePolicyMediaCard.Margin = stackPolicy ? new Thickness(0, 14, 0, 0) : new Thickness(0);

            var stackCompare = width < 980 || height < 760;
            SaveCompareLayout.ColumnDefinitions[1].Width = stackCompare ? new GridLength(0) : new GridLength(14);
            SaveCompareLayout.ColumnDefinitions[2].Width = stackCompare ? new GridLength(0) : inspectorWidth;
            SaveCompareLayout.RowDefinitions[1].Height = stackCompare ? new GridLength(1, GridUnitType.Auto) : new GridLength(0);
            Grid.SetColumn(SaveCompareRetentionScrollViewer, stackCompare ? 0 : 2);
            Grid.SetColumnSpan(SaveCompareRetentionScrollViewer, stackCompare ? 3 : 1);
            Grid.SetRow(SaveCompareRetentionScrollViewer, stackCompare ? 1 : 0);
            SaveCompareRetentionScrollViewer.Margin = stackCompare ? new Thickness(0, 14, 0, 0) : new Thickness(0);
            SaveCompareRetentionScrollViewer.MaxHeight = stackCompare ? Math.Max(180, Math.Min(420, height * 0.42)) : double.PositiveInfinity;
            SaveCompareMainScrollViewer.MaxHeight = stackCompare ? Math.Max(220, Math.Min(420, height * 0.45)) : double.PositiveInfinity;
        }
    }
}
