using Syroot.BinaryData;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace ForzaTools.Bundle.Blobs;

public class MeshBlob : BundleBlob
{
    public short UnkV9_1 { get; set; }
    public short UnkV9_2 { get; set; }
    public short UnkV9_3 { get; set; }
    public short UnkV9_4 { get; set; }

    public short Unk1 { get; set; }
    public short Unk2 { get; set; }
    public byte LODLevel1 { get; set; }
    public byte LODLevel2 { get; set; }
    public short Unk3 { get; set; }
    public byte Unk4 { get; set; }

    public byte UnkV2 { get; set; }
    public byte UnkV2_2 { get; set; }
    public byte UnkV3 { get; set; }

    public byte Unk5 { get; set; }
    public short Unk6 { get; set; }
    public int Unk7 { get; set; }
    public int Unk8 { get; set; }

    public uint FaceStartIndex { get; set; }
    public uint UnkColor { get; set; }
    public uint FaceCount { get; set; }
    public uint VertexStartIndex { get; set; }

    public float UnkV6 { get; set; }
    public uint NumVerts { get; set; }

    public uint Unk9 { get; set; }

    public List<UnkEntry> UnkEntries { get; set; } = new List<UnkEntry>();
    public record UnkEntry(uint a, uint b, uint c, uint d);

    public int VertexLayoutIndex { get; set; }
    public int UnkV4 { get; set; }

    public int[] UnkArr { get; set; }

    public uint UnkV1 { get; set; }

    public Vector4[] Vecs { get; set; }
    public Vector4 UnkV8Vec { get; set; }
    public Vector4 UnkV8Vec2 { get; set; }

    public override void ReadBlobData(BinaryStream bs)
    {
        if (IsAtLeastVersion(1, 9))
        {
            UnkV9_1 = bs.ReadInt16();
            UnkV9_2 = bs.ReadInt16();
            UnkV9_3 = bs.ReadInt16();
            UnkV9_4 = bs.ReadInt16();
        }

        Unk1 = bs.ReadInt16();
        Unk2 = bs.ReadInt16();
        LODLevel1 = bs.Read1Byte();
        LODLevel2 = bs.Read1Byte();
        Unk3 = bs.ReadInt16();
        Unk4 = bs.Read1Byte();

        if (IsAtLeastVersion(1, 2))
        {
            UnkV2 = bs.Read1Byte();
            UnkV2_2 = bs.Read1Byte();
        }

        if (IsAtLeastVersion(1, 3))
        {
            UnkV3 = bs.Read1Byte();
        }

        Unk5 = bs.Read1Byte();
        Unk6 = bs.ReadInt16();
        Unk7 = bs.ReadInt32();
        Unk8 = bs.ReadInt32();

        FaceStartIndex = bs.ReadUInt32();
        UnkColor = bs.ReadUInt32();
        FaceCount = bs.ReadUInt32();
        VertexStartIndex = bs.ReadUInt32();

        if (IsAtLeastVersion(1, 6))
        {
            UnkV6 = bs.ReadSingle();
            NumVerts = bs.ReadUInt32();
        }

        Unk9 = bs.ReadUInt32();

        int unkCount = bs.ReadInt32();
        for (int i = 0; i < unkCount; i++)
        {
            uint a = bs.ReadUInt32();
            uint b = bs.ReadUInt32();
            uint c = bs.ReadUInt32();
            uint d = bs.ReadUInt32();
            UnkEntries.Add(new UnkEntry(a, b, c, d));
        }

        if (IsAtLeastVersion(1, 4))
        {
            VertexLayoutIndex = bs.ReadInt32();
            UnkV4 = bs.ReadInt32();
        }

        int unkCount2 = bs.ReadInt32();
        UnkArr = bs.ReadInt32s(unkCount2);

        if (IsAtLeastVersion(1, 1))
        {
            UnkV1 = bs.ReadUInt32();
        }

        Vecs = MemoryMarshal.Cast<byte, Vector4>(bs.ReadBytes(0x10 * 5)).ToArray();

        if (IsAtLeastVersion(1, 8))
        {
            UnkV8Vec = MemoryMarshal.Read<Vector4>(bs.ReadBytes(0x10));
            UnkV8Vec2 = MemoryMarshal.Read<Vector4>(bs.ReadBytes(0x10));
        }
    }

    public override void SerializeBlobData(BinaryStream bs)
    {
        if (IsAtLeastVersion(1, 9))
        {
            bs.WriteInt16(UnkV9_1);
            bs.WriteInt16(UnkV9_2);
            bs.WriteInt16(UnkV9_3);
            bs.WriteInt16(UnkV9_4);
        }

        bs.WriteInt16(Unk1);
        bs.WriteInt16(Unk2);
        bs.WriteByte(LODLevel1);
        bs.WriteByte(LODLevel2);
        bs.WriteInt16(Unk3);
        bs.WriteByte(Unk4);

        if (IsAtLeastVersion(1, 2))
        {
            bs.WriteByte(UnkV2);
            bs.WriteByte(UnkV2_2);
        }

        if (IsAtLeastVersion(1, 3))
        {
            bs.WriteByte(UnkV3);
        }

        bs.WriteByte(Unk5);
        bs.WriteInt16(Unk6);
        bs.WriteInt32(Unk7);
        bs.WriteInt32(Unk8);

        bs.WriteUInt32(FaceStartIndex);
        bs.WriteUInt32(UnkColor);
        bs.WriteUInt32(FaceCount);
        bs.WriteUInt32(VertexStartIndex);

        if (IsAtLeastVersion(1, 6))
        {
            bs.WriteSingle(UnkV6);
            bs.WriteUInt32(NumVerts);
        }

        bs.WriteUInt32(Unk9);

        bs.WriteUInt32((uint)UnkEntries.Count);
        for (int i = 0; i < UnkEntries.Count; i++)
        {
            bs.WriteUInt32(UnkEntries[i].a);
            bs.WriteUInt32(UnkEntries[i].b);
            bs.WriteUInt32(UnkEntries[i].c);
            bs.WriteUInt32(UnkEntries[i].d);
        }

        if (IsAtLeastVersion(1, 4))
        {
            bs.WriteInt32(VertexLayoutIndex);
            bs.WriteInt32(UnkV4);
        }

        bs.WriteInt32(UnkArr.Length);
        bs.WriteInt32s(UnkArr);

        if (IsAtLeastVersion(1, 1))
        {
            bs.WriteUInt32(UnkV1);
        }

        bs.Write(MemoryMarshal.Cast<Vector4, byte>(Vecs));

        if (IsAtLeastVersion(1, 8))
        {
            bs.WriteVector4(UnkV8Vec);
            bs.WriteVector4(UnkV8Vec2);
        }
    }
}
