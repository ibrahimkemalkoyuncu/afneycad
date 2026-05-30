using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Çizim-Hesap Senkronizasyon Servisi (DrawingSyncService)
   NEDEN: CalculationTableWindow'da bir borunun DN'si değiştirildiğinde,
          çizimdeki ilgili Ø etiketlerini (TextEntity) otomatik güncellemek için.

   GERI BESLEME DÖNGÜSÜ (OtoNET "Çizimi Güncelle" karşılığı):
   - `PipeDN_Changed(segmentId, newDN)` gelince → boruyu bul → midpoint hesapla
   - Yakın TextEntity'lerdeki "Ø..." metni yeni DN ile güncelle
   - Eşleşen annotation yoksa yeni TextEntity oluştur
*/
public class DrawingSyncService
{
    private readonly CadDatabase _database;
    private const double SearchRadius = 300.0; // mm — annotation arama yarıçapı
    private const string AnnotationLayer = "MEP_Annotations";

    public DrawingSyncService(CadDatabase database) { _database = database; }

    public class SyncResult
    {
        public int UpdatedLabels  { get; set; }
        public int CreatedLabels  { get; set; }
        public List<string> Log   { get; set; } = [];
    }

    // ── Tek Boru Etiketi Güncelle ─────────────────────────────────────────────────

    public SyncResult SyncPipeLabel(string segmentIdOrTag, double newDN)
    {
        var result = new SyncResult();

        // 1. Boruyu bul — segment tag'i (KH-001 vb.) veya GUID üzerinden
        PipeEntity? pipe = FindPipe(segmentIdOrTag);
        if (pipe == null)
        {
            result.Log.Add($"Boru bulunamadı: {segmentIdOrTag}");
            return result;
        }

        // 2. Eski ve yeni DN
        double oldDN = pipe.InnerDiameter;
        pipe.InnerDiameter = newDN;
        pipe.ApplySystemColor();

        // 3. Borunun midpoint'ini hesapla
        var mid = new Vector3D(
            (pipe.StartPoint.X + pipe.EndPoint.X) / 2.0,
            (pipe.StartPoint.Y + pipe.EndPoint.Y) / 2.0, 0);

        // 4. Yakın TextEntity'leri bul ve güncelle
        string oldPattern = $"Ø{oldDN:F0}";
        string newText    = $"Ø{newDN:F0}";

        var annotations = _database.GetAllEntities()
            .OfType<TextEntity>()
            .Where(t => t.Layer == AnnotationLayer
                     && t.Position.DistanceTo(mid) <= SearchRadius
                     && t.Text.StartsWith("Ø", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (annotations.Count > 0)
        {
            foreach (var ann in annotations)
            {
                ann.Text = ann.Text.Replace(oldPattern, newText);
                result.UpdatedLabels++;
                result.Log.Add($"Etiket güncellendi: '{oldPattern}' → '{newText}' @ ({mid.X:F0},{mid.Y:F0})");
            }
        }
        else
        {
            // Etiket yoksa yeni oluştur
            double angle = Math.Atan2(
                pipe.EndPoint.Y - pipe.StartPoint.Y,
                pipe.EndPoint.X - pipe.StartPoint.X) * 180.0 / Math.PI;

            var offsetPos = new Vector3D(mid.X, mid.Y + 80, 0);
            var label = new TextEntity(newText, offsetPos, 12.0, angle)
            {
                Color = 0xFFFFFF00,
                Layer = AnnotationLayer
            };
            _database.AddEntity(label);
            result.CreatedLabels++;
            result.Log.Add($"Yeni etiket oluşturuldu: '{newText}' @ ({mid.X:F0},{mid.Y:F0})");
        }

        Serilog.Log.Information(
            "[DrawingSync] {Id}: DN {Old} → {New} | {U} güncellendi, {C} oluşturuldu",
            segmentIdOrTag, oldDN, newDN, result.UpdatedLabels, result.CreatedLabels);

        return result;
    }

    // ── Tüm Borular ───────────────────────────────────────────────────────────────

    public SyncResult SyncAll()
    {
        var result = new SyncResult();
        var pipes = _database.GetAllEntities().OfType<PipeEntity>().ToList();

        foreach (var pipe in pipes)
        {
            var r = SyncPipeLabel(pipe.Id.ToString(), pipe.InnerDiameter);
            result.UpdatedLabels  += r.UpdatedLabels;
            result.CreatedLabels  += r.CreatedLabels;
            result.Log.AddRange(r.Log);
        }

        return result;
    }

    // ── Helper ────────────────────────────────────────────────────────────────────

    private PipeEntity? FindPipe(string idOrTag)
    {
        // Önce GUID dene
        if (Guid.TryParse(idOrTag, out Guid guid))
            return _database.GetAllEntities().OfType<PipeEntity>()
                .FirstOrDefault(p => p.Id == guid);

        // Segment tag araması (KH-001 vb.) — Tag property yoksa Name/Layer'da ara
        return _database.GetAllEntities().OfType<PipeEntity>()
            .FirstOrDefault(p => p.Layer?.Contains(idOrTag, StringComparison.OrdinalIgnoreCase) == true);
    }
}
