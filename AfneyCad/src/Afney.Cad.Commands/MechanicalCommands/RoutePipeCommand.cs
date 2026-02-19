using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Database.Core;
using Afney.Cad.Database.Transactions;
using Afney.Cad.Database.Transactions.Operations;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Commands.MechanicalCommands;

/*
   NE: Boru Yönlendirme Komutu (RoutePipeCommand)
   NEDEN: Kullanıcının belirlediği noktalar arasında otomatik dirsek ve boru segmentleri oluşturarak rota çizmek için.
*/
public class RoutePipeCommand : ICadCommand
{
    private readonly CadDatabase _database;
    private readonly MechanicalKernel _kernel;
    private readonly PipeRoutingEngine _routingEngine;
    private readonly List<CadEntity> _ghostEntities = new();
    
    private string _currentMaterial = "PVC";
    private MechanicalSystemType _currentSystem = MechanicalSystemType.DomesticColdWater;
    private double _currentDiameter = 100.0;

    public string CommandName => "ROUTEPIPE";
    public Vector3D? ActivePoint => _routingEngine.LastPoint;

    public event Action<string>? OnFeedback;
    public event Action? OnCompleted;
    public event Action<CadEntity>? OnEntityPlaced;
    public event Action<CadEntity>? OnEntityRemoved;

    public RoutePipeCommand(CadDatabase database, MechanicalKernel kernel)
    {
        _database = database;
        _kernel = kernel;
        _routingEngine = new PipeRoutingEngine();
        
        // Kernel üzerindeki akıllı parça seçiciyi motora bağla (Entegrasyon)
        _routingEngine.SetFittingSelector(_kernel.FittingSelector);
    }

    /*
       NE: Rota Ayarlarını Yap (SetSettings)
       NEDEN: Çizilecek borunun çapını, sistem tipini (Soğuk Su, Atık Su vb.) ve standardını belirleyerek hidrolik hesaplara baz oluşturmak için.
    */
    public void SetSettings(double diameter, MechanicalSystemType systemType, string material = "PVC", double slope = 0.0)
    {
        _currentDiameter = diameter;
        _currentSystem = systemType;
        _currentMaterial = material;

        _routingEngine.SetDiameter(diameter);
        _routingEngine.SetSystemType(systemType);
        _routingEngine.SetSlope(slope);

        // Standartlardan boru detaylarını al (Mebrure Hanım'ın kütüphanesi)
        var standard = _kernel.PipeStandards.GetStandard(material, "TS EN 12056"); 
        if (standard != null)
        {
            var def = standard.GetBySize(diameter);
            
            // Mühendislik verisini motora aktar
            _routingEngine.SetStandardDefinition(def);
        }
    }

    /*
       NE: Komutu Başlat (Start)
       NEDEN: Kullanıcıya boru rotalamanın başladığını bildirmek ve ilk tıklama için (Başlangıç Noktası) rehber metin göstermek için.
    */
    public void Start()
    {
        OnFeedback?.Invoke($"BORU ROTALAMA: Başlangıç noktası seçin. (DN{_currentDiameter}, {_currentSystem})");
    }

    /*
       NE: Tıklama Olayı (OnPointerPressed)
       NEDEN: İlk tıklamada hattı başlatmak veya bir boruya branşman (T) ile bağlanmak; sonraki tıklamalarda ise rota üzerinde yeni boru segmentleri oluşturmak için.
    */
    /*
       NE: Tıklama Olayı (OnPointerPressed)
       NEDEN: İlk tıklamada hattı başlatmak veya bir boruya branşman (T) ile bağlanmak; sonraki tıklamalarda ise rota üzerinde yeni boru segmentleri oluşturmak için.
    */
    public void OnPointerPressed(Vector3D point)
    {
        // 1. İLK TIK: Rota başlangıcı
        if (_routingEngine.LastPoint == null)
        {
            // Tıklanan noktada boru var mı kontrol et (Branşman için)
            var clickedPipe = FindPipeAtPoint(point, 10.0);
            if (clickedPipe != null)
            {
                StartBranching(clickedPipe, point);
            }
            else
            {
                _routingEngine.StartRoute(point, _currentDiameter);
                OnFeedback?.Invoke($"Başlangıç: {point}. Sonraki noktayı seçin.");
            }
            return;
        }

        // 2. SONRAKİ TIKLAR: Manuel Rota (Kullanıcı Kontrollü)
        // Pathfinding şimdilik devre dışı (Manuel çizim öncelikli)
        
        var newEntities = _routingEngine.AddPoint(point);
        foreach (var entity in newEntities)
        {
            RegisterNewEntity(entity);
        }
        
        OnFeedback?.Invoke($"Boru eklendi. Devam edin veya ESC ile bitirin. (Son: {point})");
    }

