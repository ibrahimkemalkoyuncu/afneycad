using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Mechanical.Entities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Afney.Cad.Application.Services;

// Gelişmiş Properties Panel veri servisi — entity özelliklerini dinamik okuma/yazma
public class EntityPropertyService
{
    private readonly CadDatabase _database;

    public EntityPropertyService(CadDatabase database) => _database = database;

    // Entity'den tüm özellikleri çıkar
    public List<PropertyItem> GetProperties(CadEntity entity)
    {
        var props = new List<PropertyItem>();

        // Genel özellikler (tüm entity'ler)
        props.Add(new PropertyItem("Genel", "ID", entity.Id.ToString()[..8], false));
        props.Add(new PropertyItem("Genel", "Tip", entity.GetType().Name, false));
        props.Add(new PropertyItem("Genel", "Katman", entity.Layer ?? "0", true));
        props.Add(new PropertyItem("Genel", "Renk", $"#{entity.Color:X8}", true));

        var bbox = entity.GetBoundingBox();
        props.Add(new PropertyItem("Geometri", "Merkez X", bbox.Center.X.ToString("F1"), false));
        props.Add(new PropertyItem("Geometri", "Merkez Y", bbox.Center.Y.ToString("F1"), false));
        props.Add(new PropertyItem("Geometri", "Merkez Z", bbox.Center.Z.ToString("F1"), false));

        // Tip bazlı özellikler
        if (entity is LineEntity line)
        {
            props.Add(new PropertyItem("Çizgi", "Başlangıç X", line.StartPoint.X.ToString("F1"), true));
            props.Add(new PropertyItem("Çizgi", "Başlangıç Y", line.StartPoint.Y.ToString("F1"), true));
            props.Add(new PropertyItem("Çizgi", "Bitiş X", line.EndPoint.X.ToString("F1"), true));
            props.Add(new PropertyItem("Çizgi", "Bitiş Y", line.EndPoint.Y.ToString("F1"), true));
            double len = (line.EndPoint - line.StartPoint).Length();
            props.Add(new PropertyItem("Çizgi", "Uzunluk", $"{len:F1} mm ({len / 1000:F3} m)", false));
            double angle = Math.Atan2(line.EndPoint.Y - line.StartPoint.Y, line.EndPoint.X - line.StartPoint.X) * 180 / Math.PI;
            props.Add(new PropertyItem("Çizgi", "Açı", $"{angle:F1}°", false));
        }
        else if (entity is CircleEntity circle)
        {
            props.Add(new PropertyItem("Daire", "Merkez X", circle.Center.X.ToString("F1"), true));
            props.Add(new PropertyItem("Daire", "Merkez Y", circle.Center.Y.ToString("F1"), true));
            props.Add(new PropertyItem("Daire", "Yarıçap", $"{circle.Radius:F1} mm", true));
            props.Add(new PropertyItem("Daire", "Çap", $"{circle.Radius * 2:F1} mm", false));
            props.Add(new PropertyItem("Daire", "Çevre", $"{2 * Math.PI * circle.Radius:F1} mm", false));
            props.Add(new PropertyItem("Daire", "Alan", $"{Math.PI * circle.Radius * circle.Radius / 1e6:F4} m²", false));
        }
        else if (entity is TextEntity text)
        {
            props.Add(new PropertyItem("Metin", "İçerik", text.Text, true));
            props.Add(new PropertyItem("Metin", "Konum X", text.Position.X.ToString("F1"), true));
            props.Add(new PropertyItem("Metin", "Konum Y", text.Position.Y.ToString("F1"), true));
            props.Add(new PropertyItem("Metin", "Yükseklik", $"{text.Height:F1} mm", true));
            props.Add(new PropertyItem("Metin", "Rotasyon", $"{text.Rotation:F1}°", true));
            props.Add(new PropertyItem("Metin", "Stil", text.Style, true));
        }
        else if (entity is PipeEntity pipe)
        {
            props.Add(new PropertyItem("Boru", "Sistem", pipe.SystemType.ToString(), true));
            props.Add(new PropertyItem("Boru", "Malzeme", pipe.PipeMaterialType.ToString(), true));
            props.Add(new PropertyItem("Boru", "İç Çap", $"DN{pipe.InnerDiameter:F0} mm", true));
            props.Add(new PropertyItem("Boru", "Uzunluk", $"{pipe.GetLength() / 1000.0:F2} m", false));
            props.Add(new PropertyItem("Boru", "Debi", $"{pipe.FlowRate:F3} m³/h", false));
            props.Add(new PropertyItem("Boru", "Hız", $"{pipe.Velocity:F2} m/s", false));
            props.Add(new PropertyItem("Boru", "Basınç Kaybı", $"{pipe.PressureDrop:F4} mSS", false));
            props.Add(new PropertyItem("Boru", "Eğim", $"%{pipe.Slope * 100:F1}", true));
            props.Add(new PropertyItem("Boru", "FU Toplam", $"{pipe.TotalFixtureUnits:F1}", false));
            props.Add(new PropertyItem("Boru", "Sıcaklık", $"{pipe.Temperature:F0} °C", true));
            props.Add(new PropertyItem("Boru", "Hız Aşımı", pipe.HasHydraulicViolation ? "EVET" : "Hayır", false));
            props.Add(new PropertyItem("Boru", "Çap Kilitli", pipe.IsSizeLocked ? "Evet" : "Hayır", true));
        }
        else if (entity is SanitaryFixtureEntity fixture)
        {
            props.Add(new PropertyItem("Cihaz", "Tip", fixture.FixtureType, true));
            props.Add(new PropertyItem("Cihaz", "Yük Birimi", $"{fixture.FixtureUnit:F1} FU", true));
            props.Add(new PropertyItem("Cihaz", "Konum X", fixture.Position.X.ToString("F1"), true));
            props.Add(new PropertyItem("Cihaz", "Konum Y", fixture.Position.Y.ToString("F1"), true));
            props.Add(new PropertyItem("Cihaz", "Rotasyon", $"{fixture.Rotation:F1}°", true));
        }
        else if (entity is LwPolylineEntity poly)
        {
            props.Add(new PropertyItem("Polyline", "Nokta Sayısı", poly.Vertices.Count.ToString(), false));
            props.Add(new PropertyItem("Polyline", "Kapalı", poly.IsClosed ? "Evet" : "Hayır", true));
            double perimeter = 0;
            for (int i = 0; i < poly.Vertices.Count - 1; i++)
                perimeter += (poly.Vertices[i + 1] - poly.Vertices[i]).Length();
            if (poly.IsClosed && poly.Vertices.Count > 1)
                perimeter += (poly.Vertices[0] - poly.Vertices[^1]).Length();
            props.Add(new PropertyItem("Polyline", "Çevre", $"{perimeter:F1} mm ({perimeter / 1000:F3} m)", false));
        }

        return props;
    }

