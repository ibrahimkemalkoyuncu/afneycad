using System;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;

namespace Afney.Cad.Database.Transactions.Operations;

/*
NE:
Varlık Özellikleri Güncelleme Operasyonu.

NE İÇİN:
Kullanıcının sağ panelden (PropertyGrid) değiştirdiği nitelikleri (Çap, Malzeme, İsim vs.) Geri Al / Yinele (Undo/Redo) sistemine aktarmak için.

NEREDE:
Transaction Katmanında.

AMAÇ:
Atomik, geri alınabilir veri güncellemeleri.
*/
public class ModifyEntityPropertyOperation : IOperation
{
    private readonly Action _doAction;
    private readonly Action _undoAction;
    private readonly string _name;

    public string Name => _name;

    public ModifyEntityPropertyOperation(string propertyName, Action doAction, Action undoAction)
    {
        _name = $"Özellik değişikliği: {propertyName}";
        _doAction = doAction;
        _undoAction = undoAction;
    }

    public void Do()
    {
        _doAction?.Invoke();
    }

    public void Undo()
    {
        _undoAction?.Invoke();
    }
}
