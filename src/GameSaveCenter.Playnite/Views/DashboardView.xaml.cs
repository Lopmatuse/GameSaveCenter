using System.Windows.Controls;
using GameSaveCenter.Playnite.ViewModels;
namespace GameSaveCenter.Playnite.Views
{
    public partial class DashboardView : UserControl
    {
        public DashboardView(GameSaveCenterPlugin plugin)
        {
            InitializeComponent();
            DataContext = new DashboardViewModel(plugin);
        }
    }
}
