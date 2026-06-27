namespace Afney.Cad.Presentation.Services;

public class PrintLayout
{
    public string Name { get; set; } = "Layout1";
    public string PaperSize { get; set; } = "A3";
    public bool IsLandscape { get; set; } = true;
    public double Scale { get; set; } = 100;
    public double MarginLeft { get; set; } = 10;
    public double MarginTop { get; set; } = 10;
    public double MarginRight { get; set; } = 10;
    public double MarginBottom { get; set; } = 10;
    public bool ShowTitleBlock { get; set; } = true;
    public double ViewportCenterX { get; set; }
    public double ViewportCenterY { get; set; }
    public double ViewportZoom { get; set; } = 1.0;
}

public class LayoutService
{
    private readonly List<PrintLayout> _layouts = new();
    private int _activeIndex = -1;

    public static readonly Dictionary<string, (double W, double H)> PaperSizes = new()
    {
        ["A4"] = (297, 210),
        ["A3"] = (420, 297),
        ["A2"] = (594, 420),
        ["A1"] = (841, 594),
        ["A0"] = (1189, 841),
    };

    public LayoutService()
    {
        _layouts.Add(new PrintLayout { Name = "Model" });
    }

    public PrintLayout AddLayout(string name = "", string paperSize = "A3")
    {
        if (string.IsNullOrEmpty(name))
            name = $"Layout{_layouts.Count}";

        var layout = new PrintLayout
        {
            Name = name,
            PaperSize = paperSize,
            IsLandscape = true,
            Scale = 100
        };
        _layouts.Add(layout);
        _activeIndex = _layouts.Count - 1;
        return layout;
    }

    public void RemoveLayout(int index)
    {
        if (index > 0 && index < _layouts.Count)
            _layouts.RemoveAt(index);
    }

    public PrintLayout? ActiveLayout => _activeIndex >= 0 && _activeIndex < _layouts.Count
        ? _layouts[_activeIndex] : _layouts.Count > 0 ? _layouts[0] : null;

    public void SetActive(int index)
    {
        if (index >= 0 && index < _layouts.Count)
            _activeIndex = index;
    }

    public IReadOnlyList<PrintLayout> Layouts => _layouts;

    public (double printW, double printH) GetPrintableArea(PrintLayout layout)
    {
        var (w, h) = PaperSizes.TryGetValue(layout.PaperSize, out var size) ? size : (420, 297);
        if (layout.IsLandscape) (w, h) = (Math.Max(w, h), Math.Min(w, h));
        else (w, h) = (Math.Min(w, h), Math.Max(w, h));
        return (w - layout.MarginLeft - layout.MarginRight, h - layout.MarginTop - layout.MarginBottom);
    }

    public string GetScaleText(PrintLayout layout) => layout.Scale switch
    {
        <= 0 => "Fit",
        1 => "1:1",
        _ => $"1:{layout.Scale:F0}"
    };
}
