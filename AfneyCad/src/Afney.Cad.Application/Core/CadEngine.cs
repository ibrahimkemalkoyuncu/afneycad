using System;
using System.Linq;
using System.Collections.Generic;
using Afney.Cad.Commands.Engine;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Render;
using Afney.Cad.SpatialIndex.Core;
using Afney.Cad.Database.Transactions; 
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical;
using Afney.Cad.Domain.Entities.Basic;
using Serilog;

namespace Afney.Cad.Application.Core;

/*
   NE: Merkezi CAD Motoru (CadEngine)
   NEDEN: Tüm CAD platformunun beyni olarak Rendering, Database, Commands ve Mechanical Kernel modüllerini orkestre eder.

   MÜHENDİSLİK DETAYI (Kemal):
   - SINGLETON PATTERN: Uygulama genelinde tek bir işlemci motoru üzerinden veri tutarlılığı sağlar.
   - ORKESTRASYON: Veritabanı (Database), Komut Yönetimi (CommandManager), İşlem Geçmişi (Undo/Redo) ve Mekanik Zeka (MechanicalKernel) arasındaki iletişimi yönetir.
   - RENDER DÖNGÜSÜ: Görüntüleme alanındaki nesneleri Render metoduyla donanım hızlandırmalı olarak çizdirir.
   - MEKANSAL İNDEKS (SpatialIndex): QuadTree algoritması kullanarak binlerce nesne içinden hızlı seçim (Pick) ve kenetleme (Snap) imkanı sunar.
*/
public class CadEngine
{
    private static CadEngine? _instance;
    public static CadEngine Instance => _instance ??= new CadEngine();

    // Alt Sistemler
    public CadDatabase Database { get; private set; } = null!;
    public CommandManager CommandManager { get; private set; } = null!;
    public TransactionManager TransactionManager { get; private set; } = null!;
    public QuadTree SpatialIndex { get; private set; } = null!;
    
    // Modüller
    public MechanicalKernel MechanicalKernel { get; private set; } = null!; // Mete Bey: Eklendi

    private readonly List<CadEntity> _selectedEntities = new();
    public IReadOnlyList<CadEntity> SelectedEntities => _selectedEntities;

    public event Action? SelectionChanged;

    public CadEngine()
    {
        Initialize();
    }

    // Selection API
    /*
       NE: Seçimi Temizle (ClearSelection)
       NEDEN: Mevcut seçili tüm nesneleri listeden çıkararak boş bir seçim durumu oluşturmak için.
    */
    public void ClearSelection()
    {
        if (_selectedEntities.Count > 0)
        {
            _selectedEntities.Clear();
            SelectionChanged?.Invoke();
        }
    }

    /*
       NE: Nesne Seç (Select)
       NEDEN: Verilen bir CAD nesnesini seçili hale getirmek, gerekirse mevcut seçimi sıfırlamak veya üzerine eklemek için.
    */
    public void Select(CadEntity entity, bool addToSelection = false)
    {
        if(!addToSelection)
        {
            if (_selectedEntities.Count == 1 && _selectedEntities[0] == entity) return; // Zaten sadece bu seçili
            _selectedEntities.Clear();
        }

        if (!_selectedEntities.Contains(entity))
        {
            _selectedEntities.Add(entity);
            SelectionChanged?.Invoke();
        }
    }

    /*
       NE: Seçimi Kaldır (Deselect)
       NEDEN: Belirli bir nesneyi seçili nesneler listesinden çıkarmak için.
    */
    public void Deselect(CadEntity entity)
    {
        if (_selectedEntities.Remove(entity))
        {
            SelectionChanged?.Invoke();
        }
    }

    /*
       NE: Seçimi Tersine Çev (ToggleSelection)
       NEDEN: Bir nesne seçiliyse seçimini kaldırmak, değilse seçili nesneler listesine eklemek (CTRL+Click davranışı) için.
    */
    public void ToggleSelection(CadEntity entity)
    {
        if (_selectedEntities.Contains(entity))
            Deselect(entity);
        else
            Select(entity, addToSelection: true);
    }

