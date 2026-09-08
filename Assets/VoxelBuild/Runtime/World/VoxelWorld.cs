using System;
using System.Collections.Generic;

namespace VoxelBuild.World
{
    using VoxelBuild.Core;

    /// <summary>Read-only access to block data, used by meshing and pathfinding.</summary>
    public interface IBlockQuery
    {
        Int3 SizeInBlocks { get; }
        bool InBounds(Int3 p);
        BlockType GetBlock(Int3 p);
        /// <summary>Floor tile on the bottom plane of the cell, or Air for none.</summary>
        BlockType GetFloor(Int3 p);
    }

    public static class BlockQueryExtensions
    {
        public static bool IsSolid(this IBlockQuery q, Int3 p) => BlockRegistry.IsSolid(q.GetBlock(p));
        public static bool IsAir(this IBlockQuery q, Int3 p) => q.GetBlock(p) == BlockType.Air;
        public static bool HasFloor(this IBlockQuery q, Int3 p) => q.GetFloor(p) != BlockType.Air;
    }

    /// <summary>Rules for the floor layer that sits between block layers.</summary>
    public static class FloorRules
    {
        /// <summary>A floor needs a solid block in the layer directly below it within this many cells horizontally.</summary>
        public const int SupportRadius = 3;

        public static bool IsSupported(IBlockQuery world, Int3 cell)
        {
            int y = cell.y - 1;
            if (y < 0) return false;
            for (int dz = -SupportRadius; dz <= SupportRadius; dz++)
                for (int dx = -SupportRadius; dx <= SupportRadius; dx++)
                    if (world.IsSolid(new Int3(cell.x + dx, y, cell.z + dz))) return true;
            return false;
        }

        /// <summary>Whether a floor tile may be placed in the cell right now.</summary>
        public static bool CanPlace(IBlockQuery world, Int3 cell)
        {
            if (!world.InBounds(cell) || cell.y < 1) return false;
            if (world.IsSolid(cell)) return false;
            return IsSupported(world, cell);
        }
    }

    /// <summary>
    /// Finite voxel world made of fixed-size chunks. All block reads and writes go through here so
    /// that change notifications reach the renderer, item system and job board.
    /// </summary>
    public sealed class VoxelWorld : IBlockQuery
    {
        public const int ChunkSize = Chunk.Size;

        public Int3 SizeInChunks { get; }
        public Int3 SizeInBlocks { get; }

        private readonly Chunk[] chunks;

        /// <summary>Increments on every block change. Systems cache this to know when to re-evaluate.</summary>
        public int Version { get; private set; }

        /// <summary>Fired after a block changes: position, old type, new type.</summary>
        public event Action<Int3, BlockType, BlockType> BlockChanged;

        /// <summary>Fired with a chunk coordinate whenever that chunk (or a neighbour edge) needs remeshing.</summary>
        public event Action<Int3> ChunkDirtied;

        /// <summary>Fired after a floor tile changes: position, old floor, new floor (Air = none).</summary>
        public event Action<Int3, BlockType, BlockType> FloorChanged;

        private readonly Dictionary<Int3, BlockType> floors = new Dictionary<Int3, BlockType>();

        public BlockType GetFloor(Int3 p) => floors.TryGetValue(p, out var f) ? f : BlockType.Air;

        public IEnumerable<KeyValuePair<Int3, BlockType>> Floors => floors;

        /// <summary>Sets or clears (Air) the floor tile of a cell. Returns true if it changed.</summary>
        public bool SetFloor(Int3 p, BlockType floor)
        {
            if (!InBounds(p)) return false;
            var old = GetFloor(p);
            if (old == floor) return false;
            if (floor == BlockType.Air) floors.Remove(p);
            else floors[p] = floor;
            Version++;
            FloorChanged?.Invoke(p, old, floor);
            DirtyCell(p);
            return true;
        }

        public VoxelWorld(Int3 sizeInChunks)
        {
            if (sizeInChunks.x <= 0 || sizeInChunks.y <= 0 || sizeInChunks.z <= 0)
                throw new ArgumentException("World size must be positive", nameof(sizeInChunks));
            SizeInChunks = sizeInChunks;
            SizeInBlocks = sizeInChunks * ChunkSize;
            chunks = new Chunk[sizeInChunks.x * sizeInChunks.y * sizeInChunks.z];
            for (int y = 0; y < sizeInChunks.y; y++)
                for (int z = 0; z < sizeInChunks.z; z++)
                    for (int x = 0; x < sizeInChunks.x; x++)
                        chunks[ChunkIndex(x, y, z)] = new Chunk(new Int3(x, y, z));
        }

        private int ChunkIndex(int cx, int cy, int cz) => (cy * SizeInChunks.z + cz) * SizeInChunks.x + cx;

        public bool InBounds(Int3 p) =>
            p.x >= 0 && p.y >= 0 && p.z >= 0 && p.x < SizeInBlocks.x && p.y < SizeInBlocks.y && p.z < SizeInBlocks.z;

        public bool ChunkInBounds(Int3 c) =>
            c.x >= 0 && c.y >= 0 && c.z >= 0 && c.x < SizeInChunks.x && c.y < SizeInChunks.y && c.z < SizeInChunks.z;

