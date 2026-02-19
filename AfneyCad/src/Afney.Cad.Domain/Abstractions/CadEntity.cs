using Afney.Cad.Geometry.Primitives;
using System.Text.Json.Serialization;
using Afney.Cad.Domain.Entities.Basic;

namespace Afney.Cad.Domain.Abstractions;

/*
   NE: CAD Nesne Atası (CadEntity)
   NE İÇİN: Çizimdeki tüm geometrik ve mekanik varlıkların (Çizgi, Boru, Vana vb.) ortak davranışlarını, kimlik bilgilerini ve görsel özniteliklerini merkezi olarak tanımlamak için.
   
   NASIL (Mühendislik Açıklaması):
   - Tüm nesneler tekil bir GUID (Id) ile takip edilir; bu, veritabanı ilişkileri ve Undo/Redo için vazgeçilmezdir.
   - Grafik motorundan bağımsızlık için 'Draw' metodunu soyut olarak sunar.
   - Kenetlenme (Snap) ve Çerçeve (BoundingBox) hesaplamaları ile hassas mühendislik etkileşimini sağlar.
*/
public abstract class CadEntity
{
    // KİMLİK BİLGİLERİ (IDENTITY)
    public Guid Id { get; set; } = Guid.NewGuid(); // Veritabanı ve ilişki yönetimi için benzersiz anahtar.

    // GÖRSEL ÖZNİTELİKLER (ATTRIBUTES)
    public string Layer { get; set; } = "0";      // Katman (Layer) bilgisi. Mimaride duvar, kolon ayrımı için kritiktir.
    public uint Color { get; set; } = 0xFFFFFFFF; // Renk (ARGB formatında). Görselleştirme ve baskı için kullanılır.

    // BLOK BİLGİSİ (HIERARCHY)
    // Bir nesne eğer bir Blok (INSERT) referansının parçasıysa, bu ID dolu olur.
    // Örnek: Lavabo bloğunun içindeki çizgiler, aynı ParentBlockId'ye sahip olur.
    public Guid? ParentBlockId { get; set; } 
    public string? ParentBlockName { get; set; } // Lavabo, Kolon vb. blok adını taşır.
    public Vector3D? SourceBlockPosition { get; set; } // Blok referans noktası (Insert Point)
    public double? SourceBlockRotation { get; set; } // Blok rotasyonu
    public bool IsFromBlock => ParentBlockId.HasValue; 

    // SEÇİM DURUMU (SELECTION)
    public bool IsSelected { get; set; } = false;
    public virtual bool Selectable => true; // Varsayılan olarak her şey seçilebilir.

    // Mekansal Dönüşüm Matrisi (World Transform)
    public Matrix4x4 TransformMatrix { get; set; } = Matrix4x4.Identity;

    // ÇİZİM VE ETKİLEŞİM (RENDERING & INTERACTION)
    /*
       NE: Çizim Metodu (Draw)
       NEDEN: Nesnenin geometri tipine göre (Çizgi, Ark, Metin vb.) render motoru tarafından ekrana basılmasını sağlar.
    */
    public abstract void Draw(IRenderContext context);
    
    // GEOMETRİK İŞLEMLER (GEOMETRY OPS)
    // Önbellekli Bounding Box (Performans için)
    protected CadBoundingBox? _cachedBoundingBox;
    
    /*
       NE: Sınırlayıcı Kutu Getir (GetBoundingBox)
       NEDEN: Nesneyi tam olarak içine alan en küçük dikdörtgen kutuyu döner. Zoom Extents, seçim ve performans optimizasyonları (Culling) için kullanılır.
    */
    public CadBoundingBox GetBoundingBox()
    {
        if (_cachedBoundingBox == null)
        {
            _cachedBoundingBox = CalculateBoundingBox();
        }
        return _cachedBoundingBox.Value;
    }

