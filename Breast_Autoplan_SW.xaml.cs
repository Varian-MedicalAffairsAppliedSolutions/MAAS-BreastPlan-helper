using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using System.Threading.Tasks;

namespace GridBlockCreator
{
    /// <summary>
    /// Interaction logic for UserControl1.xaml
    /// </summary>

    public partial class BreastAutoPlanDialog : UserControl
    {

        enum SIDE { RIGHT, LEFT, ERROR };

        #region Variable definitions

        //define variables

        //Dose calculation algorithim, edit this if outdated
        private readonly string DoseCalculationAlgorithm = "AAA_15606";
        private readonly string DoseCalculationAlgorithm_Old = "AAA_13623";
        //Optimization algorithm, edit this if outdated
        private readonly string OptimizationAlgorithm = "PO_15606";
        //Leaf Motion Calculator, edit this if outdated
        private readonly string LeafMotionCalculator = "Varian Leaf Motion Calculator [15.6.06]";

        //Optimization settings
        private readonly OptimizationOptionsIMRT opt = new OptimizationOptionsIMRT(
            1000,
            OptimizationOption.RestartOptimization,
            OptimizationConvergenceOption.TerminateIfConverged,
            OptimizationIntermediateDoseOption.UseIntermediateDose,
            "1");

        #endregion

        #region Internal classes/structs

        private class BreastSide
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
        private class Energy
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
        private class Prescription
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

        #endregion

        #region Constructors

        public BreastAutoPlanDialog()
        {
            InitializeComponent();
        }

        public BreastAutoPlanDialog(ScriptContext context) : this()
        {
            double separation = 0;
            Context = context;
            Course course = context.Course;
            Plan = course.ExternalPlanSetups.FirstOrDefault(); ;
            //SS = Plan.StructureSet;
            StructureSet ss = Plan.StructureSet;

            displayPatId.Content = context.Patient.Id;

            #region cboPlanId ComboBox

            //  Creating a list of plansetups in the course in context and attach it to cboPlanId Combobox
            List<string> ps_list = course.PlanSetups.Select(s => s.Id).ToList();
            cboPlanId.ItemsSource = ps_list;

            if (ps_list.Count == 1)
            {
                cboPlanId.SelectedIndex = 0;
            }
            else
            {
                cboPlanId.SelectedItem = ps_list.FirstOrDefault(x => x == Plan.Id);
            }

            #endregion

            #region cboBreastSide ComboBox

            List<BreastSide> brSide = new List<BreastSide>()
            {
                new BreastSide(ss, "Right", "Lung_R", new AxisAlignedMargins(StructureMarginGeometry.Outer, 25, 25, 0, 0, 0, 0)),
                new BreastSide(ss, "Left", "Lung_L", new AxisAlignedMargins(StructureMarginGeometry.Outer, 0, 25, 0, 25, 0, 0))
            };

            cboBreastSide.ItemsSource = brSide;
            if (cboPlanId.SelectedItem != null)
            {
                cboBreastSide.SelectedIndex = (int)FindTreatmentSide(Plan);
            }

            #endregion

            #region cboIpsiLung ComboBox

            //Creating a list of lung Ids
            List<string> Lung_Ids = ss.Structures.Where(x => x.Id.ToUpper().Contains("LUNG")).Select(x => x.Id).ToList();

            // Attaching  list of lung Ids to ipsilung combobox
            cboIpsiLung.ItemsSource = Lung_Ids;    //  JAK: Question - what should happen if Lung_Ids.Count == 0?

            //Selecting default value for ipsilung combobox based on the side of the breast 
            BreastSide side = cboBreastSide.SelectedItem as BreastSide;
            if (side.LungId != null)
            {
                cboIpsiLung.SelectedItem = side.LungId;
            }
            else
            {
                MessageBox.Show("Please select ipsi lateral lung Id");
            }

            #endregion

            #region heartList ComboBox

            //Creating a list of heart Ids
            List<string> Heart_Ids = ss.Structures.Where(x => x.Id.ToUpper().Contains("HEART")).Select(x => x.Id).ToList();

            cboHeart.ItemsSource = Heart_Ids;

            string heart = Heart_Ids.FirstOrDefault(x => x.ToUpper() == "HEART");

            if (heart != null)
            {
                cboHeart.SelectedItem = heart;
            }

            #endregion

            #region cboPrescription ComboBox

            //  JAK: This cleans up the code further down, but retains ease of modification.
            List<Prescription> prescriptions = new List<Prescription>
            {
                new Prescription(){Fractions = 16, DoseCGy = 4250},
                new Prescription(){Fractions = 5, DoseCGy = 2600},
                new Prescription(){Fractions = 15, DoseCGy = 4000},
                new Prescription(){Fractions = 25, DoseCGy = 5000}
            };
            cboPrescription.ItemsSource = prescriptions;

            #region Unused

            //List<string> prescritions_list = new List<string>
            //{
            //    "4250 cGy in 16 Fx",
            //    "2600 cGy in 5 Fx",
            //    "4000 cGy in 15 Fx",
            //    "5000 cGy in 25 Fx"
            //};

            //PrescriptionValue.ItemsSource = prescritions_list;

            #endregion

            #endregion

            #region cboBeamEnergy ComboBox

            // Beam sepation calculation
            Structure external = Plan.StructureSet.Structures.FirstOrDefault(x => x.Id.ToUpper().Contains("BODY"));
            if (external == null)
            {
                external = Plan.StructureSet.Structures.FirstOrDefault(x => x.DicomType.ToUpper() == "EXTERNAL");
            }

            Beam b1 = Plan.Beams.ElementAt(0);
            Beam b2 = Plan.Beams.ElementAt(1);

            separation = ComputeBeamSeparation(b1, b2, external);
            txtSeparation.Text = Math.Round(separation / 10, 2).ToString();

            //Creating list of available beam energies
            //  JAK (2023-02-06): This will be easier to maintain.  If new separations/energies
            //  are required, they can be added here and it will require no extra coding.  The
            //  more extra coding required, the greater the chance for new bugs to arise.
            List<Energy> energies = new List<Energy>()
            {
                new Energy(){ Index = 0, MeV = 6, MinSep = 0, MaxSep = 210 },
                new Energy(){ Index = 1, MeV = 10, MinSep = 210, MaxSep = 250 },
                new Energy(){ Index = 2, MeV = 15, MinSep = 250 }
            };

            //Assigning enery values to beam energy combobox.
            cboBeamEnergy.ItemsSource = energies;
            IEnumerable<int> selected = energies.Where(x => x.IsInRange(separation)).Select(x => x.Index);
            cboBeamEnergy.SelectedIndex = selected.ElementAt(0);

            #endregion

        }

