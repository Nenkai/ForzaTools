using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Syroot.BinaryData;

namespace ForzaTools.PlaygroundMiniZip
{
    public class ChunkMap
    {
        public List<ChunkMapEntry> Entries = new List<ChunkMapEntry>();

        public void Load(Stream stream, uint length)
        {
            using var bs = new BinaryStream(stream);

            for (uint i = 0; i < length; i++)
            {
                var entry = new ChunkMapEntry();
                entry.Unk = bs.ReadInt16();
                entry.Unk2 = bs.Read1Byte();
                entry.Type = (ResourceContentType)bs.Read1Byte();
                Entries.Add(entry);
            }
        }
    }

    public enum ResourceContentType
    {
        Procedural = 0,
        PhysicsTemplate = 1,
        MemoryPlaceholder = 2,
        AIOpenWorldBlock = 3,
        ModelBin = 4,
        Texture = 5,
        Unknown = 6,
        ModelGr2 = 7,
        TriggerZone = 8,
        AudioSoundscape = 9,
        AudioSoundBank = 10,
        LightBlock = 11,
        PVSZone = 12,
        AmbientOcclusion = 13,
        WaterDepth = 14,
        HavokNavMesh = 15,
        VoxelGIPack = 16,
        MegaTextureChunk = 17,
        MegaTextureSource = 18,
        ProceduralMap = 19,
        FilePack = 20,
        // 21 Empty
        IndirectLight = 22,
        // 23 Empty
        MegaTextureSourceHQ = 24,
        EntityStreamingCell = 25,
        VolumetricFog = 26,
        VolumetricFogTexture = 27,
        Unk28,
    }
}