    /*
       NE: Motoru Başlat (Initialize)
       NEDEN: Veritabanı, işlem yöneticisi, uzamsal indeks ve mekanik çekirdek gibi tüm alt sistemleri oluşturup aralarındaki event bağlarını kurmak için.
    */
    private void Initialize()
    {
        // 1. Veritabanını Hazırla
        Database = new CadDatabase();

        // 2. Transaction Manager
        TransactionManager = new TransactionManager();

        // 3. Spatial Index
        var worldBounds = new CadBoundingBox(new Vector3D(-10000, -10000, 0), new Vector3D(10000, 10000, 0));
        SpatialIndex = new QuadTree(worldBounds);

        // 4. Command Manager
        CommandManager = new CommandManager();

        // 5. Mechanical Kernel Başlat (Mete Bey İstegi)
        MechanicalKernel = new MechanicalKernel();

        // Event Abonelikleri
        // Database değişikliklerini Spatial Index'e yansıt
        Database.EntityAdded += (entity) => SpatialIndex.Insert(entity);
        Database.EntityRemoved += (entity) => SpatialIndex.Remove(entity);

        // Database değişikliklerini Mechanical Kernel'e (Topology Graph) yansıt
        Database.EntityAdded += MechanicalKernel.OnEntityAddedToDatabase;
        Database.EntityRemoved += MechanicalKernel.OnEntityRemovedFromDatabase;
    }


    // UI'dan gelen 'Update'
    /*
       NE: Sistem GÃ¼ncelleme (Update)
       NEDEN: Animasyonlar, zamanlanmÄ±ÅŸ gÃ¶revler ve dinamik UI bileÅŸenlerinin periyodik gÃ¼ncellenmesini saÄŸlamak iÃ§in.
    */
    public void Update()
    {
        // Animasyonlar
    }

    /*
       NE: Nesne Seçimi Sorgusu (Pick)
       NEDEN: Ekranda tıklanan bir noktadaki tolerans dahilinde kalan tüm CAD nesnelerini uzamsal indeksi kullanarak saptamak için.
    */
    public List<CadEntity> Pick(Vector3D location, double tolerance)
    {
        var pickBox = new CadBoundingBox(
            new Vector3D(location.X - tolerance, location.Y - tolerance, 0),
            new Vector3D(location.X + tolerance, location.Y + tolerance, 0)
        );
                                                        
        var candidates = new HashSet<CadEntity>();
        SpatialIndex.QueryRange(pickBox, candidates);
        return candidates.ToList();
    }

    /*
       NE: Snap Noktası Bul (FindSnapPoint)
       NEDEN: Fare imlecinin yakınıdaki nesne köşelerini, merkezlerini veya özel tutma noktalarını milimetrik hassasiyetle yakalamak için.
    */
    public SnapPoint? FindSnapPoint(Vector3D cursor, double tolerance)
    {
        // 1. Adayları bul
        var candidates = Pick(cursor, tolerance * 2); // Biraz daha geniş arayabiliriz

        SnapPoint? bestSnap = null;
        double minDistance = tolerance; // Başlangıç toleransı

        foreach (var entity in candidates)
        {
             // Görünürlük kontrolü
             var layer = Database.GetLayer(entity.Layer);
             if (layer != null && (!layer.IsVisible || layer.IsLocked)) continue;

             var snapPoints = entity.GetSnapPoints();
             foreach (var sp in snapPoints)
             {
                 double dist = System.Math.Sqrt(System.Math.Pow(sp.Position.X - cursor.X, 2) + System.Math.Pow(sp.Position.Y - cursor.Y, 2)); // Basit 2D
                 if (dist < minDistance)
                 {
                     minDistance = dist;
                     bestSnap = sp;
                 }
             }
        }

        return bestSnap;
    }

