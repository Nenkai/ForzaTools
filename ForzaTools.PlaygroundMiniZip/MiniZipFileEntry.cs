using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Syroot.BinaryData;

namespace ForzaTools.PlaygroundMiniZip
{
    public class MiniZipFileEntry
    {
        public uint RelativeDataOffset { get; set; }
        public ulong DataOffset { get; set; }
        public uint CompressFileSize { get; set; }
        public uint uncompressFileSize { get; set; }
        public ushort CompressMethod { get; set; }
        public byte Padding { get; set; }
        public ushort ParentDirIndex { get; set; }

        public int Index { get; set; }
        public int ChunkFileIndex { get; set; }
        public MiniZipChunk ParentChunk { get; set; }

        public void Read(BinaryStream bs, uint version)
        {
            RelativeDataOffset = bs.ReadUInt32();
            uncompressFileSize = bs.ReadUInt32();

            ushort flags = bs.ReadUInt16();

            if (version >= 101) // FH5 and above (?) - FH5 supports 100 still - this is the only change
            {
                CompressMethod = (ushort)(flags & 0xFFF); // 12 bits
                Padding = (byte)(flags >> 12);
            }
            else
            {
                CompressMethod = (ushort)(flags & 0x3FFF); // 14 bits
                Padding = (byte)(flags >> 14);

            }

            ParentDirIndex = bs.ReadUInt16();
        }
    }
}
