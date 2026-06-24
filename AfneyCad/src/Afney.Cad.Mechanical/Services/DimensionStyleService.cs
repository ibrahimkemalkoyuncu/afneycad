using System.Text.Json;

namespace Afney.Cad.Mechanical.Services;

public class DimensionStyle
{
    public string Name        { get; set; } = "Standard";
    public double TextHeight   { get; set; } = 250.0;
    public double ArrowSize    { get; set; } = 200.0;
    public double ExtLineGap   { get; set; } = 50.0;
    public double ExtLineOver  { get; set; } = 75.0;
    public uint   Color        { get; set; } = 0xFF00CCFF;
    public string TextFont     { get; set; } = "Consolas";
    public bool   ShowUnits    { get; set; } = true;
    public int    Precision    { get; set; } = 0;
    public string UnitFormat   { get; set; } = "mm";
}

public class DimensionStyleService
{
    private readonly Dictionary<string, DimensionStyle> _styles = new();
    private string _activeStyleName = "Standard";

    public DimensionStyleService()
    {
        _styles["Standard"] = new DimensionStyle { Name = "Standard" };
        _styles["ISO-25"]   = new DimensionStyle { Name = "ISO-25", TextHeight = 350, ArrowSize = 280, Precision = 1 };
        _styles["Compact"]  = new DimensionStyle { Name = "Compact", TextHeight = 125, ArrowSize = 100, ExtLineGap = 25, ExtLineOver = 40 };
        _styles["Large"]    = new DimensionStyle { Name = "Large", TextHeight = 500, ArrowSize = 400, ExtLineGap = 100, ExtLineOver = 150 };
    }

    public DimensionStyle ActiveStyle => _styles.TryGetValue(_activeStyleName, out var s) ? s : _styles["Standard"];
    public string ActiveStyleName => _activeStyleName;
    public IReadOnlyList<string> StyleNames => _styles.Keys.ToList();

    public void SetActiveStyle(string name)
    {
        if (_styles.ContainsKey(name)) _activeStyleName = name;
    }

    public void AddStyle(DimensionStyle style)
    {
        _styles[style.Name] = style;
    }

    public void RemoveStyle(string name)
    {
        if (name != "Standard") _styles.Remove(name);
    }

    public DimensionStyle? GetStyle(string name) =>
        _styles.TryGetValue(name, out var s) ? s : null;

    public string ExportToJson()
    {
        var data = new { ActiveStyle = _activeStyleName, Styles = _styles.Values.ToList() };
        return JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
    }

    public void ImportFromJson(string json)
    {
        var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("Styles", out var stylesEl))
        {
            foreach (var styleEl in stylesEl.EnumerateArray())
            {
                var style = JsonSerializer.Deserialize<DimensionStyle>(styleEl.GetRawText());
                if (style != null) _styles[style.Name] = style;
            }
        }
        if (doc.RootElement.TryGetProperty("ActiveStyle", out var activeEl))
            _activeStyleName = activeEl.GetString() ?? "Standard";
    }
}
