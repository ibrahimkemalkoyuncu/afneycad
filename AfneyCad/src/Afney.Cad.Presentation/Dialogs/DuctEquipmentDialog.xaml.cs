using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;
using Afney.Cad.Mechanical.Services;
using static Afney.Cad.Mechanical.Services.AirFilterSelectionService;

namespace Afney.Cad.Presentation.Dialogs;

public partial class DuctEquipmentDialog : Window
{
    public DuctEquipmentEntity? Prototype { get; private set; }

    public DuctEquipmentDialog()
    {
        InitializeComponent();
        OnTypeChanged(this, new RoutedEventArgs());
    }

    private DuctEquipmentType CurrentType =>
        RbFlex.IsChecked == true ? DuctEquipmentType.FlexConnector :
        RbFilter.IsChecked == true ? DuctEquipmentType.Filter :
        RbHeat.IsChecked == true ? DuctEquipmentType.HeatingCoil :
        RbCool.IsChecked == true ? DuctEquipmentType.CoolingCoil :
        RbCav.IsChecked == true ? DuctEquipmentType.CavBox : DuctEquipmentType.VavBox;

    private void OnTypeChanged(object sender, RoutedEventArgs e)
    {
        if (PanelFlex == null) return;

        var t = CurrentType;
        PanelFlex.Visibility = t == DuctEquipmentType.FlexConnector ? Visibility.Visible : Visibility.Collapsed;
        PanelFilter.Visibility = t == DuctEquipmentType.Filter ? Visibility.Visible : Visibility.Collapsed;
        PanelCoil.Visibility = t is DuctEquipmentType.HeatingCoil or DuctEquipmentType.CoolingCoil ? Visibility.Visible : Visibility.Collapsed;
        PanelBox.Visibility = t is DuctEquipmentType.CavBox or DuctEquipmentType.VavBox ? Visibility.Visible : Visibility.Collapsed;
        RowRh.Visibility = t == DuctEquipmentType.CoolingCoil ? Visibility.Visible : Visibility.Collapsed;

        if (t == DuctEquipmentType.HeatingCoil) { TxtAirIn.Text = "5"; TxtAirOut.Text = "30"; TxtWaterIn.Text = "80"; TxtWaterOut.Text = "60"; TxtCoilVel.Text = "3.0"; }
        if (t == DuctEquipmentType.CoolingCoil) { TxtAirIn.Text = "28"; TxtAirOut.Text = "13"; TxtWaterIn.Text = "7"; TxtWaterOut.Text = "12"; TxtCoilVel.Text = "2.5"; }

        TxtResult.Text = "";
        Prototype = null;
    }

    private static double Num(string s) =>
        double.Parse(s.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture);

    private static double? OptNum(string s) => string.IsNullOrWhiteSpace(s) ? null : Num(s);

    private static string F(double v, int digits = 1) => v.ToString("F" + digits, CultureInfo.CurrentCulture);

    private void OnCalculate_Click(object sender, RoutedEventArgs e) => Calculate();

    private void OnPlace_Click(object sender, RoutedEventArgs e)
    {
        if (!Calculate()) return;
        DialogResult = true;
    }

