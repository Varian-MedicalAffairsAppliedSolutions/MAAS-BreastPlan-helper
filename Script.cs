using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using MAAS_BreastPlan_helper;
using System.Windows.Input;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using MAAS_BreastPlan_helper.MAAS_BreastPlan_helper;

// TODO: Uncomment the following line if the script requires write access.
//15.x or later:
[assembly: ESAPIScript(IsWriteable = true)]


namespace VMS.TPS
{
  public class Script
  {
    private string newBuildURL = "https://github.com/Varian-Innovation-Center/MAAS-BreastPlan-helper";

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

        // Check NOEXPIRE FILE
        var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        var noexp_path = Path.Combine(path, "NOEXPIRE");
        bool foundNoExpire = System.IO.File.Exists(noexp_path);

        // search for json config in current dir, NOTE: see instructions on how to create this at the top of the file.
        var json_path = Path.Combine(path, "config.json");
        if (!File.Exists(json_path)) { throw new Exception($"Could not locate json path {json_path}"); }
        var settings = JsonConvert.DeserializeObject<SettingsClass>(File.ReadAllText(json_path));

        // Get expiration date from Assembly
        var asmCa = Assembly.GetExecutingAssembly().CustomAttributes.FirstOrDefault(ca => ca.AttributeType == typeof(AssemblyExpirationDate));
        DateTime exp;
        var provider = new CultureInfo("en-US");
        DateTime.TryParse(asmCa.ConstructorArguments.FirstOrDefault().Value as string, provider, DateTimeStyles.None, out exp);

        // Check expiration date
        if (exp < DateTime.Now && !foundNoExpire)
        {
            MessageBox.Show("Application has expired. Newer builds with future expiration dates can be found here: https://github.com/Varian-Innovation-Center/MAAS-PlanComplexity");
            return;
        }

        // Check that they have agreed to EULA
        if (!settings.EULAAgreed)
        {
            var msg0 = "You are bound by the terms of the Varian Limited Use Software License Agreement (LULSA). \"To stop viewing this message set EULA to \"true\" in DoseRateEditor.exe.config\"\nShow license agreement?";
            string title = "Varian LULSA";
            var buttons = MessageBoxButton.YesNo;
            var result = MessageBox.Show(msg0, title, buttons);
            if (result == MessageBoxResult.Yes)
            {
                Process.Start("notepad.exe", Path.Combine(path, "license.txt"));
            }

            // Save that they have seen EULA
            settings.EULAAgreed = true;
            File.WriteAllText(Path.Combine(path, "config.json"), JsonConvert.SerializeObject(settings));
        }

        // Display opening msg
        string msg = $"The current MAAS-SFRThelper application is provided AS IS as a non-clinical, research only tool in evaluation only. The current " +
        $"application will only be available until {exp.Date} after which the application will be unavailable. " +
        $"By Clicking 'Yes' you agree that this application will be evaluated and not utilized in providing planning decision support\n\n" +
        $"Newer builds with future expiration dates can be found here: {newBuildURL}\n\n" +
        "See the FAQ for more information on how to remove this pop-up and expiration";
        var res = MessageBox.Show(msg, "Agreement  ", MessageBoxButton.YesNo);

        // If they don't agree close window
        if (res == MessageBoxResult.No)
        {
            return;
        }


        var mainWindow = new MainWindow(context, settings)
        {
            DataContext  = new MainViewModel(settings)
        };
        
        mainWindow.ShowDialog();
    }
  }
}
