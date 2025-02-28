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


namespace MAAS_BreastPlan_helper
{  
    public class OffsetBeam
    {
        public string BeamID { get; set; }
        public double AngularOffset { get; set; }
        public double GantryAngle { get; set; }
        public double CollimatorAngle { get; set; }

    }
    public class BeamParameters
    {
        public ExternalBeamMachineParameters machineParameters;
        public double collimatorAngle;
        public double supportAngle;
        public VVector isocenter;
        public VRect<double> jaws;
        public string machine;
        public string scale;
    }

    public class EthosBeamDialogViewModel : BindableBase
    {
        private string output;
        private bool modifying;
        private ScriptContext context;
        public BeamParameters beamParams;

        public string Output
        {
            get { return output; }
            set { SetProperty(ref output, value); }
        }

        private string machineScale;
        public string MachineScale
        {
            get { return machineScale; }
            set { SetProperty(ref machineScale, value); }
        }


        private List<string> lateralityOptions;
        public List<string> LateralityOptions
        {
            get { return lateralityOptions; }
            set { SetProperty(ref lateralityOptions, value); }
        }


        private List<OffsetBeam> beams;
        public List<OffsetBeam> Beams
        {
            get { return beams; }
            set { SetProperty(ref beams, value); }
        }

        private List<Beam> fields;
        public List<Beam> Fields
        {
            get { return fields; }
            set { SetProperty(ref fields, value); }
        }

        private int fieldSelected;
        public int FieldSelected
        {
            get { return fieldSelected; }
            set { SetProperty(ref fieldSelected, value); }
        }


        private int sideSelected;
        public int SideSelected
        {
            get { return sideSelected; }
            set { SetProperty(ref sideSelected, value); }
        }

        private int alignSelected;
        public int AlignSelected
        {
            get { return alignSelected; }
            set { SetProperty(ref alignSelected, value); }
        }
        private List<string> allStructures;
        public List<string> AllStructures
        {
            get { return allStructures; }
            set { SetProperty(ref allStructures, value); }
        }

        private int targetSelected;
        public int TargetSelected
        {
            get { return targetSelected; }
            set { SetProperty(ref targetSelected, value); }
        }

        private double targetMargin;
        public double TargetMargin
        {
            get { return targetMargin; }
            set { SetProperty(ref targetMargin, value); }
        }

        public void updateBeamParameters()
        {
            int doserate = fields[fieldSelected].DoseRate;
            string fluencemodeid = string.Empty;
            string energymodeid = fields[fieldSelected].EnergyModeDisplayName;
            if (energymodeid.Contains("-"))
            {
                string[] splits = energymodeid.Split('-');
                energymodeid = splits[0];
                fluencemodeid = splits[1];
            }
            if (fields[fieldSelected].TreatmentUnit.Id.Contains("ETHOS"))
            {
                doserate = 800;
            }
            beamParams.machineParameters = new ExternalBeamMachineParameters(fields[fieldSelected].TreatmentUnit.Id, energymodeid, doserate, "STATIC", fluencemodeid);
            beamParams.collimatorAngle = fields[fieldSelected].ControlPoints.First().CollimatorAngle;
            beamParams.supportAngle = fields[fieldSelected].ControlPoints.First().PatientSupportAngle;
            beamParams.isocenter = fields[fieldSelected].IsocenterPosition;
            beamParams.jaws = fields[fieldSelected].ControlPoints.First().JawPositions;
            beamParams.machine = fields[fieldSelected].TreatmentUnit.Id;
            beamParams.scale = fields[fieldSelected].TreatmentUnit.MachineScaleDisplayName;
        }

