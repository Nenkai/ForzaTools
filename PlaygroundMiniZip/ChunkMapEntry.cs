using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlaygroundMiniZip
{
    public class ChunkMapEntry
    {
        public short Unk { get; set; }
        public byte Unk2 { get; set; }
        public ResourceContentType Type { get; set; }
    }
}
