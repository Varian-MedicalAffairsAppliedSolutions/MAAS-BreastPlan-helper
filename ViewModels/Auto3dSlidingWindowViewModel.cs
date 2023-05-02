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

namespace MAAS_BreastPlan_helper.ViewModels
{
    public class Auto3dSlidingWindowViewModel : BindableBase
    {
        private ScriptContext Context { get; set; }
        private SettingsClass Settings { get; set; }
        private Patient Patient { get; set; }   
        private ExternalPlanSetup Plan { get; set; }

        public ObservableCollection<string> StatusBoxItems { get; set; }

        public DelegateCommand CreatePlanCMD { get; set; }

        // -- Lung stuff --
        private Structure ipsi_lung;

        public Structure Ipsi_lung
        {
            get { return ipsi_lung; }
            set { SetProperty(ref ipsi_lung, value); }
        }

        public ObservableCollection<Structure> LungStructures { get; set; }


        // -- Heart stuff --
        private Structure heart;

        public Structure Heart
        {
            get { return heart; }
            set { SetProperty(ref heart, value); }
        }

        public ObservableCollection<Structure> HeartStructures { get; set; }


        /// <summary>
        /// Determine breast side based on isocenter coordinates
        /// </summary>
        /// <param name="plan">The treatment plan.</param>
        /// <returns>The element of the SIDE enum representing the treatment side.</returns>

        public Auto3dSlidingWindowViewModel(ScriptContext context, SettingsClass settings) 
        {
            Context = context;
            Settings = settings;

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

            var lLungStructures = Plan.StructureSet.Structures.Where(s => s.Id.ToLower().Contains("lung")).ToList();
            LungStructures = new ObservableCollection<Structure>();
            foreach (var strucuture in lLungStructures) { LungStructures.Add(strucuture); }
            
      
            //SIDE Side = FindTreatmentSide(Plan);
            

            // Pick:
            // 1. Breast side,
            // 2. Ipsilateral lung,
            // 3. Select heart
            
        }