        public EthosBeamDialogViewModel(ScriptContext ctx, EsapiWorker esapiWorker)
        {
            // ctor
            context = ctx;
            modifying = false;

            lateralityOptions = new List<string> { "Left", "Right" };
            Output = "Welcome to the BreastPlan-Helper";

            // Display additional information. Use the active plan if available.
            PlanSetup plan = context.PlanSetup != null ? context.PlanSetup : context.PlansInScope.ElementAt(0);
            ExternalPlanSetup ext_plan = (ExternalPlanSetup)plan;

            fields = new List<Beam>();
            foreach (var bm in ext_plan.Beams)
            {
                if (bm.IsSetupField.Equals(true))
                {
                    continue;
                }
                else
                {
                    fields.Add(bm);
                }
            }

            fieldSelected = 0;
            double initial_angle = fields[0].ControlPoints.First().GantryAngle;
            machineScale = fields[0].TreatmentUnit.MachineScaleDisplayName;
            sideSelected = 0;
            if ((initial_angle < 180 & machineScale == "Varian IEC") || (initial_angle > 180 & machineScale == "Varian Standard"))
            {
                sideSelected = 1;
            }

            // Target structures
            allStructures = new List<string>();
            targetSelected = -1;
            alignSelected = -1;
            string planTargetId = ext_plan.TargetVolumeID;

            foreach (var i in context.StructureSet.Structures)
            {
                //if (i.DicomType != "PTV") continue;
                allStructures.Add(i.Id);
                if (planTargetId == null) continue;
                if (i.Id == planTargetId) targetSelected = allStructures.Count() - 1;
            }
            targetMargin = 10.0;

            beamParams = new BeamParameters();
            updateBeamParameters();

            beams = new List<OffsetBeam>
            {
                new OffsetBeam() { BeamID = "Med 2", AngularOffset = -8, GantryAngle = 0, CollimatorAngle = 5 },
                new OffsetBeam() { BeamID = "Med 3", AngularOffset = -16, GantryAngle = 0, CollimatorAngle = 5 },
                new OffsetBeam() { BeamID = "Med 4", AngularOffset = 8, GantryAngle = 0, CollimatorAngle = 5 },
                new OffsetBeam() { BeamID = "Med 5", AngularOffset = 16, GantryAngle = 0, CollimatorAngle = 5 },
                new OffsetBeam() { BeamID = "Med 6", AngularOffset = 32, GantryAngle = 0, CollimatorAngle = 5 },
                new OffsetBeam() { BeamID = "Med 7", AngularOffset = 48, GantryAngle = 0, CollimatorAngle = 5 },
                new OffsetBeam() { BeamID = "Lat 1", AngularOffset = 144, GantryAngle = 0, CollimatorAngle = 5 },
                new OffsetBeam() { BeamID = "Lat 2", AngularOffset = 160, GantryAngle = 0, CollimatorAngle = 5 },
                new OffsetBeam() { BeamID = "Lat 3", AngularOffset = 176, GantryAngle = 0, CollimatorAngle = 5 },
                new OffsetBeam() { BeamID = "Lat 4", AngularOffset = 184, GantryAngle = 0, CollimatorAngle = 5 },
                new OffsetBeam() { BeamID = "Lat 5", AngularOffset = 192, GantryAngle = 0, CollimatorAngle = 5 },
                new OffsetBeam() { BeamID = "Lat 6", AngularOffset = 200, GantryAngle = 0, CollimatorAngle = 5 },
                new OffsetBeam() { BeamID = "Lat 7", AngularOffset = 208, GantryAngle = 0, CollimatorAngle = 5 },
                new OffsetBeam() { BeamID = "Lat 8", AngularOffset = 216, GantryAngle = 0, CollimatorAngle = 5 },
                new OffsetBeam() { BeamID = "PAB", AngularOffset = 240, GantryAngle = 0, CollimatorAngle = 5 }
            };

            RecalculateBeams();
        }

        public void RecalculateBeams()
        {
            double initial_angle = fields[fieldSelected].ControlPoints.First().GantryAngle;
            foreach (var bm in beams)
            {
                double ga = 0;
                if (sideSelected == 0)
                {
                    ga = (initial_angle + bm.AngularOffset) % 360;
                }
                else
                {
                    ga = (initial_angle - bm.AngularOffset) % 360;
                }

                if (ga < 0)
                {
                    ga += 360;
                }

                bm.GantryAngle = ga;
            }

            Output += "\n -- Calculated Gantry Angles";
        }

