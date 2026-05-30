using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Entities;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Basınç Düşümü Haritası Servisi (PressureMapService)
   NEDEN: Hesaplanan PressureDrop değerlerine göre borulara renk gradyanı uygulayarak
          kritik hatları görsel olarak tespit etmek için (basınç ısı haritası / heat map).

   RENK SKALASI: 0 (düşük basınç kaybı) → Yeşil, max/2 → Sarı, max → Kırmızı

   KULLANIM:
   - `Apply(_database)` → borulara PressureDrop'a göre renk yaz, eski renkler saklanır
   - `Restore(_database)` → eski renkler geri yüklenir
   - `IsActive` → mod durumu
*/
public class PressureMapService
{
    private readonly Dictionary<Guid, uint> _savedColors = new();
    private bool _isActive;

    public bool IsActive => _isActive;

    // ── Aktif Et ─────────────────────────────────────────────────────────────────

    public int Apply(CadDatabase database)
    {
        if (_isActive) Restore(database); // yeniden uygula (veriler değişmiş olabilir)

        var pipes = database.GetAllEntities().OfType<PipeEntity>().ToList();
        if (pipes.Count == 0) return 0;

        // Sadece hesaplama yapılmış boruları dikkate al
        double maxDrop = pipes.Max(p => p.PressureDrop);
        if (maxDrop <= 0) maxDrop = 1; // sıfır bölme koruması

        _savedColors.Clear();
        int colored = 0;

        foreach (var pipe in pipes)
        {
            _savedColors[pipe.Id] = pipe.Color;
            double norm = Math.Clamp(pipe.PressureDrop / maxDrop, 0, 1);
            pipe.Color = PressureColor(norm);
            colored++;
        }

        _isActive = true;
        return colored;
    }

    // ── Devre Dışı Bırak ─────────────────────────────────────────────────────────

    public void Restore(CadDatabase database)
    {
        if (!_isActive) return;

        foreach (var pipe in database.GetAllEntities().OfType<PipeEntity>())
        {
            if (_savedColors.TryGetValue(pipe.Id, out uint saved))
                pipe.Color = saved;
        }

        _savedColors.Clear();
        _isActive = false;
    }

    // ── Özet Verisi ───────────────────────────────────────────────────────────────

    public class PressureMapSummary
    {
        public double MaxPressureDropM  { get; set; }
        public double AvgPressureDropM  { get; set; }
        public int    CriticalPipeCount { get; set; }  // > 80% maks
        public int    TotalPipeCount    { get; set; }
        public List<(string Label, double DropM)> TopPipes { get; set; } = [];
    }

    public PressureMapSummary GetSummary(CadDatabase database)
    {
        var pipes = database.GetAllEntities().OfType<PipeEntity>().ToList();
        if (pipes.Count == 0) return new PressureMapSummary();

        double max = pipes.Max(p => p.PressureDrop);
        double avg = pipes.Average(p => p.PressureDrop);
        int critical = pipes.Count(p => p.PressureDrop > max * 0.8);

        var top5 = pipes.OrderByDescending(p => p.PressureDrop)
            .Take(5)
            .Select(p => ($"DN{p.InnerDiameter:F0} L={((p.EndPoint - p.StartPoint).Length() / 1000):F1}m",
                          p.PressureDrop))
            .ToList();

        return new PressureMapSummary
        {
            MaxPressureDropM  = Math.Round(max, 4),
            AvgPressureDropM  = Math.Round(avg, 4),
            CriticalPipeCount = critical,
            TotalPipeCount    = pipes.Count,
            TopPipes          = top5
        };
    }

    // ── Renk Gradyanı ─────────────────────────────────────────────────────────────
    // norm=0 → Yeşil (#00FF44), norm=0.5 → Sarı (#FFEE00), norm=1 → Kırmızı (#FF2200)

    private static uint PressureColor(double norm)
    {
        byte r, g, b;
        if (norm < 0.5)
        {
            double t = norm * 2;
            r = (byte)(0   + t * 255);
            g = (byte)(210 + t * 34);
            b = (byte)0;
        }
        else
        {
            double t = (norm - 0.5) * 2;
            r = (byte)255;
            g = (byte)(244 - t * 244);
            b = (byte)0;
        }
        return (uint)((0xFF << 24) | (r << 16) | (g << 8) | b);
    }
}
