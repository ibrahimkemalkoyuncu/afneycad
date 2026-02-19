using System;
using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Database.Core;
using Afney.Cad.Database.Transactions; 
using Afney.Cad.Database.Transactions.Operations; 
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Commands.BasicCommands;

/*
NE:
Taşıma Komutu (Move).
*/
public class MoveCommand : ICadCommand
{
    private readonly CadDatabase _database;
    private readonly TransactionManager _transactionManager;
    private readonly List<CadEntity> _entitiesToMove;
    
    private Vector3D? _basePoint;
    private Vector3D _currentMousePos;

    public string CommandName => "MOVE";
    public Vector3D? ActivePoint => _basePoint;
    public event Action<string>? OnFeedback;
    public event Action? OnCompleted;

    public MoveCommand(CadDatabase database, TransactionManager transactionManager, IEnumerable<CadEntity> selection)
    {
        _database = database;
        _transactionManager = transactionManager;
        _entitiesToMove = selection.ToList();
    }
    
    /*
       NE: Komutu Başlat (Start)
       NEDEN: Taşıma komutunu aktif ederek kullanıcıdan referans (baz) noktasını istemek için. Eğer seçili nesne yoksa komutu sonlandırır.
    */
    public void Start()
    {
        if (_entitiesToMove.Count == 0)
        {
            OnFeedback?.Invoke("MOVE: Seçili nesne yok. Komut iptal.");
            OnCompleted?.Invoke();
            return;
        }

        OnFeedback?.Invoke("MOVE: Baz noktasını belirtin.");
    }

    /*
       NE: Tıklama Olayı (OnPointerPressed)
       NEDEN: İlk tıklamada referans noktasını belirlemek, ikinci tıklamada ise bu iki nokta arasındaki farkı (Delta) hesaplayarak seçili tüm nesneleri veritabanında toplu olarak taşımak için.
    */
    public void OnPointerPressed(Vector3D point)
    {
        if (_basePoint == null)
        {
            _basePoint = point;
            OnFeedback?.Invoke("MOVE: İkinci noktayı belirtin.");
        }
        else
        {
            // İkinci nokta -> Taşı
            var delta = new Vector3D(point.X - _basePoint.Value.X, point.Y - _basePoint.Value.Y, 0);
            
            var composite = new CompositeOperation("Move Entities");
            foreach (var ent in _entitiesToMove)
            {
                composite.Add(new MoveEntityOperation(ent, delta, _database));
            }

            _transactionManager.Submit(composite);
            OnFeedback?.Invoke("MOVE: Tamamlandı.");
            OnCompleted?.Invoke();
        }
    }

    public void OnPointerMoved(Vector3D point)
    {
         _currentMousePos = point;
    }

    public void OnKeyDown(InputKey key) { }

    /*
       NE: Yardımcı Çizgileri Çiz (Draw)
       NEDEN: Baz noktası ile güncel fare konumu arasına kesikli bir "lastik bant" (rubber band) çizgisi çizerek kullanıcıya oluşacak taşıma mesafesini ve yönünü görselleştirmek için.
    */
    public void Draw(IRenderContext context)
    {
        if (_basePoint != null)
        {
             // Draw displacement vector (Rubber band)
             context.DrawLine(_basePoint.Value, _currentMousePos, 0xFF888888, 1.0, isDashed: true);
        }
    }


    public void Cancel()
    {
        _basePoint = null;
    }
}