using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.IO;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using System.Windows.Forms;
using System.Reflection;
using Microsoft.SqlServer.Server;
using Microsoft.Practices.Prism.Mvvm;
using Prism.Commands;
using System.Windows.Input;
using MAAS_BreastPlan_helper.Models;
using MAAS_BreastPlan_helper.Services;

namespace MAAS_BreastPlan_helper.ViewModels
{
    public class BreastFiFViewModel : BindableBase
    {
        private readonly EsapiWorker _esapiWorker;
        private readonly SettingsClass _settings;

        public DelegateCommand ExecuteCommand { get; private set; }
        
        private string _statusMessage;
        public string StatusMessage
        {
            get { return _statusMessage; }
            set { SetProperty(ref _statusMessage, value); }
        }
        
        private int _selectedSubFieldCountMed = 2;
        public int SelectedSubFieldCountMed
        {
            get { return _selectedSubFieldCountMed; }
            set { SetProperty(ref _selectedSubFieldCountMed, value); }
        }

        private int _selectedSubFieldCountLat = 2;
        public int SelectedSubFieldCountLat
        {
            get { return _selectedSubFieldCountLat; }
            set { SetProperty(ref _selectedSubFieldCountLat, value); }
        }

        private bool _highEnergyFlag = false;
        public bool HighEnergyFlag
        {
            get { return _highEnergyFlag; }
            set { SetProperty(ref _highEnergyFlag, value); }
        }

        private int _highEnergyLevel = 3;
        public int HighEnergyLevel
        {
            get { return _highEnergyLevel; }
            set { SetProperty(ref _highEnergyLevel, value); }
        }

        private string _selectedHighEnergyMode = "15X";
        public string SelectedHighEnergyMode
        {
            get { return _selectedHighEnergyMode; }
            set { SetProperty(ref _selectedHighEnergyMode, value); }
        }

        private ObservableCollection<string> _highEnergyModes = new ObservableCollection<string>();
        public ObservableCollection<string> HighEnergyModes
        {
            get { return _highEnergyModes; }
            set { SetProperty(ref _highEnergyModes, value); }
        }

        public BreastFiFViewModel(EsapiWorker esapiWorker, SettingsClass settings)
        {
            _esapiWorker = esapiWorker;
            _settings = settings;
            
            ExecuteCommand = new DelegateCommand(Execute, CanExecute);
            StatusMessage = "Ready";
            
            // Populate available high energy modes from the treatment machine
            PopulateAvailableHighEnergyModes();
        }

        private bool CanExecute()
        {
            return _esapiWorker.GetValue(sc => sc.PlanSetup) != null;
        }

        private void PopulateAvailableHighEnergyModes()
        {
            try
            {
                _esapiWorker.ExecuteWithErrorHandling(sc =>
                {
                    // Clear existing modes
                    HighEnergyModes.Clear();
                    
                    // Get the treatment machine from current plan's beams
                    var currentPlan = sc.PlanSetup as ExternalPlanSetup;
                    if (currentPlan?.Beams?.FirstOrDefault()?.TreatmentUnit == null)
                    {
                        // Fallback to default high energy modes if no plan/machine available
                        HighEnergyModes.Add("10X");
                        HighEnergyModes.Add("15X");
                        HighEnergyModes.Add("18X");
                        HighEnergyModes.Add("23X");
                        StatusMessage = "Using default high energy modes (no active plan detected)";
                        return;
                    }
                    
                    var treatmentUnit = currentPlan.Beams.First().TreatmentUnit;
                    var machineId = treatmentUnit.Id;
                    
                    // Get all available energy modes from the treatment unit
                    var availableEnergyModes = new HashSet<string>();
                    
                    // Query available energy modes from the machine's capabilities
                    // Note: ESAPI doesn't provide direct access to all machine energy modes,
                    // so we'll use the energy modes from existing beams and supplement with common high energy modes
                    
                    // Get energy modes from current plan beams
                    foreach (var beam in currentPlan.Beams.Where(b => !b.IsSetupField))
                    {
                        var energyMode = beam.EnergyModeDisplayName;
                        if (!string.IsNullOrEmpty(energyMode))
                        {
                            // Extract just the energy part (e.g., "15X" from "15X-FFF")
                            var energyOnly = energyMode.Split('-')[0];
                            if (IsHighEnergyMode(energyOnly))
                            {
                                availableEnergyModes.Add(energyOnly);
                            }
                        }
                    }
                    
                    // Add common high energy modes that might be available on this machine type
                    var commonHighEnergyModes = GetCommonHighEnergyModes(machineId);
                    foreach (var mode in commonHighEnergyModes)
                    {
                        availableEnergyModes.Add(mode);
                    }
                    
                    // Sort and add to collection
                    var sortedModes = availableEnergyModes.OrderBy(mode => 
                    {
                        // Extract numeric part for sorting
                        var numericPart = System.Text.RegularExpressions.Regex.Match(mode, @"\d+").Value;
                        return int.TryParse(numericPart, out int num) ? num : 0;
                    }).ToList();
                    
                    foreach (var mode in sortedModes)
                    {
                        HighEnergyModes.Add(mode);
                    }
                    
                    // If no high energy modes found, add default fallback
                    if (HighEnergyModes.Count == 0)
                    {
                        HighEnergyModes.Add("10X");
                        HighEnergyModes.Add("15X");
                        StatusMessage = "Using default high energy modes (none detected on machine)";
                    }
                    else
                    {
                        StatusMessage = $"Found {HighEnergyModes.Count} high energy modes available on {machineId}";
                    }
                    
                    // Set default selection to first available mode, or 15X if available
                    if (HighEnergyModes.Contains("15X"))
                    {
                        SelectedHighEnergyMode = "15X";
                    }
                    else if (HighEnergyModes.Count > 0)
                    {
                        SelectedHighEnergyMode = HighEnergyModes.First();
                    }
                    
                }, ex =>
                {
                    // Error handling - use default modes
                    HighEnergyModes.Clear();
                    HighEnergyModes.Add("10X");
                    HighEnergyModes.Add("15X");
                    HighEnergyModes.Add("18X");
                    HighEnergyModes.Add("23X");
                    StatusMessage = $"Error detecting machine energy modes: {ex.Message}. Using defaults.";
                });
            }
            catch (Exception ex)
            {
                // Fallback to default modes on any error
                HighEnergyModes.Clear();
                HighEnergyModes.Add("10X");
                HighEnergyModes.Add("15X");
                HighEnergyModes.Add("18X");
                HighEnergyModes.Add("23X");
                StatusMessage = $"Error populating energy modes: {ex.Message}. Using defaults.";
            }
        }
        
        private bool IsHighEnergyMode(string energyMode)
        {
            // Consider 10X and above as high energy modes
            var numericPart = System.Text.RegularExpressions.Regex.Match(energyMode, @"\d+").Value;
            if (int.TryParse(numericPart, out int energy))
            {
                return energy >= 10; // 10X, 15X, 18X, 23X, etc.
            }
            return false;
        }
        
        private List<string> GetCommonHighEnergyModes(string machineId)
        {
            var modes = new List<string>();
            
            // Add common high energy modes based on machine type
            if (machineId.ToUpper().Contains("TRUEBEAM") || machineId.ToUpper().Contains("CLINAC"))
            {
                modes.AddRange(new[] { "10X", "15X", "18X" });
            }
            else if (machineId.ToUpper().Contains("EDGE"))
            {
                modes.AddRange(new[] { "10X", "15X", "18X" });
            }
            else if (machineId.ToUpper().Contains("HALCYON"))
            {
                modes.AddRange(new[] { "10X" }); // Halcyon typically has 6X and 10X
            }
            else if (machineId.ToUpper().Contains("ETHOS"))
            {
                modes.AddRange(new[] { "10X", "15X" });
            }
            else
            {
                // Generic fallback for unknown machines
                modes.AddRange(new[] { "10X", "15X", "18X", "23X" });
            }
            
            return modes;
        }

        /// <summary>
        /// Public method to refresh available high energy modes (useful when plan or machine changes)
        /// </summary>
        public void RefreshAvailableHighEnergyModes()
        {
            PopulateAvailableHighEnergyModes();
        }