        public void RecalculateColls()
        {
            if (!modifying)
            {
                context.Patient.BeginModifications();
                modifying = true;
            }

            PlanSetup plan = context.PlanSetup != null ? context.PlanSetup : context.PlansInScope.ElementAt(0);
            ExternalPlanSetup ext_plan = (ExternalPlanSetup)plan;
            var target_name = allStructures[alignSelected];
            var target_initial = context.StructureSet.Structures.Where(x => x.Id == target_name).First();
            var margins = new FitToStructureMargins(targetMargin);

            foreach (var bm in beams)
            {
                bool make_field = true;
                foreach (var ext in ext_plan.Beams)
                    if (bm.BeamID.Equals(ext.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        make_field = false;
                        string message = string.Format("\nUnable to add field {0}, label matches an existing field ID.\nField IDs must be unique.", bm.BeamID);
                        Output += message;
                        //MessageBox.Show(message);
                    }

                if (make_field)
                {
                    ext_plan.AddStaticBeam(beamParams.machineParameters, beamParams.jaws, bm.CollimatorAngle, bm.GantryAngle, beamParams.supportAngle, beamParams.isocenter);
                    ext_plan.Beams.Last().FitCollimatorToStructure(margins, target_initial, true, true, true);
                    bm.CollimatorAngle = ext_plan.Beams.Last().ControlPoints.First().CollimatorAngle;
                    ext_plan.RemoveBeam(ext_plan.Beams.Last());
                }
            }

            Output += "\n -- Calculated Collimator Angles";
        }

        public void RecalculateMLCs()
        {
            if (!modifying)
            {
                context.Patient.BeginModifications();
                modifying = true;
            }

            PlanSetup plan = context.PlanSetup != null ? context.PlanSetup : context.PlansInScope.ElementAt(0);
            ExternalPlanSetup ext_plan = (ExternalPlanSetup)plan;
            var target_name = allStructures[targetSelected];
            var target_initial = context.StructureSet.Structures.Where(x => x.Id == target_name).First();
            var margins = new FitToStructureMargins(5);

            foreach (var bm in ext_plan.Beams)
            {
                if (bm.MLC == null) continue;
                bm.FitMLCToStructure(margins, target_initial, false, JawFitting.FitToRecommended, OpenLeavesMeetingPoint.OpenLeavesMeetingPoint_Middle, ClosedLeavesMeetingPoint.ClosedLeavesMeetingPoint_Center);
            }
        }

        public void FindBeamAngles()
        {
            PlanSetup plan = context.PlanSetup != null ? context.PlanSetup : context.PlansInScope.ElementAt(0);
            ExternalPlanSetup ext_plan = (ExternalPlanSetup)plan;

            float[,] mlc_leaf_pos = new float[2, 60];
            var target_name = allStructures[targetSelected];
            var target_initial = context.StructureSet.Structures.Where(x => x.Id == target_name).First();
            var margins = new FitToStructureMargins(5);

            foreach (var bm in beams)
            {
                bool make_field = true;
                foreach (var ext in ext_plan.Beams)
                    if (bm.BeamID.Equals(ext.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        make_field = false;
                        string message = string.Format("\nUnable to add field {0}, label matches an existing field ID.\nField IDs must be unique.", bm.BeamID);
                        Output += message;
                        //MessageBox.Show(message);
                    }

                if (make_field)
                {
                    //ext_plan.AddStaticBeam(beamParams.machineParameters, beamParams.jaws, bm.CollimatorAngle, bm.GantryAngle, beamParams.supportAngle, beamParams.isocenter);
                    ext_plan.AddMLCBeam(beamParams.machineParameters, mlc_leaf_pos, beamParams.jaws, bm.CollimatorAngle, bm.GantryAngle, beamParams.supportAngle, beamParams.isocenter);
                    ext_plan.Beams.Last().Id = bm.BeamID;
                    ext_plan.Beams.Last().FitCollimatorToStructure(margins, target_initial, true, true, false);
                    //ext_plan.Beams.Last().FitMLCToStructure(margins, target_initial, true, JawFitting.FitToRecommended, OpenLeavesMeetingPoint.OpenLeavesMeetingPoint_Middle, ClosedLeavesMeetingPoint.ClosedLeavesMeetingPoint_Center);
                    //bm.CollimatorAngle = ext_plan.Beams.Last().ControlPoints.First().CollimatorAngle;
                }
            }

            fields.Clear();
            foreach (var bm in ext_plan.Beams)
            {
                fields.Add(bm);
            }

            // And the main structure with target
            Output += "\n - Created Fields";
            // MessageBox.Show("Created Beams");
        }

        public void DeleteExcessAngles()
        {
            PlanSetup plan = context.PlanSetup != null ? context.PlanSetup : context.PlansInScope.ElementAt(0);
            ExternalPlanSetup ext_plan = (ExternalPlanSetup)plan;

            var selid = fields[fieldSelected].Id;

            List<Beam> to_remove = new List<Beam>();

            foreach (var bm in ext_plan.Beams)
            {
                if (bm.Id != selid)
                {
                    to_remove.Add(bm);
                }
            }

            foreach (var bm in to_remove)
            {
                 ext_plan.RemoveBeam(bm);
            }

            fields.Clear();
            foreach (var bm in ext_plan.Beams)
            {
                fields.Add(bm);
            }
            fieldSelected = 0;
            updateBeamParameters();

            Output += "\n - Removed Fields";
        }
        
        public void CreateBeams()
        {
            if (!modifying)
            {
                context.Patient.BeginModifications();
                modifying = true;
            }
            FindBeamAngles();
        }

        public void DeleteBeams()
        {
            if (!modifying)
            {
                context.Patient.BeginModifications();
                modifying = true;
            }
            DeleteExcessAngles();
        }
    }
}
