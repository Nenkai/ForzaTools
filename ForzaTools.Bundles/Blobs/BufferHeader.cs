using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using System.Runtime.InteropServices;

using Syroot.BinaryData;

using ForzaTools.Shared;

namespace ForzaTools.Bundles.Blobs;

public class BufferHeader
{
    public ushort BufferWidth { get; set; }
    public byte NumElements { get; set; }
    public DXGI_FORMAT Type { get; set; }
    public byte[][] Data { get; set; }

    public void Read(BinaryStream bs)
    {
        int bufferCount = bs.ReadInt32();
        uint totalSize = bs.ReadUInt32();
        BufferWidth = bs.ReadUInt16();
        NumElements = bs.Read1Byte();
        bs.Read1Byte(); // Pad?
        Type = (DXGI_FORMAT)bs.ReadInt32();

        Data = new byte[bufferCount][];
        for (int i = 0; i < bufferCount; i++)
            Data[i] = bs.ReadBytes(BufferWidth);
    }

    public void Serialize(BinaryStream bs)
    {
        bs.WriteInt32(Data.Length);
        bs.WriteUInt32((uint)(Data.Length * BufferWidth));
        bs.WriteUInt16(BufferWidth);
        bs.WriteByte(NumElements);
        bs.WriteByte(0); // Pad?
        bs.WriteInt32((int)Type);

        for (int i = 0; i < Data.Length; i++)
            bs.WriteBytes(Data[i]);
    }
}
