using Syroot.BinaryData;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using System.Runtime.InteropServices;

namespace ForzaTools.Bundles.Blobs;

public class MorphBlob : BundleBlob
{
    public ushort Unk { get; set; }
    public string Name { get; set; }

    public override void ReadBlobData(BinaryStream bs)
    {
        Unk = bs.ReadUInt16();
        Name = bs.ReadString(StringCoding.Int32CharCount);
    }

    public override void SerializeBlobData(BinaryStream bs)
    {
        bs.WriteUInt16(Unk);
        bs.WriteString(Name, StringCoding.Int32CharCount);
    }
}
