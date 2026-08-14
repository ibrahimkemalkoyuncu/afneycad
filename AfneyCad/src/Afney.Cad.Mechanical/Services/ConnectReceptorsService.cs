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
   NEDEN: Veritabanındaki tüm (veya seçili) armatürleri otomatik algılayıp,
          her birinin portuna uygun sistem tipindeki en yakın boruya akıllıca bağlar.

   NASIL (TS 1258 / DIN 1988):
   1. Her armatürün portları (ColdWater, HotWater, Drainage) listelenir.
   2. Her port için veritabanındaki borular sistem tipi filtresiyle sorgulanır.
   3. En yakın boru seçilir, AutoBranching ile T-parçası ve branşman oluşturulur.
   4. Bölünen "DB'de orijinal" borular ToRemove'a eklenir.
      Henüz DB'ye eklenmemiş "yeni oluşmuş" borular bölündüğünde NewEntities'den çıkarılır.
      Böylece MainWindow'a dönen sonuç tutarlı olur: sadece gerçekten DB'de olan
      entity'ler ToRemove'da, sadece gerçekten yeni olanlar NewEntities'de.
*/
public class ConnectReceptorsService
{
    private readonly CadDatabase _database;
    private readonly MechanicalKernel _kernel;
    private readonly AutoBranchingService _branching;

    public double MaxBranchDistanceMM { get; set; } = 3000.0;

    public ConnectReceptorsService(CadDatabase database, MechanicalKernel kernel)
    {
        _database = database;
        _kernel   = kernel;
        _branching = new AutoBranchingService(database, kernel);
    }

    public class ConnectResult
    {
        public List<CadEntity> NewEntities  { get; set; } = [];
        public List<CadEntity> ToRemove     { get; set; } = [];
        public int ConnectedCount           { get; set; }
        public int SkippedCount             { get; set; }
        public List<string> SkipReasons     { get; set; } = [];
    }

    public ConnectResult ConnectAll()
    {
        var fixtures = _database.GetAllEntities()
            .OfType<SanitaryFixtureEntity>()
            .ToList();
        return ConnectFixtures(fixtures);
    }

    public ConnectResult ConnectSelected(IEnumerable<SanitaryFixtureEntity> fixtures)
        => ConnectFixtures(fixtures.ToList());

    // ── Core ────────────────────────────────────────────────────────────────────

