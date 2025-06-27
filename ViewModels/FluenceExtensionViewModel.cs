using Prism.Mvvm;
using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using System.Reflection;
using System.Runtime.CompilerServices;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using System.Windows.Controls;
using System.IO;
using MAAS_BreastPlan_helper.Models;
using MAAS_BreastPlan_helper.Services;
using MAAS_BreastPlan_helper.Views;
using static MAAS_BreastPlan_helper.ViewModels.FluenceExtensionViewModel;
using Prism.Commands;


namespace MAAS_BreastPlan_helper.ViewModels
{
    public class FluenceExtensionViewModel : BindableBase
    {
        private readonly EsapiWorker _esapiWorker;
        private readonly SettingsClass _settings;
        private Structure _body;

        // Observable Collections
        private ObservableCollection<Structure> _structures;
        public ObservableCollection<Structure> Structures
        {
            get { return _structures; }
            set { SetProperty(ref _structures, value); }
        }

        private ObservableCollection<BeamSelectionItem> _beamSelectionItems;
        public ObservableCollection<BeamSelectionItem> BeamSelectionItems
        {
            get { return _beamSelectionItems; }
            set { SetProperty(ref _beamSelectionItems, value); }
        }

        private ObservableCollection<string> _fluenceDepthOptions;
        public ObservableCollection<string> FluenceDepthOptions
        {
            get { return _fluenceDepthOptions; }
            set { SetProperty(ref _fluenceDepthOptions, value); }
        }

        // Selected Structure
        private Structure _selectedPTVStructure;
        public Structure SelectedPTVStructure
        {
            get { return _selectedPTVStructure; }
            set
            {
                SetProperty(ref _selectedPTVStructure, value);
                CommandManager.InvalidateRequerySuggested();
            }
        }