        #endregion

        #region Event Handlers

        #region BtnClose_Click

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Window window = Parent as Window;
            window.Close();
        }

        #endregion

        #region BtnPlan_Click - where the action is!

        //async is to allow the listbox in the GUI to update during script run time
        private async void BtnPlan_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                //allow modifications to the patient plan.  (JAK, 2023-02-02: moved to the top for emphasis.)
                Context.Patient.BeginModifications();

                #region Defining context

                //await allows another process to run, in this case allows the GUI to update
                await UpdateListBox("Starting to Plan....");

                //get needed variables from eclipse
                Patient pt = Context.Patient;
                Course cou = Context.Course;
                ExternalPlanSetup plan = cou.ExternalPlanSetups.FirstOrDefault(x => x.Id == cboPlanId.SelectedItem.ToString());
                // Structure set associated with the plan;
                StructureSet ss = plan.StructureSet;

                #endregion

                #region Begin patient modification and create a copy of a plan from the original plan

                //set the machine paramters
                ExternalBeamMachineParameters machineParameters = new ExternalBeamMachineParameters("TB_H", cboBeamEnergy.SelectedItem.ToString(), 600, "STATIC", string.Empty);

                await UpdateListBox("Creating copy of plan with slected beam energy....");
                //create a copy of the original plan by creating a new plan and copying beams from the original plan.
                ExternalPlanSetup copied_plan = cou.AddExternalPlanSetup(ss);
                CopiedSS = copied_plan.StructureSet;

                CopyBeams(plan, copied_plan, machineParameters);

