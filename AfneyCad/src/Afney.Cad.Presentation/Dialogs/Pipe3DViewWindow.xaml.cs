using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs
{
    public partial class Pipe3DViewWindow : Window
    {
        private readonly CadDatabase _database;

        public Pipe3DViewWindow(CadDatabase database)
        {
            InitializeComponent();
            _database = database;
            Generate3D();
        }

        private void Generate3D()
        {
            try
            {
                var lod = LodCombo.SelectedIndex switch
                {
                    0 => Pipe3DModelService.LevelOfDetail.LOD100,
                    1 => Pipe3DModelService.LevelOfDetail.LOD200,
                    2 => Pipe3DModelService.LevelOfDetail.LOD300,
                    _ => Pipe3DModelService.LevelOfDetail.LOD200
                };

                var service = new Pipe3DModelService(_database);
                var result = service.GenerateAll3DModels(lod);

                var group = new Model3DGroup();

                foreach (var model in result.Models)
                {
                    if (model.Vertices.Count < 3 || model.Faces.Count == 0) continue;

                    var mesh = new MeshGeometry3D();
                    foreach (var v in model.Vertices)
                    {
                        mesh.Positions.Add(new Point3D(v.X, v.Y, v.Z));
                    }
                    foreach (var face in model.Faces)
                    {
                        mesh.TriangleIndices.Add(face.A);
                        mesh.TriangleIndices.Add(face.B);
                        mesh.TriangleIndices.Add(face.C);
                    }

                    var color = model.SystemType switch
                    {
                        "DomesticColdWater" => Colors.DodgerBlue,
                        "DomesticHotWater" => Colors.OrangeRed,
                        "WasteWater" => Colors.Gray,
                        _ => Colors.LightGray
                    };

                    var material = new DiffuseMaterial(new SolidColorBrush(color));
                    var geometry = new GeometryModel3D(mesh, material);
                    geometry.BackMaterial = material;
                    group.Children.Add(geometry);
                }

                ModelGroup.Content = group;
                StatsText.Text = $"Modeller: {result.Models.Count} | Vertices: {result.TotalVertices} | Faces: {result.TotalFaces} | LOD: {lod}";
            }
            catch (Exception ex)
            {
                StatsText.Text = $"Hata: {ex.Message}";
            }
        }

        private void Lod_Changed(object sender, SelectionChangedEventArgs e) { }
        private void Refresh_Click(object sender, RoutedEventArgs e) => Generate3D();
        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
