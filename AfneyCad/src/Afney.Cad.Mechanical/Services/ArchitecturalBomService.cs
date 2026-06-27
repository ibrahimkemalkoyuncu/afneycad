using System.Text;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Entities;

namespace Afney.Cad.Mechanical.Services;

public class ArchBomItem
{
    public string Category    { get; set; } = "";
    public string Description { get; set; } = "";
    public string Size        { get; set; } = "";
    public double Quantity    { get; set; }
    public string Unit        { get; set; } = "";
    public double Area        { get; set; }
    public double Volume      { get; set; }
}

public class ArchBomResult
{
    public List<ArchBomItem> Items { get; set; } = new();
    public int WallCount    { get; set; }
    public int ColumnCount  { get; set; }
    public int BeamCount    { get; set; }
    public int DoorCount    { get; set; }
    public int WindowCount  { get; set; }
    public int RoomCount    { get; set; }
    public double TotalWallLengthM { get; set; }
    public double TotalWallAreaM2  { get; set; }
    public double TotalRoomAreaM2  { get; set; }
}

public class ArchitecturalBomService
{
    private readonly CadDatabase _database;

    public ArchitecturalBomService(CadDatabase database) { _database = database; }

    public ArchBomResult Generate()
    {
        var r = new ArchBomResult();
        var entities = _database.GetAllEntities().ToList();

        var walls = entities.OfType<WallEntity>().ToList();
        foreach (var g in walls.GroupBy(w => new { w.Material, T = (int)w.ThicknessMm }))
        {
            double len = g.Sum(w => w.GetLengthM());
            double area = g.Sum(w => w.GetAreaM2());
            double vol = g.Sum(w => w.GetVolumeM3());
            r.Items.Add(new ArchBomItem { Category = "Duvar", Description = g.First().GetMaterialText(), Size = $"{g.Key.T} mm", Quantity = g.Count(), Unit = "adet", Area = area, Volume = vol });
            r.TotalWallLengthM += len;
            r.TotalWallAreaM2 += area;
        }
        r.WallCount = walls.Count;

        var columns = entities.OfType<ColumnEntity>().ToList();
        foreach (var g in columns.GroupBy(c => new { c.Shape, c.Material, S = c.GetSizeText() }))
        {
            double vol = g.Sum(c => c.GetVolumeM3());
            r.Items.Add(new ArchBomItem { Category = "Kolon", Description = $"{g.Key.Material} {g.Key.Shape}", Size = g.Key.S, Quantity = g.Count(), Unit = "adet", Volume = vol });
        }
        r.ColumnCount = columns.Count;

        var beams = entities.OfType<BeamEntity>().ToList();
        foreach (var g in beams.GroupBy(b => new { b.Material, S = b.GetSizeText() }))
        {
            double len = g.Sum(b => b.GetLengthM());
            double vol = g.Sum(b => b.GetVolumeM3());
            r.Items.Add(new ArchBomItem { Category = "Kiris", Description = $"{g.Key.Material}", Size = g.Key.S, Quantity = g.Count(), Unit = "adet", Area = len, Volume = vol });
        }
        r.BeamCount = beams.Count;

        var doors = entities.OfType<DoorEntity>().ToList();
        foreach (var g in doors.GroupBy(d => new { d.Type, W = (int)d.WidthMm, H = (int)d.HeightMm }))
        {
            r.Items.Add(new ArchBomItem { Category = "Kapi", Description = g.First().GetTypeText(), Size = $"{g.Key.W}x{g.Key.H}", Quantity = g.Count(), Unit = "adet" });
        }
        r.DoorCount = doors.Count;

        var windows = entities.OfType<WindowEntity>().ToList();
        foreach (var g in windows.GroupBy(w => new { w.Type, W = (int)w.WidthMm, H = (int)w.HeightMm }))
        {
            double area = g.Sum(w => (w.WidthMm / 1000.0) * (w.HeightMm / 1000.0));
            r.Items.Add(new ArchBomItem { Category = "Pencere", Description = g.First().GetTypeText(), Size = $"{g.Key.W}x{g.Key.H}", Quantity = g.Count(), Unit = "adet", Area = area });
        }
        r.WindowCount = windows.Count;

        var rooms = entities.OfType<RoomEntity>().ToList();
        foreach (var room in rooms)
        {
            r.Items.Add(new ArchBomItem { Category = "Mahal", Description = room.Name ?? "Oda", Size = "", Quantity = 1, Unit = "adet", Area = room.Area / 1_000_000.0 });
            r.TotalRoomAreaM2 += room.Area / 1_000_000.0;
        }
        var mahals = entities.OfType<MahalEntity>().ToList();
        foreach (var m in mahals)
        {
            r.Items.Add(new ArchBomItem { Category = "Mahal", Description = m.MahalName, Size = m.MahalType, Quantity = 1, Unit = "adet", Area = m.Area });
            r.TotalRoomAreaM2 += m.Area;
        }
        r.RoomCount = rooms.Count + mahals.Count;

        return r;
    }