    private ConnectResult ConnectFixtures(List<SanitaryFixtureEntity> fixtures)
    {
        var result = new ConnectResult();
        if (fixtures.Count == 0) return result;

        var allPipes = _database.GetAllEntities().OfType<PipeEntity>().ToList();
        if (allPipes.Count == 0)
        {
            result.SkipReasons.Add("Veritabanında boru bulunamadı. Önce tesisat çizin.");
            return result;
        }

        // Hangi boru ID'lerinin gerçekten DB'de olduğunu takip et.
        // Bölünen yeni borular bu sette olmaz → ToRemove'a gitmez, NewEntities'den silinir.
        var dbEntityIds = new HashSet<Guid>(allPipes.Select(p => p.Id));

        // Performans: Her fixture×port için TÜM boruları lineer taramak yerine, borular
        // sistem tipine göre önceden gruplanır — arama sadece ilgili sistemin borularında yapılır.
        // allPipes'taki tüm ekleme/çıkarma (bölme) işlemleri bu grup listelerine de yansıtılır,
        // böylece davranış/sonuç birebir aynı kalır — sadece taranan küme daralır.
        var pipesBySystem = allPipes
            .GroupBy(p => p.SystemType)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Yeni oluşturulan tüm entity'leri ID → entity eşlemesiyle tut.
        // Bir entity hem oluşturulup hem sonradan bölünürse dict'ten silinerek temizlenir.
        var newEntitiesMap = new Dictionary<Guid, CadEntity>();

        foreach (var fixture in fixtures)
        {
            foreach (var port in fixture.GetPorts())
            {
                var targetSystem = PortNameToSystemType(port.Name);
                if (targetSystem == MechanicalSystemType.Undefined)
                {
                    result.SkipReasons.Add($"{fixture.FixtureType}/{port.Name}: sistem tipi belirlenemedi.");
                    result.SkippedCount++;
                    continue;
                }

                var candidate = FindNearestCompatiblePipe(port.Position, targetSystem, pipesBySystem);
                if (candidate == null)
                {
                    result.SkipReasons.Add(
                        $"{fixture.FixtureType}/{port.Name}: {MaxBranchDistanceMM / 1000:F1}m yarıçapında {targetSystem} hattı bulunamadı.");
                    result.SkippedCount++;
                    continue;
                }

                try
                {
                    var branchResult = _branching.CreateBranchConnectionPublic(port.Position, candidate, port);
                    if (!branchResult.NewEntities.Any())
                    {
                        result.SkipReasons.Add($"{fixture.FixtureType}/{port.Name}: branşman noktası boru üzerinde değil.");
                        result.SkippedCount++;
                        continue;
                    }

                    // 1. Candidate artık bölündü → working listeden çıkar
                    allPipes.Remove(candidate);
                    if (pipesBySystem.TryGetValue(candidate.SystemType, out var candidateSystemList))
                        candidateSystemList.Remove(candidate);

                    // 2. Candidate DB'deydi → ToRemove; yoksa (daha önce yeni oluşmuştu) → NewEntities'den sil
                    if (dbEntityIds.Contains(candidate.Id))
                        result.ToRemove.Add(candidate);
                    else
                        newEntitiesMap.Remove(candidate.Id);

                    // 3. Tüm yeni entity'leri kaydet
                    foreach (var ent in branchResult.NewEntities)
                        newEntitiesMap[ent.Id] = ent;

                    // 4. Yeni boru segmentlerini (aynı yönde olanlar) working listesine ekle
                    var newSegments = branchResult.NewEntities
                        .OfType<PipeEntity>()
                        .Where(p => IsSameDirection(p, candidate))
                        .ToList();
                    allPipes.AddRange(newSegments);
                    foreach (var seg in newSegments)
                    {
                        if (!pipesBySystem.TryGetValue(seg.SystemType, out var segList))
                        {
                            segList = new List<PipeEntity>();
                            pipesBySystem[seg.SystemType] = segList;
                        }
                        segList.Add(seg);
                    }

                    result.ConnectedCount++;
                    Serilog.Log.Information(
                        "[ConnectReceptors] {F}/{P} → {S} OK ({N} yeni parça)",
                        fixture.FixtureType, port.Name, targetSystem, branchResult.NewEntities.Count);
                }
                catch (Exception ex)
                {
                    result.SkipReasons.Add($"{fixture.FixtureType}/{port.Name}: {ex.Message}");
                    result.SkippedCount++;
                }
            }
        }

        result.NewEntities = [.. newEntitiesMap.Values];

        Serilog.Log.Information(
            "[ConnectReceptors] Tamamlandı: {Ok} bağlantı, {Skip} atlandı, {New} yeni entity, {Rem} kaldırılacak.",
            result.ConnectedCount, result.SkippedCount, result.NewEntities.Count, result.ToRemove.Count);

        return result;
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private PipeEntity? FindNearestCompatiblePipe(
        Vector3D portPos, MechanicalSystemType targetSystem, Dictionary<MechanicalSystemType, List<PipeEntity>> pipesBySystem)
    {
        PipeEntity? best = null;
        double minDist = double.MaxValue;
        if (!pipesBySystem.TryGetValue(targetSystem, out var pipes)) return null;

        foreach (var pipe in pipes)
        {
            double dist = DistanceToSegment(portPos, pipe.StartPoint, pipe.EndPoint);
            if (dist < minDist && dist <= MaxBranchDistanceMM) { minDist = dist; best = pipe; }
        }
        return best;
    }

    private static double DistanceToSegment(Vector3D p, Vector3D s, Vector3D e)
    {
        var v = e - s;
        var w = p - s;
        double c1 = w.Dot(v);
        double c2 = v.Dot(v);
        if (c2 <= 0) return p.DistanceTo(s);
        double b = c1 / c2;
        if (b < 0) return p.DistanceTo(s);
        if (b > 1) return p.DistanceTo(e);
        return p.DistanceTo(s + v * b);
    }

    private static MechanicalSystemType PortNameToSystemType(string portName) => portName switch
    {
        "ColdWater"             => MechanicalSystemType.DomesticColdWater,
        "HotWater"              => MechanicalSystemType.DomesticHotWater,
        "Drainage" or "Waste"   => MechanicalSystemType.WasteWater,
        _                       => MechanicalSystemType.Undefined
    };

    private static bool IsSameDirection(PipeEntity p, PipeEntity reference)
    {
        var d1 = (p.EndPoint - p.StartPoint).Normalize();
        var d2 = (reference.EndPoint - reference.StartPoint).Normalize();
        return Math.Abs(Math.Abs(d1.Dot(d2)) - 1.0) < 0.01;
    }
}
