using System.Windows.Controls;
using System.Windows.Media;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Presentation.Controls;

public partial class PropertiesPanel : UserControl
{
    public PropertiesPanel()
    {
        InitializeComponent();
    }

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
