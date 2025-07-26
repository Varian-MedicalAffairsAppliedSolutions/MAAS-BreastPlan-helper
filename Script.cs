using System;
using System.Linq;
using System.Windows;
using System.Reflection;
using System.Runtime.CompilerServices;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using MAAS_BreastPlan_helper.Models;
using MAAS_BreastPlan_helper;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using System.Windows.Media.Imaging;
using MAAS.Common.EulaVerification;
using MAAS_BreastPlan_helper.ViewModels;
using MAAS_BreastPlan_helper.Services;

// TODO: Uncomment the following line if the script requires write access.
//15.x or later:
[assembly: ESAPIScript(IsWriteable = true)]


namespace VMS.TPS
{
    public class Script
    {
        // Define the project information for EULA verification
        private const string PROJECT_NAME = "BreastPlan-helper";
        private const string PROJECT_VERSION = "1.0.0";
        private const string LICENSE_URL = "https://varian-medicalaffairsappliedsolutions.github.io/MAAS-BreastPlan-helper/";
        private const string GITHUB_URL = "https://github.com/Varian-MedicalAffairsAppliedSolutions/MAAS-BreastPlan-helper";

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Execute(ScriptContext context)
        {
            try
            {
                // Define constants
                const string PROJECT_NAME = "BreastPlan-helper";
                const string PROJECT_VERSION = "1.0.0";
                const string LICENSE_URL = "https://varian-medicalaffairsappliedsolutions.github.io/MAAS-BreastPlan-helper/";
                const string GITHUB_URL = "https://github.com/Varian-MedicalAffairsAppliedSolutions/MAAS-BreastPlan-helper";

                // License verification
                var eulaVerifier = new EulaVerifier(PROJECT_NAME, PROJECT_VERSION, LICENSE_URL);
                var eulaConfig = EulaConfig.Load(PROJECT_NAME);
                if (eulaConfig.Settings == null)
                    eulaConfig.Settings = new ApplicationSettings();

                if (!eulaVerifier.IsEulaAccepted())
                {
                    MessageBox.Show(
                        $"This version of {PROJECT_NAME} (v{PROJECT_VERSION}) requires license acceptance before first use.\n\n" +
                        "You will be prompted to provide an access code. Please follow the instructions to obtain your code.",
                        "License Acceptance Required",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    BitmapImage qrCode = null;
                    try
                    {
                        string assemblyName = Assembly.GetExecutingAssembly().GetName().Name;
                        qrCode = new BitmapImage(new Uri($"pack://application:,,,/{assemblyName};component/Resources/qrcode.bmp"));
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error loading QR code: {ex.Message}");
                    }

                    if (!eulaVerifier.ShowEulaDialog(qrCode))
                    {
                        MessageBox.Show("License acceptance is required to use this application.\n\nThe application will now close.",
                            "License Not Accepted", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                // Load config.json
                var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                var jsonPath = Path.Combine(path, "config.json");
                SettingsClass settings = null;

                try
                {
                    if (File.Exists(jsonPath))
                    {
                        string jsonContent = File.ReadAllText(jsonPath);
                        settings = JsonConvert.DeserializeObject<SettingsClass>(jsonContent);
                    }
                    else
                    {
                        MessageBox.Show("Config file not found. Default settings will be used.");
                        settings = new SettingsClass();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error reading config.json: {ex.Message}");
                    settings = new SettingsClass();
                }

                // Merge license-specific values into settings
                if (eulaConfig?.Settings != null)
                {
                    settings.Validated = eulaConfig.Settings.Validated;
                    settings.EULAAgreed = eulaConfig.Settings.EULAAgreed;
                }

                // Check for required context
                if (context.Patient == null || context.PlanSetup == null)
                {
                    MessageBox.Show("No active plan selected - exiting.");
                    return;
                }

                // Expiration check
                var noexp_path = Path.Combine(path, "NOEXPIRE");
                bool foundNoExpire = File.Exists(noexp_path);

                var asmCa = Assembly.GetExecutingAssembly().CustomAttributes.FirstOrDefault(ca => ca.AttributeType == typeof(AssemblyExpirationDate));
                DateTime exp;
                var provider = new CultureInfo("en-US");
                DateTime.TryParse(asmCa?.ConstructorArguments.FirstOrDefault().Value as string, provider, DateTimeStyles.None, out exp);

                if (exp < DateTime.Now && !foundNoExpire)
                {
                    MessageBox.Show($"Application has expired. Newer builds with future expiration dates can be found here: {GITHUB_URL}");
                    return;
                }

                // Disclaimer message
                string msg = $"The current BreastPlan-helper application is provided AS IS as a non-clinical, research only tool in evaluation only. The current " +
                             $"application will only be available until {exp.Date} after which the application will be unavailable. " +
                             "By Clicking 'Yes' you agree that this application will be evaluated and not utilized in providing planning decision support\n\n" +
                             $"Newer builds with future expiration dates can be found here: {GITHUB_URL}\n\n" +
                             "See the FAQ for more information on how to remove this pop-up and expiration";

                string msg2 = $"Application will only be available until {exp.Date} after which the application will be unavailable. " +
                              "By Clicking 'Yes' you agree that this application will be evaluated and not utilized in providing planning decision support\n\n" +
                              $"Newer builds with future expiration dates can be found here: {GITHUB_URL} \n\n" +
                              "See the FAQ for more information on how to remove this pop-up and expiration";

                if (!foundNoExpire)
                {
                    if (!settings.Validated)
                    {
                        var res = MessageBox.Show(msg, "Agreement", MessageBoxButton.YesNo);
                        if (res == MessageBoxResult.No) return;
                    }
                    else
                    {
                        var res = MessageBox.Show(msg2, "Agreement", MessageBoxButton.YesNo);
                        if (res == MessageBoxResult.No) return;
                    }
                }

                // Launch UI
                var mainWindow = new MainWindow(context, settings, jsonPath);
                mainWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}\n\n{ex.StackTrace}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}