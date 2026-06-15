using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Proje Revizyon Takip Servisi (RevisionTrackingService)
   NEDEN: FINE MEP'in en kritik eksiklerinden biri olan Rev.A/B/C revizyon yönetimi.
          Mühendislik ofislerinde aynı proje 10-20 kez revize edilir;
          hangi revizyonda ne değiştiğinin belgelenmesi yasal zorunluluk.

   KAPSAM:
   - Revizyon kaydı: numara (A/B/C/D veya 1/2/3), tarih, mühendis, açıklama
   - Değişiklik notu (change notice): ne değişti, neden değişti
   - Durum takibi: Taslak → Kontrol → Onaylandı → Yayınlandı → İptal
   - JSON serialize/deserialize (proje dosyasıyla birlikte saklanır)
   - Revizyon karşılaştırma özeti
*/
public class RevisionTrackingService
{
    // ── Revizyon Durumu ──────────────────────────────────────────────────────────

    public enum RevisionStatus
    {
        Taslak,
        KontrolBekliyor,
        Onaylandı,
        Yayınlandı,
        İptal
    }

    // ── Revizyon Kaydı ───────────────────────────────────────────────────────────

    public class RevisionEntry
    {
        public string         Id           { get; set; } = Guid.NewGuid().ToString("N")[..8];
        public string         RevCode      { get; set; } = "";    // A, B, C veya 1, 2, 3
        public DateTime       Date         { get; set; } = DateTime.Now;
        public string         Engineer     { get; set; } = "";
        public string         Checker      { get; set; } = "";
        public string         Approver     { get; set; } = "";
        public string         Description  { get; set; } = "";    // Kısa başlık
        public string         ChangeNotice { get; set; } = "";    // Detaylı değişiklik notu
        public RevisionStatus Status       { get; set; } = RevisionStatus.Taslak;
        public List<string>   ChangedAreas { get; set; } = [];    // Değişen alan/sistem listesi

        [JsonIgnore]
        public string StatusLabel => Status switch
        {
            RevisionStatus.Taslak           => "📝 Taslak",
            RevisionStatus.KontrolBekliyor  => "🔍 Kontrol",
            RevisionStatus.Onaylandı        => "✅ Onaylandı",
            RevisionStatus.Yayınlandı       => "📤 Yayınlandı",
            RevisionStatus.İptal            => "❌ İptal",
            _                               => ""
        };

        [JsonIgnore]
        public string DateStr => Date.ToString("dd.MM.yyyy");
    }

    // ── Proje Başlık Bilgisi ─────────────────────────────────────────────────────

    public class ProjectTitleBlock
    {
        public string ProjectName    { get; set; } = "";
        public string ProjectNumber  { get; set; } = "";
        public string Client         { get; set; } = "";
        public string DrawingTitle   { get; set; } = "";
        public string DrawingNumber  { get; set; } = "";
        public string Scale          { get; set; } = "1:50";
        public string Phase          { get; set; } = "Uygulama Projesi";
        public string ResponsibleEng { get; set; } = "";
        public string CompanyName    { get; set; } = "";
        public DateTime IssueDate    { get; set; } = DateTime.Now;
    }

    // ── Servis Alanları ──────────────────────────────────────────────────────────

    private readonly List<RevisionEntry> _revisions = [];
    public  ProjectTitleBlock TitleBlock { get; } = new();

    public IReadOnlyList<RevisionEntry> Revisions => _revisions;

    // ── Revizyon Yönetimi ────────────────────────────────────────────────────────

    public RevisionEntry AddRevision(string description, string engineer = "",
                                      string changeNotice = "", RevisionStatus status = RevisionStatus.Taslak)
    {
        string code = NextRevCode();
        var entry = new RevisionEntry
        {
            RevCode      = code,
            Date         = DateTime.Now,
            Engineer     = engineer,
            Description  = description,
            ChangeNotice = changeNotice,
            Status       = status
        };
        _revisions.Add(entry);
        return entry;
    }

    public bool UpdateStatus(string id, RevisionStatus newStatus,
                              string checker = "", string approver = "")
    {
        var rev = _revisions.FirstOrDefault(r => r.Id == id);
        if (rev == null) return false;

        rev.Status   = newStatus;
        if (!string.IsNullOrEmpty(checker))  rev.Checker  = checker;
        if (!string.IsNullOrEmpty(approver)) rev.Approver = approver;
        if (newStatus == RevisionStatus.Yayınlandı) rev.Date = DateTime.Now;
        return true;
    }

    public bool DeleteRevision(string id)
    {
        var rev = _revisions.FirstOrDefault(r => r.Id == id);
        if (rev == null || rev.Status == RevisionStatus.Yayınlandı) return false;
        return _revisions.Remove(rev);
    }

    // ── Aktif / Son Revizyon ─────────────────────────────────────────────────────

    public RevisionEntry? CurrentRevision =>
        _revisions.LastOrDefault(r => r.Status == RevisionStatus.Yayınlandı)
        ?? _revisions.LastOrDefault();

