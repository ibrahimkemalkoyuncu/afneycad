namespace Afney.Cad.Presentation.Services;

public class CommandHistoryService
{
    private readonly List<string> _history = new();
    private int _index = -1;
    private const int MaxHistory = 100;

    public static readonly string[] AllCommands = {
        "line", "circle", "polyline", "rectangle", "pipe", "offset", "trim", "extend",
        "mirror", "copy", "move", "explode", "block", "insert", "wblock",
        "dimlinear", "dimaligned", "dimradius", "dimangular", "dimcontinue",
        "mtext", "dist", "hatch", "print", "autoroute", "spec",
        "bagla", "kolon", "baslangic", "kabul", "dwgimport",
        "mahal", "kolonsema", "etiket", "metraj", "lejant",
        "ifc", "ifcimport", "dxf", "rec", "scale", "rotate"
    };

    public void Add(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return;
        if (_history.Count > 0 && _history[^1] == command) return;
        _history.Add(command);
        if (_history.Count > MaxHistory) _history.RemoveAt(0);
        _index = _history.Count;
    }

    public string? NavigateUp()
    {
        if (_history.Count == 0) return null;
        _index = Math.Max(0, _index - 1);
        return _history[_index];
    }

    public string? NavigateDown()
    {
        if (_history.Count == 0) return null;
        _index = Math.Min(_history.Count, _index + 1);
        return _index < _history.Count ? _history[_index] : "";
    }

    public List<string> GetSuggestions(string prefix, int maxResults = 8)
    {
        if (string.IsNullOrEmpty(prefix)) return new();
        var lower = prefix.ToLower();
        return AllCommands
            .Where(c => c.StartsWith(lower))
            .Take(maxResults)
            .ToList();
    }
}
