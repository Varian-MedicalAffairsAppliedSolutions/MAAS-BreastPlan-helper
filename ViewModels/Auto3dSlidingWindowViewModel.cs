using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using VMS.TPS.Common.Model.API;
using MAAS_BreastPlan_helper.Models;
using MAAS_BreastPlan_helper.Services;
using VMS.TPS.Common.Model.Types;
using System.Collections.ObjectModel;
using Prism.Commands;
using static VMS.TPS.Common.Model.Types.DoseValue;
using static MAAS_BreastPlan_helper.Models.Utils;
using Newtonsoft.Json;
using System.IO;
using System.Text.RegularExpressions;
using Serilog;


/*
 * Pass on of Ryan's requests complete
 * Still to do:
 * (maybe in 16.1) Add check for valid leaf motion calculator name
 * 1. (DIFFICULT, DO LATER) Post processing of IDL structures: Keep largest part (2D-All)
- use getImageContour on each plane, retain largest delete others - dont delete if it's a hole
- returns an array for each contour
 * */

/*
Priority 1.
1. DONE - Rx of final copied plan defaults to 2Gy X 25 (Final plan Rx needs to match starting plan Rx) 
2. DONE - Automatically calculate dose if not present (instead of just throwing exception)
3. DONE - gives message warning about bolus (? bolus seems to be copied) Bolus is currently not used if attached to the plan.
   Is it possible to use bolus in the copied plan and during optimization? -- halcyon plan level, TB field level

 
Priority 2.
1. DONE - prechecks automatically with extended exception message as a popup.
   Create separate button to perform existing checks before starting Auto-Plan. The idea would be to allow the user to see a 
   comprehensive list of everything that needs to be adjusted in Eclipse rather 
   than opening and closing the script multiple times to find one new issue with each crash.
2. TEST - Add check if new generated plan name will exceed max characters
3. TEST - Add check if MLC is missing
5. TEST - Add check if generated structures already exist. Provide option if they do exist to skip structure generation step and use existing structures for optimization

Priority 4.
1. TEST - To add to this: another bug that has been causing issues is when existing optimization objectives
are in the starting plan (before it is copied). Is it  possible to clear these objectives each time
the autoplan runs? (before copying the plan and propagating the new objectives).

// TODO 7.17
1. Fix Jaws: Syntax - public bool FixedJaws { get; } 

2. Set Fluence Smoothing factors: 80 (X) and 60 (Y): Syntax - public double SmoothX { get; } & public double SmoothY { get; }

3. Turn off NTO or set priority to zero (whichever is easiest): Syntax - public OptimizationNormalTissueParameter AddNormalTissueObjective(
    double priority,
    double distanceFromTargetBorderInMM,
    double startDosePercentage,
    double endDosePercentage,
    double fallOff
)

has context menu
*/

namespace MAAS_BreastPlan_helper.ViewModels
{

    public class Auto3dSlidingWindowViewModel : BindableBase
    {
        #region class members
        private readonly EsapiWorker _esapiWorker;
        private readonly SettingsClass _settings;
        private Patient Patient { get; set; }
        
        private ExternalPlanSetup _plan;
        public ExternalPlanSetup Plan
        {
            get { return _plan; }
            set { SetProperty(ref _plan, value); }
        }

        private double _sepIso;
        public double SepIso
        {
            get { return _sepIso; }
            set { SetProperty(ref _sepIso, value); }
        }

        private double _sepIsoEdge;
        public double SepIsoEdge
        {
            get { return _sepIsoEdge; }
            set { SetProperty(ref _sepIsoEdge, value); }
        }

        private double _sepDmaxEdgeAfterOpt;
        public double SepDmaxEdgeAfterOpt
        {
            get { return _sepDmaxEdgeAfterOpt; }
            set { SetProperty(ref _sepDmaxEdgeAfterOpt, value); }
        }

        private bool _customPTV;
        public bool CustomPTV
        {
            get { return _customPTV; }
            set { SetProperty(ref _customPTV, value); }
        }

        private bool _cboPTVEnabled;
        public bool CBOPTVEnabled
        {
            get { return _cboPTVEnabled; }
            set { SetProperty(ref _cboPTVEnabled, value); }
        }

