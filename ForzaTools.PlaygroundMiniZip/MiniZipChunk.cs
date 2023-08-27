using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForzaTools.PlaygroundMiniZip
{
    public class MiniZipChunk
    {
        public int Index { get; set; }
        public ulong DataStartOffset { get; set; }

        public List<MiniZipFileEntry> Entries { get; set; } = new();
    }
}
