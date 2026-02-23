using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Otomatik Sonuç Yazma Servisi (AutoAnnotationService)
   NEDEN: FINE SANI standardında, hesaplama sonuçlarını (DN, akış hızı, debi, basınç kaybı)
          boru segmentleri üzerine otomatik olarak yazıp paftayı tamamlamak için.
   
   ÇALIŞMA MANTIĞI:
   1. Tüm boru segmentlerini tarar
   2. Her segmentin orta noktasına ve uygun ofsetine etiket yerleştirir
   3. Etiket formatı ve boyutu özelleştirilebilir
   4. Çakışma kontrolü ile etiketler üst üste binmez
*/
public class AutoAnnotationService
{
    private readonly CadDatabase _database;

    // Etiket ayarları
    public double TextHeight { get; set; } = 12.0;          // Metin yüksekliği (mm)
    public double OffsetDistance { get; set; } = 80.0;       // Borudan etiket mesafesi (mm)
    public uint DiameterColor { get; set; } = 0xFFFFFF00;    // Sarı - Çap etiketi rengi
    public uint FlowColor { get; set; } = 0xFF00CCFF;       // Açık mavi - Debi etiketi rengi
    public uint VelocityColor { get; set; } = 0xFF00FF88;   // Yeşil - Hız etiketi rengi
    public bool ShowDiameter { get; set; } = true;
    public bool ShowFlowRate { get; set; } = true;
    public bool ShowVelocity { get; set; } = true;
    public bool ShowPressureDrop { get; set; } = false;
    public bool ShowDirection { get; set; } = true;
    public string AnnotationLayer { get; set; } = "MEP_Annotations";

    public AutoAnnotationService(CadDatabase database)
    {
        _database = database;
    }

    /*
       NE: Tüm Borulara Etiket Yaz (AnnotateAllPipes)
       NEDEN: Tek tıkla tüm boru segmentlerine hesaplanmış değerleri yazdırmak.
    */
    public List<CadEntity> AnnotateAllPipes()
    {
        var annotations = new List<CadEntity>();
        var pipes = _database.GetAllEntities().OfType<PipeEntity>().ToList();
        var existingPositions = new List<Vector3D>();

        foreach (var pipe in pipes)
        {
            var pipeAnnotations = AnnotateSinglePipe(pipe, existingPositions);
            annotations.AddRange(pipeAnnotations);
        }

        return annotations;
    }

