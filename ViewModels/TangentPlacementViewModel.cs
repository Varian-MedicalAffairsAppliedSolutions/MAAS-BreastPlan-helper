using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Prism.Mvvm;
using System.Windows.Input;
using Prism.Commands;
using MAAS_BreastPlan_helper.Models;

namespace MAAS_BreastPlan_helper.ViewModels
{
    // Define enum outside the class but within the namespace
    public enum OptimizationTarget
    {
        IpsilateralLung,
        ContralateralBreast,
        Heart
    }

    public class TangentPlacementViewModel : BindableBase
    {
        private readonly ScriptContext _context;
        private readonly SettingsClass _settings;
        private string _statusMessage = "Ready";
        private bool _statusIsError = false;
        private bool _hasContralateralBreast = false;
        private bool _useDivergenceCorrection = false;
        private Structure _selectedBody;
        private Structure _selectedPTV;
        private Structure _selectedLung;
        private Structure _selectedHeart;
        private Structure _selectedContralateralBreast;
        private string _detectedLaterality = "Unknown";
        private ObservableCollection<Structure> _structures = new ObservableCollection<Structure>();
        
        // Enum for optimization targets - defined ONCE at class level
        private OptimizationTarget _selectedOptimizationTarget = OptimizationTarget.IpsilateralLung;
        
        // Properties for Beam's Eye View visualization
        private bool _isBeamEyeViewVisible = false;
        private UIElement _medialBeamEyeView;
        private UIElement _lateralBeamEyeView;

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public bool StatusIsError
        {
            get => _statusIsError;
            set => SetProperty(ref _statusIsError, value);
        }

        public bool HasContralateralBreast
        {
            get => _hasContralateralBreast;
            set => SetProperty(ref _hasContralateralBreast, value);
        }

        public bool UseDivergenceCorrection
        {
            get => _useDivergenceCorrection;
            set => SetProperty(ref _useDivergenceCorrection, value);
        }
        
        public Structure SelectedBody
        {
            get => _selectedBody;
            set => SetProperty(ref _selectedBody, value);
        }

        public Structure SelectedPTV
        {
            get => _selectedPTV;
            set => SetProperty(ref _selectedPTV, value);
        }

        public Structure SelectedLung
        {
            get => _selectedLung;
            set => SetProperty(ref _selectedLung, value);
        }

        public Structure SelectedHeart
        {
            get => _selectedHeart;
            set => SetProperty(ref _selectedHeart, value);
        }

        public Structure SelectedContralateralBreast
        {
            get => _selectedContralateralBreast;
            set => SetProperty(ref _selectedContralateralBreast, value);
        }

        public string DetectedLaterality
        {
            get => _detectedLaterality;
            set => SetProperty(ref _detectedLaterality, value);
        }

        public string DetectedLateralityEnglish
        {
            get
            {
                switch (DetectedLaterality)
                {
                    case "Direita":
                        return "Right";
                    case "Esquerda":
                        return "Left";
                    default:
                        return "Unknown";
                }
            }
        }

        public ObservableCollection<Structure> Structures
        {
            get => _structures;
            set => SetProperty(ref _structures, value);
        }
        
        public OptimizationTarget SelectedOptimizationTarget
        {
            get => _selectedOptimizationTarget;
            set
            {
                if (SetProperty(ref _selectedOptimizationTarget, value))
                {
                    // Handle any additional logic needed when the optimization target changes
                    // For example, updating UI elements or recalculating fields
                }
            }
        }
        
        public bool IsBeamEyeViewVisible
        {
            get => _isBeamEyeViewVisible;
            set
            {
                if (SetProperty(ref _isBeamEyeViewVisible, value))
                {
                    // Update any dependent properties or trigger commands as needed
                }
            }
        }
        
        public UIElement MedialBeamEyeView
        {
            get => _medialBeamEyeView;
            set => SetProperty(ref _medialBeamEyeView, value);
        }
        
        public UIElement LateralBeamEyeView
        {
            get => _lateralBeamEyeView;
            set => SetProperty(ref _lateralBeamEyeView, value);
        }

        public ICommand CreateTangentsCommand { get; private set; }
        public ICommand ShowBeamEyeViewCommand { get; private set; }

        public TangentPlacementViewModel(ScriptContext context, SettingsClass settings)
        {
            _context = context;
            _settings = settings;
            CreateTangentsCommand = new RelayCommand(ExecuteTangentPlacement);
            ShowBeamEyeViewCommand = new RelayCommand(ShowBeamEyeView);
            Initialize();
        }

        private void Initialize()
        {
            try
            {
                StatusMessage = "Initializing...";
                
                // Load structures
                if (_context?.StructureSet != null)
                {
                    // Create a list of structures with a null option at the top
                    var structuresList = new List<Structure> { null };
                    structuresList.AddRange(_context.StructureSet.Structures);
                    
                    // Set the Structures collection with the null option included
                    Structures = new ObservableCollection<Structure>(structuresList);
                    
                    // Try to detect breast laterality
                    var body = _context.StructureSet.Structures.FirstOrDefault(s => s.DicomType == "EXTERNAL");
                    var ptv = _context.StructureSet.Structures.FirstOrDefault(s => s.Id.ToUpper().Contains("PTV"));
                    
                    if (body != null && ptv != null)
                    {
                        SelectedBody = body; // Auto-select body structure
                        DetectedLaterality = Lateralidade.Lado(body, ptv, _context.StructureSet);
                        
                        // Auto-select structures based on laterality
                        var autoSelectedStructures = AutoSelectStructures(_context.StructureSet, DetectedLaterality);
                        
                        // Ensure we have proper debugging information
                        string debugInfo = "Auto-selected structures:\n";
                        foreach (var structure in autoSelectedStructures)
                        {
                            debugInfo += $"- {structure.Id}\n";
                        }
                        System.Diagnostics.Debug.WriteLine(debugInfo);
                        
                        // Assign structures based on index
                        if (autoSelectedStructures.Count >= 1)
                        {
                            SelectedPTV = autoSelectedStructures[0];
                            System.Diagnostics.Debug.WriteLine($"Set PTV to: {SelectedPTV?.Id}");
                        }
                        
                        if (autoSelectedStructures.Count >= 2)
                        {
                            SelectedLung = autoSelectedStructures[1];
                            System.Diagnostics.Debug.WriteLine($"Set Lung to: {SelectedLung?.Id}");
                        }
                        
                        if (autoSelectedStructures.Count >= 3)
                        {
                            SelectedHeart = autoSelectedStructures[2];
                            System.Diagnostics.Debug.WriteLine($"Set Heart to: {SelectedHeart?.Id}");
                        }
                        
                        if (autoSelectedStructures.Count >= 4)
                        {
                            SelectedContralateralBreast = autoSelectedStructures[3];
                            HasContralateralBreast = true;
                        }
                    }
                }
                
                StatusMessage = "Ready to create tangent fields";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Initialization error: {ex.Message}";
                StatusIsError = true;
            }
        }

