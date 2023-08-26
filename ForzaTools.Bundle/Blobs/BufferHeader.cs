using Syroot.BinaryData;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using System.Runtime.InteropServices;

namespace ForzaTools.Bundle.Blobs;

public class BufferHeader
{
    public int Count;
    public int TotalSize;
    public byte BufferWidth;
    public byte Pad;
    public short Unk;
    public int Type;
    public byte[] Data { get; set; }

    public void Read(BinaryStream bs)
    {
        Count = bs.ReadInt32();
        TotalSize = bs.ReadInt32();
        BufferWidth = bs.Read1Byte();
        bs.Read1Byte();
        Unk = bs.ReadInt16();
        Type = bs.ReadInt32();
        Data = bs.ReadBytes(Count * BufferWidth);
    }

    public void Serialize(BinaryStream bs)
    {
        bs.WriteInt32(Count);
        bs.WriteInt32(TotalSize);
        bs.WriteByte(BufferWidth);
        bs.WriteByte(0);
        bs.WriteInt16(Unk);
        bs.WriteInt32(Type);
        bs.WriteBytes(Data);
    }
}