        public static Int3 ChunkCoordOf(Int3 p) => Int3.FloorDiv(p, ChunkSize);

        public static Int3 LocalOf(Int3 p) =>
            new Int3(Int3.FloorMod(p.x, ChunkSize), Int3.FloorMod(p.y, ChunkSize), Int3.FloorMod(p.z, ChunkSize));

        public Chunk GetChunk(Int3 chunkCoord)
        {
            if (!ChunkInBounds(chunkCoord)) return null;
            return chunks[ChunkIndex(chunkCoord.x, chunkCoord.y, chunkCoord.z)];
        }

        public IEnumerable<Chunk> Chunks()
        {
            for (int i = 0; i < chunks.Length; i++) yield return chunks[i];
        }

        public BlockType GetBlock(Int3 p)
        {
            if (!InBounds(p)) return BlockType.Air;
            var c = chunks[ChunkIndex(p.x >> 4, p.y >> 4, p.z >> 4)];
            return c.Get(p.x & 15, p.y & 15, p.z & 15);
        }

        public BlockType GetBlock(int x, int y, int z) => GetBlock(new Int3(x, y, z));

        /// <summary>Sets a block, fires change events, and dirties affected chunks. Returns true if changed.</summary>
        public bool SetBlock(Int3 p, BlockType type)
        {
            if (!InBounds(p)) return false;
            var c = chunks[ChunkIndex(p.x >> 4, p.y >> 4, p.z >> 4)];
            int lx = p.x & 15, ly = p.y & 15, lz = p.z & 15;
            var old = c.Get(lx, ly, lz);
            if (!c.Set(lx, ly, lz, type)) return false;
            Version++;
            // A solid block fills the cell, so any floor tile in it is gone.
            if (BlockRegistry.IsSolid(type) && floors.TryGetValue(p, out var oldFloor))
            {
                floors.Remove(p);
                FloorChanged?.Invoke(p, oldFloor, BlockType.Air);
            }
            BlockChanged?.Invoke(p, old, type);
            if (ChunkDirtied != null)
            {
                ChunkDirtied(c.Coord);
                if (lx == 0) DirtyNeighbour(c.Coord + Int3.Left);
                if (lx == ChunkSize - 1) DirtyNeighbour(c.Coord + Int3.Right);
                if (ly == 0) DirtyNeighbour(c.Coord + Int3.Down);
                if (ly == ChunkSize - 1) DirtyNeighbour(c.Coord + Int3.Up);
                if (lz == 0) DirtyNeighbour(c.Coord + Int3.Back);
                if (lz == ChunkSize - 1) DirtyNeighbour(c.Coord + Int3.Forward);
            }
            return true;
        }

        /// <summary>Sets a block silently (no events). Used by world generation before any listeners exist.</summary>
        public void SetBlockRaw(Int3 p, BlockType type)
        {
            if (!InBounds(p)) return;
            chunks[ChunkIndex(p.x >> 4, p.y >> 4, p.z >> 4)].Set(p.x & 15, p.y & 15, p.z & 15, type);
        }

        private void DirtyNeighbour(Int3 chunkCoord)
        {
            if (ChunkInBounds(chunkCoord)) ChunkDirtied?.Invoke(chunkCoord);
        }

        /// <summary>Requests a remesh of one chunk without changing blocks.</summary>
        public void DirtyChunk(Int3 chunkCoord)
        {
            if (ChunkInBounds(chunkCoord)) ChunkDirtied?.Invoke(chunkCoord);
        }

        /// <summary>Requests a remesh of the chunk holding <paramref name="p"/> (and edge neighbours) without changing blocks.</summary>
        public void DirtyCell(Int3 p)
        {
            if (!InBounds(p) || ChunkDirtied == null) return;
            var c = ChunkCoordOf(p);
            ChunkDirtied(c);
            int lx = p.x & 15, ly = p.y & 15, lz = p.z & 15;
            if (lx == 0) DirtyNeighbour(c + Int3.Left);
            if (lx == ChunkSize - 1) DirtyNeighbour(c + Int3.Right);
            if (ly == 0) DirtyNeighbour(c + Int3.Down);
            if (ly == ChunkSize - 1) DirtyNeighbour(c + Int3.Up);
            if (lz == 0) DirtyNeighbour(c + Int3.Back);
            if (lz == ChunkSize - 1) DirtyNeighbour(c + Int3.Forward);
        }

        public bool IsSolid(Int3 p) => BlockRegistry.IsSolid(GetBlock(p));
        public bool IsAir(Int3 p) => GetBlock(p) == BlockType.Air;

        /// <summary>Highest Y with a solid block in the column, or -1 if the column is empty.</summary>
        public int SurfaceY(int x, int z)
        {
            for (int y = SizeInBlocks.y - 1; y >= 0; y--)
                if (IsSolid(new Int3(x, y, z))) return y;
            return -1;
        }

        /// <summary>Total count of a given block type. Slow; intended for stats and tests.</summary>
        public int CountBlocks(BlockType type)
        {
            int n = 0;
            foreach (var c in chunks)
                for (int y = 0; y < ChunkSize; y++)
                    for (int z = 0; z < ChunkSize; z++)
                        for (int x = 0; x < ChunkSize; x++)
                            if (c.Get(x, y, z) == type) n++;
            return n;
        }
    }
}
