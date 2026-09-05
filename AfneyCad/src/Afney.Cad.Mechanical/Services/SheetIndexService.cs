using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Pafta İndeksi Servisi (SheetIndexService)
   NEDEN: Denetim raporunda tespit edildi — TitleBlockService.PaftaNo tamamen serbest-metin
          bir alandı; kullanıcı her antet için elle bir numara yazıyordu. Birden fazla pafta
          (kat/sistem/rapor bazlı) üretilen projelerde bu, çakışan/tutarsız numaralara yol açar.

   NASIL (Session #74 güncellemesi): Artık KALICI. Disipline göre seri artan numara üreten
          bir sayaç + üretilen paftaların (numara/isim/açıklama/tarih) bir listesi tutar ve
          bu veriyi JSON'a serileştirebilir (bkz. ToJson/LoadFromJson). MainWindow.FileOps.cs,
          proje dosyası (.dwg/.dxf/.afney) kaydedilirken/yüklenirken bu JSON'u proje dosyasının
          YANINA bir "<dosya>.sheetset.json" yardımcı dosyası olarak yazar/okur — tıpkı halihazırda
          var olan ".layerstate" mekanizmasında olduğu gibi (bkz. SaveLayerState/LoadLayerState).
          Gerçek DWG (ACadSharp ile R2004+ binary) ve DXF R12 formatları endüstri standardı
          interop formatlarıdır; bunlara AfneyCAD'e özel keyfi bir JSON bölümü gömmek (DWG'nin
          XRecord/Named Object Dictionary mekanizması dışında) format bütünlüğünü riske atar —
          bu yüzden bilinçli olarak sidecar dosya yaklaşımı seçildi (bkz. SheetSetPersistenceService).

   KULLANIM: TitleBlockDialog açıldığında PeekNextNumber() ile varsayılan bir numara önerilir;
             kullanıcı dilerse elle değiştirebilir. "Antet Ekle" tıklandığında gerçekte kullanılan
             PaftaNo (öneri ya da elle girilen) RegisterSheet() ile indekse kaydedilir ve sayaç
             ilerletilir. MainWindow, her doküman sekmesi (CadDocumentContext) için kendi
             SheetIndexService örneğini tutar (bkz. CadDocumentContext.SheetIndex) — böylece
             farklı projeler birbirinin pafta numaralarını karıştırmaz.
*/
public class SheetIndexService
{
    /// <summary>
    /// Geriye dönük uyumluluk için: uygulama genelinde paylaşılan varsayılan örnek.
    /// Yeni kod, doküman bazlı kalıcılık için CadDocumentContext.SheetIndex kullanmalıdır.
    /// </summary>
    public static SheetIndexService Instance { get; } = new();

    /// <summary>Varsayılan disiplin öneki (proje ayarlarından özelleştirilebilir).</summary>
    public string DefaultDiscipline { get; set; } = "M";

    public class SheetEntry
    {
        public string    Number      { get; set; } = "";
        public string    Discipline  { get; set; } = "";
        public string    Name        { get; set; } = "";        // Çizim adı
        public string    Description { get; set; } = "";        // Proje adı / açıklama
        public DateTime  Registered  { get; set; } = DateTime.Now;

        /// <summary>Sheet Set Manager'da gösterilen durum (ör. "Taslak", "Yayınlandı"). Serbest metin.</summary>
        public string    Status      { get; set; } = "Taslak";
    }

    private readonly Dictionary<string, int> _counters = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<SheetEntry> _sheets = [];

    public IReadOnlyList<SheetEntry> Sheets => _sheets;

    /// <summary>
    /// Bir sonraki numarayı, sayaç ilerletilmeden formatlar (önizleme / varsayılan değer için).
    /// </summary>
    public string PeekNextNumber(string? discipline = null)
    {
        string disc = string.IsNullOrWhiteSpace(discipline) ? DefaultDiscipline : discipline.Trim();
        int next = _counters.GetValueOrDefault(disc, 0) + 1;
        return Format(disc, next);
    }

