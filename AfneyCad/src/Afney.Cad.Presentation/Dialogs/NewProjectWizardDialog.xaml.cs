using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs;

public partial class NewProjectWizardDialog
{
    // ── Sonuç ────────────────────────────────────────────────────────────────────

    public string  ProjectName     { get; private set; } = "";
    public string  ProjectNumber   { get; private set; } = "";
    public string  ClientName      { get; private set; } = "";
    public string  EngineerName    { get; private set; } = "";
    public string  CompanyName     { get; private set; } = "";
    public string  Phase           { get; private set; } = "";
    public int     Floors          { get; private set; } = 4;
    public double  FloorHeightM    { get; private set; } = 3.0;
    public double  FloorAreaM2     { get; private set; } = 100;
    public ProjectTemplateService.ProjectTemplate? SelectedTemplate { get; private set; }

    // ── İç Durum ────────────────────────────────────────────────────────────────

    private int _currentStep = 1;
    private ProjectTemplateService.ProjectTemplate? _hoveredTemplate;

    public NewProjectWizardDialog()
    {
        InitializeComponent();
        LstCategories.SelectedIndex = 0;
    }

    // ── Adım Yönetimi ────────────────────────────────────────────────────────────

    private void BtnNext_Click(object sender, RoutedEventArgs e)
    {
        if (_currentStep == 1)
        {
            if (SelectedTemplate == null && _hoveredTemplate == null)
            {
                StatusText.Text = "⚠ Lütfen bir şablon seçin."; return;
            }
            SelectedTemplate ??= _hoveredTemplate;
            FillStep2FromTemplate();
            GoToStep(2);
        }
        else if (_currentStep == 2)
        {
            if (string.IsNullOrWhiteSpace(TxtProjName.Text))
            {
                StatusText.Text = "⚠ Proje adı zorunludur."; return;
            }
            ReadStep2();
            FillStep3Summary();
            GoToStep(3);
            BtnNext.Content  = "✅ Proje Oluştur";
            BtnNext.Background = new SolidColorBrush(Color.FromRgb(27, 94, 32));
        }
        else if (_currentStep == 3)
        {
            ReadStep2();
            DialogResult = true;
            Close();
        }
    }

