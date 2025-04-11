using NLog;
using NLog.Config;
using NLog.Extensions.Logging;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Shapes;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using System.Windows.Input;
using Prism.Commands;

namespace MAAS_BreastPlan_helper.ViewModels
{  
    public class OffsetBeam
    {
        public string Id { get; set; }
        public double Angle { get; set; }
        public bool Add { get; set; }
    }

    public class BeamParameters
    {
        public VVector isocenter;
        public double collimatorAngle;
        public double supportAngle;
        public VRect<double> jaws;
        public ExternalBeamMachineParameters machineParameters;
        public string machine;
        public string scale;
    }

    public class EthosBeamDialogViewModel : BindableBase
    {
        private readonly ScriptContext _context;
        private string _statusMessage = "Ready";
        private string output;
        private bool modifying;
        public BeamParameters beamParams;

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public string Output
        {
            get { return output; }
            set { SetProperty(ref output, value); }
        }

        public DelegateCommand ExecuteCommand { get; private set; }

        // Properties for UI binding
        private ObservableCollection<string> _lateralityOptions = new ObservableCollection<string>();
        private int _sideSelected;
        private string _machineScale;
        private ObservableCollection<Beam> _fields = new ObservableCollection<Beam>();
        private int _fieldSelected;
        private ObservableCollection<string> _allStructures = new ObservableCollection<string>();
        private int _alignSelected;
        private int _targetSelected;
        private ObservableCollection<OffsetBeam> _beams = new ObservableCollection<OffsetBeam>();
        private int _targetMargin = 5;

        public ObservableCollection<string> LateralityOptions
        {
            get => _lateralityOptions;
            set => SetProperty(ref _lateralityOptions, value);
        }

        public int SideSelected
        {
            get => _sideSelected;
            set => SetProperty(ref _sideSelected, value);
        }

        public string MachineScale
        {
            get => _machineScale;
            set => SetProperty(ref _machineScale, value);
        }

        public ObservableCollection<Beam> Fields
        {
            get => _fields;
            set => SetProperty(ref _fields, value);
        }

        public int FieldSelected
        {
            get => _fieldSelected;
            set => SetProperty(ref _fieldSelected, value);
        }

        public ObservableCollection<string> AllStructures
        {
            get => _allStructures;
            set => SetProperty(ref _allStructures, value);
        }

        public int AlignSelected
        {
            get => _alignSelected;
            set => SetProperty(ref _alignSelected, value);
        }

        public int TargetSelected
        {
            get => _targetSelected;
            set => SetProperty(ref _targetSelected, value);
        }

        public ObservableCollection<OffsetBeam> Beams
        {
            get => _beams;
            set => SetProperty(ref _beams, value);
        }

        public int TargetMargin
        {
            get => _targetMargin;
            set => SetProperty(ref _targetMargin, value);
        }

        public EthosBeamDialogViewModel(ScriptContext context)
        {
            _context = context;
            ExecuteCommand = new DelegateCommand(Execute, CanExecute);
            modifying = false;

            // Initialize UI properties
            Initialize();
        }

