using System;
using System.Collections.Generic;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Domain.Blocks;

/*
    NE: Blok Tanımı (Block Definition / Block Record)
    NEDEN: Birden fazla nesneyi (Çizgi, Çember vb.) tek bir isim altında gruplamak ve tekrar kullanmak için.
    
    NASIL (Mühendislik Detayı):
    - Bir blok tanımı, tıpki bir mini-veritabanı gibi kendi içinde Entity listesi barındırır.
    - Orijin noktası (BasePoint) referans alınarak çizilir.
    - Model Space'te görünmez; sadece 'BlockReferenceEntity' (Insert) tarafından referans verildiğinde görünür.
*/
public class CadBlockRecord
{
    public string Name { get; set; } = string.Empty;
    public Vector3D BasePoint { get; set; } = Vector3D.Zero;
    public List<CadEntity> Entities { get; set; } = new();
    
    public CadBlockRecord(string name)
    {
        Name = name;
    }
}
