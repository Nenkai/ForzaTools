using System.Runtime.InteropServices;

using BCnEncoder.Decoder;
using BCnEncoder.Shared;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats;

using Syroot.BinaryData;

using ForzaTools.Bundles.Metadata.TextureContentHeaders;
using ForzaTools.Bundles;
using ForzaTools.Bundles.Blobs;
using ForzaTools.Bundles.Metadata;
using ForzaTools.Shared;

using DurangoTypes;
using BCnEncoder.Shared.ImageFiles;
using Microsoft.Toolkit.HighPerformance;

namespace ForzaTools.TexConv;

public unsafe class Program
{
    public const string Version = "1.0.1";

    static void Main(string[] args)
    {
        Console.WriteLine("---------------------------------------------");
        Console.WriteLine($"- ForzaTools.TexConv {Version} by Nenkai");
        Console.WriteLine("---------------------------------------------");
        Console.WriteLine("- https://github.com/Nenkai");
        Console.WriteLine("---------------------------------------------");

        if (args.Length < 1)
        {
            Console.WriteLine("Usage: <input .swatchbin or folder with swatchbins> [-s (skip prompt)]");
            return;
        }

        if (Directory.Exists(args[0]))
        {
            foreach (var file in Directory.GetFiles(args[0], "*.swatchbin", SearchOption.AllDirectories))
            {
                try
                {
                    ConvertSwatch(file);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }
        else
            ConvertSwatch(args[0]);

#if RELEASE
        if (args.Length == 1 || !args.Any(e => e.Equals("-s")))
        {
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
#endif
    }

    private static void ConvertSwatch(string bundleFile)
    {
        Bundle bundle = new Bundle();
        try
        {
            using var fs = File.OpenRead(bundleFile);
            bundle.Load(fs);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: Failed to load bundle file - {ex.Message}");
            return;
        }

        var txcBlob = bundle.GetBlobById<TextureContentBlob>(Bundle.TAG_BLOB_TextureContentBlob, 0);
        var txchMetadata = txcBlob.GetMetadataByTag<TextureContentHeaderMetadata>(BundleMetadata.TAG_METADATA_TextureContentHeader);

        if (txcBlob.VersionMajor == 0) // PC
        {
            ProcessPCSwatch(bundleFile, txcBlob, txchMetadata);
        }
        else if (txcBlob.VersionMajor == 2) // XB1
        {
            ProcessDurangoSwatch(bundleFile, txcBlob, txchMetadata);
        }
        else
        {
            Console.WriteLine($"ERROR: Unsupported TextureContentHeader version {txchMetadata.Version}");
        }
    }

    private static void ProcessPCSwatch(string bundleFile, TextureContentBlob txcBlob, TextureContentHeaderMetadata txchMetadata)
    {
        var pcData = txchMetadata.GetContents();
        PCTextureContentHeader hdr = new PCTextureContentHeader();
        hdr.Read(pcData);

        DXGI_FORMAT format = hdr.DetermineFormat();

        Console.WriteLine($"{bundleFile}: {hdr.Width}x{hdr.Height} - {format} - {hdr.MipLevels} mips");

        byte[] data = txcBlob.GetContents();

        byte[] ddsFile = ToDds(data, format, hdr.Width, hdr.Height, hdr.MipLevels);
        File.WriteAllBytes(Path.ChangeExtension(bundleFile, ".dds"), ddsFile);
    }

    private static void ProcessDurangoSwatch(string bundleFile, TextureContentBlob txcBlob, TextureContentHeaderMetadata txchMetadata)
    {
        var durangoData = txchMetadata.GetContents();
        DurangoTextureContentHeader hdr = new DurangoTextureContentHeader();
        hdr.Read(durangoData);

        byte[] data = txcBlob.GetContents();
        XG_FORMAT format = hdr.DetermineFormat();

        Console.WriteLine($"[{Path.GetFileName(bundleFile)}] => {hdr.BaseMipWidth}x{hdr.BaseMipHeight} - {format} - {hdr.FullTextureNumMipLevels} mips (start: {hdr.BaseMipLevel}, count: {hdr.NumMips}) - tile mode {hdr.TileMode}");

        if (hdr.TileMode == XG_TILE_MODE.XG_TILE_MODE_2D_THIN)
        {
            (XG_RESOURCE_LAYOUT Layout, byte[] Data)? DetiledData = DurangoDetile(hdr, data);
            if (DetiledData is null)
                return;

            // We need to dealign the data.
            // https://github.com/microsoft/DirectXTK12/blob/9de75066dd751207eae8a765e14e21ae8d81b66d/Src/ScreenGrab.cpp#L290-L296
            // D3D12XBOX_TEXTURE_DATA_PITCH_ALIGNMENT is 1024, but it seems it only ever aligns to 256 here (i think?)
            byte[] dealignedData = DealignDurangoTextureData(hdr, format, DetiledData);

            byte[] ddsFile = ToDds(dealignedData, (DXGI_FORMAT)format, hdr.BaseMipWidth, hdr.BaseMipHeight, hdr.NumMips);
            File.WriteAllBytes(Path.ChangeExtension(bundleFile, ".dds"), ddsFile);
        }
    }

    private static byte[] DealignDurangoTextureData(DurangoTextureContentHeader hdr, XG_FORMAT format, (XG_RESOURCE_LAYOUT Layout, byte[] Data)? DetiledData)
    {
        uint totalSize = 0;
        uint w = hdr.BaseMipWidth; uint h = hdr.BaseMipHeight;
        for (int i = 0; i < hdr.FullTextureNumMipLevels; i++)
        {
            DxgiUtils.ComputePitch((DXGI_FORMAT)format, w, h, out ulong rowPitch, out ulong slicePitch, out ulong alignedSlicePitch);
            totalSize += (uint)slicePitch;

            w >>= 1;
            h >>= 1;
        }

        byte[] outputData = new byte[totalSize];
        w = hdr.BaseMipWidth; h = hdr.BaseMipHeight;
        ulong offset = 0;
        for (int i = 0; i < hdr.NumMips; i++)
        {
            DxgiUtils.ComputePitch((DXGI_FORMAT)format, w, h, out ulong rowPitch, out ulong slicePitch, out ulong alignedSlicePitch);

            XG_MIPLEVEL_LAYOUT mipLayout = DetiledData.Value.Layout.Plane[0].MipLayout[i];

            Span<byte> inputMip = DetiledData.Value.Data.AsSpan((int)mipLayout.OffsetBytes, (int)mipLayout.SizeBytes);
            Span<byte> outputMip = outputData.AsSpan((int)offset, (int)slicePitch);

            uint actualHeight = DxgiUtils.IsBCnFormat((DXGI_FORMAT)format) ? h / 4 : h;
            for (int y = 0; y < actualHeight; y++)
            {
                Span<byte> row = inputMip.Slice((y * (int)mipLayout.PitchBytes), (int)rowPitch);
                Span<byte> outputRow = outputMip.Slice((int)(y * (uint)rowPitch), (int)rowPitch);
                row.CopyTo(outputRow);
            }

            w >>= 1;
            h >>= 1;
            offset += slicePitch;
        }

        return outputData;
    }

    static int roundUp(int numToRound, int multiple)
    {
        if (multiple == 0)
            return numToRound;

        int remainder = numToRound % multiple;
        if (remainder == 0)
            return numToRound;

        return numToRound + multiple - remainder;
    }

    private static byte[] ToDds(byte[] data, DXGI_FORMAT dxgiFormat, int width, int height, int numMips)
    {
        var header = new DdsHeader();
        header.Height = height;
        header.Width = width;
        header.LastMipmapLevel = numMips;
        header.FormatFlags = DDSPixelFormatFlags.DDPF_FOURCC;
        header.FourCCName = "DX10";
        header.DxgiFormat = dxgiFormat;
        header.ImageData = data;

        var flags = DDSHeaderFlags.TEXTURE | DDSHeaderFlags.LINEARSIZE;
        if (numMips > 1)
            flags |= DDSHeaderFlags.MIPMAP;
        header.Flags = flags;

        DxgiUtils.ComputePitch(dxgiFormat, (uint)width, (uint)height, out ulong rowPitch, out _, out _);
        header.PitchOrLinearSize = (int)rowPitch;
        using var ms = new MemoryStream();
        header.Write(ms);

        return ms.ToArray();
    }

    private static (XG_RESOURCE_LAYOUT Layout, byte[] Data)? DurangoDetile(DurangoTextureContentHeader hdr, byte[] tiledData)
    {
        // For now we gotta use the xg lib for detiling, it sucks
        // It's pretty complex

        XG_FORMAT format = hdr.DetermineFormat();

        XG_TEXTURE2D_DESC desc;
        desc.Width = hdr.BaseMipWidth;
        desc.Height = hdr.BaseMipHeight;
        desc.Format = format;
        desc.Usage = XG_USAGE.XG_USAGE_DEFAULT; // Should be default
        desc.SampleDesc.Count = 1; // Should be 1
        desc.ArraySize = hdr.Depth_NumSlice;
        desc.MipLevels = hdr.NumMips;
        desc.BindFlags = (uint)XG_BIND_FLAG.XG_BIND_SHADER_RESOURCE;
        desc.MiscFlags = 0;
        desc.TileMode = hdr.TileMode;

        XGTextureAddressComputer* compWrapper;
        int result = XGImports.XGCreateTexture2DComputer(&desc, &compWrapper);
        if (result > 0)
        {
            Console.WriteLine($"ERROR: Failed to XGCreateTexture2DComputer (0x{result:X8})");
            return null;
        }

        XGTextureAddressComputer computer = *compWrapper;
        nint arrPtr = Marshal.AllocHGlobal(Marshal.SizeOf<XG_RESOURCE_LAYOUT>());

        result = computer.vt->GetResourceLayout(compWrapper, (XG_RESOURCE_LAYOUT*)arrPtr);
        if (result > 0)
        { 
            Console.WriteLine($"ERROR: Failed to GetResourceLayout (0x{result:X8})");
            return null;
        }

        try
        {
            XG_RESOURCE_LAYOUT layout = Marshal.PtrToStructure<XG_RESOURCE_LAYOUT>(arrPtr);
            byte[] outputFile = new byte[layout.SizeBytes];

            for (uint nSlice = 0; nSlice < 1 /* depth/images */; nSlice++)
            {
                // Go through each mips
                for (uint nMip = 0; nMip < desc.MipLevels; nMip++)
                {
                    ulong mipSizeBytes = layout.Plane[0].MipLayout[nMip].SizeBytes;
                    ulong mipOffset = layout.Plane[0].MipLayout[nMip].OffsetBytes;

                    uint nDstSubResIdx = nMip + hdr.FullTextureNumMipLevels * nSlice;
                    uint nRowPitch = layout.Plane[0].MipLayout[nMip].PitchBytes;

                    byte[] outputBytes = new byte[mipSizeBytes];

                    fixed (byte* outputPtr = outputBytes)
                    fixed (byte* inputPtr = tiledData)
                    {
                        // CopyFromSubresource will detile but not decode - just what we need
                        result = computer.vt->CopyFromSubresource(compWrapper, outputPtr, 0u, nDstSubResIdx, inputPtr, nRowPitch, 0);
                        if (result > 0)
                        {
                            Console.WriteLine($"ERROR: Failed to CopyFromSubresource (0x{result:X8})");
                            return null;
                        }

                        outputBytes.AsSpan().CopyTo(outputFile.AsSpan((int)mipOffset));
                    }
                }
            }

            return (layout, outputFile);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.Message}");
        }
        finally
        {
            Marshal.FreeHGlobal(arrPtr);
        }

        return null;
    }

    /*
    private static StreamWriter sw = new StreamWriter("txt.txt");

    public static void GetTileLocation(int x, int y)
    {
        // Doesn't work, was just a test

        int offset = 0;
        int blockSize = 0x10;

        if (x % 4 == 0)
        {
            if (x % 8 != 0)
                offset += 0x2000;
        }

        int x1Rem = x % 2;
        int x1Div = x / 2;

        int y1Rem = y % 2;
        int y1Div = y / 2;

        offset += (y1Rem * blockSize * 2);
        offset += (y1Div * blockSize * 8);

        offset += (x1Div * blockSize * 4);
        offset += (x1Rem * blockSize * 1);
    }
    */

    // This was only tested/working on NIS_SkylineFF_99_livery_001_DIFF_6b427cab-cd4e-450e-a2ff-39a8d3a2be12.swatchbin
    // Will absolutely not work on anything else
    /*
    public static void TryDeswizzle(byte[] output, ushort width, ushort height, XG_FORMAT format, XG_TILE_MODE tileMode, byte[] pTiledData, int rowPitchBytes) // CopyFromSubresource
    {
        int widthElements = width / 4; // TODO: find out how this is actually calculated instead of dividing by 4
        int heightElements = height / 4; // TODO: Same

        int xTiles = (widthElements + 7) / 8;
        int yTiles = (heightElements + 7) / 8;

        int bytesPerElement = 0x10;

        for (int yTile = 0; yTile < yTiles; yTile++)
        {
            int startY = yTile * 8;
            int endY = yTile * 8 + 8;
            if (endY > heightElements)
                endY = heightElements;

            int destLinearRowOffset = rowPitchBytes * startY;

            for (int xTile = 0; xTile < xTiles; xTile++)
            { 
                int startX = xTile * 8;
                int endX = xTile * 8 + 8;
                if (endX > widthElements)
                    endX = widthElements;

                Span<byte> startTileBytes = output.AsSpan(destLinearRowOffset + bytesPerElement * 8 * xTile);
                for (int y = startY; y < endY; y++)
                {
                    var outputRow = startTileBytes;
                    for (int x = startX; x < endX; x++)
                    {
                        long coord = addrCoordUtility(x, y, 0, 0, 0, 0, 0);

                        sw.WriteLine($"[{x},{y}] = {coord >> 12:x}");
                        pTiledData.AsSpan((int)(coord >> 12), bytesPerElement).CopyTo(outputRow);
                        outputRow = outputRow[bytesPerElement..];
                    }

                    startTileBytes = startTileBytes[rowPitchBytes..];
                }
            }
        }

        sw.Dispose();
    }

    public class _AddrArrayState
    {
        public static int field_0x00 = 0;
        public static int field_0x68 = 0x01;
        public static int field_0x70 = 4;
        public static int field_0x74 = 8;
        public static int field_0x78 = 0x100;
        public static int field_0x98 = 1;
        public static int field_0xA4 = 0;
        public static int field_0xA8 = 0;
        public static int field_0xAC = 3;
        public static int field_0xB0 = 0;
        public static int field_0xC8 = 0x40;
        public static int field_0xCC = 0x20;
        public static int field_0xE0 = 0x08;
        public static int field_0xE4 = 0x80;

        public static int field_0xE8 = 0x01;
        public static int field_0xF0 = 0x2000;
        public static int field_0xF4 = 0x2000;
        public static int field_0x130 = 1;
        public static int field_0x134 = 1;
        public static int field_0x144 = (field_0x74 / 2) + 1;
        public static int field_0x148 = 1;

    }

    public static long addrCoordUtility(int x, int y, int slice, int a4, int a5, int a6, int a7)
    {
        return addrR8xxCoordUtility(x, y);
    }

    public static long addrR8xxCoordUtility(int x, int y)
    {
        long val = addrR8xxCoord2DTiledAddress(x, y);
        return (val << 9) + (0x80 - 1);
    }

    public static long addrR8xxCoord2DTiledAddress(int x, int y)
    {
        int a3 = 0;
        int a5 = 0;
        int a6 = 0;

        int v1 = addrCoordToElemOffset(x, y);

        int v14 = a6 ^ (a5 + v1);
        int v16 = _AddrArrayState.field_0x98 > 0 ? 0 : 0 ;

        int v63 = v14 % _AddrArrayState.field_0xF4
            + _AddrArrayState.field_0xF0
            / _AddrArrayState.field_0x98
            * (x / 8 / _AddrArrayState.field_0x70 % _AddrArrayState.field_0x130 + _AddrArrayState.field_0x130 * (y / 8 % _AddrArrayState.field_0x134)
            + _AddrArrayState.field_0xE8
            * (x / _AddrArrayState.field_0xC8 + y / _AddrArrayState.field_0xCC * _AddrArrayState.field_0xE0 + _AddrArrayState.field_0xE4 * (v16 + _AddrArrayState.field_0x98 * (a3 / _AddrArrayState.field_0x68))));


        int unkX = x / 8;
        int unkY = y / 8;
        int pipe = addrRaxxTileCoordToPipe(unkX, unkY, 5);
        
        int x2 = unkX / (_AddrArrayState.field_0x130 * _AddrArrayState.field_0x70);
        int y2 = unkY / _AddrArrayState.field_0x134;


        int xb1 = x2 & 1;
        int xb2 = (x2 >> 1) & 1;
        int xb3 = (x2 >> 2) & 1;

        int yb1 = y2 & 1;
        int yb2 = (y2 >> 1) & 1;
        int yb3 = (y2 >> 2) & 1;
        int yb4 = (y2 >> 3) & 1;

        int _1 = 0, _2 = 0, _3 = 0, _4 = 0;
        switch (_AddrArrayState.field_0x74)
        {
            case 8:
                _1 = xb1 ^ yb3;
                _2 = xb2 ^ yb2 ^ yb3;
                _3 = xb3 ^ yb1;
                break;
        }

        int num = bits2Number(4, _4, _3, _2, _1);


        int v57 = 8 * _AddrArrayState.field_0x78;
        int v54 = 0;
        int v52 = 0; // v51 = a3 / a7->field68; v52 = v51 * a7->field_AC
        if (_AddrArrayState.field_0xA4 > 0)
        {

        }
        else
        {
            v54 = (v16 * _AddrArrayState.field_0x144) ^ ((num ^ (v52 + _AddrArrayState.field_0xB0)) % _AddrArrayState.field_0x74);
        }

        return v63 % v57
            + v57 * (long)((pipe ^ _AddrArrayState.field_0xA8) % _AddrArrayState.field_0x70)
            + _AddrArrayState.field_0x70 * (long)v57 * (v63 / v57 % _AddrArrayState.field_0x148 + _AddrArrayState.field_0x148 * (v54 % _AddrArrayState.field_0x74 + _AddrArrayState.field_0x74 * (v63 / (v57 * _AddrArrayState.field_0x148))))
            + 8 * _AddrArrayState.field_0x00;
    }

    public static int addrCoordToElemOffset(int x, int y)
    {
        int elemIndex = addrRaxxCoordToElemIndex(x, y);
        return elemIndex * 0x80; // TODO: Find out how this is calculated, for now multiply by 0x80

    }

    public static int addrRaxxCoordToElemIndex(int x, int y)
    {
        int xb1 = x & 1;
        int xb2 = (x >> 1) & 1;
        int xb3 = (x >> 2) & 1;

        int yb1 = y & 1;
        int yb2 = (y >> 1) & 1;
        int yb3 = (y >> 2) & 1;

        int value = bits2Number(6, yb3, xb3, yb2, xb2, yb1, xb1);
        return value;
    }

    public static int addrRaxxTileCoordToPipe(int x, int y, int a3)
    {
        int xb1 = x & 1;
        int xb2 = (x >> 1) & 1;
        int xb3 = (x >> 2) & 1;

        int yb1 = y & 1;
        int yb2 = (y >> 1) & 1;
        int yb3 = (y >> 2) & 1;

        int v14 = 0;
        switch (a3)
        {
            case 5:
                int v4 = xb1 ^ xb2 ^ yb1;
                int v13 = xb2 ^ yb2;
                return bits2Number(4, 0, v14, v13, v4);
        }

        throw new NotSupportedException();
    }

    public static int bits2Number(int size, params int[] t)
    {
        int idx = 0;
        int mask = 0;
        for (int i = 0; i < size; i++)
        {
            int value = t[idx++] | mask;
            mask = value << 1;
        }

        return mask >> 1;
    }

    // addrR8xxCoordUtility
    // addrR8xxCoord2DTiledAddress
    */
}
