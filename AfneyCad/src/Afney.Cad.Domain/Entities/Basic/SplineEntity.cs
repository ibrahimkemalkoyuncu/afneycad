using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Advanced;
using Afney.Cad.Geometry.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Afney.Cad.Domain.Entities.Basic;

/*
    NE: Spline Varlığı (SplineEntity)
    NEDEN: Mimari veya mekanik projelerde (Örn: Esnek borular, mimari peyzaj) serbest formdaki eğrileri temsil etmek için.
    
    NASIL (Mühendislik Modu):
    1. NURBS (Non-Uniform Rational B-Spline) matematik modelini kullanır.
    2. Kontrol noktaları (Control Points), Knot vektörü ve Ağırlıklar ile tanımlanır.
    3. Ekran render edilirken hassas bir şekilde tessellate edilir (PixelSize bağımlı).
*/
public class SplineEntity : CadEntity
{
    public List<Vector3D> ControlPoints { get; set; } = new();
    public List<double> Knots { get; set; } = new();
    public List<double> Weights { get; set; } = new();
    public int Degree { get; set; } = 3;

    private NURBSCurve? _cachedCurve;
    private List<Vector3D>? _cachedTessellation;

    public SplineEntity() { }

    public SplineEntity(IEnumerable<Vector3D> controlPoints, int degree = 3)
    {
        ControlPoints = controlPoints.ToList();
        Degree = degree;
        GenerateDefaultKnots();
        GenerateDefaultWeights();
    }

    /*
       NE: VarsayÄ±lan DÃ¼ÄŸÃ¼m VektÃ¶rÃ¼ Ãœret (GenerateDefaultKnots)
       NEDEN: Kontrol noktalarÄ±na uygun standard (Clamped) knot dizisini oluÅŸturarak eÄŸrinin baÅŸlangÄ±Ã§ ve bitiÅŸ noktalarÄ±ndan geÃ§mesini saÄŸlamak iÃ§in.
    */
    private void GenerateDefaultKnots()
    {
        // Clamped Knot Vector (AutoCAD Standard)
        // [0,0,0,0, 1, 2, 3, 4, 4, 4, 4]
        int n = ControlPoints.Count - 1;
        int k = n + Degree + 1;
        Knots.Clear();

        for (int i = 0; i <= Degree; i++) Knots.Add(0);
        for (int i = 1; i < n - Degree + 1; i++) Knots.Add(i);
        for (int i = 0; i <= Degree; i++) Knots.Add(n - Degree + 1);
    }

    /*
       NE: VarsayÄ±lan AÄŸÄ±rlÄ±klarÄ± Ãœret (GenerateDefaultWeights)
       NEDEN: Her bir kontrol noktasÄ± iÃ§in etkileÅŸim gÃ¼cÃ¼nÃ¼ (1.0) tanımlayarak rasyonel olmayan bir spline oluÅŸturmak iÃ§in.
    */
    private void GenerateDefaultWeights()
    {
        Weights.Clear();
        for (int i = 0; i < ControlPoints.Count; i++) Weights.Add(1.0);
    }

    /*
       NE: NURBS EÄŸri Nesnesini Al (GetCurve)
       NEDEN: Matematiksel hesaplama motorunu (NURBSCurve) veriler deÄŸiÅŸmediÄŸi sÃ¼rece bellekte tutarak (Cache) render performansÄ±nÄ± artÄ±rmak iÃ§in.
    */
    private NURBSCurve GetCurve()
    {
        if (_cachedCurve == null)
        {
            _cachedCurve = new NURBSCurve(
                ControlPoints.ToArray(), 
                Knots.ToArray(), 
                Weights.ToArray(), 
                Degree
            );
        }
        return _cachedCurve;
    }

    /*
       NE: Spline Ã‡iz (Draw)
       NEDEN: NURBS eÄŸrisini belirli sayÄ±da kÃ¼Ã§Ã¼k doÄŸru parÃ§asÄ±na bÃ¶lerek (Tessellation) ekranda pÃ¼rÃ¼zsÃ¼z bir formda gÃ¶stermek iÃ§in.
    */
    public override void Draw(IRenderContext context)
    {
        var points = Tessellate();
        if (points.Count == 0) return;

        context.DrawSpline(points, Color, LineWeight / 100.0, Linetype);

        // Geliştirme Notu: Edit modunda kontrol noktaları ve poligonu (Hull) çizilebilir.
    }

