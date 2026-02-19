using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Models;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Mahal Teknik Tablo Servisi (MahalScheduleService)
   NEDEN: Seçilen mahalin mühendislik verilerini (Vitrifiye listesi, FU toplamı, Alan) 
          Antetli bir tablo olarak CAD ekranına basmak için.
   
   MÜHENDİSLİK DETAYI (Mete):
   - TABLE ARCHITECTURE: Header, Column ve Row yapısıyla profesyonel görünüm.
   - DYNAMIC CONTENT: Armatür sayıları arttığında tablo boyutu otomatik genişler.
*/
public class MahalScheduleService
{
    private const double RowHeight = 250.0;
    private const double ColumnWidth = 2000.0;

    /*
       NE: Teknik Tablo Oluştur (GenerateRoomTable)
       NEDEN: Mahal nesnesine bağlı tüm mühendislik verilerini (alan, cihaz listesi, toplam yük) profesyonel bir rapor tablosu şeklinde çizerek CAD sahnesine eklemek için.
    */
    public List<Afney.Cad.Domain.Abstractions.CadEntity> GenerateRoomTable(MahalEntity mahal, List<SanitaryFixtureEntity> fixtures, Vector3D origin)
    {
        var entities = new List<Afney.Cad.Domain.Abstractions.CadEntity>();
        double currentY = 0;

        // 1. Tablo Başlığı
        entities.Add(new TextEntity($"MAHAL TEKNİK RAPORU: {mahal.RoomName}", origin + new Vector3D(0, currentY, 0), 200) { Color = 0xFFFF00FF });
        currentY -= RowHeight * 1.5;

        // 2. Genel Bilgiler
        entities.Add(new TextEntity($"Alan: {mahal.Area:F2} m²", origin + new Vector3D(0, currentY, 0), 150));
        currentY -= RowHeight;
        entities.Add(new TextEntity($"Tipi: {mahal.RoomType}", origin + new Vector3D(0, currentY, 0), 120));
        currentY -= RowHeight * 1.2;

        // 3. Vitrifiye Başlıkları
        entities.Add(new TextEntity("Cihaz Tipi", origin + new Vector3D(0, currentY, 0), 120) { Color = 0xFF00FFFF });
        entities.Add(new TextEntity("Adet", origin + new Vector3D(ColumnWidth * 0.4, currentY, 0), 120) { Color = 0xFF00FFFF });
        entities.Add(new TextEntity("Birim FU", origin + new Vector3D(ColumnWidth * 0.7, currentY, 0), 120) { Color = 0xFF00FFFF });
        currentY -= RowHeight;

        // Çizgi (Header Underline)
        entities.Add(new LineEntity(origin + new Vector3D(0, currentY + 50, 0), origin + new Vector3D(ColumnWidth, currentY + 50, 0)) { Color = 0xFF888888 });

        // 4. Vitrifiye Listesi (Grouping)
        var groups = fixtures.GroupBy(f => f.FixtureType);
        foreach (var group in groups)
        {
            entities.Add(new TextEntity(group.Key, origin + new Vector3D(0, currentY, 0), 110));
            entities.Add(new TextEntity(group.Count().ToString(), origin + new Vector3D(ColumnWidth * 0.4, currentY, 0), 110));
            entities.Add(new TextEntity(group.First().LoadUnits.ToString("F1"), origin + new Vector3D(ColumnWidth * 0.7, currentY, 0), 110));
            currentY -= RowHeight;
        }

        // 5. Toplam
        currentY -= RowHeight * 0.5;
        double totalFU = fixtures.Sum(f => f.LoadUnits);
        entities.Add(new TextEntity($"TOPLAM YÜK (∑FU): {totalFU:F2}", origin + new Vector3D(0, currentY, 0), 140) { Color = 0xFF00FF00 });

        return entities;
    }
}
