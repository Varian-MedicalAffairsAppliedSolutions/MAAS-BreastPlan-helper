using MAAS_BreastPlan_helper.MAAS_BreastPlan_helper;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using VMS.TPS.Common.Model.API;
using MAAS_BreastPlan_helper.Models;
using System.Numerics;
using VMS.TPS.Common.Model.Types;
using Prism.Logging;
using System.Collections.ObjectModel;
using Prism.Commands;
using static VMS.TPS.Common.Model.Types.DoseValue;
using System.Security.Cryptography;
using System.Xml;
using static MAAS_BreastPlan_helper.Models.Utils;
using static MAAS_BreastPlan_helper.ViewModels.BreastAutoDialogViewModel;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using MAAS_BreastPlan_helper.Properties;
using System.IO;

// Anthony Note fix

// TODO
// 2. get field separation
    // 1. At beam isocenter 
    // 2. At beam edge iso plane
    // 3. At beam edge Dmax plane after optimization 
// 3. Display PTV volume (at end for script generated PTV)



namespace MAAS_BreastPlan_helper.ViewModels
{
    public class Auto3dSlidingWindowViewModel : BindableBase
    {
        private ScriptContext Context { get; set; }
        private SettingsClass Settings { get; set; }
        private Patient Patient { get; set; }   
        //private ExternalPlanSetup Plan { get; set; }
        private ExternalPlanSetup plan;

        private double sepIso;

        public double SepIso
        {
            get { return sepIso; }
            set { SetProperty(ref sepIso, value); }
        }

        private double sepIsoEdge;

        public double SepIsoEdge
        {
            get { return sepIsoEdge; }
            set { SetProperty(ref sepIsoEdge, value); }
        }

        private double sepDmaxEdgeAfterOpt;

        public double SepDmaxEdgeAfterOpt
        {
            get { return sepDmaxEdgeAfterOpt; }
            set { SetProperty(ref sepDmaxEdgeAfterOpt, value); }
        }

        private bool customPTV;

        public bool CustomPTV
        {
            get { return customPTV; }
            set { SetProperty(ref customPTV, value); }
        }

        private bool cboPTVEnabled;

        public bool CBOPTVEnabled
        {
            get { return cboPTVEnabled; }
            set { SetProperty(ref cboPTVEnabled, value); }
        }

        private bool lblPTVEnabled;

        public bool LBLPTVEnabled
        {
            get { return lblPTVEnabled; }
            set { SetProperty(ref lblPTVEnabled, value); }
        }


        public ExternalPlanSetup Plan
        {
            get { return plan; }
            set { SetProperty(ref plan, value); }
        }

        private string selectedEnergy;
        public string SelectedEnergy
        {
            get { return selectedEnergy; }
            set { SetProperty(ref selectedEnergy, value); }
        }


        public ObservableCollection<SIDE> BreastSides { get; set; }
        private SIDE selectedBreastSide;

        public SIDE SelectedBreastSide
        {
            get { return selectedBreastSide; }
            set { SetProperty(ref selectedBreastSide, value); }
        }

        private double maxDoseGoal;

        public double MaxDoseGoal
        {
            get { return maxDoseGoal; }
            set { SetProperty(ref maxDoseGoal, value); }
        }



        public ObservableCollection<string> StatusBoxItems { get; set; }

        public DelegateCommand CreatePlanCMD { get; set; }

        // -- Lung stuff --
        private Structure ipsi_lung;

        public Structure Ipsi_lung
        {
            get { return ipsi_lung; }
            set { SetProperty(ref ipsi_lung, value); }
        }

        public ObservableCollection<Structure> PTVItems { get; set; }
        private Structure selectedPTV;

        public Structure SelectedPTV
        {
            get { return selectedPTV; }
            set { SetProperty(ref selectedPTV, value); }
        }


        public ObservableCollection<Structure> LungStructures { get; set; }

        private string lmcText;

        public string LMCText
        {
            get { return lmcText; }
            set { SetProperty(ref lmcText, value); }
        }

        public DelegateCommand CbCustomPTV_Click { get; set; }


        // -- Heart stuff --
        private Structure heart;

        public Structure Heart
        {
            get { return heart; }
            set { SetProperty(ref heart, value); }
        }

        public ObservableCollection<Structure> HeartStructures { get; set; }

        private string JsonPath { get; set; }  // Path to config file