    public string ExportToHtml(ArchBomResult bom, string projectName = "")
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'/><title>Mimari Metraj</title>");
        sb.AppendLine("<style>body{font-family:'Segoe UI',sans-serif;margin:30px}h1{color:#FF9800;border-bottom:2px solid #FF9800;padding-bottom:6px}");
        sb.AppendLine("table{border-collapse:collapse;width:100%;margin:12px 0}th,td{border:1px solid #CCC;padding:6px 10px;font-size:12px;text-align:left}th{background:#FFF3E0}");
        sb.AppendLine(".summary{display:flex;gap:16px;flex-wrap:wrap;margin:15px 0}.card{background:#FFF8E1;border-radius:6px;padding:12px 18px;min-width:100px}");
        sb.AppendLine(".card .val{font-size:20px;font-weight:bold;color:#E65100}</style></head><body>");

        sb.AppendLine($"<h1>MİMARİ METRAJ TABLOSU</h1>");
        if (!string.IsNullOrEmpty(projectName)) sb.AppendLine($"<p><b>Proje:</b> {projectName} | <b>Tarih:</b> {DateTime.Now:dd.MM.yyyy}</p>");

        sb.AppendLine("<div class='summary'>");
        sb.AppendLine($"<div class='card'><div>Duvar</div><div class='val'>{bom.WallCount} adet</div><div>{bom.TotalWallLengthM:F1} m | {bom.TotalWallAreaM2:F1} m2</div></div>");
        sb.AppendLine($"<div class='card'><div>Kolon</div><div class='val'>{bom.ColumnCount} adet</div></div>");
        sb.AppendLine($"<div class='card'><div>Kiris</div><div class='val'>{bom.BeamCount} adet</div></div>");
        sb.AppendLine($"<div class='card'><div>Kapi</div><div class='val'>{bom.DoorCount} adet</div></div>");
        sb.AppendLine($"<div class='card'><div>Pencere</div><div class='val'>{bom.WindowCount} adet</div></div>");
        sb.AppendLine($"<div class='card'><div>Mahal</div><div class='val'>{bom.RoomCount} adet</div><div>{bom.TotalRoomAreaM2:F1} m2</div></div>");
        sb.AppendLine("</div>");

        sb.AppendLine("<table><tr><th>Kategori</th><th>Aciklama</th><th>Boyut</th><th>Adet</th><th>Alan (m2)</th><th>Hacim (m3)</th></tr>");
        foreach (var item in bom.Items)
            sb.AppendLine($"<tr><td>{item.Category}</td><td>{item.Description}</td><td>{item.Size}</td><td>{item.Quantity}</td><td>{(item.Area > 0 ? item.Area.ToString("F2") : "-")}</td><td>{(item.Volume > 0 ? item.Volume.ToString("F3") : "-")}</td></tr>");
        sb.AppendLine("</table>");

        sb.AppendLine($"<p style='font-size:10px;color:#999'>AfneyCAD v4.0.0 — Mimari Metraj | {DateTime.Now:dd.MM.yyyy}</p></body></html>");
        return sb.ToString();
    }
}
