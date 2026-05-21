using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs;

public partial class DrawingUpdateDialog : Window
{
    public class UpdateOptions
    {
        public bool ShowLength    { get; set; } = true;
        public bool ShowDiameter  { get; set; } = true;
        public bool ShowSlope     { get; set; } = true;
        public bool ShowFillRatio { get; set; } = true;
        public bool ShowVelocity  { get; set; }
        public bool ShowFlow      { get; set; }
        public bool ShowLoadUnits { get; set; }
        public double TextHeight  { get; set; } = 50;
        public string Placement   { get; set; } = "Boru Üstü";
        public string Scope       { get; set; } = "All";
    }

    private readonly CadDatabase _database;
    private readonly WasteWaterCalcSheetService.CalcSheetResult? _calcResult;

    public DrawingUpdateDialog(CadDatabase database, WasteWaterCalcSheetService.CalcSheetResult? calcResult = null)
    {
        InitializeComponent();
        _database   = database;
        _calcResult = calcResult;
    }

    private void AutoPlace_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var opts = BuildOptions();
            int placed = PlaceAnnotations(opts);

            StatusBorder.Visibility = Visibility.Visible;
            TxtStatus.Text = $"✅ Otomatik yerleştirme tamamlandı.\n" +
                             $"{placed} boru segmenti için etiket eklendi.\n" +
                             $"Çizim ekranında görünür olması için 'Yenile' (F5) tuşuna basın.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Yerleştirme hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private int PlaceAnnotations(UpdateOptions opts)
    {
        // Mevcut etiketleri temizle (önceki güncelleme artıkları)
        var oldLabels = _database.GetAllEntities()
            .OfType<TextEntity>()
            .Where(t => t.Style == "WW_ANNOTATION")
            .ToList();
        foreach (var lbl in oldLabels)
            _database.RemoveEntity(lbl.Id);

        // Boru filtresi
        var pipes = _database.GetAllEntities()
            .OfType<PipeEntity>()
            .Where(p => opts.Scope switch
            {
                "WasteWater" => p.SystemType == MechanicalSystemType.WasteWater ||
                                p.SystemType == MechanicalSystemType.RainWater,
                _            => true
            })
            .ToList();

        // Hesap föyü satırlarını segmentId ile eşleştir
        var rowMap = _calcResult?.Rows
            .ToDictionary(r => r.SegmentId, r => r) ?? [];

        double textH = opts.TextHeight;
        int count = 0;

        foreach (var pipe in pipes)
        {
            // Metin içeriğini oluştur
            var parts = new List<string>();
            string segId = pipe.Id.ToString()[..8];

            if (opts.ShowDiameter)  parts.Add($"DN{pipe.InnerDiameter * 1000:F0}");
            if (opts.ShowLength)    parts.Add($"L={pipe.Length:F1}m");
            if (opts.ShowSlope)     parts.Add($"i={pipe.Slope * 100:F1}%");

            if (rowMap.TryGetValue(segId, out var row))
            {
                if (opts.ShowFillRatio) parts.Add($"D={row.FillRatio:F0}%");
                if (opts.ShowVelocity)  parts.Add($"v={row.VelocityMs:F2}m/s");
                if (opts.ShowFlow)      parts.Add($"Q={row.DesignFlowLs:F3}l/s");
                if (opts.ShowLoadUnits) parts.Add($"DU={row.LoadUnits:F1}");
            }

            if (parts.Count == 0) continue;

            string label = string.Join("  ", parts);

            // Boru orta noktası
            var midX = (pipe.StartPoint.X + pipe.EndPoint.X) / 2;
            var midY = (pipe.StartPoint.Y + pipe.EndPoint.Y) / 2;
            var midZ = (pipe.StartPoint.Z + pipe.EndPoint.Z) / 2;

            double yOffset = opts.Placement switch
            {
                "Boru Altı" => -textH * 1.5,
                "Boru Ortası" => 0,
                _           =>  textH * 0.8,
            };

            // Boru açısını hesapla
            double dx = pipe.EndPoint.X - pipe.StartPoint.X;
            double dy = pipe.EndPoint.Y - pipe.StartPoint.Y;
            double rotation = Math.Atan2(dy, dx) * 180 / Math.PI;

            var textPos = new Vector3D(midX, midY + yOffset, midZ);
            var text = new TextEntity(label, textPos, textH, rotation)
            {
                Style  = "WW_ANNOTATION",
                Color  = 0xFFFFFFFF // beyaz
            };

            _database.AddEntity(text);
            count++;
        }

        return count;
    }

    private UpdateOptions BuildOptions()
    {
        double textH = (CmbTextHeight.SelectedItem as ComboBoxItem)?.Content?.ToString() switch
        {
            "75 mm"  => 75,
            "100 mm" => 100,
            "150 mm" => 150,
            _        => 50
        };

        string scope = RbWaste.IsChecked == true ? "WasteWater" :
                       RbSelected.IsChecked == true ? "Selected" : "All";

        return new UpdateOptions
        {
            ShowLength    = ChkLength.IsChecked == true,
            ShowDiameter  = ChkDiameter.IsChecked == true,
            ShowSlope     = ChkSlope.IsChecked == true,
            ShowFillRatio = ChkFillRatio.IsChecked == true,
            ShowVelocity  = ChkVelocity.IsChecked == true,
            ShowFlow      = ChkFlow.IsChecked == true,
            ShowLoadUnits = ChkLoadUnits.IsChecked == true,
            TextHeight    = textH,
            Placement     = (CmbPlacement.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Boru Üstü",
            Scope         = scope,
        };
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
