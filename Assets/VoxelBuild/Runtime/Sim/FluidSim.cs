using System;
using System.Collections.Generic;

namespace VoxelBuild.Sim
{
    using VoxelBuild.Core;
    using VoxelBuild.World;

    /// <summary>
    /// Cellular water: every water cell holds 1..MaxLevel units. Water falls first, then spreads sideways when it
    /// cannot fall, equalising with lower neighbours. Only cells touched by a change are simulated, so still lakes
    /// cost nothing until somebody digs into them.
    /// </summary>
    public sealed class FluidSim : IFluidLevels
    {
        public const int Max = 8;

        private readonly VoxelWorld world;
        private readonly Dictionary<Int3, byte> levels = new Dictionary<Int3, byte>();
        private readonly HashSet<Int3> active = new HashSet<Int3>();
        private readonly List<Int3> processing = new List<Int3>();
        private readonly HashSet<Int3> dirtyChunks = new HashSet<Int3>();
        private readonly List<Int3> visualChanged = new List<Int3>();
        private bool suppressEvents;
        private float accumulator;
        private int stepCounter;

        /// <summary>Simulation seconds between flow steps.</summary>
        public float StepSeconds = 0.12f;
        /// <summary>Cells processed per step before the rest wait for the next one.</summary>
        public int MaxCellsPerStep = 20000;

        public int MaxLevel => Max;
        public int ActiveCount => active.Count;
        public int CellCount => levels.Count;

        public FluidSim(VoxelWorld world)
        {
            this.world = world;
            foreach (var chunk in world.Chunks())
            {
                if (chunk.IsEmpty) continue;
                var origin = chunk.Origin;
                for (int y = 0; y < Chunk.Size; y++)
                    for (int z = 0; z < Chunk.Size; z++)
                        for (int x = 0; x < Chunk.Size; x++)
                            if (chunk.Get(x, y, z) == BlockType.Water)
                                levels[origin + new Int3(x, y, z)] = Max;
            }
            world.BlockChanged += OnBlockChanged;
        }

        public int Level(Int3 cell) => levels.TryGetValue(cell, out var l) ? l : 0;

        /// <summary>Adds water (up to Max) to a non-solid cell and wakes it. Used by tests and future sources.</summary>
        public void AddWater(Int3 cell, int amount)
        {
            if (!world.InBounds(cell) || world.IsSolid(cell)) return;
            SetLevel(cell, Math.Min(Max, Level(cell) + amount));
            Wake(cell);
            Flush();
        }

        public void Tick(float dt)
        {
            accumulator += dt;
            int guard = 0;
            while (accumulator >= StepSeconds && guard++ < 4)
            {
                accumulator -= StepSeconds;
                Step();
            }
        }

        /// <summary>One flow step. Returns how many cells were processed.</summary>
        public int Step()
        {
            stepCounter++;
            processing.Clear();
            processing.AddRange(active);
            active.Clear();
            if (processing.Count > MaxCellsPerStep)
            {
                for (int i = MaxCellsPerStep; i < processing.Count; i++) active.Add(processing[i]);
                processing.RemoveRange(MaxCellsPerStep, processing.Count - MaxCellsPerStep);
            }

            foreach (var c in processing) Flow(c);
            Flush();
            return processing.Count;
        }

        private void Flow(Int3 c)
        {
            int l = Level(c);
            if (l <= 0) return;

            // 1. Fall.
            var below = c + Int3.Down;
            if (CanHold(below))
            {
                int lb = Level(below);
                int move = Math.Min(l, Max - lb);
                if (move > 0)
                {
                    SetLevel(below, lb + move);
                    l -= move;
                    SetLevel(c, l);
                    Wake(below);
                    Wake(c);
                    if (l <= 0) return;
                }
                // Something is still above a non-full cell below; it will drain next step.
                if (Level(below) < Max) return;
            }

            // 2. Spread sideways, starting from a rotating direction so flow has no bias.
            int start = (int)(Noise.HashU(c.x, c.y, c.z, stepCounter) & 3u);
            for (int i = 0; i < 4 && l > 1; i++)
            {
                var n = c + Directions.Horizontal[(start + i) & 3];
                if (!CanHold(n)) continue;
                int ln = Level(n);
                int diff = l - ln;
                if (diff < 2) continue;
                int move = diff / 2;
                SetLevel(n, ln + move);
                l -= move;
                SetLevel(c, l);
                Wake(n);
                Wake(c);
            }
        }

        /// <summary>A cell can receive water if it is inside the world and not solid.</summary>
        private bool CanHold(Int3 cell)
        {
            if (!world.InBounds(cell)) return false;
            var t = world.GetBlock(cell);
            return t == BlockType.Air || t == BlockType.Water;
        }

        private void SetLevel(Int3 cell, int level)
        {
            level = Math.Max(0, Math.Min(Max, level));
            if (level == 0)
            {
                if (levels.Remove(cell))
                {
                    suppressEvents = true;
                    if (world.GetBlock(cell) == BlockType.Water) world.SetBlock(cell, BlockType.Air);
                    suppressEvents = false;
                }
                return;
            }
            levels[cell] = (byte)level;
            if (world.GetBlock(cell) != BlockType.Water)
            {
                suppressEvents = true;
                world.SetBlock(cell, BlockType.Water);
                suppressEvents = false;
            }
            else visualChanged.Add(cell);
        }

        /// <summary>Marks a cell and its neighbours for simulation next step.</summary>
        public void Wake(Int3 cell)
        {
            active.Add(cell);
            for (int i = 0; i < 6; i++)
            {
                var n = cell + Directions.Offsets[i];
                if (Level(n) > 0) active.Add(n);
            }
        }

        private void Flush()
        {
            // Level-only changes do not alter blocks, so tell the renderer ourselves, once per chunk.
            foreach (var c in visualChanged) dirtyChunks.Add(VoxelWorld.ChunkCoordOf(c));
            foreach (var c in visualChanged)
            {
                var local = VoxelWorld.LocalOf(c);
                var cc = VoxelWorld.ChunkCoordOf(c);
                if (local.x == 0) dirtyChunks.Add(cc + Int3.Left);
                if (local.x == Chunk.Size - 1) dirtyChunks.Add(cc + Int3.Right);
                if (local.y == 0) dirtyChunks.Add(cc + Int3.Down);
                if (local.y == Chunk.Size - 1) dirtyChunks.Add(cc + Int3.Up);
                if (local.z == 0) dirtyChunks.Add(cc + Int3.Back);
                if (local.z == Chunk.Size - 1) dirtyChunks.Add(cc + Int3.Forward);
            }
            visualChanged.Clear();
            if (dirtyChunks.Count > 0)
            {
                foreach (var cc in dirtyChunks) world.DirtyChunk(cc);
                dirtyChunks.Clear();
            }
        }

        private void OnBlockChanged(Int3 pos, BlockType oldType, BlockType newType)
        {
            if (suppressEvents) return;
            // Something solid replaced water: the water is displaced (lost) and neighbours may settle.
            if (oldType == BlockType.Water && newType != BlockType.Water) levels.Remove(pos);
            // A block was removed: water next to it can now flow in.
            if (newType == BlockType.Air || newType == BlockType.Water) Wake(pos);
            else
            {
                for (int i = 0; i < 6; i++)
                {
                    var n = pos + Directions.Offsets[i];
                    if (Level(n) > 0) active.Add(n);
                }
            }
        }
    }
}