        private void ShowBeamEyeView(object parameter)
        {
            try
            {
                // Validate required structures are selected
                if (SelectedPTV == null || SelectedLung == null || SelectedBody == null)
                {
                    StatusMessage = "Please select all required structures first";
                    return;
                }
                
                StatusMessage = "Generating beam's eye view...";
                
                var currentPlan = _context.PlanSetup as ExternalPlanSetup;
                if (currentPlan == null)
                {
                    StatusMessage = "Please load a treatment plan.";
                    return;
                }
                
                // Get energy mode and machine parameters
                var firstBeam = currentPlan.Beams.FirstOrDefault();
                string energyModeId = "6X";
                int doseRate = 600;
                string machineId = "TrueBeam";
                
                if (firstBeam != null)
                {
                    energyModeId = firstBeam.EnergyModeDisplayName;
                    doseRate = firstBeam.DoseRate;
                    machineId = firstBeam.TreatmentUnit.Id;
                }
                
                var ebmp = new ExternalBeamMachineParameters(
                    machineId,
                    energyModeId,
                    doseRate,
                    "STATIC",
                    null
                );
                
                // Calculate isocenter based on PTV
                VVector isocenter = new VVector(
                    Math.Round(SelectedPTV.CenterPoint.x / 10.0f) * 10.0f,
                    Math.Round(SelectedPTV.CenterPoint.y / 10.0f) * 10.0f,
                    Math.Round(SelectedPTV.CenterPoint.z / 10.0f) * 10.0f
                );
                
                // Create temporary beams for visualization
                _context.Patient.BeginModifications();

                // Create a temporary plan for the beam visualization
                ExternalPlanSetup tempPlan = _context.Course.AddExternalPlanSetup(_context.StructureSet);
                string originalPlanId = currentPlan.Id;
                
                // Ensure the temp plan ID doesn't exceed 13 characters
                const int maxPlanIdLength = 13;
                string tempPrefix = "TEMP_";
                
                // Calculate how many characters we can use from the original plan ID
                int availableChars = maxPlanIdLength - tempPrefix.Length;
                string truncatedId = (originalPlanId.Length > availableChars) 
                    ? originalPlanId.Substring(0, availableChars) 
                    : originalPlanId;
                
                tempPlan.Id = tempPrefix + truncatedId;
                
                // Check if machine is Halcyon
                bool isHalcyon = machineId.ToUpper().Contains("HALCYON");
                
                // Create leaf positions array
                int leafCount = 60; // Default for Millennium MLC
                float[,] leafPositions = new float[2, leafCount];
                for (int i = 0; i < leafCount; i++)
                {
                    leafPositions[0, i] = -100.0f;
                    leafPositions[1, i] = 100.0f;
                }
                
                Beam medialBeam = null;
                Beam lateralBeam = null;
                
                // Create beams based on laterality and optimization choice
                try
                {
                    if (DetectedLaterality == "Direita")
                    {
                        if (SelectedOptimizationTarget == OptimizationTarget.ContralateralBreast && HasContralateralBreast)
                        {
                            medialBeam = Campos.TgIntD(tempPlan, ebmp, leafPositions, isocenter, SelectedBody, SelectedPTV, SelectedLung, 
                                true, SelectedContralateralBreast, isHalcyon);
                                
                            lateralBeam = Campos.TgExtD(tempPlan, ebmp, leafPositions, isocenter, SelectedBody, SelectedPTV, SelectedLung, 
                                true, SelectedContralateralBreast, isHalcyon);
                        }
                        else
                        {
                            medialBeam = Campos.TgIntD(tempPlan, ebmp, leafPositions, isocenter, SelectedBody, SelectedPTV, SelectedLung, 
                                false, null, isHalcyon);
                                
                            if (UseDivergenceCorrection)
                            {
                                lateralBeam = Campos.TgExtDComCorrecaoDivergencia(tempPlan, ebmp, leafPositions, isocenter, SelectedBody, 
                                    SelectedPTV, medialBeam, isHalcyon);
                            }
                            else
                            {
                                lateralBeam = Campos.TgExtD(tempPlan, ebmp, leafPositions, isocenter, SelectedBody, SelectedPTV, SelectedLung, 
                                    false, null, isHalcyon);
                            }
                        }
                    }
                    else // "Esquerda"
                    {
                        if (SelectedOptimizationTarget == OptimizationTarget.ContralateralBreast && HasContralateralBreast)
                        {
                            medialBeam = Campos.TgIntE(tempPlan, ebmp, leafPositions, isocenter, SelectedBody, SelectedPTV, SelectedLung, 
                                true, SelectedContralateralBreast, false, null, isHalcyon);
                                
                            lateralBeam = Campos.TgExtE(tempPlan, ebmp, leafPositions, isocenter, SelectedBody, SelectedPTV, SelectedLung, 
                                true, SelectedContralateralBreast, false, null, isHalcyon);
                        }
                        else if (SelectedOptimizationTarget == OptimizationTarget.Heart && SelectedHeart != null)
                        {
                            medialBeam = Campos.TgIntE(tempPlan, ebmp, leafPositions, isocenter, SelectedBody, SelectedPTV, SelectedLung, 
                                false, null, true, SelectedHeart, isHalcyon);
                                
                            lateralBeam = Campos.TgExtE(tempPlan, ebmp, leafPositions, isocenter, SelectedBody, SelectedPTV, SelectedLung, 
                                false, null, true, SelectedHeart, isHalcyon);
                        }
                        else
                        {
                            medialBeam = Campos.TgIntE(tempPlan, ebmp, leafPositions, isocenter, SelectedBody, SelectedPTV, SelectedLung, 
                                false, null, false, null, isHalcyon);
                                
                            if (UseDivergenceCorrection)
                            {
                                lateralBeam = Campos.TgExtEComCorrecaoDivergencia(tempPlan, ebmp, leafPositions, isocenter, SelectedBody, 
                                    SelectedPTV, medialBeam, isHalcyon);
                            }
                            else
                            {
                                lateralBeam = Campos.TgExtE(tempPlan, ebmp, leafPositions, isocenter, SelectedBody, SelectedPTV, SelectedLung, 
                                    false, null, false, null, isHalcyon);
                            }
                        }
                    }
                    
                    // Generate Beam's Eye View
                    MedialBeamEyeView = CreateBeamEyeView(medialBeam, SelectedPTV, SelectedLung, SelectedContralateralBreast, 
                        SelectedHeart, SelectedHeart != null, "Medial Tangent", HasContralateralBreast);
                        
                    LateralBeamEyeView = CreateBeamEyeView(lateralBeam, SelectedPTV, SelectedLung, SelectedContralateralBreast, 
                        SelectedHeart, SelectedHeart != null, "Lateral Tangent", HasContralateralBreast);
                    
                    // Remove temporary plan and beams
                    _context.Course.RemovePlanSetup(tempPlan);
                    
                    IsBeamEyeViewVisible = true;
                    StatusMessage = "Beam's eye view generated successfully.";
                }
                catch (Exception ex)
                {
                    // Cleanup in case of error
                    if (tempPlan != null)
                    {
                        try { _context.Course.RemovePlanSetup(tempPlan); } catch { }
                    }
                    StatusMessage = $"Error generating beam's eye view: {ex.Message}";
                    StatusIsError = true;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                StatusIsError = true;
            }
        }
        
        private UIElement CreateBeamEyeView(Beam beam, Structure ptv, Structure pulmaoipsilateral, Structure mamacontralateral, Structure coracao, bool useHeart, string title, bool hasCB)
        {
            var grid = new Grid
            {
                Width = 400, // Ajuste a largura conforme necessário
                Height = 400, // Ajuste a altura conforme necessário
            };

            // Adiciona o título acima do gráfico
            var titleTextBlock = new TextBlock
            {
                Text = title,
                FontSize = 16,
                FontWeight = FontWeights.Light,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 10) // Espaçamento acima e abaixo do título
            };
            grid.Children.Add(titleTextBlock);

            // Adiciona o Canvas para os pontos e legendas
            var canvas = new Canvas
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            grid.Children.Add(canvas);

            //define os parametros do plot
            Tuple<Canvas, Color, System.Windows.Point[][], string, int>[] Plots =
            {
                Tuple.Create(canvas, Colors.Red, beam.GetStructureOutlines(ptv, true), "PTV", 1),
                Tuple.Create(canvas, Colors.Blue, beam.GetStructureOutlines(pulmaoipsilateral, true), "Ipsilateral Lung", 2)
            };

            if (hasCB)
            {
                Plots = new Tuple<Canvas, Color, System.Windows.Point[][], string, int>[]
                {
                    Tuple.Create(canvas, Colors.Red, beam.GetStructureOutlines(ptv, true), "PTV", 1),
                    Tuple.Create(canvas, Colors.Blue, beam.GetStructureOutlines(pulmaoipsilateral, true), "Ipsilateral Lung", 2),
                    Tuple.Create(canvas, Colors.Orange, beam.GetStructureOutlines(mamacontralateral, true), "Contralateral Breast", 3) // Adiciona a mama contralateral
                };
            }

            if (useHeart)
            {
                if (hasCB)
                {
                    Plots = new Tuple<Canvas, Color, System.Windows.Point[][], string, int>[]
                    {
                        Tuple.Create(canvas, Colors.Red, beam.GetStructureOutlines(ptv, true), "PTV", 1),
                        Tuple.Create(canvas, Colors.Blue, beam.GetStructureOutlines(pulmaoipsilateral, true), "Ipsilateral Lung", 2),
                        Tuple.Create(canvas, Colors.Orange, beam.GetStructureOutlines(mamacontralateral, true), "Contralateral Breast", 3), // Adiciona a mama contralateral
                        Tuple.Create(canvas, Colors.Green, beam.GetStructureOutlines(coracao, true), "Heart", 4)
                    };
                }
                else
                {
                    Plots = new Tuple<Canvas, Color, System.Windows.Point[][], string, int>[]
                    {
                        Tuple.Create(canvas, Colors.Red, beam.GetStructureOutlines(ptv, true), "PTV", 1),
                        Tuple.Create(canvas, Colors.Blue, beam.GetStructureOutlines(pulmaoipsilateral, true), "Ipsilateral Lung", 2),
                        Tuple.Create(canvas, Colors.Green, beam.GetStructureOutlines(coracao, true), "Heart", 3)
                    };
                }
            }

            //plot das distribicoes
            foreach (var p in Plots)
            {
                AddLinearInterpolation(p.Item1, p.Item2, p.Item3, p.Item4, p.Item5);
            }

            return grid;
        }
        
