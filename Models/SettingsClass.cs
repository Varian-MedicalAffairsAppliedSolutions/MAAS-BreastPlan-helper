using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MAAS_BreastPlan_helper.Models
{
    public class SettingsClass
    {
        public double SmoothX { get; set; }
        public bool KillNormalTissueObjectives { get; set; }
        public double SmoothY { get; set; }
        public bool HotColdIDLSecondOpt { get; set; }
        public bool Debug { get; set; }
        public bool Validated { get; set; }
        public bool EULAAgreed { get; set; }
        public double HotSpotIDL { get; set; }
        public double ColdSpotIDL { get; set; }
        public bool SecondOpt { get; set; }
        public string LMCModel { get; set; }
        public bool Cleanup { get; set; }
        public double MaxDoseGoal { get; set; }
        public bool FixedJaws { get; set; }
        public string Version { get; set; }
        public DateTime LastUpdated { get; set; }

        public SettingsClass()
        {
            Validated = false;
            Version = "1.0.0";
            LastUpdated = DateTime.Now;
        }
    }
}
