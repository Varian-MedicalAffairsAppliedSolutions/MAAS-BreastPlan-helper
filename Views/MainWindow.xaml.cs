using System.Threading.Tasks;
using System.Windows;
using MahApps.Metro.Controls;
using MAAS_BreastPlan_helper.Models;
using VMS.TPS.Common.Model.API;
using MAAS_BreastPlan_helper.MAAS_BreastPlan_helper;
using MAAS_BreastPlan_helper.ViewModels;
using MAAS_BreastPlan_helper.Views;

namespace MAAS_BreastPlan_helper
{
    public partial class MainWindow : MetroWindow
    {

        public MainWindow(ScriptContext context, SettingsClass settings, string json_path)
        {
            InitializeComponent();
            var esapiWorker = new EsapiWorker(context);

            // Initialize tabs with corresponding ViewModels
            EthosAutoBeamTab.Content = new EthosBeamDialog() { DataContext = new EthosBeamDialogViewModel(context, esapiWorker)};
            TangAutoPlanTab.Content = new BreastAutoDialog() { DataContext = new BreastAutoDialogViewModel(context, settings, esapiWorker)};
            Auto3DSWTab.Content = new Auto3dSlidingWindow() { DataContext = new Auto3dSlidingWindowViewModel(context, settings, json_path, esapiWorker)};
            FluenceExtensionTab.Content = new FluenceExtensionView() { DataContext = new FluenceExtensionViewModel(context, settings, esapiWorker)};
        }
    }
}
