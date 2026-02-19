/*
 * DOSYA: DxfReader.cs
 * AMAÇ: AutoCAD Dosya Okuyucu (Safe Mode & High Fidelity) v3
 * 
 * MÜHENDİSLİK NOTU:
 * Bu sürüm 'dynamic' binding kullanarak ACadSharp kütüphanesindeki versiyon farklılıklarından kaynaklanan
 * property hatalarını (CS1061) bypass eder. Ayrıca Layer renklerini doğru çözer.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Afney.Cad.Domain.Tables;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Database.Core;
using Afney.Cad.Geometry.Primitives;

// Kütüphaneyi sadece tip kontrolü için değil, dinamik yükleme mantığıyla kullanıyoruz
using ACadSharp; 

namespace Afney.Cad.Infrastructure.FileFormats
{
    internal class CadBlockRecord
    {
        public string Name { get; set; } = "";
        public Vector3D BasePoint { get; set; }
        public List<CadEntity> Entities { get; set; } = new();
    }

    public class DxfReader
    {
        private Dictionary<string, uint> _layerColors = new();

        public CadDatabase Read(string path, bool asReference = false)
        {
            CadDatabase db = new CadDatabase();
            ACadSharp.CadDocument doc = null;

            try
            {
                if (path.ToLower().EndsWith(".dxf")) 
                { 
                    using (var r = new ACadSharp.IO.DxfReader(path)) { doc = r.Read(); } 
                }
                else 
                { 
                    using (var r = new ACadSharp.IO.DwgReader(path)) { doc = r.Read(); } 
                }
            }
            catch (Exception ex) 
            {
                System.Diagnostics.Debug.WriteLine($"Okuma Hatası: {ex.Message}");
                return db;
            }

            if (doc == null) return db;

            // 1. KATMANLARI OKU (Dynamic)
            _layerColors.Clear();
            foreach (var item in doc.Layers)
            {
                // Dynamic erişim ile 'IsFrozen' gibi riskli property'leri güvenli okuyoruz
                dynamic layer = item; 
                string name = layer.Name;
                
                // Renk Çözümleme
                dynamic colorObj = layer.Color;
                uint color = ResolveColorDyn(colorObj);
                _layerColors[name] = color;
                
                var cLay = new CadLayer(name) 
                { 
                    Color = color, 
                    IsVisible = true 
                };
                
                try { cLay.IsVisible = layer.IsOn; } catch {} // IsOn yoksa true kalır
                
                db.AddLayer(cLay);
            }

            // 2. BLOKLARI HAZIRLA
            var blockRecords = new List<CadBlockRecord>();
            // doc.BlockRecords normalde List değil Table olabilir, foreach çalışır
            foreach (var item in doc.BlockRecords) 
            {
                dynamic block = item;
                Vector3D bp = Vector3D.Zero;
                
                // Koordinat Okuma (Origin vs BasePoint)
                try { 
                    var p = block.Origin; 
                    bp = new Vector3D(p.X, p.Y, p.Z);
                } catch { 
                     try { var p = block.BasePoint; bp = new Vector3D(p.X, p.Y, p.Z); } catch {}
                }
                
                var bRec = new CadBlockRecord { Name = block.Name, BasePoint = bp };
                foreach (var ent in block.Entities) 
                {
                    bRec.Entities.AddRange(Convert(ent, asReference, null));
                }
                blockRecords.Add(bRec);
            }

            // 3. ENTITY DÖNÜŞÜMÜ
            foreach (var entity in doc.Entities)
            {
                var converted = Convert(entity, asReference, blockRecords);
                foreach (var c in converted) db.AddEntity(c);
            }

            return db;
        }

        private IEnumerable<CadEntity> Convert(object item, bool asRef, List<CadBlockRecord>? blocks)
        {
            if (item == null) return Enumerable.Empty<CadEntity>();
            
            // AGRESİF FİLTRELEME: Mimari planda leke yapan her şeyi temizle.
            string typeName = item.GetType().Name;
            if (typeName == "Solid" || typeName == "Trace" || typeName == "Hatch" || 
                typeName == "AttributeDefinition" || typeName == "Viewport" || 
                typeName == "Wipeout" || typeName == "Face3D" || typeName == "3DFace" || typeName == "Ray" || typeName == "XLine") 
                return Enumerable.Empty<CadEntity>();

            dynamic entity = item; 
            var result = new List<CadEntity>();
            
            uint color = GetColorDyn(entity, asRef);
            string layerName = "0";
            try { layerName = entity.Layer.Name; } catch {}

            // INSERT (Blok)
            if (typeName == "Insert")
            {
                try {
                    string bName = entity.Block.Name;
                    var record = blocks?.FirstOrDefault(b => b.Name == bName);
                    if (record != null)
                    {
                        Vector3D ip = new Vector3D(entity.InsertPoint.X, entity.InsertPoint.Y, 0);
                        Vector3D s = new Vector3D(entity.XScale, entity.YScale, entity.ZScale);
                        double rot = entity.Rotation;
                        double cos = Math.Cos(rot), sin = Math.Sin(rot);

                        foreach (var bEnt in record.Entities)
                        {
                            var cloned = bEnt.Clone();
                            if (CanInheritColor(cloned.Color)) cloned.Color = color; 
                            Transform(cloned, ip, s, cos, sin, record.BasePoint);
                            result.Add(cloned);
                        }
                    }
                } catch {}
                return result;
            }

            CadEntity? n = null;

            if (typeName == "TextEntity")
            {
                 n = new Afney.Cad.Domain.Entities.Basic.TextEntity(Clean(entity.Value), new Vector3D(entity.InsertPoint.X, entity.InsertPoint.Y, 0), Math.Min((double)entity.Height, 500.0), (double)entity.Rotation * 180.0/Math.PI);
            }
            else if (typeName == "MText")
            {
                 n = new Afney.Cad.Domain.Entities.Basic.TextEntity(Clean(entity.Value), new Vector3D(entity.InsertPoint.X, entity.InsertPoint.Y, 0), Math.Min((double)entity.Height, 500.0), (double)entity.Rotation * 180.0/Math.PI);
            }
            else if (typeName == "Line")
            {
                 n = new LineEntity(new Vector3D(entity.StartPoint.X, entity.StartPoint.Y, 0), new Vector3D(entity.EndPoint.X, entity.EndPoint.Y, 0));
            }
            else if (typeName == "LwPolyline")
            {
                // MÜHENDİSLİK DETAYI: Polyline Kalınlığını YOKSAY (Hairline Force)
                // Vertex'leri alırken sadece XY koordinatlarını alıyoruz, Width bilgisini okumuyoruz.
                var verts = new List<Vector3D>();
                foreach(var v in entity.Vertices) { verts.Add(new Vector3D(v.Location.X, v.Location.Y, 0)); }
                if (verts.Count >= 2) n = new LwPolylineEntity(verts, entity.IsClosed);
            }
            else if (typeName == "Circle")
            {
                n = new CircleEntity(new Vector3D(entity.Center.X, entity.Center.Y, 0), (double)entity.Radius);
            }
            else if (typeName == "Arc")
            {
                n = new ArcEntity(new Vector3D(entity.Center.X, entity.Center.Y, 0), (double)entity.Radius, (double)entity.StartAngle, (double)entity.EndAngle);
            }

            if (n != null) { n.Color = color; n.Layer = layerName; result.Add(n); }
            return result;
        }

        private bool CanInheritColor(uint c)
        {
            return c == 0xFFFFFFFF || c == 0xFF000000 || c == 0;
        }

        private uint ResolveColorDyn(dynamic c)
        {
            try {
                if (c.IsTrueColor) return (uint)(0xFF000000 | (uint)((c.R << 16) | (c.G << 8) | c.B));
                int index = c.Index;
                switch (index)
                {
                    case 1: return 0xFFFF0000;
                    case 2: return 0xFFFFFF00;
                    case 3: return 0xFF00FF00;
                    case 4: return 0xFF00FFFF;
                    case 5: return 0xFF0066FF; 
                    case 6: return 0xFFFF00FF;
                    case 7: return 0xFFFFFFFF;
                    case 0: case 256: return 0xFFFFFFFF;
                    case 8: return 0xFFAAAAAA;
                    case 9: return 0xFF888888;
                    case 250: case 251: return 0xFF666666;
                    default: return 0xFFCCCCCC;
                }
            } catch { return 0xFFFFFFFF; }
        }

        private uint GetColorDyn(dynamic e, bool asRef)
        {
            if (asRef) return 0xFF505050;
            try {
                dynamic c = e.Color;
                int idx = c.Index;
                if (idx == 256)
                {
                    try {
                        string lName = e.Layer.Name;
                        if (_layerColors.TryGetValue(lName, out uint lc)) return lc;
                    } catch {}
                    return 0xFFFFFFFF;
                }
                if (idx == 0) return 0xFFFFFFFF;
                return ResolveColorDyn(c);
            } catch { return 0xFFFFFFFF; }
        }

        private void Transform(CadEntity e, Vector3D ip, Vector3D s, double c, double sn, Vector3D bp)
        {
             if (e is LineEntity l) { l.StartPoint = T(l.StartPoint, ip, s, c, sn, bp); l.EndPoint = T(l.EndPoint, ip, s, c, sn, bp); }
             else if (e is CircleEntity ci) { ci.Center = T(ci.Center, ip, s, c, sn, bp); ci.Radius *= Math.Max(s.X, s.Y); }
             else if (e is ArcEntity a) { a.Center = T(a.Center, ip, s, c, sn, bp); a.Radius *= Math.Max(s.X, s.Y); a.StartAngle += Math.Atan2(sn, c); a.EndAngle += Math.Atan2(sn, c); }
             else if (e is Afney.Cad.Domain.Entities.Basic.TextEntity t) { t.Position = T(t.Position, ip, s, c, sn, bp); t.Rotation += Math.Atan2(sn, c) * 180.0 / Math.PI; }
             else if (e is LwPolylineEntity p) { p.Vertices = p.Vertices.Select(v => T(v, ip, s, c, sn, bp)).ToList(); }
        }

        private Vector3D T(Vector3D p, Vector3D ins, Vector3D s, double c, double sn, Vector3D bp)
        {
            double lx = (p.X - bp.X) * s.X;
            double ly = (p.Y - bp.Y) * s.Y;
            double rx = lx * c - ly * sn;
            double ry = lx * sn + ly * c;
            return new Vector3D(ins.X + rx, ins.Y + ry, 0);
        }

        private string Clean(string s) => string.IsNullOrEmpty(s) ? "" : Regex.Replace(s, @"\\P|\\A1;|\\{.+\\}|\\.+;", " ").Trim();
    }
}
