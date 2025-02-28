using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.Remoting.Contexts;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Xml;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using Prism;
using Prism.Mvvm;
using System.Configuration;
using Prism.Commands;
using MAAS_BreastPlan_helper.MAAS_BreastPlan_helper;

namespace MAAS_BreastPlan_helper.ViewModels
{
    internal class BreastAutoDialogViewModel: BindableBase
    {
        private ScriptContext Context { get; set; }
        private StructureSet CopiedSS { get; set; }

        public DelegateCommand CbCustomPTV_Click { get; set; }
        public DelegateCommand BtnPlan_Click { get; set; }
        public ObservableCollection<string> StatusBoxItems { get; set; }

        public ObservableCollection<Structure> PTVItems { get; set; }   

        private string lmcText;

        public string LMCText
        {
            get { return lmcText; }
            set { SetProperty(ref lmcText, value); }
        }

        private ExternalPlanSetup Plan { get; set; }
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

        private string sepText;

        public string SepText
        {
            get { return sepText; }
            set { SetProperty(ref sepText, value); }
        }


        private PlanSetup selectedPlanSetup;
        public PlanSetup SelectedPlanSetup
        {
            get { return selectedPlanSetup; }
            set { SetProperty(ref selectedPlanSetup, value); }
        }

        private Energy selectedEnergy;

        public Energy SelectedEnergy
        {
            get { return selectedEnergy; }
            set { SetProperty(ref selectedEnergy, value); }
        }

        public ObservableCollection<Energy> Energies { get; set; }

        private Prescription selectedPrescription;

        public Prescription SelectedPrescription
        {
            get { return selectedPrescription; }
            set { SetProperty(ref selectedPrescription, value); }
        }

        public ObservableCollection<Prescription> Prescriptions { get; set; }

        private string selectedLungId;

        private string selectedPatientId;

        public string SelectedPatientId
        {
            get { return selectedPatientId; }
            set { SetProperty(ref selectedPatientId, value); }
        }

        private string selectedPTV;

        public string SelectedPTV
        {
            get { return selectedPTV; }
            set { SetProperty(ref selectedPTV, value); }
        }

        public  string SelectedLungId
        {
            get { return selectedLungId; }
            set { SetProperty(ref selectedLungId, value); }
        }

        public ObservableCollection<string> LungIds { get; set; }

        private string selectedHeartId;

        public string SelectedHeartId
        {
            get { return selectedHeartId; }
            set { SetProperty(ref selectedHeartId, value); }
        }
        public ObservableCollection<string> HeartIds { get; set; }

        private BreastSide selectedBreastSide;
        public BreastSide SelectedBreastSide
        {
            get { return selectedBreastSide; }
            set { SetProperty(ref selectedBreastSide, value); }
        }
        public ObservableCollection<BreastSide> BreastSides { get; set; }
        public ObservableCollection<PlanSetup> PlanSetups { get; set; }           
        enum SIDE { RIGHT, LEFT, ERROR };
        public class BreastSide
        {
            public BreastSide(StructureSet ss, string side, string lungId, AxisAlignedMargins margins)
            {
                Side = side;
                Margins = margins;
                try
                {
                    Lung = ss.Structures.Where(x => x.Id.ToUpper() == lungId.ToUpper()).ElementAt(0);
                }
                catch
                {
                    Lung = null;
                }
            }
            public Structure Lung { get; protected set; }
            public string Side { get; protected set; }
            public string LungId { get { return Lung?.Id; } }
            public AxisAlignedMargins Margins { get; protected set; }

            public override string ToString()
            {
                return Side;
            }
        }
        public class Energy
        {
            public int Index { get; set; } = 0;
            public double MeV { get; set; } = 0;
            public double MinSep { get; set; } = 0;
            public double MaxSep { get; set; } = double.MaxValue;
            public bool IsInRange(double sep)
            {
                return sep > MinSep && sep <= MaxSep;
            }
            public override string ToString()
            {
                return $"{MeV}X";
            }
        }
        public class Prescription
        {
            public int Fractions { get; set; }
            public double DoseCGy { get; set; }
            public double DosePerFraction { get { return DoseCGy / Fractions; } }
            public override string ToString()
            {
                return $"{DoseCGy:0} cGy in {Fractions} Fx";
            }
            public string ToString(bool all)
            {
                if (all)
                {
                    return $"{DoseCGy:0} cGy in {Fractions} Fx--->{DosePerFraction} cGy per fraction";
                }
                return ToString();
            }
        }
        struct MeshBounds   //  Struct representing the high and low slice indices of a structure.
        {
            public int Low;
            public int High;
        }

