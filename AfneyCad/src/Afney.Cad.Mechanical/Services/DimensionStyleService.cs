using System.Text.Json;

namespace Afney.Cad.Mechanical.Services;

public class DimensionStyle
{
    public string Name        { get; set; } = "Standard";
    /*
       MÜHENDİSLİK: Değerler (TextHeight/ArrowSize/ExtLineGap/ExtLineOver) önceden 250/200/50/75
       idi — araştırma ajanı bulgusuna göre bu, aynı çizimdeki kapı/pencere etiketlerinin
       (fontSize=80mm, kullanıcının doğru kabul ettiği referans) ~3 katıydı. Artık ona yakın.
    */
    public double TextHeight   { get; set; } = 100.0;
    public double ArrowSize    { get; set; } = 80.0;
    public double ExtLineGap   { get; set; } = 20.0;
    public double ExtLineOver  { get; set; } = 30.0;
    public uint   Color        { get; set; } = 0xFF00CCFF;
    public string TextFont     { get; set; } = "Consolas";
    public bool   ShowUnits    { get; set; } = true;
    public int    Precision    { get; set; } = 0;
    public string UnitFormat   { get; set; } = "mm";

    // NE: Ok Başı Stili (ArrowStyle)
    // NEDEN: DimensionEntity.DimensionArrowStyle enum'ına string olarak eşlenir — Domain
    //        katmanına bağımlılık eklememek için burada string tutulur ("Filled"/"Open"/
    //        "Dot"/"Architectural"), DimensionStyleApplier bu string'i enum'a çevirir.
    public string ArrowStyle   { get; set; } = "Filled";
}

public class DimensionStyleService
{
    private readonly Dictionary<string, DimensionStyle> _styles = new();
    private string _activeStyleName = "Standard";

    public DimensionStyleService()
    {
        _styles["Standard"] = new DimensionStyle { Name = "Standard" };
        _styles["ISO-25"]   = new DimensionStyle { Name = "ISO-25", TextHeight = 140, ArrowSize = 112, Precision = 1 };
        _styles["Compact"]  = new DimensionStyle { Name = "Compact", TextHeight = 50, ArrowSize = 40, ExtLineGap = 10, ExtLineOver = 16 };
        _styles["Large"]    = new DimensionStyle { Name = "Large", TextHeight = 200, ArrowSize = 160, ExtLineGap = 40, ExtLineOver = 60 };
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
