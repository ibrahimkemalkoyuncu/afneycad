using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Enums;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs;

public partial class WallParallelRouteDialog
{
        private readonly CadDatabase _database;
        private readonly List<WallParallelRoutingService.WallSegment> _detectedWalls = [];

        // View-model for the auto-detect grid
        private class WallRow
        {
            public double StartX { get; init; }
            public double StartY { get; init; }
            public double EndX   { get; init; }
            public double EndY   { get; init; }
            public double Thickness { get; init; }
            public double LengthM  { get; init; }
        }

        public WallParallelRouteDialog(CadDatabase database)
        {
            InitializeComponent();
            _database = database;
            PopulateLayerFilter();
        }

        private void PopulateLayerFilter()
        {
            LayerFilter.Items.Clear();
            LayerFilter.Items.Add(new ComboBoxItem { Content = "— Tümü (Mimari Heuristic) —", IsSelected = true });
            foreach (var layer in _database.GetLayers().OrderBy(l => l))
                LayerFilter.Items.Add(new ComboBoxItem { Content = layer });
        }

        // ── Kaynak seçimi ────────────────────────────────────────────────────────

        private void WallSource_Changed(object sender, RoutedEventArgs e)
        {
            if (PanelAuto == null) return;
            bool isAuto = RbAutoDetect.IsChecked == true;
            PanelAuto.Visibility   = isAuto ? Visibility.Visible : Visibility.Collapsed;
            PanelManual.Visibility = isAuto ? Visibility.Collapsed : Visibility.Visible;
            BtnRoute.IsEnabled = !isAuto || _detectedWalls.Count > 0;
        }

        // ── Otomatik duvar tarama ────────────────────────────────────────────────

        private void ScanWalls_Click(object sender, RoutedEventArgs e)
        {
            _detectedWalls.Clear();

            string? layerFilter = (LayerFilter.SelectedItem as ComboBoxItem)?.Content?.ToString();
            bool useAll = layerFilter == null || layerFilter.StartsWith("—");

            var entities = _database.GetAllEntities().ToList();

            foreach (var ent in entities)
            {
                string layer = ent.Layer ?? "0";
                bool isWallLayer = useAll
                    ? IsWallLayer(layer)
                    : layer.Equals(layerFilter, StringComparison.OrdinalIgnoreCase);

                if (!isWallLayer) continue;

                if (ent is LineEntity line)
                {
                    double length = (line.EndPoint - line.StartPoint).Length();
                    if (length < 100) continue; // 10 cm'den kısa segmentleri atla
                    _detectedWalls.Add(new WallParallelRoutingService.WallSegment
                    {
                        Start     = line.StartPoint,
                        End       = line.EndPoint,
                        Thickness = 200
                    });
                }
                else if (ent is LwPolylineEntity poly && poly.Vertices.Count >= 2)
                {
                    for (int i = 0; i < poly.Vertices.Count - 1; i++)
                    {
                        var p1 = new Vector3D(poly.Vertices[i].X, poly.Vertices[i].Y, 0);
                        var p2 = new Vector3D(poly.Vertices[i + 1].X, poly.Vertices[i + 1].Y, 0);
                        double length = (p2 - p1).Length();
                        if (length < 100) continue;
                        _detectedWalls.Add(new WallParallelRoutingService.WallSegment
                        {
                            Start     = p1,
                            End       = p2,
                            Thickness = 200
                        });
                    }
                }
            }

            // Grid'i güncelle
            WallGrid.ItemsSource = _detectedWalls.Select(w => new WallRow
            {
                StartX    = w.Start.X,
                StartY    = w.Start.Y,
                EndX      = w.End.X,
                EndY      = w.End.Y,
                Thickness = w.Thickness,
                LengthM   = (w.End - w.Start).Length() / 1000.0
            }).ToList();

            if (_detectedWalls.Count > 0)
            {
                StatusText.Text = $"✓ {_detectedWalls.Count} duvar segmenti tespit edildi. Rotalama yapabilirsiniz.";
                BtnRoute.IsEnabled = true;
            }
            else
            {
                StatusText.Text = "⚠ Duvar bulunamadı. Farklı bir layer filtresi deneyin veya Manuel moda geçin.";
                BtnRoute.IsEnabled = false;
            }
        }

        private static bool IsWallLayer(string layer)
        {
            var upper = layer.ToUpperInvariant();
            return upper.Contains("WALL") || upper.Contains("DUVAR") ||
                   upper.Contains("MIMARI") || upper.Contains("KABA") ||
                   upper.Contains("SIVA")   || upper.Contains("YAPISAL");
        }

        // ── Rotalama ─────────────────────────────────────────────────────────────

        private void Route_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                double offset   = ParseDouble(OffsetInput.Text, 150.0);
                double diameter = ParseDouble(DiameterInput.Text, 20.0);

                List<WallParallelRoutingService.WallSegment> walls;

                if (RbManual.IsChecked == true)
                    walls = ParseManualSegments();
                else
                    walls = _detectedWalls;

                if (walls.Count == 0)
                {
                    StatusText.Text = "⚠ Rotalamak için en az 1 duvar segmenti gerekli.";
                    return;
                }

                var svc = new WallParallelRoutingService(_database)
                {
                    DefaultOffset   = offset,
                    DefaultDiameter = diameter
                };

                var result = svc.RouteAlongWalls(walls, offset, diameter);

                // Sistem tipini uygula
                var sysType = GetSelectedSystemType();
                foreach (var pipe in result.Pipes) pipe.SystemType = sysType;

                foreach (var pipe  in result.Pipes)   _database.AddEntity(pipe);
                foreach (var elbow in result.Elbows)  _database.AddEntity(elbow);

                double totalM = result.TotalLength / 1000.0;
                StatusText.Text = $"✓ {result.Pipes.Count} boru segmenti | {result.Elbows.Count} dirsek | {totalM:F2} m toplam uzunluk";
                DialogResult = true;
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Hata: {ex.Message}";
            }
        }

        private List<WallParallelRoutingService.WallSegment> ParseManualSegments()
        {
            var result = new List<WallParallelRoutingService.WallSegment>();
            var lines = ManualSegments.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                // Format: X1,Y1 → X2,Y2  or  X1,Y1 > X2,Y2
                var parts = line.Replace('→', '>').Split('>');
                if (parts.Length < 2) continue;
                if (!TryParsePoint(parts[0].Trim(), out var p1)) continue;
                if (!TryParsePoint(parts[1].Trim(), out var p2)) continue;
                result.Add(new WallParallelRoutingService.WallSegment
                {
                    Start = p1, End = p2, Thickness = 200
                });
            }
            return result;
        }

        private static bool TryParsePoint(string s, out Vector3D pt)
        {
            pt = Vector3D.Zero;
            var tokens = s.Split(',');
            if (tokens.Length < 2) return false;
            if (!double.TryParse(tokens[0].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double x)) return false;
            if (!double.TryParse(tokens[1].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double y)) return false;
            pt = new Vector3D(x, y, 0);
            return true;
        }

        private MechanicalSystemType GetSelectedSystemType()
        {
            return (SystemTypeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() switch
            {
                "Sıcak Su"  => MechanicalSystemType.DomesticHotWater,
                "Pis Su"    => MechanicalSystemType.WasteWater,
                "Yangın"    => MechanicalSystemType.FireProtection,
                _           => MechanicalSystemType.DomesticColdWater
            };
        }

    private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }

    private static double ParseDouble(string s, double fallback)
        => double.TryParse(s.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : fallback;
}
