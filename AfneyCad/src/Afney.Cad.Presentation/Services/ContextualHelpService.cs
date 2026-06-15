using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;

namespace Afney.Cad.Presentation.Services;

/*
   NE: Bağlamsal Yardım Servisi (ContextualHelpService)
   NEDEN: FINE MEP'te F1 yardım sistemi var — AfneyCAD'de yoktu.
          Her dialog / araç için bağlama özgü yardım içeriği,
          F1 tuşuna basılınca tarayıcıda açılır.
*/
public class ContextualHelpService
{
    // ── Yardım Konuları ──────────────────────────────────────────────────────────

    public record HelpTopic(string Key, string Title, string Content, string[] Keywords);

    public static readonly List<HelpTopic> Topics =
    [
        new("cooling",      "Soğutma Yük Hesabı",
            """
            <h2>❄️ Soğutma Yük Hesabı</h2>
            <p>ASHRAE HOF 2021 / TS EN 12831-3 yöntemiyle hesaplanır.</p>
            <h3>Girdi Parametreleri</h3>
            <ul>
            <li><b>Dış Tasarım Sıcaklığı:</b> Şehir seçilince otomatik dolar. ASHRAE %1 dışında tasarım sıcaklığı.</li>
            <li><b>SHGC:</b> Solar Isı Kazanım Katsayısı (0-1). Çift camlı pencere ≈ 0.25-0.40.</li>
            <li><b>Kişi Başı Yük:</b> Ofis: Duyulur 75W + Gizli 55W, Restoran: 70W+45W.</li>
            </ul>
            <h3>Formüller</h3>
            <ul>
            <li>Transmisyon: Q = U × A × CLTD</li>
            <li>Solar: Q = SHGC × A × SolarIntensity × ShadingFactor</li>
            <li>İç yük: Q = kişi×duyulur + alan×aydınlatma×0.85 + ekipman×0.90</li>
            </ul>
            <h3>Sonuçlar</h3>
            <p>kW ve TR (ton of refrigeration = 3.517 kW) cinsinden gösterilir. SHR (Sensible Heat Ratio) > 0.75 tipik ofis değeridir.</p>
            """,
            ["soğutma", "SHGC", "CLTD", "klima", "soğutma yükü"]),

        new("heating",      "Isıtma Yük Hesabı",
            """
            <h2>🔥 Isıtma Yük Hesabı</h2>
            <p>TS EN 12831 yöntemine göre ısıtma tasarım yükü hesabı.</p>
            <h3>Hesap Adımları</h3>
            <ol>
            <li>Yapı elemanlarının U-değerleri girilir (duvar, çatı, zemin, pencere)</li>
            <li>İnfiltrasyon yükü: HV = 0.34 × n × V × ΔT</li>
            <li>Güneş ışınımı ve iç kazanımlar düşürülür</li>
            </ol>
            <h3>Önemli Notlar</h3>
            <p>TS 825 bölge sınırları: 1=İstanbul/İzmir, 2=Bursa/Ankara, 3=Erzurum bölgesi.</p>
            """,
            ["ısıtma", "U-değeri", "HDD", "ısı kaybı", "kalorifer"]),

        new("pump",         "Pompa Seçimi",
            """
            <h2>💧 Pompa Seçimi</h2>
            <p>Grundfos ve Wilo pompa kataloglarından Q ve H değerlerine göre optimum pompa seçimi.</p>
            <h3>Q/H Eğrisi</h3>
            <p>Pompanın Q (debi, m³/sa) ve H (basma yüksekliği, m) ilişkisi. Sistem eğrisi ile kesişim noktası çalışma noktasıdır.</p>
            <h3>Güvenlik Payı</h3>
            <ul>
            <li>Debi: %10-15 üstünde bir pompa seçin</li>
            <li>Basınç: %20 üstünde hesaplayın</li>
            </ul>
            <h3>Verim</h3>
            <p>İyi pompa verimi: %60+. Yüksek verim = düşük işletme maliyeti. Çalışma noktası verim tepe noktasına yakın olmalı.</p>
            """,
            ["pompa", "Q/H", "debi", "basınç", "Grundfos", "Wilo"]),

        new("fan",          "Fan Seçimi",
            """
            <h2>💨 Fan Seçimi</h2>
            <p>HVAC sistemlerinde hava taşıma ve egzoz için fan seçimi.</p>
            <h3>SFP (Specific Fan Power)</h3>
            <p>EN 13779'a göre sınıflandırma:</p>
            <ul>
            <li>SFP-1: ≤500 W/(m³/s) — Çok iyi</li>
            <li>SFP-2: ≤750 W/(m³/s) — İyi</li>
            <li>SFP-3: ≤1250 W/(m³/s) — Ortalama</li>
            <li>SFP-4: ≤2000 W/(m³/s) — Kötü</li>
            </ul>
            <h3>Tip Seçimi</h3>
            <ul>
            <li>Aksiyal: Düşük basınç, yüksek debi (egzoz)</li>
            <li>Santrifüj: Yüksek basınç (AHU, uzun kanal)</li>
            <li>ERV: Isı geri kazanım ünitesi</li>
            </ul>
            """,
            ["fan", "SFP", "aksiyal", "santrifüj", "havalandırma"]),

        new("sprinkler",    "Sprinkler Hesabı",
            """
            <h2>🔥 NFPA 13 Sprinkler Tasarımı</h2>
            <p>NFPA 13 Yoğunluk/Alan metodu — otomatik sprinkler sistemi hesabı.</p>
            <h3>Tehlike Sınıfları</h3>
            <ul>
            <li><b>Hafif (LH):</b> Ofis, konut, otel — 4.1 L/(dak·m²)</li>
            <li><b>Orta-1 (OH1):</b> Otopark, kantin — 6.1 L/(dak·m²)</li>
            <li><b>Orta-2 (OH2):</b> Üretim tesisi — 8.2 L/(dak·m²)</li>
            <li><b>Yüksek (EH):</b> Boya deposu, yanıcı sıvı — 12-16 L/(dak·m²)</li>
            </ul>
            <h3>K-Faktörü</h3>
            <p>q = K × √P formülüyle hesaplanır. Standart sprinkler K=80 (L/dak/√bar).</p>
            """,
            ["sprinkler", "NFPA", "yangın", "K-faktörü", "yoğunluk"]),

        new("energy",       "Enerji Performansı (EKB)",
            """
            <h2>⚡ Enerji Kimlik Belgesi (EKB)</h2>
            <p>EPBD / TS 825:2023 — bina enerji sertifikası hesabı.</p>
            <h3>Sınıflar (kWh/m²yıl birincil enerji)</h3>
            <ul>
            <li>A++: ≤25 · A+: ≤50 · A: ≤75 · B: ≤100 · C: ≤125 · D: ≤150 · E: ≤175 · F: ≤225 · G: >225</li>
            </ul>
            <h3>Birincil Enerji Faktörleri (Türkiye)</h3>
            <ul>
            <li>Doğalgaz: fp = 1.05</li>
            <li>Elektrik: fp = 2.50</li>
            </ul>
            <h3>İyileştirme Öncelikleri</h3>
            <p>1. Yalıtım (çatı > duvar > zemin) · 2. Pencere U-değeri · 3. Sistem verimi · 4. Güneş enerjisi</p>
            """,
            ["EKB", "enerji", "EPBD", "TS 825", "sertifika", "birincil enerji"]),

        new("heatpump",     "Isı Pompası",
            """
            <h2>🌡️ Isı Pompası Seçimi (TS EN 14825)</h2>
            <h3>Temel Kavramlar</h3>
            <ul>
            <li><b>COP:</b> Anlık performans katsayısı (çalışma noktasında)</li>
            <li><b>SCOP:</b> Mevsimsel ısıtma performansı — gerçekçi yıllık enerji göstergesi</li>
            <li><b>SEER:</b> Mevsimsel soğutma performansı</li>
            </ul>
            <h3>Standart Çalışma Noktaları</h3>
            <ul>
            <li>A7/W35: +7°C dış, 35°C su — ısıtma</li>
            <li>A35/W18: +35°C dış, 18°C su — soğutma</li>
            </ul>
            <h3>Soğutucu Akışkanlar</h3>
            <ul>
            <li>R32: GWP=675 — yaygın, dengeli</li>
            <li>R290 (propan): GWP=3 — çevreci, güvenli dozaj gerekli</li>
            <li>R410A: GWP=2088 — F-gaz kısıtlaması kapsamında</li>
            </ul>
            """,
            ["ısı pompası", "COP", "SCOP", "SEER", "R32", "Daikin", "Vaillant"]),

        new("floorheating", "Yerden Isıtma",
            """
            <h2>🌡️ Yerden Isıtma (TS EN 1264)</h2>
            <h3>Tasarım Parametreleri</h3>
            <ul>
            <li><b>Besleme T:</b> Tipik 35-45°C. Yerden ısıtma için 35°C optimum.</li>
            <li><b>Boru Aralığı:</b> 75mm, 100mm, 150mm veya 200mm (standart aralıklar)</li>
            <li><b>Maks Devre:</b> 100m (basınç kaybı yönetimi). Büyük alanlar birden fazla devre.</li>
            </ul>
            <h3>Zemin Kaplaması Etkisi</h3>
            <p>Seramik: R=0 → en iyi. Halı: R=0.15 m²K/W → %30+ kapasite kaybı.</p>
            <h3>Kolektör</h3>
            <p>Her devre kolektörde ayrı vana ile balans yapılır. Kolektör boyutu = toplam devre sayısı.</p>
            """,
            ["yerden ısıtma", "radyan", "devre", "kolektör", "PEXa"]),

        new("revision",     "Revizyon Takibi",
            """
            <h2>📋 Proje Revizyon Yönetimi</h2>
            <p>Rev.A/B/C/D... akışı ile mühendislik belgelerini takip edin.</p>
            <h3>Revizyon Akışı</h3>
            <p>Taslak → Kontrol Bekliyor → Onaylandı → Yayınlandı → (İptal)</p>
            <h3>Yayınlanmış Revizyon</h3>
            <p>⚠ Yayınlanmış revizyon silinemez — sadece yeni revizyon eklenebilir.</p>
            <h3>JSON Kaydet</h3>
            <p>*.rev.json dosyası proje klasörüne kaydedilir ve proje arşivinde saklanır.</p>
            """,
            ["revizyon", "Rev.A", "onay", "yayın", "değişiklik"]),

        new("main",         "AfneyCAD Genel",
            """
            <h2>🏗️ AfneyCAD v3.5 — MEP CAD Platformu</h2>
            <h3>Temel Araçlar</h3>
            <ul>
            <li><b>Boru Çizimi:</b> Sol panelden sistem seçin → ekrana tıklayarak çizin</li>
            <li><b>Boyutlandırma:</b> F5 ile Boru Boyutlandırma (Darcy-Weisbach)</li>
            <li><b>Isı Kaybı:</b> F6 ile ısıtma yükü hesabı</li>
            <li><b>Axonometrik:</b> 3D aksonometrik görünüm SVG/HTML çıktı</li>
            </ul>
            <h3>Klavye Kısayolları</h3>
            <ul>
            <li>F1: Yardım · F5: Boyutlandırma · F6: Isıtma · Ctrl+S: Kaydet · Ctrl+Z: Geri Al</li>
            <li>Del: Seçili sil · Esc: İptal · Space: Pan modu</li>
            </ul>
            <h3>Destek</h3>
            <p>GitHub: github.com/ibrahimkemalkoyuncu/AfneyCad</p>
            """,
            ["genel", "klavye", "kısayol", "yardım"])
    ];

