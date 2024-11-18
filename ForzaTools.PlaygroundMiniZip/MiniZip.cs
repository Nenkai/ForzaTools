using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Buffers;
using System.Buffers.Binary;
using System.IO;

using Syroot.BinaryData;

using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using SharpCompress.Compressors.Deflate;

namespace ForzaTools.PlaygroundMiniZip
{
    /// <summary>
    /// Playground Minizip file (disposable object)
    /// </summary>
    public class MiniZip : IDisposable
    {
        /* This file serves for streaming - it does not contain any file names, it is addressed by file index.
         Instead, type of each content is provided in a linked ChunkMap file when a chunk is streamed in. */

        public const uint MAGIC = 0x505A4750; // PGZP
        public uint Version { get; private set; } // 100, 101 FH5

        public uint NumDirEntries { get; private set; }
        public uint NumFolders { get; private set; }
        public uint FilesPerChunk { get; private set; }
        public uint NumSubChunks { get; private set; }

        public List<uint> Indices { get; } = new List<uint>();
        public List<MiniZipChunk> SubChunks { get; set; } = new List<MiniZipChunk>();

        /// <summary>
        /// Chunk map with entries for each minizip file
        /// </summary>
        public ChunkMap ChunkMap { get; set; }

        /// <summary>
        /// From ChunkContentsMiniZip if exists
        /// </summary>
        public List<string> Names { get; } = new List<string>();

        /// <summary>
        /// Empty, used to flag end of zip, to calculate the last file's size
        /// </summary>
        public MiniZipFileEntry LastEntry { get; private set; }

        private Stream _baseStream;
        private StreamWriter _extractLogStream;
        private string _name;

        public MiniZip(string path)
        {
            Load(path);

            _name = Path.GetFileNameWithoutExtension(path);
            if (_name.StartsWith("GeoChunk") && int.TryParse(_name.AsSpan("GeoChunk".Length), out int geoChunkId))
            {
                string dir = Path.GetDirectoryName(path);
                string chunkMapPath = Path.Combine(dir, $"ChunkMap{geoChunkId}.dat");

                if (!File.Exists(chunkMapPath))
                    throw new Exception($"Chunk Map file '{chunkMapPath}' linked to geochunk file was not found");

                LoadChunkMap(chunkMapPath);

                string contentsPath = Path.Combine(dir, $"ChunkContentsMiniZip{geoChunkId}.txt");
                if (File.Exists(contentsPath))
                {
                    LoadChunkContentsFile(contentsPath);

                    if (Names.Count != NumDirEntries)
                        throw new Exception($"Mismatched number of files in ChunkContentsMiniZip{geoChunkId}.txt with minizip (files: txt: {Names.Count}, minizip: {NumDirEntries}). " +
                            $"ChunkContentsMiniZip may be incorrect or not linked to the correct minizip, try without ChunkContentsMiniZip.");
                }
            }

            if (ChunkMap is null)
                throw new Exception($"Chunk Map file linked to geochunk file was not loaded, make sure that the zip file is appropriately named");
        }

        private void LoadChunkMap(string path)
        { 
            using var fs = new FileStream(path, FileMode.Open);

            ChunkMap = new ChunkMap();
            ChunkMap.Load(fs, NumDirEntries);
        }

        private void LoadChunkContentsFile(string path)
        {
            using var fs = new StreamReader(path);

            int index = 0;
            while (!fs.EndOfStream)
            {
                string line = fs.ReadLine();
                if (string.IsNullOrEmpty(line))
                    continue;

                line = line.Replace("<PREZIPPED>", "");
                line = line.Replace("d:\\", "");

                string[] spl = line.Split("|");
                if (spl.Length != 2)
                    throw new InvalidDataException($"Invalid ChunkContentsMiniZip file - line {index + 1} was invalid");

                Names.Add(spl[0]);
                index++;
            }
        }

        private void Load(string path)
        {
            _baseStream = new FileStream(path, FileMode.Open);
            var bs = new BinaryStream(_baseStream);

            ProcessHeader(bs);
        }

