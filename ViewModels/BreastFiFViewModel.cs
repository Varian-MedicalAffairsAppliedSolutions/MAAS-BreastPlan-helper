using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.IO;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using System.Windows.Forms;
using System.Reflection;
using Microsoft.SqlServer.Server;
using Prism.Mvvm;
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
        
        private int _selectedSubFieldCount = 2; // Default to 5 subfields (index 2 = 5 subfields)
        public int SelectedSubFieldCount
        {
            get { return _selectedSubFieldCount; }
            set { SetProperty(ref _selectedSubFieldCount, value); }
        }

        public BreastFiFViewModel(EsapiWorker esapiWorker, SettingsClass settings)
        {
            _esapiWorker = esapiWorker;
            _settings = settings;
            
            ExecuteCommand = new DelegateCommand(Execute, CanExecute);
            StatusMessage = "Ready";
        }

        private bool CanExecute()
        {
            return _esapiWorker.GetValue(sc => sc.PlanSetup) != null;
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

            // Use the selected subfield count from our ViewModel
            int subFields = SelectedSubFieldCount + 3; // Convert from 0-3 index to 3-6 value

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


            double[] doseThreshold = new double[5];// dose level of MLC to block against, the max number of segment minus one
            doseThreshold[0] = 111;//isodose level to block against
            doseThreshold[1] = 108;
            doseThreshold[2] = 105;
            doseThreshold[3] = 102;
            doseThreshold[4] = 98;
            switch (subFields)//override threshold based on subfield numbers
            {
                case 3:
                    doseThreshold[0] = 107;
                    doseThreshold[1] = 103;
                    break;

                case 4:
                    doseThreshold[0] = 110;
                    doseThreshold[1] = 105;
                    doseThreshold[2] = 98;
                    break;
                case 5:
                    doseThreshold[0] = 111;
                    doseThreshold[1] = 107;
                    doseThreshold[2] = 103;
                    doseThreshold[3] = 98;
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

                //seta as lps a posteriori
                var editables = newMed.GetEditableParameters();
                editables.SetAllLeafPositions(originalMed.ControlPoints[0].LeafPositions);
                newMed.ApplyParameters(editables);

                editables = newLat.GetEditableParameters();
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
            for (int u = 0; u < newPlan.Beams.Count(); u++)
            {
                Beam ib = newPlan.Beams.ElementAt(u);

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
                    lat0BeamParameter.WeightFactor = 0.5 * latCorrection; // apply lateral correction
                    lat0.ApplyParameters(lat0BeamParameter);
                }
            }

            newPlan.SetCalculationModel(CalculationType.PhotonVolumeDose, plExt.PhotonCalculationModel);
            newPlan.CalculateDose();// calculate dose to assess initial dose distribution
            maxDose = newPlan.Dose.DoseMax3D.Dose;
            newPlan.PlanNormalizationValue = 100 * maxDose / 115;//this is in percentage; normalize to 115% max dose

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


            ////add segment MLC Med beam

            BeamProcessor.AdjustMLCSegmentMed(LPMed1, LPMed, 0, nLeafs, boundary, med_y1, med_y2, med_int_y, med_int_x, doseM, doseThreshold, Rside);
            BeamProcessor.AdjustMLCSegmentMed(LPMed2, LPMed, 1, nLeafs, boundary, med_y1, med_y2, med_int_y, med_int_x, doseM, doseThreshold, Rside);
            BeamProcessor.AdjustMLCSegmentMed(LPMed3, LPMed, 2, nLeafs, boundary, med_y1, med_y2, med_int_y, med_int_x, doseM, doseThreshold, Rside);
            BeamProcessor.AdjustMLCSegmentMed(LPMed4, LPMed, 3, nLeafs, boundary, med_y1, med_y2, med_int_y, med_int_x, doseM, doseThreshold, Rside);
            BeamProcessor.AdjustMLCSegmentMed(LPMed5, LPMed, 4, nLeafs, boundary, med_y1, med_y2, med_int_y, med_int_x, doseM, doseThreshold, Rside);


            /////////////////////////////////////
            /////smooth MLC
            /////////////////////////////////////

            if (Rside)
            {
                BeamProcessor.SmoothMLCSegment(LPMed1, 0, nLeafs);
                BeamProcessor.SmoothMLCSegment(LPMed2, 0, nLeafs);
                BeamProcessor.SmoothMLCSegment(LPMed3, 0, nLeafs);
                BeamProcessor.SmoothMLCSegment(LPMed4, 0, nLeafs);
                BeamProcessor.SmoothMLCSegment(LPMed5, 0, nLeafs);
            }
            else // Left side
            {
                BeamProcessor.SmoothMLCSegment(LPMed1, 1, nLeafs);
                BeamProcessor.SmoothMLCSegment(LPMed2, 1, nLeafs);
                BeamProcessor.SmoothMLCSegment(LPMed3, 1, nLeafs);
                BeamProcessor.SmoothMLCSegment(LPMed4, 1, nLeafs);
                BeamProcessor.SmoothMLCSegment(LPMed5, 1, nLeafs);
            }


            ////mirror lat beam MLC from MED
            for (int i = 0; i < nLeafs; i++)
            {
                LPLat1[0, i] = (-1) * LPMed1[1, i];
                LPLat1[1, i] = (-1) * LPMed1[0, i];
                LPLat2[0, i] = (-1) * LPMed2[1, i];
                LPLat2[1, i] = (-1) * LPMed2[0, i];
                LPLat3[0, i] = (-1) * LPMed3[1, i];
                LPLat3[1, i] = (-1) * LPMed3[0, i];
                LPLat4[0, i] = (-1) * LPMed4[1, i];
                LPLat4[1, i] = (-1) * LPMed4[0, i];
                LPLat5[0, i] = (-1) * LPMed5[1, i];
                LPLat5[1, i] = (-1) * LPMed5[0, i];
            }


            //post process lateral field MLC from mirroring med due to field size difference
            bool isHal = machineID.ToLower().Contains("hal") || machineID.ToLower().Contains("rds");

            if (!isHal)
            {
                int activeRowLat1Y1 = BeamProcessor.FindActiveRow(LPLat1, nLeafs, true);
                int activeRowLat2Y1 = BeamProcessor.FindActiveRow(LPLat2, nLeafs, true);
                int activeRowLat3Y1 = BeamProcessor.FindActiveRow(LPLat3, nLeafs, true);
                int activeRowLat4Y1 = BeamProcessor.FindActiveRow(LPLat4, nLeafs, true);
                int activeRowLat5Y1 = BeamProcessor.FindActiveRow(LPLat5, nLeafs, true);

                int activeRowLat1Y2 = BeamProcessor.FindActiveRow(LPLat1, nLeafs, false);
                int activeRowLat2Y2 = BeamProcessor.FindActiveRow(LPLat2, nLeafs, false);
                int activeRowLat3Y2 = BeamProcessor.FindActiveRow(LPLat3, nLeafs, false);
                int activeRowLat4Y2 = BeamProcessor.FindActiveRow(LPLat4, nLeafs, false);
                int activeRowLat5Y2 = BeamProcessor.FindActiveRow(LPLat5, nLeafs, false);


                //extend open MLC
                BeamProcessor.ExtendOpenMLC(LPLat1, nLeafs, boundary, lat_y1, lat_y2, activeRowLat1Y1, activeRowLat1Y2);
                BeamProcessor.ExtendOpenMLC(LPLat2, nLeafs, boundary, lat_y1, lat_y2, activeRowLat2Y1, activeRowLat2Y2);
                BeamProcessor.ExtendOpenMLC(LPLat3, nLeafs, boundary, lat_y1, lat_y2, activeRowLat3Y1, activeRowLat3Y2);
                BeamProcessor.ExtendOpenMLC(LPLat4, nLeafs, boundary, lat_y1, lat_y2, activeRowLat4Y1, activeRowLat4Y2);
                BeamProcessor.ExtendOpenMLC(LPLat5, nLeafs, boundary, lat_y1, lat_y2, activeRowLat5Y1, activeRowLat5Y2);
            }

            //BeamParameters med1BeamParameter, lat1BeamParameter, med2BeamParameter, lat2BeamParameter, med3BeamParameter, lat3BeamParameter, med4BeamParameter, lat4BeamParameter, med5BeamParameter, lat5BeamParameter;
            double subFieldWeight = 0.015;

            // Arrays de referência para os feixes
            float[][,] LPMedTot = { LPMed1, LPMed2, LPMed3, LPMed4, LPMed5 };
            float[][,] LPLatTot = { LPLat1, LPLat2, LPLat3, LPLat4, LPLat5 };

            //valores de referencias para campos
            VRect<double> jawsPositionMed = originalMed.ControlPoints[0].JawPositions;
            VRect<double> jawsPositionLat = originalLat.ControlPoints[0].JawPositions;
            double colAngleMed = originalMed.ControlPoints[0].CollimatorAngle, gantryAngleMed = originalMed.ControlPoints[0].GantryAngle, psaAngleMed = originalMed.ControlPoints[0].PatientSupportAngle;
            double colAngleLat = originalLat.ControlPoints[0].CollimatorAngle, gantryAngleLat = originalLat.ControlPoints[0].GantryAngle, psaAngleLat = originalLat.ControlPoints[0].PatientSupportAngle;
            VVector iso = originalMed.IsocenterPosition;

            switch (subFields)
            {
                case 3:
                    subFieldWeight = 0.035;
                    BeamProcessor.CreateBeams(newPlan, 2, beamPara, LPMedTot, LPLatTot, jawsPositionMed, jawsPositionLat,
                                colAngleMed, colAngleLat, gantryAngleMed, gantryAngleLat, psaAngleMed,
                                psaAngleLat, iso, subFieldWeight, latCorrection, isHal);
                    break;

                case 4:
                    subFieldWeight = 0.025;
                    BeamProcessor.CreateBeams(newPlan, 3, beamPara, LPMedTot, LPLatTot, jawsPositionMed, jawsPositionLat,
                                colAngleMed, colAngleLat, gantryAngleMed, gantryAngleLat, psaAngleMed,
                                psaAngleLat, iso, subFieldWeight, latCorrection, isHal);
                    break;

                case 5:
                    subFieldWeight = 0.018;
                    BeamProcessor.CreateBeams(newPlan, 4, beamPara, LPMedTot, LPLatTot, jawsPositionMed, jawsPositionLat,
                                colAngleMed, colAngleLat, gantryAngleMed, gantryAngleLat, psaAngleMed,
                                psaAngleLat, iso, subFieldWeight, latCorrection, isHal);
                    break;

                case 6:
                    subFieldWeight = 0.015;
                    BeamProcessor.CreateBeams(newPlan, 5, beamPara, LPMedTot, LPLatTot, jawsPositionMed, jawsPositionLat,
                                colAngleMed, colAngleLat, gantryAngleMed, gantryAngleLat, psaAngleMed,
                                psaAngleLat, iso, subFieldWeight, latCorrection, isHal);
                    break;
            }

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
                        try
                        {
                            //adiciona os campos do hal
                            medBeams[i] = newPlan.AddFixedSequenceBeam(beamPara, colAngleMed, gantryAngleMed, iso);
                            medBeams[i].RemoveFlatteningSequence();

                            //seta as lps a posteriori
                            var editables = medBeams[i].GetEditableParameters();
                            editables.WeightFactor = subFieldWeight;
                            medBeams[i].ApplyParameters(editables);
                            editables.SetAllLeafPositions(LPMed[i]);
                            medBeams[i].ApplyParameters(editables);

                        }
                        catch { }
                        try
                        {
                            //adiciona os campos do hal
                            latBeams[i] = newPlan.AddFixedSequenceBeam(beamPara, colAngleLat, gantryAngleLat, iso);
                            latBeams[i].RemoveFlatteningSequence();

                            //seta as lps a posteriori
                            var editables = latBeams[i].GetEditableParameters();
                            editables.WeightFactor = subFieldWeight * latCorrection;
                            latBeams[i].ApplyParameters(editables);
                            editables.SetAllLeafPositions(LPLat[i]);
                            latBeams[i].ApplyParameters(editables);

                        }
                        catch { }
                    }
                    else
                    {
                        medBeams[i] = newPlan.AddMLCBeam(beamPara, LPMed[i], jawsPositionMed, colAngleMed, gantryAngleMed, psaAngleMed, iso);
                        latBeams[i] = newPlan.AddMLCBeam(beamPara, LPLat[i], jawsPositionLat, colAngleLat, gantryAngleLat, psaAngleLat, iso);

                        // Obter e ajustar parâmetros do feixe
                        medBeamParams[i] = medBeams[i].GetEditableParameters();
                        medBeamParams[i].WeightFactor = subFieldWeight;

                        latBeamParams[i] = latBeams[i].GetEditableParameters();
                        latBeamParams[i].WeightFactor = subFieldWeight * latCorrection;

                        // Aplicar parâmetros ajustados
                        medBeams[i].ApplyParameters(medBeamParams[i]);
                        latBeams[i].ApplyParameters(latBeamParams[i]);
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