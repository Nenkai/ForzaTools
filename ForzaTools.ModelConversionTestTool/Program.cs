namespace ForzaTools.ModelConversionTestTool;

using ForzaTools.Bundle;
using ForzaTools.Bundle.Blobs;
using ForzaTools.Utils;

internal class Program
{
    static void Main(string[] args)
    {
        using var fs = new FileStream(args[0], FileMode.Open);
        var bundle = new Bundle();
        bundle.Load(fs);

        MakeFH5Compatible(bundle);

        using var output = new FileStream(args[1], FileMode.Create);
        bundle.Serialize(output);
    }

    static void MakeFH5Compatible(Bundle bundle)
    {
        // Attempts to make black models imported from FH4 not black

        /* Not needed
        foreach (var blob in bundle.Blobs)
        {
            if (blob is MeshBlob mesh)
            {
                for (int i = 0; i < mesh.UnkEntries.Count; i++)
                {
                    if (mesh.UnkEntries[i].c == 0x20)
                        mesh.UnkEntries[i].c = 0x24;
                }
            }
        }*/

        // Get the main lod mesh
        MeshBlob meshBlob = (MeshBlob)bundle.GetBlobByIndex(Bundle.TAG_BLOB_Mesh, 0);

        // Add the required 3rd tangent component to the main vertex layout
        VertexLayoutBlob layout = (VertexLayoutBlob)bundle.GetBlobByIndex(Bundle.TAG_BLOB_VertexLayout, meshBlob.VertexLayoutIndex);
        int tangentIndex = layout.Elements.FindIndex(e => layout.SemanticNames[e.SemanticNameIndex] == "TANGENT");

        D3D12_INPUT_LAYOUT_DESC thirdTangentComponent = new D3D12_INPUT_LAYOUT_DESC() // Needed
        {
            SemanticNameIndex = (short)layout.SemanticNames.IndexOf("TANGENT"),
            Format = DXGI_FORMAT.DXGI_FORMAT_R10G10B10A2_UNORM,
            InputSlot = 1,
            SemanticIndex = 2,
            AlignedByteOffset = -1,
            InstanceDataStepRate = 0,
        };

        layout.Elements.Insert(tangentIndex + 2, thirdTangentComponent);
        layout.PackedFormats.Insert(tangentIndex + 2, DXGI_FORMAT.DXGI_FORMAT_R8G8_TYPELESS); // Needed otherwise invisible
        layout.Flags |= 0x80; // Required

        int offset = layout.GetDataOffsetOfElement("TANGENT", 2);
        VertexBufferBlob buffer = (VertexBufferBlob)bundle.GetBlobByIndex(Bundle.TAG_BLOB_VertexBuffer, 1);
        for (int i = 0; i < buffer.Header.Data.Length; i++)
        {
            var l = buffer.Header.Data[i].ToList();
            l.Insert(offset, 0xFF);
            l.Insert(offset + 1, 0xFF);
            l.Insert(offset + 2, 0xFF);
            l.Insert(offset + 3, 0xFF);

            buffer.Header.Data[i] = l.ToArray();
        }

        byte totalSize = layout.GetTotalVertexSize();
        buffer.Header.BufferWidth = totalSize;
        buffer.Header.NumElements = (byte)layout.Elements.Count;
    }
}