    // Alt sınıflar bunu implemente etmeli (Eski GetBoundingBox -> CalculateBoundingBox)
    /*
       NE: Sınırlayıcı Kutu Hesapla (CalculateBoundingBox)
       NEDEN: Alt sınıflar (Line, Circle vb.) kendi özel geometrilerine göre sınır kutusunu burada hesaplar.
    */
    protected abstract CadBoundingBox CalculateBoundingBox();

    // Geometri değiştiğinde çağrılmalı
    /*
       NE: Önbelleği Temizle (InvalidateCache)
       NEDEN: Nesnenin koordinatları veya boyutu değiştiğinde, eski sınır kutusu verisini çöpe atarak yeniden hesaplanmasını tetiklemek için.
    */
    protected void InvalidateCache()
    {
        _cachedBoundingBox = null;
    }
    // GÖRSEL ÖZELLİKLER (VISUAL PROPERTIES - ACAD STANDARDS)
    
    // Çizgi Tipi Adı (Örn: "Continuous", "Dashed", "Center")
    public string Linetype { get; set; } = "Continuous";
    
    // NE: Çizgi tipi kesikli mi?
    // NEDEN: Render motoruna dash bilgisini hızlıca geçmek ve gruplamak için.
    public bool IsDashed => !string.IsNullOrEmpty(Linetype) && !Linetype.Equals("Continuous", StringComparison.OrdinalIgnoreCase);
    
    // Çizgi Kalınlığı (1/100 mm cinsinden). Örn: 25 = 0.25mm. 
    // -1: ByLayer, -2: ByBlock, -3: Default (0.25mm)
    public short LineWeight { get; set; } = -1; // Varsayılan ByLayer

    /*
       NE: Render Kalınlığını Hesapla
       NEDEN: Çizgi ağırlığı (LineWeight) değerini piksel tabanlı olmayan, mm hassasiyetinde bir render kalınlığına dönüştürmek için.
    */
    public double GetRenderWeight()
    {
        // Render için kalınlık hesapla (Piksel cinsinden değil, mantıksal mm)
        if (LineWeight > 0) return LineWeight / 100.0;
        return 0.15; // Default ince çizgi
    }

    /*
       NE: Nesneyi Ötele (Move)
       NEDEN: Nesnenin dünya koordinatlarındaki yerini bir vektör kadar değiştirmek için.
    */
    public abstract void Move(Vector3D delta);

    /*
       NE: Dönüşüm Uygula (Transform)
       NEDEN: Nesneye matris çarpımı yoluyla taşıma, döndürme veya ölçekleme işlemleri uygulamak için.
    */
    public abstract void Transform(Matrix4x4 matrix);

    /*
       NE: Kopya Oluştur (Clone)
       NEDEN: Nesnenin tam bir kopyasını oluşturmak için (Örn: AutoCAD COPY komutu).
    */
    public abstract CadEntity Clone();
    
    // Yardımcı Clone Metodu (Temel özellikleri kopyalar)
    /*
       NE: Temel Özellikleri Kopyala
       NEDEN: Deep clone işlemleri sırasında renk, katman, çizgi tipi gibi ortak özniteliklerin yeni nesneye aktarılmasını sağlamak için.
    */
    protected void CopyBaseProperties(CadEntity target)
    {
        target.Id = Guid.NewGuid(); // Yeni ID!
        target.Color = this.Color;
        target.Layer = this.Layer;
        target.ParentBlockId = this.ParentBlockId;
        target.ParentBlockName = this.ParentBlockName;
        target.SourceBlockPosition = this.SourceBlockPosition;
        target.SourceBlockRotation = this.SourceBlockRotation;
        target.TransformMatrix = this.TransformMatrix;
        target.Linetype = this.Linetype;
        target.LineWeight = this.LineWeight;
    }

    /*
       NE: Yakalama Noktalarını Getir (SnapPoints)
       NEDEN: Fare ile hassas çizim yaparken nesnenin uç noktası, merkez noktası gibi özel noktaların yakalanmasını sağlamak için.
    */
    public abstract IEnumerable<SnapPoint> GetSnapPoints();
}
