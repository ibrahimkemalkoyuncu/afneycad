using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Engine;
using Afney.Cad.Mechanical.Enums;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Armatür Bağlantı Servisi (ConnectReceptorsService)
   NEDEN: FineSANI'deki "Connect Receptors" özelliğini karşılamak için.
          Veritabanındaki tüm (veya seçili) armatürleri otomatik algılayıp, her birinin
          portuna uygun sistem tipindeki en yakın boruya akıllıca bağlar.

   NASIL (Mühendislik Detayı — TS 1258 / DIN 1988):
   1. Her armatürün portları (ColdWater, HotWater, Drainage) listelenir.
   2. Her port için veritabanındaki borular sistem tipi filtresiyle sorgulanır.
   3. En yakın boru bulunarak AutoBranchingService üzerinden bağlantı kurulur.
   4. Sonuçlar Transaction-safe olarak döndürülür (Add + Remove listesi).
*/
public class ConnectReceptorsService
{
    private readonly CadDatabase _database;
    private readonly MechanicalKernel _kernel;
    private readonly AutoBranchingService _branching;

    // Maksimum branşman mesafesi (mm): Bu mesafeyi aşan boru-armatür çiftleri bağlanmaz.
    // TS 1258 §8.2: Yatay branşman maksimum 3m = 3000mm
    public double MaxBranchDistanceMM { get; set; } = 3000.0;

    public ConnectReceptorsService(CadDatabase database, MechanicalKernel kernel)
    {
        _database = database;
        _kernel   = kernel;
        _branching = new AutoBranchingService(database, kernel);
    }

    /*
       NE: Bağlantı Sonucu
       NEDEN: Hangi armatürlerin bağlandığını, hangi yeni nesnelerin oluştuğunu
              ve hangi nesnelerin kaldırılması gerektiğini raporlamak için.
    */
    public class ConnectResult
    {
        public List<CadEntity> NewEntities   { get; set; } = new();
        public List<CadEntity> ToRemove      { get; set; } = new();  // Bölünen eski borular
        public int ConnectedCount            { get; set; } = 0;
        public int SkippedCount              { get; set; } = 0;
        public List<string> SkipReasons      { get; set; } = new();
    }

    /*
       NE: Tüm Armatürleri Bağla (ConnectAll)
       NEDEN: Tek komutla veritabanındaki tüm bağlantısız armatürleri uygun borulara bağlamak için.
       
       ALGORITMA:
       1. DB'deki tüm SanitaryFixtureEntity nesneleri alınır.
       2. Her armatür için her port kontrol edilir (ColdWater, HotWater, Drainage).
       3. Port'un sistem tipine uyan borular filtrelenir + mesafe sınırı uygulanır.
       4. En yakın boru seçilir, AutoBranching ile bağlantı kurulur.
    */
    public ConnectResult ConnectAll()
    {
        var fixtures = _database.GetAllEntities()
            .OfType<SanitaryFixtureEntity>()
            .ToList();

        return ConnectFixtures(fixtures);
    }

    /*
       NE: Seçili Armatürleri Bağla (ConnectSelected)
       NEDEN: Kullanıcının seçtiği armatürleri bağlayarak bölgesel kontrol sağlamak için.
    */
    public ConnectResult ConnectSelected(IEnumerable<SanitaryFixtureEntity> fixtures)
    {
        return ConnectFixtures(fixtures.ToList());
    }

    // ── Core Implementation ──────────────────────────────────────────────────────

