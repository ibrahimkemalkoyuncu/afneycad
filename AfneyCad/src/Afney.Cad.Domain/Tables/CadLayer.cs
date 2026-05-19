using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Afney.Cad.Domain.Tables;

/*
NE: Katman Tanımı (Layer Definition) — INotifyPropertyChanged ile UI bağlama desteği.

NE İÇİN:
Nesneleri mantıksal gruplara ayırmak, renk ve görünürlüklerini topluca yönetmek için.

AMAÇ:
AutoCAD Layer Tablosu standardını karşılamak (Name, Color, Visible, Locked, Frozen).
*/
public class CadLayer : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private void Notify([CallerMemberName] string? prop = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

    public string Name { get; set; } = "0";

    private uint _color = 0xFFFFFFFF;
    public uint Color
    {
        get => _color;
        set { if (_color != value) { _color = value; Notify(); Notify(nameof(ColorBrush)); } }
    }

    // WPF kolaylığı için Brush string'i (hex)
    public string ColorBrush => $"#{(_color >> 16) & 0xFF:X2}{(_color >> 8) & 0xFF:X2}{_color & 0xFF:X2}";

    private bool _isVisible = true;
    public bool IsVisible
    {
        get => _isVisible;
        set { if (_isVisible != value) { _isVisible = value; Notify(); Notify(nameof(BulbIcon)); } }
    }

    // Ampul ikonu: görünür = 💡 (sarı), gizli = 🌑
    public string BulbIcon => _isVisible ? "💡" : "🌑";

    private bool _isFrozen = false;
    public bool IsFrozen
    {
        get => _isFrozen;
        set { if (_isFrozen != value) { _isFrozen = value; Notify(); Notify(nameof(FreezeIcon)); } }
    }
    public string FreezeIcon => _isFrozen ? "❄" : "☀";

    private bool _isLocked = false;
    public bool IsLocked
    {
        get => _isLocked;
        set { if (_isLocked != value) { _isLocked = value; Notify(); Notify(nameof(LockIcon)); } }
    }
    public string LockIcon => _isLocked ? "🔒" : "🔓";

    public double LineWeight { get; set; } = 1.0;

    // Açıklama (opsiyonel, FİNESANİ layer description field'ına karşılık gelir)
    public string Description { get; set; } = string.Empty;

    public CadLayer(string name)
    {
        Name = name;
    }
}
