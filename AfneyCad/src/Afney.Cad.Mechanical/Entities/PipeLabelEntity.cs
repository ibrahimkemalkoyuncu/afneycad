using System;
using System.Collections.Generic;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Mechanical.Entities;

/*
    NE: Akıllı Boru Etiketi (PipeLabelEntity)
    NEDEN: Borunun çap, eğim ve malzeme bilgilerini boru üzerinde dinamik olarak göstermek için.
    
    ÖZELLİKLER:
    - Boruya Bağlılık: Boru seçildiğinde veya taşındığında etiket onunla beraber hareket eder.
    - Otomatik Hizalama: Yazı, borunun açısına göre kendini otomatik döndürür (AutoCAD Align).
    - Canlı Veri: Boru çapı değiştiğinde (AutoSize) etiketteki yazı anında güncellenir.
*/
public class PipeLabelEntity : CadEntity
{
    public Guid TargetPipeId { get; }
    private PipeEntity? _targetPipe;

    public Vector3D Position { get; set; }
    public double Height { get; set; } = 150.0;
    
    public PipeLabelEntity(PipeEntity pipe)
    {
        TargetPipeId = pipe.Id;
        _targetPipe = pipe;
        UpdatePosition();
    }

    public void SyncWithPipe(PipeEntity pipe)
    {
        _targetPipe = pipe;
        UpdatePosition();
    }

    private void UpdatePosition()
    {
        if (_targetPipe == null) return;
        
        // Borunun tam ortasına yerleştir
        var mid = (_targetPipe.StartPoint + _targetPipe.EndPoint) / 2.0;
        
        // Borunun dikini bul (Offset için)
        var dir = (_targetPipe.EndPoint - _targetPipe.StartPoint).Normalize();
        var normal = new Vector3D(-dir.Y, dir.X, 0); 
        
        // Yazıyı borunun biraz üstüne kaydır
        Position = mid + (normal * (Height * 0.8));
    }

    public override void Draw(IRenderContext context)
    {
        if (_targetPipe == null) return;

        string label = GetLabelText();
        double rotation = Math.Atan2(_targetPipe.EndPoint.Y - _targetPipe.StartPoint.Y, 
                                   _targetPipe.EndPoint.X - _targetPipe.StartPoint.X);

        // Yazı her zaman okunabilir yönde olsun (Upside down engelleme)
        if (rotation > Math.PI / 2 || rotation < -Math.PI / 2)
            rotation += Math.PI;

        uint color = IsSelected ? 0xFF00FFFF : 0xFFFFFFFF;
        context.DrawText(label, Position, rotation * 180.0 / Math.PI, Height, color);
    }

    private string GetLabelText()
    {
        if (_targetPipe == null) return "";
        
        string dn = $"Ø{_targetPipe.InnerDiameter:F0}";
        
        // Atık su ise eğim de ekle
        if (_targetPipe.SystemType == Enums.MechanicalSystemType.WasteWater)
        {
            double slopePercent = _targetPipe.Slope * 100.0;
            return $"{dn} - %{slopePercent:F1}";
        }
        
        return dn;
    }

    public override CadEntity Clone()
    {
        // Not: Klonun yeni bir boruya bağlanması gerekebilir.
        var clone = (PipeLabelEntity)this.MemberwiseClone();
        return clone;
    }

    public override void Move(Vector3D delta) => Position += delta;

    public override void Transform(Matrix4x4 matrix)
    {
        Position = matrix.Transform(Position);
    }

    protected override CadBoundingBox CalculateBoundingBox()
    {
        return new CadBoundingBox(Position, Position).Expand(Height * 2);
    }

    public override IEnumerable<SnapPoint> GetSnapPoints()
    {
        yield return new SnapPoint(Position, SnapPointType.Endpoint);
    }
}