    // ── Konu Bulma ────────────────────────────────────────────────────────────────

    public static HelpTopic? FindTopic(string key) =>
        Topics.Find(t => t.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

    public static HelpTopic? SearchTopic(string query)
    {
        query = query.ToLowerInvariant();
        return Topics.Find(t =>
            t.Key.Contains(query) || t.Title.ToLower().Contains(query) ||
            System.Array.Exists(t.Keywords, k => k.Contains(query)));
    }

    // ── HTML Yardım Sayfası Oluştur ──────────────────────────────────────────────

    public static string BuildHelpHtml(HelpTopic topic)
    {
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html><head><meta charset='utf-8'><title>AfneyCAD Yardım</title>");
        sb.Append("<style>body{font-family:Segoe UI,Arial;background:#0D1117;color:#ddd;padding:20px;max-width:800px}");
        sb.Append("h2{color:#FFD740}h3{color:#90CAF9}ul,ol{line-height:2}");
        sb.Append("b{color:#A5D6A7}code{background:#1E1E2E;padding:2px 6px;border-radius:3px}");
        sb.Append(".tag{display:inline-block;background:#0D3060;color:#90CAF9;padding:2px 8px;border-radius:10px;font-size:11px;margin:2px}");
        sb.Append("</style></head><body>");
        sb.Append($"<p style='color:#555;font-size:11px'>AfneyCAD v3.5 · F1 Bağlamsal Yardım</p>");
        sb.Append(topic.Content);
        sb.Append("<hr style='border-color:#333;margin-top:20px'/>");
        sb.Append("<p style='font-size:11px;color:#555'>Anahtar Kelimeler: ");
        foreach (var kw in topic.Keywords)
            sb.Append($"<span class='tag'>{kw}</span> ");
        sb.Append("</p></body></html>");
        return sb.ToString();
    }

    // ── F1 Tetikleyici ────────────────────────────────────────────────────────────

    public static void ShowHelp(string topicKey = "main")
    {
        var topic = FindTopic(topicKey) ?? FindTopic("main")!;
        string html = BuildHelpHtml(topic);
        string path = Path.Combine(Path.GetTempPath(), $"AfneyHelp_{topicKey}.html");
        File.WriteAllText(path, html, Encoding.UTF8);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = path, UseShellExecute = true
        });
    }

    // ── WPF F1 Hook ──────────────────────────────────────────────────────────────

    public static void RegisterF1(Window window, string topicKey)
    {
        window.KeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.F1)
            {
                ShowHelp(topicKey);
                e.Handled = true;
            }
        };
    }
}
