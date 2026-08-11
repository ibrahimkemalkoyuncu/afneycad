using System.Windows;
using Serilog;
using System;

namespace Afney.Cad.Presentation;

public partial class App : System.Windows.Application
{
    /*
       NE: Uygulama Başlatma (OnStartup)
       NEDEN: Loglama sistemini kurmak, lisans doğrulaması yapmak ve kullanıcıya ana pencere açılmadan önce splash veya lisans ekranını sunmak için.
    */
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // LOGGING BAŞLAT
        Services.LogManager.Initialize();

        // GLOBAL EXCEPTION HANDLING
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

        Log.Information("Uygulama başlatılıyor...");
        
        // --- ENTERPRISE DEPLOYMENT: LİSANS KONTROLÜ ---
        var licenseManager = new Afney.Cad.Application.Licensing.LicenseManager();
        var status = licenseManager.Validate();
        
        if (status != Afney.Cad.Application.Licensing.LicenseStatus.Valid)
        {
            // Lisans penceresini aç (Splash'ten önce)
            var dialog = new Dialogs.LicenseDialog();
            bool? result = dialog.ShowDialog();
            
            if (result != true)
            {
                Log.Warning("Lisans girişi yapılmadı veya iptal edildi. Uygulama kapatılıyor.");
                Shutdown(); // Uygulamayı kapat
                return;
            }
            
            if (dialog.IsTrialMode)
            {
                Log.Information("Kullanıcı DENEME MODU ile devam ediyor.");
                MessageBox.Show("Deneme Modundasınız. Bazı özellikler (Kaydetme, Export) kısıtlanmıştır.", "AfneyCAD Enterprise Trial", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        else
        {
             Log.Information("Geçerli Enterprise lisansı bulundu.");
        }
        
        // --- SPLASH SCREEN BAŞLAT ---
        // Lisans onaylandıysa veya Demo ise Splash ekranını aç
        var splash = new SplashWindow();
        splash.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("Uygulama sonlandırılıyor...");
        Services.LogManager.CloseAndFlush();
        base.OnExit(e);
    }

    private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Fatal(e.Exception, "Beklenmeyen UI Hatası!");
        MessageBox.Show($"Beklenmeyen bir hata oluştu:\n{e.Exception.Message}\nLog dosyasına kaydedildi.", "Kritik Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true; // Uygulamanın çökmesini engelle
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
             Log.Fatal(ex, "Kritik Sistem Hatası (Non-UI)!");
             MessageBox.Show($"Sistem hatası:\n{ex.Message}\nUygulama kapatılacak.", "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Stop);
        }
    }

    /*
       NE: Gözlemlenmeyen Task Hatası (TaskScheduler.UnobservedTaskException)
       NEDEN: Arka planda çalışan Task.Run işlemlerinde (örn. AutoSaveService.AutoSaveLoop) oluşup
       hiçbir yerde await/try-catch ile gözlemlenmeyen exception'lar, finalizer thread'inde sessizce
       kaybolmasın veya (eski .NET davranışında) process'i çökertmesin diye loglanır.
       e.SetObserved() çağrısı, exception'ın "gözlemlendiğini" işaretleyerek riski ortadan kaldırır.
    */
    private void TaskScheduler_UnobservedTaskException(object? sender, System.Threading.Tasks.UnobservedTaskExceptionEventArgs e)
    {
        Log.Fatal(e.Exception, "Gözlemlenmeyen Arka Plan Task Hatası (UnobservedTaskException)!");
        e.SetObserved();
    }
}