        public BreastAutoDialogViewModel(ScriptContext context, SettingsClass settings, EsapiWorker esapiWorker)
        {
            StatusBoxItems = new ObservableCollection<string>();
            PTVItems = new ObservableCollection<Structure>();
            CbCustomPTV_Click = new DelegateCommand(OnCustomPTV_Click);
            BtnPlan_Click = new DelegateCommand(OnBtnPlan_Click);
            LMCText = settings.LMCModel;

            double separation = 0;
            Context = context;
            Course course = context.Course;
            Plan = course.ExternalPlanSetups.FirstOrDefault(); ;
            //SS = Plan.StructureSet;
            StructureSet ss = Plan.StructureSet;

            SelectedPatientId = context.Patient.Id;

            //  Creating a list of plansetups in the course in context and attach it to cboPlanId Combobox
            var ps_list = course.PlanSetups.ToList();
            PlanSetups = new ObservableCollection<PlanSetup>();
            foreach(var ps in ps_list) { PlanSetups.Add(ps); }
            SelectedPlanSetup = PlanSetups.FirstOrDefault();

            /*
            if (ps_list.Count == 1)
            {
                cboPlanId.SelectedIndex = 0;
            }
            else
            {
                cboPlanId.SelectedItem = ps_list.FirstOrDefault(x => x == Plan.Id);
            }*/


            BreastSides = new ObservableCollection<BreastSide>()
            {
                new BreastSide(ss, "Right", "Lung_R", new AxisAlignedMargins(StructureMarginGeometry.Outer, 25, 25, 0, 0, 0, 0)),
                new BreastSide(ss, "Left", "Lung_L", new AxisAlignedMargins(StructureMarginGeometry.Outer, 0, 25, 0, 25, 0, 0))
            };

            SelectedBreastSide = BreastSides.FirstOrDefault();

            /*
            cboBreastSide.ItemsSource = brSide;
            if (cboPlanId.SelectedItem != null)
            {
                cboBreastSide.SelectedIndex = (int)FindTreatmentSide(Plan);
            }*/

            //Creating a list of lung Ids
            var tempL = ss.Structures.Where(x => x.Id.ToUpper().Contains("LUNG")).Select(x => x.Id).ToList();
            LungIds = new ObservableCollection<string>();
            foreach(var s in tempL)
            {
                LungIds.Add(s);
            }

            //Selecting default value for ipsilung combobox based on the side of the breast
            /*
            BreastSide side = cboBreastSide.SelectedItem as BreastSide;
            if (side.LungId != null)
            {
                cboIpsiLung.SelectedItem = side.LungId;
            }
            else
            {
                MessageBox.Show("Please select ipsi lateral lung Id");
            }*/


            //Creating a list of heart Ids
            var tempHearList = ss.Structures.Where(x => x.Id.ToUpper().Contains("HEART")).Select(x => x.Id).ToList();
            HeartIds= new ObservableCollection<string>();
            foreach(var s in tempHearList) { HeartIds.Add(s); }
            
            string heart = HeartIds.FirstOrDefault(x => x.ToUpper() == "HEART");

            /*
            if (heart != null)
            {
                cboHeart.SelectedItem = heart;
            }*/

            //  JAK: This cleans up the code further down, but retains ease of modification.
            Prescriptions = new ObservableCollection<Prescription>
            {
                new Prescription(){Fractions = 16, DoseCGy = 4250},
                new Prescription(){Fractions = 5, DoseCGy = 2600},
                new Prescription(){Fractions = 15, DoseCGy = 4000},
                new Prescription(){Fractions = 25, DoseCGy = 5000}
            };


            // Beam sepation calculation
            Structure external = Plan.StructureSet.Structures.FirstOrDefault(x => x.Id.ToUpper().Contains("BODY"));
            if (external == null)
            {
                external = Plan.StructureSet.Structures.FirstOrDefault(x => x.DicomType.ToUpper() == "EXTERNAL");
            }

            Beam b1 = Plan.Beams.ElementAt(0);
            Beam b2 = Plan.Beams.ElementAt(1);

            separation = ComputeBeamSeparation(b1, b2, external);
            SepText = Math.Round(separation / 10, 2).ToString();

            //Creating list of available beam energies
            //  JAK (2023-02-06): This will be easier to maintain.  If new separations/energies
            //  are required, they can be added here and it will require no extra coding.  The
            //  more extra coding required, the greater the chance for new bugs to arise.
            Energies = new ObservableCollection<Energy>()
            {
                new Energy(){ Index = 0, MeV = 6, MinSep = 0, MaxSep = 210 },
                new Energy(){ Index = 1, MeV = 10, MinSep = 210, MaxSep = 250 },
                new Energy(){ Index = 2, MeV = 15, MinSep = 250 }
            };

            //Assigning enery values to beam energy combobox.
            /*
            IEnumerable<int> selected = energies.Where(x => x.IsInRange(separation)).Select(x => x.Index);
            cboBeamEnergy.SelectedIndex = selected.ElementAt(0);*/

        }

