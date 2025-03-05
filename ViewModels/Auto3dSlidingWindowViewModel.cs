using MAAS_BreastPlan_helper.MAAS_BreastPlan_helper;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using VMS.TPS.Common.Model.API;
using MAAS_BreastPlan_helper.Models;
using VMS.TPS.Common.Model.Types;
using System.Collections.ObjectModel;
using Prism.Commands;
using static VMS.TPS.Common.Model.Types.DoseValue;
using static MAAS_BreastPlan_helper.Models.Utils;
using Newtonsoft.Json;
using System.IO;
using System.Text.RegularExpressions;
using Serilog;
using System.Windows.Threading;


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
        private readonly EsapiWorker _esapiWorker;
        #region class members
        private ScriptContext Context { get; set; }
        private SettingsClass Settings { get; set; }
        private Patient Patient { get; set; }
        //private ExternalPlanSetup Plan { get; set; }
        private ExternalPlanSetup plan;

        private bool _isRunning = false;

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
        #endregion class members
        private static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            // Close the log when the program exits
            Log.CloseAndFlush();
        }
        private void Precheck(ScriptContext ctx)
        {

            Patient = ctx.Patient;
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

            Plan = ctx.PlanSetup as ExternalPlanSetup;
            if (Plan == null)
            {
                bPrecheckPass = false;
                message += "Error: Plan is null\n";
            }

            Log.Debug($"Starting autoplan. Debug = {Settings.Debug}");
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

            //if (Settings.Debug) { await UpdateListBox("All checks passed, copying to new plan"); }
            Log.Debug("All checks passed, copying to new plan");
        }
        public Auto3dSlidingWindowViewModel(ScriptContext context, SettingsClass settings, string json_path, EsapiWorker esapiWorker)
        {
            Context = context;
            Settings = settings;
            JsonPath = json_path;
            _esapiWorker = esapiWorker;

            _esapiWorker.RunWithWait(ctx => Precheck(ctx));

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

            // Initialize observable collections        
            var ss = Plan.StructureSet;
            if (ss == null) { throw new Exception("Structure set is null"); }
            var structs = plan.StructureSet.Structures;
            if (structs == null) { throw new Exception("Structures are null"); }
            var lHeartStructures = structs.Where(s => s.Id.ToLower().Contains("heart")).ToList();
            if (lHeartStructures.Count == 0) { throw new Exception("Heart structures are empty"); }

            HeartStructures = new ObservableCollection<Structure>();
            foreach (var structure in lHeartStructures) { HeartStructures.Add(structure); }
            Heart = HeartStructures.FirstOrDefault();

            BreastSides = new ObservableCollection<SIDE>() { SIDE.RIGHT, SIDE.LEFT };
            SelectedBreastSide = FindTreatmentSide(Plan); ;

            var lLungStructures = Plan.StructureSet.Structures.Where(s => s.Id.ToLower().Contains("lung")).ToList();
            LungStructures = new ObservableCollection<Structure>();
            foreach (var strucuture in lLungStructures) { LungStructures.Add(strucuture); }

            // Select lung structure based on the position of the center point
            if (selectedBreastSide == SIDE.LEFT)
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
            foreach (var s in Plan.StructureSet.Structures.Where(s => s.Id.ToLower().Contains("ptv")))
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

        public void OnCreateBreastPlanAsync()
        {
            // Save some properties back to config
            Settings.LMCModel = joinLMC(LMCModel, LMCVersion);
            File.WriteAllText(JsonPath, JsonConvert.SerializeObject(Settings));

            // Show that we're starting the process
            UpdateListBox("Starting plan creation process...");

            // Flag that we're running
            _isRunning = true;

            // Start a separate thread to keep UI responsive
            System.Threading.Tasks.Task.Run(() =>
            {
                // Run the operation asynchronously
                _esapiWorker.Run(context =>
                {
                    try
                    {
                        // Copy plan and set new name
                        var NewPlan = context.Course.CopyPlanSetup(Plan) as ExternalPlanSetup;
                        NewPlan.Id = Utils.GetNewPlanName(context.Course, Plan.Id, 13);

                        // Force UI update between operations
                        PostUpdateToUI($"New plan created with id {NewPlan.Id}");


                        NewPlan.SetCalculationModel(CalculationType.PhotonVolumeDose, Plan.PhotonCalculationModel);
                        NewPlan.SetCalculationModel(CalculationType.PhotonIMRTOptimization, Plan.GetCalculationModel(CalculationType.PhotonIMRTOptimization));

                        // Update UI with progress info
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (Settings.Debug)
                            {
                                StatusBoxItems.Add($"New plan created with id {NewPlan.Id}");
                            }
                        });

                        Log.Debug($"New plan created with id {NewPlan.Id}");

                        // Check if there is a PTV
                        var CopiedSS = NewPlan.StructureSet;
                        var body = CopiedSS.Structures.Where(s => s.Id.ToLower().Contains("body")).First();

                        // Perform dose calc
                        NewPlan.SetCalculationModel(CalculationType.PhotonVolumeDose, Plan.PhotonCalculationModel);
                        NewPlan.CalculateDose();

                        NewPlan.SetPrescription((int)Plan.NumberOfFractions, Plan.DosePerFraction, Plan.TreatmentPercentage);

                        // Update UI with progress info
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (Settings.Debug)
                            {
                                StatusBoxItems.Add($"Set dose normalization to global max");
                            }
                        });

                        Log.Debug($"Set dose normalization to global max");

                        var maxBodyDose = Plan.GetDVHCumulativeData(body, DoseValuePresentation.Relative, VolumePresentation.Relative, 1).MaxDose;
                        NewPlan.PlanNormalizationValue = maxBodyDose.Dose;

                        // Update UI with progress info
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (Settings.Debug)
                            {
                                StatusBoxItems.Add($"Dose calculation finished");
                            }
                        });

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
                            PTV_OPT.SegmentVolume = PTV_OPT.AsymmetricMargin(margin);

                            // Update UI with progress info
                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                if (Settings.Debug)
                                {
                                    StatusBoxItems.Add($"Create PTV_OPT from 50IDL with volume: {PTV_OPT.Volume:F2} CC");
                                }
                            });

                            Log.Debug($"Create PTV_OPT from 50IDL with volume: {PTV_OPT.Volume:F2} CC");
                        }
                        else
                        {
                            PTV_OPT.SegmentVolume = SelectedPTV.SegmentVolume;

                            // Update UI with progress info
                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                if (Settings.Debug)
                                {
                                    StatusBoxItems.Add($"Using custom PTV: {SelectedPTV.Id} with volume: {SelectedPTV.Volume:F2} CC");
                                }
                            });

                            Log.Debug($"Using custom PTV: {SelectedPTV.Id} with volume: {SelectedPTV.Volume:F2} CC");
                        }

                        if (PTV_OPT.Volume < 0.0001)
                        {
                            throw new Exception($"Structure volume of PTV opt is too low: {PTV_OPT.Volume:F2} CC");
                        }

                        // Create 85 - 97% Isodose level structures
                        foreach (var idl in new double[] { 85, 88, 91, 94, 97 })
                        {
                            AddStructIfNotExists($"__IDL{idl}", CopiedSS, NewPlan, new DoseValue(idl, DoseUnit.Percent), true);
                        }

                        // Spare heart and lung on PTV
                        Utils.SpareLungHeart(PTV_OPT, Ipsi_lung, Heart, CopiedSS);

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

                        // Clear all previous optimization objectives
                        foreach (var oldObjective in optSet.Objectives)
                        {
                            optSet.RemoveObjective(oldObjective);

                            // Update UI with progress info
                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                if (Settings.Debug)
                                {
                                    StatusBoxItems.Add($"Removed old objective {oldObjective}");
                                }
                            });
                        }

                        // Define IDL structures
                        var IDL85 = CopiedSS.Structures.Where(st => st.Id == "__IDL85").FirstOrDefault();
                        var IDL88 = CopiedSS.Structures.Where(st => st.Id == "__IDL88").FirstOrDefault();
                        var IDL91 = CopiedSS.Structures.Where(st => st.Id == "__IDL91").FirstOrDefault();
                        var IDL94 = CopiedSS.Structures.Where(st => st.Id == "__IDL94").FirstOrDefault();
                        var IDL97 = CopiedSS.Structures.Where(st => st.Id == "__IDL97").FirstOrDefault();

                        // Zero NTO if settings tell us to
                        if (Settings.KillNormalTissueObjectives)
                        {
                            // Update UI with progress info
                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                if (Settings.Debug)
                                {
                                    StatusBoxItems.Add("Creating 0 priority NTO objective");
                                }
                            });

                            optSet.AddNormalTissueObjective(0, 10, 105, 60, 0.05);
                        }

                        // Add all optimization objectives
                        // Update UI with progress info for Mean objective
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (Settings.Debug)
                            {
                                StatusBoxItems.Add("Creating Mean, 102 % Rx Dose – Priority 50");
                            }
                        });

                        optSet.AddMeanDoseObjective(PTV_OPT, new DoseValue(1.02 * RxDose.Dose, RxDose.Unit), 50);

                        // Lower 95% Volume, 100% Rx Dose – Priority 135
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (Settings.Debug)
                            {
                                StatusBoxItems.Add("Creating lower 95 % Volume, 100 % Rx Dose – Priority 135");
                            }
                        });

                        optSet.AddPointObjective(PTV_OPT, OptimizationObjectiveOperator.Lower, new DoseValue(RxDose.Dose, RxDose.Unit), 95, 135);

                        // Lower 99.9% Volume, 95% Rx Dose – Priority 130
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (Settings.Debug)
                            {
                                StatusBoxItems.Add("Creating lower 99.9 % Volume, 95 % Rx Dose – Priority 130");
                            }
                        });

                        optSet.AddPointObjective(PTV_OPT, OptimizationObjectiveOperator.Lower, new DoseValue(0.95 * RxDose.Dose, RxDose.Unit), 99.9, 130);

                        // Upper 0% Volume, 108% Rx Dose – Priority 500
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (Settings.Debug)
                            {
                                StatusBoxItems.Add("Creating upper 0 % Volume, 108 % Rx Dose – Priority 500");
                            }
                        });

                        optSet.AddPointObjective(body, OptimizationObjectiveOperator.Upper, new DoseValue(((MaxDoseGoal / 100) - 0.01) * RxDose.Dose, RxDose.Unit), 0, 500);

                        // Upper 0% Volume, 103% Rx Dose – Priority 141
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (Settings.Debug)
                            {
                                StatusBoxItems.Add("Creating upper 0% Volume, 103 % Rx Dose – Priority 141");
                            }
                        });

                        optSet.AddPointObjective(IDL91, OptimizationObjectiveOperator.Upper, new DoseValue(1.03 * RxDose.Dose, RxDose.Unit), 0, 141);

                        // Upper 0% Volume, 102% Rx Dose – Priority 143
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (Settings.Debug)
                            {
                                StatusBoxItems.Add("Creating upper 0% Volume, 102 % Rx Dose – Priority 143");
                            }
                        });

                        optSet.AddPointObjective(IDL94, OptimizationObjectiveOperator.Upper, new DoseValue(1.02 * RxDose.Dose, RxDose.Unit), 0, 143);

                        // Upper 0% Volume, 102% Rx Dose – Priority 145
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (Settings.Debug)
                            {
                                StatusBoxItems.Add("Creating upper 0% Volume, 102 % Rx Dose – Priority 145");
                            }
                        });

                        optSet.AddPointObjective(IDL97, OptimizationObjectiveOperator.Upper, new DoseValue(1.02 * RxDose.Dose, RxDose.Unit), 0, 145);

                        // Upper 20% Volume, 103% Rx Dose – Priority 118
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (Settings.Debug)
                            {
                                StatusBoxItems.Add("Creating upper 20% Volume, 103 % Rx Dose – Priority 118");
                            }
                        });

                        optSet.AddPointObjective(IDL88, OptimizationObjectiveOperator.Upper, new DoseValue(1.03 * RxDose.Dose, RxDose.Unit), 20, 118);

                        // Upper 6% Volume, 105% Rx Dose – Priority 122
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (Settings.Debug)
                            {
                                StatusBoxItems.Add("Creating upper 6% Volume, 105 % Rx Dose – Priority 122");
                            }
                        });

                        optSet.AddPointObjective(IDL88, OptimizationObjectiveOperator.Upper, new DoseValue(1.05 * RxDose.Dose, RxDose.Unit), 6, 122);

                        // Upper 25% Volume, 103% Rx Dose – Priority 115
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (Settings.Debug)
                            {
                                StatusBoxItems.Add("Creating upper 25% Volume, 103 % Rx Dose – Priority 115");
                            }
                        });

                        optSet.AddPointObjective(IDL85, OptimizationObjectiveOperator.Upper, new DoseValue(1.03 * RxDose.Dose, RxDose.Unit), 25, 115);

                        // Upper 10% Volume, 105% Rx Dose – Priority 120
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (Settings.Debug)
                            {
                                StatusBoxItems.Add("Creating upper 10% Volume, 105 % Rx Dose – Priority 120");
                            }
                        });

                        optSet.AddPointObjective(IDL85, OptimizationObjectiveOperator.Upper, new DoseValue(1.05 * RxDose.Dose, RxDose.Unit), 10, 120);

                        // Add fluence smoothing and fixed jaw (on/off) to all beams
                        foreach (var bm in NewPlan.Beams.Where(b => !b.IsSetupField).ToList())
                        {
                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                if (Settings.Debug)
                                {
                                    StatusBoxItems.Add($"{bm.Id}: Setting fluence smoothing factors {Settings.SmoothX} / {Settings.SmoothY} | jaws fixed: {Settings.FixedJaws}");
                                }
                            });

                            optSet.AddBeamSpecificParameter(bm, Settings.SmoothX, Settings.SmoothY, Settings.FixedJaws);
                        }

                        // Optimize
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (Settings.Debug)
                            {
                                StatusBoxItems.Add($"Starting initial pass");
                            }
                        });

                        Log.Debug("Starting initial pass");
                        NewPlan.Optimize(opt);

                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (Settings.Debug)
                            {
                                StatusBoxItems.Add($"Finished initial pass");
                            }
                        });

                        Log.Debug("Finished initial pass");

                        // Calculate leaf motions and dose after first optimization           
                        NewPlan.SetCalculationModel(CalculationType.PhotonLeafMotions, Settings.LMCModel);

                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (Settings.Debug)
                            {
                                StatusBoxItems.Add($"Calc'ing leaf motions with fixed jaws: {Settings.FixedJaws}");
                            }
                        });

                        var lmcOptions = new LMCVOptions(Settings.FixedJaws);
                        NewPlan.CalculateLeafMotions(lmcOptions);

                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (Settings.Debug)
                            {
                                StatusBoxItems.Add($"Calc'ing dose");
                            }
                        });

                        Log.Debug("Calc'ing dose");
                        NewPlan.CalculateDose();

                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (Settings.Debug)
                            {
                                StatusBoxItems.Add($"Finished calc'ing dose");
                            }
                        });

                        Log.Debug("Finished calc'ing dose");

                        // Dose level check
                        DoseValue HotSpotIDL = new DoseValue(Settings.HotSpotIDL, DoseValue.DoseUnit.Percent);
                        if (HotSpotIDL > NewPlan.Dose.DoseMax3D)
                        {
                            var msg = $"Warning: HotspotIDL from config {Settings.HotSpotIDL} is greater than 3D dose max: {NewPlan.Dose.DoseMax3D.Dose}";

                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                if (Settings.Debug)
                                {
                                    StatusBoxItems.Add(msg);
                                }
                            });

                            Log.Debug(msg);

                            HotSpotIDL = NewPlan.Dose.DoseMax3D * 0.99;
                        }

                        // Update the separation value in the UI
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            SepDmaxEdgeAfterOpt = Utils.ComputeBeamSeparationWholeField(NewPlan.Beams.First(), NewPlan.Beams.Last(), body, SelectedBreastSide, NewPlan.Dose.DoseMax3DLocation.z);
                        });

                        if (Settings.SecondOpt)
                        {
                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                if (Settings.Debug)
                                {
                                    StatusBoxItems.Add("Starting second pass");
                                }
                            });

                            Log.Debug("Starting second pass");

                            // Create hot and cold spots
                            if (Settings.HotColdIDLSecondOpt)
                            {
                                var coldSpot = AddStructIfNotExists("__coldSpot", CopiedSS, NewPlan, new DoseValue(Settings.ColdSpotIDL, DoseValue.DoseUnit.Percent), true);
                                coldSpot.SegmentVolume = PTV_OPT.Sub(coldSpot.SegmentVolume);

                                var hotSpot = AddStructIfNotExists("__hotSpot", CopiedSS, NewPlan, new DoseValue(Settings.HotSpotIDL, DoseValue.DoseUnit.Percent), true);

                                // Add objectives for hot and cold spot
                                var RxDose_ = Plan.TotalDose;
                                optSet.AddPointObjective(hotSpot, OptimizationObjectiveOperator.Upper, new DoseValue(1.03 * RxDose_.Dose, RxDose_.Unit), 0, 35);
                                optSet.AddPointObjective(coldSpot, OptimizationObjectiveOperator.Lower, new DoseValue(0.98 * RxDose_.Dose, RxDose_.Unit), 100, 40);
                            }

                            NewPlan.Optimize(opt);
                            NewPlan.SetCalculationModel(CalculationType.PhotonLeafMotions, Settings.LMCModel);
                            NewPlan.CalculateLeafMotions(lmcOptions);
                            NewPlan.CalculateDose();

                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                if (Settings.Debug)
                                {
                                    StatusBoxItems.Add("Finished second pass");
                                }
                            });

                            Log.Debug("Finished second pass");
                        }

                        if (Settings.Cleanup)
                        {
                            var optStructs = CopiedSS.Structures.Where(s => s.Id.StartsWith("__")).ToList();
                            foreach (var os in optStructs) { CopiedSS.RemoveStructure(os); }
                        }

                        PostUpdateToUI("Complete. Close window to view plan.");

                        // Show completion message on UI thread
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            _isRunning = false;
                            MessageBox.Show($"Plan created with ID {NewPlan.Id}. Please close tool to view.");
                        });
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"Error creating breast plan: {ex.Message}");

                        // Show error message on UI thread
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            _isRunning = false;
                            MessageBox.Show($"Error creating breast plan: {ex.Message}");
                        });
                    }
                });
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

        // Helper method to post updates to UI and force processing
        private void PostUpdateToUI(string message)
        {
            Log.Debug(message);

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                if (Settings.Debug)
                {
                    StatusBoxItems.Add(message);

                    // Force UI to update by processing messages
                    System.Windows.Application.Current.Dispatcher.Invoke(
                        DispatcherPriority.Background,
                        new Action(() => { }));
                }
            });

            // Small delay to allow UI to refresh
            System.Threading.Thread.Sleep(100);
        }


        private async Task UpdateListBox(string s)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                StatusBoxItems.Add(s);
                // Force the UI to update by scrolling to the newly added item
                if (System.Windows.Application.Current.MainWindow is MainWindow mainWindow)
                {
                    if (mainWindow.FindName("StatusBox") is System.Windows.Controls.ListBox listBox)
                    {
                        listBox.ScrollIntoView(s);
                    }
                }
            });
            await Task.Delay(500);
        }
    }
}