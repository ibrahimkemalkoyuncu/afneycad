using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

namespace Afney.Cad.Infrastructure.DwgEngine;

/*
   NE: DWG Binary Akış Oluşturucu (DwgBinaryStreamBuilder)
   NEDEN: AutoCAD'in bit bazlı (bit-packing) veri yapısını oluşturmak için. (Serdar - Principal Architect)
*/
public class DwgBinaryStreamBuilder : IDisposable
{
    private readonly MemoryStream _output;
    private readonly BinaryWriter _writer;
    private byte _bitBuffer;
    private int _bitOffset = 8;

    public DwgBinaryStreamBuilder()
    {
        _output = new MemoryStream();
        _writer = new BinaryWriter(_output);
    }

    public void WriteBit(bool bit)
    {
        if (bit)
            _bitBuffer |= (byte)(1 << (_bitOffset - 1));

        _bitOffset--;

        if (_bitOffset == 0)
        {
            _writer.Write(_bitBuffer);
            _bitBuffer = 0;
            _bitOffset = 8;
        }
    }

    public void WriteBits(int value, int count)
    {
        for (int i = count - 1; i >= 0; i--)
        {
            WriteBit(((value >> i) & 1) == 1);
        }
    }

    public void WriteSentinel(byte[] sentinel)
    {
        FlushBits();
        _writer.Write(sentinel);
    }

    public void FlushBits()
    {
        if (_bitOffset < 8)
        {
            _writer.Write(_bitBuffer);
            _bitBuffer = 0;
            _bitOffset = 8;
        }
    }

    public byte[] ToArray()
    {
        FlushBits();
        return _output.ToArray();
    }

    public void Dispose()
    {
        _writer.Dispose();
        _output.Dispose();
    }
}
