using System;
using System.Collections.Generic;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Blocks;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Domain.Entities.Basic;

public class BlockReferenceEntity : CadEntity
{
    // NE: Blok Adı
    public string BlockName { get; set; } = string.Empty;
    public Vector3D Position { get; set; } = Vector3D.Zero;
    public double Scale { get; set; } = 1.0;
    public double Rotation { get; set; } = 0.0; // Derece

    // RUNTIME REFERENCE: Performans için blok tanımını (Record) doğrudan tutabiliriz.
    // Serialization (Kaydetme) sırasında bu alanın göz ardı edilmesi gerek.
    // Ancak çizim anında bu olmazsa çizim yapamayız.
    public CadBlockRecord? Definition { get; set; }

    public BlockReferenceEntity() { }
 
    public BlockReferenceEntity(string blockName, Vector3D position)
    {
        BlockName = blockName;
        Position = position;
    }

    private List<CadEntity>? _transformedEntitiesCache;
    private Matrix4x4 _lastMatrix = Matrix4x4.Identity;

    public override void Draw(IRenderContext context)
    {
        if (Definition == null || Definition.Entities.Count == 0) return;

        var matrix = Matrix4x4.TranslationMatrix(Definition.BasePoint.X * -1, Definition.BasePoint.Y * -1, Definition.BasePoint.Z * -1)
                    * Matrix4x4.Scaling(Scale, Scale, Scale)
                    * Matrix4x4.RotationZ(Rotation * System.Math.PI / 180.0)
                    * Matrix4x4.TranslationMatrix(Position.X, Position.Y, Position.Z);

        // Matrix değiştiyse veya cache boşsa güncelle
        if (_transformedEntitiesCache == null || _lastMatrix != matrix)
        {
            _transformedEntitiesCache = new List<CadEntity>();
            foreach (var entity in Definition.Entities)
            {
                var temp = entity.Clone();
                temp.Transform(matrix);
                temp.Color = (this.Color == 0xFFFFFFFF) ? temp.Color : this.Color;
                _transformedEntitiesCache.Add(temp);
            }
            _lastMatrix = matrix;
        }

        foreach (var entity in _transformedEntitiesCache)
        {
            entity.Draw(context);
        }
    }

    public override CadEntity Clone()
    {
        return new BlockReferenceEntity(BlockName, Position)
        {
            Scale = this.Scale,
            Rotation = this.Rotation,
            Definition = this.Definition, // Referansı koru
            Color = this.Color,
            Layer = this.Layer
        };
    }

    public override void Move(Vector3D delta)
    {
        Position += delta;
    }

    public override void Transform(Matrix4x4 matrix)
    {
        Position = matrix.Transform(Position);
        // Scale ve Rotation matristen ayrıştırılabilir ama kompleks.
        // Şimdilik sadece pozisyon.
    }

    protected override CadBoundingBox CalculateBoundingBox()
    {
        // Kutuyu hesaplamak için yine transform lazım
        if (Definition == null || Definition.Entities.Count == 0)
            return new CadBoundingBox(Position, Position);

        var matrix = Matrix4x4.TranslationMatrix(Definition.BasePoint.X * -1, Definition.BasePoint.Y * -1, Definition.BasePoint.Z * -1)
                    * Matrix4x4.Scaling(Scale, Scale, Scale)
                    * Matrix4x4.RotationZ(Rotation * System.Math.PI / 180.0)
                    * Matrix4x4.TranslationMatrix(Position.X, Position.Y, Position.Z);

        Vector3D min = new Vector3D(double.MaxValue, double.MaxValue, double.MaxValue);
        Vector3D max = new Vector3D(double.MinValue, double.MinValue, double.MinValue);

        foreach (var entity in Definition.Entities)
        {
            var box = entity.GetBoundingBox();
            var corners = box.GetCorners();
            foreach (var corner in corners)
            {
                var p = matrix.Transform(corner);
                min = Vector3D.Min(min, p);
                max = Vector3D.Max(max, p);
            }
        }
        return new CadBoundingBox(min, max);
    }
    
    public override IEnumerable<SnapPoint> GetSnapPoints()
    {
        yield return new SnapPoint(Position, SnapPointType.Insertion);
    }

    /*
       NE: Grip Noktaları (GetGripPoints / MoveGripPointAt)
       NEDEN: Önceden hiç override yoktu — bloklar (en sık kullanılan nesne tiplerinden biri)
              grip ile taşınamıyor veya döndürülemiyordu. İki grip: yerleşim noktası (taşıma)
              ve mevcut rotasyon yönünde bir "döndürme tutamacı" (sürükleyince Rotation güncellenir).
    */
    public override IEnumerable<Vector3D> GetGripPoints()
    {
        yield return Position;

        var box = GetBoundingBox();
        double halfExtent = System.Math.Max(box.Max.X - box.Min.X, box.Max.Y - box.Min.Y) / 2.0;
        if (halfExtent < 1e-6) halfExtent = 100;
        double rad = Rotation * System.Math.PI / 180.0;
        yield return new Vector3D(Position.X + System.Math.Cos(rad) * halfExtent, Position.Y + System.Math.Sin(rad) * halfExtent, Position.Z);
    }

    public override void MoveGripPointAt(int index, Vector3D newPosition)
    {
        if (index == 0)
        {
            Position = newPosition;
        }
        else if (index == 1)
        {
            double dx = newPosition.X - Position.X;
            double dy = newPosition.Y - Position.Y;
            if (dx * dx + dy * dy > 1e-9)
                Rotation = System.Math.Atan2(dy, dx) * 180.0 / System.Math.PI;
        }
        base.MoveGripPointAt(index, newPosition);
    }
}
