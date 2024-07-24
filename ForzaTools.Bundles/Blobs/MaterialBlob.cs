using Syroot.BinaryData;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using System.Runtime.InteropServices;

namespace ForzaTools.Bundles.Blobs;

public class MaterialBlob : BundleBlob
{
    public Bundle Bundle { get; set; }

    public override void ReadBlobData(BinaryStream bs)
    {
        Bundle = new Bundle();
        Bundle.Load(bs);
    }

    public override void SerializeBlobData(BinaryStream bs)
    {
        Bundle.Serialize(bs);
    }
}
