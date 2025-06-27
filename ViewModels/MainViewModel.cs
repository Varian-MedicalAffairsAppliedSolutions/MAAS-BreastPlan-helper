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
        public DelegateCommand<string> TabSelectionChangedCommand { get; private set; }

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
            TabSelectionChangedCommand = new DelegateCommand<string>(OnTabSelectionChanged);
            
            // Watch for Auto3dSlidingWindow completion to refresh other ViewModels
            Auto3dSlidingWindowViewModel.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(Auto3dSlidingWindowViewModel.PlanCreationCompleted) && 
                    Auto3dSlidingWindowViewModel.PlanCreationCompleted)
                {
                    RefreshAllViewModels();
                    Auto3dSlidingWindowViewModel.PlanCreationCompleted = false; // Reset flag
                }
            };
            
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

        private void OnTabSelectionChanged(string tabName)
        {
            try
            {
                // Refresh structure references when navigating to tabs that could have stale structure references
                if (tabName == "FluenceExtension")
                {
                    FluenceExtensionViewModel.RefreshData();
                    StatusMessage = "Fluence Extension tab loaded - structure references refreshed.";
                }
                else if (tabName == "TangentPlacement")
                {
                    TangentPlacementViewModel.RefreshData();
                    StatusMessage = "Tangent Placement tab loaded - structure references refreshed.";
                }
                else if (tabName == "Auto3dSlidingWindow")
                {
                    StatusMessage = "Auto 3D Sliding Window tab loaded.";
                }
                else if (tabName == "BreastFiF")
                {
                    StatusMessage = "Breast Field-in-Field tab loaded.";
                }
                else if (tabName == "EthosBeamDialog")
                {
                    StatusMessage = "Ethos Beam Dialog tab loaded.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error refreshing tab data: {ex.Message}";
            }
        }

        private void RefreshAllViewModels()
        {
            try
            {
                // Force refresh all ViewModels that store structure references
                FluenceExtensionViewModel.RefreshData();
                TangentPlacementViewModel.RefreshData();
                // Note: We don't refresh Auto3dSlidingWindowViewModel as it just completed its operation
                
                StatusMessage = "All ViewModels refreshed after plan creation.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error refreshing ViewModels: {ex.Message}";
            }
        }
    }
}
