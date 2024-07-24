using Syroot.BinaryData;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForzaTools.Bundles.Metadata;

public class AtlasMetadata : BundleMetadata
{
    public byte Unk { get; set; }
    public byte Unk2 { get; set; }

    public override void ReadMetadataData(BinaryStream bs)
    {
        Unk = bs.Read1Byte();
        Unk2 = bs.Read1Byte();
    }

    public override void SerializeMetadataData(BinaryStream bs)
    {
        bs.WriteByte(Unk);
        bs.WriteByte(Unk2);
    }
}
