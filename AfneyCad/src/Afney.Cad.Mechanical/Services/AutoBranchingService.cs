using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;
using Afney.Cad.Mechanical.Engine;

namespace Afney.Cad.Mechanical.Services;

/// <summary>
/// Mekanik branşman ve bağlantı servisi.
/// Cihazların borulara bağlanması ve kolon bağlantılarını otomatikleştirir.
/// </summary>
public class AutoBranchingService
{
    private readonly CadDatabase _database;
    private readonly AutoFittingSelector _fittingSelector;
    private readonly MechanicalKernel _kernel;

    /*
       NE: AutoBranchingService Yapıcı Metodu
       NEDEN: Veritabanı ve Mekanik Çekirdek referanslarını alarak otomatik branşman ve bağlantı hesaplarını yapmaya hazır hale gelir.
    */
    public AutoBranchingService(CadDatabase database, MechanicalKernel? kernel = null)
    {
        _database = database;
        _kernel = kernel ?? new MechanicalKernel(); // Fallback
        _fittingSelector = new AutoFittingSelector(_kernel.PipeStandards); 
    }

    /*
       NE: BranÅŸman Sonucu (BranchResult)
       NEDEN: Bir otomatik baÄŸlantÄ± iÅŸlemi sonucunda sisteme eklenen yeni parÃ§alarÄ± (boru, fittings) ve silinmesi gereken eski parÃ§alarÄ± gruplamak iÃ§in.
    */
    public class BranchResult
    {
        public List<CadEntity> NewEntities { get; set; } = new();
        public List<CadEntity> RemovedEntities { get; set; } = new();
    }