        private void ProcessHeader(BinaryStream bs)
        {
            if (bs.ReadUInt32() != MAGIC)
                throw new InvalidDataException("Not a minizip file (magic did not match).");

            Version = bs.ReadUInt32();

            uint folderIndicesOffset = bs.ReadUInt32();
            NumDirEntries = bs.ReadUInt32();
            NumFolders = bs.ReadUInt32();
            FilesPerChunk = bs.ReadUInt32();
            NumSubChunks = bs.ReadUInt32();
            uint unk = bs.ReadUInt32(); // unknown, sort of used in FH5? see below

            /* hdr = *(MiniZipHeader **)(m_MiniDirectoryEntries + 8);
                v18 = (BYTE1(hdr->field_1C) & 1) + 3;
                v19 = (char *)v16 + 4 * v12 % v13 * v18;
                v20 = (_DWORD *)(v15 + 8i64 * v14 * ((v12 + 1) / v13));
                dataOffset = v17 + *((unsigned int *)v19 + 2);
                v21 = *v20 + v20[(v12 + 1) % v13 * v18 + 2] - dataOffset;
                LODWORD(uncompressedSize) = *((_DWORD *)v19 + 3);
                flags = *((unsigned __int16 *)v19 + 8);
                parent = *((unsigned __int16 *)v19 + 9);
            */

            // Actually calculated that way
            uint totalSizeIndices = sizeof(uint) * NumFolders + 4;
            if (((sizeof(uint) * (byte)NumFolders + 4) % 8) != 0)
                totalSizeIndices = sizeof(uint) * NumFolders + 8;

            bs.Position = folderIndicesOffset;
            for (int i = 0; i < totalSizeIndices / 4; i++)
                Indices.Add(bs.ReadUInt32());

            long numFiles = NumDirEntries;
            int idx = 0;
            for (int i = 0; i < NumSubChunks; i++)
            {
                var chunk = new MiniZipChunk();
                chunk.Index = i;
                chunk.DataStartOffset = bs.ReadUInt64();

                int filesThisChunk = (int)Math.Min(FilesPerChunk, numFiles);
                for (int j = 0; j < filesThisChunk; j++)
                {
                    var file = new MiniZipFileEntry();
                    file.Read(bs, Version);
                    file.Index = idx;
                    file.ChunkFileIndex = j;
                    file.ParentChunk = chunk;
                    file.DataOffset = chunk.DataStartOffset + file.RelativeDataOffset;
                    chunk.Entries.Add(file);

                    idx++;
                }

                numFiles -= filesThisChunk;
                SubChunks.Add(chunk);
            }

            LastEntry = new MiniZipFileEntry();
            LastEntry.Read(bs, Version);

            CalculateCompressedSizes();
        }

        /// <summary>
        /// Calculates the size of each compressed file
        /// </summary>
        private void CalculateCompressedSizes()
        {
            for (int i = 0; i < NumDirEntries; i++)
            {
                var info = GetFileEntry(i);
                if (info.CompressMethod == 0)
                    continue;

                var next = i < NumDirEntries - 1 ? GetFileEntry(i + 1) : LastEntry;

                info.CompressFileSize = (uint)(next.DataOffset - info.DataOffset);
                info.CompressFileSize -= info.Padding;
            }
        }

        public void ExtractFile(int index, string outputDir, bool log = false)
        {
            if (log && _extractLogStream is null)
                _extractLogStream = new StreamWriter($"{_name}.txt");

            var info = GetFileEntry(index);
            ChunkMapEntry mapEntry = ChunkMap.Entries[index];

            string sourceFileName = GetFileNameIfExists(index);
            string geoChunkDir = Path.Combine(outputDir, _name);
            string outputName;

            if (!string.IsNullOrEmpty(sourceFileName))
                outputName = Path.Combine(geoChunkDir, sourceFileName);
            else
            {
                string extension = GetExtension(mapEntry.Type);
                outputName = Path.Combine(geoChunkDir, $"{info.Index}");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputName));