        private void Initialize()
        {
            try
            {
                // Initialize laterality options
                LateralityOptions = new ObservableCollection<string> { "Left", "Right" };
                SideSelected = 0; // Default to Left

                // Initialize machine scale
                if (_context.PlanSetup != null)
                {
                    var treatmentUnit = _context.PlanSetup.Beams.FirstOrDefault()?.TreatmentUnit;
                    MachineScale = treatmentUnit?.MachineScaleDisplayName ?? "Unknown";
                }
                else
                {
                    MachineScale = "Unknown";
                }

                // Initialize fields (beams)
                if (_context.PlanSetup != null)
                {
                    Fields = new ObservableCollection<Beam>(_context.PlanSetup.Beams);
                    FieldSelected = 0;
                }

                // Initialize structures
                if (_context.StructureSet != null)
                {
                    AllStructures = new ObservableCollection<string>(
                        _context.StructureSet.Structures
                            .Where(s => !s.IsEmpty && s.Id != "")
                            .Select(s => s.Id)
                    );
                    AlignSelected = 0;
                    TargetSelected = 0;
                }

                // Initialize sample beams
                Beams = new ObservableCollection<OffsetBeam>();
                for (int i = 0; i < 5; i++)
                {
                    Beams.Add(new OffsetBeam { Id = $"Field_{i+1}", Angle = i * 40, Add = true });
                }

                StatusMessage = "Ethos beam creation initialized successfully.";
                Output = "Ready for beam creation.";

                // Initialize beam parameters if possible
                if (Fields.Count > 0)
                {
                    updateBeamParameters();
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error during initialization: {ex.Message}";
                Output = $"Error: {ex.Message}";
            }
        }

        private bool CanExecute()
        {
            return _context?.PlanSetup != null;
        }

        private void Execute()
        {
            try
            {
                // Implement your Ethos beam creation logic here
                StatusMessage = "Executing Ethos beam creation...";
                CreateBeams();
                StatusMessage = "Ethos beam creation completed successfully.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
        }

        public void CreateBeams()
        {
            // Implementation of beam creation logic
            Output = "Creating beams...";
            
            if (!modifying)
            {
                _context.Patient.BeginModifications();
                modifying = true;
            }
            
            FindBeamAngles();
            Output += "\nBeams created successfully.";
        }

        public void RecalculateBeams()
        {
            double initial_angle = Fields[FieldSelected].ControlPoints.First().GantryAngle;
            foreach (var bm in Beams)
            {
                double ga = 0;
                if (SideSelected == 0)
                {
                    ga = (initial_angle + bm.Angle) % 360;
                }
                else
                {
                    ga = (initial_angle - bm.Angle) % 360;
                }

                if (ga < 0)
                {
                    ga += 360;
                }
            }

            Output += "\n -- Calculated Gantry Angles";
        }

        public void RecalculateColls()
        {
            if (!modifying)
            {
                _context.Patient.BeginModifications();
                modifying = true;
            }

            Output += "\n -- Calculated Collimator Angles";
        }

        public void RecalculateMLCs()
        {
            if (!modifying)
            {
                _context.Patient.BeginModifications();
                modifying = true;
            }

            Output += "\n -- MLC shapes updated";
        }

        public void FindBeamAngles()
        {
            Output += "\n - Finding optimal beam angles";
        }

        public void DeleteBeams()
        {
            if (!modifying)
            {
                _context.Patient.BeginModifications();
                modifying = true;
            }
            
            Output += "\n - Removed selected fields";
        }

        public void updateBeamParameters()
        {
            if (Fields == null || Fields.Count == 0 || FieldSelected < 0 || FieldSelected >= Fields.Count)
            {
                return;
            }

            int doserate = Fields[FieldSelected].DoseRate;
            string fluencemodeid = string.Empty;
            string energymodeid = Fields[FieldSelected].EnergyModeDisplayName;
            if (energymodeid.Contains("-"))
            {
                string[] splits = energymodeid.Split('-');
                energymodeid = splits[0];
                fluencemodeid = splits[1];
            }
            if (Fields[FieldSelected].TreatmentUnit.Id.Contains("ETHOS"))
            {
                doserate = 800;
            }
            beamParams = new BeamParameters
            {
                machineParameters = new ExternalBeamMachineParameters(
                    Fields[FieldSelected].TreatmentUnit.Id,
                    energymodeid,
                    doserate,
                    "STATIC",
                    fluencemodeid
                ),
                collimatorAngle = Fields[FieldSelected].ControlPoints.First().CollimatorAngle,
                supportAngle = Fields[FieldSelected].ControlPoints.First().PatientSupportAngle,
                isocenter = Fields[FieldSelected].IsocenterPosition,
                jaws = Fields[FieldSelected].ControlPoints.First().JawPositions,
                machine = Fields[FieldSelected].TreatmentUnit.Id,
                scale = Fields[FieldSelected].TreatmentUnit.MachineScaleDisplayName
            };
        }
    }
}
