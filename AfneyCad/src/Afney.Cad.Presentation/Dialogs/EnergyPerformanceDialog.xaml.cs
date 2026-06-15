using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs;

public partial class EnergyPerformanceDialog
{
    private readonly ObservableCollection<EnergyPerformanceService.BuildingElement> _elements = [];

    public EnergyPerformanceDialog()
    {
        InitializeComponent();
        ElementGrid.ItemsSource = _elements;
        ULimitGrid.ItemsSource = EnergyPerformanceService.TS825_U_Limits.ToList();
        LoadTemplate_Click(null!, null!);
    }

    private void AddElement_Click(object sender, RoutedEventArgs e)
        => _elements.Add(new() { Name = "Yeni Eleman", AreaM2 = 10, U_Wpm2K = 0.50 });

    private void RemElement_Click(object sender, RoutedEventArgs e)
    {
        if (ElementGrid.SelectedItem is EnergyPerformanceService.BuildingElement el)
            _elements.Remove(el);
    }

    private void LoadTemplate_Click(object sender, RoutedEventArgs e)
    {
        _elements.Clear();
        _elements.Add(new() { Name = "Dış Duvar",         AreaM2 = 90,  U_Wpm2K = 0.35 });
        _elements.Add(new() { Name = "Çatı / Tavan",      AreaM2 = 80,  U_Wpm2K = 0.20 });
        _elements.Add(new() { Name = "Zemin Döşemesi",    AreaM2 = 80,  U_Wpm2K = 0.40 });
        _elements.Add(new() { Name = "Pencere (Güney)",   AreaM2 = 12,  U_Wpm2K = 1.60 });
        _elements.Add(new() { Name = "Pencere (Kuzey)",   AreaM2 = 6,   U_Wpm2K = 1.60 });
        _elements.Add(new() { Name = "Kapı",              AreaM2 = 4,   U_Wpm2K = 2.00 });
        StatusText.Text = "Konut şablonu yüklendi.";
    }