    /*
       NE: Sahneyi Çiz (Render)
       NEDEN: Veritabanındaki nesneleri, aktif komut hayaletlerini ve snap noktalarını birleştirerek ekrana basar.
       MÜHENDİSLİK DETAYI: 
       - Batching: Aynı katman ve renkteki çizgiler tek bir emirle (DrawLines) çizilir.
       - Culling: Sadece ekranda görünen (visibleBounds) nesneler işleme alınır.
    */
    public void Render(IRenderContext context, CadBoundingBox visibleBounds, double pixelSizeInWorldUnits, SnapPoint? activeSnap = null)
    {
        // 0. Arkaplan & Grid
        /*
           NE: Izgara ve Koordinat Sistemini Ã‡iz
           NEDEN: Referans çizgilerini ve başlangıç noktasını render döngüsünün en başında zemine çizmek için.
        */
        DrawGrid(context, visibleBounds, pixelSizeInWorldUnits);
        DrawWCS(context, pixelSizeInWorldUnits);

        // 1. Görünenleri Bul (Culling)
        // Not: SpatialIndex Query Range kullanılarak sadece ekrandakiler alınır.
        var candidates = new HashSet<CadEntity>();
        SpatialIndex.QueryRange(visibleBounds, candidates);
        
        // 1.1. BATCHING LOGIC (Mete Bey: Hız İçin Grupla)
        var lineGroups = candidates.OfType<Afney.Cad.Domain.Entities.Basic.LineEntity>()
            .GroupBy(l => new { l.Layer, l.Color, l.Linetype, ((Afney.Cad.Domain.Abstractions.CadEntity)l).IsDashed });

        foreach (var group in lineGroups)
        {
            var layer = Database.GetLayer(group.Key.Layer);
            if (layer != null && !layer.IsVisible) continue;

            var segments = group.Select(l => (l.StartPoint, l.EndPoint));
            context.DrawLines(segments, group.Key.Color, 1.0 * pixelSizeInWorldUnits, group.Key.Linetype, group.Key.IsDashed);
        }

        // 1.2. Diğer Nesneler (Circle, Arc, Mechanical Entities)
        foreach (var entity in candidates)
        {
            if (entity is Afney.Cad.Domain.Entities.Basic.LineEntity) continue; // Zaten çizdik

            var layer = Database.GetLayer(entity.Layer);
            if (layer != null && !layer.IsVisible) continue;
            
            entity.Draw(context);
        }

        // 1.5. Seçili Nesneleri Vurgula (Highlight)
        foreach(var sel in SelectedEntities)
        {
            var box = sel.GetBoundingBox();
            uint color = 0xFF00ADEE; // CAD Mavi
            
            var p1 = box.Min;
            var p2 = new Vector3D(box.Max.X, box.Min.Y, box.Min.Z);
            var p3 = box.Max;
            var p4 = new Vector3D(box.Min.X, box.Max.Y, box.Max.Z);
            
            double thick = 1.0 * pixelSizeInWorldUnits; 
            context.DrawLine(p1, p2, color, thick, isDashed:true);
            context.DrawLine(p2, p3, color, thick, isDashed:true);
            context.DrawLine(p3, p4, color, thick, isDashed:true);
            context.DrawLine(p4, p1, color, thick, isDashed:true);
        }

        // 2. Aktif Komutun Ghost (Hayalet) Çizimleri
        CommandManager.DrawGhost(context);

        // 3. Snap Marker
        if (activeSnap.HasValue)
        {
            DrawSnapMarker(context, activeSnap.Value, pixelSizeInWorldUnits);
        }
    }

    /*
       NE: Snap Ä°ÅŸaretÃ§isini Ã‡iz (DrawSnapMarker)
       NEDEN: Yakalanan snap noktasÄ±nÄ±n tipine gÃ¶re (UÃ§ nokta: Kare, Orta nokta: ÃœÃ§gen vb.) ekranda sembolik bir gÃ¶rsel oluÅŸturmak iÃ§in.
    */
    private void DrawSnapMarker(IRenderContext context, SnapPoint snap, double pixelSize)
    {
        double screenPixelSize = 10; // 10 px on screen
        double r = (screenPixelSize / 2.0) * pixelSize; // World units radius

        uint color = 0xFF00FF00; // Yeşil 

        if (snap.Type == SnapPointType.Endpoint)
        {
            // Kare (Endpoint)
            var min = new Vector3D(snap.Position.X - r, snap.Position.Y - r, 0);
            var max = new Vector3D(snap.Position.X + r, snap.Position.Y + r, 0);
            context.DrawRectangle(min, max, color, 2.0 * pixelSize);
        }
        else if (snap.Type == SnapPointType.Midpoint)
        {
             // Üçgen (Midpoint) - Skia context triangle metodumuz yok, Line ile çizelim
             var p1 = new Vector3D(snap.Position.X - r, snap.Position.Y - r, 0);
             var p2 = new Vector3D(snap.Position.X + r, snap.Position.Y - r, 0);
             var p3 = new Vector3D(snap.Position.X, snap.Position.Y + r, 0);
             
             context.DrawLine(p1, p2, color, 2.0 * pixelSize);
             context.DrawLine(p2, p3, color, 2.0 * pixelSize);
             context.DrawLine(p3, p1, color, 2.0 * pixelSize);
        }
        else if (snap.Type == SnapPointType.Center)
        {
             // Daire (Center)
             context.DrawCircle(snap.Position, r, color, 2.0 * pixelSize);
        }
        else if (snap.Type == SnapPointType.Quadrant)
        {
            // Rhombus / Diamond
             var p1 = new Vector3D(snap.Position.X - r, snap.Position.Y, 0);
             var p2 = new Vector3D(snap.Position.X, snap.Position.Y + r, 0);
             var p3 = new Vector3D(snap.Position.X + r, snap.Position.Y, 0);
             var p4 = new Vector3D(snap.Position.X, snap.Position.Y - r, 0);

             context.DrawLine(p1, p2, color, 2.0 * pixelSize);
             context.DrawLine(p2, p3, color, 2.0 * pixelSize);
             context.DrawLine(p3, p4, color, 2.0 * pixelSize);
             context.DrawLine(p4, p1, color, 2.0 * pixelSize);
        }
        else
        {
             // Cross
             var p1 = new Vector3D(snap.Position.X - r, snap.Position.Y - r, 0);
             var p2 = new Vector3D(snap.Position.X + r, snap.Position.Y + r, 0);
             var p3 = new Vector3D(snap.Position.X - r, snap.Position.Y + r, 0);
             var p4 = new Vector3D(snap.Position.X + r, snap.Position.Y - r, 0);

             context.DrawLine(p1, p2, color, 2.0 * pixelSize);
             context.DrawLine(p3, p4, color, 2.0 * pixelSize);
        }
    }