        private void Execute()
        {
            try
            {
                StatusMessage = "Executing BreastFiF...";
                
                _esapiWorker.ExecuteWithErrorHandling(sc =>
                {
                    ExecuteBreastFiF(sc);
                StatusMessage = "BreastFiF completed successfully.";
                },
                ex =>
                {
                    StatusMessage = $"Error: {ex.Message}";
                    System.Windows.MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                System.Windows.MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void ExecuteBreastFiF(ScriptContext context)
        {
            //Check if Plan ID already exist, if not, run initiateplan()
            if (context.Course == null || context.StructureSet == null)
            {
                MessageBox.Show("Please open a patient and a plan", "Varian Developer");
                return;
            }

            //Get current patient and structure set
            Course cs = context.Course;
            StructureSet st_set = context.StructureSet;
            Structure body = find_structure(st_set, true, "body");
            if (body == null)
            {
                MessageBox.Show("Cannot find 'body'. Please generate the structure and rerun the script.");
                return;
            }
            Image ct = context.Image;
            Patient pt = context.Patient;
            PlanSetup pl = context.PlanSetup;
            ExternalPlanSetup plExt = context.ExternalPlanSetup;
            PatientOrientation po = pl.TreatmentOrientation;
            if (plExt.GetCalculationModel(CalculationType.PhotonVolumeDose).Length == 0)
            {
                MessageBox.Show("Please select volume dose calculation algorithm");
                return;
            }

            /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

            bool medBeamFound = false, latBeamFound = false;
            foreach (var b in pl.Beams)
            {
                if (Regex.Match(b.Id.ToLower(), "med").Success) { medBeamFound = true; }
                if (Regex.Match(b.Id.ToLower(), "lat").Success) { latBeamFound = true; }
            }

            //check the beams
            if (!medBeamFound)
            {
                MessageBox.Show("Did not find MED beam. Please add 'MED' in the beam name");
                return;
            }
            if (!latBeamFound)
            {
                MessageBox.Show("Did not find LAT beam. Please add 'LAT' in the beam name");
                return;
            }

            ExternalBeamTreatmentUnit ebtu = pl.Beams.FirstOrDefault(x => x.Id.ToLower().Contains("med")).TreatmentUnit;
            string machineID = ebtu.Id;//placeholder

            /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

            //check if the prone is supine or prone
            bool proneFlag = false;
            if (po.Equals(PatientOrientation.HeadFirstProne))
                proneFlag = true;
            double proneConversion;
            if (proneFlag)
                proneConversion = -1;
            else
                proneConversion = 1;

            // Calcula os subcampos a partir do índice da interface (0 → 3, 1 → 4, etc.)
            int subFieldsMed = SelectedSubFieldCountMed + 3;
            int subFieldsLat = SelectedSubFieldCountLat + 3;

            /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

            //acessa o numero de laminas
            int nLeafs = pl.Beams.Last().ControlPoints[0].LeafPositions.Length / 2;

            float[,] LPMed = new float[2, nLeafs]; //LP is initial leaf positon
            float[,] LPLat = new float[2, nLeafs];
            float[,] LPMed1 = new float[2, nLeafs];//segment 2 med
            float[,] LPLat1 = new float[2, nLeafs];//segment 2 lat
            float[,] LPMed2 = new float[2, nLeafs];//segment 3 med
            float[,] LPLat2 = new float[2, nLeafs];//segment 3 lat
            float[,] LPMed3 = new float[2, nLeafs];//segment 4 med
            float[,] LPLat3 = new float[2, nLeafs];//segment 4 lat
            float[,] LPMed4 = new float[2, nLeafs];//segment 5 med
            float[,] LPLat4 = new float[2, nLeafs];//segment 5 lat
            float[,] LPMed5 = new float[2, nLeafs];//segment 6 med
            float[,] LPLat5 = new float[2, nLeafs];//segment 6 lat

            /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

            double[] doseThresholdMed = new double[5];
            double[] doseThresholdLat = new double[5];

            // Thresholds para MED
            switch (subFieldsMed)
            {
                case 3:
                    doseThresholdMed[0] = 107;
                    doseThresholdMed[1] = 103;
                    break;
                case 4:
                    doseThresholdMed[0] = 110;
                    doseThresholdMed[1] = 105;
                    doseThresholdMed[2] = 98;
                    break;
                case 5:
                    doseThresholdMed[0] = 111;
                    doseThresholdMed[1] = 107;
                    doseThresholdMed[2] = 103;
                    doseThresholdMed[3] = 98;
                    break;
                default:
                    doseThresholdMed[0] = 111;
                    doseThresholdMed[1] = 108;
                    doseThresholdMed[2] = 105;
                    doseThresholdMed[3] = 102;
                    doseThresholdMed[4] = 98;
                    break;
            }

            // Thresholds para LAT (podem ser os mesmos ou adaptados)
            switch (subFieldsLat)
            {
                case 3:
                    doseThresholdLat[0] = 107;
                    doseThresholdLat[1] = 103;
                    break;
                case 4:
                    doseThresholdLat[0] = 110;
                    doseThresholdLat[1] = 105;
                    doseThresholdLat[2] = 98;
                    break;
                case 5:
                    doseThresholdLat[0] = 111;
                    doseThresholdLat[1] = 107;
                    doseThresholdLat[2] = 103;
                    doseThresholdLat[3] = 98;
                    break;
                default:
                    doseThresholdLat[0] = 111;
                    doseThresholdLat[1] = 108;
                    doseThresholdLat[2] = 105;
                    doseThresholdLat[3] = 102;
                    doseThresholdLat[4] = 98;
                    break;
            }

            /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

            //define os limites do mlc a partir de sua identificacao           
            var mlc = pl.Beams.FirstOrDefault(x => x.Id.ToLower().Contains("med"))?.MLC.Model;

            double[] boundary = new double[nLeafs + 1];

            if (mlc != null)
            {
                var mlcBoundaryInitializer = new MLCBoundaryInitializer(nLeafs);

                try
                {
                    mlcBoundaryInitializer.InitializeBoundary(mlc, nLeafs);
                    boundary = mlcBoundaryInitializer.Boundary;
                }
                catch (ArgumentException ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
            else
            {
                MessageBox.Show("The MLC wasn't found.");
            }

            double mMUinitial = 150, lMUinitial = 150;//tentative set to 150

            List<double> centroidM = new List<double>();
            List<double> centroidL = new List<double>();

            //Atribuicao das variaveis
            BeamProcessor.NewProcessBeams(pl, centroidM, centroidL, proneFlag, proneConversion, body, ct);

            bool Rside = BeamProcessor.DetermineSide(pl.Beams.FirstOrDefault(x => x.Id.ToLower().Contains("med")), proneFlag);//check if the patient is right brst or left brst
            LPMed = pl.Beams.FirstOrDefault(x => x.Id.ToLower().Contains("med")).ControlPoints[0].LeafPositions;
            LPLat = pl.Beams.FirstOrDefault(x => x.Id.ToLower().Contains("lat")).ControlPoints[0].LeafPositions;

            /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

            //Start patient modification
            pt.BeginModifications();

            Beam med0, lat0; //Med open field, Lat open field.
            //calculate open field dose
            for (int u = 0; u < pl.Beams.Count(); u++)
            {
                Beam ib = pl.Beams.ElementAt(u);

                //Med beam, identified by string "med" in the plan name
                if (Regex.Match(ib.Id.ToLower(), "med").Success)//Med beam
                {
                    med0 = ib;
                    VMS.TPS.Common.Model.API.BeamParameters med0BeamParameter = med0.GetEditableParameters();
                    med0BeamParameter.WeightFactor = 0.5;
                    med0.ApplyParameters(med0BeamParameter);
                }
                else if (Regex.Match(ib.Id.ToLower(), "lat").Success)//Lat beam
                {
                    lat0 = ib;
                    VMS.TPS.Common.Model.API.BeamParameters lat0BeamParameter = lat0.GetEditableParameters();
                    lat0BeamParameter.WeightFactor = 0.5; // apply lateral correction
                    lat0.ApplyParameters(lat0BeamParameter);
                }
            }
            plExt.CalculateDose();// calculate dose to assess initial dose distribution

            double maxDose = pl.Dose.DoseMax3D.Dose;
            pl.PlanNormalizationValue = 100 * maxDose / 115;//this is in percentage; normalize to 115% max dose

            //incorportate MU to balance field weight
            for (int u = 0; u < pl.Beams.Count(); u++)
            {
                Beam ib = pl.Beams.ElementAt(u);
                //Med beam, identified by string "med" in the plan name
                if (Regex.Match(ib.Id.ToLower(), "med").Success)//Med beam
                {
                    mMUinitial = ib.Meterset.Value;
                }
                else if (Regex.Match(ib.Id.ToLower(), "lat").Success)//Lat beam
                {
                    lMUinitial = ib.Meterset.Value;
                }
            }

            //add a new plan
            ExternalPlanSetup newPlan = cs.AddExternalPlanSetup(st_set);

            //adiciona a taxa de dose
            var doserate = pl.Beams.FirstOrDefault(x => x.Id.ToLower().Contains("med")).DoseRate;

            //cria a variavel
            var energyMode = pl.Beams.FirstOrDefault(x => x.Id.ToLower().Contains("med")).EnergyModeDisplayName;
            string primaryFluenceMode = "", technique = "STATIC";

            if (machineID.ToLower().Contains("hal")) { energyMode = "6X"; primaryFluenceMode = "FFF"; }

            ExternalBeamMachineParameters beamPara = new ExternalBeamMachineParameters(machineID, energyMode, doserate, technique, primaryFluenceMode);

            //beam creation
            Beam newMed, newLat;
            Beam originalMed = pl.Beams.FirstOrDefault(x => x.Id.ToLower().Contains("med"));
            Beam originalLat = pl.Beams.FirstOrDefault(x => x.Id.ToLower().Contains("lat"));
            if (machineID.ToLower().Contains("hal"))
            {
                //adiciona os campos do hal
                newMed = newPlan.AddFixedSequenceBeam(beamPara, originalMed.ControlPoints[0].CollimatorAngle, originalMed.ControlPoints[0].GantryAngle, originalMed.IsocenterPosition);
                newMed.RemoveFlatteningSequence();
                newLat = newPlan.AddFixedSequenceBeam(beamPara, originalLat.ControlPoints[0].CollimatorAngle, originalLat.ControlPoints[0].GantryAngle, originalLat.IsocenterPosition);
                newLat.RemoveFlatteningSequence();

                //seta as lps a posteriori - MED beam
                var editables = newMed.GetEditableParameters();
                editables.SetJawPositions(originalMed.ControlPoints[0].JawPositions);
                editables.SetAllLeafPositions(originalMed.ControlPoints[0].LeafPositions);
                newMed.ApplyParameters(editables);

                //seta as lps a posteriori - LAT beam
                editables = newLat.GetEditableParameters();
                editables.SetJawPositions(originalLat.ControlPoints[0].JawPositions);
                editables.SetAllLeafPositions(originalLat.ControlPoints[0].LeafPositions);
                newLat.ApplyParameters(editables);
            }
            else
            {
                newMed = newPlan.AddMLCBeam(beamPara, LPMed, originalMed.ControlPoints[0].JawPositions, originalMed.ControlPoints[0].CollimatorAngle, originalMed.ControlPoints[0].GantryAngle, originalMed.ControlPoints[0].PatientSupportAngle, originalMed.IsocenterPosition);
                newLat = newPlan.AddMLCBeam(beamPara, LPLat, originalLat.ControlPoints[0].JawPositions, originalLat.ControlPoints[0].CollimatorAngle, originalLat.ControlPoints[0].GantryAngle, originalLat.ControlPoints[0].PatientSupportAngle, originalLat.IsocenterPosition);
            }

            newMed.Id = "1 MED";
            newLat.Id = "2 LAT";

            /////////////////////////////////////////
            /////reblance initial two setup fields
            /////////////////////////////////////////

            double meanCentroidM = centroidM.Average();
            double meanCentroidL = centroidL.Average();
            double latCorrection = Math.Pow(meanCentroidL / meanCentroidM, 2) * mMUinitial / lMUinitial;

            //////////////////////////////////////////////////////////////////////////////////////////////
            ///// High energy beam support - Proper weight distribution

            // Calculate weight contributions that sum to 100%
            double highEnergyContribution = HighEnergyFlag ? HighEnergyLevel * 0.05 : 0.0; // Ex: level 3 → 15%
            double lowEnergyContribution = 1.0 - highEnergyContribution; // Remaining percentage for low energy

            // Individual beam weights (each beam type gets half of its energy contribution)
            double highEnergyMedWeight = highEnergyContribution * 0.5;
            double highEnergyLatWeight = highEnergyContribution * 0.5 * latCorrection;
            double lowEnergyMedWeight = lowEnergyContribution * 0.5;
            double lowEnergyLatWeight = lowEnergyContribution * 0.5 * latCorrection;

            // Rebalance factor for subfields (same logic as before, but applied to proper weights)
            double subFieldWeightRebalance = HighEnergyFlag ? 1.0 / lowEnergyContribution : 1.0;

            // Display weight distribution for user feedback
            if (HighEnergyFlag)
            {
                StatusMessage = $"Weight Distribution: High Energy {highEnergyContribution:P1} ({SelectedHighEnergyMode}), Low Energy {lowEnergyContribution:P1} (6X) | " +
                               $"MED Weights: HE={highEnergyMedWeight:F3}, LE={lowEnergyMedWeight:F3} | " +
                               $"LAT Weights: HE={highEnergyLatWeight:F3}, LE={lowEnergyLatWeight:F3}";
            }
            else
            {
                StatusMessage = "Weight Distribution: 100% Low Energy (6X) | " +
                               $"MED Weight: {lowEnergyMedWeight:F3}, LAT Weight: {lowEnergyLatWeight:F3}";
            }



            Beam newMed15 = null;
            Beam newLat15 = null;

            if (HighEnergyFlag)
            {
                var beamPara15X = new ExternalBeamMachineParameters(machineID, SelectedHighEnergyMode, doserate, technique, primaryFluenceMode);

                newMed15 = newPlan.AddMLCBeam(
                    beamPara15X,
                    LPMed,
                    originalMed.ControlPoints[0].JawPositions,
                    originalMed.ControlPoints[0].CollimatorAngle,
                    originalMed.ControlPoints[0].GantryAngle,
                    originalMed.ControlPoints[0].PatientSupportAngle,
                    originalMed.IsocenterPosition
                );
                newMed15.Id = $"1A MED {SelectedHighEnergyMode}";

                newLat15 = newPlan.AddMLCBeam(
                    beamPara15X,
                    LPLat,
                    originalLat.ControlPoints[0].JawPositions,
                    originalLat.ControlPoints[0].CollimatorAngle,
                    originalLat.ControlPoints[0].GantryAngle,
                    originalLat.ControlPoints[0].PatientSupportAngle,
                    originalLat.IsocenterPosition
                );
                newLat15.Id = $"2A LAT {SelectedHighEnergyMode}";

                // Apply correct high energy beam weights
                var med15Params = newMed15.GetEditableParameters();
                med15Params.WeightFactor = highEnergyMedWeight;
                newMed15.ApplyParameters(med15Params);

                var lat15Params = newLat15.GetEditableParameters();
                lat15Params.WeightFactor = highEnergyLatWeight;
                newLat15.ApplyParameters(lat15Params);
            }

            for (int u = 0; u < newPlan.Beams.Count(); u++)
            {
                Beam ib = newPlan.Beams.ElementAt(u);

                //Med beam, identified by string "med" in the plan name
                if (Regex.Match(ib.Id.ToLower(), "med").Success)//Med beam
                {
                    // Skip high energy beams (they already have correct weights)
                    if (!ib.Id.Contains(SelectedHighEnergyMode))
                {
                    med0 = ib;
                    VMS.TPS.Common.Model.API.BeamParameters med0BeamParameter = med0.GetEditableParameters();
                        med0BeamParameter.WeightFactor = lowEnergyMedWeight;
                    med0.ApplyParameters(med0BeamParameter);
                    }
                }
                else if (Regex.Match(ib.Id.ToLower(), "lat").Success)//Lat beam
                {
                    // Skip high energy beams (they already have correct weights)
                    if (!ib.Id.Contains(SelectedHighEnergyMode))
                {
                    lat0 = ib;
                    VMS.TPS.Common.Model.API.BeamParameters lat0BeamParameter = lat0.GetEditableParameters();
                        lat0BeamParameter.WeightFactor = lowEnergyLatWeight; // apply corrected low energy weight
                    lat0.ApplyParameters(lat0BeamParameter);
                    }
                }
            }

            newPlan.SetCalculationModel(CalculationType.PhotonVolumeDose, plExt.PhotonCalculationModel);
            newPlan.CalculateDose();// calculate dose to assess initial dose distribution
            maxDose = newPlan.Dose.DoseMax3D.Dose;

            double normGoal = HighEnergyFlag ? 109 : 115;
            newPlan.PlanNormalizationValue = 100 * maxDose / normGoal;//this is in percentage; normalize to 115% or 109% max dose

            Dose dPlan = newPlan.Dose;
            for (int i = 0; i < 2; i++)
            {
                for (int j = 0; j < nLeafs; j++)
                {
                    LPMed1[i, j] = 0;
                    LPLat1[i, j] = 0;
                    LPMed2[i, j] = 0;
                    LPLat2[i, j] = 0;
                    LPMed3[i, j] = 0;
                    LPLat3[i, j] = 0;
                    LPMed4[i, j] = 0;
                    LPLat4[i, j] = 0;
                    LPMed5[i, j] = 0;
                    LPLat5[i, j] = 0;
                }
            }

            ////////////////////////////////////////
            /////Record doseM doseL
            ///////////////////////////////////////

            //med/lat beam pixel penetration depth
            List<double[]> doseM = new List<double[]>();
            List<double[]> doseL = new List<double[]>();

            BeamProcessor.ProcessDoses(newPlan, proneFlag, proneConversion, body, ct, dPlan, doseM, doseL);

            ///////////////////////////////////////
            //add segments MLC Med beam
            ///////////////////////////////////////

            //acesssa os jaws
            double med_x1 = originalMed.ControlPoints[0].JawPositions.X1, med_x2 = originalMed.ControlPoints[0].JawPositions.X2, lat_x1 = originalLat.ControlPoints[0].JawPositions.X1, lat_x2 = originalLat.ControlPoints[0].JawPositions.X2;//beam jaw size          
            double med_y1 = originalMed.ControlPoints[0].JawPositions.Y1, med_y2 = originalMed.ControlPoints[0].JawPositions.Y2, lat_y1 = originalLat.ControlPoints[0].JawPositions.Y1, lat_y2 = originalLat.ControlPoints[0].JawPositions.Y2;//beam jaw size 

            //achar a origem
            double med_sizex = 2 * Convert.ToInt32(Math.Max(Math.Abs(med_x1), Math.Abs(med_x2)) / 2.5 + 16);
            double med_sizey = 2 * Convert.ToInt32(Math.Max(Math.Abs(med_y1), Math.Abs(med_y2)) / 2.5 + 16);
            //sampling origin
            double med_int_x = -((med_sizex / 2 - 1) * 2.5 + 2.5 / 2);
            double med_int_y = (med_sizey / 2 - 1) * 2.5 + 2.5 / 2;

            //sampling size
            double lat_sizex = 2 * Convert.ToInt32(Math.Max(Math.Abs(lat_x1), Math.Abs(lat_x2)) / 2.5 + 16);//corresponds to 4 cm black area
            double lat_sizey = 2 * Convert.ToInt32(Math.Max(Math.Abs(lat_y1), Math.Abs(lat_y2)) / 2.5 + 16);//corresponds to 4 cm black area
            //sampling origin
            double lat_int_x = -((lat_sizex / 2 - 1) * 2.5 + 2.5 / 2);
            double lat_int_y = (lat_sizey / 2 - 1) * 2.5 + 2.5 / 2;

            ////add segment MLC Med beam
            BeamProcessor.AdjustMLCSegmentMed(LPMed1, LPMed, 0, nLeafs, boundary, med_y1, med_y2, med_int_y, med_int_x, doseM, doseThresholdMed, Rside);
            BeamProcessor.AdjustMLCSegmentMed(LPMed2, LPMed, 1, nLeafs, boundary, med_y1, med_y2, med_int_y, med_int_x, doseM, doseThresholdMed, Rside);
            BeamProcessor.AdjustMLCSegmentMed(LPMed3, LPMed, 2, nLeafs, boundary, med_y1, med_y2, med_int_y, med_int_x, doseM, doseThresholdMed, Rside);
            BeamProcessor.AdjustMLCSegmentMed(LPMed4, LPMed, 3, nLeafs, boundary, med_y1, med_y2, med_int_y, med_int_x, doseM, doseThresholdMed, Rside);
            BeamProcessor.AdjustMLCSegmentMed(LPMed5, LPMed, 4, nLeafs, boundary, med_y1, med_y2, med_int_y, med_int_x, doseM, doseThresholdMed, Rside);

            //add segment MLC Lat beam - use opposite side from medial beam (!Rside)
            BeamProcessor.AdjustMLCSegment(LPLat1, LPLat, 0, nLeafs, boundary, lat_y1, lat_y2, lat_int_y, lat_int_x, doseL, doseThresholdLat, !Rside);
            BeamProcessor.AdjustMLCSegment(LPLat2, LPLat, 1, nLeafs, boundary, lat_y1, lat_y2, lat_int_y, lat_int_x, doseL, doseThresholdLat, !Rside);
            BeamProcessor.AdjustMLCSegment(LPLat3, LPLat, 2, nLeafs, boundary, lat_y1, lat_y2, lat_int_y, lat_int_x, doseL, doseThresholdLat, !Rside);
            BeamProcessor.AdjustMLCSegment(LPLat4, LPLat, 3, nLeafs, boundary, lat_y1, lat_y2, lat_int_y, lat_int_x, doseL, doseThresholdLat, !Rside);
            BeamProcessor.AdjustMLCSegment(LPLat5, LPLat, 4, nLeafs, boundary, lat_y1, lat_y2, lat_int_y, lat_int_x, doseL, doseThresholdLat, !Rside);

            /////////////////////////////////////
            /////smooth MLC
            /////////////////////////////////////

            if (Rside)
            {
                // Medial beam - smooth from side 0 (left bank)
                BeamProcessor.SmoothMLCSegment(LPMed1, 0, nLeafs);
                BeamProcessor.SmoothMLCSegment(LPMed2, 0, nLeafs);
                BeamProcessor.SmoothMLCSegment(LPMed3, 0, nLeafs);
                BeamProcessor.SmoothMLCSegment(LPMed4, 0, nLeafs);
                BeamProcessor.SmoothMLCSegment(LPMed5, 0, nLeafs);

                // Lateral beam - smooth from opposite side (side 1 for !Rside=false, meaning right bank)
                BeamProcessor.NewSmoothMLCSegment(LPLat1, 1, nLeafs);
                BeamProcessor.NewSmoothMLCSegment(LPLat2, 1, nLeafs);
                BeamProcessor.NewSmoothMLCSegment(LPLat3, 1, nLeafs);
                BeamProcessor.NewSmoothMLCSegment(LPLat4, 1, nLeafs);
                BeamProcessor.NewSmoothMLCSegment(LPLat5, 1, nLeafs);
            }
            else // Left side
            {
                // Medial beam - smooth from side 1 (right bank)
                BeamProcessor.SmoothMLCSegment(LPMed1, 1, nLeafs);
                BeamProcessor.SmoothMLCSegment(LPMed2, 1, nLeafs);
                BeamProcessor.SmoothMLCSegment(LPMed3, 1, nLeafs);
                BeamProcessor.SmoothMLCSegment(LPMed4, 1, nLeafs);
                BeamProcessor.SmoothMLCSegment(LPMed5, 1, nLeafs);

                // Lateral beam - smooth from opposite side (side 0 for !Rside=true, meaning left bank)
                BeamProcessor.NewSmoothMLCSegment(LPLat1, 0, nLeafs);
                BeamProcessor.NewSmoothMLCSegment(LPLat2, 0, nLeafs);
                BeamProcessor.NewSmoothMLCSegment(LPLat3, 0, nLeafs);
                BeamProcessor.NewSmoothMLCSegment(LPLat4, 0, nLeafs);
                BeamProcessor.NewSmoothMLCSegment(LPLat5, 0, nLeafs);
            }

            //post process lateral field MLC from mirroring med due to field size difference
            bool isHal = machineID.ToLower().Contains("hal") || machineID.ToLower().Contains("rds");

            // Add Halcyon-specific diagnostic information
            if (isHal)
            {
                StatusMessage += $" | Halcyon Mode: Enhanced jaw/MLC positioning enabled";
            }

            // Arrays de referência para os feixes
            float[][,] LPMedTot = { LPMed1, LPMed2, LPMed3, LPMed4, LPMed5 };
            float[][,] LPLatTot = { LPLat1, LPLat2, LPLat3, LPLat4, LPLat5 };

            //valores de referencias para campos
            VRect<double> jawsPositionMed = originalMed.ControlPoints[0].JawPositions;
            VRect<double> jawsPositionLat = originalLat.ControlPoints[0].JawPositions;
            double colAngleMed = originalMed.ControlPoints[0].CollimatorAngle, gantryAngleMed = originalMed.ControlPoints[0].GantryAngle, psaAngleMed = originalMed.ControlPoints[0].PatientSupportAngle;
            double colAngleLat = originalLat.ControlPoints[0].CollimatorAngle, gantryAngleLat = originalLat.ControlPoints[0].GantryAngle, psaAngleLat = originalLat.ControlPoints[0].PatientSupportAngle;
            VVector iso = originalMed.IsocenterPosition;

            // Ajuste do peso conforme número de subcampos
            double subFieldWeightMed = 0.015;
            double subFieldWeightLat = 0.015;

            if (subFieldsMed == 6) subFieldWeightMed = 0.015;
            else if (subFieldsMed == 5) subFieldWeightMed = 0.018;
            else if (subFieldsMed == 4) subFieldWeightMed = 0.025;
            else if (subFieldsMed == 3) subFieldWeightMed = 0.035;

            if (subFieldsLat == 6) subFieldWeightLat = 0.015;
            else if (subFieldsLat == 5) subFieldWeightLat = 0.018;
            else if (subFieldsLat == 4) subFieldWeightLat = 0.025;
            else if (subFieldsLat == 3) subFieldWeightLat = 0.035;

            //criacao dos campos
            BeamProcessor.CreateBeams(
                newPlan,
                subFieldsMed - 1, // nº de segmentos
                beamPara,
                LPMedTot,
                null, // sem LAT
                jawsPositionMed,
                jawsPositionLat,
                colAngleMed,
                colAngleLat,
                gantryAngleMed,
                gantryAngleLat,
                psaAngleMed,
                psaAngleLat,
                iso,
                subFieldWeightMed * subFieldWeightRebalance,
                latCorrection,
                isHal
            );

            BeamProcessor.CreateBeams(
                newPlan,
                subFieldsLat - 1,
                beamPara,
                null, // sem MED
                LPLatTot,
                jawsPositionMed,
                jawsPositionLat,
                colAngleMed,
                colAngleLat,
                gantryAngleMed,
                gantryAngleLat,
                psaAngleMed,
                psaAngleLat,
                iso,
                subFieldWeightLat * subFieldWeightRebalance,
                latCorrection,
                isHal
            );

            //final configs
            newPlan.SetPrescription(plExt.NumberOfFractions == null ? 25 : plExt.NumberOfFractions.Value, plExt.DosePerFraction, plExt.TreatmentPercentage);
            newPlan.CalculateDose();
            maxDose = newPlan.Dose.DoseMax3D.Dose;
            double currentNormalizationVal = newPlan.PlanNormalizationValue;
            newPlan.PlanNormalizationValue = currentNormalizationVal * maxDose / 110;//this is in percentage; normalize to 110% max dose
            MessageBox.Show(newPlan.Id + " is created.");
        }

        // ... existing MLCBoundaryInitializer class, BeamProcessor class, and all other methods ...
        
        public class MLCBoundaryInitializer
        {
            private double[] boundary;
            private double mlcLocation;

            public MLCBoundaryInitializer(int nLeafs)
            {
                boundary = new double[nLeafs + 1];
            }

            public double[] Boundary { get { return boundary; } }

            public void InitializeBoundary(string mlcModel, int nLeafs)
            {
                if (mlcModel.Contains("Millennium") && mlcModel.Contains("80"))
                {
                    InitializeMillennium80(nLeafs);
                }
                else if (mlcModel.Contains("Millennium") && mlcModel.Contains("120"))
                {
                    InitializeMillennium120(nLeafs);
                }
                else if ((mlcModel.Contains("HD") || mlcModel.Contains("High Definition")) && mlcModel.Contains("120"))
                {
                    InitializeHD120(nLeafs);
                }
                else if (mlcModel.Contains("SX2"))
                {
                    InitializeSX2(nLeafs);
                }
                else
                {
                    throw new ArgumentException("The MLC wasn't recognized.");
                }
            }

            private void InitializeMillennium80(int nLeafs)
            {
                mlcLocation = -195;
                for (int i = 0; i < nLeafs + 1; i++)
                {
                    boundary[i] = mlcLocation;
                    mlcLocation += 10;
                }
            }

            private void InitializeMillennium120(int nLeafs)
            {
                mlcLocation = -200;
                for (int i = 0; i < nLeafs + 1; i++)
                {
                    boundary[i] = mlcLocation;
                    if (i < 10)
                        mlcLocation += 10; // regular TB MLC
                    else if (i < 50)
                        mlcLocation += 5;  // central MLC
                    else
                        mlcLocation += 10; // peripheral MLC
                }
            }

            private void InitializeHD120(int nLeafs)
            {
                mlcLocation = -107.5;
                for (int i = 0; i < nLeafs + 1; i++)
                {
                    boundary[i] = mlcLocation;
                    if (i < 14)
                        mlcLocation += 5; // HD TB MLC
                    else if (i < 46)
                        mlcLocation += 2.5; // central MLC
                    else
                        mlcLocation += 5; // peripheral MLC
                }
            }

            private void InitializeSX2(int nLeafs)
            {
                mlcLocation = -135;
                for (int i = 1; i < nLeafs + 1; i++)
                {
                    boundary[i] = mlcLocation;
                    if (i < 28)
                    {
                        mlcLocation = -135f + (i * 10f);
                    }
                    else
                    {
                        mlcLocation = -420f + (i * 10f);
                    }
                }
            }
        }

        public class BeamProcessor
        {
            public static void NewProcessBeams(PlanSetup pl, List<double> centroidM, List<double> centroidL, bool proneFlag, double proneConversion, Structure body, Image ct)
            {
                VRect<double> jawsPositionMed, jawsPositionLat;
                double colAngleMed, gantryAngleMed, psaAngleMed;
                double colAngleLat, gantryAngleLat, psaAngleLat;
                VVector iso;

                foreach (Beam beam in pl.Beams)
                {
                    iso = beam.IsocenterPosition;
                    if (Regex.IsMatch(beam.Id, "med", RegexOptions.IgnoreCase))
                    {
                        jawsPositionMed = beam.ControlPoints[0].JawPositions;
                        colAngleMed = beam.ControlPoints[0].CollimatorAngle;
                        gantryAngleMed = beam.ControlPoints[0].GantryAngle;
                        psaAngleMed = beam.ControlPoints[0].PatientSupportAngle;

                        NewProcessBeam(beam, ref jawsPositionMed, ref colAngleMed, ref gantryAngleMed, ref psaAngleMed, ref iso, centroidM, proneFlag, proneConversion, body, ct);
                    }
                    else if (Regex.IsMatch(beam.Id, "lat", RegexOptions.IgnoreCase))
                    {
                        jawsPositionLat = beam.ControlPoints[0].JawPositions;
                        colAngleLat = beam.ControlPoints[0].CollimatorAngle;
                        gantryAngleLat = beam.ControlPoints[0].GantryAngle;
                        psaAngleLat = beam.ControlPoints[0].PatientSupportAngle;

                        NewProcessBeam(beam, ref jawsPositionLat, ref colAngleLat, ref gantryAngleLat, ref psaAngleLat, ref iso, centroidL, proneFlag, proneConversion, body, ct);
                    }
                }
            }
            public static void NewProcessBeam(Beam beam, ref VRect<double> jawsPosition, ref double colAngle, ref double gantryAngle, ref double psaAngle,
                ref VVector iso, List<double> centroidList, bool proneFlag, double proneConversion, Structure body, Image ct)
            {
                ControlPoint ctl = beam.ControlPoints[0];

                if (ctl.LeafPositions.Length == 0)
                {
                    MessageBox.Show("Please add MLC to {0} Beam", beam.Id);
                    return;
                }

                iso = beam.IsocenterPosition;
                jawsPosition = new VRect<double>(ctl.JawPositions.X1, ctl.JawPositions.Y1, ctl.JawPositions.X2, ctl.JawPositions.Y2);

                colAngle = ctl.CollimatorAngle;
                gantryAngle = ctl.GantryAngle;
                psaAngle = ctl.PatientSupportAngle;

                int sizex = GetSampleSize(jawsPosition.X1, jawsPosition.X2);
                int sizey = GetSampleSize(jawsPosition.Y1, jawsPosition.Y2);

                double int_x = -((sizex / 2 - 1) * 2.5 + 2.5 / 2);
                double int_y = (sizey / 2 - 1) * 2.5 + 2.5 / 2;

                VVector source = beam.GetSourceLocation(gantryAngle);

                ProcessSamplingGrid(sizex, sizey, int_x, int_y, jawsPosition, source, iso, gantryAngle, colAngle, psaAngle, centroidList, proneConversion, body, ct);
            }
            public static bool DetermineSide(Beam medBeam, bool proneFlag)
            {
                bool Rside = true;
                double mGantry = medBeam.ControlPoints[0].GantryAngle;
                if (Regex.Match(medBeam.Id.ToLower(), "med").Success)//Med beam
                {
                    if (!proneFlag)//supine
                    {
                        if (mGantry > 0 & mGantry < 90)
                            Rside = true;
                        else if (mGantry > 270 & mGantry < 360)
                            Rside = false;
                        else { MessageBox.Show("Please check the naming and gantry angle of MED beam."); }
                    }
                    else
                    {
                        if (mGantry > 180 & mGantry < 270)
                            Rside = true;
                        else
                            Rside = false;
                    }
                }
                return Rside;
            }
            public static int GetSampleSize(double min, double max)
            {
                return 2 * Convert.ToInt32(Math.Max(Math.Abs(min), Math.Abs(max)) / 2.5 + 16);
            }
            private static bool IsWithinJaws(double x, double y, VRect<double> jaws)
            {
                return x >= jaws.X1 && x <= jaws.X2 && y >= jaws.Y1 && y <= jaws.Y2;
            }
            public static void ProcessSamplingGrid(int sizex, int sizey, double int_x, double int_y, VRect<double> jawsPosition, VVector source, VVector iso,
                double gantryAngle, double colAngle, double psaAngle, List<double> centroidList, double proneConversion, Structure body, Image ct)
            {
                for (int i = 0; i < sizey; i++)
                {
                    for (int j = 0; j < sizex; j++)
                    {
                        double x_dist = int_x + j * 2.5;
                        double y_dist = int_y - i * 2.5;

                        if (x_dist < jawsPosition.X1 || x_dist > jawsPosition.X2 || y_dist < jawsPosition.Y1 || y_dist > jawsPosition.Y2)
                            continue;

                        VVector gridCoor = new VVector(x_dist, y_dist, 0);
                        VVector gridCoorCt = coorConversion(gantryAngle, colAngle, psaAngle, new VVector[] { gridCoor })[0];
                        gridCoorCt.x *= proneConversion;
                        gridCoorCt.y *= proneConversion;
                        gridCoorCt += iso;

                        VVector ray = gridCoorCt - source;
                        ray.ScaleToUnitLength();

                        double[] rayParameter = rayTracing(ray, source, body, ct);
                        double centroid = rayParameter[1];

                        if (centroid != 1000)
                            centroidList.Add(centroid);
                    }
                }
            }
            public static void ProcessDoses(PlanSetup newPlan, bool proneFlag, double proneConversion, Structure body, Image ct, Dose dPlan, List<double[]> doseM, List<double[]> doseL)
            {
                VRect<double> jawsPositionMed, jawsPositionLat;
                double colAngleMed, gantryAngleMed, psaAngleMed;
                double colAngleLat, gantryAngleLat, psaAngleLat;
                VVector iso;

                foreach (Beam beam in newPlan.Beams)
                {
                    iso = beam.IsocenterPosition;
                    if (Regex.IsMatch(beam.Id, "med", RegexOptions.IgnoreCase))
                    {
                        jawsPositionMed = beam.ControlPoints[0].JawPositions;
                        colAngleMed = beam.ControlPoints[0].CollimatorAngle;
                        gantryAngleMed = beam.ControlPoints[0].GantryAngle;
                        psaAngleMed = beam.ControlPoints[0].PatientSupportAngle;

                        ProcessDose(beam, ref jawsPositionMed, ref colAngleMed, ref gantryAngleMed, ref psaAngleMed, ref iso, proneFlag, proneConversion, body, ct, dPlan, doseM);
                    }
                    else if (Regex.IsMatch(beam.Id, "lat", RegexOptions.IgnoreCase))
                    {
                        jawsPositionLat = beam.ControlPoints[0].JawPositions;
                        colAngleLat = beam.ControlPoints[0].CollimatorAngle;
                        gantryAngleLat = beam.ControlPoints[0].GantryAngle;
                        psaAngleLat = beam.ControlPoints[0].PatientSupportAngle;

                        ProcessDose(beam, ref jawsPositionLat, ref colAngleLat, ref gantryAngleLat, ref psaAngleLat, ref iso, proneFlag, proneConversion, body, ct, dPlan, doseL);
                    }
                }
            }
            private static void ProcessDose(Beam beam, ref VRect<double> jawsPosition, ref double colAngle, ref double gantryAngle, ref double psaAngle, ref VVector iso,
            bool proneFlag, double proneConversion, Structure body, Image ct, Dose dPlan, List<double[]> doseList)
            {
                iso = beam.IsocenterPosition;
                ControlPoint cp = beam.ControlPoints[0];

                jawsPosition = new VRect<double>(cp.JawPositions.X1, cp.JawPositions.Y1, cp.JawPositions.X2, cp.JawPositions.Y2);
                colAngle = cp.CollimatorAngle;
                gantryAngle = cp.GantryAngle;
                psaAngle = cp.PatientSupportAngle;

                int sizeX = GetSampleSize(jawsPosition.X1, jawsPosition.X2);
                int sizeY = GetSampleSize(jawsPosition.Y1, jawsPosition.Y2);
                double intX = -((sizeX / 2 - 1) * 2.5 + 2.5 / 2);
                double intY = (sizeY / 2 - 1) * 2.5 + 2.5 / 2;

                VVector source = beam.GetSourceLocation(gantryAngle);
                VVector beamIso = beam.IsocenterPosition;

                for (int i = 0; i < sizeY; i++)
                {
                    double[] dose = new double[sizeX];
                    for (int j = 0; j < sizeX; j++)
                    {
                        double xDist = intX + j * 2.5;
                        double yDist = intY - i * 2.5;

                        if (!IsWithinJaws(xDist, yDist, jawsPosition)) continue;

                        VVector gridCoor = new VVector(xDist, yDist, 0);
                        VVector gridCoorCt = coorConversion(gantryAngle, colAngle, psaAngle, new VVector[] { gridCoor })[0];
                        gridCoorCt.x *= proneConversion;
                        gridCoorCt.y *= proneConversion;
                        gridCoorCt += beamIso;

                        VVector ray = gridCoorCt - source;
                        ray.ScaleToUnitLength();

                        dose[j] = doseTracing(ray, source, body, ct, dPlan);
                    }
                    doseList.Add(dose);
                }
            }
            public static void AdjustMLCSegment(float[,] lpTarget, float[,] lpBase, int segmentIndex, int nLeafs, double[] boundary, double y1, double y2, double int_y,
            double int_x, List<double[]> doseMap, double[] doseThreshold, bool Rside)
            {
                for (int i = 0; i < nLeafs; i++)
                {
                    double yCoor = (boundary[i] + boundary[i + 1]) / 2;

                    if (yCoor < y1 || yCoor > y2)
                        continue;

                    int rowIdx = (int)Math.Round((int_y - yCoor) / 2.5);
                    int leftIdx, rightIdx;
                    int bodyLeftIdx = 1, bodyRightIdx = doseMap[0].Count();

                    double[] tmpDose = new double[doseMap[rowIdx].Count()];
                    for (int j = 0; j < doseMap[0].Count(); j++)
                    {
                        tmpDose[j] = doseMap[rowIdx][j];
                    }

                    for (int j = 0; j < doseMap[0].Count(); j++)
                    {
                        if (tmpDose[j] > 25)
                        {
                            bodyLeftIdx = j;
                            break;
                        }
                    }

                    for (int j = doseMap[0].Count() - 1; j >= 0; j--)
                    {
                        if (tmpDose[j] > 25)
                        {
                            bodyRightIdx = j;
                            break;
                        }
                    }

                    if (tmpDose.Max() < doseThreshold[segmentIndex] && bodyLeftIdx != 1 && bodyRightIdx != doseMap[0].Count())
                    {
                        if (Rside)
                        {
                            lpTarget[0, i] = (float)(int_x + bodyLeftIdx * 2.5);
                            lpTarget[1, i] = lpBase[1, i];
                        }
                        else
                        {
                            lpTarget[0, i] = lpBase[0, i];
                            lpTarget[1, i] = (float)(int_x + bodyRightIdx * 2.5);
                        }
                        continue;
                    }

                    if (tmpDose.Max() >= doseThreshold[segmentIndex])
                    {
                        if (Rside)
                        {
                            if (segmentIndex == 0)
                            {
                                for (int j = 0; j < doseMap[0].Count() - 1; j++)
                                {
                                    if (doseMap[rowIdx][j] >= doseThreshold[segmentIndex])
                                    {
                                        for (int k = j; k < doseMap[0].Count() - 1; k++)
                                        {
                                            if (doseMap[rowIdx][k] < doseThreshold[segmentIndex])
                                            {
                                                rightIdx = k;
                                                lpTarget[0, i] = (float)(int_x + rightIdx * 2.5);
                                                lpTarget[1, i] = lpBase[1, i];
                                                break;
                                            }
                                        }
                                        break;
                                    }
                                }
                            }
                            else
                            {
                                for (int j = doseMap[0].Count() - 1; j > 0; j--)
                                {
                                    if (doseMap[rowIdx][j] >= doseThreshold[segmentIndex])
                                    {
                                        rightIdx = j;
                                        lpTarget[0, i] = (float)(int_x + rightIdx * 2.5);
                                        lpTarget[1, i] = lpBase[1, i];
                                        break;
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (segmentIndex == 0)
                            {
                                for (int j = doseMap[0].Count() - 1; j > 0; j--)
                                {
                                    if (doseMap[rowIdx][j] >= doseThreshold[segmentIndex])
                                    {
                                        for (int k = j; k > 0; k--)
                                        {
                                            if (doseMap[rowIdx][k] < doseThreshold[segmentIndex])
                                            {
                                                leftIdx = k;
                                                lpTarget[0, i] = lpBase[0, i];
                                                lpTarget[1, i] = (float)(int_x + leftIdx * 2.5);
                                                break;
                                            }
                                        }
                                        break;
                                    }
                                }
                            }
                            else
                            {
                                for (int j = 0; j < doseMap[0].Count() - 1; j++)
                                {
                                    if (doseMap[rowIdx][j] >= doseThreshold[segmentIndex])
                                    {
                                        leftIdx = j;
                                        lpTarget[0, i] = lpBase[0, i];
                                        lpTarget[1, i] = (float)(int_x + leftIdx * 2.5);
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            public static void AdjustMLCSegmentMed(float[,] LPMed, float[,] LPMedBase, int segmentIndex, int nLeafs, double[] boundary, double med_y1, double med_y2,
                              double med_int_y, double med_int_x, List<double[]> doseM, double[] doseThreshold, bool Rside)
            {
                for (int i = 0; i < nLeafs; i++)
                {
                    double yCoor = (boundary[i] + boundary[i + 1]) / 2;

                    if (yCoor < med_y1 || yCoor > med_y2)
                        continue;

                    int rowIdx = (int)Math.Round((med_int_y - yCoor) / 2.5);
                    int leftIdx, rightIdx;
                    int bodyLeftIdx = 1, bodyRightIdx = doseM[0].Count(); //right side start with far right/last index

                    //double[] tmpDose = doseM[rowIdx].ToArray();
                    double[] tmpDose = new double[doseM[rowIdx].Count()];
                    for (int j = 0; j < doseM[0].Count(); j++)
                    {
                        tmpDose[j] = doseM[rowIdx][j];
                    }
                    for (int j = 0; j < doseM[0].Count(); j++)
                    {
                        if (tmpDose[j] > 25) // Identificar corpo
                        {
                            bodyLeftIdx = j;
                            break;
                        }
                    }
                    for (int j = doseM[0].Count() - 1; j >= 0; j--)
                    {
                        if (tmpDose[j] > 25)
                        {
                            bodyRightIdx = j;
                            break;
                        }
                    }

                    if (tmpDose.Max() < doseThreshold[segmentIndex] && bodyLeftIdx != 1 && bodyRightIdx != doseM[0].Count())
                    {
                        if (Rside)
                        {
                            LPMed[0, i] = (float)(med_int_x + bodyLeftIdx * 2.5);
                            LPMed[1, i] = LPMedBase[1, i];
                        }
                        else
                        {
                            LPMed[0, i] = LPMedBase[0, i];
                            LPMed[1, i] = (float)(med_int_x + bodyRightIdx * 2.5);
                        }
                        continue;
                    }

                    if (tmpDose.Max() >= doseThreshold[segmentIndex])
                    {
                        if (Rside)
                        {
                            if (segmentIndex == 0)
                            {
                                for (int j = 0; j < doseM[0].Count() - 1; j++) // Scan left bank
                                {
                                    if (doseM[rowIdx][j] >= doseThreshold[segmentIndex])
                                    {
                                        for (int k = j; k < doseM[0].Count() - 1; k++)
                                        {
                                            if (doseM[rowIdx][k] < doseThreshold[segmentIndex])
                                            {
                                                rightIdx = k;
                                                LPMed[0, i] = (float)(med_int_x + rightIdx * 2.5);
                                                LPMed[1, i] = LPMedBase[1, i];
                                                break;
                                            }
                                        }
                                        break;
                                    }
                                }
                            }
                            else
                            {
                                for (int j = doseM[0].Count() - 1; j > 0; j--) // Scan right bank
                                {
                                    if (doseM[rowIdx][j] >= doseThreshold[segmentIndex])
                                    {
                                        rightIdx = j;
                                        LPMed[0, i] = (float)(med_int_x + rightIdx * 2.5);//fit to right hand side of 105% hotspot
                                        LPMed[1, i] = LPMedBase[1, i];
                                        break;
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (segmentIndex == 0)
                            {
                                for (int j = doseM[0].Count() - 1; j > 0; j--) // Scan right bank
                                {
                                    if (doseM[rowIdx][j] >= doseThreshold[segmentIndex])
                                    {
                                        for (int k = j; k > 0; k--)
                                        {
                                            if (doseM[rowIdx][k] < doseThreshold[segmentIndex])
                                            {
                                                leftIdx = k;
                                                LPMed[0, i] = LPMedBase[0, i];
                                                LPMed[1, i] = (float)(med_int_x + leftIdx * 2.5);
                                                break;
                                            }
                                        }
                                        break;
                                    }
                                }
                            }
                            else
                            {
                                for (int j = 0; j < doseM[0].Count() - 1; j++) // Scan left bank
                                {
                                    if (doseM[rowIdx][j] >= doseThreshold[segmentIndex])
                                    {
                                        leftIdx = j;
                                        LPMed[0, i] = LPMedBase[0, i];
                                        LPMed[1, i] = (float)(med_int_x + leftIdx * 2.5);
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            public static void NewSmoothMLCSegment(float[,] lpSegment, int leafBankIndex, int nLeafs)
            {
                float[] mlcBuffer = new float[nLeafs];

                // Copia os valores da folha (linha) especificada para um buffer
                for (int i = 0; i < nLeafs; i++)
                    mlcBuffer[i] = lpSegment[leafBankIndex, i];

                // Aplica suavização com janela de 5 posições (2 para cada lado)
                for (int i = 2; i < nLeafs - 2; i++)
                {
                    if (mlcBuffer[i] == 0)
                        continue;

                    int smoothCount = 0;
                    float smoothSum = 0;

                    for (int j = i - 2; j <= i + 2; j++)
                    {
                        if (mlcBuffer[j] != 0)
                        {
                            smoothSum += mlcBuffer[j];
                            smoothCount++;
                        }
                    }

                    lpSegment[leafBankIndex, i] = smoothSum / smoothCount;
                }
            }

            public static void SmoothMLCSegment(float[,] LPMed, int segmentIndex, int nLeafs)
            {
                float[] mlcBuffer = new float[nLeafs];

                // Copia os valores do segmento específico para um buffer temporário
                for (int i = 0; i < nLeafs; i++)
                    mlcBuffer[i] = LPMed[segmentIndex, i];

                // Aplica suavização
                for (int i = 2; i < nLeafs - 2; i++)
                {
                    if (mlcBuffer[i] == 0)
                        continue;

                    int smoothCount = 0;
                    float smoothSum = 0;

                    for (int j = i - 2; j <= i + 2; j++)
                    {
                        if (mlcBuffer[j] != 0)
                        {
                            smoothSum += mlcBuffer[j];
                            smoothCount++;
                        }
                    }

                    LPMed[segmentIndex, i] = smoothSum / smoothCount;
                }
            }
            public static int FindActiveRow(float[,] LPLat, int nLeafs, bool findFirst)
            {
                if (findFirst) // Busca o primeiro índice válido
                {
                    for (int i = 0; i < nLeafs; i++)
                    {
                        if (LPLat[0, i] != 0 && LPLat[1, i] != 0)
                        {
                            //System.Windows.MessageBox.Show(i.ToString());
                            return i; // Retorna o primeiro índice encontrado
                        }
                    }
                }
                else // Busca o último índice válido
                {
                    for (int i = nLeafs - 1; i >= 0; i--)
                    {
                        if (LPLat[0, i] != 0 && LPLat[1, i] != 0)
                        {
                            //System.Windows.MessageBox.Show(i.ToString());
                            return i; // Retorna o último índice encontrado
                        }
                    }
                }

                return findFirst ? 0 : nLeafs - 1; // Retorna um valor padrão caso não encontre
            }
            public static void ExtendOpenMLC(float[,] LPLat, int nLeafs, double[] boundary, double lat_y1, double lat_y2, int activeRowY1, int activeRowY2)
            {
                for (int i = 0; i < nLeafs; i++)
                {
                    if (boundary[i + 1] > lat_y1 && boundary[i] < lat_y2 && LPLat[0, i] == 0 && LPLat[1, i] == 0)
                    {
                        if (i < activeRowY1) // Inferior closed MLC
                        {
                            LPLat[0, i] = LPLat[0, activeRowY1];
                            LPLat[1, i] = LPLat[1, activeRowY1];
                        }
                        else
                        {
                            LPLat[0, i] = LPLat[0, activeRowY2];
                            LPLat[1, i] = LPLat[1, activeRowY2];
                        }
                    }
                }
            }
            public static void CreateBeams(ExternalPlanSetup newPlan, int subFields, ExternalBeamMachineParameters beamPara,
                         float[][,] LPMed, float[][,] LPLat, VRect<double> jawsPositionMed,
                         VRect<double> jawsPositionLat, double colAngleMed, double colAngleLat,
                         double gantryAngleMed, double gantryAngleLat, double psaAngleMed,
                         double psaAngleLat, VVector iso, double subFieldWeight, double latCorrection,
                         bool isHal)
            {
                Beam[] medBeams = new Beam[subFields];
                Beam[] latBeams = new Beam[subFields];
                VMS.TPS.Common.Model.API.BeamParameters[] medBeamParams = new VMS.TPS.Common.Model.API.BeamParameters[subFields];
                VMS.TPS.Common.Model.API.BeamParameters[] latBeamParams = new VMS.TPS.Common.Model.API.BeamParameters[subFields];

                for (int i = 0; i < subFields; i++)
                {
                    if (isHal)
                    {
                        if (LPMed != null)
                    {
                        try
                        {
                            //adiciona os campos do hal
                            medBeams[i] = newPlan.AddFixedSequenceBeam(beamPara, colAngleMed, gantryAngleMed, iso);
                            medBeams[i].RemoveFlatteningSequence();

                            //seta as lps a posteriori
                            var editables = medBeams[i].GetEditableParameters();
                                
                                // Set jaw positions for Halcyon (required for proper MLC operation)
                                editables.SetJawPositions(jawsPositionMed);
                                
                                // Set leaf positions BEFORE setting weight
                                editables.SetAllLeafPositions(LPMed[i]);
                                
                                // Set weight factor last
                            editables.WeightFactor = subFieldWeight;
                                
                            medBeams[i].ApplyParameters(editables);

                                // Set beam ID for identification
                                medBeams[i].Id = $"3{i+2} MED";
                        }
                            catch (Exception ex)
                            {
                                // Provide error feedback instead of silent failure
                                MessageBox.Show($"Error creating Halcyon MED subfield {i+1}: {ex.Message}");
                            }
                        }
                        if (LPLat != null)
                        {
                        try
                        {
                            //adiciona os campos do hal
                            latBeams[i] = newPlan.AddFixedSequenceBeam(beamPara, colAngleLat, gantryAngleLat, iso);
                            latBeams[i].RemoveFlatteningSequence();

                            //seta as lps a posteriori
                            var editables = latBeams[i].GetEditableParameters();
                                
                                // Set jaw positions for Halcyon (required for proper MLC operation)
                                editables.SetJawPositions(jawsPositionLat);
                                
                                // Set leaf positions BEFORE setting weight
                                editables.SetAllLeafPositions(LPLat[i]);
                                
                                // Set weight factor last
                            editables.WeightFactor = subFieldWeight * latCorrection;
                                
                            latBeams[i].ApplyParameters(editables);

                                // Set beam ID for identification
                                latBeams[i].Id = $"4{i+2} LAT";
                        }
                            catch (Exception ex)
                            {
                                // Provide error feedback instead of silent failure
                                MessageBox.Show($"Error creating Halcyon LAT subfield {i+1}: {ex.Message}");
                            }
                        }
                    }
                    else
                    {
                        if (LPMed != null)
                    {
                        medBeams[i] = newPlan.AddMLCBeam(beamPara, LPMed[i], jawsPositionMed, colAngleMed, gantryAngleMed, psaAngleMed, iso);

                        // Obter e ajustar parâmetros do feixe
                        medBeamParams[i] = medBeams[i].GetEditableParameters();
                        medBeamParams[i].WeightFactor = subFieldWeight;

                            // Aplicar parâmetros ajustados
                            medBeams[i].ApplyParameters(medBeamParams[i]);
                        }
                        if (LPLat != null)
                        {
                            latBeams[i] = newPlan.AddMLCBeam(beamPara, LPLat[i], jawsPositionLat, colAngleLat, gantryAngleLat, psaAngleLat, iso);

                        latBeamParams[i] = latBeams[i].GetEditableParameters();
                        latBeamParams[i].WeightFactor = subFieldWeight * latCorrection;

                        latBeams[i].ApplyParameters(latBeamParams[i]);
                    }
                }
            }
            }
        }

        //convert the coordinate in BEV to the coordinate in CT 3D
        public static VVector[] coorConversion(double gantry, double collimator, double couch, VVector[] drrCoor)
        {
            VVector[] ctCoor = new VVector[drrCoor.Length];
            double tempX = 0, tempY = 0, tempZ = 0;
            for (int i = 0; i < drrCoor.Length; i++)
            {
                tempX = drrCoor[i].x * Math.Cos(gantry * Constants.pi / 180) * Math.Cos(collimator * Constants.pi / 180) - drrCoor[i].y * Math.Cos(gantry * Constants.pi / 180) * Math.Sin(collimator * Constants.pi / 180);
                tempY = drrCoor[i].x * Math.Sin(gantry * Constants.pi / 180) * Math.Cos(collimator * Constants.pi / 180) - drrCoor[i].y * Math.Sin(gantry * Constants.pi / 180) * Math.Sin(collimator * Constants.pi / 180);
                tempZ = drrCoor[i].x * Math.Sin(collimator * Constants.pi / 180) + drrCoor[i].y * Math.Cos(collimator * Constants.pi / 180);
                ctCoor[i].x = tempX * Math.Cos(couch * Constants.pi / 180) + tempZ * Math.Sin(couch * Constants.pi / 180);
                ctCoor[i].y = tempY;
                ctCoor[i].z = -tempX * Math.Sin(couch * Constants.pi / 180) + tempZ * Math.Cos(couch * Constants.pi / 180);
            }
            return ctCoor;
        }

        //---------search structure with a given name
        public static Structure find_structure(StructureSet st_set, bool match, params string[] names)
        {
            Structure st = null;
            foreach (string name in names)
                foreach (Structure s in st_set.Structures)
                    if (match)
                    {
                        if (Regex.Match(s.Id.ToLower(), name).Success && !s.Id.ToLower().Contains("in") && !s.Id.ToLower().Contains("mm") && !s.Id.ToLower().Contains("bil") && !s.Id.ToLower().Contains("crop") && !s.Id.ToLower().Contains("ptv"))
                        {
                            return s;
                        }
                    }
            foreach (string name in names)
                foreach (Structure s in st_set.Structures)
                    if (match)
                    {
                        if (Regex.Match(s.Id.ToLower(), name).Success && !s.Id.ToLower().Contains("in") && !s.Id.ToLower().Contains("mm") && !s.Id.ToLower().Contains("bil") && !s.Id.ToLower().Contains("crop"))
                        {
                            return s;
                        }
                    }
            foreach (string name in names)
                foreach (Structure s in st_set.Structures)
                    if (s.Id.ToLower().Contains(name))
                        return s;

            return st;
        }
        public static double[] rayTracing(VVector ray, VVector source, Structure body, Image ct)
        {
            double[] rayParameter = new double[3];//intensity, centroid, depth
            bool[] inBodyFlag = new bool[101];
            double[] buffer = new double[101];
            ImageProfile ip = ct.GetImageProfile(source + 500 * ray, source + 1500 * ray, buffer);
            List<int> centroidTrack = new List<int>();//centroid of penetration tissue, used for intensity adjustment
            for (int i = 0; i < 101; i++)
            {
                if (body.IsPointInsideSegment(source + (500 + 10 * i) * ray))
                    inBodyFlag[i] = true;
                else
                    inBodyFlag[i] = false;
            }
            for (int i = 0; i < 100; i++)
            {
                if (inBodyFlag[i] & inBodyFlag[i + 1])
                {
                    rayParameter[2] += 10;//depth
                    rayParameter[0] += (ip[i].Value + ip[i + 1].Value) * 5 + 1000 * 10;//intensity
                    for (int j = 0; j < 10; j++)
                        centroidTrack.Add(10 * i + j);
                }

                else if ((inBodyFlag[i] & !inBodyFlag[i + 1]) | (!inBodyFlag[i] & inBodyFlag[i + 1]))
                {
                    for (int j = 0; j < 10; j++)
                        if (body.IsPointInsideSegment(source + (500 + 10 * i + j) * ray))
                        {
                            double[] buffer2 = new double[1];
                            ImageProfile ip2 = ct.GetImageProfile(source + (500 + 10 * i + j) * ray, source + (500 + 10 * i + j) * ray, buffer2);
                            rayParameter[2] += 1;//depth
                            rayParameter[0] += ip2[0].Value + 1000;//intensity
                            centroidTrack.Add(10 * i + j);
                        }
                }
            }
            if (centroidTrack.Count() > 0)
                rayParameter[1] = centroidTrack.Sum() / centroidTrack.Count() + 500;
            else
                rayParameter[1] = 1000;
            return rayParameter;
        }
        public static double doseTracing(VVector ray, VVector source, Structure body, Image ct, Dose dose)
        {
            double[] buffer = new double[261];
            double maxDose = 0;

            DoseProfile dp = dose.GetDoseProfile(source + 850 * ray, source + 1100 * ray, buffer);
            for (int i = 0; i < 261; i++)
            {
                if (dp[i].Value > maxDose)
                    maxDose = dp[i].Value;
            }
            return maxDose;
        }
    }
    static class Constants
    {
        public const double pi = 3.1415926525;
    }
}