    /*
       NE: Eğriyi Doğru Parçalarına Böl (Tessellate)
       NEDEN: Draw() ve ExplodeCommand (Spline'ı LwPolyline'a çevirme) aynı tessellation
              mantığına ihtiyaç duyuyor — tekrar yazmak yerine tek yerden paylaşılıyor.
    */
    public List<Vector3D> Tessellate()
    {
        if (ControlPoints.Count <= Degree) return new List<Vector3D>();

        // PERFORMANS: Sonuç noktaları _cachedCurve ile aynı ömre sahip olarak cache'lenir.
        // ControlPoints/Knots/Weights değişmediği sürece (Move/Transform/MoveGripPointAt
        // _cachedCurve'ü null'a çekerek invalidasyonu tetikler) her Draw() çağrısında
        // segments+1 kez O(p^2) NURBSCurve.Evaluate tekrar tekrar hesaplanmaz.
        if (_cachedCurve != null && _cachedTessellation != null)
        {
            return _cachedTessellation;
        }

        var curve = GetCurve();
        double startKnot = Knots[Degree];
        double endKnot = Knots[ControlPoints.Count];

        // Tessellation: Kontrol noktası sayısına göre sabit bölüntü sayısı kullanılır.
        // NOT: Zoom seviyesine (LOD) göre dinamik bölüntü şu an uygulanmıyor — Draw(IRenderContext)
        // zoom/scale bilgisi almıyor, bu yüzden aşağıdaki segment sayısı sabittir.
        int segments = System.Math.Max(20, (int)(ControlPoints.Count * 20));
        double step = (endKnot - startKnot) / segments;

        var points = new List<Vector3D>();
        for (int i = 0; i <= segments; i++)
        {
            double u = startKnot + i * step;
            points.Add(curve.Evaluate(u));
        }

        _cachedTessellation = points;
        return points;
    }

    /*
       NE: SÄ±nÄ±rlayÄ±cÄ± Kutu Hesapla (CalculateBoundingBox)
       NEDEN: Spline'Ä±n tÃ¼m kontrol noktalarÄ±nÄ± kapsayan en kÃ¼Ã§Ã¼k kutuyu (Convex Hull) hesaplayarak mekansal aramalarda kullanmak iÃ§in.
    */
    protected override CadBoundingBox CalculateBoundingBox()
    {
        // NURBS Bounding Box: Kontrol noktalarının kapsayıcı kutusudur (Convex Hull Property)
        if (!ControlPoints.Any()) return CadBoundingBox.Empty;
        
        var min = new Vector3D(double.MaxValue, double.MaxValue, double.MaxValue);
        var max = new Vector3D(double.MinValue, double.MinValue, double.MinValue);

        foreach (var p in ControlPoints)
        {
            min = Vector3D.Min(min, p);
            max = Vector3D.Max(max, p);
        }
        return new CadBoundingBox(min, max);
    }

    /*
       NE: Spline'ı Taşı (Move)
       NEDEN: Tüm kontrol noktalarını öteleyerek eğrinin formunu bozmadan yerini değiştirmek için.
    */
    public override void Move(Vector3D delta)
    {
        for (int i = 0; i < ControlPoints.Count; i++)
            ControlPoints[i] += delta;
        _cachedCurve = null;
        _cachedTessellation = null;
    }

    /*
       NE: Matris Dönüşümü (Transform)
       NEDEN: Kontrol noktalarını matrisle çarparak eğriye döndürme, ölçekleme gibi geometrik operasyonları uygulamak için.
    */
    public override void Transform(Matrix4x4 matrix)
    {
        for (int i = 0; i < ControlPoints.Count; i++)
            ControlPoints[i] = matrix.Transform(ControlPoints[i]);
        _cachedCurve = null;
        _cachedTessellation = null;
    }

    /*
       NE: Nesneyi Kopyala (Clone)
       NEDEN: Kontrol noktaları ve düğüm vektörü (knot) dahil tüm NURBS verisini derin kopyalayarak yeni bir örnek üretmek için.
    */
    public override CadEntity Clone()
    {
        return new SplineEntity
        {
            ControlPoints = new List<Vector3D>(ControlPoints),
            Knots = new List<double>(Knots),
            Weights = new List<double>(Weights),
            Degree = this.Degree,
            Color = this.Color,
            Layer = this.Layer
        };
    }

    /*
       NE: Kenetlenme Noktaları (GetSnapPoints)
       NEDEN: Eğrinin uçlarını ve formunu kontrol eden düğüm noktalarını CAD motorunun yakalayabilmesi için.
    */
    public override IEnumerable<SnapPoint> GetSnapPoints()
    {
        // Endpointler ve Kontrol Noktaları snap olabilmeli
        foreach (var p in ControlPoints)
            yield return new SnapPoint(p, SnapPointType.ControlPoint);
        
        // Gerçek eğri üzerindeki uçlar
        var curve = GetCurve();
        yield return new SnapPoint(curve.Evaluate(Knots[Degree]), SnapPointType.Endpoint);
        yield return new SnapPoint(curve.Evaluate(Knots[ControlPoints.Count]), SnapPointType.Endpoint);
    }

    /*
       NE: Grip Noktaları (GetGripPoints / MoveGripPointAt)
       NEDEN: Önceden hiç override yoktu — spline hiç grip ile düzenlenemiyordu. Her kontrol
              noktası (control point) artık ayrı bir grip; sürüklemek eğrinin o bölgesindeki
              formunu NURBS matematiğine göre gerçek şekilde değiştirir.
    */
    public override IEnumerable<Vector3D> GetGripPoints() => ControlPoints;

    public override void MoveGripPointAt(int index, Vector3D newPosition)
    {
        if (index >= 0 && index < ControlPoints.Count)
        {
            ControlPoints[index] = newPosition;
            _cachedCurve = null;
            _cachedTessellation = null;
        }
        base.MoveGripPointAt(index, newPosition);
    }
}
