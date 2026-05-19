using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Afney.Cad.Database.Core;
using Afney.Cad.Database.Persistence;
using Serilog;

namespace Afney.Cad.Presentation.Services;

/*
   NE: Otomatik Kayıt Servisi (AutoSaveService)
   NEDEN: Elektrik kesintisi, uygulama çökmesi (Crash) gibi durumlarda kullanıcının tasarım verilerini (Saatler süren emeği) kaybetmemesi için.
   
   MÜHENDİSLİK DETAYI (QA - Faz 28):
   - Belirlenen zaman aralıklarında (Örn: 5 Dk) arka planda veritabanının Snapshot'ını alır.
   - Dosya IO işlemleri asenkron (async/await) yapılır, böylece UI iş parçacığını (Main Thread) kilitlemez/dondurmaz.
   - Hata yönetimi (try-catch) sayesinde kayıt sırasındaki izin sorunları uygulamanın kapanmasına yol açmaz.
*/
public class AutoSaveService : IDisposable
{
    private readonly CadDatabase _database;
    private readonly CadSerializer _serializer;
    private readonly string _autoSaveDirectory;
    private readonly string _autoSaveFileName = "autosave.afney.bak";
    
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _backgroundTask;
    private readonly TimeSpan _saveInterval;
    
    public event Action<string>? OnAutoSaveCompleted;
    public event Action<Exception>? OnAutoSaveFailed;

    public AutoSaveService(CadDatabase database, TimeSpan saveInterval)
    {
        _database = database;
        _saveInterval = saveInterval;
        _serializer = new CadSerializer();
        
        // Kullanıcının Belgelerim klasörü içine AfneyCAD/AutoSave dizini oluştur
        string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        _autoSaveDirectory = Path.Combine(documentsPath, "AfneyCAD", "AutoSave");
        
        if (!Directory.Exists(_autoSaveDirectory))
        {
            Directory.CreateDirectory(_autoSaveDirectory);
        }
    }

    /*
       NE: Arka Plan Sürecini Başlat
       NEDEN: Task tabanlı asenkron döngüyü ayağa kaldırarak beklemeye (Delay) geçmesi için.
    */
    public void Start()
    {
        if (_cancellationTokenSource != null) return; // Zaten çalışıyor
        
        _cancellationTokenSource = new CancellationTokenSource();
        _backgroundTask = Task.Run(() => AutoSaveLoop(_cancellationTokenSource.Token));
        
        Log.Information("🔄 AutoSave servisi başlatıldı. Periyot: {Interval} dk", _saveInterval.TotalMinutes);
    }

    /*
       NE: Arka Plan Sürecini Durdur
       NEDEN: Uygulama kapatılırken iptal sinyali gönderip kaynakları güvenli biçimde temizlemek için.
    */
    public void Stop()
    {
        if (_cancellationTokenSource == null) return;
        
        _cancellationTokenSource.Cancel();
        try
        {
            _backgroundTask?.Wait(2000); // 2 saniye kapanmasını bekle
        }
        catch (AggregateException) 
        {
            // TaskCanceledException yutulur
        }
        
        _cancellationTokenSource.Dispose();
        _cancellationTokenSource = null;
        
        Log.Information("🛑 AutoSave servisi durduruldu.");
    }

    /*
       NE: Otomatik Kayıt Döngüsü
       NEDEN: Sonsuz döngü içinde bekleme -> kayıt -> bekleme stratejisiyle arka plan worker'ı çalıştırmak için.
    */
    private async Task AutoSaveLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                // X dakika bekle
                await Task.Delay(_saveInterval, token);
                
                // İptal edildiyse kayıt etmeden çık
                if (token.IsCancellationRequested) break;
                
                await PerformSaveAsync();
            }
            catch (TaskCanceledException)
            {
                // Döngü sonlandırıldı
                break;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "❌ AutoSave arka plan döngüsünde kritik hata.");
                OnAutoSaveFailed?.Invoke(ex);
            }
        }
    }

    /*
       NE: Asenkron Kayıt İşlemi
       NEDEN: Veritabanını Serialize edip diske yazma işlemi (Büyük projelerde saniyeler sürebilir) UI'ı dondurmasın diye asenkron IO ile yapmak için.
    */
    private async Task PerformSaveAsync()
    {
        try
        {
            // 1. O anki veritabanını dondur/kopyala (Serialization UI Thread'i az miktar dondurabilir ama Entity listesi kopyası alırsa hızlanır)
            // CadSerializer şu an JSON üretiyor. Büyük projede zaman alacak, FileStream ile asenkron yazacağız.
            string jsonContent = _serializer.Serialize(_database);
            
            // 2. Dosya yolu oluştur
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string backupFileName = $"autosave_{timestamp}.afney.bak"; // Geriye dönük geçmiş de tutabilir
            string filePath = Path.Combine(_autoSaveDirectory, backupFileName);
            
            // 3. Sabit "Son Kayıt" dosyasının üzerine yaz (Kısa erişim için)
            string latestFilePath = Path.Combine(_autoSaveDirectory, _autoSaveFileName);
            
            // Asenkron yazma
            await File.WriteAllTextAsync(latestFilePath, jsonContent);
            await File.WriteAllTextAsync(filePath, jsonContent);
            
            // 4. Eski Backup dosyalarını temizle (Örn: Son 10 yedeği tut, geri kalanını sil)
            CleanupOldBackups();
            
            Log.Information("💾 AutoSave tamamlandı ({Bytes} byte): {FilePath}", jsonContent.Length, latestFilePath);
            OnAutoSaveCompleted?.Invoke(latestFilePath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ AutoSave sırasında dosya yazma hatası!");
            OnAutoSaveFailed?.Invoke(ex);
        }
    }
    
    /*
       NE: Eski Yedekleri Temizle (Rotation)
       NEDEN: Hard Diskin sadece otomatik kayıtlarla (GB'larca) dolmasını önlemek için Retention Policy (Örn: Sadece son 5 kayıt) uygulamak.
    */
    private void CleanupOldBackups()
    {
        try
        {
            // Sadece timestamp'li yedek dosyaları al, sırala ve eski olanları sil (Maximum 5 adet tut)
            var files = Directory.GetFiles(_autoSaveDirectory, "autosave_*.afney.bak")
                                 .Select(f => new FileInfo(f))
                                 .OrderByDescending(f => f.CreationTime)
                                 .ToList();

            const int MAX_BACKUPS = 5;
            if (files.Count > MAX_BACKUPS)
            {
                for (int i = MAX_BACKUPS; i < files.Count; i++)
                {
                    files[i].Delete();
                    Log.Debug("🗑️ Eski AutoSave yedeği silindi: {File}", files[i].Name);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "⚠️ Eski yedekleri silerken hata oluştu.");
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
