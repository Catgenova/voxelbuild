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
        /// <summary>Infinite sources: refill to Max after giving water away.</summary>
        private readonly HashSet<Int3> sources = new HashSet<Int3>();
        /// <summary>Sideways steps travelled since the water last fell or left a source; caps how far springs spread.</summary>
        private readonly Dictionary<Int3, byte> distance = new Dictionary<Int3, byte>();
        /// <summary>Water stops spreading sideways after this many cells (4 m); falling resets the count.</summary>
        public const int MaxFlowDistance = 16;
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
        public int SourceCount => sources.Count;

        /// <summary>Whether natural lake surfaces are infinite. Set before construction via <see cref="Create"/>.</summary>
        public static bool LakeSurfacesAreSources = true;

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
            if (LakeSurfacesAreSources)
            {
                // The top layer of every generated body of water is a spring: lakes refill from the surface.
                foreach (var kv in levels)
                    if (world.GetBlock(kv.Key + Int3.Up) != BlockType.Water) sources.Add(kv.Key);
            }
            world.BlockChanged += OnBlockChanged;
        }

        public int Level(Int3 cell) => levels.TryGetValue(cell, out var l) ? l : 0;

        private int Dist(Int3 cell) => distance.TryGetValue(cell, out var d) ? d : 0;

        private void SetDist(Int3 cell, int d)
        {
            if (d <= 0) distance.Remove(cell);
            else distance[cell] = (byte)Math.Min(255, d);
        }

        public bool IsSource(Int3 cell) => sources.Contains(cell);

        /// <summary>A nearly full cell resting on something, flanked by two springs, becomes a spring itself.</summary>
        private void TryPromote(Int3 cell)
        {
            if (sources.Contains(cell) || Level(cell) < Max - 1) return;
            var below = cell + Int3.Down;
            if (CanHold(below) && Level(below) < Max) return;
            int adjacent = 0;
            for (int i = 0; i < 4; i++)
                if (sources.Contains(cell + Directions.Horizontal[i])) adjacent++;
            if (adjacent < 2) return;
            sources.Add(cell);
            SetLevel(cell, Max);
            SetDist(cell, 0);
        }

        /// <summary>Marks a cell as an infinite source (it must hold water) or clears the flag.</summary>
        public void SetSource(Int3 cell, bool isSource)
        {
            if (isSource)
            {
                if (!world.InBounds(cell) || world.IsSolid(cell)) return;
                SetLevel(cell, Max);
                SetDist(cell, 0);
                sources.Add(cell);
                Wake(cell);
                Flush();
            }
            else sources.Remove(cell);
        }

        /// <summary>
        /// Fills a bucket from a cell: returns the units taken. Sources give a full bucket and stay full;
        /// ordinary water is removed from the cell.
        /// </summary>
        public int Draw(Int3 cell)
        {
            int l = Level(cell);
            if (l <= 0) return 0;
            if (sources.Contains(cell)) return Max;
            SetLevel(cell, 0);
            Wake(cell);
            Flush();
            return l;
        }

        /// <summary>Removes a cell's water entirely, spring or not (the Drain order). Returns the units removed.</summary>
        public int Scoop(Int3 cell)
        {
            int l = Level(cell);
            if (l <= 0) return 0;
            sources.Remove(cell);
            SetLevel(cell, 0);
            Wake(cell);
            Flush();
            return l;
        }

        /// <summary>Empties a bucket into a cell. Returns the units that fit.</summary>
        public int Pour(Int3 cell, int units)
        {
            if (!CanHold(cell)) return 0;
            int room = Max - Level(cell);
            int add = Math.Min(room, units);
            if (add <= 0) return 0;
            SetLevel(cell, Level(cell) + add);
            SetDist(cell, 0);
            TryPromote(cell);
            Wake(cell);
            Flush();
            return add;
        }

        /// <summary>Nearest water cell to draw from, preferring springs so buckets never drain a pond by accident.</summary>
        public bool FindNearestWater(Int3 from, out Int3 result)
        {
            if (Nearest(sources, from, out result)) return true;
            return Nearest(levels.Keys, from, out result);
        }

        private static bool Nearest(IEnumerable<Int3> cells, Int3 from, out Int3 result)
        {
            result = Int3.Zero;
            float best = float.MaxValue;
            bool found = false;
            foreach (var c in cells)
            {
                float d = c.EuclideanDistance(from);
                if (d < best) { best = d; result = c; found = true; }
            }
            return found;
        }

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
            bool source = sources.Contains(c);
            if (!source)
            {
                TryPromote(c);
                source = sources.Contains(c);
            }
            if (source && l < Max)
            {
                SetLevel(c, Max);
                l = Max;
            }
            FlowFrom(c, l);

            if (source && Level(c) < Max)
            {
                // Refill and stay awake while neighbours keep taking water.
                SetLevel(c, Max);
                active.Add(c);
            }
        }

        private void FlowFrom(Int3 c, int l)
        {
            // 1. Fall.
            var below = c + Int3.Down;
            if (CanHold(below))
            {
                int lb = Level(below);
                int move = Math.Min(l, Max - lb);
                if (move > 0)
                {
                    SetLevel(below, lb + move);
                    SetDist(below, 0); // falling resets the spread budget
                    l -= move;
                    SetLevel(c, l);
                    Wake(below);
                    Wake(c);
                    if (l <= 0) return;
                }
                // Something is still above a non-full cell below; it will drain next step.
                if (Level(below) < Max) return;
            }

            // 2. Spread sideways (within the flow budget), starting from a rotating direction so flow has no bias.
            int dc = sources.Contains(c) ? 0 : Dist(c);
            if (dc >= MaxFlowDistance) return;
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
                if (ln == 0 || Dist(n) > dc + 1) SetDist(n, dc + 1);
                l -= move;
                SetLevel(c, l);
                TryPromote(n);
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
                distance.Remove(cell);
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
            if (BlockRegistry.IsSolid(newType)) sources.Remove(pos);
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
