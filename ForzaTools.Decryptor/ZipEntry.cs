using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForzaTools.Decryptor
{
    public class ZipEntry
    {
        public uint Signature { get; set; }
        public ushort Version { get; set; }
        public ushort Flags { get; set; }
        public ushort Compression { get; set; }
        public ushort ModTime { get; set; }
        public ushort ModeDate { get; set; }
        public uint CRC32 { get; set; }
        public uint CompressedSize { get; set; }
        public uint UncompressedSize { get; set; }
        public string FileName { get; set; }

        public long DataOffset { get; set; }

        public override string ToString()
        {
            return $"{FileName}";
        }
    }
}
