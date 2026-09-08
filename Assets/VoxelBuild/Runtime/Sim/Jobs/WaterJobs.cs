using System;

namespace VoxelBuild.Sim.Jobs
{
    using VoxelBuild.Core;
    using VoxelBuild.Nav;

    /// <summary>Shared bucket handling for the water jobs.</summary>
    internal static class BucketLogic
    {
        public const float ScoopSeconds = 1.5f;
        public const float PourSeconds = 1.0f;

        public static bool HasEmpty(ColonistCore c) => c.Inventory.Count(ItemType.Bucket) > 0;
        public static bool HasFull(ColonistCore c) => c.Inventory.Count(ItemType.WaterBucket) > 0;
        public static bool HasAny(ColonistCore c) => HasEmpty(c) || HasFull(c);

        public static void Fill(ColonistCore c)
        {
            if (c.Inventory.Remove(ItemType.Bucket, 1) > 0) c.Inventory.Add(ItemType.WaterBucket, 1);
        }

        public static void Empty(ColonistCore c)
        {
            if (c.Inventory.Remove(ItemType.WaterBucket, 1) > 0) c.Inventory.Add(ItemType.Bucket, 1);
        }

        /// <summary>
        /// Walks to and picks up a bucket (empty or full) from the ground. Returns Arrived once one is in the pack,
        /// Failed if there is none, Moving otherwise.
        /// </summary>
        public static MoveResult AcquireBucket(ColonistCore c, ref Int3? source)
        {
            if (HasAny(c)) return MoveResult.Arrived;
            var ctx = c.Ctx;
            if (source == null || (ctx.Items.Count(source.Value, ItemType.Bucket) <= 0 && ctx.Items.Count(source.Value, ItemType.WaterBucket) <= 0))
            {
                bool found = ctx.Items.FindNearest(c.Cell, (cell, stacks) =>
                    (stacks.TryGetValue(ItemType.Bucket, out var n) && n > 0) || (stacks.TryGetValue(ItemType.WaterBucket, out var m) && m > 0), out var s);
                if (!found) return MoveResult.Failed;
                source = s;
            }
            c.Activity = "Fetching bucket";
            var r = c.MoveTo(source.Value, MoveMode.Adjacent);
            if (r != MoveResult.Arrived) return r;
            var type = ctx.Items.Count(source.Value, ItemType.WaterBucket) > 0 ? ItemType.WaterBucket : ItemType.Bucket;
            int added = c.Inventory.Add(type, 1);
            ctx.Items.Remove(source.Value, type, added);
            source = null;
            return added > 0 ? MoveResult.Arrived : MoveResult.Failed;
        }
    }

    /// <summary>Scoop a designated water cell into a bucket, then carry it to a Pour order or dump it.</summary>
    public sealed class DrainJob : Job
    {
        private enum Phase { Bucket, Travel, Scoop, Dispose }

        private readonly Designation d;
        private Phase phase = Phase.Bucket;
        private Int3? bucketSource;
        private float work;
        private PourJob pour;

        public DrainJob(Designation d)
        {
            this.d = d;
        }

        public override string Label => "Draining water";

        public override void OnStart(ColonistCore c)
        {
            d.ReservedBy = c;
        }

        public override JobStatus Tick(ColonistCore c, float dt)
        {
            var ctx = c.Ctx;
            var jobs = ctx.Jobs;
            if (phase != Phase.Dispose)
            {
                if (!jobs.IsActive(d)) return JobStatus.Done;
                if (ctx.Fluids.Level(d.Cell) <= 0)
                {
                    jobs.Complete(d);
                    return JobStatus.Done;
                }
            }

            switch (phase)
            {
                case Phase.Bucket:
                {
                    if (BucketLogic.HasFull(c))
                    {
                        // Empty a carried full bucket first: use it on a pour order if any, else dump it.
                        phase = Phase.Dispose;
                        return JobStatus.Running;
                    }
                    var r = BucketLogic.AcquireBucket(c, ref bucketSource);
                    if (r == MoveResult.Failed)
                    {
                        jobs.MarkFailed(d, ctx.Now, 10f);
                        return JobStatus.Failed;
                    }
                    if (r == MoveResult.Arrived) phase = Phase.Travel;
                    return JobStatus.Running;
                }
                case Phase.Travel:
                {
                    c.Activity = "Walking to water";
                    var r = c.MoveTo(d.Cell, MoveMode.Reach);
                    if (r == MoveResult.Failed)
                    {
                        jobs.MarkFailed(d, ctx.Now);
                        return JobStatus.Failed;
                    }
                    if (r == MoveResult.Arrived) phase = Phase.Scoop;
                    return JobStatus.Running;
                }
                case Phase.Scoop:
                {
                    if (!NavRules.CanReach(c.Cell, d.Cell))
                    {
                        phase = Phase.Travel;
                        return JobStatus.Running;
                    }
                    if (!BucketLogic.HasEmpty(c))
                    {
                        phase = Phase.Bucket;
                        return JobStatus.Running;
                    }
                    c.Activity = "Scooping water";
                    c.FaceTowards(d.Cell);
                    work += dt * c.Needs.Efficiency;
                    if (work < BucketLogic.ScoopSeconds) return JobStatus.Running;
                    int units = ctx.Fluids.Scoop(d.Cell);
                    if (units > 0) BucketLogic.Fill(c);
                    jobs.Complete(d);
                    phase = Phase.Dispose;
                    work = 0f;
                    return JobStatus.Running;
                }
                default:
                {
                    if (!BucketLogic.HasFull(c)) return JobStatus.Done;
                    if (pour == null)
                    {
                        var target = jobs.FindNearest(c.Cell, DesignationKind.Pour, c, ctx.Now);
                        if (target == null)
                        {
                            BucketLogic.Empty(c);
                            c.Activity = "Dumped water";
                            return JobStatus.Done;
                        }
                        pour = new PourJob(target);
                        pour.OnStart(c);
                    }
                    var status = pour.Tick(c, dt);
                    if (status != JobStatus.Running)
                    {
                        pour.OnEnd(c, status);
                        pour = null;
                        if (status == JobStatus.Failed) BucketLogic.Empty(c);
                        return JobStatus.Done;
                    }
                    return JobStatus.Running;
                }
            }
        }

