using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Presentation.Services;

public class ClipboardService
{
    private List<CadEntity> _buffer = new();
    private Vector3D _basePoint;
    private bool _isCut;

    public bool HasContent => _buffer.Count > 0;
    public int Count => _buffer.Count;

    public void Copy(IEnumerable<CadEntity> entities, Vector3D basePoint)
    {
        _buffer = entities.Select(e => e.Clone()).ToList();
        _basePoint = basePoint;
        _isCut = false;
    }

    public void Cut(IEnumerable<CadEntity> entities, Vector3D basePoint)
    {
        _buffer = entities.Select(e => e.Clone()).ToList();
        _basePoint = basePoint;
        _isCut = true;
    }

    public List<CadEntity> Paste(Vector3D targetPoint)
    {
        if (_buffer.Count == 0) return new();

        var delta = targetPoint - _basePoint;
        var pasted = new List<CadEntity>();

        foreach (var ent in _buffer)
        {
            var clone = ent.Clone();
            clone.Move(delta);
            pasted.Add(clone);
        }

        return pasted;
    }

    public bool WasCut => _isCut;
    public void Clear() { _buffer.Clear(); _isCut = false; }
}
