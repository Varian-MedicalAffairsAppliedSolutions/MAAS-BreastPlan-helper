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

namespace MAAS_BreastPlan_helper.ViewModels
{
    public class Auto3dSlidingWindowViewModel : BindableBase
    {
        private ScriptContext Context { get; set; }
        private SettingsClass Settings { get; set; }
        private Patient Patient { get; set; }   
        private ExternalPlanSetup Plan { get; set; }

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

            if (Patient == null)
            {
                throw new Exception("Patient is null");
            }

            Plan = Context.PlanSetup as ExternalPlanSetup;

            if (Plan == null) { 
                throw new Exception("Plan is null");
            }
      
            //SIDE Side = FindTreatmentSide(Plan);
            

            // Pick:
            // 1. Breast side,
            // 2. Ipsilateral lung,
            // 3. Select heart
            
        }

        public void OnCreateBreastPlan()
        {
            List<Beam> Beams = Plan.Beams.Where(b => !b.IsSetupField).ToList();
            int nrBeams = Beams.Count();
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

            // Copy plan and set new name
            var NewPlan = Context.Course.CopyPlanSetup(Plan) as ExternalPlanSetup;
            NewPlan.Id = Utils.GetNewPlanName(Context.Course, Plan.Id, 13);

            // Check if there is a PTV
            var CopiedSS = NewPlan.StructureSet;
            var PTV = CopiedSS.Structures.Where(s => s.Id.ToLower().Contains("ptv")).First();
            var body = CopiedSS.Structures.Where(s => s.Id.ToLower().Contains("body")).First();
            var bExistsPTV = PTV != null;

            // Spare heart and lung on PTV
            Utils.SpareLungHeart(PTV, ipsi_lung, Heart, CopiedSS);

            //DoseValue renormPTV = Plan.StructureSet.Structures.Where(s => s.Id.ToLower().Contains("body")).First();
            var maxBodyDose = Plan.GetDVHCumulativeData(body, DoseValuePresentation.Relative, VolumePresentation.Relative, 1).MaxDose;
            NewPlan.PlanNormalizationValue = maxBodyDose.Dose;

            // Perform dose calc
            if (NewPlan.Dose == null)
            {
                NewPlan.CalculateDose();
            }

            // Create 50% IDL structure
            Structure IDL50 = CopiedSS.AddStructure("DOSE_REGION", "IDL50");
            IDL50.ConvertDoseLevelToStructure(NewPlan.Dose, new DoseValue(50, DoseValue.DoseUnit.Percent));

            // Apply margin
            var margin = new AxisAlignedMargins(StructureMarginGeometry.Inner, 5, 5, 5, 5, 5, 5);
            PTV.SegmentVolume = IDL50.AsymmetricMargin(margin);

            // Create 90% IDL structure
            Structure IDL90= CopiedSS.AddStructure("DOSE_REGION", "IDL90");
            IDL90.ConvertDoseLevelToStructure(NewPlan.Dose, new DoseValue(90, DoseValue.DoseUnit.Percent));

            // Apply margin again
            IDL90.SegmentVolume = IDL90.AsymmetricMargin(margin);

            // Optimization options
            OptimizationOptionsIMRT opt = new OptimizationOptionsIMRT(1000,
                OptimizationOption.RestartOptimization,
                OptimizationConvergenceOption.TerminateIfConverged,
                OptimizationIntermediateDoseOption.UseIntermediateDose,
                NewPlan.Beams.First().MLC.Id);

            NewPlan.SetCalculationModel(CalculationType.PhotonVolumeDose, Plan.PhotonCalculationModel);
            NewPlan.SetCalculationModel(CalculationType.PhotonIMRTOptimization, Plan.GetCalculationModel(CalculationType.PhotonIMRTOptimization));

            // Get optimization setup
            var optSet = NewPlan.OptimizationSetup;

            // Add all objectives
            // -- PTV_OPT --: 
            // - Upper 10 % Volume, 105 % Rx Dose – Priority 120
            optSet.AddPointObjective(PTV, OptimizationObjectiveOperator.Upper, new DoseValue(105, DoseValue.DoseUnit.Percent), 0.1 * PTV.Volume, 120);

            // - Upper 30 % Volume, 103 % Rx Dose – Priority 110
            optSet.AddPointObjective(PTV, OptimizationObjectiveOperator.Upper, new DoseValue(103, DoseValue.DoseUnit.Percent), 0.3 * PTV.Volume, 110);

            // - Lower 95 % Volume, 100 % Rx Dose – Priority 135
            optSet.AddPointObjective(PTV, OptimizationObjectiveOperator.Lower, new DoseValue(100, DoseValue.DoseUnit.Percent), 0.95 * PTV.Volume, 135);

            // - Lower 99.9 % Volume, 95 % Rx Dose – Priority 130
            optSet.AddPointObjective(PTV, OptimizationObjectiveOperator.Lower, new DoseValue(95, DoseValue.DoseUnit.Percent), 0.999 * PTV.Volume, 130);

            // -- Body --
            // - Upper 0 % Volume, 108 % Rx Dose – Priority 200
            optSet.AddPointObjective(body, OptimizationObjectiveOperator.Upper, new DoseValue(108, DoseValue.DoseUnit.Percent), 0, 200);

            // -- 90 % IDL structure --
            // - Upper 5 % Volume, 103 % Rx Dose – Priority 140
            optSet.AddPointObjective(IDL90, OptimizationObjectiveOperator.Upper, new DoseValue(103, DoseValue.DoseUnit.Percent), 0.05 * IDL90.Volume, 140);


            // Optimize
            NewPlan.Optimize(opt);


        }
    }
}
