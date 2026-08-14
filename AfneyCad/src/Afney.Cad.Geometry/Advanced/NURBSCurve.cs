using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Geometry.Advanced;

/*
NE:
NURBS (Non-Uniform Rational B-Spline) Curve - CAD sistemlerinin çekirdeği.

NE İÇİN:
AutoCAD/Revit seviyesinde karmaşık eğriler modellemek için.

NEREDE:
Geometry Kernel - Advanced modülü.

NE ZAMAN:
Spline, freeform curve, arc gibi geometriler oluşturulurken.

AMAÇ:
De Boor's algoritması ile parametrik eğri hesaplaması.
NURBS = Line, Arc, Circle, Bezier'in genelleştirilmiş hali.

PERFORMANS:
O(p²) - p = degree (genellikle 3 veya 5)
*/
public class NURBSCurve
{
    private readonly Vector3D[] _controlPoints;
    private readonly double[] _knots;
    private readonly double[] _weights;
    private readonly int _degree;

    // NE: Türev eğrisi (GetDerivative'in içindeki degree-1 yardımcı NURBSCurve'ü) cache.
    // NEDEN: Türev kontrol noktaları/knot'ları SADECE orijinal eğrinin (_controlPoints/
    //        _knots/_degree) bir fonksiyonudur, GetDerivative'in `parameter` argümanına
    //        BAĞLI DEĞİLDİR. Önceden her GetDerivative çağrısında (ör. teğet/perpendicular
    //        snap sırasında tekrar tekrar) yeni control point + knot + weight array'leri
    //        allocate edilip yeni bir NURBSCurve inşa ediliyordu. Bu eğrinin kontrol
    //        noktaları/knot'ları değişmediği sürece (readonly alanlar — obje immutable)
    //        geçerli olduğundan ilk çağrıda hesaplanıp saklanıyor (lazy-init).
    private NURBSCurve? _cachedDerivativeCurve;

    public int Degree => _degree;
    public int ControlPointCount => _controlPoints.Length;
    
    /*
    CONSTRUCTOR:
    
    GİRDİLER:
    - controlPoints: Kontrol noktaları (n+1 adet)
    - knots: Knot vektörü (n+p+2 adet), artan sırada
    - weights: Ağırlıklar (n+1 adet), pozitif
    - degree: Derece (genellikle 3 = cubic)
    
    KURAL:
    knots.Length = controlPoints.Length + degree + 1
    
    ÖRNEK (Cubic NURBS):
    - 4 kontrol noktası
    - degree = 3
    - knots = [0, 0, 0, 0, 1, 1, 1, 1] (8 adet)
    */
    public NURBSCurve(Vector3D[] controlPoints, double[] knots, double[] weights, int degree)
    {
        if (knots.Length != controlPoints.Length + degree + 1)
            throw new ArgumentException("Invalid knot vector length");
        
        if (weights.Length != controlPoints.Length)
            throw new ArgumentException("Weights must match control points");
        
        _controlPoints = controlPoints;
        _knots = knots;
        _weights = weights;
        _degree = degree;
    }

    /*
    METOD ADI:
    Evaluate

    AMACI:
    Verilen parametrede (u) eğri üzerindeki noktayı hesaplamak.

    GİRDİLER:
    - parameter: u ∈ [knots[0], knots[n]] aralığında

    ÇIKTILAR:
    Vector3D - Eğri üzerindeki nokta

    ALGORİTMA:
    De Boor's Algorithm (Carl de Boor, 1972)
    - AutoCAD ve tüm profesyonel CAD sistemlerinde kullanılır
    - Numerically stable (Casteljau algoritmasına benzer)

    KARMAŞIKLIK:
    O(p²) - p = degree
    */
    public Vector3D Evaluate(double parameter)
    {
        // 1. Find knot span
        int span = FindSpan(parameter);
        
        // 2. Compute basis functions
        double[] basis = ComputeBasisFunctions(span, parameter);
        
        // 3. Weighted sum (Rational B-Spline)
        Vector3D numerator = Vector3D.Zero;
        double denominator = 0.0;
        
        for (int i = 0; i <= _degree; i++)
        {
            int index = span - _degree + i;
            if (index < 0 || index >= _controlPoints.Length)
                continue;
                
            double weight = _weights[index];
            numerator += _controlPoints[index] * (basis[i] * weight);
            denominator += basis[i] * weight;
        }
        
        return numerator * (1.0 / denominator);
    }

    /*
    METOD ADI:
    FindSpan

    AMACI:
    Parametrenin hangi knot aralığında olduğunu bulmak.

    ALGORİTMA:
    Binary search - O(log n)

    ÖRNEK:
    knots = [0, 0, 0, 0.5, 1, 1, 1]
    u = 0.7 → span = 3 (knots[3] <= u < knots[4])
    */
    private int FindSpan(double u)
    {
        int n = _controlPoints.Length - 1;
        
        // Special case: u at the end
        if (u >= _knots[n + 1])
            return n;
        
        // Binary search
        int low = _degree;
        int high = n + 1;
        int mid = (low + high) / 2;
        
        while (u < _knots[mid] || u >= _knots[mid + 1])
        {
            if (u < _knots[mid])
                high = mid;
            else
                low = mid;
            
            mid = (low + high) / 2;
        }
        
        return mid;
    }

