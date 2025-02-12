using MahApps.Metro.Controls;
using MAAS_BreastPlan_helper;
using MAAS_BreastPlan_helper.MAAS_BreastPlan_helper;
using MAAS_BreastPlan_helper.ViewModels;
using MAAS_BreastPlan_helper.Models;
using MAAS_BreastPlan_helper.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using VMS.TPS.Common.Model.API;

namespace MAAS_BreastPlan_helper
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        public MainWindow(ScriptContext context, SettingsClass settings, string json_path)
        {
            InitializeComponent();

            // Initialize tabs with corresponding ViewModels
            EthosAutoBeamTab.Content = new EthosBeamDialog() { DataContext = new EthosBeamDialogViewModel(context) };
            TangAutoPlanTab.Content = new BreastAutoDialog() { DataContext = new BreastAutoDialogViewModel(context, settings) };
            Auto3DSWTab.Content = new Auto3dSlidingWindow() { DataContext = new Auto3dSlidingWindowViewModel(context, settings, json_path) };
            FluenceExtensionTab.Content = new FluenceExtensionView() { DataContext = new FluenceExtensionViewModel(context, settings) };
        }

        // ✅ Run on Background Thread to Keep UI Responsive
        public async void RunScriptTask()
        {
            ProgressService.Instance.StartProgress(); // Show progress bar

            await Task.Run(async () =>
            {
                for (int i = 0; i <= 100; i += 10)
                {
                    ProgressService.Instance.UpdateProgress(i, $"Processing {i}%...");
                    await Task.Delay(500); // Simulates long-running task
                }
            });

            ProgressService.Instance.CompleteProgress(); // Hide progress bar
        }
    }
}