using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Afney.Cad.Database.Core;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Services;
using static Afney.Cad.Mechanical.Services.ValveLibraryService;

namespace Afney.Cad.Presentation.Dialogs;

public partial class ValveLibraryDialog
{
    private readonly ValveLibraryService _service;
    private readonly CadDatabase? _database;

    public ValveLibraryDialog(CadDatabase? database = null)
    {
        InitializeComponent();
        _database = database;
        _service  = new ValveLibraryService();
        LoadValves();
    }

    private void LoadValves()
    {
        ValveGrid.ItemsSource = _service.GetAll();
    }

    private void ValveGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ValveGrid.SelectedItem is ValveDefinition def)
        {
            UpdateDetailPanel(def);
            BtnInsert.IsEnabled = _database != null;
        }
        else
        {
            BtnInsert.IsEnabled = false;
        }
    }

    private void ValveGrid_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ValveGrid.SelectedItem is ValveDefinition)
            Insert_Click(sender, e);
    }

    private void UpdateDetailPanel(ValveDefinition def)
    {
        DtlName.Text = def.NameTR;
        DtlId.Text   = def.Id;
        DtlDN.Text   = $"DN {def.NominalDiameter:F0}";
        DtlZeta.Text = def.LossCoefficient.ToString("F2");
        DtlType.Text = def.Type.ToString();
        DtlStd.Text  = def.Standard;
    }

    private void Insert_Click(object sender, RoutedEventArgs e)
    {
        if (_database == null) return;
        if (ValveGrid.SelectedItem is not ValveDefinition def) return;

        double x      = ParseDouble(TxtX.Text, 0);
        double y      = ParseDouble(TxtY.Text, 0);
        double rotDeg = ParseDouble(TxtRot.Text, 0);
        double rotRad = rotDeg * Math.PI / 180.0;

        var pos   = new Vector3D(x, y, 0);
        var valve = new ValveEntity(pos, def.Type, def.NominalDiameter)
        {
            Rotation = rotRad,
            Layer    = "VANA"
        };

        // Boru snap — 500mm yarıçapında en yakın boru aranır
        const double SnapTol = 500.0;
        var allPipes = _database.GetAllEntities().OfType<PipeEntity>().ToList();
        PipeEntity? nearest = null;
        double minDist = double.MaxValue;

        foreach (var pipe in allPipes)
        {
            double d = DistanceToSegment(pos, pipe.StartPoint, pipe.EndPoint);
            if (d < minDist && d <= SnapTol) { minDist = d; nearest = pipe; }
        }

        if (nearest != null)
        {
            // Vanadaki borunun yönüne yasla + split
            var dir  = nearest.EndPoint - nearest.StartPoint;
            double pipeLen = dir.Length();

            if (pipeLen > 1.0)
            {
                var unit = dir * (1.0 / pipeLen);
                var w = pos - nearest.StartPoint;
                double t = Math.Clamp(w.Dot(unit) / pipeLen, 0.0, 1.0);

                // Vanayı boruya yapıştır ve yön ver
                valve.Position = nearest.StartPoint + dir * t;
                valve.Rotation = Math.Atan2(unit.Y, unit.X);

                double half = valve.Size / 2.0;
                double tIn  = Math.Clamp(t - half / pipeLen, 0.0, 1.0);
                double tOut = Math.Clamp(t + half / pipeLen, 0.0, 1.0);

                if (tIn > 0.001)
                    _database.AddEntity(ClonePipe(nearest, nearest.StartPoint, nearest.StartPoint + dir * tIn));

                if (tOut < 0.999)
                    _database.AddEntity(ClonePipe(nearest, nearest.StartPoint + dir * tOut, nearest.EndPoint));

                _database.RemoveEntity(nearest.Id);
                TxtStatus.Text = $"✓ {def.NameTR} yerleştirildi — boru bölündü.";
            }
            else
            {
                TxtStatus.Text = $"✓ {def.NameTR} yerleştirildi (boru çok kısa, bölünmedi).";
            }
        }
        else
        {
            TxtStatus.Text = $"✓ {def.NameTR} yerleştirildi (serbest konum).";
        }

        _database.AddEntity(valve);

        // Sonraki yerleştirme için Y ötele
        if (double.TryParse(TxtY.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double yVal))
            TxtY.Text = (yVal + 500).ToString(CultureInfo.InvariantCulture);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private static PipeEntity ClonePipe(PipeEntity src, Vector3D start, Vector3D end)
        => new PipeEntity(start, end, src.InnerDiameter)
        {
            SystemType = src.SystemType,
            Layer      = src.Layer,
            Color      = src.Color
        };

    private static double DistanceToSegment(Vector3D p, Vector3D s, Vector3D e)
    {
        var v  = e - s;
        var w  = p - s;
        double c2 = v.Dot(v);
        if (c2 <= 0) return p.DistanceTo(s);
        double b = Math.Clamp(w.Dot(v) / c2, 0.0, 1.0);
        return p.DistanceTo(s + v * b);
    }

    private static double ParseDouble(string s, double fallback)
        => double.TryParse(s.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : fallback;
}