    /*
       NE: Cihazları Boruya Bağla (ConnectFixturesToPipe)
       NEDEN: Seçilen vitrifiyeleri (lavabo, klozet vb.), hedeflenen ana toplama borusuna dik veya dikey kollarla bağlayıp; ana boruyu uygun yerlerden bölerek T-parçalarını otomatik oluşturmak için.
    */
    public List<CadEntity> ConnectFixturesToPipe(List<SanitaryFixtureEntity> fixtures, PipeEntity mainPipe)
    {
        var finalEntities = new List<CadEntity>();
        
        // İşlenecek boru listesi (Başlangıçta sadece ana boru)
        // Her bağlantıda ilgili boru silinip yerine 2 yeni boru eklenecek.
        var activePipes = new List<PipeEntity> { mainPipe };
        
        // Silinecekler listesi (Calling code clean-up yapabilsin diye, ama burada new list dönüyoruz)
        // NOT: Bu metodun imzası "List<CadEntity> NewEntities" döndürüyor. 
        // Eski boruların silinmesi işlemini TransactionManager ile handled etmemiz lazım.
        // Bu yüzden "Removed" olanları da return etmemiz veya DB'den silmemiz lazım.
        // Şimdilik DB işlemi yapmadan sadece Entity listesi dönüyoruz.
        // HACK: Transaction bütünlüğü için; çağıran metodun eski 'mainPipe'ı silmesi gerektiğini biliyoruz.
        // Ancak ara boruların silinmesi bu metodun içinde yönetilmeli.

        // Cihazları ana borunun başına olan mesafelerine göre sıralayalım (Düzgün bölme için)
        // Hangi eksen? Boru ekseni.
        var pStart = mainPipe.StartPoint;
        var pEnd = mainPipe.EndPoint;
        var pipeDir = (pEnd - pStart).Normalize();

        var sortedFixtures = fixtures
            .Select(f => new { Fixture = f, Proj = GetProjectionOnLine(f.Position, pStart, pipeDir) })
            .OrderBy(x => pStart.DistanceTo(x.Proj))
            .ToList();

        // Her bir cihaz için
        foreach (var item in sortedFixtures)
        {
            var fixture = item.Fixture;
            var ports = fixture.GetPorts();

            foreach (var port in ports)
            {
                // Sistem tipi kontrolü
                // (Basitleştirilmiş: Tüm aktif borulardan "sistem tipi uyan" ve "geometrik olarak en yakın" olanı bul)
                
                PipeEntity? targetPipe = null;
                Vector3D bestProj = Vector3D.Zero;
                double minDistance = double.MaxValue;

                foreach(var pipe in activePipes)
                {
                    if (!IsSystemMatch(port.Name, pipe.SystemType)) continue;

                    var projInfo = GetProjectionOnPipe(port.Position, pipe);
                    if (projInfo.IsOnSegment)
                    {
                         double dist = port.Position.DistanceTo(projInfo.Point);
                         if (dist < minDistance)
                         {
                             minDistance = dist;
                             targetPipe = pipe;
                             bestProj = projInfo.Point;
                         }
                    }
                }

                if (targetPipe != null)
                {
                    // Bağlantıyı yap
                    var result = CreateBranchConnection(port.Position, targetPipe, port);
                    
                    // Sonuçları işle
                    if (result.NewEntities.Any())
                    {
                        // 1. Yeni parçaları ana listeye ekle
                        finalEntities.AddRange(result.NewEntities);

                        // 2. Eğer boru bölündüyse (Split), aktif boru listesini güncelle
                        // Result içinde T-Parçası varsa, boru bölünmüş demektir.
                        var newPipes = result.NewEntities.OfType<PipeEntity>().Where(p => p.InnerDiameter == targetPipe.InnerDiameter).ToList(); 
                        // (Çap kontrolü biraz riskli ama T'den çıkan ince boruyu karıştırmamak için)
                        
                        // Daha sağlam kontrol: Segmentler targetPipe ile aynı doğrultuda mı?
                        var pipeVector = (targetPipe.EndPoint - targetPipe.StartPoint).Normalize();
                        
                        var newSegments = new List<PipeEntity>();
                        foreach(var ent in result.NewEntities)
                        {
                            if(ent is PipeEntity p)
                            {
                                var pDir = (p.EndPoint - p.StartPoint).Normalize();
                                // Paralel mi? (Dot product ~1 veya ~-1)
                                if (Math.Abs(Math.Abs(pDir.Dot(pipeVector)) - 1.0) < 0.01)
                                {
                                    newSegments.Add(p);
                                }
                            }
                        }

                        if (newSegments.Count >= 2) // Evet, bölünmüş
                        {
                            activePipes.Remove(targetPipe);
                            activePipes.AddRange(newSegments);
                            
                            // Target pipe artık "Silinmesi Gerekenler" listesinde (Result.Removed)
                            // Calling code'a bunu bildirmek için finalEntities içine "Removed" işaretiyle mi eklesek?
                            // Hayır, bu metod sadece "Ne Eklendi" döndürüyor.
                            // Transaction yönetimi için "CompositeOperation" dönmek en doğrusu ama imza List<CadEntity>.
                            
                            // MEVCUT SİSTEME UYUM: 
                            // MainWindow genelde dönenleri "AddEntity" yapıyor.
                            // Silme işlemini nasıl yapacağız? 
                            // ÇÖZÜM: TransactionManager'a erişimimiz var (_database üzerinden değil ama kernel var?).
                            // En temiz yol: Bu metodun IDatabaseTransaction döndürmesi veya işlemi kendisinin yapması.
                            // Şimdilik: "PipeReplacement" mantığıyla, bölünen borunun ID'sini kullanamayız (UUID).
                            
                            // GEÇİCİ ÇÖZÜM: Calling code (MainWindow) "mainPipe"ı silsin. 
                            // Biz burada "yeni oluşan tüm parçaları" dönelim.
                            // Sorun: İlk split'ten sonra "targetPipe" zaten yeni oluşturulmuş (DB'de olmayan) bir pipe olabilir.
                            // DB'de olmayan pipe'ı silmeye çalışmak hata vermez ama gereksiz.
                            
                            // O yüzden sadece "NewEntities" mantığıyla ilerliyoruz.
                            // "mainPipe" hariç, activePipes listesindeki diğerleri zaten "finalEntities" içinde var.
                            // Onları activePipes'tan çıkardığımızda "finalEntities"den de çıkarmalıyız ki duplicate olmasın.
                            
                            // Algoritma Revizyonu:
                            // finalEntities sadece EN SON geçerli olanları mı içermeli?
                            // Hayır, bu metod bir "İnşaat" yapıyor.
                            // Bölünen boruyu listeden silmeliyiz.
                            finalEntities.Remove(targetPipe); // Eğer listede varsa sil (ilk mainPipe listede yok)
                        }
                    }
                }
            }
        }
        
        // MainPipe bu listenin içinde DEĞİL. Onu çağıran silecek.
        // ActivePipes içinde kalanlar (son segmentler) falanEntities içinde olmalı.
        // Mantık biraz karıştı, basitleştirelim:
        
        // YÖNTEM 2: Tüm işlemi sıfırdan yap.
        // Return: Sadece eklenecekler. (Eski boruyu silmek çağıranın sorumluluğu)
        // Ama biz eski boruyu parça parça böldük.
        // Sonuçta elimizde bir sürü küçük boru ve fittings var.
        
        // finalEntities içinde şunlar var:
        // - Branşman boruları
        // - Dikey iniş boruları
        // - T-Parçaları
        // - VE Bölünmüş Ana Boru Parçaları (Pipe Segments)
        
        // Önemli olan: İterasyon sırasında 'targetPipe' olarak kullanılan ara borular, bir sonraki adımda bölünüp siliniyor.
        // 'finalEntities.Remove(targetPipe)' satırı bunu hallediyor.
        
        return finalEntities;
    }

