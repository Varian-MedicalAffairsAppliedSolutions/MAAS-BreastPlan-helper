using MAAS_BreastPlan_helper.MAAS_BreastPlan_helper;
using NLog.Layouts;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;

namespace MAAS_BreastPlan_helper
{
	

	public class MainViewModel: BindableBase
    {

        public DelegateCommand HyperlinkCmd { get; private set; }

        private string postText;
        public string PostText
        {
            get { return postText; }
            set { SetProperty(ref postText, value); }
        }

        private void OnHyperlink()
        {
            var url = "http://medicalaffairs.varian.com/download/VarianLUSLA.pdf";
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(url)
                );
        }

        private bool CanHyperlink()
        {
            return true;
        }


        public MainViewModel(SettingsClass settings)
        {
            //MessageBox.Show($"Is Debug == {isDebug}");
            //var hlink = new Hyperlink() { NavigateUri = new Uri("http://medicalaffairs.varian.com/download/VarianLUSLA.pdf") };
            //Footer += hlink;
            PostText = "";
            if (!settings.Validated)
            {
                PostText = " *** Not Validated for clinical use ***";
            }

            HyperlinkCmd = new DelegateCommand(OnHyperlink, CanHyperlink);

        }

    }
}