    private void BtnBack_Click(object sender, RoutedEventArgs e)
    {
        if (_currentStep == 2) GoToStep(1);
        else if (_currentStep == 3)
        {
            GoToStep(2);
            BtnNext.Content  = "İleri ▶";
            BtnNext.Background = new SolidColorBrush(Color.FromRgb(13, 71, 161));
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false; Close();
    }

    private void GoToStep(int step)
    {
        _currentStep = step;
        Step1.Visibility = step == 1 ? Visibility.Visible : Visibility.Collapsed;
        Step2.Visibility = step == 2 ? Visibility.Visible : Visibility.Collapsed;
        Step3.Visibility = step == 3 ? Visibility.Visible : Visibility.Collapsed;
        BtnBack.IsEnabled = step > 1;
        SetStepIndicator(step);
        StatusText.Text = step == 1 ? "Adım 1: Şablon seçimi" :
                          step == 2 ? "Adım 2: Proje bilgileri" :
                                      "Adım 3: Özet";
    }

    private void SetStepIndicator(int active)
    {
        var blue  = new SolidColorBrush(Color.FromRgb(13, 71, 161));
        var dark  = new SolidColorBrush(Color.FromRgb(51, 51, 51));
        var green = new SolidColorBrush(Color.FromRgb(27, 94, 32));

        Step1Indicator.Background = active == 1 ? blue : green;
        Step2Indicator.Background = active == 2 ? blue : (active > 2 ? green : dark);
        Step3Indicator.Background = active == 3 ? blue : dark;

        SetStepText(Step1Indicator, active > 1 ? "✓" : "1");
        SetStepText(Step2Indicator, active > 2 ? "✓" : "2");
        SetStepText(Step3Indicator, "3");
    }

    private static void SetStepText(Border b, string t)
    {
        if (b.Child is TextBlock tb) { tb.Text = t; tb.Foreground = Brushes.White; }
    }

    // ── Adım 1: Şablon Kartları ─────────────────────────────────────────────────

    private void LstCategories_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LstCategories.SelectedItem is not ListBoxItem item) return;
        string tag = item.Tag?.ToString() ?? "Tümü";
        RenderTemplateCards(tag);
    }

    private void RenderTemplateCards(string category)
    {
        TemplatePanel.Children.Clear();

        var templates = category == "Tümü" || category == "Boş"
            ? ProjectTemplateService.Templates
            : ProjectTemplateService.Templates.Where(t => t.Category == category).ToList();

        if (category == "Boş")
        {
            AddEmptyCard();
            return;
        }

        foreach (var tpl in templates)
            TemplatePanel.Children.Add(BuildCard(tpl));

        if (category == "Tümü") AddEmptyCard();
    }

    private Border BuildCard(ProjectTemplateService.ProjectTemplate tpl)
    {
        bool isSelected = SelectedTemplate?.TemplateId == tpl.TemplateId;
        var (heat, cool, du, persons) = ProjectTemplateService.SummarizeTemplate(tpl);

        var card = new Border
        {
            Width = 200, Height = 170, Margin = new Thickness(6),
            Background   = new SolidColorBrush(Color.FromRgb(13, 17, 23)),
            BorderBrush  = isSelected
                ? new SolidColorBrush(Color.FromRgb(255, 215, 64))
                : new SolidColorBrush(Color.FromRgb(51, 51, 102)),
            BorderThickness = new Thickness(isSelected ? 2 : 1),
            CornerRadius = new CornerRadius(6),
            Cursor       = System.Windows.Input.Cursors.Hand,
            Tag          = tpl.TemplateId
        };

        var sp = new StackPanel { Margin = new Thickness(10, 8, 10, 8) };
        sp.Children.Add(new TextBlock { Text = tpl.Icon, FontSize = 28, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 6) });
        sp.Children.Add(new TextBlock { Text = tpl.Name, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(255, 215, 64)), TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center, FontSize = 11, Margin = new Thickness(0, 0, 0, 4) });
        sp.Children.Add(new TextBlock { Text = tpl.Category, Foreground = new SolidColorBrush(Color.FromRgb(144, 202, 249)), FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 6) });

        var infoGrid = new Grid();
        infoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        infoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var heatTb = new TextBlock { Text = $"🔥 {heat:F1} kW", Foreground = new SolidColorBrush(Color.FromRgb(239, 154, 154)), FontSize = 9 };
        var coolTb = new TextBlock { Text = $"❄ {cool:F1} kW",  Foreground = new SolidColorBrush(Color.FromRgb(128, 222, 234)), FontSize = 9 };
        Grid.SetColumn(heatTb, 0); Grid.SetColumn(coolTb, 1);
        infoGrid.Children.Add(heatTb); infoGrid.Children.Add(coolTb);
        sp.Children.Add(infoGrid);

        sp.Children.Add(new TextBlock { Text = $"{tpl.Zones.Count} bölge · {persons} kişi · {du} DU", FontSize = 9, Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 200)), Margin = new Thickness(0, 4, 0, 0), TextAlignment = TextAlignment.Center });

        card.Child = card.Child ?? sp;
        card.Child = sp;

        card.MouseLeftButtonUp += (_, _) =>
        {
            SelectedTemplate = tpl;
            RenderTemplateCards((LstCategories.SelectedItem as ListBoxItem)?.Tag?.ToString() ?? "Tümü");
            StatusText.Text = $"✓ Seçildi: {tpl.Name}";
        };

        return card;
    }

    private void AddEmptyCard()
    {
        bool sel = SelectedTemplate == null;
        var card = new Border
        {
            Width = 200, Height = 170, Margin = new Thickness(6),
            Background = new SolidColorBrush(Color.FromRgb(13, 17, 23)),
            BorderBrush = sel ? new SolidColorBrush(Color.FromRgb(255, 215, 64)) : new SolidColorBrush(Color.FromRgb(51, 51, 102)),
            BorderThickness = new Thickness(sel ? 2 : 1), CornerRadius = new CornerRadius(6),
            Cursor = System.Windows.Input.Cursors.Hand
        };
        var sp = new StackPanel { Margin = new Thickness(10, 20, 10, 10), HorizontalAlignment = HorizontalAlignment.Center };
        sp.Children.Add(new TextBlock { Text = "📄", FontSize = 36, HorizontalAlignment = HorizontalAlignment.Center });
        sp.Children.Add(new TextBlock { Text = "Boş Proje", FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(255, 215, 64)), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 4) });
        sp.Children.Add(new TextBlock { Text = "Sıfırdan başla", Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136)), FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center });
        card.Child = sp;
        card.MouseLeftButtonUp += (_, _) => { SelectedTemplate = null; RenderTemplateCards("Boş"); StatusText.Text = "Boş proje seçildi."; };
        TemplatePanel.Children.Add(card);
    }

    // ── Adım 2 Doldurma ─────────────────────────────────────────────────────────

    private void FillStep2FromTemplate()
    {
        if (SelectedTemplate == null) return;
        TplSummaryIcon.Text = SelectedTemplate.Icon;
        TplSummaryName.Text = SelectedTemplate.Name;
        TplSummaryDesc.Text = SelectedTemplate.Description;
        TplSystems.Text     = string.Join(" · ", SelectedTemplate.ActiveSystems.Select(HumanizeSystem));
        TplZoneCount.Text   = SelectedTemplate.Zones.Count.ToString();

        var (heat, cool, _, _) = ProjectTemplateService.SummarizeTemplate(SelectedTemplate);
        TplHeating.Text = $"{heat:F1} kW";
        TplCooling.Text = $"{cool:F1} kW";
        TplNotes.Text   = string.IsNullOrEmpty(SelectedTemplate.Notes) ? "—" : SelectedTemplate.Notes;

        TxtFloors.Text   = SelectedTemplate.TypicalFloors.ToString();
        TxtFloorH.Text   = SelectedTemplate.FloorHeightM.ToString("F1");
        TxtFloorArea.Text = SelectedTemplate.TypicalFloorAreaM2.ToString("F0");
    }

    private void ReadStep2()
    {
        ProjectName   = TxtProjName.Text.Trim();
        ProjectNumber = TxtProjNum.Text.Trim();
        ClientName    = TxtClient.Text.Trim();
        EngineerName  = TxtEngineer.Text.Trim();
        CompanyName   = TxtCompany.Text.Trim();
        Phase         = (CboPhase.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
        _ = int.TryParse(TxtFloors.Text,    out int f); Floors       = f > 0 ? f : 4;
        _ = double.TryParse(TxtFloorH.Text, out double fh); FloorHeightM = fh > 0 ? fh : 3.0;
        _ = double.TryParse(TxtFloorArea.Text, out double fa); FloorAreaM2  = fa > 0 ? fa : 100;
    }

    // ── Adım 3 Özeti ────────────────────────────────────────────────────────────

    private void FillStep3Summary()
    {
        SumProjName.Text  = string.IsNullOrEmpty(ProjectName) ? TxtProjName.Text : ProjectName;
        SumProjNum.Text   = $"No: {TxtProjNum.Text}";
        SumClient.Text    = $"İşveren: {TxtClient.Text}";
        SumEngineer.Text  = $"Müh: {TxtEngineer.Text}";
        SumCompany.Text   = $"Firma: {TxtCompany.Text}";
        SumPhase.Text     = $"Aşama: {Phase}";

        string tplName = SelectedTemplate?.Name ?? "Boş Proje";
        SumTemplate.Text = $"{SelectedTemplate?.Icon} {tplName}";
        SumFloors.Text   = $"{TxtFloors.Text} Kat";
        SumFloorH.Text   = $"Kat H: {TxtFloorH.Text} m";
        SumArea.Text     = $"{TxtFloorArea.Text} m² / Kat";
        SumSystems.Text  = SelectedTemplate != null
            ? string.Join(", ", SelectedTemplate.ActiveSystems.Select(HumanizeSystem))
            : "—";

        ZoneGrid.ItemsSource = SelectedTemplate?.Zones ?? [];
    }

    // ── Yardımcılar ─────────────────────────────────────────────────────────────

    private static string HumanizeSystem(string sys) => sys switch
    {
        "DomesticColdWater" => "Soğuk Su",
        "DomesticHotWater"  => "Sıcak Su",
        "WasteWater"        => "Pis Su",
        "RainWater"         => "Yağmur Suyu",
        "Heating"           => "Isıtma",
        "Cooling"           => "Soğutma",
        "Ventilation"       => "Havalandırma",
        "FireProtection"    => "Yangın",
        _                   => sys
    };
}
