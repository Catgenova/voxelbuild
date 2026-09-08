using System;
using System.Collections.Generic;

namespace VoxelBuild.Sim
{
    using VoxelBuild.Core;
    using VoxelBuild.Nav;
    using VoxelBuild.World;

    /// <summary>Everything the simulation shares. Pure C#, created by the Unity bootstrap and by tests.</summary>
    public sealed class GameContext
    {
        public readonly VoxelWorld World;
        public readonly GroundItems Items;
        public readonly Stockpiles Stockpiles;
        public readonly JobBoard Jobs;
        public readonly GameClock Clock;
        public readonly Pathfinder Pathfinder;
        public readonly BlockIndex Index;
        public readonly List<ColonistCore> Colonists = new List<ColonistCore>();
        public readonly Random Random;

        /// <summary>Optional sink for gameplay messages (the HUD shows them).</summary>
        public Action<string> Log = _ => { };

        public GameContext(VoxelWorld world, int seed = 1)
        {
            World = world;
            Items = new GroundItems(world);
            Stockpiles = new Stockpiles(world, Items);
            Jobs = new JobBoard(world);
            Clock = new GameClock();
            Pathfinder = new Pathfinder(world);
            Index = new BlockIndex(world);
            Random = new Random(seed);
        }

        public float Now => Clock.Elapsed;

        /// <summary>Advances every simulation system by one scaled step.</summary>
        public void Tick(float simDt)
        {
            if (simDt <= 0f) return;
            Clock.Advance(simDt);
            for (int i = 0; i < Colonists.Count; i++)
                Colonists[i].Tick(simDt);
        }

        /// <summary>True if any colonist's body overlaps the given cell.</summary>
        public bool IsCellOccupied(Int3 cell, ColonistCore except = null)
        {
            foreach (var c in Colonists)
            {
                if (c == except) continue;
                var f = c.Cell;
                if (f.x == cell.x && f.z == cell.z && cell.y >= f.y && cell.y < f.y + NavRules.Clearance) return true;
            }
            return false;
        }

        /// <summary>Nearest block of an indexed type (bed, workbench, bush, torch) by straight-line distance.</summary>
        public bool FindNearestBlock(Int3 from, BlockType type, Func<Int3, bool> filter, out Int3 result)
        {
            if (!BlockIndex.IsTracked(type))
                throw new ArgumentException($"{type} is not an indexed block type", nameof(type));
            result = Int3.Zero;
            float best = float.MaxValue;
            bool found = false;
            foreach (var p in Index.Positions(type))
            {
                if (filter != null && !filter(p)) continue;
                float d = p.EuclideanDistance(from);
                if (d < best)
                {
                    best = d;
                    result = p;
                    found = true;
                }
            }
            return found;
        }

        /// <summary>Total stored + carried count of an item, for the HUD.</summary>
        public int TotalItems(ItemType type)
        {
            int n = Items.TotalOf(type);
            foreach (var c in Colonists) n += c.Inventory.Count(type);
            return n;
        }
    }
}
