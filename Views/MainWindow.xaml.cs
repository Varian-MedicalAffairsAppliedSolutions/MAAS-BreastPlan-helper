using MAAS_BreastPlan_helper;
using MAAS_BreastPlan_helper.MAAS_BreastPlan_helper;
using MAAS_BreastPlan_helper.ViewModels;
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
    /// Interaction logic for Window1.xaml
    /// </summary>
    /// 
   
    // TODO: make second tab the default
    public partial class MainWindow : Window
    {
        public MainWindow(ScriptContext context, SettingsClass settings)
        {
            InitializeComponent();
            EthosAutoBeamTab.Content = new EthosBeamDialog() { DataContext = new EthosBeamDialogViewModel(context) };
            TangAutoPlanTab.Content = new BreastAutoDialog() { DataContext = new BreastAutoDialogViewModel(context, settings) };
        }
    }
}