    // Özellik değerini güncelle
    public bool SetProperty(CadEntity entity, string propertyName, string value)
    {
        try
        {
            switch (propertyName)
            {
                case "Katman": entity.Layer = value; break;
                case "Renk" when uint.TryParse(value.TrimStart('#'), System.Globalization.NumberStyles.HexNumber, null, out uint color):
                    entity.Color = color; break;
                default: return false;
            }
            _database.UpdateEntity(entity);
            return true;
        }
        catch { return false; }
    }

    // Çoklu seçim özeti
    public SelectionSummary GetSelectionSummary(IEnumerable<CadEntity> entities)
    {
        var list = entities.ToList();
        return new SelectionSummary
        {
            TotalCount = list.Count,
            TypeCounts = list.GroupBy(e => e.GetType().Name).ToDictionary(g => g.Key, g => g.Count()),
            LayerCounts = list.GroupBy(e => e.Layer ?? "0").ToDictionary(g => g.Key, g => g.Count()),
            TotalLength = list.OfType<LineEntity>().Sum(l => (l.EndPoint - l.StartPoint).Length()) / 1000.0,
            TotalPipeLength = list.OfType<PipeEntity>().Sum(p => p.GetLength()) / 1000.0,
        };
    }
}

public class PropertyItem
{
    public string Category { get; set; }
    public string Name { get; set; }
    public string Value { get; set; }
    public bool IsEditable { get; set; }

    public PropertyItem(string category, string name, string value, bool editable)
    {
        Category = category; Name = name; Value = value; IsEditable = editable;
    }
}

public class SelectionSummary
{
    public int TotalCount { get; set; }
    public Dictionary<string, int> TypeCounts { get; set; } = new();
    public Dictionary<string, int> LayerCounts { get; set; } = new();
    public double TotalLength { get; set; }
    public double TotalPipeLength { get; set; }
}
