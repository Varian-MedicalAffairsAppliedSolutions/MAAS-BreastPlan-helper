using System;
using System.Windows;
using System.Windows.Controls;
using MAAS_BreastPlan_helper.ViewModels;

namespace MAAS_BreastPlan_helper.Views
{
    /// <summary>
    /// Interaction logic for TangentPlacement.xaml
    /// </summary>
    public partial class TangentPlacementView : UserControl
    {
        private TangentPlacementViewModel _viewModel;

        public TangentPlacementView()
        {
            InitializeComponent();
            
            // Subscribe to the Loaded event to initialize radio buttons
            this.Loaded += TangentPlacementView_Loaded;
        }
        
        private void TangentPlacementView_Loaded(object sender, RoutedEventArgs e)
        {
            _viewModel = this.DataContext as TangentPlacementViewModel;
            
            if (_viewModel != null)
            {
                // Initialize radio buttons based on the view model's optimization target
                UpdateRadioButtonsFromViewModel();
            }
        }
        
        private void UpdateRadioButtonsFromViewModel()
        {
            if (_viewModel == null) return;
            
            // Set the correct radio button based on the view model's selected optimization target
            switch (_viewModel.SelectedOptimizationTarget)
            {
                case OptimizationTarget.IpsilateralLung:
                    RadioIpsilateralLung.IsChecked = true;
                    break;
                case OptimizationTarget.ContralateralBreast:
                    RadioContralateralBreast.IsChecked = true;
                    break;
                case OptimizationTarget.Heart:
                    RadioHeart.IsChecked = true;
                    break;
            }
        }

        private void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null)
            {
                _viewModel = this.DataContext as TangentPlacementViewModel;
                if (_viewModel == null) return;
            }

            var rb = sender as RadioButton;
            if (rb != null && rb.Tag != null)
            {
                string value = rb.Tag.ToString();
                OptimizationTarget target;

                if (Enum.TryParse(value, out target))
                {
                    _viewModel.SelectedOptimizationTarget = target;
                }
            }
        }
    }
}
