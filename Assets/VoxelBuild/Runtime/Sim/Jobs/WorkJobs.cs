using System;

namespace VoxelBuild.Sim.Jobs
{
    using VoxelBuild.Core;
    using VoxelBuild.Nav;

    /// <summary>Walk within reach of a designated block and dig it out. The drop goes into the colonist's pack.</summary>
    public sealed class MineJob : Job
    {
        private readonly Designation d;
        private bool working;

        public MineJob(Designation d)
        {
            this.d = d;
        }

        public override string Label => "Mining";

        public override void OnStart(ColonistCore c)
        {
            d.ReservedBy = c;
        }

        public override JobStatus Tick(ColonistCore c, float dt)
        {
            var jobs = c.Ctx.Jobs;
            if (!jobs.IsActive(d)) return JobStatus.Done;
            var block = c.Ctx.World.GetBlock(d.Cell);
            if (block == BlockType.Air)
            {
                jobs.Complete(d);
                return JobStatus.Done;
            }
            var def = BlockRegistry.Get(block);
            if (!def.IsMinable)
            {
                jobs.Complete(d);
                return JobStatus.Done;
            }

            if (!working)
            {
                c.Activity = "Walking to " + def.Name;
                var r = c.MoveTo(d.Cell, MoveMode.Reach);
                if (r == MoveResult.Failed)
                {
                    jobs.MarkFailed(d, c.Ctx.Now);
                    return JobStatus.Failed;
                }
                if (r == MoveResult.Arrived) working = true;
                return JobStatus.Running;
            }

            if (!NavRules.CanReach(c.Cell, d.Cell))
            {
                working = false;
                return JobStatus.Running;
            }

            c.Activity = "Mining " + def.Name;
            c.FaceTowards(d.Cell);
            d.WorkDone += dt * c.Needs.Efficiency;
            if (d.WorkDone < def.MineSeconds * Scale.WorkTimeFactor) return JobStatus.Running;

            c.Ctx.World.SetBlock(d.Cell, BlockType.Air);
            if (!def.Drop.IsEmpty)
            {
                int added = c.Inventory.Add(def.Drop.Type, def.Drop.Count);
                int rest = def.Drop.Count - added;
                if (rest > 0) c.Ctx.Items.AddNear(d.Cell, def.Drop.Type, rest);
            }
            jobs.Complete(d);
            return JobStatus.Done;
        }

        public override void OnEnd(ColonistCore c, JobStatus status)
        {
            c.Ctx.Jobs.Release(d, c);
        }
    }

    /// <summary>Fetch the required material if needed, then place the designated block.</summary>
    public sealed class BuildJob : Job
    {
        private enum Phase { Fetch, Travel, Work }

        private readonly Designation d;
        private Phase phase = Phase.Fetch;
        private Int3? source;
        private float waitTime;

        public BuildJob(Designation d)
        {
            this.d = d;
        }

        public override string Label => "Building " + BlockRegistry.Get(d.BuildType).Name;

        public override void OnStart(ColonistCore c)
        {
            d.ReservedBy = c;
        }

