using Syroot.BinaryData;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using System.Runtime.InteropServices;

namespace ForzaTools.Bundles.Blobs;

public class IndexBufferBlob : BundleBlob
{
    public BufferHeader Header { get; set; } = new();

    public override void ReadBlobData(BinaryStream bs)
    {
        Header.Read(bs);
    }

    public override void SerializeBlobData(BinaryStream bs)
    {
        Header.Serialize(bs);
    }
}