    /*
       NE: Sistem Uyumluluk Kontrolü (IsSystemMatch)
       NEDEN: Bir cihazın çıkış portu (Örn: Soğuk Su) ile bağlandığı borunun sistem tipinin uyuşup uyuşmadığını doğrulamak için.
    */
    private bool IsSystemMatch(string portName, MechanicalSystemType pipeSystem)
    {
        if (portName == "ColdWater" && pipeSystem == MechanicalSystemType.DomesticColdWater) return true;
        if (portName == "HotWater" && pipeSystem == MechanicalSystemType.DomesticHotWater) return true;
        if ((portName == "Drainage" || portName == "Waste") && pipeSystem == MechanicalSystemType.WasteWater) return true;
        return false;
    }

    /*
       NE: Branşman Bağlantısı Oluştur (CreateBranchConnection)
       NEDEN: Tek bir nokta (cihaz girişi) ile ana boru arasında; dikey iniş, dikey kavis, fittings ve ara borulardan oluşan fiziksel bağlantı setini üretmek için.
    */
    private BranchResult CreateBranchConnection(Vector3D sourcePoint, PipeEntity mainPipe, MechanicalPort port)
    {
        var result = new BranchResult();
        
        // 1. Projeksiyon
        var projInfo = GetProjectionOnPipe(sourcePoint, mainPipe);
        if (!projInfo.IsOnSegment) return result; 

        Vector3D targetPoint = projInfo.Point;

        // 2. Ara Hat (Dikey + Yatay + Pathfinder)
        double zDiff = targetPoint.Z - sourcePoint.Z;
        Vector3D intermediatePoint = sourcePoint;
        
        // Z Kot farkı varsa Dikey Boru
        if (Math.Abs(zDiff) > 10.0) 
        {
            intermediatePoint = new Vector3D(sourcePoint.X, sourcePoint.Y, targetPoint.Z);
            var verticalPipe = new PipeEntity(sourcePoint, intermediatePoint, mainPipe.InnerDiameter / 2.0)
            {
                SystemType = mainPipe.SystemType,
                Color = mainPipe.Color,
                Id = Guid.NewGuid()
            };
            result.NewEntities.Add(verticalPipe);
        }

        // --- PATHFINDER ENTEGRASYONU (Suggestion 19) ---
        // Intermediate -> Target arasındaki yolu engellerden sakınarak bul
        var routePoints = _kernel.Pathfinder.FindPath(intermediatePoint, targetPoint);
        
        for (int i = 0; i < routePoints.Count - 1; i++)
        {
            var segment = new PipeEntity(routePoints[i], routePoints[i + 1], mainPipe.InnerDiameter / 2.0)
            {
                SystemType = mainPipe.SystemType,
                Color = mainPipe.Color,
                Id = Guid.NewGuid()
            };
            result.NewEntities.Add(segment);
            
            // Eğer rota kırıklıysa (dirsek varsa) araya dirsek/fittings eklenebilir.
        }

        // 3. Ana Boruyu Böl (SPLIT LOGIC)
        // Mevcut boru: Start -> End
        // Yeni: Start -> Target (Tee) -> End
        
        var splitPoint = targetPoint;
        
        // 3a. Segment 1 (Start -> Split)
        if (mainPipe.StartPoint.DistanceTo(splitPoint) > 1.0) // Min mesafe kontrolü
        {
            var p1 = new PipeEntity(mainPipe.StartPoint, splitPoint, mainPipe.InnerDiameter)
            {
                SystemType = mainPipe.SystemType,
                Color = mainPipe.Color,
                PipeMaterialType = mainPipe.PipeMaterialType,
                Id = Guid.NewGuid()
            };
            result.NewEntities.Add(p1);
        }

        // 3b. Segment 2 (Split -> End)
        if (splitPoint.DistanceTo(mainPipe.EndPoint) > 1.0)
        {
            var p2 = new PipeEntity(splitPoint, mainPipe.EndPoint, mainPipe.InnerDiameter)
            {
                SystemType = mainPipe.SystemType,
                Color = mainPipe.Color,
                PipeMaterialType = mainPipe.PipeMaterialType,
                Id = Guid.NewGuid()
            };
            result.NewEntities.Add(p2);
        }

        // 3c. T-Parçası
        var mainDir = (mainPipe.EndPoint - mainPipe.StartPoint).Normalize();
        var branchDir = (intermediatePoint - targetPoint).Normalize();

        var tee = new TeeEntity(splitPoint, mainPipe.InnerDiameter, mainPipe.InnerDiameter / 2.0, mainDir, branchDir)
        {
            Color = mainPipe.Color,
            SystemType = mainPipe.SystemType,
            PipeMaterialType = mainPipe.PipeMaterialType,
            Id = Guid.NewGuid()
        };
        result.NewEntities.Add(tee);

        // 3d. Eski Boruyu Silinecekler Listesine Ekle
        result.RemovedEntities.Add(mainPipe);

        return result;
    }
    
