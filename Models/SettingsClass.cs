using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Prism.Mvvm;

namespace MAAS_BreastPlan_helper.Models
{
    public class SettingsClass : BindableBase
    {
        private double _smoothX;
        public double SmoothX
        {
            get { return _smoothX; }
            set { SetProperty(ref _smoothX, value); }
        }

        private bool _killNormalTissueObjectives;
        public bool KillNormalTissueObjectives
        {
            get { return _killNormalTissueObjectives; }
            set { SetProperty(ref _killNormalTissueObjectives, value); }
        }

        private double _smoothY;
        public double SmoothY
        {
            get { return _smoothY; }
            set { SetProperty(ref _smoothY, value); }
        }

        private bool _hotColdIDLSecondOpt;
        public bool HotColdIDLSecondOpt
        {
            get { return _hotColdIDLSecondOpt; }
            set { SetProperty(ref _hotColdIDLSecondOpt, value); }
        }

        private bool _debug;
        public bool Debug
        {
            get { return _debug; }
            set { SetProperty(ref _debug, value); }
        }

        private bool _validated;
        public bool Validated
        {
            get { return _validated; }
            set { SetProperty(ref _validated, value); }
        }

        private bool _eulaAgreed;
        public bool EULAAgreed
        {
            get { return _eulaAgreed; }
            set { SetProperty(ref _eulaAgreed, value); }
        }

        private double _hotSpotIDL;
        public double HotSpotIDL
        {
            get { return _hotSpotIDL; }
            set { SetProperty(ref _hotSpotIDL, value); }
        }

        private double _coldSpotIDL;
        public double ColdSpotIDL
        {
            get { return _coldSpotIDL; }
            set { SetProperty(ref _coldSpotIDL, value); }
        }

        private bool _secondOpt;
        public bool SecondOpt
        {
            get { return _secondOpt; }
            set { SetProperty(ref _secondOpt, value); }
        }

        private string _lmcModel;
        public string LMCModel
        {
            get { return _lmcModel; }
            set { SetProperty(ref _lmcModel, value); }
        }

        private bool _cleanup;
        public bool Cleanup
        {
            get { return _cleanup; }
            set { SetProperty(ref _cleanup, value); }
        }

        private double _maxDoseGoal;
        public double MaxDoseGoal
        {
            get { return _maxDoseGoal; }
            set { SetProperty(ref _maxDoseGoal, value); }
        }

        private bool _fixedJaws;
        public bool FixedJaws
        {
            get { return _fixedJaws; }
            set { SetProperty(ref _fixedJaws, value); }
        }

        private string _version;
        public string Version
        {
            get { return _version; }
            set { SetProperty(ref _version, value); }
        }

        private DateTime _lastUpdated;
        public DateTime LastUpdated
        {
            get { return _lastUpdated; }
            set { SetProperty(ref _lastUpdated, value); }
        }

        public SettingsClass()
        {
            Validated = false;
            Version = "1.0.0";
            LastUpdated = DateTime.Now;
        }
    }
}
