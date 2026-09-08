using System;
using System.Collections.Generic;

namespace VoxelBuild.Sim
{
    using VoxelBuild.Core;
    using VoxelBuild.World;

    public enum DesignationKind
    {
        Mine,
        Build,
    }

    /// <summary>A player order attached to one block cell.</summary>
    public sealed class Designation
    {
        public DesignationKind Kind;
        public Int3 Cell;
        public BlockType BuildType;
        public float WorkDone;
        /// <summary>Colonist currently working this order, or null.</summary>
        public object ReservedBy;
        /// <summary>Simulation time before which no colonist should retry after a failure.</summary>
        public float RetryAfter;
        /// <summary>World version at the last failure; a changed world lifts the retry delay early.</summary>
        public int FailedAtVersion = -1;

        public bool IsAvailable(object requester, float now, int worldVersion)
        {
            if (ReservedBy != null && ReservedBy != requester) return false;
            if (now < RetryAfter && worldVersion == FailedAtVersion) return false;
            return true;
        }
    }

    /// <summary>A crafting order: make <see cref="Remaining"/> batches of a recipe at a station (or by hand).</summary>
    public sealed class CraftBill
    {
        public Recipe Recipe;
        /// <summary>Station cell, or null for by-hand recipes.</summary>
        public Int3? Station;
        public int Remaining;
        public object ReservedBy;
        public float RetryAfter;

        public bool ByHand => Station == null;
    }

    /// <summary>All outstanding player orders. Colonists query it for work; the UI edits it.</summary>
    public sealed class JobBoard
    {
        private readonly Dictionary<Int3, Designation> designations = new Dictionary<Int3, Designation>();
        private readonly List<CraftBill> bills = new List<CraftBill>();
        private readonly VoxelWorld world;

        /// <summary>Fired whenever designations or bills change.</summary>
        public event Action Changed;

        public JobBoard(VoxelWorld world)
        {
            this.world = world;
            world.BlockChanged += OnBlockChanged;
        }

        public IEnumerable<Designation> Designations => designations.Values;
        public IReadOnlyList<CraftBill> Bills => bills;
        public int DesignationCount => designations.Count;

        public Designation Get(Int3 cell) => designations.TryGetValue(cell, out var d) ? d : null;

        public bool AddMine(Int3 cell)
        {
            if (!world.InBounds(cell)) return false;
            var def = BlockRegistry.Get(world.GetBlock(cell));
            if (def.Type == BlockType.Air || !def.IsMinable) return false;
            if (designations.TryGetValue(cell, out var existing) && existing.Kind == DesignationKind.Mine) return false;
            designations[cell] = new Designation { Kind = DesignationKind.Mine, Cell = cell };
            Changed?.Invoke();
            return true;
        }

        public bool AddBuild(Int3 cell, BlockType type)
        {
            if (!world.InBounds(cell)) return false;
            if (world.GetBlock(cell) != BlockType.Air) return false;
            if (!BlockRegistry.Get(type).IsBuildable) return false;
            if (designations.TryGetValue(cell, out var existing) && existing.Kind == DesignationKind.Build && existing.BuildType == type)
                return false;
            designations[cell] = new Designation { Kind = DesignationKind.Build, Cell = cell, BuildType = type };
            Changed?.Invoke();
            return true;
        }

        public int AddMineBox(IntBox box)
        {
            int n = 0;
            foreach (var c in box.Cells()) if (AddMine(c)) n++;
            return n;
        }

        public int AddBuildBox(IntBox box, BlockType type)
        {
            int n = 0;
            foreach (var c in box.Cells()) if (AddBuild(c, type)) n++;
            return n;
        }

        public bool Cancel(Int3 cell)
        {
            if (!designations.Remove(cell)) return false;
            Changed?.Invoke();
            return true;
        }