                Prescription presc = cboPrescription.SelectedItem as Prescription;
                copied_plan.SetPrescription(presc.Fractions, new DoseValue(presc.DosePerFraction, DoseValue.DoseUnit.cGy), 1.0);

                #endregion

                #region PTV creation when customized PTV option not used

                Structure ptv = null;

                // Creating PTV if customized PTV option (check box) is not selected.
                if ((bool)!cbCustomPTV.IsChecked)
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
                            copied_plan.SetCalculationModel(CalculationType.PhotonVolumeDose, DoseCalculationAlgorithm);
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

                #endregion

                #region PTV selection and use when customized PTV option not used 

                //Select customized PTV if cutomized PTV checkbox is checked 
                else if (cboPtvID.SelectedIndex >= -1 && (bool)cbCustomPTV.IsChecked)
                {
                    await UpdateListBox("Using customized PTV...");
                    ptv = CopiedSS.Structures.FirstOrDefault(x => x.Id == cboPtvID.SelectedItem.ToString());
                }
                else
                {
                    MessageBox.Show("Please select a valid PTV or close the window and create one.");
                }
                #endregion

                #region Create Expanded PTV to include flesh

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

                #endregion

                #region Creating 5mm outer margins for ipsilateral lung and heart from PTV for OAR sparing 

                //store the ipsilateral lung
                Structure ipsi_lung = CopiedSS.Structures.FirstOrDefault(x => x.Id == cboIpsiLung.SelectedItem.ToString());
                SpareLungHeart(ptv, ipsi_lung);

                #endregion

                #region Optimization

                Optimize(copied_plan, ptv, expandPTV, ipsi_lung);

                #region Unused

                ////Set Calculation Model back to optimization
                //copied_plan.SetCalculationModel(CalculationType.PhotonVolumeDose, DoseCalculationAlgorithm);
                //copied_plan.SetCalculationModel(CalculationType.PhotonIMRTOptimization, OptimizationAlgorithm);
                //await UpdateListBox("Optimizing....");
                ////Optimization       
                //OptimizationSetup optSet = copied_plan.OptimizationSetup;
                ////Perscription dose
                //double recievedPresc = (int)copied_plan.NumberOfFractions * copied_plan.DosePerFraction.Dose;

                ////set optimization objective values for each PTV
                //DoseValue upperPTV = new DoseValue(1.02 * recievedPresc, "cGy");
                //DoseValue lowerExt = new DoseValue(0.12 * recievedPresc, "cGy");
                //DoseValue lowerPTV = new DoseValue(1.01 * recievedPresc, "cGy");

                ////Set Objectives for each energy depending on selected energy
                //Energy energy = cboBeamEnergy.SelectedItem as Energy;

                ////  Set upper point objective for the PTV, and lower point objective for the expanded PTV.
                //optSet.AddPointObjective(ptv, OptimizationObjectiveOperator.Upper, upperPTV, 0, 100);
                //optSet.AddPointObjective(expandPTV, OptimizationObjectiveOperator.Lower, lowerExt, 100, 2); //  JAK: Question: does lowerExt refer to the expanded PTV or the External contour?

                ////  Set lower point objectives for PTV and EUD objective for the ipsilateral lung to shield it
                //switch (energy.MeV)
                //{
                //    case 6:
                //        optSet.AddPointObjective(ptv, OptimizationObjectiveOperator.Lower, lowerPTV, 100, 102);
                //        optSet.AddEUDObjective(ipsi_lung, OptimizationObjectiveOperator.Upper, new DoseValue(400, "cGy"), 1, 30);
                //        break;
                //    case 10:
                //        optSet.AddPointObjective(ptv, OptimizationObjectiveOperator.Lower, lowerPTV, 100, 105);
                //        optSet.AddEUDObjective(ipsi_lung, OptimizationObjectiveOperator.Upper, new DoseValue(400, "cGy"), 1, 20);
                //        break;
                //    case 15:
                //        optSet.AddPointObjective(ptv, OptimizationObjectiveOperator.Lower, lowerPTV, 100, 105);
                //        optSet.AddEUDObjective(ipsi_lung, OptimizationObjectiveOperator.Upper, new DoseValue(400, "cGy"), 1, 30);
                //        break;
                //    default:
                //        break;
                //}

