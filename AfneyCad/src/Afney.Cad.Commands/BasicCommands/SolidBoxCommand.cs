using System;
using System.Collections.Generic;
using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Database.Core;
using Afney.Cad.Database.Transactions;
using Afney.Cad.Database.Transactions.Operations;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Geometry.Topology;

namespace Afney.Cad.Commands.BasicCommands;

/*
   NE: Katı Kutu Oluşturma Komutu (SolidBoxCommand — "BOX")
   NEDEN: SolidEntity (bkz. Afney.Cad.Domain.Entities.Basic.SolidEntity) çizime eklendi ama
          kullanıcının çizimde HİÇBİR SolidEntity'si yoksa UNION/SUBTRACT/INTERSECT komutlarını
          denemesi için bir başlangıç noktası (test/demo nesnesi) yoktu. AutoCAD'in BOX komutuyla
          aynı 2-tık deseni (RectangleCommand ile birebir aynı akış): ilk tık taban dikdörtgenin
          bir köşesi, ikinci tık karşı köşesi — sabit bir yükseklikte (Z ekseni boyunca) bir
          B-Rep kutu (BRepBuilder.ExtrudeBox) üretilir ve veritabanına kalıcı SolidEntity olarak
          eklenir (Undo'lu).

   KAPSAM DIŞI (v1, dokümante edilen sonraki adım): Yükseklik şu an sabit (DefaultHeightMm) —
   3. bir tıkla (Z sürükleme) veya komut satırı sayısal girişiyle yükseklik belirleme, bu
   oturumun kapsamı dışında bırakıldı (Ana Yasa: yarım/riskli bırakmamak için minimal ama
   TAM ÇALIŞAN bir alt-küme seçildi).
*/
public class SolidBoxCommand : ICadCommand
{
    public const double DefaultHeightMm = 1000.0;

    private readonly CadDatabase _database;
    private readonly TransactionManager _transactionManager;
    private readonly double _heightMm;
    private Vector3D? _startCorner;

    public string CommandName => "BOX";
    public Vector3D? ActivePoint => _startCorner;
    public event Action<string>? OnFeedback;
#pragma warning disable CS0067
    public event Action? OnCompleted;
#pragma warning restore CS0067

    public SolidBoxCommand(CadDatabase database, TransactionManager transactionManager, double heightMm = DefaultHeightMm)
    {
        _database = database;
        _transactionManager = transactionManager;
        _heightMm = heightMm <= 0 ? DefaultHeightMm : heightMm;
    }

    public void Start()
    {
        OnFeedback?.Invoke($"BOX: Taban dikdörtgeninin ilk köşesini belirtin (Yükseklik: {_heightMm:0} mm).");
    }

    public void OnPointerPressed(Vector3D point)
    {
        if (_startCorner == null)
        {
            _startCorner = point;
            OnFeedback?.Invoke("BOX: Karşı köşeyi belirtin.");
            return;
        }

        var p1 = _startCorner.Value;
        var p2 = point;
        _startCorner = null;

        double lenU = Math.Abs(p2.X - p1.X);
        double lenV = Math.Abs(p2.Y - p1.Y);
        if (lenU < 1e-6 || lenV < 1e-6)
        {
            OnFeedback?.Invoke("BOX: Geçersiz boyut (sıfır alan). Tekrar deneyin — ilk köşeyi belirtin.");
            return;
        }

        var origin = new Vector3D(Math.Min(p1.X, p2.X), Math.Min(p1.Y, p2.Y), p1.Z);
        var solid = BRepBuilder.ExtrudeBox(origin, Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, lenU, lenV, _heightMm, name: $"Box_{Guid.NewGuid():N}");

        var entity = new SolidEntity(solid)
        {
            Layer = _database.ActiveLayerName,
            Color = _database.GetLayer(_database.ActiveLayerName)?.Color ?? 0xFFFFFFFF
        };
        _transactionManager.Submit(new AddEntityOperation(_database, entity));

        OnFeedback?.Invoke("BOX: Kutu oluşturuldu. Taban dikdörtgeninin ilk köşesini belirtin (bitirmek için ESC).");
    }

    public void OnPointerMoved(Vector3D point) { }
    public void OnKeyDown(InputKey key) { }
    public void Draw(IRenderContext context) { }

    public void Cancel()
    {
        _startCorner = null;
    }
}
