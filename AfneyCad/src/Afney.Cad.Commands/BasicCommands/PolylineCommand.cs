using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Database.Core;
using Afney.Cad.Database.Transactions; 
using Afney.Cad.Database.Transactions.Operations; 
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Commands.BasicCommands;

public class PolylineCommand : ICadCommand
{
    private readonly CadDatabase _database;
    private readonly TransactionManager _transactionManager;
    private readonly List<Vector3D> _vertices = new();
    private LwPolylineEntity? _ghostPoly;
    private LineEntity? _rubberBand;

    public string CommandName => "POLYLINE";
    public Vector3D? ActivePoint => _vertices.Count > 0 ? _vertices[^1] : null;
    public event Action<string>? OnFeedback;
    public event Action? OnCompleted;

    public PolylineCommand(CadDatabase database, TransactionManager transactionManager)
    {
        _database = database;
        _transactionManager = transactionManager;
    }

    public void Start()
    {
        OnFeedback?.Invoke("POLYLINE: Başlangıç noktasını belirtin.");
    }

    /*
       NE: Tıklama Olayı (OnPointerPressed)
       NEDEN: Her tıklamada polylinelisteye yeni bir köşe (vertex) ekleyerek hattı uzatmak ve her adımda ghost çizimini güncellemek için.
    */
    public void OnPointerPressed(Vector3D point)
    {
        _vertices.Add(point);

        if (_vertices.Count == 1)
        {
            // İlk nokta
            OnFeedback?.Invoke("POLYLINE: Sonraki noktayı belirtin.");
        }
        else
        {
            // Ara noktalar -> Ghost Polyline güncelle
            UpdateGhost();
            OnFeedback?.Invoke("POLYLINE: Sonraki noktayı belirtin (Bitirmek için ENTER).");
        }
    }

    public void OnPointerMoved(Vector3D point)
    {
        if (_vertices.Count > 0)
        {
             // Rubber band from last vertex to mouse
             var last = _vertices.Last();
             _rubberBand = new LineEntity(last, point) { Color = 0xFF888888 };
        }
    }
    
    public void OnKeyDown(InputKey key)
    {
        if (key == InputKey.Enter || key == InputKey.Space)
        {
            Finish();
        }
    }

    private void UpdateGhost()
    {
        if (_vertices.Count > 1)
        {
            _ghostPoly = new LwPolylineEntity(new List<Vector3D>(_vertices)) { Color = 0xFFAAAAAA };
        }
    }

    public void Draw(IRenderContext context)
    {
        if (_ghostPoly != null) _ghostPoly.Draw(context);
        if (_rubberBand != null) _rubberBand.Draw(context);
    }

    /*
       NE: Bitir (Finish)
       NEDEN: Kullanıcı onay verdiğinde (Enter/Space), toplanan tüm köşe noktalarını kullanarak kalıcı polyline nesnesini oluşturmak ve veritabanına eklemek için.
    */
    private void Finish()
    {
        if (_vertices.Count >= 2)
        {
            var poly = new LwPolylineEntity(_vertices) 
            { 
                 Layer = _database.ActiveLayerName,
                 Color = _database.GetLayer(_database.ActiveLayerName)?.Color ?? 0xFFFFFFFF
            };
            _transactionManager.Submit(new AddEntityOperation(_database, poly));
        }
        
        _vertices.Clear();
        _ghostPoly = null;
        _rubberBand = null;
        
        OnFeedback?.Invoke("POLYLINE: Tamamlandı.");
        OnCompleted?.Invoke();
    }

    public void Cancel()
    {
        _vertices.Clear();
        _ghostPoly = null;
        _rubberBand = null;
    }
}
