using System;
using System.Windows;
using Afney.Cad.Application.Licensing;

namespace Afney.Cad.Presentation.Dialogs;

public partial class LicenseDialog : Window
{
    private readonly LicenseManager _licenseManager;
    
    // UI'dan gelen property (Trial seçildi mi?)
    public bool IsTrialMode { get; private set; } = false;

    public LicenseDialog()
    {
        InitializeComponent();
        _licenseManager = new LicenseManager();
        
        // Önceki lisansı yükle (varsa)
        // Eğer geçersizse boş gelir, geçerliyse zaten burası açılmazdı (App.xaml.cs kontrol eder).
        // Ancak kullanıcı lisansı güncellemek için de açabilir.
    }

    private void ActivateButton_Click(object sender, RoutedEventArgs e)
    {
        var key = LicenseKeyBox.Text.Trim();
        var status = _licenseManager.ValidateKey(key);

        if (status == LicenseStatus.Valid)
        {
            _licenseManager.SaveLicense(key);
            MessageBox.Show("Lisans başarıyla aktive edildi!\nTeşekkürler.", "Aktivasyon Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
            
            DialogResult = true; // Pencereyi kapat ve devam et
            Close();
        }
        else
        {
            MessageBox.Show("Geçersiz lisans anahtarı. Lütfen tekrar deneyin.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void TrialButton_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show("Deneme Modunda devam edilsin mi?\nBazı özellikler kısıtlı olabilir.", "Deneme Modu", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
        {
            IsTrialMode = true;
            DialogResult = true; // Pencereyi kapat ve devam et (Demo olarak)
            Close();
        }
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false; // Uygulamayı kapat
        Close();
    }
}