                //6X
                //if (cboBeamEnergy.SelectedIndex == 0)
                //{
                //    //add upper and lower point objectives for PTV and a lower for expanded PTV
                //    //optSet.AddPointObjective(ptv, OptimizationObjectiveOperator.Upper, upperPTV, 0, 100);
                //    //optSet.AddPointObjective(expandPTV, OptimizationObjectiveOperator.Lower, lowerExt, 100, 2); //  JAK: Question: does lowerExt refer to the expanded PTV or the External contour?
                //    optSet.AddPointObjective(ptv, OptimizationObjectiveOperator.Lower, lowerPTV, 100, 102);
                //    //Add EUD objective for the ipsilateral lung to shield it
                //    optSet.AddEUDObjective(ipsi_lung, OptimizationObjectiveOperator.Upper, new DoseValue(400, "cGy"), 1, 30);
                //}
                ////10X
                //else if (cboBeamEnergy.SelectedIndex == 1)
                //{
                //    //add upper and lower point objectives for PTV and a lower for expanded PTV
                //    //optSet.AddPointObjective(ptv, OptimizationObjectiveOperator.Upper, upperPTV, 0, 100);
                //    //optSet.AddPointObjective(expandPTV, OptimizationObjectiveOperator.Lower, lowerExt, 100, 2);
                //    optSet.AddPointObjective(ptv, OptimizationObjectiveOperator.Lower, lowerPTV, 100, 105);
                //    //Add EUD objective for the ipsilateral lung to shield it
                //    optSet.AddEUDObjective(ipsi_lung, OptimizationObjectiveOperator.Upper, new DoseValue(400, "cGy"), 1, 20);
                //}
                ////15X
                //else// if (cboBeamEnergy.SelectedIndex == 2)
                //{
                //    //add upper and lower point objectives for PTV and a lower for expanded PTV
                //    //optSet.AddPointObjective(ptv, OptimizationObjectiveOperator.Upper, upperPTV, 0, 100);
                //    //optSet.AddPointObjective(expandPTV, OptimizationObjectiveOperator.Lower, lowerExt, 100, 2);
                //    optSet.AddPointObjective(ptv, OptimizationObjectiveOperator.Lower, lowerPTV, 100, 105);
                //    //Add EUD objective for the ipsilateral lung to shield it
                //    optSet.AddEUDObjective(ipsi_lung, OptimizationObjectiveOperator.Upper, new DoseValue(400, "cGy"), 1, 30);
                //}

                ////  Optimize the copied plan
                //copied_plan.Optimize(opt);

                #endregion

                #endregion

                #region Calculate Leaf Motions

                //  Set leaf motion calculation model
                copied_plan.SetCalculationModel(CalculationType.PhotonLeafMotions, LeafMotionCalculator);

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
                //try
                //{
                //    CalculateLeafMotions(copied_plan);
                //}
                //catch
                //{
                //    MessageBox.Show("\n Leaf motion calc unsuccessfull");
                //    return;
                //}

                #endregion

                //  Re-calculate dose
                await UpdateListBox("Calculating Dose....");
                copied_plan.CalculateDose();

                #region Hot & cold spot generation, reoptimization and final dose calculation

                await UpdateListBox("Creating Hot and Cold Spots....");

                //  Create a hotspot structure at the 105 isodose line
                Structure hotSpot105 = CreateHotColdSpotStructure(copied_plan, "105_hotspot", 105);
                //hotSpot105 = CopiedSS.Structures.FirstOrDefault(x => x.Id == "105_hotspot");
                //if (hotSpot105 != null)
                //{
                //    CopiedSS.RemoveStructure(hotSpot105);
                //}
                //hotSpot105 = CopiedSS.AddStructure("DOSE_REGION", "105_hotspot");
                //hotSpot105.ConvertDoseLevelToStructure(copied_plan.Dose, new DoseValue(105, DoseValue.DoseUnit.Percent));

