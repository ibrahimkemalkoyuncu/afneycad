using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Database.Core;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Models;
using Serilog;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Bina Birleştirme ve Otomasyon Servisi (BuildingAssemblyService)
   NEDEN: FineSANI'nin gerçek mühendislik gücü olan "Katları Üst Üste Bindirme" ve "Kolonları Otomatik Hizalama" sürecini yönetmek için.

   GÖREVLERİ (BIM Standartlarında):
   1. MASTER ORIGIN MANAGEMENT: Tüm katların aynı referans (0,0,0) noktasına göre hizalanmasını sağlar.
   2. 3D STACKING: Katları Z-ekseninde (Kot) kaydırarak gerçek 3D bina modelini oluşturur.
   3. AUTO-ALIGN RISERS: Farklı katlardaki ancak aynı XY koordinatındaki kolonları (Risers) topolojik olarak birbirine bağlar.
*/
public class BuildingAssemblyService
{
    private readonly CadDatabase _database;
    private readonly MechanicalKernel _kernel;

    public BuildingAssemblyService(CadDatabase database, MechanicalKernel kernel)
    {
        _database = database;
        _kernel = kernel;
    }

    /*
       NE: Bina Modelini Oluştur (AssembleBuilding)
       AMACI: Kat planlarını baz alarak 3D bütünsel tesisat ağını kurar.
    */
    public void AssembleBuilding(IEnumerable<LevelFileRegistration> registrations)
    {
        Log.Information(">>> BİNA MONTAJI BAŞLATILDI: Katlar üst üste dizelecek.");
        
        _database.Clear(); // Master dosyayı hazırla
        _kernel.TopologyGraph.Clear();

        foreach (var reg in registrations)
        {
            LoadAndTransformLevel(reg);
        }

        // EN KRİTİK ADIM: Katlar arası kolonları bağla
        AutoConnectCrossLevelRisers();
        
        Log.Information(">>> BİNA MONTAJI TAMAMLANDI: Kolonlar otomatik hizalandı.");
    }

    private void LoadAndTransformLevel(LevelFileRegistration reg)
    {
        // 1. Kat verisini yükle (Serileştirilmiş formdan veya dosyadan)
        // Not: Burada serializer kullanıldığını varsayıyoruz.
        var serializer = new Afney.Cad.Database.Persistence.CadSerializer();
        if (!System.IO.File.Exists(reg.FilePath)) return;

        string json = System.IO.File.ReadAllText(reg.FilePath);
        var data = serializer.Deserialize(json);

        if (data?.Entities == null) return;

        // 2. 3D Dönüşüm (Elevation/Kot Kaydırması)
        var zTransform = Matrix4x4.TranslationMatrix(0, 0, reg.Elevation);
        
        foreach (var entity in data.Entities)
        {
            entity.Transform(zTransform);
            _database.AddEntity(entity);
        }
    }

    /*
       NE: Otomatik Kolon Hizalama ve Koordinat Düzeltme (AutoConnectCrossLevelRisers)
       NEDEN: Üst üste gelen katlardaki boruların birbirini tam XY koordinatında "bulması" ve devrenin kusursuz tamamlanması için.
       
       NASIL (FineSANI ME-Fix Algoritması):
       1. Tüm dikey boruları (Vertical Pipes) bul.
       2. Kat sınırlarına yakın olan uçları saptal.
       3. Aynı XY koordinatındaki (Farklı Z) uçları birleştir.
       4. EĞER: Koordinatlar arasında ufak kaçıklık varsa (Offset), alt katı referans alıp üst katı milimetrik olarak "HİZALA" (Snap to Master).
    */
    public void AutoConnectCrossLevelRisers()
    {
        var pipes = _database.GetAllEntities().OfType<PipeEntity>().ToList();
        var verticalPipes = pipes.Where(p => IsVertical(p)).OrderBy(p => p.StartPoint.Z).ToList();
        
        const double xyTolerance = 50.0; // 5cm kaçıklık payı (AutoCAD standartlarında kabul edilebilir)
        int fixCount = 0;

        for (int i = 0; i < verticalPipes.Count; i++)
        {
            for (int j = i + 1; j < verticalPipes.Count; j++)
            {
                var pBase = verticalPipes[i]; // Alt Kat Kolonu
                var pUpper = verticalPipes[j]; // Üst Kat Kolonu

                // XY Pozisyon kontrolü (Üst katın alt ucu ile alt katın üst ucu arasındaki mesafe)
                double distXY = Math.Sqrt(Math.Pow(pBase.EndPoint.X - pUpper.StartPoint.X, 2) + 
                                          Math.Pow(pBase.EndPoint.Y - pUpper.StartPoint.Y, 2));

                if (distXY < xyTolerance) // Ufak bir kaçıklık var ama bunlar aslında aynı kolon!
                {
                    // --- MÜHENDİSLİK "FIX" İŞLEMİ (Snap to Master) ---
                    if (distXY > 0.1) // Eğer tam üst üste değilse hizala
                    {
                        var shiftX = pBase.EndPoint.X - pUpper.StartPoint.X;
                        var shiftY = pBase.EndPoint.Y - pUpper.StartPoint.Y;

                        // Üst kattaki kolonu (ve muhtemelen bağlı hatlarını) kaydır
                        // Şimdilik sadece kolonu hizalayalım (MEP Accuracy)
                        pUpper.StartPoint = new Vector3D(pBase.EndPoint.X, pBase.EndPoint.Y, pUpper.StartPoint.Z);
                        pUpper.EndPoint = new Vector3D(pBase.EndPoint.X, pBase.EndPoint.Y, pUpper.EndPoint.Z);
                        
                        fixCount++;
                    }

                    // Topolojik Bağlantı Kur
                    TryConnectPipes(pBase, pUpper);
                }
            }
        }
        
        if (fixCount > 0)
            Log.Information(">>> RISER ALIGNMENT: {Count} adet kolon dikey eksende milimetrik olarak hizalandı.", fixCount);
    }

    private void TryConnectPipes(PipeEntity pBase, PipeEntity pUpper)
    {
        // En yakın uçları (Ports) bul ve topolojiye işle
        var portsBase = pBase.GetPorts();
        var portsUpper = pUpper.GetPorts();

        foreach (var prt1 in portsBase)
        {
            foreach (var prt2 in portsUpper)
            {
                // Z mesafesi (Kat geçiş toleransı)
                double distZ = Math.Abs(prt1.Position.Z - prt2.Position.Z);
                
                if (distZ < 500.0) // 50cm mesafe içindeyse (Döşeme kalınlığı vb)
                {
                    _kernel.TopologyGraph.Connect(prt1, prt2);
                    Log.Debug("Kolon Sürekliliği Sağlandı: {B} -> {U} (Kot Farkı: {Z}mm)", pBase.Id, pUpper.Id, distZ);
                }
            }
        }
    }

    private bool IsVertical(PipeEntity pipe)
    {
        var vec = (pipe.EndPoint - pipe.StartPoint);
        if (vec.Length() < 1.0) return false;
        var dir = vec.Normalize();
        return Math.Abs(dir.Z) > 0.95; // 90 dereceye yakın dikey hatlar
    }
}

public class LevelFileRegistration
{
    public string FilePath { get; set; } = string.Empty;
    public double Elevation { get; set; }
    public string LevelName { get; set; } = string.Empty;
}
