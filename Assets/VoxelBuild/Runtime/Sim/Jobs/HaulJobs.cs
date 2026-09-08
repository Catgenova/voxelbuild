using System;

namespace VoxelBuild.Sim.Jobs
{
    using VoxelBuild.Core;

    /// <summary>Carry everything in the pack to a stockpile. Drops at the feet if no stockpile can take it.</summary>
    public sealed class DepositJob : Job
    {
        private Int3? dest;
        private ItemType type;

        public override string Label => "Hauling to stockpile";

        public override JobStatus Tick(ColonistCore c, float dt)
        {
            var ctx = c.Ctx;
            if (c.Inventory.IsEmpty) return JobStatus.Done;

            if (dest == null)
            {
                type = ItemType.None;
                foreach (var s in c.Inventory.Stacks()) { type = s.Type; break; }
                if (!ctx.Stockpiles.FindCellFor(c.Cell, type, out var d))
                {
                    DropAll(c);
                    return JobStatus.Done;
                }
                dest = d;
            }

            c.Activity = "Hauling " + ItemRegistry.Get(type).Name;
            var r = c.MoveTo(dest.Value, MoveMode.Adjacent);
            if (r == MoveResult.Failed)
            {
                DropAll(c);
                return JobStatus.Failed;
            }
            if (r == MoveResult.Arrived)
            {
                int room = GroundItems.MaxPerCell - ctx.Items.TotalInCell(dest.Value);
                int n = Math.Min(room, c.Inventory.Count(type));
                if (n > 0)
                {
                    ctx.Items.Add(dest.Value, type, n);
                    c.Inventory.Remove(type, n);
                }
                dest = null;
            }
            return JobStatus.Running;
        }

        private static void DropAll(ColonistCore c)
        {
            foreach (var s in c.Inventory.ToList())
            {
                c.Inventory.Remove(s.Type, s.Count);
                c.Ctx.Items.AddNear(c.Cell, s.Type, s.Count);
            }
        }
    }

    /// <summary>Pick up a loose pile and bring it to a stockpile.</summary>
    public sealed class HaulJob : Job
    {
        private readonly Int3 pile;
        private readonly DepositJob deposit = new DepositJob();
        private bool collected;

        public Int3 Pile => pile;

        public HaulJob(Int3 pile)
        {
            this.pile = pile;
        }

        public override string Label => "Hauling";

        public override JobStatus Tick(ColonistCore c, float dt)
        {
            var ctx = c.Ctx;
            if (!collected)
            {
                if (!ctx.Items.HasItems(pile)) return JobStatus.Done;
                c.Activity = "Collecting items";
                var r = c.MoveTo(pile, MoveMode.Adjacent);
                if (r == MoveResult.Failed)
                {
                    c.BlacklistPile(pile);
                    return JobStatus.Failed;
                }
                if (r != MoveResult.Arrived) return JobStatus.Running;

                foreach (var s in ctx.Items.StacksIn(pile).ToArraySafe())
                {
                    int added = c.Inventory.Add(s.Type, s.Count);
                    if (added > 0) ctx.Items.Remove(pile, s.Type, added);
                    if (c.Inventory.IsFull) break;
                }
                collected = true;
                if (c.Inventory.IsEmpty) return JobStatus.Done;
            }
            return deposit.Tick(c, dt);
        }
    }

    internal static class EnumerableExtensions
    {
        public static ItemStack[] ToArraySafe(this System.Collections.Generic.IEnumerable<ItemStack> src)
        {
            var list = new System.Collections.Generic.List<ItemStack>();
            foreach (var s in src) list.Add(s);
            return list.ToArray();
        }
    }
}
