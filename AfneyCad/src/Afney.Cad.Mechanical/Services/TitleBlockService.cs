using System;
using System.Collections.Generic;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Mechanical.Services;

public class TitleBlockService
{
    public enum PaperSize { A4, A3, A2, A1, A0 }

    public class TitleBlockConfig
    {
        public string FirmaAdi      { get; set; } = "";
        public string ProjeAdi      { get; set; } = "";
        public string CizimAdi      { get; set; } = "";
        public string Cizen         { get; set; } = "";
        public string KontrolEden   { get; set; } = "";
        public string Tarih         { get; set; } = "";
        public string PaftaNo       { get; set; } = "P-01";
        public string Olcek         { get; set; } = "1/100";
        public string Revizyon      { get; set; } = "A";
        public PaperSize KagitBoyu  { get; set; } = PaperSize.A3;
        public bool DrawBorderFrame { get; set; } = true;
    }

    // ── Kağıt boyutları (mm) ─────────────────────────────────────────────────
    private static readonly Dictionary<PaperSize, (double W, double H)> Sizes = new()
    {
        [PaperSize.A4] = (297,  210),
        [PaperSize.A3] = (420,  297),
        [PaperSize.A2] = (594,  420),
        [PaperSize.A1] = (841,  594),
        [PaperSize.A0] = (1189, 841),
    };

    private const string LayerAntet  = "ANTET";
    private const string LayerSinir  = "SINIR";
    private const uint   ColorBorder = 0xFFFFFFFF; // beyaz
    private const uint   ColorText   = 0xFF90CAF9; // açık mavi
    private const uint   ColorTitle  = 0xFFFFFFFF; // beyaz