        private bool _lblPTVEnabled;
        public bool LBLPTVEnabled
        {
            get { return _lblPTVEnabled; }
            set { SetProperty(ref _lblPTVEnabled, value); }
        }

        private string _selectedEnergy;
        public string SelectedEnergy
        {
            get { return _selectedEnergy; }
            set { SetProperty(ref _selectedEnergy, value); }
        }


        public ObservableCollection<SIDE> BreastSides { get; set; }
        private SIDE _selectedBreastSide;
        public SIDE SelectedBreastSide
        {
            get { return _selectedBreastSide; }
            set { SetProperty(ref _selectedBreastSide, value); }
        }

        private string _maxDoseGoal;
        public string MaxDoseGoal
        {
            get { return _maxDoseGoal; }
            set { SetProperty(ref _maxDoseGoal, value); }
        }


        public ObservableCollection<string> StatusBoxItems { get; set; }

        public DelegateCommand CreatePlanCMD { get; set; }

        // -- Lung stuff --
        private Structure _ipsiLung;
        public Structure Ipsi_lung
        {
            get { return _ipsiLung; }
            set { SetProperty(ref _ipsiLung, value); }
        }

        public ObservableCollection<Structure> PTVItems { get; set; }
        private Structure _selectedPTV;
        public Structure SelectedPTV
        {
            get { return _selectedPTV; }
            set { SetProperty(ref _selectedPTV, value); }
        }


        public ObservableCollection<Structure> LungStructures { get; set; }

        private string _lmcModel;
        public string LMCModel
        {
            get { return _lmcModel; }
            set { SetProperty(ref _lmcModel, value); }
        }

        private string _lmcVersion;
        public string LMCVersion
        {
            get { return _lmcVersion; }
            set { SetProperty(ref _lmcVersion, value); }
        }

        public DelegateCommand CbCustomPTV_Click { get; set; }


        // -- Heart stuff --
        private Structure _heart;
        public Structure Heart
        {
            get { return _heart; }
            set { SetProperty(ref _heart, value); }
        }

        public ObservableCollection<Structure> HeartStructures { get; set; }

