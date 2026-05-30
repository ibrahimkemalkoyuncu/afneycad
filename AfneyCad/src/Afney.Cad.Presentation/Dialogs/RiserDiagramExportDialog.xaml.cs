using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical;
using Afney.Cad.Mechanical.Engine;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Models;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs;

public partial class RiserDiagramExportDialog
{
    private readonly CadDatabase _database;
    private readonly MechanicalKernel? _kernel;
    private List<RiserSchema> _detectedRisers = [];

    public RiserDiagramExportDialog(CadDatabase database, MechanicalKernel? kernel = null)
    {
        InitializeComponent();
        _database = database;
        _kernel   = kernel;
        Loaded += (_, _) => RefreshRisers();
    }

    // ── Kolon Tespiti ────────────────────────────────────────────────────────────

    private void RefreshRisers_Click(object sender, RoutedEventArgs e) => RefreshRisers();

    private void RefreshRisers()
    {
        _detectedRisers.Clear();
        RiserCombo.Items.Clear();

        if (_kernel != null)
        {
            EnsureLevels();
            try
            {
                var entities = _database.GetAllEntities().OfType<MechanicalEntity>();
                _detectedRisers = _kernel.GetRiserSchemas(entities);
            }
            catch { /* topoloji henüz kurulmamış olabilir */ }
        }

        if (_detectedRisers.Count > 0)
        {
            foreach (var r in _detectedRisers)
                RiserCombo.Items.Add(new ComboBoxItem
                {
                    Content = $"{r.RiserName}  ({r.Floors.Count} kat · {r.TotalFlowRate:F2} l/s)"
                });
            RiserCombo.SelectedIndex = 0;
            RiserInfoText.Text = $"✓ {_detectedRisers.Count} kolon tespit edildi. Şema üretmek için bir kolon seçin.";
            ManualPanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            RiserInfoText.Text = _kernel == null
                ? "⚠ Kernel bağlantısı yok — manuel mod aktif."
                : "⚠ 3D modelde dikey boru (kolon) bulunamadı — manuel mod aktif.";
            ManualPanel.Visibility = Visibility.Visible;
        }
    }

    private void RiserCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        int idx = RiserCombo.SelectedIndex;
        if (idx < 0 || idx >= _detectedRisers.Count) return;
        var r = _detectedRisers[idx];
        RiserInfoText.Text =
            $"✓ {r.RiserName} · {r.Floors.Count} kat · LU: {r.TotalLoadUnits:F1} · {r.TotalFlowRate:F3} l/s";
    }

    // ── SVG Export ───────────────────────────────────────────────────────────────

    private void ExportSvg_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            List<RiserDiagramExportService.RiserFloor> floors;

            int selIdx = RiserCombo.SelectedIndex;
            if (selIdx >= 0 && selIdx < _detectedRisers.Count)
            {
                // Gerçek model verisi
                floors = _detectedRisers[selIdx].Floors
                    .Select(f => new RiserDiagramExportService.RiserFloor
                    {
                        Name         = f.FloorName,
                        Elevation    = f.Elevation,
                        BranchDN     = f.BranchDiameter > 0 ? f.BranchDiameter : 32,
                        FixtureCount = f.Fixtures.Count
                    })
                    .ToList();
            }
            else
            {
                // Manuel fallback
                int floorCount   = int.Parse(FloorCountInput.Text);
                double floorH    = double.Parse(FloorHeightInput.Text);
                double branchDN  = double.Parse(BranchDNInput.Text);
                floors = Enumerable.Range(0, floorCount)
                    .Select(i => new RiserDiagramExportService.RiserFloor
                    {
                        Name         = i == 0 ? "Zemin Kat" : $"{i}. Kat",
                        Elevation    = i * floorH,
                        BranchDN     = branchDN,
                        FixtureCount = 3
                    })
                    .ToList();
            }

            var service = new RiserDiagramExportService(_database);
            var diagram = service.GenerateRiserDiagram(floors);
            string svg  = service.ExportToSvg(diagram);
            string path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"KolonSemasi_{DateTime.Now:yyyyMMdd_HHmmss}.svg");
            System.IO.File.WriteAllText(path, svg, System.Text.Encoding.UTF8);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                { FileName = path, UseShellExecute = true });

            StatusText.Text =
                $"✅ SVG oluşturuldu: {floors.Count} kat · {diagram.Lines.Count} çizgi · {diagram.Labels.Count} etiket";
        }
        catch (Exception ex) { StatusText.Text = $"Hata: {ex.Message}"; }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private void EnsureLevels()
    {
        if (_kernel == null) return;
        if (!_kernel.LevelManager.GetLevels().Any())
        {
            _kernel.LevelManager.AddLevel(new MepLevel("Zemin Kat", 0, 3000));
            _kernel.LevelManager.AddLevel(new MepLevel("1. Kat", 3000, 3000));
        }
    }
}
