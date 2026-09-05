using Afney.Cad.Mechanical.Services;
using Xunit;

namespace Afney.Cad.Tests.Mechanical;

/*
   NE: Pafta İndeksi Servisi Test Birimi (SheetIndexServiceTests)
   NEDEN: Denetim raporu, TitleBlockService.PaftaNo'nun tamamen serbest-metin olduğunu ve
          otomatik seri numaralandırma/pafta indeksinin bulunmadığını tespit etti.
          SheetIndexService bu eksiği (dar kapsamlı, oturum ömürlü) kapatır — bu testler
          seri numara üretimini, kullanıcı override'ını ve indeks HTML çıktısını doğrular.
*/
public class SheetIndexServiceTests
{
    [Fact]
    public void PeekNextNumber_FirstCall_ReturnsDiscipline01()
    {
        var svc = new SheetIndexService();
        Assert.Equal("M-01", svc.PeekNextNumber());
    }

    [Fact]
    public void PeekNextNumber_DoesNotAdvanceCounter()
    {
        var svc = new SheetIndexService();
        Assert.Equal("M-01", svc.PeekNextNumber());
        Assert.Equal("M-01", svc.PeekNextNumber()); // Peek yan etkisiz olmalı
    }

    [Fact]
    public void RegisterSheet_WithNullNumber_AssignsSequentialAuto()
    {
        var svc = new SheetIndexService();

        var s1 = svc.RegisterSheet(null, "Zemin Kat Tesisat", "Proje A");
        var s2 = svc.RegisterSheet(null, "1. Kat Tesisat", "Proje A");
        var s3 = svc.RegisterSheet(null, "2. Kat Tesisat", "Proje A");

        Assert.Equal("M-01", s1.Number);
        Assert.Equal("M-02", s2.Number);
        Assert.Equal("M-03", s3.Number);
        Assert.Equal(3, svc.Sheets.Count);
    }

    [Fact]
    public void RegisterSheet_WithManualOverride_KeepsUserValueButAdvancesCounter()
    {
        var svc = new SheetIndexService();

        var manual = svc.RegisterSheet("CUSTOM-99", "Özel Pafta", "Proje B");
        Assert.Equal("CUSTOM-99", manual.Number);

        // Sayaç yine de ilerlemiş olmalı — bir sonraki öneri M-02 olmalı (M-01 "tüketildi").
        Assert.Equal("M-02", svc.PeekNextNumber());
    }

    [Fact]
    public void RegisterSheet_SeparateDisciplines_HaveIndependentCounters()
    {
        var svc = new SheetIndexService();

        var m1 = svc.RegisterSheet(null, "Mekanik Plan", "", "M");
        var e1 = svc.RegisterSheet(null, "Elektrik Plan", "", "E");
        var m2 = svc.RegisterSheet(null, "Mekanik Plan 2", "", "M");

        Assert.Equal("M-01", m1.Number);
        Assert.Equal("E-01", e1.Number);
        Assert.Equal("M-02", m2.Number);
    }

    [Fact]
    public void PeekNextNumber_RespectsCustomDefaultDiscipline()
    {
        var svc = new SheetIndexService { DefaultDiscipline = "MEP" };
        Assert.Equal("MEP-01", svc.PeekNextNumber());
    }

    [Fact]
    public void BuildIndexHtml_EmptyIndex_ShowsPlaceholderRow()
    {
        var svc = new SheetIndexService();
        string html = svc.BuildIndexHtml("Test Projesi");

        Assert.Contains("Pafta İndeksi", html);
        Assert.Contains("Test Projesi", html);
        Assert.Contains("henüz antet eklenmedi", html);
    }

    [Fact]
    public void BuildIndexHtml_WithSheets_ListsNumberAndName()
    {
        // Not: WebUtility.HtmlEncode ASCII dışı karakterleri (Ö, ı, ş...) sayısal karakter
        // referansına (&#xxx;) çevirir — bu yüzden test içeriğinde ASCII harfler kullanılıyor;
        // gerçek uygulamada Türkçe karakterler tarayıcıda doğru render edilir (encode edilmiş de olsa).
        var svc = new SheetIndexService();
        svc.RegisterSheet(null, "Zemin Kat Tesisat Plani", "Ornek Proje");

        string html = svc.BuildIndexHtml();

        Assert.Contains("M-01", html);
        Assert.Contains("Zemin Kat Tesisat Plani", html);
        Assert.Contains("Ornek Proje", html);
    }

    [Fact]
    public void Clear_ResetsCountersAndSheets()
    {
        var svc = new SheetIndexService();
        svc.RegisterSheet(null, "A", "B");
        Assert.Single(svc.Sheets);

        svc.Clear();

        Assert.Empty(svc.Sheets);
        Assert.Equal("M-01", svc.PeekNextNumber());
    }

    [Fact]
    public void Instance_IsSingletonAcrossCalls()
    {
        Assert.Same(SheetIndexService.Instance, SheetIndexService.Instance);
    }

    // ── Kalıcılık (Session #74: ToJson/LoadFromJson) ────────────────────────────

    [Fact]
    public void ToJson_ThenLoadFromJson_RestoresSheetsAndCounters()
    {
        var svc = new SheetIndexService();
        svc.RegisterSheet(null, "Zemin Kat Tesisat", "Proje A");
        svc.RegisterSheet(null, "1. Kat Tesisat", "Proje A", "E");

        string json = svc.ToJson();

        var restored = new SheetIndexService();
        restored.LoadFromJson(json);

        Assert.Equal(2, restored.Sheets.Count);
        Assert.Equal("M-01", restored.Sheets[0].Number);
        Assert.Equal("E-01", restored.Sheets[1].Number);

        // Sayaçlar da geri gelmeli — bir sonraki M paftası M-02 olmalı, E paftası E-02 olmalı.
        Assert.Equal("M-02", restored.PeekNextNumber("M"));
        Assert.Equal("E-02", restored.PeekNextNumber("E"));
    }

    [Fact]
    public void LoadFromJson_CorruptJson_LeavesStateUnchanged()
    {
        var svc = new SheetIndexService();
        svc.RegisterSheet(null, "Mevcut Pafta", "Proje A");

        svc.LoadFromJson("{ bozuk json ");

        // Bozuk JSON durumu bozmamalı — mevcut kayıt korunmalı.
        Assert.Single(svc.Sheets);
        Assert.Equal("Mevcut Pafta", svc.Sheets[0].Name);
    }
}