            using (var output = new FileStream(outputName, FileMode.Create))
            {
                _baseStream.Position = (long)info.DataOffset;
                Stream processorStream = GetDecompressor(_baseStream, info.CompressMethod);

                const int BufferSize = 0x20000;
                byte[] buffer = ArrayPool<byte>.Shared.Rent(BufferSize);

                long rem = info.uncompressFileSize;
                while (rem > 0)
                {
                    int chunk = (int)Math.Min(rem, BufferSize);
                    processorStream.ReadExactly(buffer, 0, chunk);
                    output.Write(buffer, 0, chunk);

                    rem -= chunk;
                }

                ArrayPool<byte>.Shared.Return(buffer);
            }

            if (string.IsNullOrEmpty(sourceFileName))
            {
                // Rename if we can based on contents
                if (mapEntry.Type == ResourceContentType.Procedural || mapEntry.Type == ResourceContentType.PhysicsTemplate)
                {
                    string fileName;
                    using (var fs = new FileStream(outputName, FileMode.Open))
                    using (var bs = new BinaryStream(fs))
                        fileName = bs.ReadString(StringCoding.Int32CharCount);

                    string newPath = $"{outputDir}/{_name}/{info.ParentDirIndex}/{info.Index}_{fileName}.{GetExtension(mapEntry.Type)}";
                    File.Move(outputName, newPath, overwrite: true);
                    outputName = newPath;
                }
            }

            ExtractLog($"{outputName}|{info.ParentDirIndex}");
        }

        public MiniZipFileEntry GetFileEntry(int index)
        {
            if (index < 0 || index > NumDirEntries)
                throw new IndexOutOfRangeException("File index is out of range");

            int chunkIndex = (int)(index / FilesPerChunk);
            int indexInChunk = (int)(index % FilesPerChunk);
            MiniZipChunk chunk = SubChunks[chunkIndex];
            return chunk.Entries[indexInChunk];
        }

        private string GetFileNameIfExists(int index)
        {
            if (Names.Count == 0)
                return null;

            if (index < 0 || index > Names.Count)
                return null;

            return Names[index];
        }

        private Stream GetDecompressor(Stream baseStream, int method)
        {
            if (method == 8) // Deflate
            {
                // For some reason, c#'s DeflateStream works in later versions, but doesn't in older - even then it fails on some chunks
                // ICSharpCode only works at all in old versions
                // SharpCompress works correctly but only in new versions

                if (Version == 101)
                    return new SharpCompress.Compressors.Deflate.DeflateStream(_baseStream, SharpCompress.Compressors.CompressionMode.Decompress);
                else
                    return new InflaterInputStream(_baseStream);
            }
            else if (method == 22)
                throw new NotSupportedException("Method 22 (deflate + tfit) for minizip is not supported");
            else
                return baseStream;
        }

        private string GetExtension(ResourceContentType contentType)
        {
            return contentType switch
            {
                ResourceContentType.Procedural => "pgeo",
                ResourceContentType.PhysicsTemplate => "phys",
                ResourceContentType.AIOpenWorldBlock => "owb",
                ResourceContentType.ModelBin => "modelbin",
                ResourceContentType.Texture => "pb",
                ResourceContentType.ModelGr2 => "gr2",
                ResourceContentType.TriggerZone => "tz",
                ResourceContentType.VoxelGIPack => "zip",
                ResourceContentType.AudioSoundscape => "soundscape",
                ResourceContentType.AudioSoundBank => "bank",
                ResourceContentType.LightBlock => "lightblock",
                ResourceContentType.PVSZone => "pvsz",
                ResourceContentType.WaterDepth => "dds",
                ResourceContentType.HavokNavMesh => "hkx",
                ResourceContentType.MegaTextureChunk => "mtxmoddxt",
                ResourceContentType.MegaTextureSource => "dxt",
                ResourceContentType.ProceduralMap => "mtp",
                ResourceContentType.IndirectLight => "gipack",
                ResourceContentType.MegaTextureSourceHQ => "hqdxt",
                ResourceContentType.VolumetricFog => "tdft",
                ResourceContentType.VolumetricFogTexture => "fvt",
                ResourceContentType.Unk28 => "predeform",
                _ => $"unk{(int)contentType}",
            };
        }

        private void ExtractLog(string message)
        {
            Console.WriteLine(message);
            _extractLogStream?.WriteLine(message);
        }


        public void Dispose()
        {
            _baseStream?.Dispose();
            _extractLogStream?.Dispose();
        }
    }
}