        // Fluence Properties
        private string _fluenceExtent;
        public string FluenceExtent
        {
            get { return _fluenceExtent; }
            set
            {
                SetProperty(ref _fluenceExtent, value);
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private string _selectedFluenceDepth;
        public string SelectedFluenceDepth
        {
            get { return _selectedFluenceDepth; }
            set
            {
                SetProperty(ref _selectedFluenceDepth, value);
                CommandManager.InvalidateRequerySuggested();
            }
        }

        // Status Message
        private string _statusMessage;
        public string StatusMessage
        {
            get { return _statusMessage; }
            set { SetProperty(ref _statusMessage, value); }
        }

        // Command
        public DelegateCommand ConfirmSelectionCommand { get; private set; }

        // Constructor
        public FluenceExtensionViewModel(EsapiWorker esapiWorker, SettingsClass settings)
        {
            _esapiWorker = esapiWorker;
            _settings = settings;
            ConfirmSelectionCommand = new DelegateCommand(ExecuteConfirmSelection, CanExecuteConfirmSelection);
            Initialize();
        }

        public void RefreshData()
        {
            RefreshStructuresAndBeams();
            StatusMessage = "Data refreshed successfully.";
        }

        private void Initialize()
        {
            RefreshStructuresAndBeams();
            FluenceDepthOptions = new ObservableCollection<string> { "0.5", "0.6", "0.7", "0.8", "0.9", "1.0" };
            FluenceExtent = "2.0"; // Default value
        }

        private void RefreshStructuresAndBeams()
        {
            _esapiWorker.RunWithWait(sc =>
            {
                try
                {
                    // Clear existing references first to avoid disposed object access
                    _body = null;
                    SelectedPTVStructure = null;
                    Structures?.Clear();
                    BeamSelectionItems?.Clear();

                    // Get the body structure
                    _body = sc.StructureSet.Structures.FirstOrDefault(x => x.DicomType == "EXTERNAL");

                    // Initialize collections with fresh structure references
                    Structures = new ObservableCollection<Structure>(
                        sc.StructureSet.Structures.Where(s => s.Id.Contains("PTV"))
                    );

                    BeamSelectionItems = new ObservableCollection<BeamSelectionItem>(
                        sc.ExternalPlanSetup.Beams
                            .Where(b => b.GetOptimalFluence() != null)
                            .Select(b => new BeamSelectionItem { BeamId = b.Id, IsSelected = false, Beam = b })
                    );

                    // Clear any previously selected PTV since structures have been refreshed
                    SelectedPTVStructure = null;
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Error refreshing structures: {ex.Message}";
                }
            });
        }

        private bool CanExecuteConfirmSelection()
        {
            bool hasStructure = SelectedPTVStructure != null;
            bool hasExtent = !string.IsNullOrWhiteSpace(FluenceExtent);
            bool hasDepth = !string.IsNullOrWhiteSpace(SelectedFluenceDepth);
            bool hasSelectedBeam = BeamSelectionItems?.Any(item => item.IsSelected) ?? false;

            StatusMessage = $"Structure: {hasStructure}, Extent: {hasExtent}, Depth: {hasDepth}, Beam: {hasSelectedBeam}";
            return hasStructure && hasExtent && hasDepth && hasSelectedBeam;
        }

        private void ExecuteConfirmSelection()
        {
            ProcessBeams();
        }

        private void ProcessBeams()
        {
            try
            {
                if (SelectedPTVStructure == null || !BeamSelectionItems.Any(item => item.IsSelected))
                {
                    return;
                }

                _esapiWorker.ExecuteWithErrorHandling(sc =>
                {
                    // Begin modifications
                    sc.Patient.BeginModifications();

                    // Save initial plan state
                    var originalPlanId = sc.ExternalPlanSetup.Id;
                    var course = sc.Course;
                    var eps = sc.ExternalPlanSetup;

                // Update plan ID for the modified version
                int index = 1;
                foreach (var p in course.PlanSetups)
                {
                    if (p.Id.Contains("Fluence_Ext")) { index++; }
                }
                eps.Id = $"Fluence_Ext_{index}";

                // Save the initial plan
                var initialPlan = (ExternalPlanSetup)course.CopyPlanSetup(eps);
                initialPlan.Id = originalPlanId;

                // Get selected beams
                var selectedBeams = BeamSelectionItems
                    .Where(item => item.IsSelected)
                    .Select(item => item.Beam)
                    .ToList();

                // Create and run beam processor with fresh structure references
                var bodyStructure = sc.StructureSet.Structures.FirstOrDefault(x => x.DicomType == "EXTERNAL");
                var ptvStructure = sc.StructureSet.Structures.FirstOrDefault(s => s.Id == SelectedPTVStructure.Id);
                
                if (ptvStructure == null)
                {
                    throw new Exception($"Selected PTV structure '{SelectedPTVStructure.Id}' not found in current structure set.");
                }

                var processor = new BeamProcessor(
                    double.Parse(FluenceExtent),
                    double.Parse(SelectedFluenceDepth),
                    bodyStructure,
                    ptvStructure
                );

                processor.ProcessBeams(selectedBeams, eps);

                    StatusMessage = "Fluence extension completed successfully.";
                    
                    // Show message box with the created plan name
                    MessageBox.Show($"Plan '{eps.Id}' has been created successfully.", 
                                    "Fluence Extension Complete", 
                                    MessageBoxButton.OK, 
                                    MessageBoxImage.Information);
                },
                ex =>
                {
                    StatusMessage = $"Error: {ex.Message}";
                });
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
        }

        // Nested Classes
        public class BeamSelectionItem : BindableBase
        {
            public string BeamId { get; set; }
            public Beam Beam { get; set; }

            private bool _isSelected;
            public bool IsSelected
            {
                get { return _isSelected; }
                set { SetProperty(ref _isSelected, value); }
            }
        }

        public class Rotacao
        {
            // Funcao para aplicar rotacao a cada ponto
            static List<Point> RotatePoints(/*List<double> xData, List<double> yData,*/ List<Point> points, double[,] rotationMatrix)
            {
                var rotatedPoints = new List<Point>();

                foreach (var point in points)
                {
                    double rotatedX = rotationMatrix[0, 0] * point.X + rotationMatrix[0, 1] * point.Y;
                    double rotatedY = rotationMatrix[1, 0] * point.X + rotationMatrix[1, 1] * point.Y;

                    rotatedPoints.Add(new Point(rotatedX, rotatedY));
                }

                return rotatedPoints;
            }

            public static List<Point> PontosRotacionados(double anguloRotacao, Beam beam, Structure structure, double xmin, double xmax, double ymin, double ymax, bool left)
            {

                // Converta o ângulo para radianos
                double angleRadians;

                if (anguloRotacao > 90)
                {
                    angleRadians = -(anguloRotacao - 360) * Math.PI / 180.0;
                }
                else { angleRadians = -anguloRotacao * Math.PI / 180.0; }

                // Crie a matriz de rotacao
                double[,] rotationMatrix = {
                        { Math.Cos(angleRadians), -Math.Sin(angleRadians) },
                        { Math.Sin(angleRadians), Math.Cos(angleRadians) }
                    };


                // Obtem o contorno da estrutura.
                var outline = beam.GetStructureOutlines(structure, true);

                // Converte os dados do contorno em uma lista de pontos.
                var bodyOutline = outline.SelectMany(o => o).ToArray();

                // Filtra os pontos de interesse.
                var pointsOfInterest = BeamProcessor.GetPointsOfInterest(bodyOutline, xmin, xmax, ymin, ymax, left);

                // Aplica a rotacao aos pontos de interesse.
                return RotatePoints(pointsOfInterest, rotationMatrix);
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
                //Catch the index of the z coordinate of the ptv Centerpoint	    
                int indice = GetSlice(ptv.CenterPoint.z, ss);

                //Create a list with the points of that slice
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

                //gets the x coordinate of the body center point to compare
                double ptoXCentral = body.CenterPoint.x;

                //point count 
                int contE = 0;
                int contD = 0;
                string lat = "";
                foreach (var p in slice)
                {
                    if (p.X < ptoXCentral) { contD++; }
                    else { contE++; }
                }
                if (contD > contE) { lat = "right"; }
                else { lat = "left"; }

                //razao
                return lat;
            }

            public static string LadoBEV(Structure structureToEvaluate, Structure ptv, Beam beam)
            {

                //Acessa as coordenadas do BEV               
                var stProj = beam.GetStructureOutlines(structureToEvaluate, true);
                var ptvProj = beam.GetStructureOutlines(ptv, true);

                double maxXPTV = double.MinValue;
                double minXPTV = double.MaxValue;

                foreach (var ponto in stProj[0])
                {
                    if (Math.Round(ponto.X / 10.0f) * 10.0f > Math.Round(maxXPTV / 10.0f) * 10.0f)
                    {
                        maxXPTV = ponto.X;
                    }
                    if (Math.Round(ponto.X / 10.0f) * 10.0f < Math.Round(minXPTV / 10.0f) * 10.0f)
                    {
                        minXPTV = ponto.X;
                    }
                }

                //ponto central para comparar
                double ptoXCentral = minXPTV + (maxXPTV - minXPTV) / 2;

                //contagem de pontos
                int contE = 0;
                int contD = 0;
                string lat;
                foreach (var p in ptvProj[0])
                {
                    if (Math.Round(p.X / 10.0f) * 10.0f > Math.Round(ptoXCentral / 10.0f) * 10.0f) { contE++; }
                    else { contD++; }
                }
                if (contD > contE) { lat = "EstADireita"; }
                else { lat = "EstAEsquerda"; }

                //lateralidade
                return lat;
            }

        }
        public class AuxiliaryFunctions
        {
            public static Tuple<double, double> Pixel2BEV(Fluence fluencia, int vx, int vy)
            {
                Point origem = new Point(fluencia.XOrigin, fluencia.YOrigin);
                double Xres = 2.5; double Yres = 2.5;
                double x = origem.X + (vx * Xres);
                double y = origem.Y - (vy * Yres);
                return Tuple.Create(x, y);
            }

            public static Tuple<int, int> BEV2Pixel(Fluence fluencia, double x, double y)
            {
                Point origem = new Point(fluencia.XOrigin, fluencia.YOrigin);
                double Xres = 2.5; double Yres = 2.5;
                int vx = (int)Math.Round((x - origem.X) / Xres);
                int vy = (int)Math.Round((origem.Y - y) / Yres);
                return Tuple.Create(vx, vy);
            }
        }
        public class BeamProcessor
        {
            private readonly double extension;
            private readonly double fluenceDepth;
            private readonly Structure body;
            private readonly Structure ptv;
            private bool variation = false;

            public BeamProcessor(double extension, double fluenceDepth, Structure body, Structure ptv)
            {
                this.extension = extension;
                this.fluenceDepth = fluenceDepth;
                this.body = body;
                this.ptv = ptv;
            }

            public void ProcessBeams(IEnumerable<Beam> beams, ExternalPlanSetup eps)
            {
                foreach (var beam in beams)
                {
                    if (!beam.Id.Contains("CBCT"))
                    {
                        var fluence = beam.GetOptimalFluence();
                        var pixels = fluence.GetPixels();
                        var bodyOutline = beam.GetStructureOutlines(body, true);
                        var ptvOutline = beam.GetStructureOutlines(ptv, true);

                        var xminMax = GetXMinMax(bodyOutline);
                        var yminMax = GetYMinMax(bodyOutline);
                        //var yminMax = GetYMinMax(ptvOutline);

                        double xmin = xminMax.Item1;
                        double xmax = xminMax.Item2;
                        double ymin = yminMax.Item1;
                        double ymax = yminMax.Item2;

                        if (Lateralidade.LadoBEV(body, ptv, beam) == "EstAEsquerda")
                        {
                            //var points = GetPointsOfInterest(bodyOutline, xmin, xmax, ymin, ymax, true);
                            var points = Rotacao.PontosRotacionados(beam.ControlPoints.First().CollimatorAngle, beam, body, xmin, xmax, ymin, ymax, true);
                            var smoothedPoints = SmoothPoints(points);
                            var interpolatedPoints = InterpolatePoints(smoothedPoints);
                            var tIndices = CalculateTIndicesLeft(interpolatedPoints);

                            AdjustPixels(fluence, pixels, tIndices, desiredLength: extension * 10, desiredDepth: fluenceDepth * 10);

                            if (variation) beam.SetOptimalFluence(new Fluence(pixels, fluence.XOrigin, fluence.YOrigin));
                        }
                        else if (Lateralidade.LadoBEV(body, ptv, beam) == "EstADireita")
                        {
                            //var points = GetPointsOfInterest(bodyOutline, xmin, xmax, ymin, ymax, true);
                            var points = Rotacao.PontosRotacionados(beam.ControlPoints.First().CollimatorAngle, beam, body, xmin, xmax, ymin, ymax, false);
                            var smoothedPoints = SmoothPoints(points);
                            var interpolatedPoints = InterpolatePoints(smoothedPoints);
                            var tIndices = CalculateTIndicesRight(interpolatedPoints);

                            AdjustPixels(fluence, pixels, tIndices, desiredLength: extension * 10, desiredDepth: fluenceDepth * 10);

                            if (variation) beam.SetOptimalFluence(new Fluence(pixels, fluence.XOrigin, fluence.YOrigin));
                        }
                    }
                }
                if (variation) { eps.CalculateLeafMotionsAndDose(); }
            }

            private Tuple<double, double> GetXMinMax(Point[][] outlines)
            {
                double xmin = double.MaxValue;
                double xmax = double.MinValue;
                foreach (var point in outlines[0])
                {
                    if (point.X < xmin) xmin = point.X;
                    if (point.X > xmax) xmax = point.X;
                }
                return Tuple.Create(xmin, xmax);
            }

            private Tuple<double, double> GetYMinMax(Point[][] outlines)
            {
                double ymin = double.MaxValue;
                double ymax = double.MinValue;
                foreach (var point in outlines[0])
                {
                    if (point.Y < ymin) ymin = point.Y;
                    if (point.Y > ymax) ymax = point.Y;
                }
                return Tuple.Create(ymin, ymax);
            }

            public static List<Point> GetPointsOfInterest(Point[] bodyOutline, double xmin, double xmax, double ymin, double ymax, bool left)
            {

                var points = new List<Point>();

                foreach (var point in bodyOutline)
                {
                    double roundedY = Math.Round(point.Y / 10.0) * 10.0;
                    double roundedX = Math.Round(point.X / 10.0) * 10.0;

                    if (roundedY >= ymin && roundedY <= ymax)
                    {
                        if ((left && roundedX > (xmin + (xmax - xmin) / 2)) ||
                            (!left && roundedX < (xmin + (xmax - xmin) / 2)))
                        {
                            points.Add(point);
                        }
                    }
                }

                return points.OrderByDescending(p => p.Y).ToList();
            }

            private List<Point> SmoothPoints(List<Point> points)
            {
                var smoothedPoints = new List<Point>();
                for (int i = 1; i < points.Count - 2; i++)
                {
                    var x = (points[i - 1].X + points[i].X + points[i + 1].X) / 3;
                    var y = (points[i - 1].Y + points[i].Y + points[i + 1].Y) / 3;
                    smoothedPoints.Add(new Point(x, y));
                }
                return smoothedPoints.OrderByDescending(p => p.Y).ToList();
            }

            private List<Point> InterpolatePoints(List<Point> points)
            {
                var interpolatedPoints = new List<Point>();
                for (int i = 0; i < points.Count - 1; i++)
                {
                    var p1 = points[i];
                    var p2 = points[i + 1];

                    interpolatedPoints.Add(p1);

                    double dy = Math.Abs(p1.Y - p2.Y);
                    if (dy > 1.1)
                    {
                        int numInterpolatedPoints = (int)(dy / 1.1);
                        for (int j = 1; j <= numInterpolatedPoints; j++)
                        {
                            var interpX = p1.X + (p2.X - p1.X) * j / (numInterpolatedPoints + 1);
                            var interpY = p1.Y + (p2.Y - p1.Y) * j / (numInterpolatedPoints + 1);
                            interpolatedPoints.Add(new Point(interpX, interpY));
                        }
                    }
                }
                interpolatedPoints.Add(points[points.Count - 1]);
                return interpolatedPoints.OrderByDescending(p => p.Y).ToList();
            }

            private List<Tuple<double, double, double, double>> CalculateTIndicesLeft(List<Point> points)
            {
                var tIndices = new List<Tuple<double, double, double, double>>();
                for (int i = 0; i < points.Count - 2; i++)
                {
                    var x1 = points[i].X;
                    var y1 = points[i].Y;
                    var x2 = points[i + 2].X;
                    var y2 = points[i + 2].Y;

                    var normalVector = CalculateNormalVectorLeft(x1, y1, x2, y2, extension * 10);
                    double startX = normalVector.Item3;
                    double startY = normalVector.Item4;
                    double endX = normalVector.Item5;
                    double endY = normalVector.Item6;

                    tIndices.Add(Tuple.Create(startX, startY, endX, endY));
                }
                return tIndices;
            }

            private Tuple<double, double, double, double, double, double> CalculateNormalVectorLeft(double x1, double y1, double x2, double y2, double desiredLength)
            {
                double dx = x2 - x1;
                double dy = y2 - y1;

                double nx, ny;
                double ySign;

                if (dx == 0)
                {
                    nx = 1.0;
                    ny = 0.0;
                    ySign = 1;
                }
                else
                {
                    double slope = dy / dx;
                    if (slope > 0)
                    {
                        nx = dy;
                        ny = dx;
                        ySign = -1;
                    }
                    else
                    {
                        nx = dy;
                        ny = -dx;
                        ySign = 1;
                    }
                }

                double norm = Math.Sqrt(nx * nx + ny * ny);
                double unitNx = nx / norm;
                double unitNy = ny / norm;

                double normalX = unitNx * desiredLength;
                double normalY = unitNy * desiredLength;

                double startX = (x1 + x2) / 2;
                double startY = (y1 + y2) / 2;
                double endX = startX - normalX;
                double endY = ySign > 0 ? startY - normalY : startY + normalY;

                return Tuple.Create(normalX, normalY, startX, startY, endX, endY);
            }
            private List<Tuple<double, double, double, double>> CalculateTIndicesRight(List<Point> points)
            {
                var tIndices = new List<Tuple<double, double, double, double>>();
                for (int i = 0; i < points.Count - 2; i++)
                {
                    var x1 = points[i].X;
                    var y1 = points[i].Y;
                    var x2 = points[i + 2].X;
                    var y2 = points[i + 2].Y;

                    var normalVector = CalculateNormalVectorRight(x1, y1, x2, y2, extension * 10);
                    double startX = normalVector.Item3;
                    double startY = normalVector.Item4;
                    double endX = normalVector.Item5;
                    double endY = normalVector.Item6;

                    tIndices.Add(Tuple.Create(startX, startY, endX, endY));
                }
                return tIndices;
            }

            private Tuple<double, double, double, double, double, double> CalculateNormalVectorRight(double x1, double y1, double x2, double y2, double desiredLength)
            {
                double dx = x2 - x1;
                double dy = y2 - y1;

                double nx, ny;
                double ySign;

                if (dx == 0)
                {
                    nx = -1.0;
                    ny = 0.0;
                    ySign = -1;
                }
                else
                {
                    //// Encontre o vetor normal (perpendicular) à reta
                    //nx = dy;
                    //ny = dx;

                    // Calcule a inclinacao da reta
                    double slope = dy / dx;

                    // Ajustar a direcao do vetor normal com base na inclinacao
                    if (slope > 0)
                    {
                        // Ajustar a direcao do vetor normal para garantir que aponte para fora
                        nx = dy;
                        ny = -dx;
                        ySign = 1;
                    }
                    else
                    {
                        // Mantem a direcao original do vetor normal
                        nx = dy;
                        ny = dx;
                        ySign = -1;
                    }

                }

                double norm = Math.Sqrt(nx * nx + ny * ny);
                double unitNx = nx / norm;
                double unitNy = ny / norm;

                double normalX = unitNx * desiredLength;
                double normalY = unitNy * desiredLength;

                double startX = (x1 + x2) / 2;
                double startY = (y1 + y2) / 2;

                double endX = startX + normalX;
                double endY;
                if (ySign < 0)
                {
                    endY = startY - normalY;
                }
                else { endY = startY + normalY; }


                return Tuple.Create(normalX, normalY, startX, startY, endX, endY);
            }
            private void AdjustPixels(Fluence fluence, float[,] pixels, List<Tuple<double, double, double, double>> tIndices, double desiredLength, double desiredDepth)
            {
                foreach (var ind in tIndices)
                {
                    double length = desiredLength;
                    int nSteps = (int)(length / 1.25);
                    double theta = Math.Atan2(ind.Item4 - ind.Item2, ind.Item3 - ind.Item1);
                    double xStep, yStep;
                    double xFluence = ind.Item1 - desiredDepth * Math.Cos(theta);
                    double yFluence = ind.Item2 - desiredDepth * Math.Sin(theta);

                    // Primeira varredura: Preencher buracos com flashPixel
                    for (int i = 0; i < nSteps; i++)
                    {
                        xStep = ind.Item1 + i * (length / nSteps) * Math.Cos(theta);
                        yStep = ind.Item2 + i * (length / nSteps) * Math.Sin(theta);

                        try
                        {
                            var stepPixel = AuxiliaryFunctions.BEV2Pixel(fluence, xStep, yStep);
                            var flashPixel = AuxiliaryFunctions.BEV2Pixel(fluence, xFluence, yFluence);

                            if (pixels[stepPixel.Item2, stepPixel.Item1] == 0)
                            {
                                pixels[stepPixel.Item2, stepPixel.Item1] = pixels[flashPixel.Item2, flashPixel.Item1];
                                variation = true;
                            }
                        }
                        catch { }
                    }

                    // Segunda varredura: Smoothing com vizinhos (linha e coluna separadamente)
                    bool hasHoles;
                    int maxIterations = 50; // Limite de iteracões
                    int iteration = 0;

                    do
                    {
                        hasHoles = false;
                        iteration++;

                        // Smoothing por linha
                        for (int y = 0; y < pixels.GetLength(0); y++)
                        {
                            for (int x = 0; x < pixels.GetLength(1); x++)
                            {
                                if (pixels[y, x] == 0)
                                {
                                    hasHoles = true;
                                    float? leftNeighbor = null;
                                    float? rightNeighbor = null;

                                    if (x > 0 && pixels[y, x - 1] != 0)
                                        leftNeighbor = pixels[y, x - 1];
                                    else if (x > 1 && pixels[y, x - 2] != 0)
                                        leftNeighbor = pixels[y, x - 2];
                                    else if (x > 2 && pixels[y, x - 3] != 0)
                                        leftNeighbor = pixels[y, x - 3];

                                    if (x < pixels.GetLength(1) - 1 && pixels[y, x + 1] != 0)
                                        rightNeighbor = pixels[y, x + 1];
                                    else if (x < pixels.GetLength(1) - 2 && pixels[y, x + 2] != 0)
                                        rightNeighbor = pixels[y, x + 2];
                                    else if (x < pixels.GetLength(1) - 3 && pixels[y, x + 3] != 0)
                                        rightNeighbor = pixels[y, x + 3];

                                    if (leftNeighbor.HasValue && rightNeighbor.HasValue)
                                    {
                                        pixels[y, x] = (leftNeighbor.Value + rightNeighbor.Value) / 2;
                                    }
                                }
                            }
                        }

                        // Smoothing por coluna
                        for (int x = 0; x < pixels.GetLength(1); x++)
                        {
                            for (int y = 0; y < pixels.GetLength(0); y++)
                            {
                                if (pixels[y, x] == 0)
                                {
                                    hasHoles = true;
                                    float? topNeighbor = null;
                                    float? bottomNeighbor = null;

                                    if (y > 0 && pixels[y - 1, x] != 0)
                                        topNeighbor = pixels[y - 1, x];
                                    else if (y > 1 && pixels[y - 2, x] != 0)
                                        topNeighbor = pixels[y - 2, x];
                                    else if (y > 2 && pixels[y - 3, x] != 0)
                                        topNeighbor = pixels[y - 3, x];

                                    if (y < pixels.GetLength(0) - 1 && pixels[y + 1, x] != 0)
                                        bottomNeighbor = pixels[y + 1, x];
                                    else if (y < pixels.GetLength(0) - 2 && pixels[y + 2, x] != 0)
                                        bottomNeighbor = pixels[y + 2, x];
                                    else if (y < pixels.GetLength(0) - 3 && pixels[y + 3, x] != 0)
                                        bottomNeighbor = pixels[y + 3, x];

                                    if (topNeighbor.HasValue && bottomNeighbor.HasValue)
                                    {
                                        pixels[y, x] = (topNeighbor.Value + bottomNeighbor.Value) / 2;
                                    }
                                }
                            }
                        }

                    } while (hasHoles && iteration < maxIterations);
                }
            }
        }
    }
}
