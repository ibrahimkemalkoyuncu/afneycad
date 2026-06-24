using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Application.Services;

public class DynamicInputService
{
    public bool Enabled { get; set; } = true;
    public bool ShowCoordinates { get; set; } = true;
    public bool ShowDistance { get; set; } = true;
    public bool ShowAngle { get; set; } = true;

    public DynamicInputData? Calculate(Vector3D? basePoint, Vector3D cursor)
    {
        if (!Enabled) return null;

        var data = new DynamicInputData
        {
            CursorX = cursor.X,
            CursorY = cursor.Y
        };

        if (basePoint != null)
        {
            var dx = cursor.X - basePoint.Value.X;
            var dy = cursor.Y - basePoint.Value.Y;
            data.Distance = Math.Sqrt(dx * dx + dy * dy);
            data.Angle = Math.Atan2(dy, dx) * 180.0 / Math.PI;
            if (data.Angle < 0) data.Angle += 360;
            data.DeltaX = dx;
            data.DeltaY = dy;
            data.HasRelative = true;
        }

        return data;
    }

    public string FormatTooltip(DynamicInputData data)
    {
        if (data.HasRelative && ShowDistance)
        {
            string dist = data.Distance >= 1000
                ? $"{data.Distance / 1000.0:F3} m"
                : $"{data.Distance:F2}";
            return $"{dist}  < {data.Angle:F1}°";
        }

        if (ShowCoordinates)
        {
            return $"X: {data.CursorX:F2}  Y: {data.CursorY:F2}";
        }

        return "";
    }
}

public class DynamicInputData
{
    public double CursorX { get; set; }
    public double CursorY { get; set; }
    public double Distance { get; set; }
    public double Angle { get; set; }
    public double DeltaX { get; set; }
    public double DeltaY { get; set; }
    public bool HasRelative { get; set; }
}
