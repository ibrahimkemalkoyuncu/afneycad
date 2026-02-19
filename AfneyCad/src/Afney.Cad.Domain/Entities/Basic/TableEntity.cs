using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Afney.Cad.Domain.Entities.Basic;

/*
    NE: Tablo Nesnesi (TableEntity)
    NEDEN: Metraj listelerini, boru çizelgelerini ve mühendislik hesap sonuçlarını profesyonel bir pafta formatında çizimin içine gömmek için.
    
    NASIL (Mühendislik Modu):
    1. Grid tabanlı bir yapı sunar.
    2. Her hücre (cell) metin, sayı veya formül tutabilir.
    3. AutoCAD "TABLE" nesnesine muadil bir davranış sergiler.
*/
public class TableEntity : CadEntity
{
    public Vector3D Position { get; set; }
    public int Columns { get; set; }
    public int Rows { get; set; }
    public double RowHeight { get; set; } = 300.0; // 30cm standart
    public double ColumnWidth { get; set; } = 1500.0; // 1.5m standart

    private string[,] _data;

    public TableEntity(Vector3D position, int rows, int cols)
    {
        Position = position;
        Rows = rows;
        Columns = cols;
        _data = new string[rows, cols];
    }

    public void SetCell(int row, int col, string value)
    {
        if (row >= 0 && row < Rows && col >= 0 && col < Columns)
        {
            _data[row, col] = value;
        }
    }

    public string GetCell(int row, int col) => _data[row, col] ?? string.Empty;

    public override void Draw(IRenderContext context)
    {
        // 1. ÇERÇEVE VE IZGARA (GRİD) ÇİZİMİ
        double totalWidth = Columns * ColumnWidth;
        double totalHeight = Rows * RowHeight;

        // Dış Çerçeve
        context.DrawLine(Position, Position + new Vector3D(totalWidth, 0, 0), Color, 2.0);
        context.DrawLine(Position, Position - new Vector3D(0, totalHeight, 0), Color, 2.0);

        // Yatay Çizgiler
        for (int i = 0; i <= Rows; i++)
        {
            var start = Position - new Vector3D(0, i * RowHeight, 0);
            var end = start + new Vector3D(totalWidth, 0, 0);
            context.DrawLine(start, end, Color, 1.0);
        }

        // Dikey Çizgiler
        for (int j = 0; j <= Columns; j++)
        {
            var start = Position + new Vector3D(j * ColumnWidth, 0, 0);
            var end = start - new Vector3D(0, totalHeight, 0);
            context.DrawLine(start, end, Color, 1.0);
        }

        // 2. VERİLERİ YERLEŞTİR
        for (int i = 0; i < Rows; i++)
        {
            for (int j = 0; j < Columns; j++)
            {
                var cellPos = Position + new Vector3D(j * ColumnWidth + 50, -(i * RowHeight + RowHeight/2 + 75), 0);
                string text = GetCell(i, j);
                if (!string.IsNullOrEmpty(text))
                {
                    // Mini Text Render (Bunu basitleştirerek çiziyoruz)
                    // Gerçek uygulamada TextEntity.Draw mantığı buraya entegre edilir.
                }
            }
        }
    }

    protected override CadBoundingBox CalculateBoundingBox()
    {
        return new CadBoundingBox(
            Position - new Vector3D(0, Rows * RowHeight, 0),
            Position + new Vector3D(Columns * ColumnWidth, 0, 0)
        );
    }

    public override void Move(Vector3D delta)
    {
        Position += delta;
    }

    public override IEnumerable<SnapPoint> GetSnapPoints()
    {
        yield return new SnapPoint(Position, SnapPointType.Endpoint);
    }

    public override void Transform(Matrix4x4 matrix)
    {
        Position = matrix.Transform(Position);
    }

    public override CadEntity Clone()
    {
        var clone = new TableEntity(Position, Rows, Columns);
        for(int i=0; i<Rows; i++)
            for(int j=0; j<Columns; j++)
                clone.SetCell(i, j, _data[i, j]);
        return clone;
    }
}
