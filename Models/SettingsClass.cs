using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MAAS_BreastPlan_helper
{
    namespace MAAS_BreastPlan_helper
    {
        public class SettingsClass
        {
            public bool HotColdIDLSecondOpt
            public bool Debug { get; set; }
            public bool Validated { get; set; }
            public bool EULAAgreed { get; set; }

            public double HotSpotIDL { get; set; }
            public double ColdSpotIDL { get; set; }
            public bool SecondOpt { get; set; }

            public string LMCModel { get; set; }

            public bool Cleanup { get; set; }

            public double MaxDoseGoal { get; set; }
        }
    }
}
