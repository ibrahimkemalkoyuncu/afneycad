using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Mechanical.Models;

namespace Afney.Cad.Mechanical.Services;

/*
NE: Kat Yönetim Servisi (LevelManager)
NEDEN: Binadaki kat/seviye bilgilerini merkezi olarak yönetmek ve MEP hesaplamalarına sunmak için.

MÜHENDİSLİK DETAYI (Mebrure Hanım):
- FINE SANI benzeri Level Manager: Kat tanımlama, düzenleme ve silme işlemlerini yönetir.
- Elevation sırasına göre otomatik sıralama yapar.
- Riser Engine ve Hydraulic Calculation modüllerinin veri kaynağıdır.
- Event-driven: Kat eklenince/silinince ilgili sistemler haberdar olur.
*/
public class LevelManager
{
    private readonly List<MepLevel> _levels = new();
    
    // Event: Kat tablosu değiştiğinde
    public event Action? LevelTableChanged;
    
    /*
    NE: Constructor - Varsayılan Katları Oluşturur
    NEDEN: Türkiye standartlarına göre tipik bir bina için başlangıç katları.
    */
    public LevelManager()
    {
        // Varsayılan katlar (Türkiye standartları - 3m kat yüksekliği)
        _levels.Add(new MepLevel("Zemin Kat", 0, 3000));
        _levels.Add(new MepLevel("1. Kat", 3000, 3000));
        _levels.Add(new MepLevel("2. Kat", 6000, 3000));
        _levels.Add(new MepLevel("3. Kat", 9000, 3000));
    }
    
    /*
    NE: Kat Listesini Döndürür
    NEDEN: Read-only erişim sağlar, dış müdahaleyi engeller.
    */
    public IReadOnlyList<MepLevel> GetLevels()
    {
        return _levels.AsReadOnly();
    }
    
    /*
    NE: Yeni Kat Ekler
    NEDEN: Kullanıcı proje ihtiyacına göre kat tanımlamak için.
    */
    public void AddLevel(MepLevel level)
    {
        if (level == null) throw new ArgumentNullException(nameof(level));
        
        // Aynı elevation'da kat var mı kontrol et
        if (_levels.Any(l => Math.Abs(l.Elevation - level.Elevation) < 1.0))
        {
            throw new InvalidOperationException($"Bu kotda ({level.Elevation}mm) zaten bir kat tanımlı!");
        }
        
        _levels.Add(level);
        
        // Elevation'a göre sırala (alçaktan yükseğe)
        _levels.Sort((a, b) => a.Elevation.CompareTo(b.Elevation));
        
        LevelTableChanged?.Invoke();
    }
    
    /*
    NE: Kat Siler
    NEDEN: Yanlış tanımlanmış veya gereksiz katları temizlemek için.
    */
    public void RemoveLevel(string levelName)
    {
        var level = _levels.FirstOrDefault(l => l.Name == levelName);
        if (level != null)
        {
            _levels.Remove(level);
            LevelTableChanged?.Invoke();
        }
    }
    
    /*
    NE: Kat Günceller
    NEDEN: Mevcut kat özelliklerini (elevation, height) değiştirmek için.
    */
    public void UpdateLevel(string oldName, string newName, double newElevation, double newHeight)
    {
        var level = _levels.FirstOrDefault(l => l.Name == oldName);
        if (level != null)
        {
            level.Name = newName;
            level.Elevation = newElevation;
            level.Height = newHeight;
            
            // Yeniden sırala
            _levels.Sort((a, b) => a.Elevation.CompareTo(b.Elevation));
            
            LevelTableChanged?.Invoke();
        }
    }
    
    /*
    NE: Belirli Bir Elevation Değerinin Hangi Kat Aralığında Olduğunu Bulur
    NEDEN: Boru veya vitrifiye koordinatından kat bilgisi çıkarmak için (Riser Engine).
    */
    public MepLevel? GetLevelAtElevation(double z)
    {
        // En yakın ALT kattı bul
        return _levels
            .Where(l => l.Elevation <= z)
            .OrderByDescending(l => l.Elevation)
            .FirstOrDefault();
    }
    
    /*
    NE: Tüm Katları Temizler
    */
    public void Clear()
    {
        _levels.Clear();
        LevelTableChanged?.Invoke();
    }
}
