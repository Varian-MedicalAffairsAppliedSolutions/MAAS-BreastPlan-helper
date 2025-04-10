using System;
using System.Windows.Input;
using VMS.TPS.Common.Model.API;
using MAAS_BreastPlan_helper.Models;
using Prism.Commands;
using Prism.Mvvm;

namespace MAAS_BreastPlan_helper.ViewModels
{
    public class MainViewModel : BindableBase
    {
        private readonly ScriptContext _context;
        private readonly SettingsClass _settings;
        private string _statusMessage = "Ready";

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public DelegateCommand ExecuteCommand { get; private set; }

        public MainViewModel(ScriptContext context, SettingsClass settings)
        {
            _context = context;
            _settings = settings;
            ExecuteCommand = new DelegateCommand(Execute, CanExecute);
        }

        private bool CanExecute()
        {
            return _context?.PlanSetup != null;
        }

        private void Execute()
        {
            try
            {
                StatusMessage = "Executing main operation...";
                // Add your implementation
                StatusMessage = "Operation completed successfully.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
        }
    }
}
