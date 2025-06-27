using System;
using System.Configuration;
using System.IO;
using Newtonsoft.Json;
using MAAS_BreastPlan_helper.Models;

namespace MAAS_BreastPlan_helper.Services
{
    public static class AppConfigHelper
    {
        private static SettingsClass _settings;
        private static readonly object _lock = new object();

        public static string GetValueByKey(string key)
        {
            try
            {
                return ConfigurationManager.AppSettings[key] ?? "false";
            }
            catch
            {
                return "false";
            }
        }

        public static SettingsClass LoadSettings(string configPath = null)
        {
            lock (_lock)
            {
                if (_settings != null)
                    return _settings;

                try
                {
                    var settingsPath = configPath ?? Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "MAAS-BreastPlan-helper",
                        "config.json");

                    if (File.Exists(settingsPath))
                    {
                        var json = File.ReadAllText(settingsPath);
                        _settings = JsonConvert.DeserializeObject<SettingsClass>(json);
                    }
                    else
                    {
                        _settings = new SettingsClass();
                        SaveSettings(_settings, settingsPath);
                    }
                }
                catch (Exception)
                {
                    _settings = new SettingsClass();
                }

                return _settings;
            }
        }

        public static void SaveSettings(SettingsClass settings, string configPath = null)
        {
            try
            {
                var settingsPath = configPath ?? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "MAAS-BreastPlan-helper",
                    "config.json");

                var directory = Path.GetDirectoryName(settingsPath);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                File.WriteAllText(settingsPath, json);

                _settings = settings;
            }
            catch (Exception)
            {
                // Log error if needed
            }
        }

        public static bool GetBoolValue(string key, bool defaultValue = false)
        {
            var value = GetValueByKey(key);
            return bool.TryParse(value, out bool result) ? result : defaultValue;
        }

        public static T GetValue<T>(string key, T defaultValue = default(T))
        {
            try
            {
                var value = GetValueByKey(key);
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }
    }
} 