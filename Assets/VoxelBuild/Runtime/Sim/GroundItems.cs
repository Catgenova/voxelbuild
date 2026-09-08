using System;
using System.Collections.Generic;

namespace VoxelBuild.Sim
{
    using VoxelBuild.Core;
    using VoxelBuild.Nav;
    using VoxelBuild.World;

    /// <summary>Items lying in the world, grouped per cell. A cell is the air block the pile sits in.</summary>
    public sealed class GroundItems
    {
        public const int MaxPerCell = 100;

        private readonly Dictionary<Int3, Dictionary<ItemType, int>> cells = new Dictionary<Int3, Dictionary<ItemType, int>>();
        private readonly VoxelWorld world;

        /// <summary>Fired when a cell's contents change (cell). Renderers listen to this.</summary>
        public event Action<Int3> CellChanged;

        public GroundItems(VoxelWorld world)
        {
            this.world = world;
            world.BlockChanged += OnBlockChanged;
        }

        public IEnumerable<Int3> Cells => cells.Keys;

        public bool HasItems(Int3 cell) => cells.TryGetValue(cell, out var d) && d.Count > 0;

        public int Count(Int3 cell, ItemType type) =>
            cells.TryGetValue(cell, out var d) && d.TryGetValue(type, out var n) ? n : 0;

        public int TotalInCell(Int3 cell)
        {
            if (!cells.TryGetValue(cell, out var d)) return 0;
            int n = 0;
            foreach (var kv in d) n += kv.Value;
            return n;
        }

        public IEnumerable<ItemStack> StacksIn(Int3 cell)
        {
            if (!cells.TryGetValue(cell, out var d)) yield break;
            foreach (var kv in d)
                if (kv.Value > 0) yield return new ItemStack(kv.Key, kv.Value);
        }

        /// <summary>Total of one item type lying anywhere.</summary>
        public int TotalOf(ItemType type)
        {
            int n = 0;
            foreach (var kv in cells)
                if (kv.Value.TryGetValue(type, out var c)) n += c;
            return n;
        }

        public Dictionary<ItemType, int> Totals()
        {
            var t = new Dictionary<ItemType, int>();
            foreach (var kv in cells)
                foreach (var s in kv.Value)
                    t[s.Key] = (t.TryGetValue(s.Key, out var n) ? n : 0) + s.Value;
            return t;
        }

        /// <summary>Adds directly into a cell (no capacity check beyond MaxPerCell). Returns amount added.</summary>
        public int Add(Int3 cell, ItemType type, int count)
        {
            if (type == ItemType.None || count <= 0) return 0;
            int room = MaxPerCell - TotalInCell(cell);
            int add = Math.Min(room, count);
            if (add <= 0) return 0;
            if (!cells.TryGetValue(cell, out var d))
            {
                d = new Dictionary<ItemType, int>();
                cells[cell] = d;
            }
            d[type] = (d.TryGetValue(type, out var n) ? n : 0) + add;
            CellChanged?.Invoke(cell);
            return add;
        }

        /// <summary>
        /// Drops items at or near a cell, spilling into nearby free cells when the target is full or solid.
        /// Returns the amount that could not be placed (should be 0 in practice).
        /// </summary>
        public int AddNear(Int3 cell, ItemType type, int count)
        {
            if (count <= 0 || type == ItemType.None) return 0;
            var origin = FindDropCell(cell);
            int remaining = count - Add(origin, type, count);
            if (remaining <= 0) return 0;

            for (int r = 1; r <= 3 && remaining > 0; r++)
                for (int dz = -r; dz <= r && remaining > 0; dz++)
                    for (int dx = -r; dx <= r && remaining > 0; dx++)
                    {
                        if (Math.Abs(dx) != r && Math.Abs(dz) != r) continue;
                        var c = FindDropCell(origin + new Int3(dx, 0, dz));
                        if (!world.InBounds(c) || world.IsSolid(c)) continue;
                        remaining -= Add(c, type, remaining);
                    }
            return remaining;
        }

        /// <summary>Resolves where a dropped item should land: first air cell above solid ground near the given cell.</summary>
        public Int3 FindDropCell(Int3 cell)
        {
            var c = cell;
            // If inside a solid block, rise until free.
            int guard = 0;
            while (world.InBounds(c) && world.IsSolid(c) && guard++ < 64) c += Int3.Up;
            // Fall until resting on something solid.
            guard = 0;
            while (world.InBounds(c + Int3.Down) && !world.IsSolid(c + Int3.Down) && guard++ < 256) c += Int3.Down;
            return c;
        }

        public int Remove(Int3 cell, ItemType type, int count)
        {
            if (!cells.TryGetValue(cell, out var d) || !d.TryGetValue(type, out var have)) return 0;
            int take = Math.Min(have, count);
            if (take <= 0) return 0;
            if (have - take == 0) d.Remove(type); else d[type] = have - take;
            if (d.Count == 0) cells.Remove(cell);
            CellChanged?.Invoke(cell);
            return take;
        }

        /// <summary>Finds the closest cell (by straight-line distance) matching the predicate.</summary>
        public bool FindNearest(Int3 from, Func<Int3, Dictionary<ItemType, int>, bool> predicate, out Int3 result)
        {
            float best = float.MaxValue;
            result = Int3.Zero;
            bool found = false;
            foreach (var kv in cells)
            {
                if (kv.Value.Count == 0) continue;
                if (!predicate(kv.Key, kv.Value)) continue;
                float d = kv.Key.EuclideanDistance(from);
                if (d < best)
                {
                    best = d;
                    result = kv.Key;
                    found = true;
                }
            }
            return found;
        }

        public bool FindNearestWithItem(Int3 from, ItemType type, out Int3 result) =>
            FindNearest(from, (c, d) => d.TryGetValue(type, out var n) && n > 0, out result);

        public bool FindNearestEdible(Int3 from, out Int3 result, out ItemType food)
        {
            bool ok = FindNearest(from, (c, d) =>
            {
                foreach (var kv in d)
                    if (kv.Value > 0 && ItemRegistry.IsEdible(kv.Key)) return true;
                return false;
            }, out result);
            food = ItemType.None;
            if (ok)
            {
                foreach (var s in StacksIn(result))
                    if (ItemRegistry.IsEdible(s.Type)) { food = s.Type; break; }
            }
            return ok;
        }

        /// <summary>Moves any pile displaced by a block change to a valid resting cell.</summary>
        private void OnBlockChanged(Int3 pos, BlockType oldType, BlockType newType)
        {
            bool nowSolid = BlockRegistry.IsSolid(newType);
            bool wasSolid = BlockRegistry.IsSolid(oldType);
            if (nowSolid && !wasSolid) Relocate(pos);
            if (wasSolid && !nowSolid) Relocate(pos + Int3.Up);
        }

        private void Relocate(Int3 cell)
        {
            if (!cells.TryGetValue(cell, out var d) || d.Count == 0) return;
            var dest = FindDropCell(cell);
            if (dest == cell) return;
            var moving = new List<ItemStack>();
            foreach (var kv in d) moving.Add(new ItemStack(kv.Key, kv.Value));
            cells.Remove(cell);
            CellChanged?.Invoke(cell);
            foreach (var s in moving) AddNear(dest, s.Type, s.Count);
        }
    }
}