        public override JobStatus Tick(ColonistCore c, float dt)
        {
            var ctx = c.Ctx;
            var jobs = ctx.Jobs;
            if (!jobs.IsActive(d)) return JobStatus.Done;
            var currentBlock = ctx.World.GetBlock(d.Cell);
            if (currentBlock != BlockType.Air && currentBlock != BlockType.Water)
            {
                jobs.Complete(d);
                return JobStatus.Done;
            }
            var def = BlockRegistry.Get(d.BuildType);
            var cost = def.BuildCost;

            switch (phase)
            {
                case Phase.Fetch:
                {
                    if (c.Inventory.Has(cost.Type, cost.Count))
                    {
                        phase = Phase.Travel;
                        return JobStatus.Running;
                    }
                    if (source == null || ctx.Items.Count(source.Value, cost.Type) <= 0)
                    {
                        if (!ctx.Items.FindNearestWithItem(c.Cell, cost.Type, out var s))
                        {
                            jobs.MarkFailed(d, ctx.Now, 10f);
                            return JobStatus.Failed;
                        }
                        source = s;
                    }
                    c.Activity = "Fetching " + ItemRegistry.Get(cost.Type).Name;
                    var r = c.MoveTo(source.Value, MoveMode.Adjacent);
                    if (r == MoveResult.Failed)
                    {
                        jobs.MarkFailed(d, ctx.Now);
                        return JobStatus.Failed;
                    }
                    if (r == MoveResult.Arrived)
                    {
                        int want = Math.Min(Math.Max(cost.Count, 16), c.Inventory.FreeSpace + c.Inventory.Count(cost.Type));
                        int take = Math.Min(ctx.Items.Count(source.Value, cost.Type), want - c.Inventory.Count(cost.Type));
                        if (take > 0)
                        {
                            int added = c.Inventory.Add(cost.Type, take);
                            ctx.Items.Remove(source.Value, cost.Type, added);
                        }
                        source = null;
                    }
                    return JobStatus.Running;
                }
                case Phase.Travel:
                {
                    c.Activity = "Walking to build site";
                    var r = c.MoveTo(d.Cell, MoveMode.Reach);
                    if (r == MoveResult.Failed)
                    {
                        jobs.MarkFailed(d, ctx.Now);
                        return JobStatus.Failed;
                    }
                    if (r == MoveResult.Arrived) phase = Phase.Work;
                    return JobStatus.Running;
                }
                default:
                {
                    if (!c.Inventory.Has(cost.Type, cost.Count))
                    {
                        phase = Phase.Fetch;
                        return JobStatus.Running;
                    }
                    if (!NavRules.CanReach(c.Cell, d.Cell) || c.OccupiesCell(d.Cell))
                    {
                        phase = Phase.Travel;
                        return JobStatus.Running;
                    }
                    if (ctx.IsCellOccupied(d.Cell, c))
                    {
                        c.Activity = "Waiting for space";
                        waitTime += dt;
                        if (waitTime > 6f)
                        {
                            jobs.MarkFailed(d, ctx.Now, 3f);
                            return JobStatus.Failed;
                        }
                        return JobStatus.Running;
                    }
                    c.Activity = "Building " + def.Name;
                    c.FaceTowards(d.Cell);
                    d.WorkDone += dt * c.Needs.Efficiency;
                    if (d.WorkDone < def.BuildSeconds * Scale.WorkTimeFactor) return JobStatus.Running;

                    c.Inventory.Remove(cost.Type, cost.Count);
                    ctx.World.SetBlock(d.Cell, d.BuildType);
                    jobs.Complete(d);
                    return JobStatus.Done;
                }
            }
        }

        public override void OnEnd(ColonistCore c, JobStatus status)
        {
            c.Ctx.Jobs.Release(d, c);
        }
    }

    /// <summary>Gather a recipe's inputs, go to the station (if any), work, and produce the output.</summary>
    public sealed class CraftJob : Job
    {
        private enum Phase { Gather, Travel, Work }

        private readonly CraftBill bill;
        private Phase phase = Phase.Gather;
        private Int3? source;
        private ItemType fetching;
        private float progress;

        public CraftJob(CraftBill bill)
        {
            this.bill = bill;
        }

        public override string Label => bill.Recipe.Name;

        public override void OnStart(ColonistCore c)
        {
            bill.ReservedBy = c;
        }

        private bool HasAllInputs(ColonistCore c)
        {
            foreach (var input in bill.Recipe.Inputs)
                if (!c.Inventory.Has(input.Type, input.Count)) return false;
            return true;
        }