    /*
       NE: Boru Ãœzerindeki Ä°zdÃ¼ÅŸÃ¼m (GetProjectionOnPipe)
       NEDEN: Bir noktanÄ±n belirli bir boru segmentine olan en kÄ±sa mesafeli izdÃ¼ÅŸÃ¼m noktasÄ±nÄ± ve bu noktanÄ±n segment Ã¼zerinde olup olmadÄ±ÄŸÄ±nÄ± bulmak iÃ§in.
    */
    private (Vector3D Point, bool IsOnSegment) GetProjectionOnPipe(Vector3D p, PipeEntity pipe)
    {
        var v = pipe.EndPoint - pipe.StartPoint;
        var w = p - pipe.StartPoint;
        double c1 = w.Dot(v);
        double c2 = v.Dot(v);
        if(c2 == 0) return (pipe.StartPoint, false);
        double b = c1 / c2;
        Vector3D projection = pipe.StartPoint + (v * b);
        bool onSegment = b >= 0 && b <= 1;
        return (projection, onSegment);
    }
    
    /*
       NE: DoÄŸru Ãœzerindeki Ä°zdÃ¼ÅŸÃ¼m (GetProjectionOnLine)
       NEDEN: Bir noktadan sonsuz bir doÄŸruya (Ä±ÅŸÄ±na) dik izdÃ¼ÅŸÃ¼m noktasÄ±nÄ± hesaplamak iÃ§in.
    */
    private Vector3D GetProjectionOnLine(Vector3D p, Vector3D start, Vector3D dir)
    {
        var w = p - start;
        double c1 = w.Dot(dir);
        return start + (dir * c1);
    }