        public async void OnCreateBreastPlan()
        {
            List<Beam> Beams = Plan.Beams.Where(b => !b.IsSetupField).ToList();
            int nrBeams = Beams.Count();
            await UpdateListBox("Starting...");
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

            await UpdateListBox("All checks passed, copying to new plan");

            // Copy plan and set new name
            var NewPlan = Context.Course.CopyPlanSetup(Plan) as ExternalPlanSetup;
            NewPlan.Id = Utils.GetNewPlanName(Context.Course, Plan.Id, 13);

            NewPlan.SetCalculationModel(CalculationType.PhotonVolumeDose, Plan.PhotonCalculationModel);
            //await UpdateListBox($"Set vol dose model");
            NewPlan.SetCalculationModel(CalculationType.PhotonIMRTOptimization, Plan.GetCalculationModel(CalculationType.PhotonIMRTOptimization));
            //await UpdateListBox($"Set opt model");

            await UpdateListBox($"New plan created with id {NewPlan.Id}");

            // Check if there is a PTV
            var CopiedSS = NewPlan.StructureSet;
            //var PTV = CopiedSS.Structures.Where(s => s.Id.ToLower().Contains("ptv")).First();
            var body = CopiedSS.Structures.Where(s => s.Id.ToLower().Contains("body")).First();
            //var bExistsPTV = PTV_OPT != null;

            //await UpdateListBox($"Tried to find PTV and Body");

            

            //await UpdateListBox($"Sparing heart and lung {NewPlan.Id}");

            //DoseValue renormPTV = Plan.StructureSet.Structures.Where(s => s.Id.ToLower().Contains("body")).First();
            
            
            var maxBodyDose = Plan.GetDVHCumulativeData(body, DoseValuePresentation.Relative, VolumePresentation.Relative, 1).MaxDose;
            NewPlan.PlanNormalizationValue = maxBodyDose.Dose;

            //await UpdateListBox($"Set norm value");

            // Perform dose calc
            if (NewPlan.Dose == null)
            {
                NewPlan.CalculateDose();
            }
            //await UpdateListBox($"Dose is calculated");

            // Create 50% IDL structure as PTV_OPT if PTV not selected

            Structure PTV_OPT = CopiedSS.AddStructure("DOSE_REGION", "PTV_OPT");
            await UpdateListBox($"PTV_OPT created");
            PTV_OPT.ConvertDoseLevelToStructure(NewPlan.Dose, new DoseValue(50, DoseValue.DoseUnit.Percent));
            await UpdateListBox($"PTV_OPT converted with vol = {PTV_OPT.Volume}");

            // Apply margin 
            var margin = new AxisAlignedMargins(StructureMarginGeometry.Inner, 5, 5, 5, 5, 5, 5);
            await UpdateListBox($"created margin");
            PTV_OPT.SegmentVolume = PTV_OPT.AsymmetricMargin(margin);
            await UpdateListBox($"changed ptv seg vol");

            // Spare heart and lung on PTV
            Utils.SpareLungHeart(PTV_OPT, ipsi_lung, Heart, CopiedSS);

            await UpdateListBox($"PTV_OPT Vol = {PTV_OPT.Volume}");

            // Create 90% IDL structure
            Structure IDL90= CopiedSS.AddStructure("DOSE_REGION", "IDL90");
            await UpdateListBox($"IDL90 created");
            IDL90.ConvertDoseLevelToStructure(NewPlan.Dose, new DoseValue(90, DoseValue.DoseUnit.Percent));
            await UpdateListBox($"IDL90 seg vol set");

            // Apply margin again
            IDL90.SegmentVolume = IDL90.AsymmetricMargin(margin);
            await UpdateListBox($"IDL90 add asym margin");

            // Optimization options
            OptimizationOptionsIMRT opt = new OptimizationOptionsIMRT(1000,
                OptimizationOption.RestartOptimization,
                OptimizationConvergenceOption.TerminateIfConverged,
                OptimizationIntermediateDoseOption.UseIntermediateDose,
                NewPlan.Beams.First().MLC.Id);

            await UpdateListBox($"optOptions created");

            

            // Get optimization setup
            var optSet = NewPlan.OptimizationSetup;
            await UpdateListBox($"Got old opt setup");

            var RxDose = Plan.TotalDose;

            // Add all objectives
            // -- PTV_OPT --: 
            // - Upper 10 % Volume, 105 % Rx Dose – Priority 120
            optSet.AddPointObjective(PTV_OPT, OptimizationObjectiveOperator.Upper, new DoseValue(1.05 * RxDose.Dose, RxDose.Unit), 10, 120);
            await UpdateListBox($"Added 1");
            // - Upper 30 % Volume, 103 % Rx Dose – Priority 110
            optSet.AddPointObjective(PTV_OPT, OptimizationObjectiveOperator.Upper, new DoseValue(1.03 * RxDose.Dose, RxDose.Unit), 30, 110);
            await UpdateListBox($"Added 2");
            // - Lower 95 % Volume, 100 % Rx Dose – Priority 135
            optSet.AddPointObjective(PTV_OPT, OptimizationObjectiveOperator.Lower, new DoseValue(RxDose.Dose, RxDose.Unit), 95, 135);
            await UpdateListBox($"Added 3");
            // - Lower 99.9 % Volume, 95 % Rx Dose – Priority 130
            optSet.AddPointObjective(PTV_OPT, OptimizationObjectiveOperator.Lower, new DoseValue(0.95 * RxDose.Dose, RxDose.Unit), 99.9, 130);
            await UpdateListBox($"Added 4");
            // -- Body --
            // - Upper 0 % Volume, 108 % Rx Dose – Priority 200
            optSet.AddPointObjective(body, OptimizationObjectiveOperator.Upper, new DoseValue(1.08 * RxDose.Dose, RxDose.Unit), 0, 200);
            await UpdateListBox($"Added 5");
            // -- 90 % IDL structure --
            // - Upper 5 % Volume, 103 % Rx Dose – Priority 140
            optSet.AddPointObjective(IDL90, OptimizationObjectiveOperator.Upper, new DoseValue(1.03 * RxDose.Dose, RxDose.Unit), 5, 140);
            await UpdateListBox($"Added 6");

            // Optimize
            NewPlan.Optimize(opt);
            await UpdateListBox($"Finished opt");



        }

        private async Task UpdateListBox(string s)
        {
            StatusBoxItems.Add(s);
            //statusBox.ScrollIntoView(s);
            await Task.Delay(1);
        }
    }
}