    public string CurrentRevCode => CurrentRevision?.RevCode ?? "—";

    // ── Karşılaştırma Özeti ──────────────────────────────────────────────────────

    public string CompareRevisions(string fromCode, string toCode)
    {
        var from = _revisions.FirstOrDefault(r => r.RevCode == fromCode);
        var to   = _revisions.FirstOrDefault(r => r.RevCode == toCode);
        if (from == null || to == null) return "Revizyon bulunamadı.";

        return $"Rev.{fromCode} → Rev.{toCode}\n" +
               $"Süre: {from.Date:dd.MM.yyyy} → {to.Date:dd.MM.yyyy}\n" +
               $"Değişiklik: {to.Description}\n" +
               $"Detay: {to.ChangeNotice}\n" +
               $"Değişen alanlar: {string.Join(", ", to.ChangedAreas)}";
    }

    // ── Revizyon Tablosu (Pafta için) ────────────────────────────────────────────

    public string BuildRevisionTableHtml()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("<table style='border-collapse:collapse;width:100%;font-size:11px'>");
        sb.Append("<tr style='background:#0D3060;color:#90CAF9'>");
        sb.Append("<th style='padding:5px;border:1px solid #333'>Rev.</th>");
        sb.Append("<th style='padding:5px;border:1px solid #333'>Tarih</th>");
        sb.Append("<th style='padding:5px;border:1px solid #333'>Açıklama</th>");
        sb.Append("<th style='padding:5px;border:1px solid #333'>Mühendis</th>");
        sb.Append("<th style='padding:5px;border:1px solid #333'>Kontrol</th>");
        sb.Append("<th style='padding:5px;border:1px solid #333'>Onay</th>");
        sb.Append("<th style='padding:5px;border:1px solid #333'>Durum</th>");
        sb.Append("</tr>");

        foreach (var r in _revisions.OrderBy(r => r.RevCode))
        {
            string bg = r.Status == RevisionStatus.Yayınlandı ? "#1B3A1B" :
                        r.Status == RevisionStatus.Onaylandı  ? "#1A2B0A" :
                        r.Status == RevisionStatus.İptal       ? "#3A1A1A" : "#1E1E2E";
            sb.Append($"<tr style='background:{bg}'>");
            sb.Append($"<td style='padding:5px;border:1px solid #333;font-weight:bold;color:#FFD740'>{r.RevCode}</td>");
            sb.Append($"<td style='padding:5px;border:1px solid #333'>{r.DateStr}</td>");
            sb.Append($"<td style='padding:5px;border:1px solid #333'>{r.Description}</td>");
            sb.Append($"<td style='padding:5px;border:1px solid #333'>{r.Engineer}</td>");
            sb.Append($"<td style='padding:5px;border:1px solid #333'>{r.Checker}</td>");
            sb.Append($"<td style='padding:5px;border:1px solid #333'>{r.Approver}</td>");
            sb.Append($"<td style='padding:5px;border:1px solid #333'>{r.StatusLabel}</td>");
            sb.Append("</tr>");
        }
        sb.Append("</table>");
        return sb.ToString();
    }

    // ── JSON Serialize / Deserialize ─────────────────────────────────────────────

    public string ToJson()
    {
        var data = new { TitleBlock, Revisions = _revisions };
        return JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
    }

    public void LoadFromJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            _revisions.Clear();

            if (doc.RootElement.TryGetProperty("Revisions", out var revArr))
            {
                foreach (var el in revArr.EnumerateArray())
                {
                    var rev = JsonSerializer.Deserialize<RevisionEntry>(el.GetRawText());
                    if (rev != null) _revisions.Add(rev);
                }
            }
        }
        catch { /* Corrupt JSON — start fresh */ }
    }

    // ── Yardımcı: Revizyon Kodu Üretici ─────────────────────────────────────────

    private string NextRevCode()
    {
        if (_revisions.Count == 0) return "A";

        var last = _revisions.Last().RevCode;

        // Harf tabanlı: A → B → ... → Z → AA → AB
        if (last.All(char.IsLetter))
        {
            char[] chars = last.ToCharArray();
            for (int i = chars.Length - 1; i >= 0; i--)
            {
                if (chars[i] < 'Z') { chars[i]++; return new string(chars); }
                chars[i] = 'A';
            }
            return "A" + new string(chars);
        }

        // Sayı tabanlı: 1 → 2 → 3
        if (int.TryParse(last, out int n)) return (n + 1).ToString();

        return ((_revisions.Count + 1).ToString());
    }

    // ── Standart Revizyon Nedenleri ──────────────────────────────────────────────

    public static readonly string[] StandardChangeReasons =
    [
        "İlk Yayın",
        "Mimari Değişikliğe Göre Revize",
        "Müşteri Talebi",
        "İdare Görüşü",
        "Saha Tespiti",
        "Hesap Revizyonu",
        "Boru Güzergahı Değişikliği",
        "Ekipman Değişikliği",
        "Standart Güncellemesi",
        "Mühendis Revizyonu",
    ];
}
