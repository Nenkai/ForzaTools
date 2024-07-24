using Syroot.BinaryData;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using System.Runtime.InteropServices;

namespace ForzaTools.Bundles.Blobs;

public class MaterialShaderParameterBlob : BundleBlob
{
    public uint Unk1 { get; set; }
    public uint Unk2 { get; set; }
    public uint Unk3 { get; set; }
    public byte Unk4 { get; set; }


    public override void ReadBlobData(BinaryStream bs)
    {
        Unk1 = bs.ReadUInt32();
        Unk2 = bs.ReadUInt32();
        Unk3 = bs.ReadUInt32();
        Unk4 = bs.Read1Byte();
    }

    public override void SerializeBlobData(BinaryStream bs)
    {
        bs.WriteUInt32(Unk1);
        bs.WriteUInt32(Unk2);
        bs.WriteUInt32(Unk3);
        bs.WriteByte(Unk4);
    }
}
