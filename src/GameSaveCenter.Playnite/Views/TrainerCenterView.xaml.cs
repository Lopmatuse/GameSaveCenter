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
            TrainerCatalogLayout.HorizontalAlignment = HorizontalAlignment.Stretch;
            TrainerCatalogLayout.VerticalAlignment = VerticalAlignment.Stretch;
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
            var stackCatalog = width < 980;
            TrainerCatalogLayout.RowDefinitions[0].Height = new GridLength(1, GridUnitType.Star);
            TrainerCatalogLayout.RowDefinitions[1].Height = stackCatalog
                ? new GridLength(1, GridUnitType.Star)
                : new GridLength(0);
            TrainerCatalogLayout.ColumnDefinitions[1].Width = stackCatalog
                ? new GridLength(0)
                : new GridLength(14);
            Grid.SetRow(TrainerCatalogResultsPanel, 0);
            Grid.SetColumn(TrainerCatalogResultsPanel, 0);
            Grid.SetColumnSpan(TrainerCatalogResultsPanel, stackCatalog ? 3 : 1);
            Grid.SetRow(TrainerCatalogReleasesPanel, stackCatalog ? 1 : 0);
            Grid.SetColumn(TrainerCatalogReleasesPanel, stackCatalog ? 0 : 2);
            Grid.SetColumnSpan(TrainerCatalogReleasesPanel, stackCatalog ? 3 : 1);
            TrainerCatalogResultsPanel.Margin = stackCatalog ? new Thickness(0, 0, 0, 10) : new Thickness(0);
            TrainerCatalogReleasesPanel.Margin = stackCatalog ? new Thickness(0) : new Thickness(0);
        }
    }
}
