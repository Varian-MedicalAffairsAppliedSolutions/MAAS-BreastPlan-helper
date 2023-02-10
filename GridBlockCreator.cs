using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using GridBlockCreator;
using System.Windows.Input;

// TODO: Replace the following version attributes by creating AssemblyInfo.cs. You can do this in the properties of the Visual Studio project.
[assembly: AssemblyVersion("1.0.0.1")]
[assembly: AssemblyFileVersion("1.0.0.1")]
[assembly: AssemblyInformationalVersion("1.0")]

// TODO: Uncomment the following line if the script requires write access.
//15.x or later:
[assembly: ESAPIScript(IsWriteable = true)]

namespace VMS.TPS
{
  public class Script
  {
    private bool isDebug = false;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Execute(ScriptContext context)
    {

        if (context.Patient == null || context.PlanSetup == null)
        {
            MessageBox.Show("No active plan selected - exiting.");
            return;
        }

        PlanSetup plan = context.PlanSetup != null ? context.PlanSetup : context.PlansInScope.ElementAt(0);
        if (plan.PlanType != PlanType.ExternalBeam)
        {
            MessageBox.Show("Please open an external beam plan.");
            return;
        }

        ExternalPlanSetup ext_plan = (ExternalPlanSetup)plan;

        int tx_beams = 0;
        foreach (var bm in ext_plan.Beams)
        {
            if (bm.IsSetupField.Equals(true))
            {
                continue;
            }
            else
            {
                tx_beams += 1;
            }
        }
        if (tx_beams < 1)
        {
            MessageBox.Show("There must be at least 1 treatment field in the plan.\nSetup fields are ignored.");
            return;
        }

        //var showBanner = System.Configuration.ConfigurationManager.AppSettings["DisplayTerms"].ToLower() == "true";
        //  MessageBox.Show($"IsDebug Flag: {System.Configuration.ConfigurationManager.AppSettings["DisplayTerms"].ToLower()}");
        var mainWindow = new GridBlockCreator.MainWindow(context, isDebug);
        
        mainWindow.ShowDialog();
    }
  }
}
