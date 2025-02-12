using Prism.Mvvm;
using System.Windows.Threading;
using System;
using System.Windows;

namespace MAAS_BreastPlan_helper.Models
{
    public class ProgressService : BindableBase
    {
        // Thread-safe Singleton using Lazy<T>
        private static readonly Lazy<ProgressService> _instance =
            new Lazy<ProgressService>(() => new ProgressService());

        public static ProgressService Instance => _instance.Value;

        // Private constructor to enforce singleton pattern
        private ProgressService() { }

        private double _progressValue;
        public double ProgressValue
        {
            get => _progressValue;
            set => SetProperty(ref _progressValue, value);
        }

        private Visibility _progressBarVisibility = Visibility.Collapsed;
        public Visibility ProgressBarVisibility
        {
            get => _progressBarVisibility;
            set => SetProperty(ref _progressBarVisibility, value);
        }


        private string _progressMessage;
        public string ProgressMessage
        {
            get => _progressMessage;
            set => SetProperty(ref _progressMessage, value);
        }

        // Methods to manage progress bar
        public void StartProgress()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ProgressValue = 0;
                ProgressBarVisibility = Visibility.Visible;
                ProgressMessage = "Initializing...";
            });
        }

        public void UpdateProgress(double value, string message = null)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ProgressValue = value;
                if (!string.IsNullOrEmpty(message))
                    ProgressMessage = message;
            });
        }

        public void CompleteProgress()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ProgressValue = 100;
                ProgressMessage = "Completed";
                ProgressBarVisibility = Visibility.Collapsed;
            });
        }
    }
}