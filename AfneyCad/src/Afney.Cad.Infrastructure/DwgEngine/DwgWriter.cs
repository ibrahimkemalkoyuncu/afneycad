using System;
using System.IO;
using System.Collections.Generic;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Infrastructure.DwgEngine;

/*
   NE: DWG Dosya Yazıcı (DwgWriter)
   NEDEN: AfneyCAD iç CAD dokümanını AutoCAD'in anlayabileceği .dwg formatında disk üzerine yazmak için.
*/
public class DwgWriter
{
    private readonly DwgEntitySerializer _serializer = new();

    public void Write(DwgDocument doc, string filePath)
    {
        using (var builder = new DwgBinaryStreamBuilder())
        {
            // 1. HEADER SECTION (AC1032 - AutoCAD 2018)
            byte[] header = System.Text.Encoding.ASCII.GetBytes(doc.Header.Version); // AC1032...
            builder.WriteSentinel(header);

            // 2. CLASS SECTION & OBJECT SECTION
            // (Burada Handle re-mapping ve Object Table yazılır)

            // 3. ENTITY SECTION (Nesne Verileri)
            foreach (var proxy in doc.Entities)
            {
                var binaryData = _serializer.Serialize(proxy);
                builder.WriteSentinel(binaryData);
            }

            // 4. FILE TERMINAL (Sentinel + CRC)
            byte[] terminalSentinel = new byte[16]; // Örnek terminal
            builder.WriteSentinel(terminalSentinel);

            File.WriteAllBytes(filePath, builder.ToArray());
        }
    }
}

/*
   NE: DWG Nesne Serileştirici (DwgEntitySerializer)
   NEDEN: Her bir CAD nesnesini (Line, Circle, Block) DWG spesifikasyonundaki bit-field karşılıklarına dönüştürmek için.
*/
public class DwgEntitySerializer
{
    public byte[] Serialize(DwgEntityProxy proxy)
    {
        using (var builder = new DwgBinaryStreamBuilder())
        {
            // Örnek: LINE Serileştirme (AutoCAD R2018 formatı)
            if (proxy.DwgType == "LINE")
            {
                // AutoCAD Handle (Bit-packing)
                builder.WriteBits((int)proxy.Handle, 8); 
                
                // Koordinatları yaz (Bit-stream formatında)
                var start = (Vector3D)proxy.RawProperties["Start"];
                var end = (Vector3D)proxy.RawProperties["End"];
                
                // Not: Gerçek AutoCAD formatında koordinatlar 'Modular RD' formatında saklanır.
                WriteDouble(builder, start.X);
                WriteDouble(builder, start.Y);
                WriteDouble(builder, end.X);
                WriteDouble(builder, end.Y);
            }

            return builder.ToArray();
        }
    }

    private void WriteDouble(DwgBinaryStreamBuilder builder, double value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        foreach (byte b in bytes)
            builder.WriteBits(b, 8);
    }
}
