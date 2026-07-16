using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Presentation.Controls;

public partial class PropertiesPanel : UserControl
{
    public PropertiesPanel()
    {
        InitializeComponent();
    }

    /*
       NE: Nesne Değişikliği Olayı (EntityModified)
       NEDEN: Geometri alanlarında yapılan düzenlemeleri MainWindow'a bildirip
              veritabanı güncellemesi + yeniden çizim tetiklemek için (IntelligencePanel'deki
              aynı desenle tutarlı).
    */
    public event EventHandler<CadEntity>? EntityModified;

    public void UpdateSelection(IReadOnlyList<CadEntity>? entities)
    {
        if (entities == null || entities.Count == 0)
        {
            TxtSelection.Text = "Seçilen yok";
            ClearAll();
            return;
        }

        var entity = entities[0];
        TxtSelection.Text = entities.Count == 1
            ? $"{GetTypeName(entity)}"
            : $"All({entities.Count})";

        uint c = entity.Color;
        byte r = (byte)((c >> 16) & 0xFF);
        byte g = (byte)((c >> 8) & 0xFF);
        byte b = (byte)(c & 0xFF);
        ColorSwatch.Background = new SolidColorBrush(Color.FromRgb(r, g, b));
        TxtColor.Text = entities.Count > 1 ? "Değişen" : $"#{r:X2}{g:X2}{b:X2}";

        TxtLayer.Text = entity.Layer ?? "0";
        TxtLinetype.Text = entity.Linetype ?? "BYLAYER";
        TxtLinetypeScale.Text = "1.00";
        TxtLineWeight.Text = entity.LineWeight == -1 ? "BYLAYER" : $"{entity.LineWeight / 100.0:F2}";
        TxtThickness.Text = "0.00";

        var bb = entity.GetBoundingBox();
        TxtCenterX.Text = bb.Center.X.ToString("F2");
        TxtCenterY.Text = bb.Center.Y.ToString("F2");
        TxtCenterZ.Text = bb.Center.Z.ToString("F2");

        double height = bb.Max.Y - bb.Min.Y;
        TxtHeight.Text = height.ToString("F2");

        if (entities.Count == 1)
            PopulateGeometryFields(entity);
        else
            HideGeometrySection();
    }

    private void ClearAll()
    {
        ColorSwatch.Background = new SolidColorBrush(Colors.Gray);
        TxtColor.Text = "BYLAYER";
        TxtLayer.Text = "";
        TxtLinetype.Text = "BYLAYER";
        TxtLinetypeScale.Text = "1.00";
        TxtLineWeight.Text = "BYLAYER";
        TxtThickness.Text = "0.00";
        TxtCenterX.Text = "0.00";
        TxtCenterY.Text = "0.00";
        TxtCenterZ.Text = "0.00";
        TxtHeight.Text = "0.00";
        HideGeometrySection();
    }

    // ── Nesne Tipine Özel Düzenlenebilir Geometri Alanları ───────────────────

    private void HideGeometrySection()
    {
        GeometryFieldsPanel.Children.Clear();
        GeometryExpander.Visibility = Visibility.Collapsed;
    }

    private void PopulateGeometryFields(CadEntity entity)
    {
        GeometryFieldsPanel.Children.Clear();

        switch (entity)
        {
            case LineEntity line:
                AddVectorField("Başlangıç", line.StartPoint,
                    v => Commit(line, "Başlangıç Noktası", line.StartPoint, v, nv => line.StartPoint = nv));
                AddVectorField("Bitiş", line.EndPoint,
                    v => Commit(line, "Bitiş Noktası", line.EndPoint, v, nv => line.EndPoint = nv));
                break;

            case CircleEntity circle:
                AddVectorField("Merkez", circle.Center,
                    v => Commit(circle, "Merkez", circle.Center, v, nv => circle.Center = nv));
                AddNumericField("Yarıçap", circle.Radius,
                    r => Commit(circle, "Yarıçap", circle.Radius, r, nv => circle.Radius = nv));
                break;

            case ArcEntity arc:
                AddVectorField("Merkez", arc.Center,
                    v => Commit(arc, "Merkez", arc.Center, v, nv => arc.Center = nv));
                AddNumericField("Yarıçap", arc.Radius,
                    r => Commit(arc, "Yarıçap", arc.Radius, r, nv => arc.Radius = nv));
                AddNumericField("Başlangıç Açı (°)", arc.StartAngle * 180.0 / Math.PI,
                    deg => Commit(arc, "Başlangıç Açı", arc.StartAngle, deg * Math.PI / 180.0, nv => arc.StartAngle = nv));
                AddNumericField("Bitiş Açı (°)", arc.EndAngle * 180.0 / Math.PI,
                    deg => Commit(arc, "Bitiş Açı", arc.EndAngle, deg * Math.PI / 180.0, nv => arc.EndAngle = nv));
                break;

            case TextEntity text:
                AddTextField("İçerik", text.Text,
                    t => Commit(text, "Metin İçeriği", text.Text, t, nv => text.Text = nv));
                AddNumericField("Yükseklik", text.Height,
                    h => Commit(text, "Yükseklik", text.Height, h, nv => text.Height = nv));
                AddNumericField("Döndürme (°)", text.Rotation,
                    r => Commit(text, "Döndürme", text.Rotation, r, nv => text.Rotation = nv));
                break;

            default:
                HideGeometrySection();
                return;
        }

        GeometryExpander.Visibility = Visibility.Visible;
    }