        /// <summary>
        /// Determine breast side based on isocenter coordinates
        /// </summary>
        /// <param name="plan">The treatment plan.</param>
        /// <returns>The element of the SIDE enum representing the treatment side.</returns>

        public Auto3dSlidingWindowViewModel(ScriptContext context, SettingsClass settings, string json_path) 
        {
            SepIso = 0;
            SepIsoEdge = 0;
            SepDmaxEdgeAfterOpt = 0;

            Context = context;
            Settings = settings;
            JsonPath = json_path;

            Patient = Context.Patient;
            Patient.BeginModifications();

            if (Patient == null)
            {
                throw new Exception("Patient is null");
            }

            Plan = Context.PlanSetup as ExternalPlanSetup;

            if (Plan == null) { 
                throw new Exception("Plan is null");
            }

            CreatePlanCMD = new DelegateCommand(OnCreateBreastPlan);

            StatusBoxItems = new ObservableCollection<string>();

            // Initialize observable collections
            var lHeartStructures = Plan.StructureSet.Structures.Where(s => s.Id.ToLower().Contains("heart")).ToList();
            HeartStructures = new ObservableCollection<Structure>();
            foreach (var structure in lHeartStructures) { HeartStructures.Add(structure); }
            Heart = HeartStructures.FirstOrDefault();

            BreastSides = new ObservableCollection<SIDE>() { SIDE.RIGHT, SIDE.LEFT };
            SelectedBreastSide = FindTreatmentSide(Plan); ;

            var lLungStructures = Plan.StructureSet.Structures.Where(s => s.Id.ToLower().Contains("lung")).ToList();
            LungStructures = new ObservableCollection<Structure>();
            foreach (var strucuture in lLungStructures) { LungStructures.Add(strucuture); }

            // Select lung structure based on the position of the center point
            if(selectedBreastSide == SIDE.LEFT) 
            { 
                Ipsi_lung = LungStructures.Where(s => s.CenterPoint.x > 0).FirstOrDefault();
            }
            else
            {
                Ipsi_lung = LungStructures.Where(s => s.CenterPoint.x <= 0).FirstOrDefault();
            }
            
            /*
            Ipsi_lung = LungStructures.FirstOrDefault();

            if (SelectedBreastSide == SIDE.LEFT)
            {
                var lung_candidates = LungStructures.Where(s => s.Id.ToLower().Contains("left")).ToList().Concat(LungStructures.Where(s => s.Id.ToLower().Contains("lt")).ToList());// + LungStructures.Where(s => s.Id.ToLower().Contains("left")).ToList();
                if (lung_candidates.Count() > 0)
                {
                    Ipsi_lung = lung_candidates.FirstOrDefault();
                }
            }*/

            MaxDoseGoal = settings.MaxDoseGoal;
            
            SelectedEnergy = Utils.GetFluenceEnergyMode(Plan.Beams.Where(b => !b.IsSetupField).First()).Item2;

            LMCText = settings.LMCModel;

            PTVItems = new ObservableCollection<Structure>();
            foreach(var s in Plan.StructureSet.Structures.Where(s => s.Id.ToLower().Contains("ptv")))
            {
                PTVItems.Add(s);
            }

            // Initialize Custom ptv chekbox to false
            CustomPTV = false;
            LBLPTVEnabled = false;
            CBOPTVEnabled = false;

            CbCustomPTV_Click = new DelegateCommand(OnCustomPTV_Click);


            var body = Plan.StructureSet.Structures.Where(s => s.Id.ToLower().Contains("body")).First();

            SepIso = Utils.ComputeBeamSeparation(Plan.Beams.First(), Plan.Beams.Last(), body); // center of field iso plane
            SepIsoEdge = Utils.ComputeBeamSeparationWholeField(Plan.Beams.First(), Plan.Beams.Last(), body); // field edge iso plane
            
            // Pick:
            // 1. Breast side,
            // 2. Ipsilateral lung,
            // 3. Select heart

        }

        private void OnCustomPTV_Click()
        {
            if (customPTV)
            {
                CBOPTVEnabled = true;
                LBLPTVEnabled = true;
                SelectedPTV = PTVItems.FirstOrDefault();

            }
            else
            {
                CBOPTVEnabled = false;
                LBLPTVEnabled = false;
                SelectedPTV = null;
            }
        }