        private string JsonPath { get; set; }  // Path to config file\
        #endregion class members
        private static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            // Close the log when the program exits
            Log.CloseAndFlush();
        }
        private void Precheck()
        {

            Patient = _esapiWorker.GetValue(context => context.Patient);
            Patient.BeginModifications();
            // Runs before optimization to ensure the setup is correct and warn if not
            var bPrecheckPass = true;
            var bShowWarn = false;
            var message = "";
            var warn_msg = "";

            if (Patient == null)
            {
                bPrecheckPass = false;
                message += "Error: Patient is null\n";
            }

            Plan = _esapiWorker.GetValue(context => context.PlanSetup as ExternalPlanSetup);
            if (Plan == null)
            {
                bPrecheckPass = false;
                message += "Error: Plan is null\n";
            }

            Log.Debug($"Starting autoplan. Debug = {_settings.Debug}");
            Log.Debug("Checking two-field plan parameters");

            List<Beam> Beams = Plan.Beams.Where(b => !b.IsSetupField).ToList();
            int nrBeams = Beams.Count();
            //await UpdateListBox("Starting...");
            if (nrBeams != 2)
            {
                bPrecheckPass = false;
                message += $"Error: Must have 2 beams but got {nrBeams}\n";
            }

            foreach (var bm in Beams)
            {
                // Check if the inital plan has boluses
                if (bm.Boluses.Count() > 0)
                {
                    bShowWarn = true;
                    warn_msg += $"Warning: The initial plan contains {bm.Boluses.Count()} boluses on beam {bm.Id}.\n These can\n't be copied to the new plan for optimization.";
                }

                //if (bm.MLC == null)
                //{
                //    bPrecheckPass = false;
                //     message += $"Error: MLC for beam {bm.Id} is null\n";
                //}
            }



            // TODO: find better gantry diff function
            // Check the beams are > 160 deg apart
            var ganDiff = Math.Abs(Beams.First().ControlPoints.First().GantryAngle - Beams.Last().ControlPoints.First().GantryAngle);
            if (ganDiff < 160)
            {
                bPrecheckPass = false;
                message += "Error: Gantry angle difference is greater than 160 degrees\n";
            }

            var bAreSameEnergy = Beams.First().EnergyModeDisplayName == Beams.Last().EnergyModeDisplayName;
            if (!bAreSameEnergy)
            {
                bPrecheckPass = false;
                message += "Error: Beams are not same energy\n";
            }

            // Check that the field weights sum to 1
            //var weightSum = Beams.Select(b => b.WeightFactor).Sum();
            //if (weightSum != 1)
            //{
            //    bPrecheckPass= false;
            //    message += $"Error: Beam weights don't sum to 1: {weightSum}\n";
            //}

            if (bShowWarn)
            {
                MessageBox.Show(warn_msg);
            }

            // Finally show Error message
            if (!bPrecheckPass)
            {
                throw new Exception($"The following prechecks did not pass, please resolve and try again:\n{message}");
            }

            // Check dose last since that is dependant on other steps
            var bHasDose = Plan.Dose != null;
            if (!bHasDose)
            {
                var msg = "No dose on initial plan. Calculating, please wait ...";
                Log.Debug(msg);

                var result = Plan.CalculateDose();

                if (!result.Success)
                {
                    bPrecheckPass = false;
                    message += $"Calculation failed: {result}\n";
                }

                Log.Debug("Dose calc on initial plan successful");
            }

            //if (_settings.Debug) { await UpdateListBox("All checks passed, copying to new plan"); }
            Log.Debug("All checks passed, copying to new plan");
        }
        public Auto3dSlidingWindowViewModel(EsapiWorker esapiWorker, SettingsClass settings, string json_path)
        {
            _esapiWorker = esapiWorker;
            _settings = settings;
            JsonPath = json_path;
            
            // Initialize Plan from EsapiWorker
            Plan = _esapiWorker.GetValue(sc => sc.ExternalPlanSetup);
            
            Precheck();

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

            CreatePlanCMD = new DelegateCommand(OnCreateBreastPlanAsync);

            StatusBoxItems = new ObservableCollection<string>();

            // Initialize observable collections using EsapiWorker to ensure fresh structure references
            _esapiWorker.RunWithWait(sc =>
            {
                var ss = sc.StructureSet;
                if (ss == null) { throw new Exception("Structure set is null"); }
                var structs = ss.Structures;
                if (structs == null) { throw new Exception("Structures are null"); }
                var lHeartStructures = structs.Where(s => s.Id.ToLower().Contains("heart")).ToList();
                if (lHeartStructures.Count == 0) { throw new Exception("Heart structures are empty"); }

                HeartStructures = new ObservableCollection<Structure>();
                foreach (var structure in lHeartStructures) { HeartStructures.Add(structure); }
                Heart = HeartStructures.FirstOrDefault();

                BreastSides = new ObservableCollection<SIDE>() { SIDE.RIGHT, SIDE.LEFT };
                SelectedBreastSide = FindTreatmentSide(sc.ExternalPlanSetup);

                var lLungStructures = ss.Structures.Where(s => s.Id.ToLower().Contains("lung")).ToList();
                LungStructures = new ObservableCollection<Structure>();
                foreach (var structure in lLungStructures) { LungStructures.Add(structure); }

                // Select lung structure based on the position of the center point
                if (SelectedBreastSide == SIDE.LEFT)
                {
                    Ipsi_lung = LungStructures.Where(s => s.CenterPoint.x > 0).FirstOrDefault();
                }
                else
                {
                    Ipsi_lung = LungStructures.Where(s => s.CenterPoint.x <= 0).FirstOrDefault();
                }

                MaxDoseGoal = _settings.MaxDoseGoal.ToString();

                SelectedEnergy = Utils.GetFluenceEnergyMode(sc.ExternalPlanSetup.Beams.Where(b => !b.IsSetupField).First()).Item2;

                var LMCSplit = splitLMC(_settings.LMCModel);
                LMCModel = LMCSplit.Item1;
                LMCVersion = LMCSplit.Item2;

                PTVItems = new ObservableCollection<Structure>();
                foreach (var s in ss.Structures.Where(s => s.Id.ToLower().Contains("ptv")))
                {
                    PTVItems.Add(s);
                }

                // Initialize Custom ptv checkbox to false
                CustomPTV = false;
                LBLPTVEnabled = false;
                CBOPTVEnabled = false;

                var body = ss.Structures.Where(s => s.Id.ToLower().Contains("body")).First();

                SepIso = Utils.ComputeBeamSeparation(sc.ExternalPlanSetup.Beams.First(), sc.ExternalPlanSetup.Beams.Last(), body); // center of field iso plane
                SepIsoEdge = Utils.ComputeBeamSeparationWholeField(sc.ExternalPlanSetup.Beams.First(), sc.ExternalPlanSetup.Beams.Last(), body, SelectedBreastSide); // field edge iso plane

                // Initialize max dose goal
                MaxDoseGoal = "107";
            });

            CbCustomPTV_Click = new DelegateCommand(OnCustomPTV_Click);
            
            // Add initial status message
            AddStatusMessage("Auto 3D Sliding Window ready.");
            AddStatusMessage("Please verify settings before proceeding.");

            // Pick:
            // 1. Breast side,
            // 2. Ipsilateral lung,
            // 3. Select heart
        }

