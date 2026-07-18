using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Engine;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;

namespace Afney.Cad.Mechanical.Services
{
    public class ValidationResult
    {
        public bool IsValid { get; set; } = true;
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
        public List<Guid> ProblematicEntityIds { get; set; } = new List<Guid>();
    }

    /*
       NE: DomainGuardService (Süreç ve Hesap Öncesi Denetleyicisi)
       NEDEN: FineSANI'nin süreç-kilitli (Flow-locked) mimarisinde, tesisat çizimi bittikten sonra "Hesapla" aşamasına geçmeden önce
              verinin doğruluğunu, yön hatalarını ve açık uçları tespit edip raporlamak için.
    */
    public class DomainGuardService
    {
        private readonly CadDatabase _database;
        private readonly MechanicalTopologyGraph _topology;

        public DomainGuardService(CadDatabase? database, MechanicalTopologyGraph topology)
        {
            _database = database!; // Can be null initially
            _topology = topology ?? throw new ArgumentNullException(nameof(topology));
        }

        /*
           NE: Sistemi Denetle (ValidateSystem)
           NEDEN: Tüm tesisatı kurallara karşı denetleyip "Hesap Yapılabilir/Yapılamaz" kararı vermek için.
        */
        public ValidationResult ValidateSystem()
        {
            var result = new ValidationResult();

            // 1. Açık Uç Kontrolü
            CheckOpenEnds(result);

            // 2. Akış Yönü ve Döngü (Cycle) Kontrolü
            CheckTopologyConsistency(result);

            // 3. MÜHENDİSLİK GUARD: Armatür Kontrolü (V-P01/V-P02)
            CheckFixtureAvailability(result);

            // 4. MÜHENDİSLİK GUARD: Giriş Noktası Kontrolü (V-000)
            CheckSourceConnectivity(result);

            // 5. Sistem Tipi Tutarlılığı
            CheckSystemConsistency(result);

            // 6. Pis Su / Yağmur Suyu Boru Eğim Kontrolü (TS EN 12056-2, min %2)
            CheckWastePipeSlopes(result);

            if (result.Errors.Any())
            {
                result.IsValid = false;
            }

            return result;
        }

        /*
           NE: Armatür Varlığını Kontrol Et (CheckFixtureAvailability)
           NEDEN: Sistemde hiç armatür (Lavabo, WC vb.) yoksa hesaplama yapmak anlamsızdır.
        */
        private void CheckFixtureAvailability(ValidationResult result)
        {
            if (_database == null) return;
            var fixtures = _database.GetAllEntities().OfType<SanitaryFixtureEntity>().ToList();
            if (!fixtures.Any())
            {
                result.Errors.Add("Hata (V-P01): Şebekede tanımlı armatür bulunamadı. Hesaplama yapılamaz.");
            }
        }

        /*
           NE: Kaynak Bağlantısını Kontrol Et (CheckSourceConnectivity)
           NEDEN: Tesisatın bir ana su girişine veya kolon başlangıcına bağlı olması gerekir.

           ÖNCEDEN: "Basitleştirilmiş" olarak yalnızca şebekede EN AZ BİR MechanicalLoadNode
           VAR MI diye bakılıyordu — çizimde tamamen izole (hiçbir boruya bağlı olmayan,
           yanlışlıkla boşluğa bırakılmış) bir giriş noktası bile bu kontrolü geçiyordu.

           ARTIK: En az bir giriş noktasının GERÇEKTEN boru şebekesine bağlı olduğu (portu
           IsConnected) VE o şebeke üzerinden BFS ile en az bir gerçek armatüre (SanitaryFixtureEntity)
           ulaşılabildiği doğrulanıyor — yani kaynak sadece var olmakla kalmıyor, tesisatı
           gerçekten besliyor.

           KAPSAM DIŞI: Kaynağın "doğru yönde" (örn. ters bağlanmış bir vana) olup olmadığının
           tam akış-yönü analizi ayrı bir mimari konu (FlowCalculationService'in kök tespiti) —
           bu metod sadece bağlantı/ulaşılabilirlik doğruluyor, akış yönü doğruluğu doğrulamıyor.
        */
        private void CheckSourceConnectivity(ValidationResult result)
        {
            if (_database == null) return;

            var loadNodes = _database.GetAllEntities().OfType<MechanicalLoadNode>().ToList();
            if (!loadNodes.Any())
            {
                result.Errors.Add("Hata (V-000): Şebekede su giriş noktası (Valve/Meter) veya ana kolon tespiti yapılamadı.");
                return;
            }

            bool anyConnectedSourceReachesFixture = false;
            foreach (var loadNode in loadNodes)
            {
                var startGraphNode = _topology.GetNode(loadNode.Id);
                if (startGraphNode == null) continue;
                if (!startGraphNode.Ports.Any(p => p.IsConnected)) continue; // izole giriş noktası

                var visited = new HashSet<Guid> { loadNode.Id };
                var queue = new Queue<Guid>();
                queue.Enqueue(loadNode.Id);

                while (queue.Count > 0 && !anyConnectedSourceReachesFixture)
                {
                    var current = queue.Dequeue();
                    foreach (var neighbor in _topology.GetNeighbors(current))
                    {
                        if (visited.Contains(neighbor.EntityId)) continue;
                        visited.Add(neighbor.EntityId);

                        if (neighbor.Entity is SanitaryFixtureEntity)
                        {
                            anyConnectedSourceReachesFixture = true;
                            break;
                        }
                        queue.Enqueue(neighbor.EntityId);
                    }
                }

                if (anyConnectedSourceReachesFixture) break;
            }

            if (!anyConnectedSourceReachesFixture)
            {
                result.Errors.Add("Hata (V-000): Giriş noktası (Valve/Meter) mevcut ama boru şebekesi üzerinden hiçbir armatüre bağlı değil — izole veya kopuk olabilir.");
            }
        }

