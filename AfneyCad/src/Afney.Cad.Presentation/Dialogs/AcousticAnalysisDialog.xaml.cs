using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs;

public partial class AcousticAnalysisDialog
{
    private readonly AcousticAnalysisService _service = new();
    private readonly CadDatabase? _database;
    private AcousticResult? _last;

    public AcousticAnalysisDialog(CadDatabase? database = null)
    {
        InitializeComponent();
        _database = database;
    }

    /*
       NE: Susturucu Seç (PickSilencer_Click)
       NEDEN — GERÇEK BOŞLUK (Session #75 iş akışı denetiminde bulundu): `ApplyToNoiseBudget`
              servis metodu ve SilencerSelectionDialog ayrı ayrı vardı ama aralarında UI
              bağlantısı yoktu — kullanıcı SilencerSelectionDialog'da bir model seçse bile
              o modelin kritik bant IL değerini burada elle yeniden yazmak zorundaydı.
    */
    private void PickSilencer_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SilencerSelectionDialog { Owner = this };
        if (dlg.ShowDialog() == true && dlg.SelectedInsertionLossDb.HasValue)
        {
            TxtSilencerLoss.Text = dlg.SelectedInsertionLossDb.Value.ToString("F0", CultureInfo.InvariantCulture);
        }
    }

    private void Calculate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var input = new AcousticInput
            {
                AirFlowM3h = ParseDouble(TxtAirFlow.Text, 1000),
                FanPressurePa = ParseDouble(TxtFanPressure.Text, 400),
                FanEfficiency = ParseDouble(TxtFanEff.Text, 0.7),
                DuctWidthMm = ParseDouble(TxtDuctWidth.Text, 400),
                DuctLengthM = ParseDouble(TxtDuctLength.Text, 10),
                IsDuctLined = ChkDuctLined.IsChecked == true,
                BranchCount = (int)ParseDouble(TxtBranchCount.Text, 2),
                ElbowCount = (int)ParseDouble(TxtElbowCount.Text, 3),
                SilencerInsertionLossDb = ParseDouble(TxtSilencerLoss.Text, 0),
                TerminalVelocityMs = ParseDouble(TxtTerminalVel.Text, 3.0),
                RoomVolumeM3 = ParseDouble(TxtRoomVolume.Text, 50),
                RoomType = (CboRoomType.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Ofis (Özel)"
            };

            var r = _service.AnalyzeSystem(input);
            _last = r;

            ResFanLw.Text = $"{r.FanSoundPowerLw:F0} dB";
            ResDuctAtt.Text = $"{r.DuctAttenuationDb:F1} dB";
            ResTerminalLw.Text = $"{r.TerminalNoiseLw:F0} dB";
            ResRoomCorr.Text = $"{r.RoomCorrectionDb:F1} dB";
            ResRoomLp.Text = $"{r.RoomSoundPressureLp:F0} dBA";
            ResNrStatus.Text = $"NR {r.NRLimit} — {(r.NRCompliant ? "UYGUN" : "AŞILDI")}";
            ResRecommendation.Text = r.Recommendation;

            SummaryText.Text = $"{input.RoomType}: Debi={input.AirFlowM3h:F0} m³/h, Dallanma={input.BranchCount}, Dirsek={input.ElbowCount}";
            StatusText.Text = r.NRCompliant
                ? $"✓ Oda ses basıncı {r.RoomSoundPressureLp:F0} dBA, NR {r.NRLimit} sınırında uygun."
                : $"⚠ Oda ses basıncı {r.RoomSoundPressureLp:F0} dBA, NR {r.NRLimit} sınırını aşıyor.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Hata: {ex.Message}";
        }
    }

    /*
       NE: Çizime Ekle (AddToDrawing_Click)
       NEDEN — Session #75 iş akışı denetiminde bulunan boşluk: bu ekran hesap sonucunu
              hiçbir yere yazmıyordu. `TS825InsulationDialog` ile aynı desen.
    */
    private void AddToDrawing_Click(object sender, RoutedEventArgs e)
    {
        if (_last is null) { Calculate_Click(sender, e); if (_last is null) return; }
        if (_database is null)
        {
            MessageBox.Show("Aktif çizim bulunamadı.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var r = _last;
        string txt = $"HVAC Gürültü Analizi — Oda Ses Basıncı={r.RoomSoundPressureLp:F0} dBA, NR {r.NRLimit} " +
                     $"({(r.NRCompliant ? "UYGUN" : "AŞILDI")}), Fan Lw={r.FanSoundPowerLw:F0} dB";

        var te = new TextEntity(txt, new Vector3D(0, 0, 0), 200)
        {
            Color = 0xFF90CAF9,
            Layer = "GURULTU_ANALIZ_HESAP"
        };
        _database.AddEntity(te);
        MessageBox.Show("Gürültü analizi özeti çizime eklendi (katman: GURULTU_ANALIZ_HESAP, konum: 0,0).",
            "Tamamlandı", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private static double ParseDouble(string s, double fallback)
        => double.TryParse((s ?? "").Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : fallback;
}