        private Tuple<string, string> GetFluenceEnergyMode(Beam bm)
        {
            // Lifted from my python code @ craman96/MAAS
            var energy_mode_splits = bm.EnergyModeDisplayName.Split('-');

            var energy_mode_id = energy_mode_splits[0];

            var primary_fluence_mode = "";
            if (energy_mode_splits.Length > 1)
            {
                primary_fluence_mode = energy_mode_splits[1];
            }

            return new Tuple<string, string>(primary_fluence_mode, energy_mode_id);
        }

        //async is to allow the listbox in the GUI to update during script run time
        private async void OnBtnPlan_Click()
        {
            try
            {
                await UpdateListBox("Starting run...");
                //allow modifications to the patient plan.  (JAK, 2023-02-02: moved to the top for emphasis.)
                Context.Patient.BeginModifications();

                //await allows another process to run, in this case allows the GUI to update
                await UpdateListBox("Starting to Plan....");

                //get needed variables from eclipse
                Patient pt = Context.Patient;
                Course cou = Context.Course;
                ExternalPlanSetup plan = selectedPlanSetup as ExternalPlanSetup;
                // Structure set associated with the plan;
                StructureSet ss = plan.StructureSet;

                // Assert that plan has MLC
                if (plan.Beams.Where(b => !b.IsSetupField).First().MLC == null)
                {
                    await UpdateListBox("Original Plan must have fields MLCs. Returning.");
                    return;
                }

                OptimizationOptionsIMRT opt = new OptimizationOptionsIMRT(
                        1000,
                        OptimizationOption.RestartOptimization,
                        OptimizationConvergenceOption.TerminateIfConverged,
                        OptimizationIntermediateDoseOption.UseIntermediateDose,
                        plan.Beams.First().MLC.Id);


                // Get multiplier for dose and dose units
                var doseUnit = plan.TotalDose.Unit;
                double scalingFactor = 1;
                if (doseUnit == DoseValue.DoseUnit.Gy)
                {
                    scalingFactor /= 100;
                }
                await UpdateListBox($"Dose unit / scaling factor = {doseUnit.ToString()} / {scalingFactor}");

                var unpack_getFluenceEnergyMode = GetFluenceEnergyMode(plan.Beams.First());
                string primary_fluence_mode = unpack_getFluenceEnergyMode.Item1;
                string energy_mode_id = unpack_getFluenceEnergyMode.Item2;

                var machineParameters = new ExternalBeamMachineParameters(
                    plan.Beams.First().TreatmentUnit.Id,
                    energy_mode_id,
                    plan.Beams.First().DoseRate,
                    "STATIC",
                    primary_fluence_mode
                );

                //set the machine paramters
                /*ExternalBeamMachineParameters machineParameters = new ExternalBeamMachineParameters(
                    "TB_H", cboBeamEnergy.SelectedItem.ToString(), 600, "STATIC", string.Empty);*/

                await UpdateListBox("Creating copy of plan with slected beam energy....");
                //create a copy of the original plan by creating a new plan and copying beams from the original plan.
                ExternalPlanSetup copied_plan = cou.AddExternalPlanSetup(ss);
                CopiedSS = copied_plan.StructureSet;

                CopyBeams(plan, copied_plan, machineParameters);

                Prescription presc = SelectedPrescription;


                copied_plan.SetPrescription(presc.Fractions, new DoseValue(presc.DosePerFraction * scalingFactor, doseUnit), 1.0);


                Structure ptv = null;

                // Creating PTV if customized PTV option (check box) is not selected.
                if (customPTV)
                {
                    //Check if PTV is there
                    Structure checkIso50 = CopiedSS.Structures.FirstOrDefault(x => x.Id.ToUpper().Contains("PTV_BREAST") && x.DicomType == "CTV");
                    if (checkIso50 != null)
                    {
                        await UpdateListBox("PTV has already been created....");
                        ptv = CopiedSS.Structures.FirstOrDefault(x => x.Id.ToUpper() == "PTV_BREAST");

                    }
                    else
                    {

                        if (copied_plan.IsDoseValid)
                        {
                            await UpdateListBox("Creating PTV....");
                            //if dose is already calculated, skip the step and alert the user
                            ptv = CopiedSS.AddStructure("CTV", "PTV_Breast");
                            //Convert 50% isodose level to structure
                            ptv.ConvertDoseLevelToStructure(copied_plan.Dose, new DoseValue(50, DoseValue.DoseUnit.Percent));
                        }
                        else
                        {
                            //if does is not already calculated, calculate it and display progress
                            await UpdateListBox("Calculating Dose and creating PTV...");
                            copied_plan.SetCalculationModel(CalculationType.PhotonVolumeDose, plan.PhotonCalculationModel);
                            copied_plan.CalculateDose();

                            //Add PTV structure
                            ptv = CopiedSS.AddStructure("CTV", "PTV_Breast");
                            //Convert 50% isodose level to structure
                            ptv.ConvertDoseLevelToStructure(copied_plan.Dose, new DoseValue(50, DoseValue.DoseUnit.Percent));
                        }
                    }
                    // Use isotropic 3mm for PTV structure 
                    AxisAlignedMargins margins1 = new AxisAlignedMargins(StructureMarginGeometry.Inner, 3, 3, 3, 3, 3, 3);
                    ptv.SegmentVolume = ptv.AsymmetricMargin(margins1);
                }

                //Select customized PTV if cutomized PTV checkbox is checked 
                else if (CustomPTV && SelectedPTV != null)
                {
                    await UpdateListBox("Using customized PTV...");
                    ptv = CopiedSS.Structures.FirstOrDefault(x => x.Id == SelectedPTV);
                }
                else
                {
                    MessageBox.Show("Please select a valid PTV or close the window and create one.");
                }


                //Create 2.5 cm expansion on PTV in anterio-lateral directions to include flash;

                Structure expandPTV = CopiedSS.Structures.FirstOrDefault(x => x.Id == "PTV_Expanded" && x.DicomType == "PTV");
                try
                {
                    if (expandPTV == null)
                    {
                        expandPTV = await CreateExpandedPTV(ptv, ss, copied_plan);
                    }
                    else
                    {
                        await UpdateListBox("Extended PTV has already been created....");
                    }
                }
                catch (Exception ex)
                {
                    _ = MessageBox.Show("Body structure cannot be edited\n" + ex.ToString());
                    return;
                }

                //store the ipsilateral lung
                Structure ipsi_lung = CopiedSS.Structures.FirstOrDefault(x => x.Id == SelectedLungId);
                SpareLungHeart(ptv, ipsi_lung);

                Optimize(opt, plan, copied_plan, ptv, expandPTV, ipsi_lung, scalingFactor, doseUnit);

                await UpdateListBox($"Calculating Leaf Motions with {LMCText}");
                copied_plan.SetCalculationModel(CalculationType.PhotonLeafMotions, LMCText);

                //  Calculate the leaf motions
                await UpdateListBox("Calculating Leaf Motions....");
                try
                {
                    //Use fixed jaws option for leaf motion calculator
                    copied_plan.CalculateLeafMotions(new LMCVOptions(true));
                }
                catch
                {
                    MessageBox.Show("\n Leaf motion calc unsuccessfull");
                    return;
                }

                //  Re-calculate dose
                await UpdateListBox("Calculating Dose....");
                copied_plan.CalculateDose();


                await UpdateListBox("Creating Hot and Cold Spots....");


                Structure coldSpot100 = CopiedSS.AddStructure("DOSE_REGION", "coldSpot100");
                coldSpot100.ConvertDoseLevelToStructure(copied_plan.Dose, new DoseValue(100, DoseValue.DoseUnit.Percent));
                coldSpot100.SegmentVolume = ptv.Sub(coldSpot100.SegmentVolume);

                Structure hotSpot105 = CopiedSS.AddStructure("DOSE_REGION", "hotSpot105");
                hotSpot105.ConvertDoseLevelToStructure(copied_plan.Dose, new DoseValue(105, DoseValue.DoseUnit.Percent));

                await UpdateListBox("coldspot subtraction complete");



                OptimizationSetup optSet = copied_plan.OptimizationSetup;
                await UpdateListBox("Opt setup complete");

                double recievedPresc = (int)copied_plan.NumberOfFractions * copied_plan.DosePerFraction.Dose;

                await UpdateListBox("623");


                if (hotSpot105.Volume > 0)
                {
                    await UpdateListBox("628");
                    optSet.AddPointObjective(hotSpot105, OptimizationObjectiveOperator.Upper,
                    new DoseValue(1.03 * recievedPresc, doseUnit), 0, 45);
                    await UpdateListBox("631");
                }
                await UpdateListBox($"coldspot vol {coldSpot100.Volume}");
                if (coldSpot100.Volume > 0)
                {
                    await UpdateListBox("635");
                    optSet.AddPointObjective(coldSpot100, OptimizationObjectiveOperator.Lower,
                    new DoseValue(0.98 * recievedPresc, doseUnit), 100, 20);
                    await UpdateListBox("638");
                }

                //if there is any objectives
                if (copied_plan.OptimizationSetup.Objectives != null)
                {
                    await UpdateListBox("Re-optimizing....");
                    //optimize plan again


                    copied_plan.Optimize(opt);
                    await UpdateListBox("Re-calculating Leaf Motions....");
                    //re-calculate leaf motions after optimization
                    copied_plan.SetCalculationModel(CalculationType.PhotonLeafMotions, LMCText);

                    try
                    {
                        //Use fixed jaws option for leaf motion calculator
                        copied_plan.CalculateLeafMotions(new LMCVOptions(true));
                    }
                    catch
                    {
                        MessageBox.Show("\n Leaf motion calc unsuccessfull");
                    }

                    await UpdateListBox("Re-calculating Dose...");
                    //re-calculate dose after optimization
                    copied_plan.CalculateDose();
                }


                //Name the new plan as SW_PhotonEnergy
                string pId = "SW_" + SelectedEnergy;
                //Check if plan with existing id exists of not.
                ExternalPlanSetup plansetup = cou.ExternalPlanSetups.FirstOrDefault(x => x.Id == pId);
                //If plan does not exist already
                if (plansetup == null)
                {
                    copied_plan.Id = pId;
                }
                //If plan exists
                else
                {
                    //copied_plan.Id = pId;
                    var ps_test = cou.ExternalPlanSetups.ToList();
                    List<string> Id_list = new List<string> { };
                    foreach (var ps in ps_test)
                    {
                        Id_list.Add(ps.Id);
                    }
                    //if the length of the ID is less than 6, add "_SW_" to the original ID
                    string match = Id_list.Find(x => x == plansetup.Id);
                    int ii = 1;
                    while (match != null)
                    {
                        copied_plan.Id = "SW_" + SelectedEnergy + "_" + ii.ToString();
                        match = Id_list.Find(x => x == copied_plan.Id);
                        ii++;
                    }
                }


                //Display the finishing messages
                await UpdateListBox("Planning is done...");

            }
            catch (Exception ex)
            {
                _ = MessageBox.Show($"Error creating plan!\n{ex.Message}");
                return;
            }
        }