        /*
           NE: Sistem Tipi Tutarlılığını Kontrol Et (CheckSystemConsistency)
           NEDEN: Temiz su hattına yanlışlıkla pis su armatürü bağlanması gibi hataları yakalamak için.
        */
        private void CheckSystemConsistency(ValidationResult result)
        {
            // Topolojik ağda farklı sistem tiplerinin karıştığı boruları bul
            foreach (var node in _topology.Nodes)
            {
                if (node.Entity is PipeEntity pipe)
                {
                    var neighbors = _topology.GetNeighbors(node.EntityId);
                    foreach (var neighbor in neighbors)
                    {
                        if (neighbor.SystemType != pipe.SystemType && 
                            neighbor.SystemType != MechanicalSystemType.Undefined && 
                            pipe.SystemType != MechanicalSystemType.Undefined)
                        {
                            result.Warnings.Add($"Sistem Karmaşası: {pipe.SystemType} hattı, {neighbor.SystemType} bir nesneye bağlanmış.");
                        }
                    }
                }
            }
        }

        /*
           NE: Açık Uçları Tespit Et (CheckOpenEnds)
           NEDEN: Bir borunun, fitingin veya bağlanan bir nesnenin boşta ucu kalıp kalmadığını tespit etmek için.
                  Açık uçlu borular hesaplamayı (özellikle basınç kaybı ve ağaç algoritmalarını) bozar.
        */
        private void CheckOpenEnds(ValidationResult result)
        {
            foreach (var node in _topology.Nodes)
            {
                // Mahaller açık uç mantığının dışındadır
                if (node.Entity is MahalEntity) continue;

                foreach (var port in node.Ports)
                {
                    if (!port.IsConnected)
                    {
                        string entityName = GetEntityDisplayName(node.Entity);
                        result.Errors.Add($"Açık Uç Tespit Edildi: {entityName} üzerinde bağdaşmayan '{port.Name}' portu.");
                        result.ProblematicEntityIds.Add(node.EntityId);
                    }
                }
            }
        }

        /*
           NE: Topolojik Tutarlılık Kontrolü (CheckTopologyConsistency)
           NEDEN: Sistemde döngüler (kapalı çevrimler - kapalı sistemler hariç) veya izole kalmış tesisat parçaları var mı görmek için.
        */
        private void CheckTopologyConsistency(ValidationResult result)
        {
            var allNodes = _topology.Nodes.ToList();
            if (allNodes.Count == 0) return;

            // BFS ile bağlı bileşen sayısını bul
            var visited = new HashSet<Guid>();
            int componentCount = 0;
            int isolatedCount  = 0;

            foreach (var startNode in allNodes)
            {
                if (visited.Contains(startNode.EntityId)) continue;

                componentCount++;
                var queue = new Queue<Guid>();
                queue.Enqueue(startNode.EntityId);
                visited.Add(startNode.EntityId);

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    foreach (var neighbor in _topology.GetNeighbors(current))
                    {
                        if (!visited.Contains(neighbor.EntityId))
                        {
                            visited.Add(neighbor.EntityId);
                            queue.Enqueue(neighbor.EntityId);
                        }
                    }
                }

                // Tek başına kopuk kalan entity = izole bileşen
                if (componentCount > 1)
                    isolatedCount++;
            }

