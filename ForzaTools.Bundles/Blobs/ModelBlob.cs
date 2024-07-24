using Syroot.BinaryData;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using System.Runtime.InteropServices;

namespace ForzaTools.Bundles.Blobs;

public class ModelBlob : BundleBlob
{
    public ushort MeshCount { get; set; }
    public ushort BuffersCount { get; set; }
    public ushort VertexLayoutCount { get; set; }
    public ushort MaterialCount { get; set; }
    public ushort Unk { get; set; }
    public ushort Unk2 { get; set; }
    public ushort Unk3 { get; set; }
    public ushort Unk4 { get; set; }


    public override void ReadBlobData(BinaryStream bs)
    {
        MeshCount = bs.ReadUInt16();
        BuffersCount = bs.ReadUInt16();
        VertexLayoutCount = bs.ReadUInt16();
        MaterialCount = bs.ReadUInt16();
        Unk = bs.ReadUInt16();
        Unk2 = bs.ReadUInt16();
        Unk3 = bs.ReadUInt16();
        Unk4 = bs.ReadUInt16();
    }

    public override void SerializeBlobData(BinaryStream bs)
    {
        bs.WriteUInt16(MeshCount);
        bs.WriteUInt16(BuffersCount);
        bs.WriteUInt16(VertexLayoutCount);
        bs.WriteUInt16(MaterialCount);
        bs.WriteUInt16(Unk);
        bs.WriteUInt16(Unk2);
        bs.WriteUInt16(Unk3);
        bs.WriteUInt16(Unk4);
    }
}