    /*
       NE: Branşmanı Başlat (StartBranching)
       NEDEN: Yeni çizilen borunun başlangıcını mevcut bir boruya (Pipe) dayadığımızda, araya otomatik bir T-parçası atarak hattı ikiye bölmek ve topolojik sürekliliği sağlamak için.
    */
    private void StartBranching(PipeEntity target, Vector3D branchPoint)
    {
        var entities = _routingEngine.StartBranch(target, branchPoint);
        
        // Eski boruyu sil
        // Not: RemoveEntity çağrısı MainWindow tarafından yönetilmeli veya veritabanı olayları ile senkronize olmalı.
        // Burada direkt veritabanından siliyoruz, Undo/Redo için bunu CommandHistory'e bildirmek gerekir ama şimdilik direct call.
        _database.RemoveEntity(target.Id); 
        OnEntityRemoved?.Invoke(target);

        // Yeni parçaları ekle
        foreach (var e in entities)
        {
            RegisterNewEntity(e);
        }
        
        OnFeedback?.Invoke("Branşman (T) oluşturuldu. Rotaya devam ediliyor...");
    }

    /*
       NE: Nesneyi Kaydet (RegisterNewEntity)
       NEDEN: Yeni oluşturulan her bir boru veya dirsek parçasını merkezi sistemlere bildirerek veritabanına eklenmesini ve topolojiye dahil edilmesini sağlamak için.
    */
    private void RegisterNewEntity(CadEntity entity)
    {
        // ÖNEMLİ: Veritabanına ekleme işlemini MainWindow OnEntityPlaced -> TransactionManager üzerinden yapıyor olabilir.
        // Ancak genellikle Command kendi işini yapar veya event fırlatır.
        // MainWindow implementation: cmd.OnEntityPlaced += entity => _history...Submit(...)
        // Bu yüzden burada _database.AddEntity(entity) ÇAĞIRMAMALIYIZ. Sadece Event fırlatmalıyız.
        // FAKAT: Canlı önizleme için veritabanında olması gerekebilir. 
        // Genelde Pattern şöyledir: Command geçici (Transient) olarak çizer, Commit olduğunda veritabanına girer.
        // Ama RoutePipe "Sürekli" bir komut (Continuous). Her segment anında commit olmalı.
        
        // MainWindow'daki koda güvenerek sadece Event fırlatıyoruz.
        // MainWindow: cmd.OnEntityPlaced += entity => _history.TransactionManager.Submit(new AddEntityOperation(_database, entity));
        
        OnEntityPlaced?.Invoke(entity);
    }

    /*
       NE: Boruyu Noktada Bul (FindPipeAtPoint)
       NEDEN: Kullanıcının tıkladığı koordinatta mevcut bir boru olup olmadığını (tolerans dahilinde) kontrol ederek branşman (T) bağlantısı başlatıp başlatmayacağımıza karar vermek için.
    */
    private PipeEntity? FindPipeAtPoint(Vector3D point, double tolerance)
    {
        return _database.GetAllEntities()
            .OfType<PipeEntity>()
            .FirstOrDefault(p => DistancePointToLine(point, p.StartPoint, p.EndPoint) < tolerance);
    }

    /*
       NE: Nokta-Çizgi Mesafesi (DistancePointToLine)
       NEDEN: Tıklanan farenin, borunun eksenine ne kadar yakın olduğunu (L2 normu izdüşümü ile) hesaplayarak hassas yakalama (Snap/Pick) yapmak için.
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

    /*
       NE: Fare Hareket Olayı (OnPointerMoved)
       NEDEN: Çizilmekte olan borunun ucunu fare imleciyle birlikte hareket ettirerek lastik bant (Rubber-band) efektiyle anlık önizleme sağlamak için.
    */
    public void OnPointerMoved(Vector3D point) => UpdateGhost(point);

    /*
       NE: Önizleme Güncelle (UpdateGhost)
       NEDEN: Fare hareket ettikçe, henüz çizilmeyen borunun nereye uzanacağını (Sarı hayalet çizgi ile) görselleştirerek kullanıcıya geri bildirim sağlamak için.
    */
    private void UpdateGhost(Vector3D endPoint)
    {
        _ghostEntities.Clear();
        var lastPoint = _routingEngine.LastPoint;
        if (lastPoint == null) return;

        // Ghost line (Sarı rehber çizgi)
        var line = new Afney.Cad.Domain.Entities.Basic.LineEntity(lastPoint.Value, endPoint) 
        { 
            Color = 0xAAFFFF00 // Yarı saydam sarı
        };
        _ghostEntities.Add(line);
    }

    public void OnKeyDown(InputKey key)
    {
        if (key == InputKey.Escape) OnCompleted?.Invoke();
    }

    public void Draw(IRenderContext context)
    {
        foreach (var ghost in _ghostEntities) ghost.Draw(context);
    }

    public void Cancel() => _ghostEntities.Clear();
}