                //create a coldspot structure at the 100 isodose line
                Structure coldSpot100 = CreateHotColdSpotStructure(copied_plan, "100_coldspot", 100);
                //coldSpot100 = CopiedSS.Structures.FirstOrDefault(x => x.Id == "100_coldspot");
                //if (coldSpot100 != null)
                //{
                //    CopiedSS.RemoveStructure(coldSpot100);
                //}
                //coldSpot100 = CopiedSS.AddStructure("DOSE_REGION", "100_coldspot");
                //coldSpot100.ConvertDoseLevelToStructure(copied_plan.Dose, new DoseValue(100, DoseValue.DoseUnit.Percent));

                //  Subtract the current cold spot volume from the PTV and assign the result to the cold spot.
                coldSpot100.SegmentVolume = ptv.Sub(coldSpot100);

                OptimizationSetup optSet = copied_plan.OptimizationSetup;
                double recievedPresc = (int)copied_plan.NumberOfFractions * copied_plan.DosePerFraction.Dose;

                //if hotspot/coldspot isn't empty add a point objective
                if (!hotSpot105.IsEmpty)
                {
                    optSet.AddPointObjective(hotSpot105, OptimizationObjectiveOperator.Upper,
                    new DoseValue(1.03 * recievedPresc, DoseValue.DoseUnit.cGy), 0, 45);
                }
                if (!coldSpot100.IsEmpty)
                {
                    optSet.AddPointObjective(coldSpot100, OptimizationObjectiveOperator.Lower,
                    new DoseValue(0.98 * recievedPresc, DoseValue.DoseUnit.cGy), 100, 20);
                }

                //if there is any objectives
                if (copied_plan.OptimizationSetup.Objectives != null)
                {
                    await UpdateListBox("Re-optimizing....");
                    //optimize plan again
                    copied_plan.Optimize(opt);
                    await UpdateListBox("Re-calculating Leaf Motions....");
                    //re-calculate leaf motions after optimization
                    copied_plan.SetCalculationModel(CalculationType.PhotonLeafMotions, LeafMotionCalculator);

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

                #endregion

                #region Plan ID assignment and end of plan generation

                //Name the new plan as SW_PhotonEnergy
                string pId = "SW_" + cboBeamEnergy.SelectedItem.ToString();
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
                        copied_plan.Id = "SW_" + cboBeamEnergy.SelectedItem.ToString() + "_" + ii.ToString();
                        match = Id_list.Find(x => x == copied_plan.Id);
                        ii++;
                    }
                }


                //Display the finishing messages
                await UpdateListBox("Planning is done...");
#if !DEBUG
                MessageBox.Show("Please open and evalute new plan with ID:\n" + copied_plan.Id);
