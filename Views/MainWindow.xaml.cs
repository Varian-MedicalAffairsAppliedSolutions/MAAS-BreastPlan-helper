using System.Threading.Tasks;
using System.Windows;
using MahApps.Metro.Controls;
using MAAS_BreastPlan_helper.Models;
using VMS.TPS.Common.Model.API;
using MAAS_BreastPlan_helper.ViewModels;
using MAAS_BreastPlan_helper.Views;

namespace MAAS_BreastPlan_helper
{
    public partial class MainWindow : MetroWindow
    {
        public MainWindow(ScriptContext context, SettingsClass settings, string json_path)
        {
            InitializeComponent();

            // Initialize tabs with corresponding ViewModels
            EthosAutoBeamTab.Content = new EthosBeamDialog() { DataContext = new EthosBeamDialogViewModel(context) };
            Auto3DSWTab.Content = new Auto3dSlidingWindow() { DataContext = new Auto3dSlidingWindowViewModel(context, settings, json_path) };
            FluenceExtensionTab.Content = new FluenceExtensionView() { DataContext = new FluenceExtensionViewModel(context, settings) };
            BreastFiFTab.Content = new BreastFiFView() { DataContext = new BreastFiFViewModel(context, settings) };
            TangentPlacementTab.Content = new TangentPlacementView() { DataContext = new TangentPlacementViewModel(context, settings) };
        }
    }
}