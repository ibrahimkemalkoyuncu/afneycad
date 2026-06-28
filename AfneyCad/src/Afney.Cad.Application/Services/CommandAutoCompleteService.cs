using System;
using System.Collections.Generic;
using System.Linq;

namespace Afney.Cad.Application.Services;

// Komut satırı otomatik tamamlama + geçmiş yönetimi
public class CommandAutoCompleteService
{
    private readonly List<CommandEntry> _commands;
    private readonly List<string> _history = new();
    private int _historyIndex = -1;
    public int MaxHistory { get; set; } = 50;

    public CommandAutoCompleteService()
    {
        _commands = new List<CommandEntry>
        {
            // Çizim
            new("L", "LINE", "Çizgi çiz", "Çizim"),
            new("C", "CIRCLE", "Daire çiz", "Çizim"),
            new("PL", "POLYLINE", "Polyline çiz", "Çizim"),
            new("RECT", "RECTANGLE", "Dikdörtgen çiz", "Çizim"),
            new("H", "HATCH", "Tarama ekle", "Çizim"),
            new("MT", "MTEXT", "Çoklu metin ekle", "Çizim"),

            // Düzenleme
            new("TR", "TRIM", "Buda", "Düzenleme"),
            new("EX", "EXTEND", "Uzat", "Düzenleme"),
            new("MI", "MIRROR", "Aynala", "Düzenleme"),
            new("CO", "COPY", "Kopyala", "Düzenleme"),
            new("M", "MOVE", "Taşı", "Düzenleme"),
            new("X", "EXPLODE", "Patlat", "Düzenleme"),
            new("O", "OFFSET", "Ötele", "Düzenleme"),

            // Boyutlandırma
            new("DIM", "DIMLINEAR", "Doğrusal ölçü", "Boyut"),
            new("DIMA", "DIMALIGNED", "Hizalı ölçü", "Boyut"),
            new("DIMR", "DIMRADIUS", "Yarıçap ölçü", "Boyut"),
            new("DIMANG", "DIMANGULAR", "Açısal ölçü", "Boyut"),
            new("DCO", "DIMCONTINUE", "Zincirleme ölçü", "Boyut"),
            new("DIST", "DIST", "Mesafe ölç", "Boyut"),

            // Tesisat
            new("P", "PIPE", "Boru çiz", "Tesisat"),
            new("CF", "CONNECT", "Cihaz bağla", "Tesisat"),
            new("RISER", "RISER", "Kolon borusu", "Tesisat"),
            new("SP", "SOURCE", "Başlangıç noktası", "Tesisat"),
            new("DUCT", "DUCT", "Kanal çiz", "HVAC"),
            new("DC", "DUCTCONNECT", "Kanal bağla", "HVAC"),

            // Mahal
            new("MA", "MAHAL", "Mahal tanımla", "Mahal"),
            new("MAN", "MAHALANALIZ", "Mahal incele", "Mahal"),
            new("KS", "KOLONSEMA", "Kolon şeması", "Mahal"),

            // Blok
            new("B", "BLOCK", "Blok tanımla", "Blok"),
            new("I", "INSERT", "Blok yerleştir", "Blok"),
            new("WBLOCK", "WBLOCK", "WBlock sihirbazı", "Blok"),

            // Rapor/Export
            new("BOM", "BOM", "Metraj raporu", "Rapor"),
            new("IFC", "IFCEXPORT", "IFC dışa aktar", "BIM"),
            new("DXF", "DXFEXPORT", "DXF dışa aktar", "Export"),
            new("SPEC", "TECHSPEC", "Teknik şartname", "Rapor"),

            // Mimari
            new("AD", "ARCHDETECT", "Mimari algılama", "Mimari"),
            new("MB", "ARCHBOM", "Mimari metraj", "Mimari"),
            new("REC", "REC", "Mimari tanıma", "Mimari"),

            // Yazdırma
            new("PRINT", "PRINT", "Yazdır", "Çıktı"),
            new("PLOT", "PLOT", "Çizdir", "Çıktı"),
        };
    }

    // Otomatik tamamlama önerileri
    public List<CommandSuggestion> GetSuggestions(string input, int maxResults = 8)
    {
        if (string.IsNullOrWhiteSpace(input)) return new();

        string upper = input.ToUpperInvariant();

        return _commands
            .Where(c => c.Alias.StartsWith(upper) || c.FullName.StartsWith(upper)
                || c.Description.Contains(input, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(c => c.Alias == upper ? 100 : c.Alias.StartsWith(upper) ? 50 : 10)
            .ThenBy(c => c.Alias.Length)
            .Take(maxResults)
            .Select(c => new CommandSuggestion
            {
                Alias = c.Alias,
                FullName = c.FullName,
                Description = c.Description,
                Category = c.Category,
                DisplayText = $"{c.Alias} ({c.FullName}) — {c.Description}"
            })
            .ToList();
    }

    // Geçmişe ekle
    public void AddToHistory(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return;
        _history.Remove(command);
        _history.Insert(0, command);
        if (_history.Count > MaxHistory) _history.RemoveAt(_history.Count - 1);
        _historyIndex = -1;
    }

    // Geçmişte yukarı git
    public string? HistoryUp()
    {
        if (_history.Count == 0) return null;
        _historyIndex = Math.Min(_historyIndex + 1, _history.Count - 1);
        return _history[_historyIndex];
    }

    // Geçmişte aşağı git
    public string? HistoryDown()
    {
        if (_historyIndex <= 0) { _historyIndex = -1; return ""; }
        _historyIndex--;
        return _history[_historyIndex];
    }

    public IReadOnlyList<string> GetHistory() => _history;
    public IReadOnlyList<CommandEntry> GetAllCommands() => _commands;
}

public class CommandEntry
{
    public string Alias { get; set; }
    public string FullName { get; set; }
    public string Description { get; set; }
    public string Category { get; set; }

    public CommandEntry(string alias, string fullName, string description, string category)
    {
        Alias = alias; FullName = fullName; Description = description; Category = category;
    }
}

public class CommandSuggestion
{
    public string Alias { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    public string DisplayText { get; set; } = "";
}