    /*
       NE: Akıllı Kollektör Oluştur (CreateSmartCollector)
       NEDEN: Mahal içindeki cihazları (Örn: Bir banyodaki lavabo, duş ve klozet), duvara paralel giden ana bir toplama hattında birleştirip tek bir çıkışa (Şafta) yönlendiren komple tesisat ağını tek tıkla kurmak için.
    */
    public List<CadEntity> CreateSmartCollector(List<SanitaryFixtureEntity> fixtures, Vector3D roomCenter, MechanicalSystemType systemType)
    {
        if (!fixtures.Any()) return new List<CadEntity>();

        // 1. Cihazları Sırala
        var xs = fixtures.Select(f => f.Position.X).ToList();
        var ys = fixtures.Select(f => f.Position.Y).ToList();
        bool isHorizontal = (xs.Max() - xs.Min()) > (ys.Max() - ys.Min());

        var sortedFixtures = isHorizontal 
            ? fixtures.OrderBy(f => f.Position.X).ToList() 
            : fixtures.OrderBy(f => f.Position.Y).ToList();

        // 2. Ana Hat Geometrisini Belirle
        double avgCoord = isHorizontal ? ys.Average() : xs.Average();
        double pipeZ = fixtures.First().Position.Z - 500; 
        if (systemType == MechanicalSystemType.DomesticColdWater) pipeZ = fixtures.First().Position.Z + 500;

        Vector3D startP, endP;
        if (isHorizontal)
        {
            startP = new Vector3D(sortedFixtures.First().Position.X - 500, avgCoord, pipeZ);
            endP = new Vector3D(sortedFixtures.Last().Position.X + 1000, avgCoord, pipeZ);
        }
        else
        {
            startP = new Vector3D(avgCoord, sortedFixtures.First().Position.Y - 500, pipeZ);
            endP = new Vector3D(avgCoord, sortedFixtures.Last().Position.Y + 1000, pipeZ);
        }

        // Ana Kollektör Borusu (İlk parça)
        var collectorPipe = new PipeEntity(startP, endP, 100)
        {
            SystemType = systemType,
            Color = (systemType == MechanicalSystemType.WasteWater) ? 0xFF8B4513 : 0xFF0000FF
        };

        // 3. Bağlantı Algoritmasını Çalıştır
        // Bu metod (ConnectFixturesToPipe) ana boruyu böler ve tüm parçaları döndürür.
        var connectionNetwork = ConnectFixturesToPipe(sortedFixtures, collectorPipe);
        
        // Eğer hiçbir bağlantı yapılmadıysa (örn sistem tipi uyuşmazlığı), sadece kollektörü dön
        if (!connectionNetwork.Any())
        {
            return new List<CadEntity> { collectorPipe };
        }
        
        // ConnectFixturesToPipe mantığında, 'collectorPipe' (ana boru) final listede yer almaz (parçalanmıştır).
        // Bu yüzden sadece result'ı dönmek yeterli.
        
        return connectionNetwork;
    }