    /// <summary>
    /// Bir paftayı indekse kaydeder. <paramref name="number"/> boş bırakılırsa (veya PeekNextNumber
    /// ile önerilen değerle aynıysa) sayaç ilerletilerek otomatik numara atanır; kullanıcı elle farklı
    /// bir numara girmişse o numara olduğu gibi kaydedilir (sayaç ise yine de bir sonraki öneri için ilerletilir).
    /// </summary>
    public SheetEntry RegisterSheet(string? number, string name, string description, string? discipline = null)
    {
        string disc = string.IsNullOrWhiteSpace(discipline) ? DefaultDiscipline : discipline.Trim();
        int next = _counters.GetValueOrDefault(disc, 0) + 1;
        string autoNumber = Format(disc, next);

        string finalNumber = string.IsNullOrWhiteSpace(number) ? autoNumber : number.Trim();

        // Sayaç, otomatik öneri kabul edilmiş olsun ya da kullanıcı elle bir numara girmiş olsun
        // her "Antet Ekle" işleminde bir birim ilerler — böylece bir sonraki öneri her zaman
        // "şimdiye kadar üretilen pafta sayısı + 1" olur.
        _counters[disc] = next;

        var entry = new SheetEntry
        {
            Number      = finalNumber,
            Discipline  = disc,
            Name        = name ?? "",
            Description = description ?? "",
            Registered  = DateTime.Now
        };
        _sheets.Add(entry);
        return entry;
    }

    private static string Format(string discipline, int seq) => $"{discipline}-{seq:00}";

    /// <summary>Test/oturum sıfırlama.</summary>
    public void Clear()
    {
        _counters.Clear();
        _sheets.Clear();
    }

    // ── Sheet Set Manager desteği (Session #74) ─────────────────────────────────

    /// <summary>
    /// Kullanıcının Sheet Set Manager'dan elle eklediği bir pafta kaydı — RegisterSheet'ten
    /// farkı, sayaç/otomatik numaralandırmayı hiç etkilememesidir (numara olduğu gibi kaydedilir).
    /// </summary>
    public SheetEntry AddManualEntry(string number, string name, string description, string discipline = "", string status = "Taslak")
    {
        var entry = new SheetEntry
        {
            Number      = number ?? "",
            Discipline  = string.IsNullOrWhiteSpace(discipline) ? DefaultDiscipline : discipline.Trim(),
            Name        = name ?? "",
            Description = description ?? "",
            Status      = string.IsNullOrWhiteSpace(status) ? "Taslak" : status,
            Registered  = DateTime.Now
        };
        _sheets.Add(entry);
        return entry;
    }

    /// <summary>Bir pafta kaydını listeden kaldırır (numaralandırma sayacını etkilemez).</summary>
    public bool RemoveSheet(SheetEntry entry) => _sheets.Remove(entry);

    /// <summary>Bir paftayı listede bir üst sıraya taşır (Sheet Set Manager'da yeniden sıralama için).</summary>
    public bool MoveUp(SheetEntry entry)
    {
        int i = _sheets.IndexOf(entry);
        if (i <= 0) return false;
        (_sheets[i - 1], _sheets[i]) = (_sheets[i], _sheets[i - 1]);
        return true;
    }

    /// <summary>Bir paftayı listede bir alt sıraya taşır (Sheet Set Manager'da yeniden sıralama için).</summary>
    public bool MoveDown(SheetEntry entry)
    {
        int i = _sheets.IndexOf(entry);
        if (i < 0 || i >= _sheets.Count - 1) return false;
        (_sheets[i + 1], _sheets[i]) = (_sheets[i], _sheets[i + 1]);
        return true;
    }

    // ── JSON Serialize / Deserialize (kalıcılık için — Session #74) ─────────────

    private class PersistedState
    {
        public string DefaultDiscipline { get; set; } = "M";
        public Dictionary<string, int> Counters { get; set; } = new();
        public List<SheetEntry> Sheets { get; set; } = [];
    }