            if (isolatedCount > 0)
            {
                result.Warnings.Add($"Topoloji Uyarısı: Şebekede {isolatedCount} adet izole (bağlantısız) tesisat grubu tespit edildi. Bu gruplar hesaplamalara dahil edilmeyecek.");
            }

            // Döngü tespiti: BFS'de ziyaret edilen düğüm sayısı ile toplam düğüm sayısını karşılaştır
            // Tek bileşenli bir ağaçta |kenar| = |düğüm| - 1; döngü varsa fazla kenar mevcuttur.
            // Basit yaklaşım: geriye bağlantı (back-edge) tespiti
            var parentMap = new Dictionary<Guid, Guid?>();
            foreach (var startNode in allNodes)
            {
                if (parentMap.ContainsKey(startNode.EntityId)) continue;
                var stack = new Stack<(Guid Id, Guid? Parent)>();
                stack.Push((startNode.EntityId, null));

                while (stack.Count > 0)
                {
                    var (current, parent) = stack.Pop();
                    if (parentMap.ContainsKey(current))
                    {
                        result.Warnings.Add("Topoloji Uyarısı: Şebekede kapalı çevrim (döngü) tespit edildi. Kapalı sistem ısıtma dışı sistemlerde beklenmedik hesap sonuçlarına yol açabilir.");
                        break;
                    }
                    parentMap[current] = parent;
                    foreach (var neighbor in _topology.GetNeighbors(current))
                    {
                        if (neighbor.EntityId != parent)
                            stack.Push((neighbor.EntityId, current));
                    }
                }
            }
        }

        /*
           NE: Pis Su / Yağmur Suyu Boru Eğim Kontrolü (CheckWastePipeSlopes)
           NEDEN: TS EN 12056-2 — yatay atık su borularının minimum eğimi %2 (0.02) olmalı.
                  Dikey kolonlar (|dZ/L| > 0.8) bu kontrolün dışındadır.
        */
        private void CheckWastePipeSlopes(ValidationResult result)
        {
            if (_database == null) return;
            const double MinSlope = 0.02;

            var wastePipes = _database.GetAllEntities()
                .OfType<PipeEntity>()
                .Where(p => p.SystemType is MechanicalSystemType.WasteWater
                                        or MechanicalSystemType.RainWater)
                .ToList();

            foreach (var pipe in wastePipes)
            {
                // Dikey kolon borular eğim kontrolünden muaf
                var dir = (pipe.EndPoint - pipe.StartPoint);
                double length = dir.Length();
                if (length < 1) continue;
                double verticalRatio = Math.Abs(dir.Z) / length;
                if (verticalRatio > 0.8) continue;

                // Slope = 0 ise geometriden hesapla
                double slope = pipe.Slope > 0
                    ? pipe.Slope
                    : (length > 0 ? Math.Abs(dir.Z) / Math.Sqrt(dir.X * dir.X + dir.Y * dir.Y + 0.0001) : 0);

                if (slope < MinSlope)
                {
                    string sys = pipe.SystemType == MechanicalSystemType.WasteWater ? "Pis Su" : "Yağmur Suyu";
                    result.Warnings.Add(
                        $"Eğim Yetersiz ({sys}): Boru DN{pipe.InnerDiameter:F0} — " +
                        $"mevcut eğim %{slope * 100:F1}, minimum %{MinSlope * 100:F0} olmalı (TS EN 12056-2).");
                    result.ProblematicEntityIds.Add(pipe.Id);
                }
            }
        }

        private string GetEntityDisplayName(MechanicalEntity entity)
        {
            if (entity is PipeEntity pipe) return $"Boru (Ø{pipe.InnerDiameter})";
            if (entity is SanitaryFixtureEntity sf) return $"Vitrifiye/{sf.FixtureType}";
            if (entity is ElbowEntity) return "Dirsek";
            if (entity is TeeEntity) return "T-Parçası";
            if (entity is ReducerEntity) return "Redüksiyon";

            return $"Nesne (Tip: {entity.GetType().Name})";
        }
    }
}