#endif

                #endregion

            }
            catch (Exception ex)
            {
                _ = MessageBox.Show($"Error creating plan!\n{ex.Message}");
                return;
            }
        }

        #endregion

        #region CbCustomPTV_Click
        // Checkbox to select customized PTV created by the user.
        private void CbCustomPTV_Click(object sender, RoutedEventArgs e)
        {
            if ((bool)cbCustomPTV.IsChecked)
            {
                cboPtvID.IsEnabled = true;
                lblPtvId.IsEnabled = true;
                List<string> struc = new List<string> { };
                foreach (Structure s in Context.StructureSet.Structures)
                {
                    struc.Add(s.Id);
                }
                List<string> ptv_Ids = struc.Where(x => x.ToUpper().Contains("PTV")).ToList();
                cboPtvID.ItemsSource = ptv_Ids;
                //string ptv_gen = struc.FirstOrDefault(x => x.ToUpper() == "PTV");
            }
            else
            {
                cboPtvID.IsEnabled = false;
                lblPtvId.IsEnabled = false;
            }
        }
        #endregion

        #region Window_Loaded (close window on error)

        //Window (i.e. GUI) loading event function created to close window if no patient and/or course is loaded
        private void Window_Loaded(object sender, EventArgs e)
        {
            Window window = sender as Window;
            window.Close();
        }

        #endregion

        #endregion

        #region Functions

        #region Beam Separation Functions

        #region DirectionTowardSource

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

        #endregion

        #region GetStructureEntryPoint

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

        #endregion

        #endregion

        #region CalculateLeafMotions

        private async void CalculateLeafMotions(ExternalPlanSetup copied_plan)
        {
            //  Set leaf motion calculation model
            copied_plan.SetCalculationModel(CalculationType.PhotonLeafMotions, LeafMotionCalculator);

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

        #endregion

        #region ComputeBeamSeparation

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

        #endregion

        #region CopyBeams

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
                if (b.MLC == null)  //  beam is a not a setup field and has no MLCs
                {
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
                }
            }
        }

        #endregion

        #region CreateExpandedPTV

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
            BreastSide side = cboBreastSide.SelectedItem as BreastSide;
            AxisAlignedMargins margins = side.Margins;

            expandPTV.SegmentVolume = ptv.AsymmetricMargin(margins);

            #region Checking/Removing holes in body structure

            await UpdateListBox("Checking/Removing holes in body structure....");

            //  Change calculation model to reset dose volume to enable structure
            //  set espectially external (body) structure edit option.
            plan.SetCalculationModel(CalculationType.PhotonVolumeDose, DoseCalculationAlgorithm_Old);

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

            #endregion

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

        #region Functions used for checking/removing holes in external/body structure

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

        #endregion

        #endregion

        #region CreateHotColdSpot

        private Structure CreateHotColdSpotStructure(ExternalPlanSetup copied_plan, string ID, double dose)
        {
            Structure structure = CopiedSS.Structures.FirstOrDefault(x => x.Id == "100_coldspot");
            if (structure != null)
            {
                CopiedSS.RemoveStructure(structure);
            }
            structure = CopiedSS.AddStructure("DOSE_REGION", "100_coldspot");
            structure.ConvertDoseLevelToStructure(copied_plan.Dose, new DoseValue(dose, DoseValue.DoseUnit.Percent));

            return structure;
        }

        #endregion

        #region FindTreatmentSide

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

        #endregion

        #region Optimize

        private async void Optimize(ExternalPlanSetup copied_plan, Structure ptv, Structure expandPTV, Structure ipsi_lung)
        {
            //Set Calculation Model back to optimization
            copied_plan.SetCalculationModel(CalculationType.PhotonVolumeDose, DoseCalculationAlgorithm);
            copied_plan.SetCalculationModel(CalculationType.PhotonIMRTOptimization, OptimizationAlgorithm);
            await UpdateListBox("Optimizing....");
            //Optimization       
            OptimizationSetup optSet = copied_plan.OptimizationSetup;
            //Perscription dose
            double recievedPresc = (int)copied_plan.NumberOfFractions * copied_plan.DosePerFraction.Dose;

            //set optimization objective values for each PTV
            DoseValue upperPTV = new DoseValue(1.02 * recievedPresc, "cGy");
            DoseValue lowerExt = new DoseValue(0.12 * recievedPresc, "cGy");
            DoseValue lowerPTV = new DoseValue(1.01 * recievedPresc, "cGy");

            //Set Objectives for each energy depending on selected energy
            Energy energy = cboBeamEnergy.SelectedItem as Energy;

            //  Set upper point objective for the PTV, and lower point objective for the expanded PTV.
            optSet.AddPointObjective(ptv, OptimizationObjectiveOperator.Upper, upperPTV, 0, 100);
            optSet.AddPointObjective(expandPTV, OptimizationObjectiveOperator.Lower, lowerExt, 100, 2); //  JAK: Question: does lowerExt refer to the expanded PTV or the External contour?

            //  Set lower point objectives for PTV and EUD objective for the ipsilateral lung to shield it
            switch (energy.MeV)
            {
                case 6:
                    optSet.AddPointObjective(ptv, OptimizationObjectiveOperator.Lower, lowerPTV, 100, 102);
                    optSet.AddEUDObjective(ipsi_lung, OptimizationObjectiveOperator.Upper, new DoseValue(400, "cGy"), 1, 30);
                    break;
                case 10:
                    optSet.AddPointObjective(ptv, OptimizationObjectiveOperator.Lower, lowerPTV, 100, 105);
                    optSet.AddEUDObjective(ipsi_lung, OptimizationObjectiveOperator.Upper, new DoseValue(400, "cGy"), 1, 20);
                    break;
                case 15:
                    optSet.AddPointObjective(ptv, OptimizationObjectiveOperator.Lower, lowerPTV, 100, 105);
                    optSet.AddEUDObjective(ipsi_lung, OptimizationObjectiveOperator.Upper, new DoseValue(400, "cGy"), 1, 30);
                    break;
                default:
                    break;
            }

            #region Unused

            //6X
            //if (cboBeamEnergy.SelectedIndex == 0)
            //{
            //    //add upper and lower point objectives for PTV and a lower for expanded PTV
            //    //optSet.AddPointObjective(ptv, OptimizationObjectiveOperator.Upper, upperPTV, 0, 100);
            //    //optSet.AddPointObjective(expandPTV, OptimizationObjectiveOperator.Lower, lowerExt, 100, 2); //  JAK: Question: does lowerExt refer to the expanded PTV or the External contour?
            //    optSet.AddPointObjective(ptv, OptimizationObjectiveOperator.Lower, lowerPTV, 100, 102);
            //    //Add EUD objective for the ipsilateral lung to shield it
            //    optSet.AddEUDObjective(ipsi_lung, OptimizationObjectiveOperator.Upper, new DoseValue(400, "cGy"), 1, 30);
            //}
            ////10X
            //else if (cboBeamEnergy.SelectedIndex == 1)
            //{
            //    //add upper and lower point objectives for PTV and a lower for expanded PTV
            //    //optSet.AddPointObjective(ptv, OptimizationObjectiveOperator.Upper, upperPTV, 0, 100);
            //    //optSet.AddPointObjective(expandPTV, OptimizationObjectiveOperator.Lower, lowerExt, 100, 2);
            //    optSet.AddPointObjective(ptv, OptimizationObjectiveOperator.Lower, lowerPTV, 100, 105);
            //    //Add EUD objective for the ipsilateral lung to shield it
            //    optSet.AddEUDObjective(ipsi_lung, OptimizationObjectiveOperator.Upper, new DoseValue(400, "cGy"), 1, 20);
            //}
            ////15X
            //else// if (cboBeamEnergy.SelectedIndex == 2)
            //{
            //    //add upper and lower point objectives for PTV and a lower for expanded PTV
            //    //optSet.AddPointObjective(ptv, OptimizationObjectiveOperator.Upper, upperPTV, 0, 100);
            //    //optSet.AddPointObjective(expandPTV, OptimizationObjectiveOperator.Lower, lowerExt, 100, 2);
            //    optSet.AddPointObjective(ptv, OptimizationObjectiveOperator.Lower, lowerPTV, 100, 105);
            //    //Add EUD objective for the ipsilateral lung to shield it
            //    optSet.AddEUDObjective(ipsi_lung, OptimizationObjectiveOperator.Upper, new DoseValue(400, "cGy"), 1, 30);
            //}

            #endregion

            //  Optimize the copied plan
            copied_plan.Optimize(opt);

        }

        #endregion

        #region SpareLungHeart

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
            Structure heart = CopiedSS.Structures.FirstOrDefault(x => x.Id == cboHeart.SelectedItem.ToString());

            //Create a temp structure with 5mm outer matrgins to exclude any overlap between PTV and heart for planning. 
            Structure heart_placeholder = CopiedSS.AddStructure("DOSE_REGION", "heart_PH");
            var margins_heart = new AxisAlignedMargins(StructureMarginGeometry.Outer, 5, 5, 5, 5, 5, 5);
            heart_placeholder.SegmentVolume = heart.AsymmetricMargin(margins_heart);

            //perform a boolean operation of subtraction to shield heart
            ptv.SegmentVolume = ptv.Sub(heart_placeholder);
            CopiedSS.RemoveStructure(heart_placeholder);
        }

        #endregion

        #region UpdateListBox

        /// <summary>
        /// Update the 'status' list box, adding string 's'.
        /// </summary>
        /// <param name="s">The new status update</param>
        /// <returns>A 'Task' object.</returns>
        private async Task UpdateListBox(string s)
        {
            statusBox.Items.Add(s);
            statusBox.ScrollIntoView(s);
            await Task.Delay(1);
        }

        #endregion

        #endregion

        #region Properties

        private ScriptContext Context { get; set; }
        private StructureSet CopiedSS { get; set; }
        private ExternalPlanSetup Plan { get; set; }

        #endregion

    }
}
