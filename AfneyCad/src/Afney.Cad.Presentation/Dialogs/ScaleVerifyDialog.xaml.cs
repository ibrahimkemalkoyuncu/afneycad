using System;
using System.Globalization;
using System.Windows;

namespace Afney.Cad.Presentation.Dialogs;

/*
   NE: Ölçek Doğrulama Dialogu (ScaleVerifyDialog)
   NEDEN: Mimardan gelen bir DWG'nin birimi (INSUNITS) yanlış/eksik olabilir —
          DwgImportService artık dosyadaki INSUNITS'i doğru okuyup uyguluyor, ama
          dosyanın KENDİSİ bu bilgiyi hiç taşımıyorsa veya yanlış taşıyorsa otomatik
          algılama işe yaramaz. Bu dialog, kullanıcının çizimde iki nokta seçip
          (ScaleVerifyCommand) GERÇEK bilinen bir uzunlukla (ör. bir kapı genişliği:
          900mm) karşılaştırmasını sağlar — AutoCAD'in SCALE komutundaki "referans
          uzunluk" mantığıyla aynı fikir.
*/
public partial class ScaleVerifyDialog : Window
{
    private readonly double _measuredMm;

    /// <summary>Kullanıcı "Uygula"ya bastığında hesaplanan düzeltme çarpanı (yeni/eski).</summary>
    public double CorrectionFactor { get; private set; } = 1.0;

    public ScaleVerifyDialog(double measuredMm)
    {
        InitializeComponent();
        _measuredMm = measuredMm;

        MeasuredText.Text = measuredMm >= 1000
            ? $"{measuredMm / 1000.0:F3} m  ({measuredMm:F0} mm)"
            : $"{measuredMm:F1} mm";

        Loaded += (_, _) => RealLengthBox.Focus();
    }

    private void RealLengthBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (!double.TryParse(RealLengthBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double realMm) || realMm <= 0 || _measuredMm <= 0)
        {
            FactorText.Text = "Düzeltme çarpanı: —";
            VerdictText.Text = "Geçerli, pozitif bir uzunluk (mm) girin.";
            ApplyButton.IsEnabled = false;
            return;
        }

        double factor = realMm / _measuredMm;
        FactorText.Text = $"Düzeltme çarpanı: ×{factor:F4}  (yeni = eski × {factor:F4})";

        if (Math.Abs(factor - 1.0) < 0.01)
        {
            VerdictText.Text = "Ölçek zaten doğru görünüyor (fark %1'in altında) — düzeltmeye gerek olmayabilir, yine de uygulayabilirsiniz.";
        }
        else
        {
            string yon = factor > 1.0 ? "büyütülecek" : "küçültülecek";
            VerdictText.Text = $"Çizimdeki TÜM nesneler seçtiğiniz 1. nokta etrafında ×{factor:F4} oranında {yon}. Bu işlem Undo (Ctrl+Z) ile geri alınabilir.";
        }

        CorrectionFactor = factor;
        ApplyButton.IsEnabled = true;
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
