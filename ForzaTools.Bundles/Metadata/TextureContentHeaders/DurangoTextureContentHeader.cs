using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Syroot.BinaryData;

using ForzaTools.Bundles.Blobs;
using ForzaTools.Shared;
using DurangoTypes;

namespace ForzaTools.Bundles.Metadata.TextureContentHeaders;

public class DurangoTextureContentHeader
{
    public Guid Guid { get; set; }

    // Textures can be splitted into multiple files, as mip ranges.
    // https://github.com/Nenkai/ForzaTools/issues/3

    /// <summary>
    /// Width of the first mip for this texture/split.
    /// </summary>
    public ushort BaseMipWidth { get; set; }

    /// <summary>
    /// Height of the first mip for this texture/split.
    /// </summary>
    public ushort BaseMipHeight { get; set; }

    /// <summary>
    /// Number of slices.
    /// </summary>
    public ushort Depth_NumSlice { get; set; }

    /// <summary>
    /// Full texture width.
    /// </summary>
    public ushort FullTextureWidth { get; set; }

    /// <summary>
    /// Full texture height.
    /// </summary>
    public ushort FullTextureHeight { get; set; }

    public ushort UnkMip1 { get; set; }

    /// <summary>
    /// Number of mips for this texture/split. Example: If <see cref="BaseMipLevel"/> is 5 and NumMips is 2, this texture contains mip 5 and 6 of a full texture.
    /// </summary>
    public byte NumMips { get; set; }

    /// <summary>
    /// Number of mips in the main texture (when not split).
    /// </summary>
    public byte FullTextureNumMipLevels { get; set; }

    /// <summary>
    /// Base mip level for this texture/split. 5 would mean this texture is part of a bigger texture, and contains mips 5 and onwards.
    /// </summary>
    public byte BaseMipLevel { get; set; }

    public uint RawBitFlags { get; set; }

    public XG_TILE_MODE TileMode
    {
        get => (XG_TILE_MODE)(RawBitFlags & 0b11111);
        set => RawBitFlags |= (byte)((byte)value & 0b11111);
    }

    public byte Encoding
    {
        get => (byte)(RawBitFlags >> 5 & 0b111111);
        set => RawBitFlags |= (byte)((value & 0b111111) << 5);
    }

    public byte Transcoding
    {
        get => (byte)(RawBitFlags >> 11 & 0b111111);
        set => RawBitFlags |= (byte)((value & 0b111111) << 11);
    }

    public byte UnkBits1
    {
        get => (byte)(RawBitFlags >> 17 & 0b111);
        set => RawBitFlags |= (byte)((value & 0b111) << 17);
    }

    public byte ColorProfile
    {
        get => (byte)(RawBitFlags >> 20 & 0b111);
        set => RawBitFlags |= (byte)((value & 0b111) << 20);
    }

    public bool Flag1
    {
        get => (RawBitFlags >> 25 & 1) == 1;
        set => RawBitFlags |= (byte)((value ? 1 : 0) << 25);
    }

    public bool Flag2
    {
        get => (RawBitFlags >> 26 & 1) == 1;
        set => RawBitFlags |= (byte)((value ? 1 : 0) << 26);
    }

    public bool Flag3
    {
        get => (RawBitFlags >> 27 & 1) == 1;
        set => RawBitFlags |= (byte)((value ? 1 : 0) << 27);
    }

    public byte PitchOrLinearSize
    {
        get => (byte)(RawBitFlags >> 28 & 0b1111);
        set => RawBitFlags |= (byte)((value & 0b1111) << 28);
    }

    public void Read(byte[] data)
    {
        using BinaryStream bs = new BinaryStream(new MemoryStream(data));
        Guid = new Guid(bs.ReadBytes(0x10));
        BaseMipWidth = bs.ReadUInt16();
        BaseMipHeight = bs.ReadUInt16();
        Depth_NumSlice = bs.ReadUInt16();
        FullTextureWidth = bs.ReadUInt16();
        FullTextureHeight = bs.ReadUInt16();
        UnkMip1 = bs.ReadUInt16();
        NumMips = bs.Read1Byte();
        FullTextureNumMipLevels = bs.Read1Byte();
        BaseMipLevel = bs.Read1Byte();
        RawBitFlags = bs.ReadUInt32();
    }

    public XG_FORMAT DetermineFormat()
    {
        if (Transcoding <= 1)
            return (XG_FORMAT)(ColorProfile == 0 ? encodingToDxgiFormats[Encoding].format : encodingToDxgiFormats[Encoding].formatSrgb);
        else
            return (XG_FORMAT)(ColorProfile == 0 ? transcodingToDxgiFormats[Transcoding].format : transcodingToDxgiFormats[Transcoding].formatSrgb);
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