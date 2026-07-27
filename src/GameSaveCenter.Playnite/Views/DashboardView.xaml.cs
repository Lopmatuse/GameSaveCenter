using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using GameSaveCenter.Playnite.ViewModels;

namespace GameSaveCenter.Playnite.Views
{
    public partial class DashboardView : UserControl
    {
        private readonly GameSaveCenterPlugin plugin;
        private readonly DispatcherTimer refreshTimer;

        public DashboardView(GameSaveCenterPlugin plugin)
        {
            this.plugin = plugin;
            InitializeComponent();
            DataContext = new DashboardViewModel(plugin);
            refreshTimer = new DispatcherTimer(DispatcherPriority.Background);
            refreshTimer.Tick += OnRefreshTimerTick;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            refreshTimer.Interval = TimeSpan.FromSeconds(Math.Max(5, Math.Min(300, plugin.Settings.DashboardRefreshSeconds)));
            if (plugin.Settings.EnableDashboardAutoRefresh) refreshTimer.Start();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) => refreshTimer.Stop();

        private void OnRefreshTimerTick(object sender, EventArgs e)
        {
            (DataContext as DashboardViewModel)?.RequestBackgroundRefresh();
        }
    }
}