    private bool Calculate()
    {
        try
        {
            double diameter = Num(TxtDiameter.Text);
            double flow = Num(TxtFlow.Text);
            if (diameter <= 0 || flow <= 0) throw new FormatException("Kanal çapı ve debi sıfırdan büyük olmalı.");

            var t = CurrentType;
            var eq = new DuctEquipmentEntity(Vector3D.Zero, t, diameter) { AirFlowM3h = flow };
            var sb = new StringBuilder();
            bool ok = true;

            switch (t)
            {
                case DuctEquipmentType.FlexConnector:
                {
                    double len = Num(TxtFlexLength.Text);
                    var check = FlexConnectorService.CheckLength(len, ChkStrictFlex.IsChecked == true);
                    eq.Size = len;
                    eq.PressureDropPa = FlexConnectorService.EstimatePressureDropPa(diameter, len, flow);
                    sb.AppendLine(check.Message);
                    sb.AppendLine($"Tahmini basınç kaybı: {F(eq.PressureDropPa)} Pa (pürüzlülük {FlexConnectorService.DefaultRoughnessMm:F1} mm varsayımı; üretici verisi öncelikli).");
                    ok = check.IsCompliant;
                    break;
                }
                case DuctEquipmentType.Filter:
                {
                    var oda = (OutdoorAirCategory)Math.Clamp((int)Num(TxtOda.Text), 1, 3);
                    var sup = (SupplyAirCategory)Math.Clamp((int)Num(TxtSup.Text), 1, 5);
                    var req = GetRecommendedMinimum(oda, sup);
                    var stages = TxtStages.Text.Split(',', ';').Where(s => !string.IsNullOrWhiteSpace(s)).Select(Num).ToList();
                    double cum = CumulativeEfficiencyPct(stages);
                    double vel = Num(TxtFilterVel.Text);
                    double dp = Num(TxtFilterDp.Text);

                    eq.FilterClass = FormatRequirement(req);
                    eq.PressureDropPa = dp;
                    sb.AppendLine($"Eurovent Rec. 4/23 Tablo 3 — ODA{(int)oda}/SUP{(int)sup}: {FormatRequirement(req)}");
                    sb.AppendLine($"Kümülatif verim ({string.Join(" + ", stages.Select(s => "%" + s))}): %{F(cum)}");
                    sb.AppendLine(MeetsRequirement(req, cum) ? "✓ Gereksinim sağlanıyor." : "✗ Gereksinim sağlanmıyor — kademe ekleyin veya sınıfı yükseltin.");
                    sb.AppendLine($"Dış hava girişi ilk kademe ≥ ePM10 %{MinFirstStageEpm10Pct}; nemlendirici sonrası ≥ ePM2,5 %{MinPostHumidifierEpm25Pct}.");
                    sb.AppendLine($"Gerekli yüz alanı: {F(RequiredFaceAreaM2(flow, vel), 3)} m² ({F(vel)} m/s).");
                    if (dp > 0) sb.AppendLine($"Fan gücüne etkisi (η=%60): {F(FanPowerForPressureDropW(flow, dp, 0.6), 0)} W.");
                    ok = MeetsRequirement(req, cum);
                    break;
                }
                case DuctEquipmentType.HeatingCoil:
                {
                    var r = AirCoilSelectionService.SelectHeating(flow, Num(TxtAirIn.Text), Num(TxtAirOut.Text), Num(TxtWaterIn.Text), Num(TxtWaterOut.Text), Num(TxtCoilVel.Text));
                    AppendCoil(sb, r);
                    eq.CapacityKw = r.CapacityKw;
                    ok = r.IsValid;
                    break;
                }
                case DuctEquipmentType.CoolingCoil:
                {
                    var r = AirCoilSelectionService.SelectCooling(flow, Num(TxtAirIn.Text), Num(TxtRhIn.Text) / 100.0, Num(TxtAirOut.Text), Num(TxtRhOut.Text) / 100.0,
                        Num(TxtWaterIn.Text), Num(TxtWaterOut.Text), Num(TxtCoilVel.Text));
                    AppendCoil(sb, r);
                    if (r.IsValid)
                    {
                        sb.AppendLine($"Duyulur {F(r.SensibleKw)} kW, gizli {F(r.LatentKw)} kW, kondens {F(r.CondensateKgPerH)} kg/h.");
                        eq.CapacityKw = r.CapacityKw;
                    }
                    ok = r.IsValid;
                    break;
                }
                default:
                {
                    var kind = t == DuctEquipmentType.CavBox ? VavCavBoxService.BoxKind.Cav : VavCavBoxService.BoxKind.Vav;
                    var r = VavCavBoxService.Select(kind, flow, minOutdoorAirM3h: Num(TxtMinOa.Text), maxInletVelocityMs: Num(TxtMaxInletVel.Text),
                        coldSupplyC: OptNum(TxtColdSupply.Text), reheatDischargeC: OptNum(TxtReheatOut.Text));
                    if (r.IsValid)
                    {
                        eq.MinFlowM3h = r.MinFlowM3h;
                        eq.CapacityKw = r.ReheatCapacityKw;
                        eq.InnerDiameter = r.InletDiameterMm;
                        sb.AppendLine($"Giriş çapı: DN{r.InletDiameterMm:F0} ({F(r.InletVelocityAtMaxMs)} m/s @ maks, {F(r.InletVelocityAtMinMs)} m/s @ min)");
                        sb.AppendLine($"Debi aralığı: {F(r.MinFlowM3h, 0)}–{F(r.MaxFlowM3h, 0)} m³/h (turndown %{F(r.TurndownRatio * 100, 0)})");
                        if (r.ReheatCapacityKw > 0) sb.AppendLine($"Reheat yükü (min. debide): {F(r.ReheatCapacityKw)} kW");
                        sb.AppendLine("Δp ve NC değerleri üreticinin AHRI 880 tablosundan doğrulanmalıdır.");
                    }
                    foreach (var w in r.Warnings) sb.AppendLine("⚠ " + w);
                    ok = r.IsValid;
                    break;
                }
            }

            TxtResult.Text = sb.ToString();
            Prototype = ok ? eq : null;
            return ok;
        }
        catch (FormatException ex)
        {
            TxtResult.Text = "Girdi hatası: " + ex.Message;
            Prototype = null;
            return false;
        }
    }

    private static void AppendCoil(StringBuilder sb, AirCoilSelectionService.CoilResult r)
    {
        if (r.IsValid)
        {
            sb.AppendLine($"Kapasite: {F(r.CapacityKw)} kW");
            sb.AppendLine($"Su debisi: {F(r.WaterFlowLps, 3)} l/s");
            sb.AppendLine($"Yüz alanı: {F(r.FaceAreaM2, 3)} m² ({F(r.FaceVelocityMs)} m/s)");
        }
        foreach (var w in r.Warnings) sb.AppendLine("⚠ " + w);
    }
}