    private ConnectResult ConnectFixtures(List<SanitaryFixtureEntity> fixtures)
    {
        var result = new ConnectResult();
        if (!fixtures.Any()) return result;

        // Borular listesini bir kez al (performans için)
        var allPipes = _database.GetAllEntities()
            .OfType<PipeEntity>()
            .ToList();

        if (!allPipes.Any())
        {
            result.SkipReasons.Add("Veritabanında boru bulunamadı. Önce tesisat çizin.");
            return result;
        }

        // Her armatür için bağlantı kur
        foreach (var fixture in fixtures)
        {
            var ports = fixture.GetPorts();
            foreach (var port in ports)
            {
                // 1. Port'un sistem tipini belirle
                var targetSystem = PortNameToSystemType(port.Name);
                if (targetSystem == MechanicalSystemType.Undefined)
                {
                    result.SkipReasons.Add($"{fixture.FixtureType} / Port '{port.Name}': Sistem tipi belirlenemedi.");
                    result.SkippedCount++;
                    continue;
                }

                // 2. Uygun boruları filtrele (sistem tipi + mesafe)
                var candidate = FindNearestCompatiblePipe(port.Position, targetSystem, allPipes);
                if (candidate == null)
                {
                    result.SkipReasons.Add($"{fixture.FixtureType} / Port '{port.Name}': {MaxBranchDistanceMM/1000:F1}m yarıçapında {targetSystem} hattı bulunamadı.");
                    result.SkippedCount++;
                    continue;
                }

                // 3. Branşman bağlantısını kur (AutoBranchingService delegate)
                try
                {
                    var branchResult = _branching.CreateBranchConnectionPublic(port.Position, candidate, port);

                    if (branchResult.NewEntities.Any())
                    {
                        result.NewEntities.AddRange(branchResult.NewEntities);
                        result.ToRemove.AddRange(branchResult.RemovedEntities);
                        result.ConnectedCount++;

                        // Bölünen boruyu aktif listeden kaldır
                        // (Bir sonraki portun aynı boruyu tekrar hedef almaması için)
                        allPipes.Remove(candidate);

                        // Yeni oluşan paralel boru segmentlerini listeye ekle
                        var newSegments = branchResult.NewEntities.OfType<PipeEntity>()
                            .Where(p => IsSameDirection(p, candidate))
                            .ToList();
                        allPipes.AddRange(newSegments);

                        Serilog.Log.Information(
                            "[ConnectReceptors] {Fixture} / {Port} → {System} BranchOK ({Count} yeni parça)",
                            fixture.FixtureType, port.Name, targetSystem, branchResult.NewEntities.Count);
                    }
                    else
                    {
                        result.SkipReasons.Add($"{fixture.FixtureType} / Port '{port.Name}': Branşman noktası boru üzerinde değil.");
                        result.SkippedCount++;
                    }
                }
                catch (Exception ex)
                {
                    result.SkipReasons.Add($"{fixture.FixtureType} / Port '{port.Name}': Hata — {ex.Message}");
                    result.SkippedCount++;
                }
            }
        }

        Serilog.Log.Information(
            "[ConnectReceptors] Tamamlandı: {Ok} bağlantı başarılı, {Skip} atlandı.",
            result.ConnectedCount, result.SkippedCount);

        return result;
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    /*
       NE: En Yakın Uyumlu Boruyu Bul (FindNearestCompatiblePipe)
       NEDEN: Port konumuna en yakın ve sistem tipine uygun boruyu MaxBranchDistance içinden seçmek için.
    */
    private PipeEntity? FindNearestCompatiblePipe(
        Vector3D portPos,
        MechanicalSystemType targetSystem,
        List<PipeEntity> pipes)
    {
        PipeEntity? best = null;
        double minDist = double.MaxValue;

        foreach (var pipe in pipes)
        {
            if (pipe.SystemType != targetSystem) continue;

            double dist = DistanceToSegment(portPos, pipe.StartPoint, pipe.EndPoint);
            if (dist < minDist && dist <= MaxBranchDistanceMM)
            {
                minDist = dist;
                best = pipe;
            }
        }

        return best;
    }

    /*
       NE: Nokta - Segment Mesafesi
    */
    private double DistanceToSegment(Vector3D p, Vector3D s, Vector3D e)
    {
        var v = e - s;
        var w = p - s;
        double c1 = w.Dot(v);
        double c2 = v.Dot(v);
        if (c2 <= 0) return p.DistanceTo(s);
        double b = c1 / c2;
        if (b < 0) return p.DistanceTo(s);
        if (b > 1) return p.DistanceTo(e);
        return p.DistanceTo(s + (v * b));
    }

    /*
       NE: Port Adından Sistem Tipi Belirle
    */
    private MechanicalSystemType PortNameToSystemType(string portName) => portName switch
    {
        "ColdWater"                        => MechanicalSystemType.DomesticColdWater,
        "HotWater"                         => MechanicalSystemType.DomesticHotWater,
        "Drainage"  or "Waste"             => MechanicalSystemType.WasteWater,
        _                                  => MechanicalSystemType.Undefined
    };

    /*
       NE: Boru Yönlerinin Eşitliğini Kontrol Et
    */
    private bool IsSameDirection(PipeEntity p, PipeEntity reference)
    {
        var d1 = (p.EndPoint         - p.StartPoint).Normalize();
        var d2 = (reference.EndPoint - reference.StartPoint).Normalize();
        return Math.Abs(Math.Abs(d1.Dot(d2)) - 1.0) < 0.01;
    }
}