    /*
       NE: Özellik Değişikliğini Gönder (Commit)
       NEDEN: Geometri alanları önceden doğrudan property set ediyordu — Undo/Redo (Ctrl+Z)
              geometri düzenlemelerini kapsamıyordu. IntelligencePanel'deki
              SubmitPropertyChange deseniyle tutarlı: değişiklik TransactionManager
              üzerinden geçiyor, geri alınabilir hale geliyor.
    */
    private void Commit<T>(CadEntity entity, string propName, T oldValue, T newValue, Action<T> apply)
    {
        if (Equals(oldValue, newValue)) return;

        var mainWindow = System.Windows.Application.Current.MainWindow as MainWindow;
        void Do() { apply(newValue); NotifyModified(entity); }
        void Undo() { apply(oldValue); NotifyModified(entity); }

        if (mainWindow != null)
        {
            var op = new Afney.Cad.Database.Transactions.Operations.ModifyEntityPropertyOperation(propName, Do, Undo);
            mainWindow.ActiveContext.History.TransactionManager.Submit(op);
        }
        else
        {
            Do();
        }
    }

    private void NotifyModified(CadEntity entity)
    {
        entity.NotifyGeometryChanged();
        EntityModified?.Invoke(this, entity);
    }

    private void AddVectorField(string label, Vector3D value, Action<Vector3D> onCommit)
    {
        GeometryFieldsPanel.Children.Add(new TextBlock { Text = label, Style = (Style)FindResource("SectionHeader"), FontSize = 9, Margin = new Thickness(0, 4, 0, 0) });
        AddNumericField("  X", value.X, x => onCommit(new Vector3D(x, value.Y, value.Z)));
        AddNumericField("  Y", value.Y, y => onCommit(new Vector3D(value.X, y, value.Z)));
        AddNumericField("  Z", value.Z, z => onCommit(new Vector3D(value.X, value.Y, z)));
    }

    private void AddNumericField(string label, double value, Action<double> onCommit)
    {
        AddEditableField(label, value.ToString("F2"), text =>
        {
            if (double.TryParse(text, out double parsed))
                onCommit(parsed);
        });
    }

    private void AddTextField(string label, string value, Action<string> onCommit)
    {
        AddEditableField(label, value, onCommit);
    }

    private void AddEditableField(string label, string value, Action<string> onCommit)
    {
        var dock = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
        dock.Children.Add(new TextBlock { Text = label, Style = (Style)FindResource("PropLabel") });

        var box = new TextBox
        {
            Text = value,
            Background = new SolidColorBrush(Color.FromRgb(45, 45, 48)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(85, 85, 85)),
            BorderThickness = new Thickness(1),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 11,
            Padding = new Thickness(2)
        };
        box.LostFocus += (_, _) => onCommit(box.Text);
        box.KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) onCommit(box.Text); };

        dock.Children.Add(box);
        GeometryFieldsPanel.Children.Add(dock);
    }

    private static string GetTypeName(CadEntity e) => e.GetType().Name switch
    {
        "LineEntity" => "Çizgi",
        "CircleEntity" => "Daire",
        "ArcEntity" => "Yay",
        "TextEntity" => "Metin",
        "LwPolylineEntity" => "Polyline",
        "BlockReferenceEntity" => "Blok Ref.",
        "HatchEntity" => "Hatch",
        "SplineEntity" => "Spline",
        "DimensionEntity" => "Ölçü",
        _ => e.GetType().Name.Replace("Entity", "")
    };
}
