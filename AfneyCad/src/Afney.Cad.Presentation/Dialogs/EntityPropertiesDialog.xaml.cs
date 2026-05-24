using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;

namespace Afney.Cad.Presentation.Dialogs;

public partial class EntityPropertiesDialog : Window
{
    private readonly CadDatabase _database;
    private readonly CadEntity _entity;

    // Toplanan field editörleri — Apply sırasında okunur
    private readonly List<(string PropName, Func<string> GetValue, Action<string> SetValue)> _fields = [];

    public EntityPropertiesDialog(CadDatabase database, CadEntity entity)
    {
        InitializeComponent();
        _database = database;
        _entity   = entity;
        BuildUI();
    }

    // ── UI Builder ────────────────────────────────────────────────────────────
    private void BuildUI()
    {
        (string icon, string typeName) = _entity switch
        {
            PipeEntity           => ("🔵", "Boru"),
            SanitaryFixtureEntity=> ("🚿", "Sıhhi Armatür"),
            LineEntity           => ("📏", "Çizgi"),
            CircleEntity         => ("⭕", "Daire"),
            ArcEntity            => ("〰", "Yay"),
            TextEntity           => ("🅣", "Metin"),
            LwPolylineEntity     => ("📐", "Polyline"),
            _                    => ("⬜", _entity.GetType().Name)
        };

        TxtEntityIcon.Text = icon;
        TxtEntityType.Text = typeName;
        TxtEntityId.Text   = $"ID: {_entity.Id}";
        Title = $"Özellikler — {typeName}";

        var panel = PropertiesPanel;

        // Ortak özellikler
        AddSectionHeader(panel, "Genel");
        AddRow(panel, "Katman",
            () => _entity.Layer ?? "",
            v  => _entity.Layer = v);
        AddColorRow(panel, "Renk (ARGB hex)",
            () => _entity.Color.ToString("X8"),
            v  => { if (uint.TryParse(v, System.Globalization.NumberStyles.HexNumber, null, out uint c)) _entity.Color = c; });

        // Tip bazlı özellikler
        switch (_entity)
        {
            case PipeEntity pipe:
                BuildPipeFields(panel, pipe);
                break;
            case SanitaryFixtureEntity fix:
                BuildFixtureFields(panel, fix);
                break;
            case LineEntity line:
                BuildLineFields(panel, line);
                break;
            case CircleEntity circle:
                BuildCircleFields(panel, circle);
                break;
            case ArcEntity arc:
                BuildArcFields(panel, arc);
                break;
            case TextEntity text:
                BuildTextFields(panel, text);
                break;
        }
    }

    private void BuildPipeFields(StackPanel panel, PipeEntity pipe)
    {
        AddSectionHeader(panel, "Boru Boyutları");
        AddRow(panel, "İç Çap (mm)",
            () => pipe.InnerDiameter.ToString("F1"),
            v  => { if (TryDouble(v, out double d)) pipe.InnerDiameter = d; });
        AddRow(panel, "Uzunluk (mm) [hesaplanmış]",
            () => pipe.Length.ToString("F1"),
            v  => { },   // readonly
            readOnly: true);
        AddRow(panel, "Eğim (%)",
            () => pipe.Slope.ToString("F2"),
            v  => { if (TryDouble(v, out double d)) pipe.Slope = d; });

        AddSectionHeader(panel, "Sistem");
        AddEnumRow<MechanicalSystemType>(panel, "Sistem Tipi",
            () => pipe.SystemType,
            v  => pipe.SystemType = v);
        AddRow(panel, "Debi (m³/h)",
            () => pipe.FlowRate.ToString("F4"),
            v  => { if (TryDouble(v, out double d)) pipe.FlowRate = d; });
        AddRow(panel, "Hız (m/s) [hesaplanmış]",
            () => pipe.Velocity.ToString("F3"),
            v  => { },
            readOnly: true);
        AddRow(panel, "Basınç (bar)",
            () => pipe.Pressure.ToString("F3"),
            v  => { if (TryDouble(v, out double d)) pipe.Pressure = d; });
        AddRow(panel, "Sıcaklık (°C)",
            () => pipe.Temperature.ToString("F1"),
            v  => { if (TryDouble(v, out double d)) pipe.Temperature = d; });
        AddRow(panel, "Yükleme Birimi (DU)",
            () => pipe.LoadUnits.ToString("F2"),
            v  => { if (TryDouble(v, out double d)) pipe.LoadUnits = d; });

        AddSectionHeader(panel, "Koordinatlar");
        AddRow(panel, "Başlangıç X",
            () => pipe.StartPoint.X.ToString("F2"),
            v  => { if (TryDouble(v, out double d)) pipe.StartPoint = new Afney.Cad.Geometry.Primitives.Vector3D(d, pipe.StartPoint.Y, pipe.StartPoint.Z); });
        AddRow(panel, "Başlangıç Y",
            () => pipe.StartPoint.Y.ToString("F2"),
            v  => { if (TryDouble(v, out double d)) pipe.StartPoint = new Afney.Cad.Geometry.Primitives.Vector3D(pipe.StartPoint.X, d, pipe.StartPoint.Z); });
        AddRow(panel, "Bitiş X",
            () => pipe.EndPoint.X.ToString("F2"),
            v  => { if (TryDouble(v, out double d)) pipe.EndPoint = new Afney.Cad.Geometry.Primitives.Vector3D(d, pipe.EndPoint.Y, pipe.EndPoint.Z); });
        AddRow(panel, "Bitiş Y",
            () => pipe.EndPoint.Y.ToString("F2"),
            v  => { if (TryDouble(v, out double d)) pipe.EndPoint = new Afney.Cad.Geometry.Primitives.Vector3D(pipe.EndPoint.X, d, pipe.EndPoint.Z); });
    }

