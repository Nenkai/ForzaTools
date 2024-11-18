using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Syroot.BinaryData;

using ForzaTools.Shared;

namespace ForzaTools.Bundles.Metadata;

public class PCTextureContentHeader
{
    public Guid Guid { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public uint Depth { get; set; }
    public ushort NumSlices { get; set; }
    public byte MipLevels { get; set; }
    public byte UnkBitField { get; set; }
    public uint Transcoding { get; set; }
    public uint ColorProfile2;
    public uint ColorProfile;
    public uint dword2C { get; set; }

    public List<int> Params { get; set; } = new(); // [0] = encoding, [1] == pitchOrLinearSize

    public void Read(byte[] data)
    {
        using BinaryStream bs = new BinaryStream(new MemoryStream(data));
        uint offset1 = bs.ReadUInt32();
        uint offset2 = bs.ReadUInt32();

        Guid = new Guid(bs.ReadBytes(0x10));
        Width = bs.ReadInt32();
        Height = bs.ReadInt32();
        Depth = bs.ReadUInt32();
        NumSlices = bs.ReadUInt16();
        MipLevels = bs.Read1Byte();
        UnkBitField = bs.Read1Byte();
        Transcoding = bs.ReadUInt32();
        ColorProfile2 = bs.ReadUInt32();
        ColorProfile = bs.ReadUInt32();

        bs.Position = offset1;
        int nextValueOffset;
        do
        {
            int valueOffset = bs.ReadInt32();
            if (valueOffset == -1)
                break;

            nextValueOffset = bs.ReadInt32();

            bs.Position = valueOffset;
            int value = bs.ReadInt32();
            Params.Add(value);

            if (nextValueOffset != -1)
                bs.Position = nextValueOffset;

        } while (nextValueOffset != -1);

        // TODO: read offset2 params
    }

    public DXGI_FORMAT DetermineFormat()
    {
        if (Transcoding <= 1)
            return ColorProfile == 0 ? encodingToDxgiFormats[Params[0]].format : encodingToDxgiFormats[Params[0]].formatSrgb;
        else
            return ColorProfile == 0 ? transcodingToDxgiFormats[(int)Transcoding].format : transcodingToDxgiFormats[(int)Transcoding].formatSrgb;
    }

    public record DxgiFormatEntry(DXGI_FORMAT format, DXGI_FORMAT formatSrgb, byte encodeValue);
    public List<DxgiFormatEntry> encodingToDxgiFormats = new()
    {
        new(DXGI_FORMAT.DXGI_FORMAT_BC1_UNORM, DXGI_FORMAT.DXGI_FORMAT_BC1_UNORM_SRGB, 1),
        new(DXGI_FORMAT.DXGI_FORMAT_BC2_UNORM, DXGI_FORMAT.DXGI_FORMAT_BC2_UNORM_SRGB, 2),
        new(DXGI_FORMAT.DXGI_FORMAT_BC3_UNORM, DXGI_FORMAT.DXGI_FORMAT_BC3_UNORM_SRGB, 3),
        new(DXGI_FORMAT.DXGI_FORMAT_BC4_UNORM, DXGI_FORMAT.DXGI_FORMAT_BC4_UNORM, 4),
        new(DXGI_FORMAT.DXGI_FORMAT_BC4_SNORM, DXGI_FORMAT.DXGI_FORMAT_BC4_SNORM, 5),
        new(DXGI_FORMAT.DXGI_FORMAT_BC5_UNORM, DXGI_FORMAT.DXGI_FORMAT_BC5_UNORM, 6),
        new(DXGI_FORMAT.DXGI_FORMAT_BC5_SNORM, DXGI_FORMAT.DXGI_FORMAT_BC5_SNORM, 7),
        new(DXGI_FORMAT.DXGI_FORMAT_BC6H_UF16, 0, 8),
        new(DXGI_FORMAT.DXGI_FORMAT_BC6H_SF16, 0, 9),
        new(DXGI_FORMAT.DXGI_FORMAT_BC7_UNORM, DXGI_FORMAT.DXGI_FORMAT_BC7_UNORM_SRGB, 10),
        new(DXGI_FORMAT.DXGI_FORMAT_R32G32B32A32_FLOAT, 0, 11),
        new(DXGI_FORMAT.DXGI_FORMAT_R16G16B16A16_UNORM, 0, 12),
        new(DXGI_FORMAT.DXGI_FORMAT_R16G16B16A16_FLOAT, 0, 13),
        new(DXGI_FORMAT.DXGI_FORMAT_R8G8B8A8_UNORM, DXGI_FORMAT.DXGI_FORMAT_R8G8B8A8_UNORM_SRGB, 14),
        new(DXGI_FORMAT.DXGI_FORMAT_B5G6R5_UNORM, 0, 15),
        new(DXGI_FORMAT.DXGI_FORMAT_B5G5R5A1_UNORM, 0, 16),
        new(0, 0, 17),
        new(0, 0, 18),
        new(0, 0, 19),
        new(DXGI_FORMAT.DXGI_FORMAT_R8_UNORM, 0, 20),
        new(DXGI_FORMAT.DXGI_FORMAT_A8_UNORM, 0, 0),
    };

    public List<DxgiFormatEntry> transcodingToDxgiFormats = new()
    {
        new(0, 0, 1),
        new(0, 0, 2),
        new(DXGI_FORMAT.DXGI_FORMAT_BC1_UNORM, DXGI_FORMAT.DXGI_FORMAT_BC1_UNORM_SRGB, 3),
        new(DXGI_FORMAT.DXGI_FORMAT_BC2_UNORM, DXGI_FORMAT.DXGI_FORMAT_BC2_UNORM_SRGB, 4),
        new(DXGI_FORMAT.DXGI_FORMAT_BC3_UNORM, DXGI_FORMAT.DXGI_FORMAT_BC3_UNORM_SRGB, 5),
        new(DXGI_FORMAT.DXGI_FORMAT_BC4_UNORM, DXGI_FORMAT.DXGI_FORMAT_BC4_UNORM, 6),
        new(DXGI_FORMAT.DXGI_FORMAT_BC4_SNORM, DXGI_FORMAT.DXGI_FORMAT_BC4_SNORM, 7),
        new(DXGI_FORMAT.DXGI_FORMAT_BC5_UNORM, DXGI_FORMAT.DXGI_FORMAT_BC5_UNORM, 8),
        new(DXGI_FORMAT.DXGI_FORMAT_BC5_SNORM, DXGI_FORMAT.DXGI_FORMAT_BC5_SNORM, 9),
        new(DXGI_FORMAT.DXGI_FORMAT_BC6H_UF16, 0, 10),
        new(DXGI_FORMAT.DXGI_FORMAT_BC6H_SF16, 0, 11),
        new(DXGI_FORMAT.DXGI_FORMAT_BC7_UNORM, DXGI_FORMAT.DXGI_FORMAT_BC7_UNORM_SRGB, 0),
    };
}