        private void AddLinearInterpolation(Canvas canvas, Color color, System.Windows.Point[][] points, string label, int n)
        {
            double maxY = points.SelectMany(p => p).Max(p => p.Y); // Encontrar a coordenada Y maxima
            double minY = points.SelectMany(p => p).Min(p => p.Y); // Encontrar a coordenada Y maxima

            Polyline polyline = new Polyline
            {
                Stroke = new SolidColorBrush(color),
                StrokeThickness = 1.5 // Ajuste a espessura da linha conforme necessário
            };

            foreach (var structurePoints in points)
            {
                foreach (var p in structurePoints)
                {
                    // Cria uma cópia da variável p 
                    var pointCopy = new System.Windows.Point(p.X, p.Y);

                    // Inverte a orientação do eixo Y
                    pointCopy.Y *= -1;

                    // Adiciona o ponto à polyline
                    polyline.Points.Add(pointCopy);
                }
            }

            canvas.Children.Add(polyline);

            // Adiciona a legenda
            var legend = new TextBlock
            {
                Text = label,
                Foreground = new SolidColorBrush(color),
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            //Ajusta a posicao horizontal da legenda em funcao da largura do canvas
            Canvas.SetLeft(legend, canvas.ActualWidth + 10 * 450 / 6);

            // Ajusta a posição vertical da legenda para evitar sobreposição
            Canvas.SetTop(legend, canvas.ActualHeight + n * 15);

            canvas.Children.Add(legend);
        }

        private void ExecuteTangentPlacement(object parameter)
        {
            try
            {
                StatusMessage = "Creating tangent fields...";
                
                if (_context?.Patient == null || _context.StructureSet == null)
                {
                    StatusMessage = "Please load a patient and structure set first.";
                    return;
                }

                var currentPlan = _context.PlanSetup as ExternalPlanSetup;
                if (currentPlan == null)
                {
                    StatusMessage = "Please load a treatment plan.";
                    return;
                }
                
                // Validate required structures are selected
                if (SelectedBody == null)
                {
                    StatusMessage = "Please select a body structure.";
                    return;
                }
                
                if (SelectedPTV == null)
                {
                    StatusMessage = "Please select a PTV structure.";
                    return;
                }
                
                if (SelectedLung == null)
                {
                    StatusMessage = "Please select an ipsilateral lung structure.";
                    return;
                }

                // Begin modifications to create a new plan
                _context.Patient.BeginModifications();
                
                // Create a new plan with name based on original plan
                string originalPlanId = currentPlan.Id;
                string baseId;
                
                // Plan IDs without revision number have a maximum length of 13 characters
                // We need to ensure we have room for the "_#" suffix (at least 2 characters)
                const int maxPlanIdLength = 13;
                const int suffixSpaceNeeded = 2; // For "_1", "_2", etc.
                
                // If the original ID is too long, truncate it
                if (originalPlanId.Length > maxPlanIdLength - suffixSpaceNeeded)
                {
                    baseId = originalPlanId.Substring(0, maxPlanIdLength - suffixSpaceNeeded);
                }
                else
                {
                    baseId = originalPlanId;
                }
                
                string newPlanId = baseId;
                int counter = 1;
                
                // Make sure plan ID is unique by adding a counter suffix
                while (_context.Course.ExternalPlanSetups.Any(p => p.Id == newPlanId))
                {
                    newPlanId = $"{baseId}_{counter}";
                    
                    // If adding the counter made the ID too long, truncate the base ID further
                    if (newPlanId.Length > maxPlanIdLength)
                    {
                        // Recalculate how much space we need for the suffix (could be "_10", "_11", etc.)
                        int counterDigits = counter.ToString().Length;
                        int spaceNeeded = counterDigits + 1; // +1 for the underscore
                        baseId = originalPlanId.Substring(0, maxPlanIdLength - spaceNeeded);
                        newPlanId = $"{baseId}_{counter}";
                    }
                    
                    counter++;
                }
                
                // Create the new plan
                ExternalPlanSetup newPlan = _context.Course.AddExternalPlanSetup(_context.StructureSet);
                newPlan.Id = newPlanId;

                // Copy some basic properties from the current plan
                try
                {
                    // Try to copy machine, energy settings from current plan
                    // Use null-coalescing operator to handle nullable types
                    int fractions = currentPlan.NumberOfFractions ?? 1;  // Default to 1 fraction if null
                    newPlan.SetPrescription(fractions, currentPlan.DosePerFraction, 100.0);
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Note: Could not copy prescription settings: {ex.Message}";
                    StatusIsError = true;
                }

                // Get energy mode and machine parameters from current plan
                var firstBeam = currentPlan.Beams.FirstOrDefault();
                string energyModeId = "6X";
                int doseRate = 600;
                string machineId = "TrueBeam";
                
                if (firstBeam != null)
                {
                    energyModeId = firstBeam.EnergyModeDisplayName;
                    doseRate = firstBeam.DoseRate;
                    machineId = firstBeam.TreatmentUnit.Id;
                }
                
                var ebmp = new ExternalBeamMachineParameters(
                    machineId,
                    energyModeId,
                    doseRate,
                    "STATIC",
                    null
                );
                
                // Calculate isocenter based on PTV
                VVector isocenter = new VVector(
                    Math.Round(SelectedPTV.CenterPoint.x / 10.0f) * 10.0f,
                    Math.Round(SelectedPTV.CenterPoint.y / 10.0f) * 10.0f,
                    Math.Round(SelectedPTV.CenterPoint.z / 10.0f) * 10.0f
                );
                
                // Create default leaf positions
                int leafCount = 60; // Default for Millennium MLC
                float[,] leafPositions = new float[2, leafCount];
                for (int i = 0; i < leafCount; i++)
                {
                    leafPositions[0, i] = -100.0f;
                    leafPositions[1, i] = 100.0f;
                }
                
                // Check if machine is Halcyon
                bool isHalcyon = machineId.ToUpper().Contains("HALCYON");
                
                // Create beams based on laterality, optimization target, and divergence correction
                if (DetectedLaterality == "Direita")
                {
                    Beam tgInt;
                    Beam tgExt;
                    
                    if (SelectedOptimizationTarget == OptimizationTarget.ContralateralBreast && HasContralateralBreast && SelectedContralateralBreast != null)
                    {
                        tgInt = Campos.TgIntD(newPlan, ebmp, leafPositions, isocenter, SelectedBody, SelectedPTV, SelectedLung, 
                            true, SelectedContralateralBreast, isHalcyon);
                            
                        tgExt = Campos.TgExtD(newPlan, ebmp, leafPositions, isocenter, SelectedBody, SelectedPTV, SelectedLung, 
                            true, SelectedContralateralBreast, isHalcyon);
                    }
                    else
                    {
                        tgInt = Campos.TgIntD(newPlan, ebmp, leafPositions, isocenter, SelectedBody, SelectedPTV, SelectedLung, 
                            false, null, isHalcyon);
                            
                        if (UseDivergenceCorrection)
                        {
                            tgExt = Campos.TgExtDComCorrecaoDivergencia(newPlan, ebmp, leafPositions, isocenter, SelectedBody, 
                                SelectedPTV, tgInt, isHalcyon);
                        }
                        else
                        {
                            tgExt = Campos.TgExtD(newPlan, ebmp, leafPositions, isocenter, SelectedBody, SelectedPTV, SelectedLung, 
                                false, null, isHalcyon);
                        }
                    }
                    
                    // Set beam names
                    tgInt.Id = "Med";
                    tgExt.Id = "Lat";
                }
                else // "Esquerda"
                {
                    Beam tgInt;
                    Beam tgExt;
                    
                    if (SelectedOptimizationTarget == OptimizationTarget.ContralateralBreast && HasContralateralBreast && SelectedContralateralBreast != null)
                    {
                        tgInt = Campos.TgIntE(newPlan, ebmp, leafPositions, isocenter, SelectedBody, SelectedPTV, SelectedLung, 
                            true, SelectedContralateralBreast, false, null, isHalcyon);
                            
                        tgExt = Campos.TgExtE(newPlan, ebmp, leafPositions, isocenter, SelectedBody, SelectedPTV, SelectedLung, 
                            true, SelectedContralateralBreast, false, null, isHalcyon);
                    }
                    else if (SelectedOptimizationTarget == OptimizationTarget.Heart && SelectedHeart != null)
                    {
                        tgInt = Campos.TgIntE(newPlan, ebmp, leafPositions, isocenter, SelectedBody, SelectedPTV, SelectedLung, 
                            false, null, true, SelectedHeart, isHalcyon);
                            
                        tgExt = Campos.TgExtE(newPlan, ebmp, leafPositions, isocenter, SelectedBody, SelectedPTV, SelectedLung, 
                            false, null, true, SelectedHeart, isHalcyon);
                    }
                    else
                    {
                        tgInt = Campos.TgIntE(newPlan, ebmp, leafPositions, isocenter, SelectedBody, SelectedPTV, SelectedLung, 
                            false, null, false, null, isHalcyon);
                            
                        if (UseDivergenceCorrection)
                        {
                            tgExt = Campos.TgExtEComCorrecaoDivergencia(newPlan, ebmp, leafPositions, isocenter, SelectedBody, 
                                SelectedPTV, tgInt, isHalcyon);
                        }
                        else
                        {
                            tgExt = Campos.TgExtE(newPlan, ebmp, leafPositions, isocenter, SelectedBody, SelectedPTV, SelectedLung, 
                                false, null, false, null, isHalcyon);
                        }
                    }
                    
                    // Set beam names
                    tgInt.Id = "Med";
                    tgExt.Id = "Lat";
                }
                
                StatusMessage = $"Tangent fields created successfully in new plan '{newPlan.Id}'.";
                
                // Show message box with success message
                MessageBox.Show($"Tangent fields have been created successfully in the new plan '{newPlan.Id}'.", 
                                "Tangent Placement Complete", 
                                MessageBoxButton.OK, 
                                MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                StatusIsError = true;
            }
        }

        // RelayCommand implementation for commands
        public class RelayCommand : ICommand
        {
            private readonly Action<object> _execute;
            private readonly Predicate<object> _canExecute;

            public RelayCommand(Action<object> execute, Predicate<object> canExecute = null)
            {
                _execute = execute ?? throw new ArgumentNullException(nameof(execute));
                _canExecute = canExecute;
            }

            public bool CanExecute(object parameter) => _canExecute == null || _canExecute(parameter);
            public void Execute(object parameter) => _execute(parameter);

            public event EventHandler CanExecuteChanged
            {
                add => CommandManager.RequerySuggested += value;
                remove => CommandManager.RequerySuggested -= value;
            }
        }
        
        private List<Structure> AutoSelectStructures(StructureSet ss, string lateralidade)
        {
            try
            {
                List<Structure> structures = new List<Structure>();
                var sortedStructures = ss.Structures.OrderBy(s => s.Id);

                // First identify heart to exclude it from other matches
                var heart = sortedStructures.FirstOrDefault(s => 
                    s.Id.ToUpper().Contains("CORACAO") || 
                    s.Id.ToUpper().Contains("HEART") || 
                    s.Id.ToUpper().Contains("CARDIAC"));
                
                // Identify PTV structure (target)
                var ptv = sortedStructures.FirstOrDefault(s => s.Id.ToUpper().Contains("PTV") && s.Id.ToUpper().Contains(lateralidade == "Esquerda" ? "ESQ" : "DIR"));
                if (ptv == null)
                    ptv = sortedStructures.FirstOrDefault(s => s.Id.ToUpper().Contains("PTV"));
                
                // Identify ipsilateral lung based on laterality with improved matching
                Structure ipsiLung = null;
                
                // First check for "ipsi" naming convention
                ipsiLung = sortedStructures.FirstOrDefault(s => 
                    (s.Id.ToUpper().Contains("PULMAO") || s.Id.ToUpper().Contains("LUNG")) && 
                    s.Id.ToUpper().Contains("IPSI"));
                
                // If not found, use the standard approach based on laterality
                if (ipsiLung == null)
                {
                    var lungs = sortedStructures.Where(s => 
                        (s.Id.ToUpper().Contains("PULMAO") || s.Id.ToUpper().Contains("LUNG")) && 
                        s.Id != heart?.Id).ToList(); // Avoid selecting heart as lung
                    
                    if (lateralidade == "Direita")
                    {
                        // For right breast, select right lung
                        ipsiLung = lungs.FirstOrDefault(s => 
                            s.Id.ToUpper().Contains("DIR") || 
                            s.Id.ToUpper().Contains("RIGHT") ||
                            (s.CenterPoint.x <= 0 && !s.Id.ToUpper().Contains("ESQ") && !s.Id.ToUpper().Contains("LEFT")));
                    }
                    else // "Esquerda"
                    {
                        // For left breast, select left lung
                        ipsiLung = lungs.FirstOrDefault(s => 
                            s.Id.ToUpper().Contains("ESQ") || 
                            s.Id.ToUpper().Contains("LEFT") ||
                            (s.CenterPoint.x > 0 && !s.Id.ToUpper().Contains("DIR") && !s.Id.ToUpper().Contains("RIGHT")));
                    }
                }
                
                // Auto-select contralateral breast with improved matching
                Structure contraBreast = null;
                
                // First check for "contra" naming convention
                contraBreast = sortedStructures.FirstOrDefault(s => 
                    (s.Id.ToUpper().Contains("MAMA") || s.Id.ToUpper().Contains("BREAST")) && 
                    s.Id.ToUpper().Contains("CONTRA"));
                
                // If not found, use the standard approach based on laterality
                if (contraBreast == null)
                {
                    if (lateralidade == "Direita")
                    {
                        // For right breast, select left breast
                        contraBreast = sortedStructures.FirstOrDefault(s => 
                            (s.Id.ToUpper().Contains("BREAST") && (s.Id.ToUpper().Contains("LEFT") || s.Id.ToUpper().Contains("ESQ"))) ||
                            (s.Id.ToUpper().Contains("MAMA") && (s.Id.ToUpper().Contains("ESQ") || s.Id.ToUpper().Contains("LEFT"))));
                    }
                    else // "Esquerda"
                    {
                        // For left breast, select right breast
                        contraBreast = sortedStructures.FirstOrDefault(s => 
                            (s.Id.ToUpper().Contains("BREAST") && (s.Id.ToUpper().Contains("RIGHT") || s.Id.ToUpper().Contains("DIR"))) ||
                            (s.Id.ToUpper().Contains("MAMA") && (s.Id.ToUpper().Contains("DIR") || s.Id.ToUpper().Contains("RIGHT"))));
                    }
                }
                
                // Add found structures to the list in order
                if (ptv != null) structures.Add(ptv);
                if (ipsiLung != null) structures.Add(ipsiLung);
                if (heart != null) structures.Add(heart);
                if (contraBreast != null) structures.Add(contraBreast);
                
                System.Diagnostics.Debug.WriteLine($"AutoSelectStructures results:");
                System.Diagnostics.Debug.WriteLine($"- PTV: {ptv?.Id}");
                System.Diagnostics.Debug.WriteLine($"- Ipsilateral Lung: {ipsiLung?.Id}");
                System.Diagnostics.Debug.WriteLine($"- Heart: {heart?.Id}");
                System.Diagnostics.Debug.WriteLine($"- Contralateral Breast: {contraBreast?.Id}");
                
                return structures;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in AutoSelectStructures: {ex.Message}");
                return new List<Structure>();
            }
        }
        
        // Keep the original struct/class definitions, just keeping everything the same
        public struct Point
        {
            public double X;
            public double Y;

            public Point(double x, double y)
            {
                X = x;
                Y = y;
            }
        }
        
        public class Polygon
        {
            private List<Point> vertices;

            public Polygon(List<Point> vertices)
            {
                this.vertices = vertices;
            }

            public bool IsPointInside(double coordenadax, double coordenaday)
            {
                int count = 0;
                int n = vertices.Count;

                for (int i = 0, j = n - 1; i < n; j = i++)
                {
                    if ((vertices[i].Y > coordenaday) != (vertices[j].Y > coordenaday) &&
                        coordenadax < (vertices[j].X - vertices[i].X) * (coordenaday - vertices[i].Y) / (vertices[j].Y - vertices[i].Y) + vertices[i].X)
                    {
                        count++;
                    }
                }

                return count % 2 == 1;
            }

            public static Polygon CreateNewPolygon(Structure structure, Beam beam)
            {
                //acessa as coordenadas da estrutura
                var outline = beam.GetStructureOutlines(structure, true);

                //cria a lista para a estrutura                
                List<Point> Est = new List<Point>();

                for (int i = 0; i < outline[0].Count(); i++)
                {
                    //adiciona os pontos a lista
                    Est.Add(new Point(outline[0][i].X, outline[0][i].Y));
                }

                //cria o poligono a partir dos pontos
                return new Polygon(Est);
            }

            public double CalculateIntersectionArea(Structure structure, Beam beam)
            {
                //cria o poligono a partir dos pontos
                Polygon other = CreateNewPolygon(structure, beam);

                List<Point> intersectionVertices = new List<Point>();

                // Encontrar os vértices que estão dentro do outro polígono
                foreach (Point vertex in vertices)
                {
                    if (other.IsPointInside(vertex.X, vertex.Y))
                    {
                        intersectionVertices.Add(vertex);
                    }
                }

                foreach (Point vertex in other.vertices)
                {
                    if (IsPointInside(vertex.X, vertex.Y))
                    {
                        intersectionVertices.Add(vertex);
                    }
                }

                // Se não houver interseção, a área é zero
                if (intersectionVertices.Count < 3)
                {
                    return 0;
                }

                // Ordenar os pontos no sentido anti-horário para formar um polígono convexo
                intersectionVertices = SortVerticesInAntiClockwiseOrder(intersectionVertices);

                // Calcular a área do polígono resultante usando a fórmula de Shoelace
                double area = 0;
                int n = intersectionVertices.Count;
                for (int i = 0; i < n; i++)
                {
                    area += (intersectionVertices[i].X * intersectionVertices[(i + 1) % n].Y - intersectionVertices[(i + 1) % n].X * intersectionVertices[i].Y);
                }
                area = Math.Abs(area) / 2;

                return area;
            }

            // Método para ordenar os pontos no sentido anti-horário usando o algoritmo de Graham Scan
            private List<Point> SortVerticesInAntiClockwiseOrder(List<Point> vertices)
            {
                Point centroid = CalculateCentroid(vertices);

                // Ordenar os pontos pela polar angle em relação ao centroide
                vertices.Sort((p1, p2) => Math.Atan2(p1.Y - centroid.Y, p1.X - centroid.X).CompareTo(Math.Atan2(p2.Y - centroid.Y, p2.X - centroid.X)));

                return vertices;
            }

            // Método para calcular o centroide de um conjunto de pontos
            private Point CalculateCentroid(List<Point> vertices)
            {
                double sumX = vertices.Sum(v => v.X);
                double sumY = vertices.Sum(v => v.Y);
                int count = vertices.Count;

                return new Point(sumX / count, sumY / count);
            }
        }
        public class AngulosTangentes
        {
            public static double GantryTgIntMamaD(ExternalPlanSetup eps, ExternalBeamMachineParameters ebmp, Structure ptv, Structure pulmaoipsi, bool considerCB, Structure mamacontra)
            {
                double areagauss1;
                double areadef1 = double.MaxValue;
                int gt1foar1 = 0;
                int gt1;
                VVector isocenter = new VVector(Math.Round(ptv.CenterPoint.x / 10.0f) * 10.0f, Math.Round(ptv.CenterPoint.y / 10.0f) * 10.0f, Math.Round(ptv.CenterPoint.z / 10.0f) * 10.0f);
                for (gt1 = 30; gt1 <= 75; gt1++)
                {
                    //criar o feixe
                    Beam imrt1 = eps.AddStaticBeam(ebmp, new VRect<double>(-100, -100, 100, 100), 0, gt1, 0, isocenter);

                    //Area Interseccao pulmao
                    //cria o poligono a partir dos pontos
                    Polygon polPTV = Polygon.CreateNewPolygon(ptv, imrt1);
                    //calcula a area de interseccao
                    areagauss1 = polPTV.CalculateIntersectionArea(pulmaoipsi, imrt1);
                    //armazenamento area e obtencao angulo do gantry
                    if (areagauss1 < areadef1)
                    {
                        areadef1 = areagauss1;
                        gt1foar1 = gt1;
                    }
                    //remocao do feixe apos os testes
                    eps.RemoveBeam(imrt1);
                }
                if (considerCB)
                {
                    int control = gt1foar1;
                    for (gt1 = control - 10; gt1 <= control; gt1++)
                    {
                        //criar o feixe
                        Beam imrt1 = eps.AddStaticBeam(ebmp, new VRect<double>(-100, -100, 100, 100), 0, gt1, 0, isocenter);

                        //Area Interseccao pulmao
                        //cria o poligono a partir dos pontos
                        Polygon polPTV = Polygon.CreateNewPolygon(ptv, imrt1);
                        //calcula a area de interseccao
                        areagauss1 = polPTV.CalculateIntersectionArea(mamacontra, imrt1);
                        //armazenamento area e obtencao angulo do gantry
                        if (Math.Round(areagauss1) <= Math.Round(areadef1))
                        {
                            areadef1 = areagauss1;
                            gt1foar1 = gt1;
                        }
                        //remocao do feixe apos os testes
                        eps.RemoveBeam(imrt1);
                    }

                }
                return gt1foar1;
            }
            public static double GantryTgExtMamaD(ExternalPlanSetup eps, ExternalBeamMachineParameters ebmp, Structure ptv, Structure pulmaoipsi, bool considerCB, Structure mamacontra)
            {
                double areagauss2;
                double areadef2 = double.MaxValue;
                int gt2foar2 = 0;
                int gt2;
                VVector isocenter = new VVector(Math.Round(ptv.CenterPoint.x / 10.0f) * 10.0f, Math.Round(ptv.CenterPoint.y / 10.0f) * 10.0f, Math.Round(ptv.CenterPoint.z / 10.0f) * 10.0f);
                for (gt2 = 200; gt2 <= 245; gt2++)
                {
                    //criar o feixe
                    Beam imrt2 = eps.AddStaticBeam(ebmp, new VRect<double>(-100, -100, 100, 100), 0, gt2, 0, isocenter);
                    //Area Interseccao pulmao
                    //cria o poligono a partir dos pontos
                    Polygon polPulmao = Polygon.CreateNewPolygon(pulmaoipsi, imrt2);
                    //calcula a area de interseccao
                    areagauss2 = polPulmao.CalculateIntersectionArea(ptv, imrt2);
                    //armazenamento area e obtencao angulo do gantry
                    if (areagauss2 < areadef2)
                    {
                        areadef2 = areagauss2;
                        gt2foar2 = gt2;
                    }
                    //remocao do feixe apos os testes
                    eps.RemoveBeam(imrt2);
                }
                if (considerCB)
                {
                    int control = gt2foar2;
                    for (gt2 = control - 10; gt2 <= control; gt2++)
                    {
                        //criar o feixe
                        Beam imrt2 = eps.AddStaticBeam(ebmp, new VRect<double>(-100, -100, 100, 100), 0, gt2, 0, isocenter);
                        //Area Interseccao pulmao
                        //cria o poligono a partir dos pontos
                        Polygon polMama = Polygon.CreateNewPolygon(mamacontra, imrt2);
                        //calcula a area de interseccao
                        areagauss2 = polMama.CalculateIntersectionArea(ptv, imrt2);
                        //armazenamento area e obtencao angulo do gantry
                        if (Math.Round(areagauss2) <= Math.Round(areadef2))
                        {
                            areadef2 = areagauss2;
                            gt2foar2 = gt2;
                        }
                        //remocao do feixe apos os testes
                        eps.RemoveBeam(imrt2);
                    }
                }
                return gt2foar2;
            }
            public static double GantryTgExtMamaDDivergencia(Beam TgInt, bool isHalcyon)
            {
                double gantryTgInt = TgInt.ControlPoints[0].GantryAngle;
                double fieldSize = double.MinValue;
                double fieldSizeTemp;
                float[,] leafPositions = TgInt.ControlPoints[0].LeafPositions;
                int nLeafs = TgInt.ControlPoints[0].LeafPositions.Length / 2;
                if (!isHalcyon)
                {
                    fieldSize = Math.Abs(TgInt.ControlPoints[0].JawPositions.X1 - TgInt.ControlPoints[0].JawPositions.X2);
                }
                else
                {
                    for (int i = 0; i < nLeafs; i++)
                    {
                        fieldSizeTemp = Math.Abs(leafPositions[0, i] - leafPositions[1, i]);
                        if (fieldSizeTemp > fieldSize) { fieldSize = fieldSizeTemp; }
                    }
                }
                //correcao da divergencia
                double delta = (Math.Atan(fieldSize / TgInt.SSD) * 180) / Math.PI;
                double gt2foar2 = gantryTgInt + 180 - delta;

                return gt2foar2;
            }
            public static double GantryTgIntMamaE(ExternalPlanSetup eps, ExternalBeamMachineParameters ebmp, Structure ptv, Structure pulmaoipsi, bool considerCB, Structure mamacontra, bool considerHeart, Structure coracao)
            {
                double areagauss1;
                double areadef1 = double.MaxValue;
                int gt1foar1 = 0;
                int gt1;
                VVector isocenter = new VVector(Math.Round(ptv.CenterPoint.x / 10.0f) * 10.0f, Math.Round(ptv.CenterPoint.y / 10.0f) * 10.0f, Math.Round(ptv.CenterPoint.z / 10.0f) * 10.0f);
                for (gt1 = 295; gt1 <= 340; gt1++)
                {
                    //criar o feixe
                    Beam imrt1 = eps.AddStaticBeam(ebmp, new VRect<double>(-100, -100, 100, 100), 0, gt1, 0, isocenter);
                    //Area Interseccao pulmao
                    //cria o poligono a partir dos pontos
                    Polygon polPulmao = Polygon.CreateNewPolygon(pulmaoipsi, imrt1);
                    //calcula a area de interseccao
                    areagauss1 = polPulmao.CalculateIntersectionArea(ptv, imrt1);
                    //armazenamento area e obtencao angulo do gantry
                    if (areagauss1 < areadef1)
                    {
                        areadef1 = areagauss1;
                        gt1foar1 = gt1;
                    }
                    //remocao do feixe apos os testes
                    eps.RemoveBeam(imrt1);
                }
                if (considerCB)
                {
                    for (gt1 = gt1foar1; gt1 <= 340; gt1++)
                    {
                        //criar o feixe
                        Beam imrt1 = eps.AddStaticBeam(ebmp, new VRect<double>(-100, -100, 100, 100), 0, gt1, 0, isocenter);
                        //Area Interseccao pulmao
                        //cria o poligono a partir dos pontos
                        Polygon polPulmao = Polygon.CreateNewPolygon(mamacontra, imrt1);
                        //calcula a area de interseccao
                        areagauss1 = polPulmao.CalculateIntersectionArea(ptv, imrt1);
                        //armazenamento area e obtencao angulo do gantry
                        if (areagauss1 < areadef1)
                        {
                            areadef1 = areagauss1;
                            gt1foar1 = gt1;
                        }
                        //remocao do feixe apos os testes
                        eps.RemoveBeam(imrt1);
                    }
                }
                if (considerHeart)
                {
                    for (gt1 = gt1foar1; gt1 <= 340; gt1++)
                    {
                        //criar o feixe
                        Beam imrt1 = eps.AddStaticBeam(ebmp, new VRect<double>(-100, -100, 100, 100), 0, gt1, 0, isocenter);
                        //Area Interseccao pulmao
                        //cria o poligono a partir dos pontos
                        Polygon polPulmao = Polygon.CreateNewPolygon(coracao, imrt1);
                        //calcula a area de interseccao
                        areagauss1 = polPulmao.CalculateIntersectionArea(ptv, imrt1);
                        //armazenamento area e obtencao angulo do gantry
                        if (areagauss1 < areadef1)
                        {
                            areadef1 = areagauss1;
                            gt1foar1 = gt1;
                        }
                        //remocao do feixe apos os testes
                        eps.RemoveBeam(imrt1);
                    }
                }
                return gt1foar1;
            }
            public static double GantryTgExtMamaE(ExternalPlanSetup eps, ExternalBeamMachineParameters ebmp, Structure ptv, Structure pulmaoipsi, bool considerCB, Structure mamacontra, bool considerHeart, Structure coracao)
            {
                double areagauss2;
                double areadef2 = double.MaxValue;
                int gt2foar2 = 0;
                int gt2;
                VVector isocenter = new VVector(Math.Round(ptv.CenterPoint.x / 10.0f) * 10.0f, Math.Round(ptv.CenterPoint.y / 10.0f) * 10.0f, Math.Round(ptv.CenterPoint.z / 10.0f) * 10.0f);
                for (gt2 = 110; gt2 <= 155; gt2++)
                {
                    //criar o feixe
                    Beam imrt2 = eps.AddStaticBeam(ebmp, new VRect<double>(-100, -100, 100, 100), 0, gt2, 0, isocenter);
                    //Area Interseccao pulmao
                    //cria o poligono a partir dos pontos
                    Polygon polPTV = Polygon.CreateNewPolygon(ptv, imrt2);
                    //calcula a area de interseccao
                    areagauss2 = polPTV.CalculateIntersectionArea(pulmaoipsi, imrt2);
                    //armazenamento area e obtencao angulo do gantry
                    if (areagauss2 < areadef2)
                    {
                        areadef2 = areagauss2;
                        gt2foar2 = gt2;
                    }
                    //remocao do feixe apos os testes
                    eps.RemoveBeam(imrt2);
                }
                if (considerCB)
                {
                    for (gt2 = gt2foar2; gt2 <= 155; gt2++)
                    {
                        //criar o feixe
                        Beam imrt2 = eps.AddStaticBeam(ebmp, new VRect<double>(-100, -100, 100, 100), 0, gt2, 0, isocenter);
                        //Area Interseccao pulmao
                        //cria o poligono a partir dos pontos
                        Polygon polPTV = Polygon.CreateNewPolygon(ptv, imrt2);
                        //calcula a area de interseccao
                        areagauss2 = polPTV.CalculateIntersectionArea(mamacontra, imrt2);
                        //armazenamento area e obtencao angulo do gantry
                        if (areagauss2 < areadef2)
                        {
                            areadef2 = areagauss2;
                            gt2foar2 = gt2;
                        }
                        //remocao do feixe apos os testes
                        eps.RemoveBeam(imrt2);
                    }
                }
                if (considerHeart)
                {
                    for (gt2 = gt2foar2; gt2 <= 155; gt2++)
                    {
                        //criar o feixe
                        Beam imrt2 = eps.AddStaticBeam(ebmp, new VRect<double>(-100, -100, 100, 100), 0, gt2, 0, isocenter);
                        //Area Interseccao pulmao
                        //cria o poligono a partir dos pontos
                        Polygon polPTV = Polygon.CreateNewPolygon(ptv, imrt2);
                        //calcula a area de interseccao
                        areagauss2 = polPTV.CalculateIntersectionArea(coracao, imrt2);
                        //armazenamento area e obtencao angulo do gantry
                        if (areagauss2 < areadef2)
                        {
                            areadef2 = areagauss2;
                            gt2foar2 = gt2;
                        }
                        //remocao do feixe apos os testes
                        eps.RemoveBeam(imrt2);
                    }
                }
                return gt2foar2;
            }
            public static double GantryTgExtMamaEDivergencia(Beam TgInt, bool isHalcyon)
            {
                double gantryTgInt = TgInt.ControlPoints[0].GantryAngle;
                double fieldSize = double.MinValue;
                double fieldSizeTemp;
                float[,] leafPositions = TgInt.ControlPoints[0].LeafPositions;
                int nLeafs = TgInt.ControlPoints[0].LeafPositions.Length / 2;
                if (!isHalcyon)
                {
                    fieldSize = Math.Abs(TgInt.ControlPoints[0].JawPositions.X1 - TgInt.ControlPoints[0].JawPositions.X2);
                }
                else
                {
                    for (int i = 0; i < nLeafs; i++)
                    {
                        fieldSizeTemp = Math.Abs(leafPositions[0, i] - leafPositions[1, i]);
                        if (fieldSizeTemp > fieldSize) { fieldSize = fieldSizeTemp; }
                    }
                }
                //correcao da divergencia
                double delta = (Math.Atan(fieldSize / TgInt.SSD) * 180) / Math.PI;
                double gt2foar2 = gantryTgInt - 180 + delta;

                return gt2foar2;
            }
        }
        public class Lateralidade
        {
            public static int GetSlice(double z, StructureSet SS)
            {
                var imageRes = SS.Image.ZRes;
                return Convert.ToInt32((z - SS.Image.Origin.z) / imageRes);
            }
            public static string Lado(Structure body, Structure ptv, StructureSet ss)
            {
                //Pegar indice z do Centerpoint da estCentral	    
                int indice = GetSlice(ptv.CenterPoint.z, ss);

                //Criar lista com pontos do slice do ptv
                IEnumerable<VVector> pt = ptv.MeshGeometry.Positions.Select(e => new VVector(e.X, e.Y, e.Z));
                List<System.Windows.Point> slice = new List<System.Windows.Point>();
                System.Windows.Point point;
                foreach (var ponto in pt)
                {
                    if (Math.Round(ponto.z / 10.0f) * 10.0f == Math.Round((ss.Image.Origin.z + (indice) * ss.Image.ZRes) / 10.0f) * 10.0f)
                    {
                        point = new System.Windows.Point(ponto.x, ponto.y);
                        slice.Add(point);
                    }
                }

                //ponto central do body para comparar
                double ptoXCentral = body.CenterPoint.x;

                //contagem de pontos
                int contE = 0;
                int contD = 0;
                string lat = "";
                foreach (var p in slice)
                {
                    if (p.X < ptoXCentral) { contD++; }
                    else { contE++; }
                }
                if (contD > contE) { lat = "Direita"; }
                else { lat = "Esquerda"; }

                //razao
                return lat;
            }
        }

        public static List<Structure> SelectStructures(StructureSet ss, string lateralidade)
        {
            // Code that might be calling static AutoSelectStructures needs to be updated
            // This is just a sample implementation - make sure it aligns with your actual needs
            List<Structure> selectedStructures = new List<Structure>();

            // Auto-select body
            Structure body = ss.Structures.FirstOrDefault(x => x.DicomType == "EXTERNAL");
            selectedStructures.Add(body);

            // Auto-select structures based on laterality
            var sortedStructures = ss.Structures.OrderBy(s => s.Id);

            // Identify PTV structure
            var ptv = sortedStructures.FirstOrDefault(s => s.Id.ToUpper().Contains("PTV") && s.Id.ToUpper().Contains(lateralidade == "Esquerda" ? "ESQ" : "DIR"));
            if (ptv == null)
                ptv = sortedStructures.FirstOrDefault(s => s.Id.ToUpper().Contains("PTV"));
            
            // Identify ipsilateral lung
            var lung = sortedStructures.FirstOrDefault(s => 
                (lateralidade == "Esquerda" && (s.Id.ToUpper().Contains("PULMAO") || s.Id.ToUpper().Contains("LUNG")) && s.Id.ToUpper().Contains("ESQ")) ||
                (lateralidade == "Direita" && (s.Id.ToUpper().Contains("PULMAO") || s.Id.ToUpper().Contains("LUNG")) && s.Id.ToUpper().Contains("DIR")));
            
            // Identify heart (usually relevant for left-sided cases)
            var heart = sortedStructures.FirstOrDefault(s => s.Id.ToUpper().Contains("CORACAO") || s.Id.ToUpper().Contains("HEART"));
            
            // Identify contralateral breast
            var contralateralBreast = sortedStructures.FirstOrDefault(s => 
                (lateralidade == "Esquerda" && (s.Id.ToUpper().Contains("MAMA") || s.Id.ToUpper().Contains("BREAST")) && s.Id.ToUpper().Contains("DIR")) ||
                (lateralidade == "Direita" && (s.Id.ToUpper().Contains("MAMA") || s.Id.ToUpper().Contains("BREAST")) && s.Id.ToUpper().Contains("ESQ")));
            
            // Add found structures to the list
            if (ptv != null) selectedStructures.Add(ptv);
            if (lung != null) selectedStructures.Add(lung);
            if (heart != null) selectedStructures.Add(heart);
            if (contralateralBreast != null) selectedStructures.Add(contralateralBreast);
            
            return selectedStructures;
        }
        private static ComboBox CriarComboBoxComOpcaoNenhuma(StructureSet ss)
        {
            ComboBox comboBox = new ComboBox();
            comboBox.Items.Add("None"); // Default option for no structure
            foreach (var s in ss.Structures)
            {
                comboBox.Items.Add(s);
            }
            comboBox.SelectedIndex = 0; // Set "None" as default
            return comboBox;
        }
        private static void AdicionarComboBox(StackPanel parent, string label, ComboBox comboBox)
        {
            StackPanel stackPanel = new StackPanel { Margin = new Thickness(10) };
            stackPanel.Children.Add(new TextBlock { Text = label });
            stackPanel.Children.Add(comboBox);
            parent.Children.Add(stackPanel);
        }
        private static UserControl CriarGrafico(Beam beam, Structure ptv, Structure pulmaoipsilateral, Structure mamacontralateral, Structure coracao, bool useHeart, string title, bool hasCB)
        {
            var userControl = new UserControl();

            var grid = new Grid
            {
                Width = 450, 
                Height = 450, 
                Margin = new Thickness(10)
            };

            // Adiciona o título acima do gráfico
            var titleTextBlock = new TextBlock
            {
                Text = title,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 5, 0, 15)
            };
            grid.Children.Add(titleTextBlock);

            // Create border for the canvas
            var border = new Border
            {
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(10, 40, 10, 40), // Top margin to account for title
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            grid.Children.Add(border);

            // Adiciona o Canvas para os pontos e legendas
            var canvas = new Canvas
            {
                Width = 380,
                Height = 380,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Background = Brushes.White,
                ClipToBounds = true
            };
            border.Child = canvas;
            
            // Add a center marker
            var centerMarker = new Ellipse
            {
                Width = 4,
                Height = 4,
                Fill = Brushes.Black,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Canvas.SetLeft(centerMarker, canvas.Width / 2 - 2);
            Canvas.SetTop(centerMarker, canvas.Height / 2 - 2);
            canvas.Children.Add(centerMarker);

            //define os parametros do plot
            Tuple<Canvas, Color, System.Windows.Point[][], string, int>[] Plots =
            {
                Tuple.Create(canvas, Colors.Red, beam.GetStructureOutlines(ptv, true), "PTV", 1),
                Tuple.Create(canvas, Colors.Blue, beam.GetStructureOutlines(pulmaoipsilateral, true), "Ipsilateral Lung", 2)
            };

            if (hasCB)
            {
                Plots = new Tuple<Canvas, Color, System.Windows.Point[][], string, int>[]
                {
                    Tuple.Create(canvas, Colors.Red, beam.GetStructureOutlines(ptv, true), "PTV", 1),
                    Tuple.Create(canvas, Colors.Blue, beam.GetStructureOutlines(pulmaoipsilateral, true), "Ipsilateral Lung", 2),
                    Tuple.Create(canvas, Colors.Orange, beam.GetStructureOutlines(mamacontralateral, true), "Contralateral Breast", 3) // Adiciona a mama contralateral
                };
            }

            if (coracao != null)
            {
                if (hasCB)
                {
                    Plots = new Tuple<Canvas, Color, System.Windows.Point[][], string, int>[]
                    {
                        Tuple.Create(canvas, Colors.Red, beam.GetStructureOutlines(ptv, true), "PTV", 1),
                        Tuple.Create(canvas, Colors.Blue, beam.GetStructureOutlines(pulmaoipsilateral, true), "Ipsilateral Lung", 2),
                        Tuple.Create(canvas, Colors.Orange, beam.GetStructureOutlines(mamacontralateral, true), "Contralateral Breast", 3), // Adiciona a mama contralateral
                        Tuple.Create(canvas, Colors.Green, beam.GetStructureOutlines(coracao, true), "Heart", 4)
                    };
                }
                else
                {
                    Plots = new Tuple<Canvas, Color, System.Windows.Point[][], string, int>[]
                    {
                        Tuple.Create(canvas, Colors.Red, beam.GetStructureOutlines(ptv, true), "PTV", 1),
                        Tuple.Create(canvas, Colors.Blue, beam.GetStructureOutlines(pulmaoipsilateral, true), "Ipsilateral Lung", 2),
                        Tuple.Create(canvas, Colors.Orange, beam.GetStructureOutlines(coracao, true), "Heart", 3)
                    };
                }
            }

            //plot das distribicoes
            foreach (var p in Plots)
            {
                AdicionarInterpolacaoLinear(p.Item1, p.Item2, p.Item3, p.Item4, p.Item5);
            }

            userControl.Content = grid;

            return userControl;
        }
        private static void AdicionarInterpolacaoLinear(Canvas canvas, Color color, System.Windows.Point[][] points, string label, int n)
        {
            double maxY = points.SelectMany(p => p).Max(p => p.Y); // Encontrar a coordenada Y maxima
            double minY = points.SelectMany(p => p).Min(p => p.Y); // Encontrar a coordenada Y maxima

            Polyline polyline = new Polyline
            {
                Stroke = new SolidColorBrush(color),
                StrokeThickness = 1.5 // Ajuste a espessura da linha conforme necessário
            };

            foreach (var structurePoints in points)
            {
                foreach (var p in structurePoints)
                {
                    // Cria uma cópia da variável p 
                    var pointCopy = new System.Windows.Point(p.X, p.Y);

                    // Inverte a orientação do eixo Y
                    pointCopy.Y *= -1;

                    // Adiciona o ponto à polyline
                    polyline.Points.Add(pointCopy);
                }
            }

            canvas.Children.Add(polyline);

            // Adiciona a legenda
            var legend = new TextBlock
            {
                Text = label,
                Foreground = new SolidColorBrush(color),
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            //Ajusta a posicao horizontal da legenda em funcao da largura do canvas
            Canvas.SetLeft(legend, canvas.ActualWidth + 10 * 450 / 6);

            // Ajusta a posição vertical da legenda para evitar sobreposição
            Canvas.SetTop(legend, canvas.ActualHeight + n * 15);

            canvas.Children.Add(legend);
        }
        public static void EscolherConfiguracaoCampos(ExternalPlanSetup eps, bool hasCB, double anguloGantryTgInt, double anguloGantryTgExt, ExternalBeamMachineParameters ebmp, VVector isocenter, Structure ptv, Structure pulmaoipsilateral, Structure mamacontralateral, Structure coracao, bool useHeart)
        {

            // Cria o campo
            Beam beamTI = eps.AddStaticBeam(ebmp, new VRect<double>(-100, -100, 100, 100), 0, anguloGantryTgInt, 0, isocenter);
            Beam beamTE = eps.AddStaticBeam(ebmp, new VRect<double>(-100, -100, 100, 100), 0, anguloGantryTgExt, 0, isocenter);

            Window window = new Window
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Title = "Beam's eye view projection:"
            };

            StackPanel stackPanel = new StackPanel();
            window.Content = stackPanel;

            // Cria um StackPanel exclusivo para os gráficos
            StackPanel graficoContainer = new StackPanel();
            graficoContainer.Orientation = Orientation.Horizontal;
            graficoContainer.HorizontalAlignment = HorizontalAlignment.Center;

            // Adiciona os gráficos ao contêiner
            UserControl graficoControl = CriarGrafico(beamTI, ptv, pulmaoipsilateral, mamacontralateral, coracao, coracao != null, "Medial tangent representantion:", hasCB);
            graficoControl.Margin = new Thickness(10, 0, 40, 0);
            graficoContainer.Children.Add(graficoControl);

            UserControl graficoControl2 = CriarGrafico(beamTE, ptv, pulmaoipsilateral, mamacontralateral, coracao, coracao != null, "Lateral tangent representantion:", hasCB);
            graficoControl2.Margin = new Thickness(40, 0, 10, 0);
            graficoContainer.Children.Add(graficoControl2);

            // Adiciona o contêiner de gráficos ao layout principal
            stackPanel.Children.Add(graficoContainer);

            // Remove os campos
            eps.RemoveBeam(beamTI);
            eps.RemoveBeam(beamTE);

            // Adiciona o botão OK
            Button okButton = new Button
            {
                Content = "OK",
                Height = 40,
                Margin = new Thickness(10)
            };
            okButton.Click += (sender, e) => { window.DialogResult = true; };
            stackPanel.Children.Add(okButton);

            // Mostra a janela e espera a interação do usuário
            window.ShowDialog();
        }
        public class Campos
        {
            public static Beam TgIntD(ExternalPlanSetup eps, ExternalBeamMachineParameters ebmp, float[,] leafPositions, VVector isocenter, Structure body, Structure ptv, Structure pulmaoipsi, bool considerCB, Structure mamacontra, bool isHalcyon)
            {
                double angGantry = AngulosTangentes.GantryTgIntMamaD(eps, ebmp, ptv, pulmaoipsi, considerCB, mamacontra);
                //Beam beam = eps.AddStaticBeam(ebmp, new VRect<double>(-100, -100, 100, 100), 0, angGantry, 0, isocenter);
                Beam beam = eps.AddMLCBeam(ebmp, leafPositions, new VRect<double>(-100, -100, 100, 100), 0, angGantry, 0, isocenter);
                //try { beam.FitMLCToStructure(new FitToStructureMargins(25, 5, 5, 5), ptv, true, JawFitting.FitToStructure, OpenLeavesMeetingPoint.OpenLeavesMeetingPoint_Middle, ClosedLeavesMeetingPoint.ClosedLeavesMeetingPoint_Center); }
                //catch { }
                double colAngle = 0;
                if (!isHalcyon)
                {
                    try { beam.FitCollimatorToStructure(new FitToStructureMargins(0, 0, 0, 0), ptv, true, true, true); }
                    catch { }
                    if (beam.ControlPoints[0].CollimatorAngle < 180)
                    {
                        colAngle = beam.ControlPoints[0].CollimatorAngle + 180;
                        eps.RemoveBeam(beam);
                        beam = eps.AddMLCBeam(ebmp, leafPositions, new VRect<double>(-100, -100, 100, 100), colAngle, angGantry, 0, isocenter);
                        try { beam.FitMLCToStructure(new FitToStructureMargins(25, 5, 5, 5), ptv, false, JawFitting.FitToStructure, OpenLeavesMeetingPoint.OpenLeavesMeetingPoint_Middle, ClosedLeavesMeetingPoint.ClosedLeavesMeetingPoint_Center); }
                        catch { }
                    }
                    else
                    {
                        try { beam.FitMLCToStructure(new FitToStructureMargins(25, 5, 5, 5), ptv, false, JawFitting.FitToStructure, OpenLeavesMeetingPoint.OpenLeavesMeetingPoint_Middle, ClosedLeavesMeetingPoint.ClosedLeavesMeetingPoint_Center); }
                        catch { }
                    }
                }
                else
                {
                    try { beam.FitMLCToStructure(new FitToStructureMargins(25, 5, 5, 5), ptv, true, JawFitting.FitToStructure, OpenLeavesMeetingPoint.OpenLeavesMeetingPoint_Middle, ClosedLeavesMeetingPoint.ClosedLeavesMeetingPoint_Center); }
                    catch { }
                }
                return beam;
            }
            public static Beam TgIntE(ExternalPlanSetup eps, ExternalBeamMachineParameters ebmp, float[,] leafPositions, VVector isocenter, Structure body, Structure ptv, Structure pulmaoipsi, bool considerCB, Structure mamacontra, bool considerHeart, Structure coracao, bool isHalcyon)
            {
                double angGantry = AngulosTangentes.GantryTgIntMamaE(eps, ebmp, ptv, pulmaoipsi, considerCB, mamacontra, considerHeart, coracao);
                //Beam beam = eps.AddStaticBeam(ebmp, new VRect<double>(-100, -100, 100, 100), 0, angGantry, 0, isocenter);
                Beam beam = eps.AddMLCBeam(ebmp, leafPositions, new VRect<double>(-100, -100, 100, 100), 0, angGantry, 0, isocenter);
                //try { beam.FitMLCToStructure(new FitToStructureMargins(5, 5, 25, 5), ptv, true, JawFitting.FitToStructure, OpenLeavesMeetingPoint.OpenLeavesMeetingPoint_Middle, ClosedLeavesMeetingPoint.ClosedLeavesMeetingPoint_Center); }
                //catch { }
                double colAngle = 0;
                if (!isHalcyon)
                {
                    try { beam.FitCollimatorToStructure(new FitToStructureMargins(0, 0, 0, 0), ptv, true, true, true); }
                    catch { }
                    if (beam.ControlPoints[0].CollimatorAngle > 180)
                    {
                        colAngle = beam.ControlPoints[0].CollimatorAngle - 180;
                        eps.RemoveBeam(beam);
                        beam = eps.AddMLCBeam(ebmp, leafPositions, new VRect<double>(-100, -100, 100, 100), colAngle, angGantry, 0, isocenter);
                        try { beam.FitMLCToStructure(new FitToStructureMargins(5, 5, 25, 5), ptv, false, JawFitting.FitToStructure, OpenLeavesMeetingPoint.OpenLeavesMeetingPoint_Middle, ClosedLeavesMeetingPoint.ClosedLeavesMeetingPoint_Center); }
                        catch { }
                    }
                    else
                    {
                        try { beam.FitMLCToStructure(new FitToStructureMargins(5, 5, 25, 5), ptv, false, JawFitting.FitToStructure, OpenLeavesMeetingPoint.OpenLeavesMeetingPoint_Middle, ClosedLeavesMeetingPoint.ClosedLeavesMeetingPoint_Center); }
                        catch { }
                    }
                }
                else
                {
                    try { beam.FitMLCToStructure(new FitToStructureMargins(5, 5, 25, 5), ptv, true, JawFitting.FitToStructure, OpenLeavesMeetingPoint.OpenLeavesMeetingPoint_Middle, ClosedLeavesMeetingPoint.ClosedLeavesMeetingPoint_Center); }
                    catch { }
                }
                return beam;
            }
            public static Beam TgExtD(ExternalPlanSetup eps, ExternalBeamMachineParameters ebmp, float[,] leafPositions, VVector isocenter, Structure body, Structure ptv, Structure pulmaoipsi, bool considerCB, Structure mamacontra, bool isHalcyon)
            {
                double angGantry = AngulosTangentes.GantryTgExtMamaD(eps, ebmp, ptv, pulmaoipsi, considerCB, mamacontra);
                //Beam beam = eps.AddStaticBeam(ebmp, new VRect<double>(-100, -100, 100, 100), 0, angGantry, 0, isocenter);
                Beam beam = eps.AddMLCBeam(ebmp, leafPositions, new VRect<double>(-100, -100, 100, 100), 0, angGantry, 0, isocenter);
                //try { beam.FitMLCToStructure(new FitToStructureMargins(5, 5, 25, 5), ptv, true, JawFitting.FitToStructure, OpenLeavesMeetingPoint.OpenLeavesMeetingPoint_Middle, ClosedLeavesMeetingPoint.ClosedLeavesMeetingPoint_Center); }
                //catch { }
                double colAngle = 0;
                if (!isHalcyon)
                {
                    try { beam.FitCollimatorToStructure(new FitToStructureMargins(0, 0, 0, 0), ptv, true, true, true); }
                    catch { }
                    if (beam.ControlPoints[0].CollimatorAngle > 180)
                    {
                        colAngle = beam.ControlPoints[0].CollimatorAngle - 180;
                        eps.RemoveBeam(beam);
                        beam = eps.AddMLCBeam(ebmp, leafPositions, new VRect<double>(-100, -100, 100, 100), colAngle, angGantry, 0, isocenter);
                        try { beam.FitMLCToStructure(new FitToStructureMargins(5, 5, 25, 5), ptv, false, JawFitting.FitToStructure, OpenLeavesMeetingPoint.OpenLeavesMeetingPoint_Middle, ClosedLeavesMeetingPoint.ClosedLeavesMeetingPoint_Center); }
                        catch { }
                    }
                    else
                    {
                        try { beam.FitMLCToStructure(new FitToStructureMargins(5, 5, 25, 5), ptv, false, JawFitting.FitToStructure, OpenLeavesMeetingPoint.OpenLeavesMeetingPoint_Middle, ClosedLeavesMeetingPoint.ClosedLeavesMeetingPoint_Center); }
                        catch { }
                    }
                }
                else
                {
                    try { beam.FitMLCToStructure(new FitToStructureMargins(5, 5, 25, 5), ptv, true, JawFitting.FitToStructure, OpenLeavesMeetingPoint.OpenLeavesMeetingPoint_Middle, ClosedLeavesMeetingPoint.ClosedLeavesMeetingPoint_Center); }
                    catch { }
                }
                return beam;
            }
            public static Beam TgExtDComCorrecaoDivergencia(ExternalPlanSetup eps, ExternalBeamMachineParameters ebmp, float[,] leafPositions, VVector isocenter, Structure body, Structure ptv, Beam TgInt, bool isHalcyon)
            {
                double angGantry = AngulosTangentes.GantryTgExtMamaDDivergencia(TgInt, isHalcyon);
                //Beam beam = eps.AddStaticBeam(ebmp, new VRect<double>(-100, -100, 100, 100), 0, angGantry, 0, isocenter);
                Beam beam = eps.AddMLCBeam(ebmp, leafPositions, new VRect<double>(-100, -100, 100, 100), 0, angGantry, 0, isocenter);
                //try { beam.FitMLCToStructure(new FitToStructureMargins(5, 5, 25, 5), ptv, true, JawFitting.FitToStructure, OpenLeavesMeetingPoint.OpenLeavesMeetingPoint_Middle, ClosedLeavesMeetingPoint.ClosedLeavesMeetingPoint_Center); }
                //catch { }
                double colAngle = 0;
                if (!isHalcyon)
                {
                    try { beam.FitCollimatorToStructure(new FitToStructureMargins(0, 0, 0, 0), ptv, true, true, true); }
                    catch { }
                    if (beam.ControlPoints[0].CollimatorAngle > 180)
                    {
                        colAngle = beam.ControlPoints[0].CollimatorAngle - 180;
                        eps.RemoveBeam(beam);
                        beam = eps.AddMLCBeam(ebmp, leafPositions, new VRect<double>(-100, -100, 100, 100), colAngle, angGantry, 0, isocenter);
                        try { beam.FitMLCToStructure(new FitToStructureMargins(5, 5, 25, 5), ptv, false, JawFitting.FitToStructure, OpenLeavesMeetingPoint.OpenLeavesMeetingPoint_Middle, ClosedLeavesMeetingPoint.ClosedLeavesMeetingPoint_Center); }
                        catch { }
                    }
                    else
                    {
                        try { beam.FitMLCToStructure(new FitToStructureMargins(5, 5, 25, 5), ptv, false, JawFitting.FitToStructure, OpenLeavesMeetingPoint.OpenLeavesMeetingPoint_Middle, ClosedLeavesMeetingPoint.ClosedLeavesMeetingPoint_Center); }
                        catch { }
                    }
                }
                else
                {
                    try { beam.FitMLCToStructure(new FitToStructureMargins(5, 5, 25, 5), ptv, true, JawFitting.FitToStructure, OpenLeavesMeetingPoint.OpenLeavesMeetingPoint_Middle, ClosedLeavesMeetingPoint.ClosedLeavesMeetingPoint_Center); }
                    catch { }
                }
                //
                return beam;
            }
            public static Beam TgExtE(ExternalPlanSetup eps, ExternalBeamMachineParameters ebmp, float[,] leafPositions, VVector isocenter, Structure body, Structure ptv, Structure pulmaoipsi, bool considerCB, Structure mamacontra, bool considerHeart, Structure coracao, bool isHalcyon)
            {
                double angGantry = AngulosTangentes.GantryTgExtMamaE(eps, ebmp, ptv, pulmaoipsi, considerCB, mamacontra, considerHeart, coracao);
                //Beam beam = eps.AddStaticBeam(ebmp, new VRect<double>(-100, -100, 100, 100), 0, angGantry, 0, isocenter);
                Beam beam = eps.AddMLCBeam(ebmp, leafPositions, new VRect<double>(-100, -100, 100, 100), 0, angGantry, 0, isocenter);
                //try { beam.FitMLCToStructure(new FitToStructureMargins(25, 5, 5, 5), ptv, true, JawFitting.FitToStructure, OpenLeavesMeetingPoint.OpenLeavesMeetingPoint_Middle, ClosedLeavesMeetingPoint.ClosedLeavesMeetingPoint_Center); }
                //catch { }
                double colAngle = 0;
                if (!isHalcyon)
                {
                    try { beam.FitCollimatorToStructure(new FitToStructureMargins(0, 0, 0, 0), ptv, true, true, true); }
                    catch { }
                    if (beam.ControlPoints[0].CollimatorAngle < 180)
                    {
                        colAngle = beam.ControlPoints[0].CollimatorAngle + 180;
                        eps.RemoveBeam(beam);
                        beam = eps.AddMLCBeam(ebmp, leafPositions, new VRect<double>(-100, -100, 100, 100), colAngle, angGantry, 0, isocenter);
                        try { beam.FitMLCToStructure(new FitToStructureMargins(25, 5, 5, 5), ptv, false, JawFitting.FitToStructure, OpenLeavesMeetingPoint.OpenLeavesMeetingPoint_Middle, ClosedLeavesMeetingPoint.ClosedLeavesMeetingPoint_Center); }
                        catch { }
                    }
                    else
                    {
                        try { beam.FitMLCToStructure(new FitToStructureMargins(25, 5, 5, 5), ptv, false, JawFitting.FitToStructure, OpenLeavesMeetingPoint.OpenLeavesMeetingPoint_Middle, ClosedLeavesMeetingPoint.ClosedLeavesMeetingPoint_Center); }
                        catch { }
                    }
                }
                else
                {
                    try { beam.FitMLCToStructure(new FitToStructureMargins(25, 5, 5, 5), ptv, true, JawFitting.FitToStructure, OpenLeavesMeetingPoint.OpenLeavesMeetingPoint_Middle, ClosedLeavesMeetingPoint.ClosedLeavesMeetingPoint_Center); }
                    catch { }
                }
                return beam;
            }
            public static Beam TgExtEComCorrecaoDivergencia(ExternalPlanSetup eps, ExternalBeamMachineParameters ebmp, float[,] leafPositions, VVector isocenter, Structure body, Structure ptv, Beam TgInt, bool isHalcyon)
            {
                double angGantry = AngulosTangentes.GantryTgExtMamaEDivergencia(TgInt, isHalcyon);
                //Beam beam = eps.AddStaticBeam(ebmp, new VRect<double>(-100, -100, 100, 100), 0, angGantry, 0, isocenter);
                Beam beam = eps.AddMLCBeam(ebmp, leafPositions, new VRect<double>(-100, -100, 100, 100), 0, angGantry, 0, isocenter);
                //try { beam.FitMLCToStructure(new FitToStructureMargins(25, 5, 5, 5), ptv, true, JawFitting.FitToStructure, OpenLeavesMeetingPoint.OpenLeavesMeetingPoint_Middle, ClosedLeavesMeetingPoint.ClosedLeavesMeetingPoint_Center); }
                //catch { }
                double colAngle = 0;
                if (!isHalcyon)
                {
                    try { beam.FitCollimatorToStructure(new FitToStructureMargins(0, 0, 0, 0), ptv, true, true, true); }
                    catch { }
                    if (beam.ControlPoints[0].CollimatorAngle < 180)
                    {
                        colAngle = beam.ControlPoints[0].CollimatorAngle + 180;
                        eps.RemoveBeam(beam);
                        beam = eps.AddMLCBeam(ebmp, leafPositions, new VRect<double>(-100, -100, 100, 100), colAngle, angGantry, 0, isocenter);
                        try { beam.FitMLCToStructure(new FitToStructureMargins(25, 5, 5, 5), ptv, false, JawFitting.FitToStructure, OpenLeavesMeetingPoint.OpenLeavesMeetingPoint_Middle, ClosedLeavesMeetingPoint.ClosedLeavesMeetingPoint_Center); }
                        catch { }
                    }
                    else
                    {
                        try { beam.FitMLCToStructure(new FitToStructureMargins(25, 5, 5, 5), ptv, false, JawFitting.FitToStructure, OpenLeavesMeetingPoint.OpenLeavesMeetingPoint_Middle, ClosedLeavesMeetingPoint.ClosedLeavesMeetingPoint_Center); }
                        catch { }
                    }
                }
                else
                {
                    try { beam.FitMLCToStructure(new FitToStructureMargins(25, 5, 5, 5), ptv, true, JawFitting.FitToStructure, OpenLeavesMeetingPoint.OpenLeavesMeetingPoint_Middle, ClosedLeavesMeetingPoint.ClosedLeavesMeetingPoint_Center); }
                    catch { }
                }
                return beam;
            }
        }
        public enum FormaIncidencia
        {
            Pulmao,
            PulmaoComCorrecaoDivergencia,
            Mama,
            Coracao
        }
        public static FormaIncidencia EscolherFormaIncidencia(bool lateralidadeEsquerda, bool hasCB)
        {
            // Default to Pulmao
            FormaIncidencia escolha = FormaIncidencia.Pulmao;

            if (lateralidadeEsquerda)
            {
                // Left-sided case
                if (hasCB)
                {
                    // For left-sided cases with contralateral breast, prioritize it
                    escolha = FormaIncidencia.Mama;
                }
                else
                {
                    // For left-sided cases without contralateral breast, prioritize heart
                    escolha = FormaIncidencia.Coracao;
                }
            }
            else
            {
                // Right-sided case
                if (hasCB)
                {
                    // For right-sided cases with contralateral breast, prioritize it
                    escolha = FormaIncidencia.Mama;
                }
                else
                {
                    // For right-sided cases without contralateral breast, add divergence correction
                    escolha = FormaIncidencia.PulmaoComCorrecaoDivergencia;
                }
            }

            return escolha;
        }
    } // End of TangentPlacementViewModel class
} // End of namespace
