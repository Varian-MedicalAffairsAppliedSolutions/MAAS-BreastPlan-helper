using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
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
using VMS.TPS.Common.Model.Types;
using NLog;
using System.Net.NetworkInformation;
using System.ComponentModel;
using MAAS_BreastPlan_helper.ViewModels;
 
namespace MAAS_BreastPlan_helper.Views
{
    /// <summary>
    /// Interaction logic for EthosBeamDialog.xaml
    /// </summary>
    public partial class EthosBeamDialog : UserControl
    {
        private EthosBeamDialogViewModel ViewModel => DataContext as EthosBeamDialogViewModel;

        public EthosBeamDialog()
        {
            InitializeComponent();
        }

        void TimerTick(object state)
        {
            var who = state as string;
            Console.WriteLine(who);
        }

        private void RecalculateBeamAngles(object sender, RoutedEventArgs e)
        {
            ViewModel.RecalculateBeams();
            dgSimple.Items.Refresh();
        }

        private void RecalculateCollimatorAngles(object sender, RoutedEventArgs e)
        {
            ViewModel.RecalculateColls();
            dgSimple.Items.Refresh();
        }
        private void RecalculateMLC(object sender, RoutedEventArgs e)
        {
            ViewModel.RecalculateMLCs();
            dgSimple.Items.Refresh();
        }

        private void DeleteBeams(object sender, RoutedEventArgs e)
        {
            ViewModel.DeleteBeams();
            SeedField.Items.Refresh();
        }

        private void CreateBeams(object sender, RoutedEventArgs e)
        {
            ViewModel.CreateBeams();
            SeedField.Items.Refresh();
        }
    }
}

