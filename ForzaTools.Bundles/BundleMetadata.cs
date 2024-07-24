using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

using Syroot.BinaryData;

namespace ForzaTools.Bundles;

public abstract class BundleMetadata
{
    public const int InfoSize = 0x08;

    // Models (modelbin)
    public const uint TAG_METADATA_Name = 0x4E616D65; // "Name"
    public const uint TAG_METADATA_TextureContentHeader = 0x54584348; // "TXCH"
    public const uint TAG_METADATA_Identifier = 0x49642020; // "Id  "
    public const uint TAG_METADATA_BBox = 0x42426F78; // "BBox"

    // Materials (materialbin)
    public const uint TAG_METADATA_Atlas = 0x41545354; // "ATST"

    public uint Tag { get; set; }
    public byte Version { get; set; }
    public ushort Size { get; set; }

    private byte[] _data { get; set; }

    public virtual void Read(BinaryStream bs)
    {
        long basePos = bs.Position;
        Tag = bs.ReadUInt32();

        ushort flags = bs.ReadUInt16();
        Size = (ushort)(flags >> 4); // 12 bits
        Version = (byte)(flags & 0b1111); // 4 bits

        ushort offset = bs.ReadUInt16();

        bs.Position = basePos + offset;
        _data = bs.ReadBytes(Size);

        bs.Position = basePos + offset;
        ReadMetadataData(bs);
    }

    public virtual void SerializeHeader(BinaryStream bs)
    {
        bs.WriteUInt32(Tag);
        bs.WriteUInt32(0); // TODO
    }

    public abstract void ReadMetadataData(BinaryStream bs);

    public abstract void SerializeMetadataData(BinaryStream bs);

    public byte[] GetContents() => _data;
}
