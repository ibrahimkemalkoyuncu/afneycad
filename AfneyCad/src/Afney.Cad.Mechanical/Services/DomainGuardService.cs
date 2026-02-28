using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Engine;
using Afney.Cad.Mechanical.Entities;

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

        public DomainGuardService(CadDatabase database, MechanicalTopologyGraph topology)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
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

            if (result.Errors.Any())
            {
                result.IsValid = false;
            }

            return result;
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
                // Odalar açık uç mantığının dışındadır
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
            // İleride ağacın (Tree) yapısının kontrolü, ters akış tespiti (Reverse Flow) gibi mantıklar buraya eklenecektir.
            // Örn: DFS / BFS ile Adjacency List gezilir ve Cycle Detection yapılır.
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
