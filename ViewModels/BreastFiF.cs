using System;
using System.Windows.Input;
using VMS.TPS.Common.Model.API;
using MAAS_BreastPlan_helper.Models;
using System.Linq;
using System.Collections.Generic;
using VMS.TPS.Common.Model.Types;

namespace MAAS_BreastPlan_helper.ViewModels
{
    public class BreastFiFViewModel : ViewModelBase
    {
        private readonly ScriptContext _context;
        private readonly SettingsClass _settings;
        private string _statusMessage = "Ready";
        private int _selectedSubFieldCount = 5;

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged(nameof(StatusMessage));
            }
        }

        public int SelectedSubFieldCount
        {
            get => _selectedSubFieldCount;
            set
            {
                _selectedSubFieldCount = value;
                OnPropertyChanged(nameof(SelectedSubFieldCount));
            }
        }

        public ICommand ExecuteCommand { get; }

        public BreastFiFViewModel(ScriptContext context, SettingsClass settings)
        {
            _context = context;
            _settings = settings;
            ExecuteCommand = new RelayCommand(ExecuteBreastFiF);
        }

        private void ExecuteBreastFiF()
        {
            try
            {
                if (_context.Course == null || _context.StructureSet == null)
                {
                    StatusMessage = "Please load a patient and structure set first.";
                    return;
                }

                var plan = _context.PlanSetup;
                if (plan == null)
                {
                    StatusMessage = "Please load a treatment plan.";
                    return;
                }

                var beams = plan.Beams.ToList();
                var medBeam = beams.FirstOrDefault(b => b.Id.ToUpper().Contains("MED"));
                var latBeam = beams.FirstOrDefault(b => b.Id.ToUpper().Contains("LAT"));

                if (medBeam == null || latBeam == null)
                {
                    StatusMessage = "Plan must contain beams with 'MED' and 'LAT' in their names.";
                    return;
                }

                // Validate patient orientation
                if (_context.Image.ImagingOrientation != PatientOrientation.HeadFirstSupine)
                {
                    StatusMessage = "Patient must be in Head First Supine position.";
                    return;
                }

                // Create Field-in-Field plan
                var courseId = _context.Course.Id;
                var planId = $"FiF_{plan.Id}";
                
                // Create a copy of the current plan
                var fiFPlan = _context.Course.AddPlanSetup(_context.StructureSet);
                if (fiFPlan == null)
                {
                    StatusMessage = "Failed to create new plan.";
                return;
            }

                // Copy beams from original plan
                foreach (var beam in beams)
                {
                    var newBeam = fiFPlan.AddStaticBeam(beam.TreatmentUnit, beam.MLCPlanType, beam.Id,
                        beam.GantryAngle, beam.CollimatorAngle, beam.CouchAngle, beam.IsocenterPosition);
                    
                    // Copy control points
                    for (int i = 0; i < beam.ControlPoints.Count; i++)
                    {
                        var cp = beam.ControlPoints[i];
                        newBeam.CreateControlPoint(i, cp.MetersetWeight, cp.JawPositions, cp.LeafPositions);
                    }
                }

                StatusMessage = "Field-in-Field plan created successfully.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
        }
    }
}