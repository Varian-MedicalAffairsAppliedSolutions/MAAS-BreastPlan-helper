using System.Threading.Tasks;
using System.Windows;
using MahApps.Metro.Controls;
using MAAS_BreastPlan_helper.Models;
using MAAS_BreastPlan_helper.Services;
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

            // Create service abstraction
            var esapiWorker = new EsapiWorker(context);
            
            // Set DataContext to MainViewModel with service composition
            DataContext = new MainViewModel(esapiWorker, settings, json_path);

            // Initialize tabs with corresponding Views bound to composed ViewModels
            var mainViewModel = (MainViewModel)DataContext;
            
            EthosAutoBeamTab.Content = new EthosBeamDialog() { DataContext = mainViewModel.EthosBeamDialogViewModel };
            Auto3DSWTab.Content = new Auto3dSlidingWindow() { DataContext = mainViewModel.Auto3dSlidingWindowViewModel };
            FluenceExtensionTab.Content = new FluenceExtensionView() { DataContext = mainViewModel.FluenceExtensionViewModel };
            BreastFiFTab.Content = new BreastFiFView() { DataContext = mainViewModel.BreastFiFViewModel };
            TangentPlacementTab.Content = new TangentPlacementView() { DataContext = mainViewModel.TangentPlacementViewModel };
        }
    }
}