        public override JobStatus Tick(ColonistCore c, float dt)
        {
            var ctx = c.Ctx;
            var jobs = ctx.Jobs;
            if (bill.Remaining <= 0) return JobStatus.Done;
            if (bill.Station.HasValue && ctx.World.GetBlock(bill.Station.Value) != bill.Recipe.Station)
            {
                jobs.RemoveBill(bill);
                return JobStatus.Failed;
            }

            switch (phase)
            {
                case Phase.Gather:
                {
                    if (HasAllInputs(c))
                    {
                        phase = Phase.Travel;
                        return JobStatus.Running;
                    }
                    if (source == null)
                    {
                        fetching = ItemType.None;
                        foreach (var input in bill.Recipe.Inputs)
                            if (!c.Inventory.Has(input.Type, input.Count)) { fetching = input.Type; break; }
                        if (fetching == ItemType.None || !ctx.Items.FindNearestWithItem(c.Cell, fetching, out var s))
                        {
                            jobs.MarkBillFailed(bill, ctx.Now);
                            return JobStatus.Failed;
                        }
                        source = s;
                    }
                    c.Activity = "Fetching " + ItemRegistry.Get(fetching).Name;
                    var r = c.MoveTo(source.Value, MoveMode.Adjacent);
                    if (r == MoveResult.Failed)
                    {
                        jobs.MarkBillFailed(bill, ctx.Now);
                        return JobStatus.Failed;
                    }
                    if (r == MoveResult.Arrived)
                    {
                        int need = 0;
                        foreach (var input in bill.Recipe.Inputs)
                            if (input.Type == fetching) need = input.Count - c.Inventory.Count(fetching);
                        int take = Math.Min(ctx.Items.Count(source.Value, fetching), Math.Max(need, 0));
                        if (take > 0)
                        {
                            int added = c.Inventory.Add(fetching, take);
                            ctx.Items.Remove(source.Value, fetching, added);
                        }
                        source = null;
                        if (take <= 0)
                        {
                            jobs.MarkBillFailed(bill, ctx.Now);
                            return JobStatus.Failed;
                        }
                    }
                    return JobStatus.Running;
                }
                case Phase.Travel:
                {
                    if (!bill.Station.HasValue)
                    {
                        phase = Phase.Work;
                        return JobStatus.Running;
                    }
                    c.Activity = "Walking to workbench";
                    var r = c.MoveTo(bill.Station.Value, MoveMode.Reach);
                    if (r == MoveResult.Failed)
                    {
                        jobs.MarkBillFailed(bill, ctx.Now);
                        return JobStatus.Failed;
                    }
                    if (r == MoveResult.Arrived) phase = Phase.Work;
                    return JobStatus.Running;
                }
                default:
                {
                    if (!HasAllInputs(c))
                    {
                        phase = Phase.Gather;
                        return JobStatus.Running;
                    }
                    if (bill.Station.HasValue)
                    {
                        if (!NavRules.CanReach(c.Cell, bill.Station.Value))
                        {
                            phase = Phase.Travel;
                            return JobStatus.Running;
                        }
                        c.FaceTowards(bill.Station.Value);
                    }
                    c.Activity = bill.Recipe.Name;
                    progress += dt * c.Needs.Efficiency;
                    if (progress < bill.Recipe.WorkSeconds) return JobStatus.Running;

                    foreach (var input in bill.Recipe.Inputs) c.Inventory.Remove(input.Type, input.Count);
                    var output = bill.Recipe.Output;
                    int addedOut = c.Inventory.Add(output.Type, output.Count);
                    if (addedOut < output.Count) ctx.Items.AddNear(c.Cell, output.Type, output.Count - addedOut);
                    jobs.BillProgress(bill);
                    ctx.Log($"{c.Name} crafted {output}");
                    return JobStatus.Done;
                }
            }
        }

        public override void OnEnd(ColonistCore c, JobStatus status)
        {
            if (bill.ReservedBy == c) bill.ReservedBy = null;
        }
    }
}