    /*
       NE: Izgara Ã‡iz (DrawGrid)
       NEDEN: Arkaplanda teknik Ã§izim kÄ±lavuzu (Izgara - Grid) oluÅŸturmak iÃ§in. Zoom seviyesine gÃ¶re izgara aralÄ±klarÄ±nÄ± dinamik olarak hesaplar.
    */
    private void DrawGrid(IRenderContext context, CadBoundingBox visibleBounds, double pixelSize)
    {
        // Grid rengi (ARGB)
        uint gridColor = 0xFF282828; // Koyu gri (40, 40, 40)
        
        // Zoom seviyesine göre grid aralığını hesapla
        // Hedef: Ekranda çizgiler arası ~100 piksel olsun
        double targetSpacing = 100 * pixelSize;
        double power = System.Math.Floor(System.Math.Log10(targetSpacing));
        double interval = System.Math.Pow(10, power);
        
        // Ara değer interval refinement
        if (targetSpacing / interval < 2) interval *= 1;
        else if (targetSpacing / interval < 5) interval *= 2;
        else interval *= 5;

        // X Çizgileri (Dikey) - visibleBounds.Min.X'den başla
        double startX = System.Math.Floor(visibleBounds.Min.X / interval) * interval;
        for (double x = startX; x <= visibleBounds.Max.X; x += interval)
        {
            context.DrawLine(
                new Vector3D(x, visibleBounds.Min.Y, 0), 
                new Vector3D(x, visibleBounds.Max.Y, 0), 
                gridColor, 1.0 * pixelSize);
        }

        // Y Çizgileri (Yatay)
        double startY = System.Math.Floor(visibleBounds.Min.Y / interval) * interval;
        for (double y = startY; y <= visibleBounds.Max.Y; y += interval)
        {
            context.DrawLine(
                new Vector3D(visibleBounds.Min.X, y, 0), 
                new Vector3D(visibleBounds.Max.X, y, 0), 
                gridColor, 1.0 * pixelSize);
        }
    }

    /*
       NE: Orijinal Koordinat Sistemini Ã‡iz (DrawWCS)
       NEDEN: Ã‡izimin (0,0) merkez noktasÄ±nÄ± X (KÄ±rmÄ±zÄ±) ve Y (YeÅŸil) eksenleriyle vurgulayarak kullanÄ±cÄ±nÄ±n konum algÄ±sÄ±nÄ± gÃ¼Ã§lendirmek iÃ§in.
    */
    private void DrawWCS(IRenderContext context, double pixelSize)
    {
        // Basit bir orijin göstergesi (0,0)
        double length = 50 * pixelSize; // Ekranda 50 piksel boyunda
        
        // X Ekseni (Kırmızı)
        context.DrawLine(new Vector3D(0, 0, 0), new Vector3D(length, 0, 0), 0xFFFF0000, 2.0 * pixelSize);
        
        // Y Ekseni (Yeşil)
        context.DrawLine(new Vector3D(0, 0, 0), new Vector3D(0, length, 0), 0xFF00FF00, 2.0 * pixelSize);
    }
}
