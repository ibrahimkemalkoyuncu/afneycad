using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Afney.Cad.Database.Core;
using Afney.Cad.Geometry.Topology;
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

                // Kanallar (DuctEntity) — Pipe3DModelService bunları hiç kapsamıyordu (sadece
                // Pipe/Elbow/Tee/Reducer), bu yüzden ayrı B-Rep servisiyle üretiliyor.
                var ductSolids = new DuctBRepService(_database).GenerateAllDuctSolids();
                var ductMaterial = new DiffuseMaterial(new SolidColorBrush(Colors.MediumSeaGreen));
                int ductTriangles = 0;
                foreach (var solid in ductSolids)
                    ductTriangles += AddBRepSolid(group, solid, ductMaterial);

                int wallCount = 0, wallTriangles = 0;
                if (ShowWallsCheckBox.IsChecked == true)
                {
                    var wallSolids = new WallBRepService(_database).GenerateAllWallSolids();
                    var wallMaterial = new DiffuseMaterial(new SolidColorBrush(Color.FromArgb(160, 200, 200, 200)));

                    foreach (var solid in wallSolids)
                    {
                        int tris = AddBRepSolid(group, solid, wallMaterial);
                        if (tris > 0) { wallCount++; wallTriangles += tris; }
                    }
                }

                ModelGroup.Content = group;
                StatsText.Text = $"Modeller: {result.Models.Count} | Vertices: {result.TotalVertices} | Faces: {result.TotalFaces} | LOD: {lod}"
                    + $" | Kanallar (B-Rep): {ductSolids.Count}, {ductTriangles} üçgen"
                    + (ShowWallsCheckBox.IsChecked == true ? $" | Duvarlar (B-Rep): {wallCount}, {wallTriangles} üçgen" : "");
            }
            catch (Exception ex)
            {
                StatsText.Text = $"Hata: {ex.Message}";
            }
        }

        /// <summary>B-Rep Solid'i tessellate edip Model3DGroup'a ekler, üretilen üçgen sayısını döner (0 = boş/atlandı).</summary>
        private static int AddBRepSolid(Model3DGroup group, Solid solid, Material material)
        {
            var (verts, faces) = BRepTessellator.Tessellate(solid);
            if (verts.Count < 3 || faces.Count == 0) return 0;

            var mesh = new MeshGeometry3D();
            foreach (var v in verts)
                mesh.Positions.Add(new Point3D(v.X, v.Y, v.Z));
            foreach (var face in faces)
            {
                mesh.TriangleIndices.Add(face.A);
                mesh.TriangleIndices.Add(face.B);
                mesh.TriangleIndices.Add(face.C);
            }

            group.Children.Add(new GeometryModel3D(mesh, material) { BackMaterial = material });
            return faces.Count;
        }

        private void Lod_Changed(object sender, SelectionChangedEventArgs e) { }
        private void Refresh_Click(object sender, RoutedEventArgs e) => Generate3D();
        private void ShowWalls_Changed(object sender, RoutedEventArgs e) => Generate3D();
        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
