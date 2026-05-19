using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Database.Core;
using Afney.Cad.Database.Transactions;
using Afney.Cad.Database.Transactions.Operations;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Commands.BasicCommands;

/*
   NE: Ayna (Mirror) Komutu
   NEDEN: Seçili objeleri kullanıcı tarafından belirlenen iki noktalık bir eksene göre simetrik olarak kopyalamak için.
*/
public class MirrorCommand : ICadCommand
{
    private enum State { WaitForFirstPoint, WaitForSecondPoint }
    private State _currentState = State.WaitForFirstPoint;

    private readonly CadDatabase _database;
    private readonly TransactionManager _transactionManager;
    private readonly List<CadEntity> _selectedEntities;

    private Vector3D? _firstPoint;
    private Vector3D? _currentMousePos;
    
    // Ghost (Önizleme) için
    private List<CadEntity>? _ghostEntities;

    public string CommandName => "MIRROR";
    public Vector3D? ActivePoint => _firstPoint; // Base Point çizgisi çekmek için

    public event Action<string>? OnFeedback;
    public event Action? OnCompleted;

    public MirrorCommand(CadDatabase database, TransactionManager transactionManager, IEnumerable<CadEntity> selection)
    {
        _database = database;
        _transactionManager = transactionManager;
        _selectedEntities = selection.ToList();
    }

    public void Start()
    {
        if (_selectedEntities.Count == 0)
        {
            OnFeedback?.Invoke("MIRROR: Aynalanacak nesneleri seçin. Komut iptal.");
            OnCompleted?.Invoke();
            return;
        }

        OnFeedback?.Invoke("MIRROR: Simetri ekseninin birinci noktasını tıklayın.");
    }

    public void OnPointerPressed(Vector3D point)
    {
        if (_currentState == State.WaitForFirstPoint)
        {
            _firstPoint = point;
            _currentState = State.WaitForSecondPoint;
            OnFeedback?.Invoke("MIRROR: Simetri ekseninin ikinci noktasını tıklayın.");
        }
        else if (_currentState == State.WaitForSecondPoint)
        {
            ApplyMirror(point);
            OnCompleted?.Invoke();
        }
    }

    public void OnPointerMoved(Vector3D point)
    {
        _currentMousePos = point;

        if (_currentState == State.WaitForSecondPoint && _firstPoint.HasValue)
        {
            UpdateGhosts(point);
        }
    }

    private void UpdateGhosts(Vector3D secondPoint)
    {
        // Aynalama Ekseni P1-P2 arasında çok kısa ise önizleme gösterme
        if (_firstPoint!.Value.DistanceTo(secondPoint) < 1e-5)
        {
            _ghostEntities = null;
            return;
        }

        var mirrorMatrix = Matrix4x4.Reflection(_firstPoint.Value, secondPoint);
        _ghostEntities = new List<CadEntity>();

        foreach (var ent in _selectedEntities)
        {
            var clone = ent.Clone();
            clone.Color = 0xFFFFFFAA; // Uçuk sarı (Önizleme Rengi)
            clone.Transform(mirrorMatrix);
            _ghostEntities.Add(clone);
        }
    }

    private void ApplyMirror(Vector3D secondPoint)
    {
        if (_firstPoint!.Value.DistanceTo(secondPoint) < 1e-5)
        {
            OnFeedback?.Invoke("MIRROR: Eksen çok kısa. İşlem iptal edildi.");
            return;
        }

        var mirrorMatrix = Matrix4x4.Reflection(_firstPoint.Value, secondPoint);
        var composite = new CompositeOperation("Mirror Entities");

        foreach (var ent in _selectedEntities)
        {
            var clone = ent.Clone();
            clone.Transform(mirrorMatrix);
            composite.Add(new AddEntityOperation(_database, clone));
        }

        _transactionManager.Submit(composite);
        OnFeedback?.Invoke($"MIRROR: {_selectedEntities.Count} nesne aynalandı.");
    }

    public void OnKeyDown(InputKey key)
    {
        if (key == InputKey.Escape)
            Cancel();
    }

    public void Cancel()
    {
        _ghostEntities = null;
        OnCompleted?.Invoke();
    }

    public void Draw(IRenderContext context)
    {
        if (_currentState == State.WaitForSecondPoint && _firstPoint.HasValue && _currentMousePos.HasValue)
        {
            // Sanal Aynalama Ekseni Çizgisi (Dashed White/Yellow)
            context.DrawLine(_firstPoint.Value, _currentMousePos.Value, 0xFFFFFFFF, 1.0 * context.PixelSize, isDashed: true);

            // Önizleme Nesneleri (Ghosts)
            if (_ghostEntities != null)
            {
                foreach (var ghost in _ghostEntities)
                {
                    ghost.Draw(context);
                }
            }
        }
    }
}