    private void BuildFixtureFields(StackPanel panel, SanitaryFixtureEntity fix)
    {
        AddSectionHeader(panel, "Armatür Bilgileri");
        AddRow(panel, "Tip",
            () => fix.FixtureType,
            v  => fix.FixtureType = v);
        AddRow(panel, "Yükleme Birimi (DU)",
            () => fix.LoadUnits.ToString("F2"),
            v  => { if (TryDouble(v, out double d)) fix.LoadUnits = d; });
        AddRow(panel, "Genişlik (mm)",
            () => fix.Width.ToString("F0"),
            v  => { if (TryDouble(v, out double d)) fix.Width = d; });
        AddRow(panel, "Derinlik (mm)",
            () => fix.Depth.ToString("F0"),
            v  => { if (TryDouble(v, out double d)) fix.Depth = d; });
        AddRow(panel, "Dönüş (derece)",
            () => (fix.Rotation * 180.0 / Math.PI).ToString("F1"),
            v  => { if (TryDouble(v, out double d)) fix.Rotation = d * Math.PI / 180.0; });
        AddEnumRow<MechanicalSystemType>(panel, "Sistem Tipi",
            () => fix.SystemType,
            v  => fix.SystemType = v);

        AddSectionHeader(panel, "Konum");
        AddRow(panel, "X", () => fix.Position.X.ToString("F2"),
            v  => { if (TryDouble(v, out double d)) fix.Position = new Afney.Cad.Geometry.Primitives.Vector3D(d, fix.Position.Y, fix.Position.Z); });
        AddRow(panel, "Y", () => fix.Position.Y.ToString("F2"),
            v  => { if (TryDouble(v, out double d)) fix.Position = new Afney.Cad.Geometry.Primitives.Vector3D(fix.Position.X, d, fix.Position.Z); });
    }

    private void BuildLineFields(StackPanel panel, LineEntity line)
    {
        AddSectionHeader(panel, "Nokta Koordinatları");
        AddRow(panel, "Başlangıç X", () => line.StartPoint.X.ToString("F2"),
            v => { if (TryDouble(v, out double d)) line.StartPoint = new Afney.Cad.Geometry.Primitives.Vector3D(d, line.StartPoint.Y, line.StartPoint.Z); });
        AddRow(panel, "Başlangıç Y", () => line.StartPoint.Y.ToString("F2"),
            v => { if (TryDouble(v, out double d)) line.StartPoint = new Afney.Cad.Geometry.Primitives.Vector3D(line.StartPoint.X, d, line.StartPoint.Z); });
        AddRow(panel, "Bitiş X", () => line.EndPoint.X.ToString("F2"),
            v => { if (TryDouble(v, out double d)) line.EndPoint = new Afney.Cad.Geometry.Primitives.Vector3D(d, line.EndPoint.Y, line.EndPoint.Z); });
        AddRow(panel, "Bitiş Y", () => line.EndPoint.Y.ToString("F2"),
            v => { if (TryDouble(v, out double d)) line.EndPoint = new Afney.Cad.Geometry.Primitives.Vector3D(line.EndPoint.X, d, line.EndPoint.Z); });

        double len = (line.EndPoint - line.StartPoint).Length();
        AddRow(panel, "Uzunluk (mm) [hesaplanmış]",
            () => len.ToString("F2"), v => { }, readOnly: true);
    }

    private void BuildCircleFields(StackPanel panel, CircleEntity circle)
    {
        AddSectionHeader(panel, "Daire Özellikleri");
        AddRow(panel, "Merkez X", () => circle.Center.X.ToString("F2"),
            v => { if (TryDouble(v, out double d)) circle.Center = new Afney.Cad.Geometry.Primitives.Vector3D(d, circle.Center.Y, circle.Center.Z); });
        AddRow(panel, "Merkez Y", () => circle.Center.Y.ToString("F2"),
            v => { if (TryDouble(v, out double d)) circle.Center = new Afney.Cad.Geometry.Primitives.Vector3D(circle.Center.X, d, circle.Center.Z); });
        AddRow(panel, "Yarıçap (mm)", () => circle.Radius.ToString("F2"),
            v => { if (TryDouble(v, out double d)) circle.Radius = d; });
        AddRow(panel, "Çap (mm) [hesaplanmış]",
            () => (circle.Radius * 2).ToString("F2"), v => { }, readOnly: true);
    }

