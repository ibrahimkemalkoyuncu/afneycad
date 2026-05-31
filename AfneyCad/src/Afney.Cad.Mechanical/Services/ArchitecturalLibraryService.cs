using System.Collections.Generic;
using Afney.Cad.Mechanical.Models;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Mimari Kütüphane Servisi (ArchitecturalLibraryService)
   NEDEN: Kolon, döşeme, çatı ve mobilya gibi mimari nesnelerin standart bir katalogunu
          sunmak; kullanıcının hızlı yerleştirme yapabilmesi için.
*/
public class ArchitecturalLibraryService
{
    public class ArchitecturalItem
    {
        public string Id          { get; set; } = "";
        public string NameTR      { get; set; } = "";
        public string Category    { get; set; } = "";
        public ObstacleType Type  { get; set; }
        public double Width       { get; set; }   // mm
        public double Depth       { get; set; }   // mm
        public double Height      { get; set; }   // mm
        public string Description { get; set; } = "";

        // BIM özellikleri
        public double UValue        { get; set; }
        public FireRatingClass FireRating { get; set; } = FireRatingClass.NotRated;
        public int FireResistanceMin { get; set; }
    }

    private static readonly List<ArchitecturalItem> _catalog = [
        // ── KOLONLAR ─────────────────────────────────────────────────────────────
        new() { Id="COL-001", NameTR="Kare Kolon 30×30",    Category="Kolon",   Type=ObstacleType.Column,
                Width=300, Depth=300, Height=3000, Description="Betonarme kare kolon",
                FireRating=FireRatingClass.A1, FireResistanceMin=60 },
        new() { Id="COL-002", NameTR="Kare Kolon 40×40",    Category="Kolon",   Type=ObstacleType.Column,
                Width=400, Depth=400, Height=3000, Description="Betonarme kare kolon",
                FireRating=FireRatingClass.A1, FireResistanceMin=90 },
        new() { Id="COL-003", NameTR="Silindirik Kolon Ø30",Category="Kolon",   Type=ObstacleType.Column,
                Width=300, Depth=300, Height=3000, Description="Yuvarlak beton kolon" },
        new() { Id="COL-004", NameTR="Çelik HEA 200",        Category="Kolon",   Type=ObstacleType.Column,
                Width=200, Depth=200, Height=3000, Description="Çelik yapı kolonu",
                FireRating=FireRatingClass.A1, FireResistanceMin=30 },

        // ── DÖŞEMELER ─────────────────────────────────────────────────────────────
        new() { Id="SLB-001", NameTR="Nervürlü Döşeme 25cm",  Category="Döşeme",  Type=ObstacleType.Slab,
                Width=1000, Depth=1000, Height=250, Description="Nervürlü betonarme plak",
                UValue=1.2, FireRating=FireRatingClass.A1, FireResistanceMin=90 },
        new() { Id="SLB-002", NameTR="Mantar Döşeme 20cm",    Category="Döşeme",  Type=ObstacleType.Slab,
                Width=1000, Depth=1000, Height=200, Description="Mantar plak döşeme",
                UValue=1.4, FireRating=FireRatingClass.A1, FireResistanceMin=60 },
        new() { Id="SLB-003", NameTR="Prefabrik Plak 16cm",   Category="Döşeme",  Type=ObstacleType.Slab,
                Width=1000, Depth=1000, Height=160, Description="Prefabrik betonarme plak",
                UValue=1.8, FireRating=FireRatingClass.A1, FireResistanceMin=60 },

        // ── ÇATILAR ──────────────────────────────────────────────────────────────
        new() { Id="ROF-001", NameTR="Düz Çatı (Yeşil)",      Category="Çatı",    Type=ObstacleType.Roof,
                Width=1000, Depth=1000, Height=400, Description="Yeşil çatı — XPS 10cm",
                UValue=0.25, FireRating=FireRatingClass.B },
        new() { Id="ROF-002", NameTR="Düz Çatı (Gravürlü)",   Category="Çatı",    Type=ObstacleType.Roof,
                Width=1000, Depth=1000, Height=350, Description="Gravürlü çatı — EPS 8cm",
                UValue=0.30 },
        new() { Id="ROF-003", NameTR="Eğimli Çatı 30°",       Category="Çatı",    Type=ObstacleType.Roof,
                Width=1000, Depth=1000, Height=500, Description="Kiremit örtülü ahşap çatı",
                UValue=0.35 },

        // ── MOBİLYA / İÇ MEKAN ────────────────────────────────────────────────────
        new() { Id="FRN-001", NameTR="Ofis Masası 160×80",    Category="Mobilya", Type=ObstacleType.Furniture,
                Width=1600, Depth=800, Height=750, Description="Standart çalışma masası" },
        new() { Id="FRN-002", NameTR="Koltuk 90×90",          Category="Mobilya", Type=ObstacleType.Furniture,
                Width=900, Depth=900, Height=800, Description="Oturma grubu koltuğu" },
        new() { Id="FRN-003", NameTR="Dolap 100×60×200",      Category="Mobilya", Type=ObstacleType.Furniture,
                Width=1000, Depth=600, Height=2000, Description="Duvar dolabı" },
        new() { Id="FRN-004", NameTR="Tuvalet (WC)",          Category="Mobilya", Type=ObstacleType.Furniture,
                Width=360, Depth=680, Height=400, Description="Asma klozet — TS 12813" },
        new() { Id="FRN-005", NameTR="Lavabo 60×45",          Category="Mobilya", Type=ObstacleType.Furniture,
                Width=600, Depth=450, Height=200, Description="Seramik lavabo" },
        new() { Id="FRN-006", NameTR="Banyo Küveti 170×70",   Category="Mobilya", Type=ObstacleType.Furniture,
                Width=1700, Depth=700, Height=600, Description="Emaye çelik küvet" },
        new() { Id="FRN-007", NameTR="Buzdolabı 60×65",       Category="Mobilya", Type=ObstacleType.Furniture,
                Width=600, Depth=650, Height=1800, Description="Mutfak buzdolabı" },
        new() { Id="FRN-008", NameTR="Yatak 160×200",         Category="Mobilya", Type=ObstacleType.Furniture,
                Width=1600, Depth=2000, Height=550, Description="Çift kişilik yatak" },
        new() { Id="FRN-009", NameTR="Merdiven Basamağı",     Category="Mobilya", Type=ObstacleType.Furniture,
                Width=1200, Depth=250, Height=175, Description="Tek basamak — TS EN 14975" },

        // ── KAPLAR & EKIPMAN ──────────────────────────────────────────────────────
        new() { Id="EQP-001", NameTR="Asansör 120×140",       Category="Ekipman", Type=ObstacleType.Column,
                Width=1200, Depth=1400, Height=3000, Description="Bina asansörü — TS EN 81" },
        new() { Id="EQP-002", NameTR="Merdiven Kovası 3×4",   Category="Ekipman", Type=ObstacleType.Column,
                Width=3000, Depth=4000, Height=3000, Description="Yangın merdiveni alanı" },
    ];