    private EnergyPerformanceService.EnergyInput BuildInput()
    {
        double.TryParse(TxtArea.Text,       out double area);
        double.TryParse(TxtVolume.Text,     out double vol);
        double.TryParse(TxtACR.Text,        out double acr);
        double.TryParse(TxtLighting.Text,   out double light);
        double.TryParse(TxtOccupants.Text,  out double occ);
        double.TryParse(TxtHeatEff.Text,    out double heff);
        double.TryParse(TxtCoolEff.Text,    out double ceff);
        double.TryParse(TxtDHWEff.Text,     out double deff);
        double.TryParse(TxtSolarFrac.Text,  out double sf);

        return new EnergyPerformanceService.EnergyInput
        {
            ConditionedAreaM2   = area   > 0 ? area : 150,
            ConditionedVolumeM3 = vol    > 0 ? vol  : 420,
            City                = (CboCity.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "İstanbul",
            BuildingType        = (CboType.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Konut",
            AirChangeRate       = acr    > 0 ? acr  : 0.5,
            LightingWpm2        = light  > 0 ? light: 8,
            OccupantsCount      = occ    > 0 ? occ  : 4,
            HeatingSystemEff    = heff   > 0 ? heff : 0.90,
            CoolingSystemEff    = ceff   > 0 ? ceff : 3.5,
            DHWSystemEff        = deff   > 0 ? deff : 0.85,
            HasSolarDHW         = ChkSolar.IsChecked == true,
            SolarFractionDHW    = sf     > 0 ? sf   : 0.40,
            HasCooling          = true,
            Elements            = [.. _elements]
        };
    }

    private void Calculate_Click(object sender, RoutedEventArgs e)
    {
        var r = EnergyPerformanceService.Calculate(BuildInput());

        ResClass.Text = r.EnergyClass;
        ResScore.Text = $"{r.PrimaryEnergyKwhpm2:F0} kWh/(m²yıl)";
        ResHeat.Text  = $"{r.HeatingNeedKwhpm2:F0} kWh/(m²yıl)";
        ResCool.Text  = $"{r.CoolingNeedKwhpm2:F0} kWh/(m²yıl)";
        ResDHW.Text   = $"{r.DHWNeedKwhpm2:F0} kWh/(m²yıl)";
        ResLight.Text = $"{r.LightingNeedKwhpm2:F0} kWh/(m²yıl)";
        ResCO2.Text   = $"{r.CO2Kgpm2:F1} kg/(m²yıl)";
        ResHT.Text    = $"{r.HeatLossWK:F0} W/K";

        ClassBorder.Background = r.EnergyClass switch
        {
            "A++" or "A+"  => new SolidColorBrush(Color.FromRgb(27, 94, 32)),
            "A"            => new SolidColorBrush(Color.FromRgb(46, 125, 50)),
            "B"            => new SolidColorBrush(Color.FromRgb(104, 159, 56)),
            "C"            => new SolidColorBrush(Color.FromRgb(205, 220, 57)),
            "D"            => new SolidColorBrush(Color.FromRgb(255, 202, 40)),
            "E"            => new SolidColorBrush(Color.FromRgb(255, 143, 0)),
            "F"            => new SolidColorBrush(Color.FromRgb(216, 67, 21)),
            _              => new SolidColorBrush(Color.FromRgb(140, 0, 0))
        };

        ResRecommendations.Text = r.Recommendations.Count > 0
            ? "• " + string.Join("\n• ", r.Recommendations)
            : "✅ Bina iyi performans gösteriyor — belirgin iyileştirme gerekmiyor.";

        StatusText.Text = $"✓ Sınıf {r.EnergyClass} · {r.PrimaryEnergyKwhpm2:F0} kWh/(m²yıl) · CO₂: {r.CO2Kgpm2:F1} kg/(m²yıl)";
    }

    private void ExportHtml_Click(object sender, RoutedEventArgs e)
    {
        var r = EnergyPerformanceService.Calculate(BuildInput());
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html><head><meta charset='utf-8'><title>EKB Raporu</title>");
        sb.Append("<style>body{font-family:Arial;background:#0D1117;color:#ddd;padding:20px}h1{color:#FFD740}");
        sb.Append(".badge{display:inline-block;background:#1B5E20;color:#FFD740;font-size:60px;font-weight:900;padding:20px 40px;border-radius:12px;margin:10px}");
        sb.Append("table{border-collapse:collapse;width:100%}th{background:#0D3060;color:#90CAF9;padding:6px;border:1px solid #333}td{padding:5px;border:1px solid #333}</style></head><body>");
        sb.Append($"<h1>⚡ Enerji Kimlik Belgesi (EKB)</h1>");
        sb.Append($"<div class='badge'>{r.EnergyClass}</div> &nbsp; <span style='font-size:22px;color:#FFD740'>{r.PrimaryEnergyKwhpm2:F0} kWh/(m²yıl)</span>");
        sb.Append("<table style='margin-top:16px'>");
        sb.Append($"<tr><th>Kalem</th><th>Net İhtiyaç kWh/(m²yıl)</th></tr>");
        sb.Append($"<tr><td>Isıtma</td><td>{r.HeatingNeedKwhpm2:F1}</td></tr>");
        sb.Append($"<tr><td>Soğutma</td><td>{r.CoolingNeedKwhpm2:F1}</td></tr>");
        sb.Append($"<tr><td>SHW</td><td>{r.DHWNeedKwhpm2:F1}</td></tr>");
        sb.Append($"<tr><td>Aydınlatma</td><td>{r.LightingNeedKwhpm2:F1}</td></tr>");
        sb.Append($"<tr><td><b>Birincil Enerji</b></td><td><b>{r.PrimaryEnergyKwhpm2:F1}</b></td></tr>");
        sb.Append($"<tr><td>CO₂</td><td>{r.CO2Kgpm2:F2} kg/(m²yıl)</td></tr>");
        sb.Append("</table>");
        if (r.Recommendations.Count > 0)
        {
            sb.Append("<h2 style='color:#90CAF9'>Öneriler</h2><ul>");
            foreach (var rec in r.Recommendations) sb.Append($"<li>{rec}</li>");
            sb.Append("</ul>");
        }
        sb.Append($"<p style='color:#555;font-size:10px'>AfneyCAD EnergyPerformanceService · TS 825:2023 · EPBD · {DateTime.Now:dd.MM.yyyy HH:mm}</p>");
        sb.Append("</body></html>");

        string path = Path.Combine(Path.GetTempPath(), $"EKB_{DateTime.Now:yyyyMMdd}.html");
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = path, UseShellExecute = true });
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