    // ── Ana üretim metodu ─────────────────────────────────────────────────────
    public List<CadEntity> Generate(TitleBlockConfig cfg, Vector3D origin)
    {
        var entities = new List<CadEntity>();
        var (w, h) = Sizes[cfg.KagitBoyu];

        // ── 1. Dış çerçeve (Sheet Border) ────────────────────────────────────
        if (cfg.DrawBorderFrame)
        {
            double margin = 10;
            entities.AddRange(Rect(
                origin.X + margin, origin.Y + margin,
                origin.X + w - margin, origin.Y + h - margin,
                ColorBorder, LayerSinir));
        }

        // ── 2. Antet kutusu (sağ-alt köşe) ───────────────────────────────────
        // Sabit boyut: 180mm geniş × 55mm yüksek
        const double bW = 180, bH = 55;
        double bx = origin.X + w - 10 - bW; // sağ kenardan 10mm içeri
        double by = origin.Y + 10;           // alt kenardan 10mm yukarı

        // Dış kutu
        entities.AddRange(Rect(bx, by, bx + bW, by + bH, ColorBorder, LayerAntet));

        // İç yatay çizgiler
        double row1Y = by + bH - 14; // Firma adı satırı
        double row2Y = by + bH - 28; // Proje adı satırı
        double row3Y = by + bH - 42; // Çizim adı satırı
        // row4 = by (alt satır)

        entities.Add(HLine(bx, bx + bW, row1Y, ColorBorder, LayerAntet));
        entities.Add(HLine(bx, bx + bW, row2Y, ColorBorder, LayerAntet));
        entities.Add(HLine(bx, bx + bW, row3Y, ColorBorder, LayerAntet));

        // Dikey bölücüler (alt satır için)
        double colX1 = bx + 60;  // Pafta No sütunu
        double colX2 = bx + 110; // Ölçek sütunu
        double colX3 = bx + 140; // Revizyon sütunu

        entities.Add(VLine(colX1, by, row3Y, ColorBorder, LayerAntet));
        entities.Add(VLine(colX2, by, row3Y, ColorBorder, LayerAntet));
        entities.Add(VLine(colX3, by, row3Y, ColorBorder, LayerAntet));

        // Sağ dikey: Çizen/Kontrol sütunu
        double colX4 = bx + 130;
        entities.Add(VLine(colX4, row3Y, row1Y, ColorBorder, LayerAntet));
        // İkinci yatay (çizen/kontrol arası)
        double midY = (row3Y + row1Y) / 2;
        entities.Add(HLine(colX4, bx + bW, midY, ColorBorder, LayerAntet));

        // ── 3. Etiket metinleri ───────────────────────────────────────────────
        double th = 3.5; // metin yüksekliği (mm)
        double tf = 2.0; // küçük font

        // Firma adı (büyük)
        entities.Add(Txt(cfg.FirmaAdi,   bx + 2, row1Y + 4, 5.0,  ColorTitle, LayerAntet));

        // Proje adı
        entities.Add(Txt("Proje:",       bx + 2, row2Y + 8,  tf, ColorText,  LayerAntet));
        entities.Add(Txt(cfg.ProjeAdi,   bx + 2, row2Y + 3,  th, ColorTitle, LayerAntet));

        // Çizim adı
        entities.Add(Txt("Çizim:",       bx + 2, row3Y + 8,  tf, ColorText,  LayerAntet));
        entities.Add(Txt(cfg.CizimAdi,   bx + 2, row3Y + 3,  th, ColorTitle, LayerAntet));

        // Alt satır: Pafta No
        entities.Add(Txt("Pafta No",     bx + 2,   by + 8,  tf, ColorText,  LayerAntet));
        entities.Add(Txt(cfg.PaftaNo,    bx + 2,   by + 3,  th, ColorTitle, LayerAntet));

        // Ölçek
        entities.Add(Txt("Ölçek",        colX1 + 2, by + 8,  tf, ColorText,  LayerAntet));
        entities.Add(Txt(cfg.Olcek,      colX1 + 2, by + 3,  th, ColorTitle, LayerAntet));

        // Revizyon
        entities.Add(Txt("Rev.",         colX2 + 2, by + 8,  tf, ColorText,  LayerAntet));
        entities.Add(Txt(cfg.Revizyon,   colX2 + 2, by + 3,  th, ColorTitle, LayerAntet));

        // Tarih
        entities.Add(Txt("Tarih",        colX3 + 2, by + 8,  tf, ColorText,  LayerAntet));
        entities.Add(Txt(cfg.Tarih == "" ? DateTime.Now.ToString("dd.MM.yyyy") : cfg.Tarih,
                                         colX3 + 2, by + 3,  th, ColorTitle, LayerAntet));

        // Çizen / Kontrol
        entities.Add(Txt("Çizen:",       colX4 + 2, midY + 8, tf, ColorText,  LayerAntet));
        entities.Add(Txt(cfg.Cizen,      colX4 + 2, midY + 3, th, ColorTitle, LayerAntet));
        entities.Add(Txt("Kontrol:",     colX4 + 2, row3Y + 8, tf, ColorText,  LayerAntet));
        entities.Add(Txt(cfg.KontrolEden, colX4 + 2, row3Y + 3, th, ColorTitle, LayerAntet));

        return entities;
    }

    // ── Yardımcılar ──────────────────────────────────────────────────────────
    private static IEnumerable<CadEntity> Rect(double x1, double y1, double x2, double y2, uint color, string layer)
    {
        yield return Line(x1, y1, x2, y1, color, layer);
        yield return Line(x2, y1, x2, y2, color, layer);
        yield return Line(x2, y2, x1, y2, color, layer);
        yield return Line(x1, y2, x1, y1, color, layer);
    }

    private static LineEntity HLine(double x1, double x2, double y, uint color, string layer)
        => Line(x1, y, x2, y, color, layer);

    private static LineEntity VLine(double x, double y1, double y2, uint color, string layer)
        => Line(x, y1, x, y2, color, layer);

    private static LineEntity Line(double x1, double y1, double x2, double y2, uint color, string layer)
        => new(new Vector3D(x1, y1, 0), new Vector3D(x2, y2, 0)) { Color = color, Layer = layer };

    private static TextEntity Txt(string text, double x, double y, double h, uint color, string layer)
        => new(text, new Vector3D(x, y, 0), h, 0) { Color = color, Layer = layer };
}
