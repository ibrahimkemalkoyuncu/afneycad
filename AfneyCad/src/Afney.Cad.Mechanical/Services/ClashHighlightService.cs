using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Models;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Gerçek Zamanlı Çakışma Vurgu Servisi (ClashHighlightService)
   NEDEN: ClashDetectionService bulgularını viewport'ta kırmızı/turuncu renk override
          ile görsel olarak işaretlemek için (PressureMapService ile aynı desen).

   RENK KODLARI:
   - Critical çakışma: #FF2200 (Kırmızı) — boru-boru kesişme, kolon çarpması
   - Warning çakışma:  #FF8800 (Turuncu) — yakın geçiş, duvar çarpması

   KULLANIM:
   - Apply(database, obstacles) → çakışan entity'lere renk uygula, eski renkler sakla
   - Restore(database)          → orijinal renkler geri yüklenir
   - IsActive                   → mod durumu
*/
public class ClashHighlightService
{
    private readonly Dictionary<Guid, uint> _savedColors = new();
    private int _criticalCount;
    private int _warningCount;
    private bool _isActive;

    public bool IsActive => _isActive;

    // ── Aktif Et ─────────────────────────────────────────────────────────────────

    public ClashHighlightSummary Apply(CadDatabase database, List<ArchitecturalObstacle>? obstacles = null)
    {
        if (_isActive) Restore(database);

        var allEntities = database.GetAllEntities().ToList();
        var mechEntities = allEntities.OfType<MechanicalEntity>().ToList();

        var svc = new ClashDetectionService(obstacles ?? []);
        var clashes = svc.DetectClashes(mechEntities);

        _savedColors.Clear();
        _criticalCount = 0;
        _warningCount = 0;

        // Çakışan entity ID'lerini topla (Critical → kırmızı, Warning → turuncu)
        var criticalIds = new HashSet<Guid>();
        var warningIds  = new HashSet<Guid>();

        foreach (var clash in clashes)
        {
            if (clash.Severity == ClashSeverity.Critical)
            {
                criticalIds.Add(clash.EntityA_Id);
                if (clash.EntityB_Id.HasValue) criticalIds.Add(clash.EntityB_Id.Value);
            }
            else
            {
                warningIds.Add(clash.EntityA_Id);
                if (clash.EntityB_Id.HasValue) warningIds.Add(clash.EntityB_Id.Value);
            }
        }

        // Critical öncelik — aynı entity hem warning hem critical ise critical kazanır
        warningIds.ExceptWith(criticalIds);

        foreach (var entity in allEntities)
        {
            if (entity is not MechanicalEntity me) continue;

            if (criticalIds.Contains(me.Id))
            {
                _savedColors[me.Id] = me.Color;
                me.Color = 0xFFFF2200; // Kırmızı
                _criticalCount++;
            }
            else if (warningIds.Contains(me.Id))
            {
                _savedColors[me.Id] = me.Color;
                me.Color = 0xFFFF8800; // Turuncu
                _warningCount++;
            }
        }

        _isActive = true;

        return new ClashHighlightSummary
        {
            TotalClashes   = clashes.Count,
            CriticalCount  = _criticalCount,
            WarningCount   = _warningCount,
            AffectedEntities = _savedColors.Count,
            Clashes        = clashes
        };
    }

    // ── Devre Dışı Bırak ─────────────────────────────────────────────────────────

    public void Restore(CadDatabase database)
    {
        if (!_isActive) return;

        foreach (var entity in database.GetAllEntities().OfType<MechanicalEntity>())
        {
            if (_savedColors.TryGetValue(entity.Id, out uint saved))
                entity.Color = saved;
        }

        _savedColors.Clear();
        _criticalCount = 0;
        _warningCount  = 0;
        _isActive = false;
    }

    // ── Özet ─────────────────────────────────────────────────────────────────────

    public class ClashHighlightSummary
    {
        public int TotalClashes      { get; set; }
        public int CriticalCount     { get; set; }
        public int WarningCount      { get; set; }
        public int AffectedEntities  { get; set; }
        public List<ClashResult> Clashes { get; set; } = [];
    }
}
