using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

using Syroot.BinaryData;

using ForzaTools.Bundles.Metadata;
using static System.Reflection.Metadata.BlobBuilder;
using System.Xml.Linq;

namespace ForzaTools.Bundles;

public abstract class BundleBlob
{
    public const int InfoSize = 0x18;

    public uint Tag { get; set; }
    public byte VersionMajor { get; set; }
    public byte VersionMinor { get; set; }

    public List<BundleMetadata> Metadatas { get; set; } = new List<BundleMetadata>();

    private byte[] _data { get; set; }

    public T GetMetadataByTag<T>(uint tag) where T : BundleMetadata
    {
        foreach (var metadata in Metadatas)
        {
            if (metadata.Tag == tag)
                return (T)metadata;
        }

        return null;
    }

    public virtual void Read(BinaryStream bs, long baseBundleOffset)
    {
        Tag = bs.ReadUInt32();
        VersionMajor = bs.Read1Byte();
        VersionMinor = bs.Read1Byte();

        uint metadataCount = bs.ReadUInt16();
        uint metadataOffset = bs.ReadUInt32();
        uint dataOffset = bs.ReadUInt32();
        uint dataSize = bs.ReadUInt32();
        bs.ReadUInt32();

        long basePos = bs.Position;
        for (int i = 0; i < metadataCount; i++)
        {
            bs.Position = baseBundleOffset + metadataOffset + (i * BundleMetadata.InfoSize);
            uint metadataTag = bs.ReadUInt32();
            bs.Position -= 4;

            BundleMetadata metadata = GetMetadataObjectByTag(metadataTag);
            metadata.Read(bs);

            Metadatas.Add(metadata);
        }

        bs.Position = baseBundleOffset + dataOffset;
        _data = bs.ReadBytes((int)dataSize);

        bs.Position = baseBundleOffset + dataOffset;
        ReadBlobData(bs);

    }

    public abstract void ReadBlobData(BinaryStream bs);

    public abstract void SerializeBlobData(BinaryStream bs);

    private BundleMetadata GetMetadataObjectByTag(uint tag)
    {
        return tag switch
        {
            BundleMetadata.TAG_METADATA_Name => new NameMetadata(),
            BundleMetadata.TAG_METADATA_Identifier => new IdentifierMetadata(),
            BundleMetadata.TAG_METADATA_Atlas => new AtlasMetadata(),
            BundleMetadata.TAG_METADATA_BBox => new BoundaryBoxMetadata(),
            BundleMetadata.TAG_METADATA_TextureContentHeader => new TextureContentHeaderMetadata(),
            _ => throw new NotImplementedException($"Unimplemented metadata tag {tag:X8}"),
        };
    }

    public void SerializeMetadatas(BinaryStream bs)
    {
        long headersStartOffset = bs.Position;
        long lastDataPos = bs.Position + (BundleMetadata.InfoSize * Metadatas.Count);
        for (int j = 0; j < Metadatas.Count; j++)
        {
            bs.Position = lastDataPos;

            long headerOffset = headersStartOffset + (BundleMetadata.InfoSize * j);
            long dataStartOffset = lastDataPos;

            BundleMetadata metadata = Metadatas[j];
            metadata.SerializeMetadataData(bs);

            ulong relativeOffset = (ulong)(lastDataPos - headerOffset);

            lastDataPos = bs.Position;

            bs.Position = headerOffset;
            bs.WriteUInt32(metadata.Tag);

            ulong metadataSize = (ulong)(lastDataPos - dataStartOffset);
            Debug.Assert(metadataSize <= ushort.MaxValue);

            ushort flags = (ushort)(metadataSize << 4 | (ushort)(metadata.Version & 0b1111)); // 12 bits size, 4 bits unk
            bs.WriteUInt16(flags);

            Debug.Assert(relativeOffset <= ushort.MaxValue);
            bs.WriteUInt16((ushort)relativeOffset);
        }

        bs.Position = lastDataPos;
    }

    public byte[] GetContents() => _data;

    public bool IsAtMostVersion(byte versionMajor, byte versionMinor)
    {
        return VersionMajor != versionMajor && (VersionMajor != versionMajor || versionMinor > 2);
    }

    public bool IsAtLeastVersion(byte versionMajor, byte versionMinor)
    {
        return VersionMajor > versionMajor || (VersionMajor == versionMajor && VersionMinor >= versionMinor);
    }
}
