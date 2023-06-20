﻿using MAAS_BreastPlan_helper.MAAS_BreastPlan_helper;
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
using System.Collections.Specialized;
using System.Collections;
using System.Windows.Controls;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;
using Serilog;
using Serilog.Events;
using System.Net;

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

        private string lmcModel;

        public string LMCModel
        {
            get { return lmcModel; }
            set { SetProperty(ref lmcModel, value); }
        }

        private string lmcVersion;

        public string LMCVersion
        {
            get { return lmcVersion; }
            set { SetProperty(ref lmcVersion, value); }
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

        private string JsonPath { get; set; }  // Path to config file\

        private static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            // Close the log when the program exits
            Log.CloseAndFlush();
        }


        public Auto3dSlidingWindowViewModel(ScriptContext context, SettingsClass settings, string json_path) 
        {
            var json_dir = Path.GetDirectoryName(json_path);
            var log_path = Path.Combine(json_dir, "log.txt");

            // Close the log when the program exits
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;

            // Setup logging
            Log.Logger = new LoggerConfiguration()
                .WriteTo.File(log_path, rollingInterval: RollingInterval.Day) // Log messages to a file
                .MinimumLevel.Debug() // Set the minimum log level (e.g., Debug, Information, Error)
                .CreateLogger();

            SepIso = 0;
            SepIsoEdge = 0;
            //SepDmaxEdgeAfterOpt = 0;

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

            CreatePlanCMD = new DelegateCommand(OnCreateBreastPlanAsync);

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
            
            

            MaxDoseGoal = settings.MaxDoseGoal;
            
            SelectedEnergy = Utils.GetFluenceEnergyMode(Plan.Beams.Where(b => !b.IsSetupField).First()).Item2;

            var LMCSplit = splitLMC(settings.LMCModel);
            LMCModel = LMCSplit.Item1;
            LMCVersion = LMCSplit.Item2;

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
            SepIsoEdge = Utils.ComputeBeamSeparationWholeField(Plan.Beams.First(), Plan.Beams.Last(), body, selectedBreastSide); // field edge iso plane
            
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

        public async void OnCreateBreastPlanAsync()
        {
            // Save some properties back to config
            // LMC
            Settings.HotSpotIDL = MaxDoseGoal;

            Settings.LMCModel = joinLMC(LMCModel, LMCVersion);


            File.WriteAllText(JsonPath, JsonConvert.SerializeObject(Settings));

            if (Settings.Debug)
            {
                await UpdateListBox($"Starting autoplan. Debug = {Settings.Debug}");
                await UpdateListBox("Checking two-field plan parameters");
            }
            Log.Debug($"Starting autoplan. Debug = {Settings.Debug}");
            Log.Debug("Checking two-field plan parameters");
            

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


            if (Settings.Debug) { await UpdateListBox("All checks passed, copying to new plan"); }
            Log.Debug("All checks passed, copying to new plan");

            // Copy plan and set new name
            var NewPlan = Context.Course.CopyPlanSetup(Plan) as ExternalPlanSetup;
            NewPlan.Id = Utils.GetNewPlanName(Context.Course, Plan.Id, 13);

            NewPlan.SetCalculationModel(CalculationType.PhotonVolumeDose, Plan.PhotonCalculationModel);
            NewPlan.SetCalculationModel(CalculationType.PhotonIMRTOptimization, Plan.GetCalculationModel(CalculationType.PhotonIMRTOptimization));

            if (Settings.Debug) { await UpdateListBox($"New plan created with id {NewPlan.Id}"); }
            Log.Debug($"New plan created with id {NewPlan.Id}");

            // Check if there is a PTV
            var CopiedSS = NewPlan.StructureSet;
            var body = CopiedSS.Structures.Where(s => s.Id.ToLower().Contains("body")).First();

            // Perform dose calc
            NewPlan.SetCalculationModel(CalculationType.PhotonVolumeDose, Plan.PhotonCalculationModel);
            NewPlan.CalculateDose();
            NewPlan.SetPrescription(25, new DoseValue(2, DoseUnit.Gy), 1);

            if (Settings.Debug) { await UpdateListBox($"Set dose normalization to global max"); }
            Log.Debug($"Set dose normalization to global max");

            var maxBodyDose = Plan.GetDVHCumulativeData(body, DoseValuePresentation.Relative, VolumePresentation.Relative, 1).MaxDose;
            NewPlan.PlanNormalizationValue = maxBodyDose.Dose;

            //var DM3D = NewPlan.Dose.DoseMax3D;
            if (Settings.Debug) { await UpdateListBox($"Dose calculation finished"); }
            Log.Debug("Dose calculation finished");

            // Delete existing opt structures
            var optStructsOld = CopiedSS.Structures.Where(s => s.Id.StartsWith("__")).ToList();
            foreach (var os in optStructsOld) { CopiedSS.RemoveStructure(os); }

            Structure PTV_OPT = CopiedSS.AddStructure("DOSE_REGION", "__PTV_OPT");
            var margin = new AxisAlignedMargins(StructureMarginGeometry.Inner, 5, 5, 5, 5, 5, 5);

            if (!CustomPTV)
            {
                // Create 50% IDL structure as PTV_OPT if PTV not selected 
                PTV_OPT.ConvertDoseLevelToStructure(NewPlan.Dose, new DoseValue(50, DoseUnit.Percent));
                //await UpdateListBox($"Create PTV_OPT from 50IDL with volume: {PTV_OPT.Volume:F2} CC");
                PTV_OPT.SegmentVolume = PTV_OPT.AsymmetricMargin(margin);
                if (Settings.Debug) { await UpdateListBox($"Create PTV_OPT from 50IDL with volume: {PTV_OPT.Volume:F2} CC"); }
                Log.Debug($"Create PTV_OPT from 50IDL with volume: {PTV_OPT.Volume:F2} CC");
            }

            else
            {
                
                PTV_OPT.SegmentVolume = SelectedPTV.SegmentVolume;
                if (Settings.Debug) { await UpdateListBox($"Using custom PTV: {selectedPTV.Id} with volume: {selectedPTV.Volume:F2} CC"); }
                Log.Debug($"Using custom PTV: {selectedPTV.Id} with volume: {selectedPTV.Volume:F2} CC");
            }


            if (PTV_OPT.Volume < 0.0001)
            {
                throw new Exception($"Structure volume of PTV opt is too low: {PTV_OPT.Volume:F2} CC");
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
            if (Settings.Debug) { await UpdateListBox($"IDL90 add asym margin, vol = {IDL90.Volume:F2} CC"); }
            Log.Debug($"IDL90 add asym margin, vol = {IDL90.Volume:F2} CC");

            // Optimization options
            OptimizationOptionsIMRT opt = new OptimizationOptionsIMRT(1000,
                OptimizationOption.RestartOptimization,
                OptimizationConvergenceOption.TerminateIfConverged,
                OptimizationIntermediateDoseOption.UseIntermediateDose,
                NewPlan.Beams.First().MLC.Id);


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
            var RxDose = Plan.TotalDose;

            // Add all objectives
            // -- PTV_OPT --: 
            

            if (Settings.Debug) { await UpdateListBox("Creating upper 10 % Volume, 105 % Rx Dose – Priority 120"); }
            optSet.AddPointObjective(PTV_OPT, OptimizationObjectiveOperator.Upper, new DoseValue(1.05 * RxDose.Dose, RxDose.Unit), 10, 120);

            // - Upper 30 % Volume, 103 % Rx Dose – Priority 110
            if (Settings.Debug) { await UpdateListBox("Creating upper 30 % Volume, 103 % Rx Dose – Priority 110"); }
            optSet.AddPointObjective(PTV_OPT, OptimizationObjectiveOperator.Upper, new DoseValue(1.03 * RxDose.Dose, RxDose.Unit), 30, 110);
            ////await UpdateListBox($"Added 2");
            // - Lower 95 % Volume, 100 % Rx Dose – Priority 135
            if (Settings.Debug) { await UpdateListBox("Creating lower 95 % Volume, 100 % Rx Dose – Priority 135"); }
            optSet.AddPointObjective(PTV_OPT, OptimizationObjectiveOperator.Lower, new DoseValue(RxDose.Dose, RxDose.Unit), 95, 135);
            ////await UpdateListBox($"Added 3");
            // - Lower 99.9 % Volume, 95 % Rx Dose – Priority 130
            if (Settings.Debug) { await UpdateListBox("Creating lower 99.9 % Volume, 95 % Rx Dose – Priority 130"); }
            optSet.AddPointObjective(PTV_OPT, OptimizationObjectiveOperator.Lower, new DoseValue(0.95 * RxDose.Dose, RxDose.Unit), 99.9, 130);
            ////await UpdateListBox($"Added 4");
            // -- Body --
            // - Upper 0 % Volume, 108 % Rx Dose – Priority 200
            if (Settings.Debug) { await UpdateListBox("Creating upper 0 % Volume, 108 % Rx Dose – Priority 200"); }
            optSet.AddPointObjective(body, OptimizationObjectiveOperator.Upper, new DoseValue(((MaxDoseGoal / 100) - 0.01) * RxDose.Dose, RxDose.Unit), 0, 200);
            ////await UpdateListBox($"Added 5");
            // -- 90 % IDL structure --
            // - Upper 5 % Volume, 103 % Rx Dose – Priority 140
            if (Settings.Debug) { await UpdateListBox("Creating upper 5 % Volume, 103 % Rx Dose – Priority 140"); }
            optSet.AddPointObjective(IDL90, OptimizationObjectiveOperator.Upper, new DoseValue(1.03 * RxDose.Dose, RxDose.Unit), 5, 140);
            ////await UpdateListBox($"Added 6");
            ////await UpdateListBox("Created objectives. Starting optimization...");

            // Optimize
            if (Settings.Debug) { await UpdateListBox($"Starting initial pass"); }
            Log.Debug("Starting initial pass");
            NewPlan.Optimize(opt);
            if (Settings.Debug) { await UpdateListBox($"Finished initial pass"); }
            Log.Debug("Finished initial pass");

            // Dose level check
            DoseValue HotSpotIDL = new DoseValue(Settings.HotSpotIDL, DoseValue.DoseUnit.Percent);
            if (HotSpotIDL > NewPlan.Dose.DoseMax3D)
            {
                var msg = $"Warning: HotspotIDL from config {Settings.HotSpotIDL} is greater than 3D dose max: {NewPlan.Dose.DoseMax3D.Dose}";
                if (Settings.Debug) { await UpdateListBox(msg); }
                Log.Debug(msg);

                HotSpotIDL = NewPlan.Dose.DoseMax3D * 0.95;
            }


            NewPlan.SetCalculationModel(CalculationType.PhotonLeafMotions, Settings.LMCModel);
            NewPlan.CalculateLeafMotions();

            NewPlan.CalculateDose();

            SepDmaxEdgeAfterOpt = Utils.ComputeBeamSeparationWholeField(NewPlan.Beams.First(), NewPlan.Beams.Last(), body, selectedBreastSide, NewPlan.Dose.DoseMax3DLocation.z);


            if (Settings.SecondOpt)
            {
                if (Settings.Debug) { await UpdateListBox("Starting second pass"); }
                Log.Debug("Starging second pass");
                // Create hot and cold spotes
                Structure coldSpot = CopiedSS.AddStructure("DOSE_REGION", "__coldSpot");
                coldSpot.ConvertDoseLevelToStructure(NewPlan.Dose, new DoseValue(Settings.ColdSpotIDL, DoseValue.DoseUnit.Percent));
                coldSpot.SegmentVolume = PTV_OPT.Sub(coldSpot.SegmentVolume);

                Structure hotSpot = CopiedSS.AddStructure("DOSE_REGION", "__hotSpot");
                hotSpot.ConvertDoseLevelToStructure(NewPlan.Dose, HotSpotIDL);

                // Add objectives for hot and cold spot
                //optSet.AddPointObjective(hotSpot, OptimizationObjectiveOperator.Upper, new DoseValue(((MaxDoseGoal/100) - 0.02) * RxDose.Dose, RxDose.Unit), 10, 60);

                var RxDose_ = Plan.TotalDose;
                optSet.AddPointObjective(hotSpot, OptimizationObjectiveOperator.Upper, new DoseValue(1.03 * RxDose_.Dose, RxDose_.Unit), 0, 45);
                optSet.AddPointObjective(coldSpot, OptimizationObjectiveOperator.Lower, new DoseValue(0.98 * RxDose_.Dose, RxDose_.Unit), 100, 20);
            
                NewPlan.Optimize(opt);
         
                NewPlan.SetCalculationModel(CalculationType.PhotonLeafMotions, Settings.LMCModel);
                NewPlan.CalculateLeafMotions();
                NewPlan.CalculateDose();

                if (Settings.Debug) { await UpdateListBox("Finished second pass"); }
                Log.Debug("Finished second pass");

            }

            if (Settings.Cleanup)
            {
                var optStructs = CopiedSS.Structures.Where(s => s.Id.StartsWith("__")).ToList();
                foreach (var os in optStructs) { CopiedSS.RemoveStructure(os); }
            }

            if (Settings.Debug) { await UpdateListBox("Complete. Close window to view plan."); }
            Log.Debug("Complete. Close window to view plan");

            MessageBox.Show($"Plan created with ID {NewPlan.Id}. Please close tool to view.");
        }

        private Tuple<string, string> splitLMC(string lMCModel)
        {
        
            Regex regex = new Regex(@"^([^\[]+)(\s\[(\d+\.\d+\.\d+)\])?$");

            string model;
            string version;
            Match match = regex.Match(lMCModel);
            if (match.Success)
            {
                model = match.Groups[1].Value.Trim();
                version = match.Groups[3].Value;
                
            }
            else
            {
                throw new Exception($"Could not find model and version from {lMCModel}");
            }

            return new Tuple<string, string> (model, version);
        }

        public string joinLMC(string model, string version)
        {
            return $"{model} [{version}]";
        }

        private async Task UpdateListBox(string s)
        {
            StatusBoxItems.Add(s);
            //StatusBox.ScrollIntoView(s);
            await Task.Delay(500);
        }


    }
}