        // Checkbox to select customized PTV created by the user.
        private void OnCustomPTV_Click()
        {
            if (customPTV)
            {
                CBOPTVEnabled = true;
                LBLPTVEnabled= true;
              
                PTVItems.Clear();
                foreach(var ps in Context.StructureSet.Structures) { 
                    if (ps.Id.ToUpper().Contains("PTV"))
                    {
                        PTVItems.Add(ps);
                    }
                }
                //string ptv_gen = struc.FirstOrDefault(x => x.ToUpper() == "PTV");
            }
            else
            {
                CBOPTVEnabled = false;
                LBLPTVEnabled = false;
            }
        }

        //Window (i.e. GUI) loading event function created to close window if no patient and/or course is loaded
        private void Window_Loaded(object sender, EventArgs e)
        {
            Window window = sender as Window;
            window.Close();
        }

        /// <summary>
        /// Fine the beam direction vector pointing towards the source.
        /// </summary>
        /// <param name="beam">The beam to be evaluated.</param>
        /// <returns>A VVector struct containing the coordinate directions.</returns>
        public VVector DirectionTowardSource(Beam beam)
        {
            VVector sourceLocation = beam.GetSourceLocation(beam.ControlPoints.First().GantryAngle);
            VVector dirVec = sourceLocation - beam.IsocenterPosition;
            dirVec.ScaleToUnitLength();
            return dirVec;
        }


