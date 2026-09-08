using System;

namespace VoxelBuild.World
{
    using VoxelBuild.Core;

    /// <summary>A 16x16x16 cube of blocks. Storage only; meshing and logic live elsewhere.</summary>
    public sealed class Chunk
    {
        public const int Size = 16;
        public const int Volume = Size * Size * Size;

        public readonly Int3 Coord;
        private readonly ushort[] blocks = new ushort[Volume];

        /// <summary>Number of non-air blocks; lets renderers skip empty chunks.</summary>
        public int NonAirCount { get; private set; }

        /// <summary>Incremented on every change so views can detect staleness.</summary>
        public int Version { get; private set; }

        public Chunk(Int3 coord)
        {
            Coord = coord;
        }

        public static int Index(int x, int y, int z) => (y * Size + z) * Size + x;

        public BlockType Get(int x, int y, int z) => (BlockType)blocks[Index(x, y, z)];

        public BlockType Get(Int3 local) => Get(local.x, local.y, local.z);

        /// <summary>Sets a block. Returns true if the value changed.</summary>
        public bool Set(int x, int y, int z, BlockType type)
        {
            int i = Index(x, y, z);
            ushort v = (ushort)type;
            if (blocks[i] == v) return false;
            if (blocks[i] == 0) NonAirCount++;
            if (v == 0) NonAirCount--;
            blocks[i] = v;
            Version++;
            return true;
        }

        public bool IsEmpty => NonAirCount == 0;

        public Int3 Origin => Coord * Size;

        public static bool InLocalBounds(int x, int y, int z) =>
            x >= 0 && x < Size && y >= 0 && y < Size && z >= 0 && z < Size;

        /// <summary>Raw copy for save systems.</summary>
        public void CopyTo(ushort[] dest) => Array.Copy(blocks, dest, Volume);

        public void CopyFrom(ushort[] src)
        {
            Array.Copy(src, blocks, Volume);
            NonAirCount = 0;
            for (int i = 0; i < Volume; i++)
                if (blocks[i] != 0) NonAirCount++;
            Version++;
        }
    }
}
