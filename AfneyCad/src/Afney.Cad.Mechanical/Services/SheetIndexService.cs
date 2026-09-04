using System;
using System.Collections.Generic;
using System.Linq;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Pafta İndeksi Servisi (SheetIndexService)
   NEDEN: Denetim raporunda tespit edildi — TitleBlockService.PaftaNo tamamen serbest-metin
          bir alandı; kullanıcı her antet için elle bir numara yazıyordu. Birden fazla pafta
          (kat/sistem/rapor bazlı) üretilen projelerde bu, çakışan/tutarsız numaralara yol açar.

   NASIL: Oturum (session) ömürlü, disipline göre seri artan numara üreten basit bir sayaç +
          üretilen paftaların (numara/isim/açıklama) bir listesi. Kalıcı proje dosyasında
          "pafta" kavramı birinci sınıf bir varlık olarak saklanmadığından (her antet çağrısı
          bağımsız bir TitleBlockDialog örneği kullanıyor, MDI sekmeleri kalıcı değil), bu
          servis KASITLI olarak sadece çalışma oturumu boyunca geçerlidir — uygulama yeniden
          başlatıldığında sayaç sıfırlanır. Bu, mimariyi büyük ölçüde değiştirmeden güvenle
          uygulanabilecek dar kapsamlı bir çözümdür.

   KULLANIM: TitleBlockDialog açıldığında PeekNextNumber() ile varsayılan bir numara önerilir;
             kullanıcı dilerse elle değiştirebilir. "Antet Ekle" tıklandığında gerçekte kullanılan
             PaftaNo (öneri ya da elle girilen) RegisterSheet() ile indekse kaydedilir ve sayaç
             ilerletilir.
*/
public class SheetIndexService
{
    /// <summary>
    /// Uygulama genelinde tek bir oturum boyunca paylaşılan pafta indeksi.
    /// (Kalıcı depolama yok — bkz. sınıf açıklaması.)
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
                      "Bu oturumda henüz antet eklenmedi.</td></tr>");
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
