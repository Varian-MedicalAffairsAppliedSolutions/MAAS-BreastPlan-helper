using System;
using System.Windows.Input;
using VMS.TPS.Common.Model.API;
using MAAS_BreastPlan_helper.Models;
using MAAS_BreastPlan_helper.Services;
using Prism.Commands;
using Prism.Mvvm;

namespace MAAS_BreastPlan_helper.ViewModels
{
    public class MainViewModel : BindableBase
    {
        private readonly EsapiWorker _esapiWorker;
        private readonly SettingsClass _settings;
        private string _statusMessage = "Ready";
        private string _windowTitle;

        public string StatusMessage
        {
            get { return _statusMessage; }
            set { SetProperty(ref _statusMessage, value); }
        }

        public string WindowTitle
        {
            get { return _windowTitle; }
            set { SetProperty(ref _windowTitle, value); }
        }

        // Child ViewModels for composition
        public BreastFiFViewModel BreastFiFViewModel { get; }
        public TangentPlacementViewModel TangentPlacementViewModel { get; }
        public Auto3dSlidingWindowViewModel Auto3dSlidingWindowViewModel { get; }
        public FluenceExtensionViewModel FluenceExtensionViewModel { get; }
        public EthosBeamDialogViewModel EthosBeamDialogViewModel { get; }

        public DelegateCommand ExecuteCommand { get; private set; }

        public MainViewModel(EsapiWorker esapiWorker, SettingsClass settings, string jsonPath = null)
        {
            _esapiWorker = esapiWorker;
            _settings = settings;
            
            // Initialize child ViewModels with service abstraction
            BreastFiFViewModel = new BreastFiFViewModel(_esapiWorker, _settings);
            TangentPlacementViewModel = new TangentPlacementViewModel(_esapiWorker, _settings);
            Auto3dSlidingWindowViewModel = new Auto3dSlidingWindowViewModel(_esapiWorker, _settings, jsonPath);
            FluenceExtensionViewModel = new FluenceExtensionViewModel(_esapiWorker, _settings);
            EthosBeamDialogViewModel = new EthosBeamDialogViewModel(_esapiWorker);

            ExecuteCommand = new DelegateCommand(Execute, CanExecute);
            
            // Set window title based on validation status
            WindowTitle = AppConfigHelper.GetBoolValue("ValidForClinicalUse") 
                ? "MAAS-BreastPlan-helper" 
                : "MAAS-BreastPlan-helper  \t NOT VALIDATED FOR CLINICAL USE";
        }

        private bool CanExecute()
        {
            return _esapiWorker.GetValue(sc => sc.PlanSetup) != null;
        }

        private void Execute()
        {
            try
            {
                StatusMessage = "Executing main operation...";
                
                _esapiWorker.ExecuteWithErrorHandling(sc =>
                {
                    // Main execution logic here
                    StatusMessage = "Operation completed successfully.";
                }, 
                ex =>
                {
                    StatusMessage = $"Error: {ex.Message}";
                });
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
        }
    }
}