        public void OnCreateBreastPlan()
        {
            // Save some properties back to config
            // LMC
            Settings.HotSpotIDL = MaxDoseGoal;
            Settings.LMCModel = LMCText;
            File.WriteAllText(JsonPath, JsonConvert.SerializeObject(Settings));


            List<Beam> Beams = Plan.Beams.Where(b => !b.IsSetupField).ToList();
            int nrBeams = Beams.Count();
            //await UpdateListBox("Starting...");
            if (nrBeams != 2)
            {
                throw new Exception($"Must have 2 beams but got {nrBeams}");
            }

            // Check dose
            var bHasDose = Plan.Dose != null;
            if (!bHasDose)
            {
                throw new Exception("Error no dose!");
            }

            // TODO: find better gantry diff function
            // Check the beams are > 160 deg apart
            var ganDiff = Math.Abs(Beams.First().ControlPoints.First().GantryAngle - Beams.Last().ControlPoints.First().GantryAngle);
            if (ganDiff < 160)
            {
                throw new Exception("Gantry angle difference is greater than 160 degrees");
            }

            var bAreSameEnergy = Beams.First().EnergyModeDisplayName == Beams.Last().EnergyModeDisplayName;
            if (!bAreSameEnergy)
            {
                throw new Exception("Beams are not same energy");
            }

            // Check that the field weights sum to 1
            var weightSum = Beams.Select(b => b.WeightFactor).Sum();
            if (weightSum != 1)
            {
                throw new Exception($"Beam weights don't sum to 1: {weightSum}");
            }

            //await UpdateListBox("All checks passed, copying to new plan");

            // Copy plan and set new name
            var NewPlan = Context.Course.CopyPlanSetup(Plan) as ExternalPlanSetup;
            NewPlan.Id = Utils.GetNewPlanName(Context.Course, Plan.Id, 13);

            NewPlan.SetCalculationModel(CalculationType.PhotonVolumeDose, Plan.PhotonCalculationModel);
            //await UpdateListBox($"Set vol dose model");
            NewPlan.SetCalculationModel(CalculationType.PhotonIMRTOptimization, Plan.GetCalculationModel(CalculationType.PhotonIMRTOptimization));
            //await UpdateListBox($"Set opt model");

            //await UpdateListBox($"New plan created with id {NewPlan.Id}");

            // Check if there is a PTV
            var CopiedSS = NewPlan.StructureSet;
            //var PTV = CopiedSS.Structures.Where(s => s.Id.ToLower().Contains("ptv")).First();
            var body = CopiedSS.Structures.Where(s => s.Id.ToLower().Contains("body")).First();
            //var bExistsPTV = PTV_OPT != null;

            //await UpdateListBox($"Tried to find PTV and Body");



            //await UpdateListBox($"Sparing heart and lung {NewPlan.Id}");

            //DoseValue renormPTV = Plan.StructureSet.Structures.Where(s => s.Id.ToLower().Contains("body")).First();


            // Perform dose calc
            NewPlan.SetCalculationModel(CalculationType.PhotonVolumeDose, Plan.PhotonCalculationModel);
            NewPlan.CalculateDose();
            NewPlan.SetPrescription(25, new DoseValue(2, DoseUnit.Gy), 1);

            //await UpdateListBox($"Set norm value");
            var maxBodyDose = Plan.GetDVHCumulativeData(body, DoseValuePresentation.Relative, VolumePresentation.Relative, 1).MaxDose;
            NewPlan.PlanNormalizationValue = maxBodyDose.Dose;

            var DM3D = NewPlan.Dose.DoseMax3D;
            //await UpdateListBox($"Dose max of newplan and plan = {NewPlan.Dose.DoseMax3D} | {Plan.Dose.DoseMax3D}");
            //await UpdateListBox($"Dose is calculated");


            // Delete existing opt structures
            var optStructsOld = CopiedSS.Structures.Where(s => s.Id.StartsWith("__")).ToList();
            foreach (var os in optStructsOld) { CopiedSS.RemoveStructure(os); }

            Structure PTV_OPT = CopiedSS.AddStructure("DOSE_REGION", "__PTV_OPT");
            var margin = new AxisAlignedMargins(StructureMarginGeometry.Inner, 5, 5, 5, 5, 5, 5);

            if (!CustomPTV)
            {
                // Create 50% IDL structure as PTV_OPT if PTV not selected 
                //await UpdateListBox($"PTV_OPT created");
                PTV_OPT.ConvertDoseLevelToStructure(NewPlan.Dose, new DoseValue(50, DoseUnit.Percent));
                PTV_OPT.SegmentVolume = PTV_OPT.AsymmetricMargin(margin);
            }

            else
            {
                PTV_OPT.SegmentVolume = SelectedPTV.SegmentVolume;
            }


            if (PTV_OPT.Volume < 0.0001)
            {
                throw new Exception($"Structure volume of PTV opt is too low: {PTV_OPT.Volume} CC");
            }

            // Create 70 - 95% Isodose level structures
            Structure IDL75 = CopiedSS.AddStructure("DOSE_REGION", "__IDL75");
            IDL75.ConvertDoseLevelToStructure(NewPlan.Dose, new DoseValue(75, DoseUnit.Percent));

            Structure IDL80 = CopiedSS.AddStructure("DOSE_REGION", "__IDL80");
            IDL80.ConvertDoseLevelToStructure(NewPlan.Dose, new DoseValue(80, DoseUnit.Percent));

            Structure IDL85 = CopiedSS.AddStructure("DOSE_REGION", "__IDL85"); 
            IDL85.ConvertDoseLevelToStructure(NewPlan.Dose, new DoseValue(85, DoseUnit.Percent));

            Structure IDL90 = CopiedSS.AddStructure("DOSE_REGION", "__IDL90"); 
            IDL90.ConvertDoseLevelToStructure(NewPlan.Dose, new DoseValue(90, DoseUnit.Percent));

            Structure IDL95 = CopiedSS.AddStructure("DOSE_REGION", "__IDL95"); 
            IDL95.ConvertDoseLevelToStructure(NewPlan.Dose, new DoseValue(95, DoseUnit.Percent));

           
            // Spare heart and lung on PTV
            Utils.SpareLungHeart(PTV_OPT, Ipsi_lung, Heart, CopiedSS);

            // Apply margin again
            IDL90.SegmentVolume = IDL90.AsymmetricMargin(margin);
            //await UpdateListBox($"IDL90 add asym margin, vol = {IDL90.Volume} CC");

            // Optimization options
            OptimizationOptionsIMRT opt = new OptimizationOptionsIMRT(1000,
                OptimizationOption.RestartOptimization,
                OptimizationConvergenceOption.TerminateIfConverged,
                OptimizationIntermediateDoseOption.UseIntermediateDose,
                NewPlan.Beams.First().MLC.Id);

            //await UpdateListBox($"optOptions created");

            var unpack_getFluenceEnergyMode = Utils.GetFluenceEnergyMode(Plan.Beams.First());
            string primary_fluence_mode = unpack_getFluenceEnergyMode.Item1;
            string energy_mode_id = unpack_getFluenceEnergyMode.Item2;

            var machineParameters = new ExternalBeamMachineParameters(
                Plan.Beams.First().TreatmentUnit.Id,
                energy_mode_id,
                Plan.Beams.First().DoseRate,
                "STATIC",
                primary_fluence_mode
            );

            // Delete copied beams in new plan
            foreach (var nb in NewPlan.Beams.Where(b => !b.IsSetupField).ToList())
            {
                if (!nb.IsSetupField)
                {
                    NewPlan.RemoveBeam(nb);
                }
            }

            foreach (var bm in Plan.Beams.Where(b => !b.IsSetupField).ToList())
            {
                //  beam is a not a setup field 
                Beam Temp = NewPlan.AddStaticBeam(
                    machineParameters,
                    bm.ControlPoints[0].JawPositions,
                    bm.ControlPoints[0].CollimatorAngle,
                    bm.ControlPoints[0].GantryAngle,
                    bm.ControlPoints[0].PatientSupportAngle,
                    bm.IsocenterPosition
                    );
                Temp.Id = bm.Id;
            }


            // Get optimization setup
            var optSet = NewPlan.OptimizationSetup;
            //await UpdateListBox($"Got old opt setup");

            var RxDose = Plan.TotalDose;

            // Add all objectives
            // -- PTV_OPT --: 
            // - Upper 10 % Volume, 105 % Rx Dose – Priority 120
            optSet.AddPointObjective(PTV_OPT, OptimizationObjectiveOperator.Upper, new DoseValue(1.05 * RxDose.Dose, RxDose.Unit), 10, 120);
            //await UpdateListBox($"Added 1");
            // - Upper 30 % Volume, 103 % Rx Dose – Priority 110
            optSet.AddPointObjective(PTV_OPT, OptimizationObjectiveOperator.Upper, new DoseValue(1.03 * RxDose.Dose, RxDose.Unit), 30, 110);
            //await UpdateListBox($"Added 2");
            // - Lower 95 % Volume, 100 % Rx Dose – Priority 135
            optSet.AddPointObjective(PTV_OPT, OptimizationObjectiveOperator.Lower, new DoseValue(RxDose.Dose, RxDose.Unit), 95, 135);
            //await UpdateListBox($"Added 3");
            // - Lower 99.9 % Volume, 95 % Rx Dose – Priority 130
            optSet.AddPointObjective(PTV_OPT, OptimizationObjectiveOperator.Lower, new DoseValue(0.95 * RxDose.Dose, RxDose.Unit), 99.9, 130);
            //await UpdateListBox($"Added 4");
            // -- Body --
            // - Upper 0 % Volume, 108 % Rx Dose – Priority 200
            optSet.AddPointObjective(body, OptimizationObjectiveOperator.Upper, new DoseValue(((MaxDoseGoal / 100) - 0.01) * RxDose.Dose, RxDose.Unit), 0, 200);
            //await UpdateListBox($"Added 5");
            // -- 90 % IDL structure --
            // - Upper 5 % Volume, 103 % Rx Dose – Priority 140
            optSet.AddPointObjective(IDL90, OptimizationObjectiveOperator.Upper, new DoseValue(1.03 * RxDose.Dose, RxDose.Unit), 5, 140);
            //await UpdateListBox($"Added 6");

            // Optimize
            NewPlan.Optimize(opt);

            
            //await UpdateListBox($"Finished opt");

            // Remove the ptv opt
            //CopiedSS.RemoveStructure(PTV_OPT);
            //CopiedSS.RemoveStructure(IDL90);

            NewPlan.SetCalculationModel(CalculationType.PhotonLeafMotions, Settings.LMCModel);
            NewPlan.CalculateLeafMotions();

            NewPlan.CalculateDose();

            SepDmaxEdgeAfterOpt = Utils.ComputeBeamSeparationWholeField(NewPlan.Beams.First(), NewPlan.Beams.Last(), body, NewPlan.Dose.DoseMax3DLocation.z);

            if (Settings.SecondOpt)
            { 
                // Create hot and cold spotes
                Structure coldSpot = CopiedSS.AddStructure("DOSE_REGION", "__coldSpot");
                coldSpot.ConvertDoseLevelToStructure(NewPlan.Dose, new DoseValue(Settings.ColdSpotIDL, DoseValue.DoseUnit.Percent));
                coldSpot.SegmentVolume = PTV_OPT.Sub(coldSpot.SegmentVolume);

                Structure hotSpot = CopiedSS.AddStructure("DOSE_REGION", "__hotSpot");
                hotSpot.ConvertDoseLevelToStructure(NewPlan.Dose, new DoseValue(Settings.HotSpotIDL, DoseValue.DoseUnit.Percent));

                // Add objectives for hot and cold spot
                //optSet.AddPointObjective(hotSpot, OptimizationObjectiveOperator.Upper, new DoseValue(((MaxDoseGoal/100) - 0.02) * RxDose.Dose, RxDose.Unit), 10, 60);

                var RxDose_ = Plan.TotalDose;
                optSet.AddPointObjective(hotSpot, OptimizationObjectiveOperator.Upper, new DoseValue(1.03 * RxDose_.Dose, RxDose_.Unit), 0, 45);
                optSet.AddPointObjective(coldSpot, OptimizationObjectiveOperator.Lower, new DoseValue(0.98 * RxDose_.Dose, RxDose_.Unit), 100, 20);
            
                NewPlan.Optimize(opt);
         
                NewPlan.SetCalculationModel(CalculationType.PhotonLeafMotions, Settings.LMCModel);
                NewPlan.CalculateLeafMotions();
                NewPlan.CalculateDose();

                if (Settings.Cleanup)
                {
                    var optStructs = CopiedSS.Structures.Where(s => s.Id.StartsWith("__")).ToList();
                    foreach (var os in optStructs) { CopiedSS.RemoveStructure(os); }
                }
            }

            MessageBox.Show($"Plan created with ID {NewPlan.Id}. Please close tool to view.");
        }

        
    }
}
