using System;
using System.Collections.Generic;

namespace VoxelBuild.Sim
{
    using VoxelBuild.Core;
    using VoxelBuild.World;

    /// <summary>Player-designated storage cells. Colonists haul loose items here.</summary>
    public sealed class Stockpiles
    {
        private readonly HashSet<Int3> cells = new HashSet<Int3>();
        private readonly VoxelWorld world;
        private readonly GroundItems items;

        public event Action Changed;

        public Stockpiles(VoxelWorld world, GroundItems items)
        {
            this.world = world;
            this.items = items;
        }

        public IEnumerable<Int3> Cells => cells;
        public int Count => cells.Count;
        public bool Contains(Int3 cell) => cells.Contains(cell);

        public void Add(Int3 cell)
        {
            if (cells.Add(cell)) Changed?.Invoke();
        }

        public void Remove(Int3 cell)
        {
            if (cells.Remove(cell)) Changed?.Invoke();
        }

        public void AddBox(IntBox box)
        {
            bool any = false;
            foreach (var c in box.Cells())
            {
                if (!world.InBounds(c) || world.IsSolid(c) || !world.IsSolid(c + Int3.Down)) continue;
                any |= cells.Add(c);
            }
            if (any) Changed?.Invoke();
        }

        public void RemoveBox(IntBox box)
        {
            bool any = false;
            foreach (var c in box.Cells()) any |= cells.Remove(c);
            if (any) Changed?.Invoke();
        }

        /// <summary>True if an item pile in this cell counts as stored.</summary>
        public bool IsStored(Int3 cell) => cells.Contains(cell);

        /// <summary>Finds the best cell to put an item in: prefers piles of the same type, then empty cells; nearest wins.</summary>
        public bool FindCellFor(Int3 from, ItemType type, out Int3 result)
        {
            result = Int3.Zero;
            float bestScore = float.MaxValue;
            bool found = false;
            foreach (var c in cells)
            {
                if (!IsUsable(c)) continue;
                int total = items.TotalInCell(c);
                if (total >= GroundItems.MaxPerCell) continue;
                float score = c.EuclideanDistance(from);
                if (items.Count(c, type) > 0) score -= 100f; // strongly prefer merging
                else if (total > 0) score += 50f;            // avoid mixing types
                if (score < bestScore)
                {
                    bestScore = score;
                    result = c;
                    found = true;
                }
            }
            return found;
        }

        public bool HasFreeSpace()
        {
            foreach (var c in cells)
                if (IsUsable(c) && items.TotalInCell(c) < GroundItems.MaxPerCell) return true;
            return false;
        }

        private bool IsUsable(Int3 c) => world.InBounds(c) && !world.IsSolid(c) && world.IsSolid(c + Int3.Down);
    }
}
