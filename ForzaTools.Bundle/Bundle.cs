using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Syroot.BinaryData;
using ForzaTools.Bundle.Blobs;

namespace ForzaTools.Bundle;

public class Bundle
{
    public const uint BundleTag = 0x47727562;
    public byte VersionMajor { get; set; }
    public byte VersionMinor { get; set; }

    public List<BundleBlob> Blobs { get; set; } = new List<BundleBlob>();

    // Textures (swatchbin)
    const uint TAG_BLOB_TextureContentBlob = 0x54584342; // "TXCB"

    // Models (modelbin)
    const uint TAG_BLOB_Skeleton = 0x536B656C; // "Skel"
    const uint TAG_BLOB_Morph = 0x4D727068; // "Mrph"
    const uint TAG_BLOB_Material = 0x4D617449; // "Matl"
    const uint TAG_BLOB_Mesh = 0x4D657368; // "Mesh"
    const uint TAG_BLOB_IndexBuffer = 0x496E6442; // "IndB"
    const uint TAG_BLOB_VertexLayout = 0x564C6179; // "VLay"
    const uint TAG_BLOB_VertexBuffer = 0x56657242; // "VerB"
    const uint TAG_BLOB_MorphBuffer = 0x4D427566; // "MBuf"
    const uint TAG_BLOB_Skin = 0x536B696E; // "Skin"
    const uint TAG_BLOB_Model = 0x4D6F646C; // "Modl"

    // Materials (materialbin)
    const uint TAG_BLOB_MaterialResource = 0x4D415449; // "MATI"
    const uint TAG_BLOB_MaterialShaderParameter = 0x4D545052; // "MTPR"


    public void Load(Stream stream)
    {
        long baseBundleOffset = stream.Position;

        var bs = new BinaryStream(stream);
        uint tag = bs.ReadUInt32();
        if (tag != BundleTag)
            throw new InvalidDataException("Unexpected tag for bundle");

        VersionMajor = bs.Read1Byte();
        VersionMinor = bs.Read1Byte();

        uint blobCount;
        if (VersionMinor >= 1)
        {
            bs.ReadInt16();
            uint headerSize = bs.ReadUInt32();
            uint totalSize = bs.ReadUInt32();
            blobCount = bs.ReadUInt32();
        }
        else
        {
            blobCount = bs.ReadUInt16();
            bs.Position += 0x08;
        }

        long basePos = bs.Position;
        for (int i = 0; i < blobCount; i++)
        {
            bs.Position = basePos + (i * BundleBlob.InfoSize);
            uint blobTag = bs.ReadUInt32();
            bs.Position -= 4;

            BundleBlob blob = GetBlobByTag(blobTag);
            blob.Read(bs, baseBundleOffset);
            Blobs.Add(blob);
        }
    }

    public void Serialize(Stream stream)
    {
        long baseBundleOffset = stream.Position;

        var bs = new BinaryStream(stream);

        // Write header later
        bs.Position += 0x14;

        // Skip blob headers for now
        long blobHeadersOffset = bs.Position;
        bs.Position += Blobs.Count * BundleBlob.InfoSize;

        // Write metadatas
        long lastMetadataOffset = bs.Position;
        for (int i = 0; i < Blobs.Count; i++)
        {
            BundleBlob blob = Blobs[i];
            long metadataOffset = bs.Position;

            blob.SerializeMetadatas(bs);
            lastMetadataOffset = bs.Position;

            bs.Position = blobHeadersOffset + (i * BundleBlob.InfoSize);
            bs.Position += 6;
            bs.WriteUInt16((ushort)blob.Metadatas.Count);
            bs.WriteUInt32((uint)(metadataOffset - baseBundleOffset));

            bs.Position = lastMetadataOffset;
        }

        // Align metadata block
        bs.Align(0x04, grow: true);

        long headerSize = bs.Position - baseBundleOffset;

        // Write blob data
        long lastBlobDataOffset = bs.Position;
        long lastBlobDataOffsetWithAlign = bs.Position;

        for (int i = 0; i < Blobs.Count; i++)
        {
            BundleBlob blob = Blobs[i];
            long blobDataOffset = bs.Position;

            blob.SerializeBlobData(bs);
            lastBlobDataOffset = bs.Position;

            // Align it. Size does not account for it
            bs.Align(0x04, grow: true);
            lastBlobDataOffsetWithAlign = bs.Position;

            bs.Position = blobHeadersOffset + (i * BundleBlob.InfoSize);
            bs.WriteUInt32(blob.Tag);
            bs.WriteByte(blob.VersionMajor);
            bs.WriteByte(blob.VersionMinor);
            bs.Position += 0x06;

            bs.WriteUInt32((uint)(blobDataOffset - baseBundleOffset));
            bs.WriteUInt32((uint)(lastBlobDataOffset - blobDataOffset));
            bs.WriteUInt32((uint)(lastBlobDataOffset - blobDataOffset));
            bs.Position = lastBlobDataOffsetWithAlign;
        }

        bs.Position = baseBundleOffset;

        // Write header
        bs.WriteUInt32(BundleTag);
        bs.WriteByte(VersionMajor);
        bs.WriteByte(VersionMinor);
        bs.WriteInt16(0);
        bs.WriteUInt32((uint)headerSize);
        bs.WriteUInt32((uint)(lastBlobDataOffsetWithAlign - baseBundleOffset));
        bs.WriteUInt32((uint)Blobs.Count);

        bs.Position = lastBlobDataOffsetWithAlign;
    }

    private BundleBlob GetBlobByTag(uint tag)
    {
        return tag switch
        {
            TAG_BLOB_Skeleton => new SkeletonBlob(),
            TAG_BLOB_Morph => new MorphBlob(),
            TAG_BLOB_Material => new MaterialBlob(),
            TAG_BLOB_MaterialResource => new MaterialResourceBlob(),
            TAG_BLOB_MaterialShaderParameter => new MaterialShaderParameterBlob(),
            TAG_BLOB_Mesh => new MeshBlob(),
            TAG_BLOB_IndexBuffer => new IndexBufferBlob(),
            TAG_BLOB_VertexLayout => new VertexLayoutBlob(),
            TAG_BLOB_VertexBuffer => new VertexBufferBlob(),
            TAG_BLOB_MorphBuffer => new MorphBufferBlob(),
            TAG_BLOB_Model => new ModelBlob(),
            _ => throw new Exception($"Unimplemented tag {tag:X8}")
        };
    }
}
