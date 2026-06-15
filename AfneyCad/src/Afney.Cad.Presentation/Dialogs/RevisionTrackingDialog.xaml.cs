using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs;

public partial class RevisionTrackingDialog
{
    private readonly RevisionTrackingService _svc;

    public RevisionTrackingDialog(RevisionTrackingService service)
    {
        InitializeComponent();
        _svc = service;

        foreach (var r in RevisionTrackingService.StandardChangeReasons)
            CboReason.Items.Add(new ComboBoxItem { Content = r });
        CboReason.SelectedIndex = 0;

        LoadTitleBlock();
        RefreshGrid();
    }

    // ── Revizyon Listesi ─────────────────────────────────────────────────────────

    private void RefreshGrid()
    {
        RevGrid.ItemsSource = null;
        RevGrid.ItemsSource = _svc.Revisions;
        StatusText.Text = $"Toplam {_svc.Revisions.Count} revizyon · Aktif: Rev.{_svc.CurrentRevCode}";
    }

    private void RevGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RevGrid.SelectedItem is not RevisionTrackingService.RevisionEntry rev) return;
        DetailRevCode.Text  = $"Rev. {rev.RevCode}";
        DetailStatus.Text   = rev.StatusLabel;
        DetailDesc.Text     = rev.Description;
        DetailNotice.Text   = string.IsNullOrEmpty(rev.ChangeNotice) ? "(Detay girilmemiş)" : rev.ChangeNotice;
        DetailAreas.Text    = rev.ChangedAreas.Count > 0 ? string.Join(", ", rev.ChangedAreas) : "—";
    }

    // ── Revizyon Ekleme ──────────────────────────────────────────────────────────

    private void AddRevision_Click(object sender, RoutedEventArgs e)
    {
        string desc = TxtDesc.Text.Trim();
        if (string.IsNullOrEmpty(desc)) { StatusText.Text = "⚠ Revizyon açıklaması boş olamaz."; return; }

        string reason = (CboReason.SelectedItem as ComboBoxItem)?.Content?.ToString()
                        ?? CboReason.Text;
        string notice   = TxtChangeNotice.Text.Trim();
        if (!string.IsNullOrEmpty(reason) && !notice.Contains(reason))
            notice = $"Neden: {reason}\n{notice}";

        var status = CboInitStatus.SelectedIndex switch
        {
            1 => RevisionTrackingService.RevisionStatus.KontrolBekliyor,
            2 => RevisionTrackingService.RevisionStatus.Onaylandı,
            3 => RevisionTrackingService.RevisionStatus.Yayınlandı,
            _ => RevisionTrackingService.RevisionStatus.Taslak
        };

        var rev = _svc.AddRevision(desc, TxtEngineer.Text.Trim(), notice, status);
        rev.Checker  = TxtChecker.Text.Trim();
        rev.Approver = TxtApprover.Text.Trim();

        string areas = TxtChangedAreas.Text;
        if (!string.IsNullOrEmpty(areas))
            rev.ChangedAreas.AddRange(areas.Split(',').Select(a => a.Trim()).Where(a => a.Length > 0));

        // Formu temizle
        TxtDesc.Text = TxtChangeNotice.Text = TxtChangedAreas.Text = "";
        TxtEngineer.Text = TxtChecker.Text = TxtApprover.Text = "";
        CboInitStatus.SelectedIndex = 0;

        RefreshGrid();
        StatusText.Text = $"✓ Rev.{rev.RevCode} eklendi.";
    }

    // ── Revizyon Düzenleme (satıra çift tıklama) ────────────────────────────────

    private void EditRevision_Click(object sender, RoutedEventArgs e)
    {
        if (RevGrid.SelectedItem is not RevisionTrackingService.RevisionEntry rev) return;
        TxtDesc.Text         = rev.Description;
        TxtChangeNotice.Text = rev.ChangeNotice;
        TxtChangedAreas.Text = string.Join(", ", rev.ChangedAreas);
        TxtEngineer.Text     = rev.Engineer;
        TxtChecker.Text      = rev.Checker;
        TxtApprover.Text     = rev.Approver;
        StatusText.Text      = $"Rev.{rev.RevCode} düzenleme modunda — değiştirip 'Revizyon Ekle'ye basın (yeni Rev. olarak kaydedilir)";
    }

    // ── Silme ────────────────────────────────────────────────────────────────────

    private void DeleteRevision_Click(object sender, RoutedEventArgs e)
    {
        if (RevGrid.SelectedItem is not RevisionTrackingService.RevisionEntry rev) return;
        if (rev.Status == RevisionTrackingService.RevisionStatus.Yayınlandı)
        {
            StatusText.Text = "⚠ Yayınlanmış revizyon silinemez."; return;
        }
        if (MessageBox.Show($"Rev.{rev.RevCode} silinsin mi?", "Onay",
            MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            _svc.DeleteRevision(rev.Id);
            RefreshGrid();
        }
    }

    // ── Durum Değiştirme ─────────────────────────────────────────────────────────

    private void Approve_Click(object sender, RoutedEventArgs e) => ChangeStatus(RevisionTrackingService.RevisionStatus.Onaylandı);
    private void Publish_Click(object sender, RoutedEventArgs e) => ChangeStatus(RevisionTrackingService.RevisionStatus.Yayınlandı);
    private void Cancel_Click(object sender, RoutedEventArgs e)  => ChangeStatus(RevisionTrackingService.RevisionStatus.İptal);

    private void ChangeStatus(RevisionTrackingService.RevisionStatus status)
    {
        if (RevGrid.SelectedItem is not RevisionTrackingService.RevisionEntry rev) return;
        _svc.UpdateStatus(rev.Id, status);
        RefreshGrid();
        StatusText.Text = $"✓ Rev.{rev.RevCode} → {status}";
    }

    // ── Proje Başlık Bloğu Yükle/Kaydet ─────────────────────────────────────────

    private void LoadTitleBlock()
    {
        var tb = _svc.TitleBlock;
        TxtProjName.Text     = tb.ProjectName;
        TxtProjNumber.Text   = tb.ProjectNumber;
        TxtClient.Text       = tb.Client;
        TxtDrawingTitle.Text = tb.DrawingTitle;
        TxtDrawingNumber.Text = tb.DrawingNumber;
        TxtRespEng.Text      = tb.ResponsibleEng;
        TxtCompany.Text      = tb.CompanyName;
    }

    private void SaveTitleBlock()
    {
        var tb = _svc.TitleBlock;
        tb.ProjectName    = TxtProjName.Text.Trim();
        tb.ProjectNumber  = TxtProjNumber.Text.Trim();
        tb.Client         = TxtClient.Text.Trim();
        tb.DrawingTitle   = TxtDrawingTitle.Text.Trim();
        tb.DrawingNumber  = TxtDrawingNumber.Text.Trim();
        tb.ResponsibleEng = TxtRespEng.Text.Trim();
        tb.CompanyName    = TxtCompany.Text.Trim();
        tb.Phase          = (CboPhase.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
        tb.Scale          = CboScale.Text;
    }

    // ── JSON Kaydet / Yükle ──────────────────────────────────────────────────────

    private void SaveJson_Click(object sender, RoutedEventArgs e)
    {
        SaveTitleBlock();
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Revizyon Dosyası Kaydet", Filter = "JSON|*.rev.json",
            FileName = $"{_svc.TitleBlock.ProjectName}_Revizyon.rev.json"
        };
        if (dlg.ShowDialog() != true) return;
        File.WriteAllText(dlg.FileName, _svc.ToJson(), Encoding.UTF8);
        StatusText.Text = $"✓ Kaydedildi: {System.IO.Path.GetFileName(dlg.FileName)}";
    }

    private void LoadJson_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "JSON|*.rev.json|Tümü|*.*" };
        if (dlg.ShowDialog() != true) return;
        _svc.LoadFromJson(File.ReadAllText(dlg.FileName, Encoding.UTF8));
        LoadTitleBlock();
        RefreshGrid();
        StatusText.Text = $"✓ Yüklendi: {System.IO.Path.GetFileName(dlg.FileName)}";
    }

    // ── HTML Rapor ───────────────────────────────────────────────────────────────

    private void ExportReport_Click(object sender, RoutedEventArgs e)
    {
        SaveTitleBlock();
        var tb = _svc.TitleBlock;
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html><head><meta charset='utf-8'><title>Revizyon Raporu</title>");
        sb.Append("<style>body{font-family:Arial;background:#0D1117;color:#ddd;padding:20px}");
        sb.Append("h1{color:#FFD740}h2{color:#90CAF9}");
        sb.Append(".box{background:#0A1020;padding:12px;border-radius:4px;margin:10px 0}");
        sb.Append("</style></head><body>");
        sb.Append($"<h1>📋 {tb.ProjectName} — Revizyon Takip Föyü</h1>");
        sb.Append($"<div class='box'><b>Proje No:</b> {tb.ProjectNumber} &nbsp;|&nbsp; ");
        sb.Append($"<b>İşveren:</b> {tb.Client} &nbsp;|&nbsp; ");
        sb.Append($"<b>Pafta:</b> {tb.DrawingTitle} ({tb.DrawingNumber}) &nbsp;|&nbsp; ");
        sb.Append($"<b>Ölçek:</b> {tb.Scale} &nbsp;|&nbsp; ");
        sb.Append($"<b>Aşama:</b> {tb.Phase}<br/>");
        sb.Append($"<b>Sorumlu Müh:</b> {tb.ResponsibleEng} &nbsp;|&nbsp; ");
        sb.Append($"<b>Firma:</b> {tb.CompanyName} &nbsp;|&nbsp; ");
        sb.Append($"<b>Tarih:</b> {DateTime.Now:dd.MM.yyyy HH:mm}</div>");
        sb.Append("<h2>Revizyon Tablosu</h2>");
        sb.Append(_svc.BuildRevisionTableHtml());

        if (_svc.Revisions.Any(r => !string.IsNullOrEmpty(r.ChangeNotice)))
        {
            sb.Append("<h2>Değişiklik Notları</h2>");
            foreach (var r in _svc.Revisions.Where(rv => !string.IsNullOrEmpty(rv.ChangeNotice)))
            {
                sb.Append($"<div class='box'><b style='color:#FFD740'>Rev.{r.RevCode}</b> — {r.Description}<br/>");
                sb.Append($"<pre style='white-space:pre-wrap;color:#AAA'>{r.ChangeNotice}</pre></div>");
            }
        }
        sb.Append("</body></html>");

        string path = Path.Combine(Path.GetTempPath(), $"Revizyon_{tb.ProjectName}_{DateTime.Now:yyyyMMdd}.html");
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = path, UseShellExecute = true });
        StatusText.Text = $"✓ Rapor açıldı.";
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
