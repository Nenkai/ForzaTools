using Syroot.BinaryData;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using System.Runtime.InteropServices;

namespace ForzaTools.Bundles.Blobs;

public class MaterialResourceBlob : BundleBlob
{
    public string Path { get; set; }

    public override void ReadBlobData(BinaryStream bs)
    {
        Path = bs.ReadString(StringCoding.VariableByteCount);
    }

    public override void SerializeBlobData(BinaryStream bs)
    {
        bs.WriteString(Path, StringCoding.VariableByteCount);
    }
}