    private void BuildArcFields(StackPanel panel, ArcEntity arc)
    {
        AddSectionHeader(panel, "Yay Özellikleri");
        AddRow(panel, "Merkez X", () => arc.Center.X.ToString("F2"),
            v => { if (TryDouble(v, out double d)) arc.Center = new Afney.Cad.Geometry.Primitives.Vector3D(d, arc.Center.Y, arc.Center.Z); });
        AddRow(panel, "Merkez Y", () => arc.Center.Y.ToString("F2"),
            v => { if (TryDouble(v, out double d)) arc.Center = new Afney.Cad.Geometry.Primitives.Vector3D(arc.Center.X, d, arc.Center.Z); });
        AddRow(panel, "Yarıçap (mm)", () => arc.Radius.ToString("F2"),
            v => { if (TryDouble(v, out double d)) arc.Radius = d; });
        AddRow(panel, "Başlangıç Açısı (°)", () => (arc.StartAngle * 180 / Math.PI).ToString("F2"),
            v => { if (TryDouble(v, out double d)) arc.StartAngle = d * Math.PI / 180; });
        AddRow(panel, "Bitiş Açısı (°)", () => (arc.EndAngle * 180 / Math.PI).ToString("F2"),
            v => { if (TryDouble(v, out double d)) arc.EndAngle = d * Math.PI / 180; });
    }

    private void BuildTextFields(StackPanel panel, TextEntity text)
    {
        AddSectionHeader(panel, "Metin Özellikleri");
        AddRow(panel, "İçerik", () => text.Text, v => text.Text = v);
        AddRow(panel, "Yükseklik (mm)", () => text.Height.ToString("F1"),
            v => { if (TryDouble(v, out double d)) text.Height = d; });
        AddRow(panel, "Dönüş (°)", () => text.Rotation.ToString("F1"),
            v => { if (TryDouble(v, out double d)) text.Rotation = d; });
        AddRow(panel, "Konum X", () => text.Position.X.ToString("F2"),
            v => { if (TryDouble(v, out double d)) text.Position = new Afney.Cad.Geometry.Primitives.Vector3D(d, text.Position.Y, text.Position.Z); });
        AddRow(panel, "Konum Y", () => text.Position.Y.ToString("F2"),
            v => { if (TryDouble(v, out double d)) text.Position = new Afney.Cad.Geometry.Primitives.Vector3D(text.Position.X, d, text.Position.Z); });
    }

    // ── UI Yardımcıları ───────────────────────────────────────────────────────
    private void AddSectionHeader(StackPanel panel, string title)
    {
        panel.Children.Add(new Separator());
        panel.Children.Add(new Label { Content = title.ToUpperInvariant() });
    }

    private void AddRow(StackPanel panel, string label,
        Func<string> getter, Action<string> setter, bool readOnly = false)
    {
        var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var lbl = new TextBlock { Text = label, Margin = new Thickness(0, 0, 8, 0) };
        Grid.SetColumn(lbl, 0);

        var tb = new TextBox
        {
            Text       = getter(),
            IsReadOnly = readOnly,
            Background = readOnly
                ? System.Windows.Media.Brushes.Transparent
                : null,
            Foreground = readOnly
                ? System.Windows.Media.Brushes.Gray
                : System.Windows.Media.Brushes.White,
        };
        Grid.SetColumn(tb, 1);

        if (!readOnly)
            _fields.Add((label, () => tb.Text, setter));

        grid.Children.Add(lbl);
        grid.Children.Add(tb);
        panel.Children.Add(grid);
    }

    private void AddColorRow(StackPanel panel, string label,
        Func<string> getter, Action<string> setter)
        => AddRow(panel, label, getter, setter);

    private void AddEnumRow<TEnum>(StackPanel panel, string label,
        Func<TEnum> getter, Action<TEnum> setter) where TEnum : struct, Enum
    {
        var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var lbl = new TextBlock { Text = label, Margin = new Thickness(0, 0, 8, 0) };
        Grid.SetColumn(lbl, 0);

        var cmb = new ComboBox();
        foreach (var val in Enum.GetValues<TEnum>())
            cmb.Items.Add(val);
        cmb.SelectedItem = getter();
        Grid.SetColumn(cmb, 1);

        _fields.Add((label,
            () => cmb.SelectedItem?.ToString() ?? "",
            v  => { if (Enum.TryParse<TEnum>(v, out var parsed)) setter(parsed); }));

        grid.Children.Add(lbl);
        grid.Children.Add(cmb);
        panel.Children.Add(grid);
    }

    // ── Olaylar ───────────────────────────────────────────────────────────────
    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        foreach (var (_, getValue, setValue) in _fields)
            setValue(getValue());

        TxtStatus.Text = "✓ Uygulandı";
        EntityChanged?.Invoke(this, _entity);
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        Apply_Click(sender, e);
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    public event EventHandler<CadEntity>? EntityChanged;

    // ── Yardımcı ──────────────────────────────────────────────────────────────
    private static bool TryDouble(string s, out double result)
        => double.TryParse(s.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out result);
}
