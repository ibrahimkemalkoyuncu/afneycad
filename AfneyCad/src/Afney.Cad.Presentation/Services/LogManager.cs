using Serilog;
using System;
using System.IO;

namespace Afney.Cad.Presentation.Services
{
    public static class LogManager
    {
        /*
           NE: Loglama Sistemini Başlat (Initialize)
           NEDEN: Serilog yapılandırmasını kurarak, uygulama çalışma günlüklerini (app-.txt) ve sadece kritik hataları (error-.txt) ayrı dosyalarda tutmak için.
        */
        public static void Initialize()
        {
            // Log dosyaları için klasör
            string logFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            if (!Directory.Exists(logFolder)) Directory.CreateDirectory(logFolder);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                // Genel Loglar (Günlük yuvarlanan dosya)
                .WriteTo.File(Path.Combine(logFolder, "app-.txt"),
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                    retainedFileCountLimit: 30,
                    fileSizeLimitBytes: 50L * 1024 * 1024,
                    rollOnFileSizeLimit: true)
                // Sadece Hatalar
                .WriteTo.File(Path.Combine(logFolder, "error-.txt"),
                    rollingInterval: RollingInterval.Day,
                    restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Error,
                    retainedFileCountLimit: 30,
                    fileSizeLimitBytes: 50L * 1024 * 1024,
                    rollOnFileSizeLimit: true)
                .CreateLogger();

            Log.Information("=== AfneyCAD Başlatıldı ===");
        }

        public static void CloseAndFlush()
        {
            Log.Information("=== AfneyCAD Kapatılıyor ===");
            Log.CloseAndFlush();
        }
    }
}