        private Structure AddStructIfNotExists(string name, StructureSet ss, ExternalPlanSetup plan, DoseValue dv, bool warnOnExist)
        {
            var existing = ss.Structures.Where(st => st.Id == name).ToList();

            if (existing.Count() == 0)
            {
                Structure s = ss.AddStructure("DOSE_REGION", name);
                s.ConvertDoseLevelToStructure(plan.Dose, dv);
                return s;
            }

            if (warnOnExist)
            {
                MessageBox.Show($"Warning: optimization structure {name} already exists.");
            }

            return existing.FirstOrDefault();
        }

        private void OnCustomPTV_Click()
        {
            if (CustomPTV)
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
            await _esapiWorker.ExecuteWithErrorHandlingAsync(async sc =>
            {
                // Save some properties back to config
                _settings.LMCModel = joinLMC(LMCModel, LMCVersion);
                File.WriteAllText(JsonPath, JsonConvert.SerializeObject(_settings));

                // Create a completely isolated copy of the plan to work with
                // This ensures we don't interfere with the original plan context used by other tabs
                var originalPlan = sc.ExternalPlanSetup;
                var NewPlan = sc.Course.CopyPlanSetup(originalPlan) as ExternalPlanSetup;
                NewPlan.Id = Utils.GetNewPlanName(sc.Course, originalPlan.Id, 13);

            NewPlan.SetCalculationModel(CalculationType.PhotonVolumeDose, originalPlan.PhotonCalculationModel);
            NewPlan.SetCalculationModel(CalculationType.PhotonIMRTOptimization, originalPlan.GetCalculationModel(CalculationType.PhotonIMRTOptimization));

                if (_settings.Debug) { await UpdateListBox($"New plan created with id {NewPlan.Id}"); }
                Log.Debug($"New plan created with id {NewPlan.Id}");

                // Check if there is a PTV
                var CopiedSS = NewPlan.StructureSet;
                var body = CopiedSS.Structures.Where(s => s.Id.ToLower().Contains("body")).First();

                // Perform dose calc
                NewPlan.SetCalculationModel(CalculationType.PhotonVolumeDose, originalPlan.PhotonCalculationModel);
                NewPlan.CalculateDose();

                NewPlan.SetPrescription((int)originalPlan.NumberOfFractions, originalPlan.DosePerFraction, originalPlan.TreatmentPercentage);
                //REM: NewPlan.SetPrescription(25, new DoseValue(2, DoseUnit.Gy), 1);

                if (_settings.Debug) { await UpdateListBox($"Set dose normalization to global max"); }
            Log.Debug($"Set dose normalization to global max");

            var maxBodyDose = originalPlan.GetDVHCumulativeData(body, DoseValuePresentation.Relative, VolumePresentation.Relative, 1).MaxDose;
            NewPlan.PlanNormalizationValue = maxBodyDose.Dose;

            //var DM3D = NewPlan.Dose.DoseMax3D;
                if (_settings.Debug) { await UpdateListBox($"Dose calculation finished"); }
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
                if (_settings.Debug) { await UpdateListBox($"Create PTV_OPT from 50IDL with volume: {PTV_OPT.Volume:F2} CC"); }
                Log.Debug($"Create PTV_OPT from 50IDL with volume: {PTV_OPT.Volume:F2} CC");
            }

            else
            {
                // Use fresh structure reference from new plan
                var newSelectedPTV = CopiedSS.Structures.FirstOrDefault(s => s.Id == SelectedPTV?.Id);
                if (newSelectedPTV == null)
                {
                    throw new Exception($"Selected PTV '{SelectedPTV?.Id}' not found in copied structure set.");
                }

                PTV_OPT.SegmentVolume = newSelectedPTV.SegmentVolume;
                if (_settings.Debug) { await UpdateListBox($"Using custom PTV: {newSelectedPTV.Id} with volume: {newSelectedPTV.Volume:F2} CC"); }
                Log.Debug($"Using custom PTV: {newSelectedPTV.Id} with volume: {newSelectedPTV.Volume:F2} CC");
            }

            if (PTV_OPT.Volume < 0.0001)
            {
                throw new Exception($"Structure volume of PTV opt is too low: {PTV_OPT.Volume:F2} CC");
            }

            // Create 85 - 97% Isodose level structures
            //Structure IDL85 = CopiedSS.AddStructure("DOSE_REGION", "__IDL85");
            //IDL85.ConvertDoseLevelToStructure(NewPlan.Dose, new DoseValue(85, DoseUnit.Percent));
            //Ryan - Changed IDL structures to 85,88, 91, 94, and 97
            foreach (var idl in new double[] { 85, 88, 91, 94, 97 })
            {
                AddStructIfNotExists($"__IDL{idl}", CopiedSS, NewPlan, new DoseValue(idl, DoseUnit.Percent), true);
            }

            // Spare heart and lung on PTV - use fresh structure references from new plan
            var newIpsiLung = CopiedSS.Structures.FirstOrDefault(s => s.Id == Ipsi_lung?.Id);
            var newHeart = CopiedSS.Structures.FirstOrDefault(s => s.Id == Heart?.Id);
            
            if (newIpsiLung != null && newHeart != null)
            {
                Utils.SpareLungHeart(PTV_OPT, newIpsiLung, newHeart, CopiedSS);
            }


            // Optimization options
            OptimizationOptionsIMRT opt = new OptimizationOptionsIMRT(1000,
                OptimizationOption.RestartOptimization,
                OptimizationConvergenceOption.TerminateIfConverged,
                OptimizationIntermediateDoseOption.UseIntermediateDose,
                NewPlan.Beams.First().MLC.Id);

            var unpack_getFluenceEnergyMode = Utils.GetFluenceEnergyMode(originalPlan.Beams.First());
            string primary_fluence_mode = unpack_getFluenceEnergyMode.Item1;
            string energy_mode_id = unpack_getFluenceEnergyMode.Item2;

            var machineParameters = new ExternalBeamMachineParameters(
                originalPlan.Beams.First().TreatmentUnit.Id,
                energy_mode_id,
                originalPlan.Beams.First().DoseRate,
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

            foreach (var bm in originalPlan.Beams.Where(b => !b.IsSetupField).ToList())
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
            var RxDose = originalPlan.TotalDose;

            // Clear all previous optimization objectives
                foreach (var oldObjective in optSet.Objectives)
                {
                    optSet.RemoveObjective(oldObjective);
                    if (_settings.Debug) { await UpdateListBox($"Removed old objective {oldObjective}"); }
                }

            // Add all objectives
            // -- PTV_OPT --: 

            // Define IDL strucutures
            var IDL85 = CopiedSS.Structures.Where(st => st.Id == "__IDL85").FirstOrDefault();
            var IDL88 = CopiedSS.Structures.Where(st => st.Id == "__IDL88").FirstOrDefault();
            var IDL91 = CopiedSS.Structures.Where(st => st.Id == "__IDL91").FirstOrDefault();
            var IDL94 = CopiedSS.Structures.Where(st => st.Id == "__IDL94").FirstOrDefault();
            var IDL97 = CopiedSS.Structures.Where(st => st.Id == "__IDL97").FirstOrDefault();

            //if (Settings.Debug) { await UpdateListBox("Creating Mean, 102 % Rx Dose – Priority 50"); }
            //optSet.AddPointObjective(PTV_OPT, OptimizationObjectiveOperator , new DoseValue(1.02 * RxDose.Dose, RxDose.Unit), 50);

                // Zero NTO if settings tell us to
                // RC - NTO priority = 0. Other setting values must be > 0. Changed these values to default NTO settings.
                if (_settings.KillNormalTissueObjectives)
                {
                    if (_settings.Debug) { await UpdateListBox("Creating 0 priority NTO objective"); }
                    optSet.AddNormalTissueObjective(0, 10, 105, 60, 0.05); // This just ensures that the priority of the NTO objective is zero
                }

                if (_settings.Debug) { await UpdateListBox("Creating Mean, 102 % Rx Dose – Priority 50"); }
                optSet.AddMeanDoseObjective(PTV_OPT, new DoseValue(1.02 * RxDose.Dose, RxDose.Unit), 50);
                ////await UpdateListBox($"Added 2");
                // - Lower 95 % Volume, 100 % Rx Dose – Priority 135
                if (_settings.Debug) { await UpdateListBox("Creating lower 95 % Volume, 100 % Rx Dose – Priority 135"); }
                optSet.AddPointObjective(PTV_OPT, OptimizationObjectiveOperator.Lower, new DoseValue(RxDose.Dose, RxDose.Unit), 95, 135);
                ////await UpdateListBox($"Added 3");
                // - Lower 99.9 % Volume, 95 % Rx Dose – Priority 130
                if (_settings.Debug) { await UpdateListBox("Creating lower 99.9 % Volume, 95 % Rx Dose – Priority 130"); }
                optSet.AddPointObjective(PTV_OPT, OptimizationObjectiveOperator.Lower, new DoseValue(0.95 * RxDose.Dose, RxDose.Unit), 99.9, 130);
            ////await UpdateListBox($"Added 4");
                // -- Body --
                // - Upper 0 % Volume, 108 % Rx Dose – Priority 200
                if (_settings.Debug) { await UpdateListBox("Creating upper 0 % Volume, 108 % Rx Dose – Priority 500"); }
                optSet.AddPointObjective(body, OptimizationObjectiveOperator.Upper, new DoseValue((double.Parse(MaxDoseGoal) / 100.0 - 0.01) * RxDose.Dose, RxDose.Unit), 0, 500);
                ////await UpdateListBox($"Added 5");
                // -- 91 % IDL structure --
                // - Upper 0 % Volume, 103 % Rx Dose – Priority 141
                if (_settings.Debug) { await UpdateListBox("Creating upper  5% Volume, 103 % Rx Dose – Priority 141"); }
                optSet.AddPointObjective(IDL91, OptimizationObjectiveOperator.Upper, new DoseValue(1.03 * RxDose.Dose, RxDose.Unit), 0, 141);
                ////await UpdateListBox($"Added 6");
                // -- 94 % IDL structure --
                // - Upper 0 % Volume, 102 % Rx Dose – Priority 143
                if (_settings.Debug) { await UpdateListBox("Creating upper  0% Volume, 102 % Rx Dose – Priority 143"); }
                optSet.AddPointObjective(IDL94, OptimizationObjectiveOperator.Upper, new DoseValue(1.02 * RxDose.Dose, RxDose.Unit), 0, 143);
                // -- 97 % IDL structure --
                // - Upper 0 % Volume, 102 % Rx Dose – Priority 145
                if (_settings.Debug) { await UpdateListBox("Creating upper  0% Volume, 102 % Rx Dose – Priority 145"); }
                optSet.AddPointObjective(IDL97, OptimizationObjectiveOperator.Upper, new DoseValue(1.02 * RxDose.Dose, RxDose.Unit), 0, 145);
                // -- 88 % IDL structure --
                // - Upper 20 % Volume, 103 % Rx Dose – Priority 118
                if (_settings.Debug) { await UpdateListBox("Creating upper  20% Volume, 103 % Rx Dose – Priority 118"); }
                optSet.AddPointObjective(IDL88, OptimizationObjectiveOperator.Upper, new DoseValue(1.03 * RxDose.Dose, RxDose.Unit), 20, 118);
                // -- 88 % IDL structure --
                // - Upper 6 % Volume, 105 % Rx Dose – Priority 122
                if (_settings.Debug) { await UpdateListBox("Creating upper  6% Volume, 105 % Rx Dose – Priority 122"); }
                optSet.AddPointObjective(IDL88, OptimizationObjectiveOperator.Upper, new DoseValue(1.05 * RxDose.Dose, RxDose.Unit), 6, 122);
                // -- 85 % IDL structure --
                // - Upper 25 % Volume, 103 % Rx Dose – Priority 115
                if (_settings.Debug) { await UpdateListBox("Creating upper  25% Volume, 103 % Rx Dose – Priority 115"); }
                optSet.AddPointObjective(IDL85, OptimizationObjectiveOperator.Upper, new DoseValue(1.03 * RxDose.Dose, RxDose.Unit), 25, 115);
                // -- 85 % IDL structure --                    
                // - Upper 10 % Volume, 105 % Rx Dose – Priority 120
                if (_settings.Debug) { await UpdateListBox("Creating upper  10% Volume, 105 % Rx Dose – Priority 120"); }
                optSet.AddPointObjective(IDL85, OptimizationObjectiveOperator.Upper, new DoseValue(1.05 * RxDose.Dose, RxDose.Unit), 10, 120);

                // Add fluence smoothing and fixed jaw (on/off) to all beams
                foreach (var bm in NewPlan.Beams.Where(b => !b.IsSetupField).ToList())
                {
                    if (_settings.Debug) { await UpdateListBox($"{bm.Id}: Setting fluence smoothing factors {_settings.SmoothX} / {_settings.SmoothY} | jaws fixed: {_settings.FixedJaws}"); }
                    optSet.AddBeamSpecificParameter(bm, _settings.SmoothX, _settings.SmoothY, _settings.FixedJaws);
                }

                // Optimize
                if (_settings.Debug) { await UpdateListBox($"Starting initial pass"); }
                Log.Debug("Starting initial pass");
                NewPlan.Optimize(opt);
                if (_settings.Debug) { await UpdateListBox($"Finished initial pass"); }
                Log.Debug("Finished initial pass");

                // Calculate leaf motions and dose after first optimization           
                NewPlan.SetCalculationModel(CalculationType.PhotonLeafMotions, _settings.LMCModel);
                if (_settings.Debug) { await UpdateListBox($"Calc'ing leaf motions with fixed jaws: {_settings.FixedJaws}"); }
                var lmcOptions = new LMCVOptions(_settings.FixedJaws);
                NewPlan.CalculateLeafMotions(lmcOptions);

                if (_settings.Debug) { await UpdateListBox($"Calc'ing dose"); }
                Log.Debug("Calc'ing dose");
                NewPlan.CalculateDose();
                // Calculate dose after first optimization
                if (_settings.Debug) { await UpdateListBox($"Finished calc'ing dose"); }
                Log.Debug("Finished calc'ing dose");

                // Dose level check
                DoseValue HotSpotIDL = new DoseValue(_settings.HotSpotIDL, DoseValue.DoseUnit.Percent);
                if (HotSpotIDL > NewPlan.Dose.DoseMax3D)
                {
                    var msg = $"Warning: HotspotIDL from config {_settings.HotSpotIDL} is greater than 3D dose max: {NewPlan.Dose.DoseMax3D.Dose}";
                    if (_settings.Debug) { await UpdateListBox(msg); }
                    Log.Debug(msg);

                    HotSpotIDL = NewPlan.Dose.DoseMax3D * 0.99;
                }

                SepDmaxEdgeAfterOpt = Utils.ComputeBeamSeparationWholeField(NewPlan.Beams.First(), NewPlan.Beams.Last(), body, SelectedBreastSide, NewPlan.Dose.DoseMax3DLocation.z);

                if (_settings.SecondOpt)
                {
                    if (_settings.Debug) { await UpdateListBox("Starting second pass"); }
                    Log.Debug("Starting second pass");
                    // Create hot and cold spotes

                    if (_settings.HotColdIDLSecondOpt)
                    {
                        var coldSpot = AddStructIfNotExists("__coldSpot", CopiedSS, NewPlan, new DoseValue(_settings.ColdSpotIDL, DoseValue.DoseUnit.Percent), true);
                        coldSpot.SegmentVolume = PTV_OPT.Sub(coldSpot.SegmentVolume);

                        var hotSpot = AddStructIfNotExists("__hotSpot", CopiedSS, NewPlan, new DoseValue(_settings.HotSpotIDL, DoseValue.DoseUnit.Percent), true);

                        // Add objectives for hot and cold spot
                        //optSet.AddPointObjective(hotSpot, OptimizationObjectiveOperator.Upper, new DoseValue(((MaxDoseGoal/100) - 0.02) * RxDose.Dose, RxDose.Unit), 10, 60);

                        optSet.AddPointObjective(hotSpot, OptimizationObjectiveOperator.Upper, new DoseValue(1.03 * RxDose.Dose, RxDose.Unit), 0, 35);
                        optSet.AddPointObjective(coldSpot, OptimizationObjectiveOperator.Lower, new DoseValue(0.98 * RxDose.Dose, RxDose.Unit), 100, 40);

                    }

                    NewPlan.Optimize(opt);
                    NewPlan.SetCalculationModel(CalculationType.PhotonLeafMotions, _settings.LMCModel);
                    NewPlan.CalculateLeafMotions(lmcOptions);
                    NewPlan.CalculateDose();

                    if (_settings.Debug) { await UpdateListBox("Finished second pass"); }
                    Log.Debug("Finished second pass");

                }

                if (_settings.Cleanup)
                {
                    var optStructs = CopiedSS.Structures.Where(s => s.Id.StartsWith("__")).ToList();
                    foreach (var os in optStructs) { CopiedSS.RemoveStructure(os); }
                }

                if (_settings.Debug) { await UpdateListBox("Complete. Close window to view plan."); }
                Log.Debug("Complete. Close window to view plan");

                // Notify completion and trigger refresh of other ViewModels
                StatusMessage = "Plan creation completed. Other tabs will be refreshed automatically.";
                PlanCreationCompleted = true;

                MessageBox.Show($"Plan created with ID {NewPlan.Id}. Please close tool to view.");
            },
            ex =>
            {
                StatusMessage = $"Error: {ex.Message}";
            });
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

            return new Tuple<string, string>(model, version);
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

        public void AddStatusMessage(string message)
        {
            StatusBoxItems.Add(message);
            // Set the last item as selected to trigger auto-scrolling
            if (StatusBoxItems.Count > 0)
            {
                SelectedStatusItem = StatusBoxItems[StatusBoxItems.Count - 1];
            }
        }

        private string _selectedStatusItem;
        public string SelectedStatusItem
        {
            get { return _selectedStatusItem; }
            set { SetProperty(ref _selectedStatusItem, value); }
        }

        private void Execute()
        {
            try
            {
                AddStatusMessage("Starting Auto 3D Sliding Window planning...");
                UpdateStatusMessage("Executing 3D sliding window operation...");
                
                // Implementation for Auto 3D Sliding Window planning
                OnCreateBreastPlanAsync();
                
                AddStatusMessage("Operation completed successfully.");
                UpdateStatusMessage("3D sliding window operation completed successfully.");
            }
            catch (Exception ex)
            {
                AddStatusMessage($"Error: {ex.Message}");
                UpdateStatusMessage($"Error: {ex.Message}");
            }
        }

        // Add status message references to missing locations
        private void UpdateStatusMessage(string message)
        {
            StatusMessage = message;
        }

        private string _statusMessage;
        public string StatusMessage
        {
            get { return _statusMessage; }
            set { SetProperty(ref _statusMessage, value); }
        }

        private bool _planCreationCompleted = false;
        public bool PlanCreationCompleted
        {
            get { return _planCreationCompleted; }
            set { SetProperty(ref _planCreationCompleted, value); }
        }
    }
}
