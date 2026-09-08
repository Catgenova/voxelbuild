using System;
using System.Collections.Generic;
using UnityEngine;

namespace VoxelBuild.Rendering
{
    using VoxelBuild.Core;
    using VoxelBuild.World;

    /// <summary>Owns one <see cref="ChunkView"/> per chunk, remeshes dirty chunks over frames, and applies the view slice.</summary>
    public sealed class WorldRenderer : MonoBehaviour
    {
        public const int TerrainLayer = 6;

        /// <summary>Milliseconds of remeshing allowed per frame after the initial build.</summary>
        public float FrameBudgetMs = 5f;

        private VoxelWorld world;
        private IFluidLevels fluids;
        private Material waterMaterial;
        private float blockSize;
        private ChunkView[] views;
        private readonly HashSet<Int3> dirty = new HashSet<Int3>();
        private readonly List<Int3> dirtyList = new List<Int3>();
        private int sliceY;

        public float BlockSize => blockSize;
        public int MaxSliceY => world.SizeInBlocks.y - 1;

        public event Action<int> SliceChanged;

        /// <summary>Highest visible block layer. Everything above is cut away so the player can see underground.</summary>
        public int SliceY
        {
            get => sliceY;
            set
            {
                int clamped = Mathf.Clamp(value, 1, MaxSliceY);
                if (clamped == sliceY) return;
                int low = Mathf.Min(sliceY, clamped);
                sliceY = clamped;
                foreach (var v in views)
                    if (v.Coord.y * Chunk.Size + Chunk.Size - 1 >= low) MarkDirty(v.Coord);
                SliceChanged?.Invoke(sliceY);
            }
        }

        public void Init(VoxelWorld world, BlockAtlas atlas, IFluidLevels fluids, float blockSize)
        {
            this.world = world;
            this.fluids = fluids;
            this.blockSize = blockSize;
            waterMaterial = atlas.WaterMaterial;
            sliceY = MaxSliceY;
            var sc = world.SizeInChunks;
            views = new ChunkView[sc.x * sc.y * sc.z];
            int i = 0;
            foreach (var chunk in world.Chunks())
            {
                var go = new GameObject($"Chunk {chunk.Coord}");
                go.transform.SetParent(transform, false);
                var view = go.AddComponent<ChunkView>();
                view.Init(chunk.Coord, atlas.TerrainMaterial, atlas.CrystalMaterial, atlas.WaterMaterial, blockSize, TerrainLayer);
                views[i++] = view;
                dirty.Add(chunk.Coord);
            }
            world.ChunkDirtied += MarkDirty;
            RebuildAllNow();
        }

        public void MarkDirty(Int3 chunkCoord)
        {
            if (world.ChunkInBounds(chunkCoord)) dirty.Add(chunkCoord);
        }

        /// <summary>Synchronously rebuilds every dirty chunk (used at startup).</summary>
        public void RebuildAllNow()
        {
            foreach (var c in dirty) FindView(c).Rebuild(world, fluids, sliceY, blockSize);
            dirty.Clear();
        }

        private ChunkView FindView(Int3 c)
        {
            var sc = world.SizeInChunks;
            return views[(c.y * sc.z + c.z) * sc.x + c.x];
        }

        private void Update()
        {
            // Drift the ripple normal map so lakes look alive (all maps share the base map's UV transform).
            if (waterMaterial != null)
            {
                float t = Time.time;
                waterMaterial.SetTextureOffset("_BaseColorMap", new Vector2(t * 0.02f, t * 0.013f));
            }
            if (dirty.Count == 0) return;
            dirtyList.Clear();
            dirtyList.AddRange(dirty);
            // Nearest-to-camera first would be nicer; lowest Y first keeps the visible cut consistent.
            dirtyList.Sort((a, b) => a.y.CompareTo(b.y));
            float start = Time.realtimeSinceStartup;
            foreach (var c in dirtyList)
            {
                FindView(c).Rebuild(world, fluids, sliceY, blockSize);
                dirty.Remove(c);
                if ((Time.realtimeSinceStartup - start) * 1000f > FrameBudgetMs) break;
            }
        }

        // ---- Coordinate helpers shared by the player tools and views ----

        public Vector3 CellToWorld(Int3 cell) => new Vector3(cell.x, cell.y, cell.z) * blockSize;
        public Vector3 CellCenter(Int3 cell) => new Vector3(cell.x + 0.5f, cell.y + 0.5f, cell.z + 0.5f) * blockSize;
        public Vector3 CellFloorCenter(Int3 cell) => new Vector3(cell.x + 0.5f, cell.y, cell.z + 0.5f) * blockSize;

        public Int3 WorldToCell(Vector3 p) =>
            new Int3(Mathf.FloorToInt(p.x / blockSize), Mathf.FloorToInt(p.y / blockSize), Mathf.FloorToInt(p.z / blockSize));

        public Bounds WorldBounds
        {
            get
            {
                var s = world.SizeInBlocks;
                var size = new Vector3(s.x, s.y, s.z) * blockSize;
                return new Bounds(size * 0.5f, size);
            }
        }
    }
}
