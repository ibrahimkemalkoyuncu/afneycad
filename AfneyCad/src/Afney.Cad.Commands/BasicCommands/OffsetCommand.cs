using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Database.Core;
using Afney.Cad.Database.Transactions; 
using Afney.Cad.Database.Transactions.Operations; 
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Commands.BasicCommands;

/*
   NE: Offset (Öteleme) Komutu
   NEDEN: Seçilen objeleri eksene dik doğrultuda fare ile belirlenen yöne (ve mesafeye) öteleyerek paralel kopyalarını üretmek için.
*/
public class OffsetCommand : ICadCommand
{
    private readonly CadDatabase _database;
    private readonly TransactionManager _transactionManager;
    private readonly List<CadEntity> _entitiesToOffset;
    
    // Ghost drawing
    private List<CadEntity>? _ghostEntities;
    private Vector3D? _currentMousePos;

    public string CommandName => "OFFSET";
    public Vector3D? ActivePoint => null; // Offset için base point çizim esnasında dinamiktir
    public event Action<string>? OnFeedback;
    public event Action? OnCompleted;

    public OffsetCommand(CadDatabase database, TransactionManager transactionManager, IEnumerable<CadEntity> selection)
    {
        _database = database;
        _transactionManager = transactionManager;
        var enumerable = selection as CadEntity[] ?? selection.ToArray();
        // Desteklenen objeler: Line, Pipe, Circle, Arc (Polyline henüz desteklenmiyor)
        _entitiesToOffset = enumerable.Where(e => e is LineEntity || e is PipeEntity || e is CircleEntity || e is ArcEntity).ToList();

        if (_entitiesToOffset.Count != enumerable.Length)
        {
             Serilog.Log.Warning("OFFSET: Bazı nesneler offset işlemini desteklemiyor ve listeden çıkarıldı.");
        }
    }
    
    public void Start()
    {
        if (_entitiesToOffset.Count == 0)
        {
            OnFeedback?.Invoke("OFFSET: Öncelikle ötelenecek Line veya Pipe nesnelerini seçin. Komut iptal.");
            OnCompleted?.Invoke();
            return;
        }

        OnFeedback?.Invoke("OFFSET: Öteleme yönü ve mesafesini belirlemek için hedef noktaya tıklayın.");
    }

    public void OnPointerPressed(Vector3D point)
    {
        UpdateGhosts(point);
        
        if (_ghostEntities != null && _ghostEntities.Count > 0)
        {
            var composite = new CompositeOperation("Offset Entities");
            foreach (var ent in _ghostEntities)
            {
                // Clone yapılmış olanları temiz (gri) renginden orijinal rengine çevirelim
                ent.Color = _entitiesToOffset.FirstOrDefault(o => o.Layer == ent.Layer)?.Color ?? 0xFFFFFFFF;
                composite.Add(new AddEntityOperation(_database, ent));
            }

            _transactionManager.Submit(composite);
            
            OnFeedback?.Invoke($"OFFSET: {_ghostEntities.Count} nesne ötelendi.");
        }
        else
        {
            OnFeedback?.Invoke("OFFSET: Ötelenen nesne hesaplanamadı.");
        }
        
        OnCompleted?.Invoke();
    }

    public void OnPointerMoved(Vector3D point)
    {
         _currentMousePos = point;
         UpdateGhosts(point);
    }

    private void UpdateGhosts(Vector3D targetPoint)
    {
         _ghostEntities = new List<CadEntity>();
         
         foreach (var ent in _entitiesToOffset)
         {
             var clone = ent.Clone();
             clone.Color = 0xFFAAAAAA; // Ghost rengi (Gri)
             
             if (ent is LineEntity line)
             {
                 var v = CalculateOffsetVector(line.StartPoint, line.EndPoint, targetPoint);
                 clone.Move(v);
             }
             else if (ent is PipeEntity pipe)
             {
                 var v = CalculateOffsetVector(pipe.StartPoint, pipe.EndPoint, targetPoint);
                 clone.Move(v);
             }
             else if (ent is CircleEntity circle)
             {
                 // Çember offset: yeni yarıçap = merkezden hedef noktaya olan mesafe.
                 // Dışarı tıklarsa büyür, içeri tıklarsa küçülür — Line offset'teki "hedefe doğru ötele" mantığıyla tutarlı.
                 _ghostEntities.Add(new CircleEntity(circle.Center, targetPoint.DistanceTo(circle.Center)) { Color = 0xFFAAAAAA, Layer = circle.Layer });
                 continue;
             }
             else if (ent is ArcEntity arc)
             {
                 _ghostEntities.Add(new ArcEntity(arc.Center, targetPoint.DistanceTo(arc.Center), arc.StartAngle, arc.EndAngle) { Color = 0xFFAAAAAA, Layer = arc.Layer });
                 continue;
             }
             else
             {
                 continue; // Şimdilik desteklemeyenleri silme ama offsetlemesin de
             }

             _ghostEntities.Add(clone);
         }
    }

