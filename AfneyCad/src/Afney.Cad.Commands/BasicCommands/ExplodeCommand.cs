using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Database.Core;
using Afney.Cad.Database.Transactions;
using Afney.Cad.Database.Transactions.Operations;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities.Annotation;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Commands.BasicCommands;

/*
   NE: Patlat (Explode) Komutu
   NEDEN: Birleşik nesneleri (BlockReference, LwPolyline, Hatch, Dimension, Spline) temel
          bileşenlerine (Line/Text/Polyline) ayırmak için.
   NOT: Önceden sadece BlockReference ve LwPolyline destekleniyordu.
*/
public class ExplodeCommand : ICadCommand
{
    private readonly CadDatabase _database;
    private readonly TransactionManager _transactionManager;
    private readonly List<CadEntity> _selectedEntities;

    public string CommandName => "EXPLODE";
    public Vector3D? ActivePoint => null;

    public event Action<string>? OnFeedback;
    public event Action? OnCompleted;

    public ExplodeCommand(CadDatabase database, TransactionManager transactionManager, IEnumerable<CadEntity> selection)
    {
        _database = database;
        _transactionManager = transactionManager;
        _selectedEntities = selection.ToList();
    }

    public void Start()
    {
        if (_selectedEntities.Count == 0)
        {
            OnFeedback?.Invoke("EXPLODE: Patlatılacak nesneleri seçin. İşlem iptal edildi.");
            OnCompleted?.Invoke();
            return;
        }

        int explodedCount = 0;
        var composite = new CompositeOperation("Explode Entities");

        foreach (var entity in _selectedEntities)
        {
            if (entity is BlockReferenceEntity blkRef)
            {
                if (blkRef.Definition != null && blkRef.Definition.Entities.Count > 0)
                {
                    // BlockReference'daki dönüşüm matrisini (Scale, Rotate, Translate) hesapla
                    var matrix = Matrix4x4.TranslationMatrix(blkRef.Definition.BasePoint.X * -1, blkRef.Definition.BasePoint.Y * -1, blkRef.Definition.BasePoint.Z * -1)
                                 * Matrix4x4.Scaling(blkRef.Scale, blkRef.Scale, blkRef.Scale)
                                 * Matrix4x4.RotationZ(blkRef.Rotation * System.Math.PI / 180.0)
                                 * Matrix4x4.TranslationMatrix(blkRef.Position.X, blkRef.Position.Y, blkRef.Position.Z);

                    foreach (var subEnt in blkRef.Definition.Entities)
                    {
                        var clone = subEnt.Clone();
                        clone.Transform(matrix);
                        // Eğer bloğun kendi rengi Default (White) değilse alt bileşenlere miras bırakılabilir.
                        if (blkRef.Color != 0xFFFFFFFF) clone.Color = blkRef.Color;
                        clone.Layer = blkRef.Layer;
                        
                        composite.Add(new AddEntityOperation(_database, clone));
                    }
                    composite.Add(new RemoveEntityOperation(_database, blkRef));
                    explodedCount++;
                }
            }
            else if (entity is LwPolylineEntity polyline)
            {
                var verts = polyline.Vertices.ToList();
                if (verts.Count > 1)
                {
                    for (int i = 0; i < verts.Count - 1; i++)
                    {
                        var line = new LineEntity(verts[i], verts[i + 1])
                        {
                            Color = polyline.Color,
                            Layer = polyline.Layer
                        };
                        composite.Add(new AddEntityOperation(_database, line));
                    }

                    // Kapalı polyline ise son noktayı ilk noktaya bağla
                    if (polyline.IsClosed && verts.Count > 2)
                    {
                         var line = new LineEntity(verts[^1], verts[0])
                        {
                            Color = polyline.Color,
                            Layer = polyline.Layer
                        };
                        composite.Add(new AddEntityOperation(_database, line));
                    }
                    composite.Add(new RemoveEntityOperation(_database, polyline));
                    explodedCount++;
                }
            }
            else if (entity is HatchEntity hatch)
            {
                var verts = hatch.BoundaryVertices;
                if (verts.Count > 1)
                {
                    // Sınır her zaman kapalı bir poligon (3+ köşe) — son köşeden ilkine dönen
                    // kenar dahil. 2 köşeli dejenere durumda tek çizgi (döngü olmadan) yeterli.
                    int segCount = verts.Count >= 3 ? verts.Count : 1;
                    for (int i = 0; i < segCount; i++)
                    {
                        var a = verts[i];
                        var b = verts[(i + 1) % verts.Count];
                        composite.Add(new AddEntityOperation(_database, new LineEntity(a, b) { Color = hatch.Color, Layer = hatch.Layer }));
                    }
                    composite.Add(new RemoveEntityOperation(_database, hatch));
                    explodedCount++;
                }
            }
            else if (entity is DimensionEntity dim)
            {
                var pieces = dim.ExplodeToBasicEntities();
                if (pieces.Count > 0)
                {
                    foreach (var piece in pieces)
                        composite.Add(new AddEntityOperation(_database, piece));
                    composite.Add(new RemoveEntityOperation(_database, dim));
                    explodedCount++;
                }
            }
            else if (entity is SplineEntity spline)
            {
                var points = spline.Tessellate();
                if (points.Count > 1)
                {
                    composite.Add(new AddEntityOperation(_database, new LwPolylineEntity(points, isClosed: false) { Color = spline.Color, Layer = spline.Layer }));
                    composite.Add(new RemoveEntityOperation(_database, spline));
                    explodedCount++;
                }
            }
        }

        if (explodedCount > 0)
        {
            _transactionManager.Submit(composite);
            OnFeedback?.Invoke($"EXPLODE: {explodedCount} adet birleşik nesne temel bileşenlerine ayrıldı.");
        }
        else
        {
            OnFeedback?.Invoke("EXPLODE: Seçilen nesneler patlatılabilir yapıda değil (Block/Polyline bulunamadı).");
        }

        OnCompleted?.Invoke();
    }

    public void OnPointerPressed(Vector3D point) { }
    public void OnPointerMoved(Vector3D point) { }
    public void OnKeyDown(InputKey key) { }
    public void Cancel() { OnCompleted?.Invoke(); }
    public void Draw(IRenderContext context) { }
}
