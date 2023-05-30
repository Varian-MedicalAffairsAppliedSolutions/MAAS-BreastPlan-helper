using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace MAAS_BreastPlan_helper.Models
{
    public class Utils
    {
        public enum SIDE { RIGHT, LEFT, ERROR };
        public static SIDE FindTreatmentSide(ExternalPlanSetup plan)
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
        public static Tuple<string, string> GetFluenceEnergyMode(Beam bm)
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
        public static Tuple<List<double>, List<double>, List<double>, List<ControlPointParameters>, GantryDirection> GetBeamAngles(Beam beam)
        {
            var edits = beam.GetEditableParameters();
            var cps = edits.ControlPoints.ToList();

            var gan_direction = beam.GantryDirection;

            var gantry_angles = cps.Select(x => x.GantryAngle).ToList();
            var col_angles = cps.Select(x => x.CollimatorAngle).ToList();
            var couch_angles = cps.Select(x => x.PatientSupportAngle).ToList();

            return new Tuple<List<double>, List<double>, List<double>, List<ControlPointParameters>, GantryDirection>(gantry_angles, col_angles, couch_angles, cps, gan_direction);
        }

        public static void SpareLungHeart(Structure ptv, Structure ipsi_lung, Structure heart, StructureSet ss)
        {
            //Create a temp structure with 5mm outer matrgins to exclude any overlap between PTV and ipsi lateral lung for planning. 
            Structure ipsiL_placeholder = ss.AddStructure("DOSE_REGION", "__ipsi_Ls");
            var margins_ipsi = new AxisAlignedMargins(StructureMarginGeometry.Outer, 5, 5, 5, 5, 5, 5);
            ipsiL_placeholder.SegmentVolume = ipsi_lung.AsymmetricMargin(margins_ipsi);

            //perform a boolean operation of subtraction to shield ipsilateral lung
            ptv.SegmentVolume = ptv.Sub(ipsiL_placeholder);
            ss.RemoveStructure(ipsiL_placeholder);

            //Create a temp structure with 5mm outer matrgins to exclude any overlap between PTV and heart for planning. 
            Structure heart_placeholder = ss.AddStructure("DOSE_REGION", "__heart_PH");
            var margins_heart = new AxisAlignedMargins(StructureMarginGeometry.Outer, 5, 5, 5, 5, 5, 5);
            heart_placeholder.SegmentVolume = heart.AsymmetricMargin(margins_heart);

            //perform a boolean operation of subtraction to shield heart
            ptv.SegmentVolume = ptv.Sub(heart_placeholder);
            ss.RemoveStructure(heart_placeholder);
        }
        public void NormalizePlanToBodyMax(ExternalPlanSetup planSetup, Structure body)
        {
            var prescriptionDose = planSetup.TotalDose;
            var doseGrid = planSetup.Dose;

            var maximumDose = planSetup.GetDoseAtVolume(body, 0, VolumePresentation.AbsoluteCm3, DoseValuePresentation.Absolute).Dose; // GetDoseAtVolume(body, 0.0, DoseValuePresentation.Absolute).Dose;
            var scalingFactor = prescriptionDose / maximumDose;

            //doseGrid.Scale(scalingFactor);
            //planSetup.SetDoseGrid(doseGrid);
        }


    public static string GetNewPlanName(Course crs, string proposedName, int maxLength)
        {
            string name = string.Empty;

            for (int n = 1; n < 100; n++)
            {
                name = $"{proposedName}_{n}";
                if (crs.PlanSetups.Count(p => p.Id == name) == 0) // If we don't find that name
                {
                    break; // We found a usable name
                }
                else if (n == 99)
                {
                    throw new Exception("Maximum new plan index reached (99)");
                }

            }

            // Check name is not too long
            if (name.Length > maxLength)
            {
                throw new Exception($"Proposed name {name} exceeds the max length: {maxLength}");
            }

            return name;
        }
        public static void copy_beam(Beam bm, List<double> msws, bool delete_original = false, ExternalPlanSetup new_plan = null, string technique_id = "ARC")
        {
            //(string primary_fluence_mode, string energy_mode_id) = GetFluenceEnergyMode(bm);
            var unpack_getFluenceEnergyMode = GetFluenceEnergyMode(bm);
            string primary_fluence_mode = unpack_getFluenceEnergyMode.Item1;
            string energy_mode_id = unpack_getFluenceEnergyMode.Item2;

            // ASSERT
            if (!new String[] { "", "FFF", "SRS" }.Contains(primary_fluence_mode))
            {
                throw new Exception($"Primary fluence mode {primary_fluence_mode} not one of the valid options");
            }

            var angles = Utils.GetBeamAngles(bm);

            var gantry_angles = angles.Item1;
            var col_angles = angles.Item2;
            var couch_angles = angles.Item3;
            var cps = angles.Item4;

            var ebmp = new ExternalBeamMachineParameters(
                bm.TreatmentUnit.Id,
                energy_mode_id,
                bm.DoseRate,
                technique_id,
                primary_fluence_mode
            );

            ExternalPlanSetup plan;
            if (new_plan == null)
            {
                plan = (ExternalPlanSetup)bm.Plan;
            }
            else
            {
                plan = new_plan;
            }

            var new_bm = plan.AddVMATBeam(
                ebmp,
                msws,
                col_angles.First(),
                gantry_angles.First(),
                gantry_angles.Last(),
                bm.GantryDirection,
                couch_angles.First(),
                bm.IsocenterPosition
                );

            var edits_new = new_bm.GetEditableParameters();
            var cps_new = edits_new.ControlPoints.ToList();

            for (int j = 0; j < cps_new.Count(); j++)
            {
                cps_new[j].JawPositions = cps[j].JawPositions;
                cps_new[j].LeafPositions = cps[j].LeafPositions;
            }

            new_bm.ApplyParameters(edits_new);
            new_bm.Id = bm.Id; //+ "_new"; // Truncate and add 'new' to the name

            // Delete original beam if it's called for
            if (delete_original)
            {
                var orig_plan = bm.Plan as ExternalPlanSetup;
                orig_plan.RemoveBeam(bm);
            }

        }

        /// <summary>
        /// Compute the separation (in mm) between the entry points of two beams to a structure.
        /// </summary>
        /// <param name="b1">One of the beams entering the structure.</param>
        /// <param name="b2">The other beam entering the structure.</param>
        /// <param name="external">The structure being entered.</param>
        /// <returns>A double representing the separation distance between the entry points of the two beams.</returns>
        public static double ComputeBeamSeparation(Beam b1, Beam b2, Structure external)
        {
            VVector direction1 = DirectionTowardSource(b1);
            VVector direction2 = DirectionTowardSource(b2);
            VVector results1 = GetStructureEntryPoint(external, direction1, b1.IsocenterPosition);
            VVector results2 = GetStructureEntryPoint(external, direction2, b2.IsocenterPosition);

            //  JAK (2023-06-03): this is a much more condensed way to compute the distance of separation.
            return Math.Round((results1 - results2).Length / 10, 2);
        }

        public static double ComputeBeamSeparationWholeField(Beam b1, Beam b2, Structure external, SIDE side, double z_override=0)
        {
            VVector direction1 = DirectionTowardSource(b1);
            VVector direction2 = DirectionTowardSource(b2);

            var p1 = b1.IsocenterPosition;
            var p2 = b2.IsocenterPosition;

            if (side == SIDE.LEFT)
            {
                p1.x += b1.ControlPoints.First().JawPositions.X1;
                p2.x += b2.ControlPoints.First().JawPositions.X1;
            }
            else
            {
                p1.x += b1.ControlPoints.First().JawPositions.X2;
                p2.x += b2.ControlPoints.First().JawPositions.X2;
            }
            

            if (z_override != 0)
            {
                p1.z = z_override; p2.z = z_override;
            }
                

            VVector results1 = GetStructureEntryPoint(external, direction1, p1);
            VVector results2 = GetStructureEntryPoint(external, direction2, p2);

            //  JAK (2023-06-03): this is a much more condensed way to compute the distance of separation.
            return Math.Round((results1 - results2).Length / 10, 2);
        }

        /// <summary>
        /// Find the beam direction vector pointing towards the source.
        /// </summary>
        /// <param name="beam">The beam to be evaluated.</param>
        /// <returns>A VVector struct containing the coordinate directions.</returns>
        public static VVector DirectionTowardSource(Beam beam)
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
        

        
        public static VVector GetStructureEntryPoint(Structure structure, VVector direction, VVector point)
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

    }
}
