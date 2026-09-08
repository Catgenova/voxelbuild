using System;
using System.Collections.Generic;

namespace VoxelBuild.Sim
{
    using VoxelBuild.Core;
    using VoxelBuild.World;

    /// <summary>Keeps the positions of rare, interesting blocks (beds, workbenches, bushes) so searches never scan the world.</summary>
    public sealed class BlockIndex
    {
        private static readonly BlockType[] Tracked = { BlockType.Bed, BlockType.Workbench, BlockType.CarpentryBench, BlockType.BerryBush, BlockType.Torch };

        private readonly Dictionary<BlockType, HashSet<Int3>> positions = new Dictionary<BlockType, HashSet<Int3>>();

        public BlockIndex(VoxelWorld world)
        {
            foreach (var t in Tracked) positions[t] = new HashSet<Int3>();
            foreach (var chunk in world.Chunks())
            {
                if (chunk.IsEmpty) continue;
                var origin = chunk.Origin;
                for (int y = 0; y < Chunk.Size; y++)
                    for (int z = 0; z < Chunk.Size; z++)
                        for (int x = 0; x < Chunk.Size; x++)
                        {
                            var t = chunk.Get(x, y, z);
                            if (positions.TryGetValue(t, out var set)) set.Add(origin + new Int3(x, y, z));
                        }
            }
            world.BlockChanged += OnBlockChanged;
        }

        public static bool IsTracked(BlockType type) => Array.IndexOf(Tracked, type) >= 0;

        public IEnumerable<Int3> Positions(BlockType type) =>
            positions.TryGetValue(type, out var set) ? set : (IEnumerable<Int3>)Array.Empty<Int3>();

        public int Count(BlockType type) => positions.TryGetValue(type, out var set) ? set.Count : 0;

        private void OnBlockChanged(Int3 pos, BlockType oldType, BlockType newType)
        {
            if (positions.TryGetValue(oldType, out var oldSet)) oldSet.Remove(pos);
            if (positions.TryGetValue(newType, out var newSet)) newSet.Add(pos);
        }
    }
}