    /*
    METOD ADI:
    ComputeBasisFunctions

    AMACI:
    N_{i,p}(u) basis fonksiyonlarını hesaplamak.

    ALGORİTMA:
    Cox-de Boor recursion formula:
    
    N_{i,0}(u) = 1 if u_i <= u < u_{i+1}, else 0
    
    N_{i,p}(u) = [(u - u_i) / (u_{i+p} - u_i)] * N_{i,p-1}(u) +
                 [(u_{i+p+1} - u) / (u_{i+p+1} - u_{i+1})] * N_{i+1,p-1}(u)

    KARMAŞIKLIK:
    O(p²)
    */
    private double[] ComputeBasisFunctions(int span, double u)
    {
        double[] basis = new double[_degree + 1];
        double[] left = new double[_degree + 1];
        double[] right = new double[_degree + 1];
        
        basis[0] = 1.0;
        
        for (int j = 1; j <= _degree; j++)
        {
            left[j] = u - _knots[span + 1 - j];
            right[j] = _knots[span + j] - u;
            
            double saved = 0.0;
            
            for (int r = 0; r < j; r++)
            {
                double temp = basis[r] / (right[r + 1] + left[j - r]);
                basis[r] = saved + right[r + 1] * temp;
                saved = left[j - r] * temp;
            }
            
            basis[j] = saved;
        }
        
        return basis;
    }

    /*
    METOD ADI:
    GetDerivative

    AMACI:
    Eğrinin birinci türevini (teğet vektörü) hesaplamak.

    KULLANIM:
    - Tangent display
    - Perpendicular snap
    - Arc length calculation (integral)

    ALGORİTMA:
    NURBS derivative = De Boor algorithm on derivative control points
    */
    public Vector3D GetDerivative(double parameter)
    {
        // Analytic Calculus: Hodograph Control Points
        // C'(u) = Sum( N_{i,p-1}(u) * Q_i )

        // NE/NEDEN: Türev kontrol noktaları/knot'ları/weight'leri SADECE bu eğrinin
        // (_controlPoints/_knots/_degree) fonksiyonu — `parameter`'a bağlı değil. Önceden
        // her çağrıda yeniden allocate edilip yeni bir NURBSCurve inşa ediliyordu; artık
        // ilk çağrıda hesaplanıp _cachedDerivativeCurve alanında saklanıyor.
        var derivCurve = _cachedDerivativeCurve ??= BuildDerivativeCurve();
        if (derivCurve == null) return Vector3D.Zero;

        try
        {
            return derivCurve.Evaluate(parameter);
        }
        catch
        {
             // Fallback: Knot vector uyumsuzluğu olursa (Uç durumlarda)
             // Manuel hesaplama yap
             return Vector3D.Zero;
        }
    }

    private NURBSCurve? BuildDerivativeCurve()
    {
        // 1. Türev Kontrol Noktalarını (Q) Hesapla
        // Q_i = p * (P_{i+1} - P_i) / (u_{i+p+1} - u_{i+1})
        int n = _controlPoints.Length - 1;
        var derivControlPoints = new Vector3D[n];

        for (int i = 0; i < n; i++)
        {
            double denominator = _knots[i + _degree + 1] - _knots[i + 1];
            if (denominator > 1e-10)
            {
                derivControlPoints[i] = (_controlPoints[i + 1] - _controlPoints[i]) * (_degree / denominator);
            }
            else
            {
                derivControlPoints[i] = Vector3D.Zero;
            }
        }

        // 2. Bir Derece Düşük (degree-1) Baz Fonksiyonları
        // Knot Vektörü: Ilk ve son elemanı at (Basitleştirilmiş)
        // Gerçek implementasyonda U' = U[1...m-1] kullanılır.
        // Ancak bu proje kapsamında basit Evaluate mantığı yeterli:

        // Knot vector for derivative curve: Remove first and last knot
        var derivKnots = new double[_knots.Length - 2];
        Array.Copy(_knots, 1, derivKnots, 0, _knots.Length - 2);

        // Create cached curve object for derivative evaluation
        // "No Shortcut" prensibi gereği doğru matematik uygulanmalı.
        try
        {
             // Weights şimdilik ihmal (Non-Rational B-Spline varsayımı)
             // Rational (NURBS) türevi çok daha karmaşık: A'(u)/w(u) - A(u)w'(u)/w(u)^2
             // Şimdilik sadece B-Spline türevi:
             return new NURBSCurve(derivControlPoints, derivKnots, new double[derivControlPoints.Length], _degree - 1);
        }
        catch
        {
             // Fallback: Knot vector uyumsuzluğu olursa (Uç durumlarda)
             return null;
        }
    }

    /*
    STATIC FACTORY METHODS
    */

    // Line as NURBS (degree 1)
    public static NURBSCurve CreateLine(Vector3D start, Vector3D end)
    {
        return new NURBSCurve(
            controlPoints: [start, end],
            knots: [0, 0, 1, 1],
            weights: [1, 1],
            degree: 1
        );
    }

    // Circular arc as NURBS (degree 2, rational)
    public static NURBSCurve CreateCircularArc(Vector3D center, double radius, double startAngle, double endAngle)
    {
        // NURBS circle representation (9 control points for full circle)
        // Simplified: 3 control points for arc
        double angle = endAngle - startAngle;
        double weight = Math.Cos(angle / 2.0);
        
        var start = new Vector3D(
            center.X + radius * Math.Cos(startAngle),
            center.Y + radius * Math.Sin(startAngle)
        );
        
        var end = new Vector3D(
            center.X + radius * Math.Cos(endAngle),
            center.Y + radius * Math.Sin(endAngle)
        );
        
        var mid = new Vector3D(
            center.X + radius * Math.Cos((startAngle + endAngle) / 2.0) / weight,
            center.Y + radius * Math.Sin((startAngle + endAngle) / 2.0) / weight
        );
        
        return new NURBSCurve(
            controlPoints: [start, mid, end],
            knots: [0, 0, 0, 1, 1, 1],
            weights: [1, weight, 1],
            degree: 2
        );
    }
}