    /*
       NE: Tek Boruya Etiket Yaz (AnnotateSinglePipe)
       NEDEN: Belirli bir boru segmentine DN, debi, hız ve basınç kaybı etiketlerini yerleştirmek.
    */
    public List<CadEntity> AnnotateSinglePipe(PipeEntity pipe, List<Vector3D>? existingPositions = null)
    {
        var annotations = new List<CadEntity>();
        existingPositions ??= new List<Vector3D>();

        // Boru orta noktası ve açısı
        var midPoint = new Vector3D(
            (pipe.StartPoint.X + pipe.EndPoint.X) / 2.0,
            (pipe.StartPoint.Y + pipe.EndPoint.Y) / 2.0,
            0);

        double angle = Math.Atan2(
            pipe.EndPoint.Y - pipe.StartPoint.Y,
            pipe.EndPoint.X - pipe.StartPoint.X);

        // Boruya dik ofset vektörü (sola veya yukarı)
        var perpendicular = new Vector3D(-Math.Sin(angle), Math.Cos(angle), 0);
        double angleDeg = angle * 180.0 / Math.PI;
        // Metin okunabilirliği: 90-270 arası ise ters çevir
        if (angleDeg > 90 || angleDeg < -90) angleDeg += 180;

        int lineOffset = 0;

        // 1. ÇAP ETİKETİ
        if (ShowDiameter && pipe.InnerDiameter > 0)
        {
            var pos = midPoint + perpendicular * (OffsetDistance + lineOffset * (TextHeight + 4));
            if (!IsOverlapping(pos, existingPositions))
            {
                var label = new TextEntity(
                    $"Ø{pipe.InnerDiameter:F0}",
                    pos, TextHeight, angleDeg);
                label.Color = DiameterColor;
                label.Layer = AnnotationLayer;
                annotations.Add(label);
                existingPositions.Add(pos);
            }
            lineOffset++;
        }

        // 2. DEBİ ETİKETİ
        if (ShowFlowRate && pipe.FlowRate > 0)
        {
            var pos = midPoint + perpendicular * (OffsetDistance + lineOffset * (TextHeight + 4));
            if (!IsOverlapping(pos, existingPositions))
            {
                double flowLps = pipe.FlowRate / 3.6;
                var label = new TextEntity(
                    $"Q={flowLps:F2} L/s",
                    pos, TextHeight * 0.85, angleDeg);
                label.Color = FlowColor;
                label.Layer = AnnotationLayer;
                annotations.Add(label);
                existingPositions.Add(pos);
            }
            lineOffset++;
        }

        // 3. HIZ ETİKETİ
        if (ShowVelocity && pipe.Velocity > 0)
        {
            var pos = midPoint + perpendicular * (OffsetDistance + lineOffset * (TextHeight + 4));
            if (!IsOverlapping(pos, existingPositions))
            {
                string velocityWarning = pipe.Velocity > 1.5 ? " ⚠" : "";
                var label = new TextEntity(
                    $"V={pipe.Velocity:F2} m/s{velocityWarning}",
                    pos, TextHeight * 0.85, angleDeg);
                label.Color = pipe.Velocity > 1.5 ? 0xFFFF4444 : VelocityColor;
                label.Layer = AnnotationLayer;
                annotations.Add(label);
                existingPositions.Add(pos);
            }
            lineOffset++;
        }

        // 4. BASINÇ KAYBI ETİKETİ
        if (ShowPressureDrop && pipe.PressureDrop > 0)
        {
            var pos = midPoint + perpendicular * (OffsetDistance + lineOffset * (TextHeight + 4));
            if (!IsOverlapping(pos, existingPositions))
            {
                var label = new TextEntity(
                    $"ΔP={pipe.PressureDrop:F3} mSS",
                    pos, TextHeight * 0.75, angleDeg);
                label.Color = 0xFFFF8800;
                label.Layer = AnnotationLayer;
                annotations.Add(label);
                existingPositions.Add(pos);
            }
            lineOffset++;
        }

        // 5. AKIŞ YÖN OKU
        if (ShowDirection && pipe.FlowRate > 0)
        {
            var dirVec = new Vector3D(
                pipe.EndPoint.X - pipe.StartPoint.X,
                pipe.EndPoint.Y - pipe.StartPoint.Y, 0);
            double len = Math.Sqrt(dirVec.X * dirVec.X + dirVec.Y * dirVec.Y);
            if (len > 0)
            {
                var norm = new Vector3D(dirVec.X / len, dirVec.Y / len, 0);
                var arrowBase = midPoint - norm * 30;
                var arrowTip = midPoint + norm * 30;
                var arrowLine = new LineEntity(arrowBase, arrowTip);
                arrowLine.Color = 0xFF00FFFF;
                arrowLine.Layer = AnnotationLayer;
                annotations.Add(arrowLine);

                // Ok uçları
                var leftWing = arrowTip - norm * 15 + perpendicular * 8;
                var rightWing = arrowTip - norm * 15 - perpendicular * 8;
                annotations.Add(new LineEntity(arrowTip, leftWing) { Color = 0xFF00FFFF, Layer = AnnotationLayer });
                annotations.Add(new LineEntity(arrowTip, rightWing) { Color = 0xFF00FFFF, Layer = AnnotationLayer });
            }
        }

        return annotations;
    }

    /*
       NE: Sistem Tipi Bazlı Etiketleme
       NEDEN: Sadece belirli bir sistem tipindeki (Soğuk Su / Sıcak Su / Pis Su) borulara etiket yazmak.
    */
    public List<CadEntity> AnnotateBySystem(MechanicalSystemType systemType)
    {
        var annotations = new List<CadEntity>();
        var pipes = _database.GetAllEntities().OfType<PipeEntity>()
            .Where(p => p.SystemType == systemType).ToList();
        var existingPositions = new List<Vector3D>();

        foreach (var pipe in pipes)
        {
            annotations.AddRange(AnnotateSinglePipe(pipe, existingPositions));
        }
        return annotations;
    }

    /*
       NE: Mevcut Etiketleri Temizle
       NEDEN: Yeniden hesaplama sonrası eski etiketleri silip güncel değerlerle değiştirmek.
    */
    public int ClearAnnotations()
    {
        var annotations = _database.GetAllEntities()
            .Where(e => e.Layer == AnnotationLayer).ToList();
        int count = annotations.Count;
        foreach (var a in annotations) _database.RemoveEntity(a.Id);
        return count;
    }

    // Çakışma kontrolü (basit mesafe)
    private bool IsOverlapping(Vector3D pos, List<Vector3D> existing)
    {
        double minDist = TextHeight * 3;
        return existing.Any(e => e.DistanceTo(pos) < minDist);
    }
}
