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

public class VertexLayoutBlob : BundleBlob
{
    public List<string> SemanticNames { get; set; } = new();
    public List<D3D12_INPUT_LAYOUT_DESC> Elements { get; set; } = new();
    public List<DXGI_FORMAT> PackedFormats { get; set; } = new();
    public uint Flags { get; set; }

    public D3D12_INPUT_LAYOUT_DESC GetElement(string name, int elementIndex)
    {
        for (int i = 0; i < Elements.Count; i++)
        {
            D3D12_INPUT_LAYOUT_DESC elem = Elements[i];
            if (SemanticNames[elem.SemanticNameIndex] == name && elem.SemanticIndex == elementIndex)
                return elem;
        }

        return null;
    }

    public int GetDataOffsetOfElement(string semantic, int semanticIndex)
    {
        int off = 0;
        for (int i = 0; i < Elements.Count; i++)
        {
            D3D12_INPUT_LAYOUT_DESC elem = Elements[i];

            if (SemanticNames[elem.SemanticNameIndex] == semantic && elem.SemanticIndex == semanticIndex)
                return off;

            off += GetSizeOfElementFormat(PackedFormats[i]);

            if (i + 1 < Elements.Count && off % 4 != 0)
            {
                if (GetSizeOfElementFormat(PackedFormats[i + 1]) >= 4)
                    off += off % 4;
            }
        }

        return off;
    }

    public byte GetTotalVertexSize()
    {
        byte size = 0;
        for (int i = 0; i < Elements.Count; i++)
        {
            D3D12_INPUT_LAYOUT_DESC elem = Elements[i];

            size += GetSizeOfElementFormat(PackedFormats[i]);

            if (i + 1 < Elements.Count && size % 4 != 0)
            {
                if (GetSizeOfElementFormat(PackedFormats[i + 1]) >= 4)
                    size += (byte)(size % 4);
            }
        }

        return size;
    }


    private static byte GetSizeOfElementFormat(DXGI_FORMAT format)
    {
        return format switch
        {
            DXGI_FORMAT.DXGI_FORMAT_R8G8_UNORM => 2,
            DXGI_FORMAT.DXGI_FORMAT_R8G8_SINT => 2,
            DXGI_FORMAT.DXGI_FORMAT_R24_UNORM_X8_TYPELESS => 4,
            DXGI_FORMAT.DXGI_FORMAT_R8G8_TYPELESS => 2,
            DXGI_FORMAT.DXGI_FORMAT_X32_TYPELESS_G8X24_UINT => 8,
            _ => throw new Exception("Unsupported"),
        };
    }

    public override void ReadBlobData(BinaryStream bs)
    {
        ushort semanticCount = bs.ReadUInt16();
        for (int i = 0; i < semanticCount; i++)
        {
            string name = bs.ReadString(StringCoding.Int32CharCount);
            SemanticNames.Add(name);
        }

        ushort elementCount = bs.ReadUInt16();
        for (int i = 0; i < elementCount; i++)
        {
            var desc = new D3D12_INPUT_LAYOUT_DESC();
            desc.Read(bs);
            Elements.Add(desc);
        }

        if (IsAtLeastVersion(1, 0))
        {
            for (int i = 0; i < elementCount; i++)
                PackedFormats.Add((DXGI_FORMAT)bs.ReadInt32());
        }

        if (IsAtLeastVersion(1, 1))
            Flags = bs.ReadUInt32();
    }

    public override void SerializeBlobData(BinaryStream bs)
    {
        bs.WriteUInt16((ushort)SemanticNames.Count);
        foreach (string semanticName in SemanticNames)
        {
            bs.WriteString(semanticName, StringCoding.Int32CharCount);
        }

        bs.WriteUInt16((ushort)Elements.Count);
        foreach (D3D12_INPUT_LAYOUT_DESC element in Elements)
        {
            element.Serialize(bs);
        }

        if (IsAtLeastVersion(1, 0))
        {
            for (int i = 0; i < PackedFormats.Count; i++)
                bs.WriteInt32((int)PackedFormats[i]);
            if (IsAtLeastVersion(1, 1))
                bs.WriteUInt32(Flags);
        }
    }
}

public class D3D12_INPUT_LAYOUT_DESC
{
    public short SemanticNameIndex;
    public short SemanticIndex;
    public int InputSlot;
    public DXGI_FORMAT Format;
    public int AlignedByteOffset;
    public int InstanceDataStepRate;

    public void Read(BinaryStream bs)
    {
        SemanticNameIndex = bs.ReadInt16();
        SemanticIndex = bs.ReadInt16();
        InputSlot = bs.ReadInt32();
        Format = (DXGI_FORMAT)bs.ReadInt32();
        AlignedByteOffset = bs.ReadInt32();
        InstanceDataStepRate = bs.ReadInt32();
    }

    public void Serialize(BinaryStream bs)
    {
        bs.WriteInt16(SemanticNameIndex);
        bs.WriteInt16(SemanticIndex);
        bs.WriteInt32(InputSlot);
        bs.WriteInt32((int)Format);
        bs.WriteInt32(AlignedByteOffset);
        bs.WriteInt32(InstanceDataStepRate);
    }
}