    /*
       NE: Kolona Bağla (ConnectToRiser)
       NEDEN: Yatay bir dağıtım borusunu, dikey bir kolon hattına (Riser) otomatik olarak bağlayıp ara parçaları ve T-elemanını eklemek için.
    */
    public BranchResult ConnectToRiser(PipeEntity horizontalPipe, PipeEntity riserPipe)
    {
        var result = new BranchResult();
        
        // Riser kontrolü (Dikey olmalı)
        if (Math.Abs(riserPipe.StartPoint.X - riserPipe.EndPoint.X) > 1.0 || 
            Math.Abs(riserPipe.StartPoint.Y - riserPipe.EndPoint.Y) > 1.0)
            return result;

        double distStart = DistancePointToLine(horizontalPipe.StartPoint, riserPipe.StartPoint, riserPipe.EndPoint);
        double distEnd = DistancePointToLine(horizontalPipe.EndPoint, riserPipe.StartPoint, riserPipe.EndPoint);

        Vector3D connectPoint = distStart < distEnd ? horizontalPipe.StartPoint : horizontalPipe.EndPoint;
        Vector3D riserPoint = new Vector3D(riserPipe.StartPoint.X, riserPipe.StartPoint.Y, connectPoint.Z);

        // Bağlantı borusu (Connector Pipe)
        if (connectPoint.DistanceTo(riserPoint) > 1.0)
        {
            var connector = new PipeEntity(connectPoint, riserPoint, horizontalPipe.InnerDiameter)
            {
                SystemType = horizontalPipe.SystemType,
                Color = horizontalPipe.Color,
                Id = Guid.NewGuid()
            };
            result.NewEntities.Add(connector);
        }

        // --- RISER SPLITTING LOGIC ---
        // Riser'ı kesip T ekliyoruz.
        // Orijinal Riser: Start -> End
        // Yeni: Start -> RiserPoint (Tee) -> End
        
        var splitPoint = riserPoint;
        
        // Segment 1 (Alt -> T)
        if (riserPipe.StartPoint.DistanceTo(splitPoint) > 1.0)
        {
            var p1 = new PipeEntity(riserPipe.StartPoint, splitPoint, riserPipe.InnerDiameter)
            {
                SystemType = riserPipe.SystemType, // Hata düzeltildi
                Color = riserPipe.Color,
                PipeMaterialType = riserPipe.PipeMaterialType,
                Id = Guid.NewGuid()
            };
            p1.SystemType = riserPipe.SystemType; // Düzeltme
            result.NewEntities.Add(p1);
        }

        // Segment 2 (T -> Üst)
        if (splitPoint.DistanceTo(riserPipe.EndPoint) > 1.0)
        {
            var p2 = new PipeEntity(splitPoint, riserPipe.EndPoint, riserPipe.InnerDiameter)
            {
                SystemType = riserPipe.SystemType,
                Color = riserPipe.Color,
                PipeMaterialType = riserPipe.PipeMaterialType,
                Id = Guid.NewGuid()
            };
            result.NewEntities.Add(p2);
        }
        
        // T-Parçası
        var mainDir = (riserPipe.EndPoint - riserPipe.StartPoint).Normalize();
        var branchDir = (connectPoint - riserPoint).Normalize();

        var tee = new TeeEntity(splitPoint, riserPipe.InnerDiameter, horizontalPipe.InnerDiameter, mainDir, branchDir)
        {
            Color = riserPipe.Color,
            SystemType = riserPipe.SystemType,
            PipeMaterialType = riserPipe.PipeMaterialType,
            Id = Guid.NewGuid()
        };
        result.NewEntities.Add(tee);

        // Eski Riser'ı silinmek üzere işaretle
        result.RemovedEntities.Add(riserPipe);

        return result;
    }

    /*
       NE: Nokta-DoÄŸru Mesafesi (DistancePointToLine)
       NEDEN: Verilen bir noktanÄ±n bir doÄŸru parÃ§asÄ±na olan en kÄ±sa Öklid mesafesini hesaplamak iÃ§in.
    */
    private double DistancePointToLine(Vector3D p, Vector3D s, Vector3D e)
    {
        var v = e - s;
        var w = p - s;
        double c1 = w.Dot(v);
        double c2 = v.Dot(v);
        if (c2 <= 0) return p.DistanceTo(s);
        double b = c1 / c2;
        Vector3D projection = (b < 0) ? s : (b > 1) ? e : s + (v * b);
        return p.DistanceTo(projection);
    }
}