    /*
       NE: Öteleme (Offset) Vektörü Hesaplayıcı
       NEDEN: Verilen A-B hattına olan en yakın dik izdüşümü bulup, o noktadan farenin mevcut yerine (P) uzanan "vektör"ü offset taşıma vektörü olarak kullanmak için.
    */
    private Vector3D CalculateOffsetVector(Vector3D A, Vector3D B, Vector3D P)
    {
        // 1. Hat vektörü (AB)
        double dx = B.X - A.X;
        double dy = B.Y - A.Y;
        
        // 2. Noktadan A'ya vektör (AP)
        double apx = P.X - A.X;
        double apy = P.Y - A.Y;

        double ab_length_sq = dx * dx + dy * dy;
        
        if (ab_length_sq < 0.000001) 
        {
           // Çizgi çok kısaysa farenin noktasına direkt ötele (Fallback)
           return new Vector3D(P.X - A.X, P.Y - A.Y, 0); 
        }

        // 3. İzdüşüm katsayısı (AP dot AB) / |AB|^2
        double t = (apx * dx + apy * dy) / ab_length_sq;
        
        // 4. Hat üzerindeki izdüşüm noktası (Projected) - Hattın dışına taşsa da doğru hattı uzatarak izdüşüm alırız.
        double projX = A.X + t * dx;
        double projY = A.Y + t * dy;

        // 5. Offset taşıma vektörü: P'den İzdüşüme olan dik uzaklık. Move() komutu P -> Projected mi yoksa Projected -> P mi?
        // Çizgiyi P tarafına o kadar mesafede ötelemek istiyoruz, yani Yön vektörü (P - Projected) olur.
        return new Vector3D(P.X - projX, P.Y - projY, 0);
    }

    public void OnKeyDown(InputKey key) 
    {
        if(key == InputKey.Escape)
            Cancel();
    }

    public void Draw(IRenderContext context)
    {
         if (_currentMousePos.HasValue && _ghostEntities != null && _entitiesToOffset.Count > 0)
         {
             // Bir adet yardımcı dik çizgi çizebiliriz (Rubber band). İlk nesnenin merkezinden P'ye.
             var first = _entitiesToOffset.FirstOrDefault();
             if (first != null)
             {
                  Vector3D basePt = new Vector3D(0,0,0);
                  if (first is LineEntity l) basePt = CalculateProjection(l.StartPoint, l.EndPoint, _currentMousePos.Value);
                  else if (first is PipeEntity p) basePt = CalculateProjection(p.StartPoint, p.EndPoint, _currentMousePos.Value);
                  
                  context.DrawLine(basePt, _currentMousePos.Value, 0xFF888888, 1.0 * context.PixelSize, isDashed: true);
             }

             // Ghosts
             foreach(var ghost in _ghostEntities) ghost.Draw(context);
         }
    }
    
    private Vector3D CalculateProjection(Vector3D A, Vector3D B, Vector3D P)
    {
        double dx = B.X - A.X;
        double dy = B.Y - A.Y;
        double apx = P.X - A.X;
        double apy = P.Y - A.Y;
        double ab_length_sq = dx * dx + dy * dy;
        if (ab_length_sq < 0.000001) return A;
        double t = (apx * dx + apy * dy) / ab_length_sq;
        return new Vector3D(A.X + t * dx, A.Y + t * dy, 0);
    }

    public void Cancel()
    {
        _ghostEntities = null;
        OnCompleted?.Invoke();
    }
}
