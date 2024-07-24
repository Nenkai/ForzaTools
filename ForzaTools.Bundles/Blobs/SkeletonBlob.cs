using Syroot.BinaryData;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace ForzaTools.Bundles.Blobs;

public class SkeletonBlob : BundleBlob
{
    public List<Bone> Bones { get; set; } = new List<Bone>();
    public int Unk { get; set; }

    public override void ReadBlobData(BinaryStream bs)
    {
        ushort numBones = bs.ReadUInt16();
        for (int i = 0; i < numBones; i++)
        {
            Bone bone = new Bone();
            bone.Name = bs.ReadString(StringCoding.Int32CharCount);
            bone.ParentId = bs.ReadInt16();
            bone.FirstChildIndex = bs.ReadInt16();
            bone.NextIndex = bs.ReadInt16();
            bone.Matrix = MemoryMarshal.Read<Matrix4x4>(bs.ReadBytes((sizeof(float) * 4) * 4));
            Bones.Add(bone);
        }

        Unk = bs.ReadInt32();
    }

    public override void SerializeBlobData(BinaryStream bs)
    {
        bs.WriteUInt16((ushort)Bones.Count);
        for (int i = 0; i < Bones.Count; i++)
        {
            Bone bone = Bones[i];
            bs.WriteString(bone.Name, StringCoding.Int32CharCount);
            bs.WriteInt16(bone.ParentId);
            bs.WriteInt16(bone.FirstChildIndex);
            bs.WriteInt16(bone.NextIndex);

            bs.WriteMatrix4x4(bone.Matrix);
        }

        bs.WriteInt32(Unk);
    }
}

public class Bone
{
    public string Name { get; set; }
    public short ParentId { get; set; }
    public short FirstChildIndex { get; set; }
    public short NextIndex { get; set; }
    public Matrix4x4 Matrix { get; set; }

    public override string ToString()
    {
        return $"{Name} (Parent: {ParentId})";
    }
}
