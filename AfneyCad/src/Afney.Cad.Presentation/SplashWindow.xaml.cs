using System;
using System.Threading.Tasks;
using System.Windows;

namespace Afney.Cad.Presentation;

public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    /*
       NE: Yükleme Tamamlandığında (OnLoaded)
       NEDEN: Arka plan yükleme simülasyonunu başlatmak ve ardından ana pencereyi (MainWindow) ekrana getirip splash ekranını kapatmak için.
    */
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await SimulateLoading();
        
        // Open Main Window
        var mainWindow = new MainWindow();
        mainWindow.Show();
        
        // Close Splash
        Close();
    }

    private async Task SimulateLoading()
    {
        var modules = new[] 
        { 
            "Loading Core Kernel...", 
            "Initializing Memory Database (ACDB)...", 
            "Loading Geometry Engine...", 
            "Loading Mechanical Module...",  // NEW!
            "Loading Render System (SkiaSharp)...", 
            "Loading Plugins...", 
            "Connecting to AI Assistant...",
            "Preparing Canvas..."
        };

        for (int i = 0; i < modules.Length; i++)
        {
            StatusText.Text = modules[i];
            LoadingProgress.Value = (i + 1) * (100.0 / modules.Length);
            await Task.Delay(500);
        }

        StatusText.Text = "Ready.";
        await Task.Delay(300);
    }
}
