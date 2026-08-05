using System;
using System.Windows;
using System.Windows.Controls;
using GameSaveCenter.Playnite.ViewModels;

namespace GameSaveCenter.Playnite.Views
{
    public partial class TrainerCenterView : UserControl
    {
        public TrainerCenterView() => InitializeComponent();

        private void OnTrainerCatalogSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0 || DataContext is not DashboardViewModel viewModel)
                return;

            if (viewModel.LoadTrainerReleasesCommand.CanExecute(null))
                viewModel.LoadTrainerReleasesCommand.Execute(null);
        }

        public void ApplyResponsiveLayout(double width, double height)
        {
            InstalledToolsLayout.HorizontalAlignment = HorizontalAlignment.Stretch;
            InstalledToolsLayout.VerticalAlignment = VerticalAlignment.Stretch;
            TrainerReleasesLayout.HorizontalAlignment = HorizontalAlignment.Stretch;
            TrainerReleasesLayout.VerticalAlignment = VerticalAlignment.Stretch;
            // Keep the selected-tool inspector reachable at short heights. The list remains
            // the primary star row; only the secondary settings card receives a finite scroll
            // channel so it cannot push the list or the tab content outside the viewport.
            TrainerToolsSettingsScrollViewer.MaxHeight = Math.Max(190, Math.Min(280, height * 0.36));
            var stackInstalled = width < 980;
            InstalledToolsLayout.ColumnDefinitions[1].Width = stackInstalled
                ? new GridLength(0)
                : new GridLength(14);
            InstalledToolsLayout.ColumnDefinitions[2].Width = stackInstalled
                ? new GridLength(0)
                : new GridLength(320);
            Grid.SetColumn(TrainerToolsSettingsScrollViewer, stackInstalled ? 0 : 2);
            Grid.SetRow(TrainerToolsSettingsScrollViewer, stackInstalled ? 3 : 0);
            Grid.SetRowSpan(TrainerToolsSettingsScrollViewer, stackInstalled ? 1 : 4);
            TrainerToolsSettingsScrollViewer.MaxHeight = stackInstalled
                ? Math.Max(190, Math.Min(280, height * 0.36))
                : double.PositiveInfinity;
            TrainerToolsSettingsScrollViewer.Margin = stackInstalled
                ? new Thickness(0, 10, 0, 0)
                : new Thickness(0);
            var stackReleases = width < 980;
            TrainerReleasesLayout.RowDefinitions[0].Height = new GridLength(1, GridUnitType.Star);
            TrainerReleasesLayout.RowDefinitions[1].Height = stackReleases
                ? GridLength.Auto
                : new GridLength(0);
            TrainerReleasesLayout.ColumnDefinitions[1].Width = stackReleases
                ? new GridLength(0)
                : new GridLength(14);
            TrainerReleasesLayout.ColumnDefinitions[2].Width = stackReleases
                ? new GridLength(0)
                : new GridLength(320);
            Grid.SetRow(TrainerCatalogReleasesPanel, 0);
            Grid.SetColumn(TrainerCatalogReleasesPanel, 0);
            Grid.SetColumnSpan(TrainerCatalogReleasesPanel, stackReleases ? 3 : 1);
            Grid.SetRow(TrainerReleaseInfoPanel, stackReleases ? 1 : 0);
            Grid.SetColumn(TrainerReleaseInfoPanel, stackReleases ? 0 : 2);
            Grid.SetColumnSpan(TrainerReleaseInfoPanel, stackReleases ? 3 : 1);
            TrainerCatalogReleasesPanel.Margin = new Thickness(0);
            TrainerReleaseInfoPanel.Margin = stackReleases
                ? new Thickness(0, 10, 0, 0)
                : new Thickness(0);
        }
    }
}