    /// <summary>
    /// Servisin tüm durumunu (sayaçlar + pafta listesi) JSON'a dönüştürür.
    /// Proje dosyasıyla birlikte (sidecar dosya olarak) kaydedilmek üzere tasarlanmıştır.
    /// </summary>
    public string ToJson()
    {
        var state = new PersistedState
        {
            DefaultDiscipline = DefaultDiscipline,
            Counters = new Dictionary<string, int>(_counters, StringComparer.OrdinalIgnoreCase),
            Sheets = _sheets
        };
        return JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Daha önce ToJson() ile üretilmiş bir durumu geri yükler. Bozuk/eksik JSON durumunda
    /// mevcut boş duruma sessizce geri döner (proje dosyasının açılmasını engellemez).
    /// </summary>
    public void LoadFromJson(string json)
    {
        try
        {
            var state = JsonSerializer.Deserialize<PersistedState>(json);
            if (state == null) return;

            _counters.Clear();
            foreach (var kv in state.Counters) _counters[kv.Key] = kv.Value;

            _sheets.Clear();
            _sheets.AddRange(state.Sheets);

            if (!string.IsNullOrWhiteSpace(state.DefaultDiscipline))
                DefaultDiscipline = state.DefaultDiscipline;
        }
        catch { /* Bozuk JSON — mevcut (boş) durumla devam et */ }
    }

    // ── Pafta İndeksi (HTML) ─────────────────────────────────────────────────────
    public string BuildIndexHtml(string projectName = "AfneyCAD Projesi")
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("<html><head><meta charset='utf-8'><title>Pafta İndeksi</title></head>");
        sb.Append("<body style='font-family:Segoe UI,Arial,sans-serif;background:#12121a;color:#EEE;padding:20px'>");
        sb.Append($"<h2 style='color:#90CAF9'>Pafta İndeksi — {System.Net.WebUtility.HtmlEncode(projectName)}</h2>");
        sb.Append($"<p style='color:#888;font-size:12px'>Oluşturma: {DateTime.Now:dd.MM.yyyy HH:mm}</p>");
        sb.Append("<table style='border-collapse:collapse;width:100%;font-size:13px'>");
        sb.Append("<tr style='background:#0D3060;color:#90CAF9'>");
        sb.Append("<th style='padding:6px;border:1px solid #333'>Pafta No</th>");
        sb.Append("<th style='padding:6px;border:1px solid #333'>Çizim Adı</th>");
        sb.Append("<th style='padding:6px;border:1px solid #333'>Açıklama / Proje</th>");
        sb.Append("<th style='padding:6px;border:1px solid #333'>Kayıt Tarihi</th>");
        sb.Append("</tr>");

        if (_sheets.Count == 0)
        {
            sb.Append("<tr><td colspan='4' style='padding:10px;text-align:center;color:#888'>" +
                      "Bu projede henüz antet eklenmedi.</td></tr>");
        }

        foreach (var s in _sheets.OrderBy(s => s.Discipline).ThenBy(s => s.Number))
        {
            sb.Append("<tr style='background:#1E1E2E'>");
            sb.Append($"<td style='padding:6px;border:1px solid #333;font-weight:bold;color:#FFD740'>{System.Net.WebUtility.HtmlEncode(s.Number)}</td>");
            sb.Append($"<td style='padding:6px;border:1px solid #333'>{System.Net.WebUtility.HtmlEncode(s.Name)}</td>");
            sb.Append($"<td style='padding:6px;border:1px solid #333'>{System.Net.WebUtility.HtmlEncode(s.Description)}</td>");
            sb.Append($"<td style='padding:6px;border:1px solid #333'>{s.Registered:dd.MM.yyyy HH:mm}</td>");
            sb.Append("</tr>");
        }

        sb.Append("</table></body></html>");
        return sb.ToString();
    }
}