        /// <summary>
        /// Determine the entry point of a radiation beam into a structure.
        /// </summary>
        /// <param name="structure">The structure the beam is entering.</param>
        /// <param name="direction">The direction VVector of the beam.</param>
        /// <param name="point">The isocenter position for the beam.</param>
        /// <returns>A VVector representing the position where the beam enters the structure.</returns>
        public VVector GetStructureEntryPoint(Structure structure, VVector direction, VVector point)
        {
            double stepSizeInmm = 1.0;
            VVector tDir = direction;
            tDir.ScaleToUnitLength();

            VVector startPoint = point + tDir * (600 / 2);
            VVector endPoint = point - tDir * (600 / 2);
            SegmentProfile profile = structure.GetSegmentProfile(startPoint, endPoint, new System.Collections.BitArray((int)Math.Ceiling((endPoint - startPoint).Length / stepSizeInmm)));

            startPoint = profile.First(spp => spp.Value == true).Position;
            //endPoint = profile.Last(spp => spp.Value == true).Position;

            return startPoint;
        }


        private async void CalculateLeafMotions(ExternalPlanSetup plan, ExternalPlanSetup copied_plan)
        {
            //  Set leaf motion calculation model
            copied_plan.SetCalculationModel(CalculationType.PhotonLeafMotions, LMCText);

            //  Calculate the leaf motions
            await UpdateListBox("Calculating Leaf Motions....");
            try
            {
                //Use fixed jaws option for leaf motion calculator
                copied_plan.CalculateLeafMotions(new LMCVOptions(true));
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// Compute the separation (in mm) between the entry points of two beams to a structure.
        /// </summary>
        /// <param name="b1">One of the beams entering the structure.</param>
        /// <param name="b2">The other beam entering the structure.</param>
        /// <param name="external">The structure being entered.</param>
        /// <returns>A double representing the separation distance between the entry points of the two beams.</returns>
        private double ComputeBeamSeparation(Beam b1, Beam b2, Structure external)
        {
            VVector direction1 = DirectionTowardSource(b1);
            VVector direction2 = DirectionTowardSource(b2);
            VVector results1 = GetStructureEntryPoint(external, direction1, b1.IsocenterPosition);
            VVector results2 = GetStructureEntryPoint(external, direction2, b2.IsocenterPosition);

            //  JAK (2023-06-03): this is a much more condensed way to compute the distance of separation.
            return (results1 - results2).Length;
        }


        /// <summary>
        /// Copy the radiation beams from one plan to another.
        /// </summary>
        /// <param name="original">The plan to copy the beams from.</param>
        /// <param name="copy">The plan to copy the beams to.</param>
        /// <param name="parameters">The applicable LINAC parameters.</param>
        private void CopyBeams(ExternalPlanSetup original, ExternalPlanSetup copy, ExternalBeamMachineParameters parameters)
        {
            foreach (Beam b in original.Beams)
            {
                if (b.IsSetupField) //  JAK (2023-02-06): if it's a setup field, skip it.
                {
                    continue;
                }
                else
                {
                    //  beam is a not a setup field 
                    Beam Temp = copy.AddStaticBeam(
                        parameters,
                        b.ControlPoints[0].JawPositions,
                        b.ControlPoints[0].CollimatorAngle,
                        b.ControlPoints[0].GantryAngle,
                        b.ControlPoints[0].PatientSupportAngle,
                        b.IsocenterPosition
                        );
                    Temp.Id = b.Id;
                }
                /*
                else// if (b.MLC != null)   //  beam is a not a setup field and has MLCs
                {
                    Beam Temp = copy.AddMLCBeam(
                        parameters,
                        b.ControlPoints[0].LeafPositions,
                        b.ControlPoints[0].JawPositions,
                        b.ControlPoints[0].CollimatorAngle,
                        b.ControlPoints[0].GantryAngle,
                        b.ControlPoints[0].PatientSupportAngle,
                        b.IsocenterPosition
                        );
                    Temp.Id = b.Id;
                }*/
            }
        }

        /// <summary>
        /// Use an asymmetric margin, depending on treatment side, to expand the PTV
        /// </summary>
        /// <param name="ptv">The PTV to expand.</param>
        /// <param name="ss">The StructureSet that provides the frame of reference.</param>
        /// <param name="plan">The plan to add the expanded PTV to.</param>
        /// <returns>A reference to the expanded PTV structure.</returns>
        private async Task<Structure> CreateExpandedPTV(Structure ptv, StructureSet ss, ExternalPlanSetup plan)
        {

            //add the expanded structure of the PTV
            Structure expandPTV = CopiedSS.AddStructure("PTV", "PTV_Expanded");

            //Set the margins for each breast side, depending which side was selected
            BreastSide side = SelectedBreastSide;
            AxisAlignedMargins margins = side.Margins;

            expandPTV.SegmentVolume = ptv.AsymmetricMargin(margins);



            await UpdateListBox("Checking/Removing holes in body structure....");

            //  Change calculation model to reset dose volume to enable structure
            //  set espectially external (body) structure edit option.
            var calcModel = plan.PhotonCalculationModel;
            plan.SetCalculationModel(CalculationType.PhotonVolumeDose, calcModel);

            //  Ensure the original external margin is saved.
            Structure external = CopiedSS.Structures.FirstOrDefault(x => x.DicomType == "EXTERNAL");
            //  Copying original body structure before modifictaion
            Structure external_original = CopiedSS.Structures.FirstOrDefault(x => x.Id == "Body_Original");
            if (external_original == null)
            {
                external_original = CopiedSS.AddStructure("ORGAN", "Body_Original");
                external_original.SegmentVolume = external.SegmentVolume;
            }

            //  Creating a box around the image set on slice by slice basis with inner margins
            //  of 2 voxels to remove holes in body that may be created by expansion on PTV.
            Structure ext_test2 = CreateBox(ss, expandPTV);

            // removing body holes if there are any
            expandPTV.SegmentVolume = expandPTV.And(ext_test2);
            try
            {
                external.SegmentVolume = external.Or(expandPTV);
            }
            catch
            {
                throw;
            }

            //Remove the box structure
            if (CopiedSS.CanRemoveStructure(ext_test2))
            {
                CopiedSS.RemoveStructure(ext_test2);
            }



            return expandPTV;
        }

        /// <summary>
        /// Creates a box (as a Structure) based on the input structure.
        /// </summary>
        /// <param name="ss">The StructureSet that provides the frame of reference.</param>
        /// <param name="structure">The structure to base the box on.</param>
        /// <returns>A reference to a Structure object containing the box.</returns>
        private Structure CreateBox(StructureSet ss, Structure structure)
        {
            //  Creating VVectors in x, y direction for the box.
            Structure ext_test2 = ss.AddStructure("CONTROL", "Image_Box");
            var Img = CopiedSS.Image;
            VVector origin = Img.Origin;
            VVector res = new VVector(Img.XRes, Img.YRes, 0);
            VVector size = new VVector(Img.XSize, Img.YSize, 0);

            VVector v1, v2, v3, v4;
            v1 = origin + 2 * res;
            v2 = origin + new VVector((size.x - 2) * res.x, 2 * res.y, 0);
            v3 = origin + new VVector((size.x - 2) * res.x, (size.y - 2) * res.y, 0);
            v4 = origin + new VVector(2 * res.x, (size.y - 2) * res.y, 0);

            VVector[][] positions = new VVector[][]
            {
                    new VVector[] {v1, v2, v3, v4}
            };

            MeshBounds msh = MeshBoundsSlices(structure, CopiedSS);

            //Creating a box structure up to 5 slices below and 5 slices above the expanded PTV structure.
            for (int i = msh.Low - 5; i < msh.High + 6; i++)
            {
                foreach (VVector[] v in positions)
                {
                    ext_test2.AddContourOnImagePlane(v, i);
                }
            }

            return ext_test2;
        }

        /// <summary>
        /// Finds the high and low slice indices of a structure in reference to a particular structure set.
        /// </summary>
        /// <param name="structure">The structure whose high/low slice bounds are to be determined.</param>
        /// <param name="SS">The reference structure set.</param>
        /// <returns>A MeshBounds object encapsulating the high and low slice indices of the structure.</returns>
        private MeshBounds MeshBoundsSlices(Structure structure, StructureSet SS)
        {
            MeshBounds bounds;
            if (structure != null)
            {
                var meshgeometry = structure.MeshGeometry;
                if (meshgeometry != null)
                {
                    //MessageBox.Show("meshgeometry is not null");
                    var mesh = structure.MeshGeometry.Bounds;
                    if (mesh != null)
                    {
                        //MessageBox.Show("mesh is not null");
                        var meshLow = GetSlice(mesh.Z, SS);
                        var meshUp = GetSlice(mesh.Z + mesh.SizeZ, SS);

                        //MessageBox.Show("meshLow is " + meshLow.ToString() + "meshUp is " + meshUp.ToString());

                        bounds.Low = meshLow;
                        bounds.High = meshUp;
                        return bounds;
                    }

                }

            }

            bounds.Low = 0;
            bounds.High = 0;
            return bounds;
        }

        /// <summary>
        /// Get the slice index associated with position 'z' in the coordinate space defined by a given StructureSet
        /// </summary>
        /// <param name="z">The z location whose slice index is to be computed.</param>
        /// <param name="SS">The structure set providing the frame of reference for the z position</param>
        /// <returns>Returns an integer index of the z position in the structure set.</returns>
        public int GetSlice(double z, StructureSet SS)
        {
            var imageRes = SS.Image.ZRes;
            return Convert.ToInt32((z - SS.Image.Origin.z) / imageRes);
        }

        private Structure CreateHotColdSpotStructure(ExternalPlanSetup copied_plan, string ID, double dose)
        {
            Structure structure = CopiedSS.Structures.FirstOrDefault(x => x.Id == "100_coldspot");
            if (structure != null)
            {
                CopiedSS.RemoveStructure(structure);
            }
            structure = CopiedSS.AddStructure("DOSE_REGION", "100_coldspot");
            var DL = new DoseValue(dose, DoseValue.DoseUnit.Percent);
            Debug.Assert(DL.IsRelativeDoseValue);
            MessageBox.Show($"DoseValue {DL}");
            structure.ConvertDoseLevelToStructure(copied_plan.Dose, DL);

            return structure;
        }

        /// <summary>
        /// Determine breast side based on isocenter coordinates
        /// </summary>
        /// <param name="plan">The treatment plan.</param>
        /// <returns>The element of the SIDE enum representing the treatment side.</returns>
        private SIDE FindTreatmentSide(ExternalPlanSetup plan)
        {
            Beam b = plan.Beams.First();

            // Find the beam isocenter location(s)

            double Iso_x = b.IsocenterPosition.x;

            if (Iso_x < 0)
            {
                return SIDE.RIGHT;
            }
            else if (Iso_x > 0)
            {
                return SIDE.LEFT;
            }
            else
            {
                MessageBox.Show("Side cannot be determind");
                return SIDE.ERROR;
            }
        }

        private async void Optimize(OptimizationOptionsIMRT opt, ExternalPlanSetup plan, ExternalPlanSetup copied_plan, Structure ptv, Structure expandPTV, Structure ipsi_lung, double scalingFactor, DoseValue.DoseUnit doseUnit)
        {
            //Set Calculation Model back to optimization
            copied_plan.SetCalculationModel(CalculationType.PhotonVolumeDose, plan.PhotonCalculationModel);
            copied_plan.SetCalculationModel(CalculationType.PhotonIMRTOptimization, plan.GetCalculationModel(CalculationType.PhotonIMRTOptimization));
            await UpdateListBox("Optimizing....");
            //Optimization       
            OptimizationSetup optSet = copied_plan.OptimizationSetup;
            //Perscription dose

            // -- Test fix Gy/cGy scaling by removing scaling factor -- 
            double recievedPresc = (int)copied_plan.NumberOfFractions * copied_plan.DosePerFraction.Dose;

            //set optimization objective values for each PTV
            DoseValue upperPTV = new DoseValue(1.02 * recievedPresc, doseUnit);
            DoseValue lowerExt = new DoseValue(0.12 * recievedPresc, doseUnit);
            DoseValue lowerPTV = new DoseValue(1.01 * recievedPresc, doseUnit);

            //Set Objectives for each energy depending on selected energy
            Energy energy = SelectedEnergy;

            //  Set upper point objective for the PTV, and lower point objective for the expanded PTV.
            optSet.AddPointObjective(ptv, OptimizationObjectiveOperator.Upper, upperPTV, 0, 100);
            optSet.AddPointObjective(expandPTV, OptimizationObjectiveOperator.Lower, lowerExt, 100, 2); //  JAK: Question: does lowerExt refer to the expanded PTV or the External contour?

            //  Set lower point objectives for PTV and EUD objective for the ipsilateral lung to shield it
            switch (energy.MeV)
            {
                case 6:
                    optSet.AddPointObjective(ptv, OptimizationObjectiveOperator.Lower, lowerPTV, 100, 102);
                    optSet.AddEUDObjective(ipsi_lung, OptimizationObjectiveOperator.Upper, new DoseValue(400 * scalingFactor, doseUnit), 1, 30);
                    break;
                case 10:
                    optSet.AddPointObjective(ptv, OptimizationObjectiveOperator.Lower, lowerPTV, 100, 105);
                    optSet.AddEUDObjective(ipsi_lung, OptimizationObjectiveOperator.Upper, new DoseValue(400 * scalingFactor, doseUnit), 1, 20);
                    break;
                case 15:
                    optSet.AddPointObjective(ptv, OptimizationObjectiveOperator.Lower, lowerPTV, 100, 105);
                    optSet.AddEUDObjective(ipsi_lung, OptimizationObjectiveOperator.Upper, new DoseValue(400 * scalingFactor, doseUnit), 1, 30);
                    break;
                default:
                    break;
            }

            //  Optimize the copied plan
            copied_plan.Optimize(opt);

        }

        private void SpareLungHeart(Structure ptv, Structure ipsi_lung)
        {
            //Create a temp structure with 5mm outer matrgins to exclude any overlap between PTV and ipsi lateral lung for planning. 
            Structure ipsiL_placeholder = CopiedSS.AddStructure("DOSE_REGION", "ipsi_Ls");
            var margins_ipsi = new AxisAlignedMargins(StructureMarginGeometry.Outer, 5, 5, 5, 5, 5, 5);
            ipsiL_placeholder.SegmentVolume = ipsi_lung.AsymmetricMargin(margins_ipsi);

            //perform a boolean operation of subtraction to shield ipsilateral lung
            ptv.SegmentVolume = ptv.Sub(ipsiL_placeholder);
            CopiedSS.RemoveStructure(ipsiL_placeholder);

            //store heart structure.
            Structure heart = CopiedSS.Structures.FirstOrDefault(x => x.Id == selectedHeartId);

            //Create a temp structure with 5mm outer matrgins to exclude any overlap between PTV and heart for planning. 
            Structure heart_placeholder = CopiedSS.AddStructure("DOSE_REGION", "heart_PH");
            var margins_heart = new AxisAlignedMargins(StructureMarginGeometry.Outer, 5, 5, 5, 5, 5, 5);
            heart_placeholder.SegmentVolume = heart.AsymmetricMargin(margins_heart);

            //perform a boolean operation of subtraction to shield heart
            ptv.SegmentVolume = ptv.Sub(heart_placeholder);
            CopiedSS.RemoveStructure(heart_placeholder);
        }

        /// <summary>
        /// Update the 'status' list box, adding string 's'.
        /// </summary>
        /// <param name="s">The new status update</param>
        /// <returns>A 'Task' object.</returns>
        private async Task UpdateListBox(string s)
        {
            StatusBoxItems.Add(s);
            //statusBox.ScrollIntoView(s);
            await Task.Delay(1);
        }

    }
}
