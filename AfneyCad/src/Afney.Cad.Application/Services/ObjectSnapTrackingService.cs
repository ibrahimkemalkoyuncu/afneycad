using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Application.Services;

public class ObjectSnapTrackingService
{
    public bool Enabled { get; set; } = false;
    private readonly List<Vector3D> _acquiredPoints = new();
    private const int MaxAcquired = 7;

    public void AcquirePoint(Vector3D snapPoint)
    {
        if (_acquiredPoints.Count >= MaxAcquired)
            _acquiredPoints.RemoveAt(0);

        foreach (var p in _acquiredPoints)
            if (Math.Abs(p.X - snapPoint.X) < 1e-6 && Math.Abs(p.Y - snapPoint.Y) < 1e-6)
                return;

        _acquiredPoints.Add(snapPoint);
    }

    public void ClearAcquired() => _acquiredPoints.Clear();

    public Vector3D? FindAlignment(Vector3D cursor, double tolerance)
    {
        if (!Enabled || _acquiredPoints.Count == 0) return null;

        foreach (var pt in _acquiredPoints)
        {
            if (Math.Abs(cursor.X - pt.X) < tolerance)
                return new Vector3D(pt.X, cursor.Y, 0);

            if (Math.Abs(cursor.Y - pt.Y) < tolerance)
                return new Vector3D(cursor.X, pt.Y, 0);
        }
        return null;
    }

    /*
       NE: Hizalama Çizgilerini Getir (GetTrackingLines)
       NEDEN: Ekrana çizilecek kesikli hizalama çizgilerini üretir — SADECE fare, daha önce
              yakalanmış bir OSNAP noktasının yatay/dikey hizası üzerindeyken (FindAlignment
              ile aynı `tolerance` kullanılarak) bir çizgi döner; aksi halde boş liste.
    */
    public List<(Vector3D From, Vector3D To)> GetTrackingLines(Vector3D cursor, double tolerance, double extent = 10000)
    {
        var lines = new List<(Vector3D, Vector3D)>();
        if (!Enabled) return lines;

        foreach (var pt in _acquiredPoints)
        {
            if (Math.Abs(cursor.X - pt.X) < tolerance)
            {
                lines.Add((new Vector3D(pt.X, pt.Y - extent, 0), new Vector3D(pt.X, pt.Y + extent, 0)));
            }
            if (Math.Abs(cursor.Y - pt.Y) < tolerance)
            {
                lines.Add((new Vector3D(pt.X - extent, pt.Y, 0), new Vector3D(pt.X + extent, pt.Y, 0)));
            }
        }
        return lines;
    }
}