        public override void OnEnd(ColonistCore c, JobStatus status)
        {
            c.Ctx.Jobs.Release(d, c);
            if (pour != null)
            {
                pour.OnEnd(c, status);
                pour = null;
            }
        }
    }

    /// <summary>Fill a bucket at the nearest water (springs preferred) and empty it into the designated cell.</summary>
    public sealed class PourJob : Job
    {
        private enum Phase { Bucket, FindWater, TravelWater, Draw, Travel, Pour }

        private readonly Designation d;
        private Phase phase = Phase.Bucket;
        private Int3? bucketSource;
        private Int3 waterCell;
        private float work;

        public PourJob(Designation d)
        {
            this.d = d;
        }

        public override string Label => "Pouring water";

        public override void OnStart(ColonistCore c)
        {
            d.ReservedBy = c;
        }

        public override JobStatus Tick(ColonistCore c, float dt)
        {
            var ctx = c.Ctx;
            var jobs = ctx.Jobs;
            if (!jobs.IsActive(d)) return JobStatus.Done;
            if (ctx.World.IsSolid(d.Cell) || ctx.Fluids.Level(d.Cell) >= FluidSim.Max)
            {
                jobs.Complete(d);
                return JobStatus.Done;
            }

            switch (phase)
            {
                case Phase.Bucket:
                {
                    var r = BucketLogic.AcquireBucket(c, ref bucketSource);
                    if (r == MoveResult.Failed)
                    {
                        jobs.MarkFailed(d, ctx.Now, 10f);
                        return JobStatus.Failed;
                    }
                    if (r == MoveResult.Arrived) phase = BucketLogic.HasFull(c) ? Phase.Travel : Phase.FindWater;
                    return JobStatus.Running;
                }
                case Phase.FindWater:
                {
                    if (!ctx.Fluids.FindNearestWater(c.Cell, out waterCell))
                    {
                        jobs.MarkFailed(d, ctx.Now, 15f);
                        return JobStatus.Failed;
                    }
                    phase = Phase.TravelWater;
                    return JobStatus.Running;
                }
                case Phase.TravelWater:
                {
                    if (ctx.Fluids.Level(waterCell) <= 0)
                    {
                        phase = Phase.FindWater;
                        return JobStatus.Running;
                    }
                    c.Activity = "Walking to water";
                    var r = c.MoveTo(waterCell, MoveMode.Reach);
                    if (r == MoveResult.Failed)
                    {
                        jobs.MarkFailed(d, ctx.Now);
                        return JobStatus.Failed;
                    }
                    if (r == MoveResult.Arrived) phase = Phase.Draw;
                    return JobStatus.Running;
                }
                case Phase.Draw:
                {
                    if (!NavRules.CanReach(c.Cell, waterCell))
                    {
                        phase = Phase.TravelWater;
                        return JobStatus.Running;
                    }
                    c.Activity = "Filling bucket";
                    c.FaceTowards(waterCell);
                    work += dt * c.Needs.Efficiency;
                    if (work < BucketLogic.ScoopSeconds) return JobStatus.Running;
                    work = 0f;
                    if (ctx.Fluids.Draw(waterCell) <= 0)
                    {
                        phase = Phase.FindWater;
                        return JobStatus.Running;
                    }
                    BucketLogic.Fill(c);
                    phase = Phase.Travel;
                    return JobStatus.Running;
                }
                case Phase.Travel:
                {
                    c.Activity = "Carrying water";
                    var r = c.MoveTo(d.Cell, MoveMode.Reach);
                    if (r == MoveResult.Failed)
                    {
                        jobs.MarkFailed(d, ctx.Now);
                        return JobStatus.Failed;
                    }
                    if (r == MoveResult.Arrived) phase = Phase.Pour;
                    return JobStatus.Running;
                }
                default:
                {
                    if (!NavRules.CanReach(c.Cell, d.Cell))
                    {
                        phase = Phase.Travel;
                        return JobStatus.Running;
                    }
                    if (!BucketLogic.HasFull(c))
                    {
                        phase = Phase.Bucket;
                        return JobStatus.Running;
                    }
                    if (c.OccupiesCell(d.Cell))
                    {
                        phase = Phase.Travel;
                        return JobStatus.Running;
                    }
                    c.Activity = "Pouring water";
                    c.FaceTowards(d.Cell);
                    work += dt * c.Needs.Efficiency;
                    if (work < BucketLogic.PourSeconds) return JobStatus.Running;
                    ctx.Fluids.Pour(d.Cell, FluidSim.Max);
                    BucketLogic.Empty(c);
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
}