    public List<ArchitecturalItem> GetAll() => _catalog;

    public List<ArchitecturalItem> GetByCategory(string category)
        => _catalog.FindAll(i => i.Category.Equals(category, System.StringComparison.OrdinalIgnoreCase));

    public List<string> GetCategories()
        => [.. _catalog.Select(i => i.Category).Distinct().Order()];

    public ArchitecturalItem? GetById(string id)
        => _catalog.Find(i => i.Id == id);

    // Seçilen item'dan ArchitecturalObstacle oluştur
    public ArchitecturalObstacle CreateObstacle(ArchitecturalItem item, Afney.Cad.Geometry.Primitives.Vector3D position)
    {
        double hw = item.Width / 2.0, hd = item.Depth / 2.0;
        return new ArchitecturalObstacle
        {
            Type          = item.Type,
            Name          = item.NameTR,
            Description   = item.Description,
            Height        = item.Height,
            FireRating    = item.FireRating,
            FireResistanceMinutes = item.FireResistanceMin,
            UValueOverride = item.UValue > 0 ? item.UValue : null,
            Boundary = [
                new(position.X - hw, position.Y - hd, position.Z),
                new(position.X + hw, position.Y - hd, position.Z),
                new(position.X + hw, position.Y + hd, position.Z),
                new(position.X - hw, position.Y + hd, position.Z),
            ]
        };
    }
}