        public int CancelBox(IntBox box)
        {
            int n = 0;
            var toRemove = new List<Int3>();
            foreach (var kv in designations)
                if (box.Contains(kv.Key)) toRemove.Add(kv.Key);
            foreach (var c in toRemove) { designations.Remove(c); n++; }
            if (n > 0) Changed?.Invoke();
            return n;
        }

        /// <summary>Completes and removes a designation (called by jobs).</summary>
        public void Complete(Designation d)
        {
            if (designations.TryGetValue(d.Cell, out var cur) && cur == d)
            {
                designations.Remove(d.Cell);
                Changed?.Invoke();
            }
        }

        public bool IsActive(Designation d) => designations.TryGetValue(d.Cell, out var cur) && cur == d;

        public void MarkFailed(Designation d, float now, float delay = 6f)
        {
            d.RetryAfter = now + delay;
            d.FailedAtVersion = world.Version;
            d.ReservedBy = null;
        }

        public void Release(Designation d, object owner)
        {
            if (d.ReservedBy == owner) d.ReservedBy = null;
        }

        /// <summary>Nearest available designation of a kind, optionally filtered. Uses straight-line distance as the estimate.</summary>
        public Designation FindNearest(Int3 from, DesignationKind kind, object requester, float now, Func<Designation, bool> filter = null)
        {
            Designation best = null;
            float bestD = float.MaxValue;
            int version = world.Version;
            foreach (var d in designations.Values)
            {
                if (d.Kind != kind) continue;
                if (!d.IsAvailable(requester, now, version)) continue;
                if (filter != null && !filter(d)) continue;
                float dist = d.Cell.EuclideanDistance(from);
                if (dist < bestD)
                {
                    bestD = dist;
                    best = d;
                }
            }
            return best;
        }

        // ---- Craft bills ----

        public CraftBill AddBill(Recipe recipe, Int3? station, int count)
        {
            var bill = new CraftBill { Recipe = recipe, Station = station, Remaining = Math.Max(1, count) };
            bills.Add(bill);
            Changed?.Invoke();
            return bill;
        }

        public void RemoveBill(CraftBill bill)
        {
            if (bills.Remove(bill)) Changed?.Invoke();
        }

        public void BillProgress(CraftBill bill)
        {
            bill.Remaining--;
            if (bill.Remaining <= 0) bills.Remove(bill);
            Changed?.Invoke();
        }

        public void MarkBillFailed(CraftBill bill, float now, float delay = 8f)
        {
            bill.RetryAfter = now + delay;
            bill.ReservedBy = null;
        }

        public IEnumerable<CraftBill> BillsAt(Int3? station)
        {
            foreach (var b in bills)
                if (Nullable.Equals(b.Station, station)) yield return b;
        }

        public CraftBill FindBill(object requester, float now, Func<CraftBill, bool> filter)
        {
            foreach (var b in bills)
            {
                if (b.ReservedBy != null && b.ReservedBy != requester) continue;
                if (now < b.RetryAfter) continue;
                if (b.Station.HasValue && world.GetBlock(b.Station.Value) != b.Recipe.Station) continue;
                if (filter != null && !filter(b)) continue;
                return b;
            }
            return null;
        }

        private void OnBlockChanged(Int3 pos, BlockType oldType, BlockType newType)
        {
            // A mined-out mine order or an occupied build site is no longer valid.
            if (designations.TryGetValue(pos, out var d))
            {
                bool invalid = (d.Kind == DesignationKind.Mine && newType == BlockType.Air)
                               || (d.Kind == DesignationKind.Build && newType != BlockType.Air);
                if (invalid)
                {
                    designations.Remove(pos);
                    Changed?.Invoke();
                }
            }
            // Bills attached to a removed station go away too.
            if (BlockRegistry.IsSolid(oldType) && newType == BlockType.Air)
            {
                int removed = bills.RemoveAll(b => b.Station.HasValue && b.Station.Value == pos);
                if (removed > 0) Changed?.Invoke();
            }
        }
    }
}
