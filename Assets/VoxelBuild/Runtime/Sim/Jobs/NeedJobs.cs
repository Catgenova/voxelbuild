using System;

namespace VoxelBuild.Sim.Jobs
{
    using VoxelBuild.Core;
    using VoxelBuild.Nav;

    /// <summary>Find food (pack, ground pile, or a wild berry bush) and eat until satisfied.</summary>
    public sealed class EatJob : Job
    {
        private enum Phase { Find, FetchPile, Forage, Eat }

        private Phase phase = Phase.Find;
        private Int3 target;
        private ItemType food;
        private float eatTimer;
        private float forageWork;

        public override string Label => "Eating";

        public override JobStatus Tick(ColonistCore c, float dt)
        {
            var ctx = c.Ctx;
            switch (phase)
            {
                case Phase.Find:
                {
                    if (c.Inventory.HasAnyEdible())
                    {
                        phase = Phase.Eat;
                        return JobStatus.Running;
                    }
                    if (ctx.Items.FindNearestEdible(c.Cell, out target, out food))
                    {
                        phase = Phase.FetchPile;
                        return JobStatus.Running;
                    }
                    if (ctx.FindNearestBlock(c.Cell, BlockType.BerryBush, null, out target))
                    {
                        phase = Phase.Forage;
                        return JobStatus.Running;
                    }
                    c.Activity = "No food!";
                    c.FoodSearchFailedAt = ctx.Now;
                    return JobStatus.Failed;
                }
                case Phase.FetchPile:
                {
                    c.Activity = "Getting food";
                    var r = c.MoveTo(target, MoveMode.Adjacent);
                    if (r == MoveResult.Failed)
                    {
                        c.BlacklistPile(target);
                        phase = Phase.Find;
                        return JobStatus.Running;
                    }
                    if (r == MoveResult.Arrived)
                    {
                        int take = Math.Min(3, ctx.Items.Count(target, food));
                        int added = c.Inventory.Add(food, take);
                        ctx.Items.Remove(target, food, added);
                        phase = added > 0 ? Phase.Eat : Phase.Find;
                    }
                    return JobStatus.Running;
                }
                case Phase.Forage:
                {
                    if (ctx.World.GetBlock(target) != BlockType.BerryBush)
                    {
                        phase = Phase.Find;
                        return JobStatus.Running;
                    }
                    c.Activity = "Foraging berries";
                    var r = c.MoveTo(target, MoveMode.Reach);
                    if (r == MoveResult.Failed)
                    {
                        c.FoodSearchFailedAt = ctx.Now;
                        return JobStatus.Failed;
                    }
                    if (r != MoveResult.Arrived) return JobStatus.Running;
                    c.FaceTowards(target);
                    forageWork += dt * c.Needs.Efficiency;
                    var def = BlockRegistry.Get(BlockType.BerryBush);
                    if (forageWork < def.MineSeconds * Scale.WorkTimeFactor) return JobStatus.Running;
                    ctx.World.SetBlock(target, BlockType.Air);
                    int got = c.Inventory.Add(def.Drop.Type, def.Drop.Count);
                    if (got < def.Drop.Count) ctx.Items.AddNear(target, def.Drop.Type, def.Drop.Count - got);
                    phase = Phase.Eat;
                    return JobStatus.Running;
                }
                default:
                {
                    var type = c.Inventory.FirstEdible();
                    if (type == ItemType.None)
                    {
                        if (c.Needs.Food >= Needs.HungryThreshold) return JobStatus.Done;
                        phase = Phase.Find;
                        return JobStatus.Running;
                    }
                    c.Activity = "Eating " + ItemRegistry.Get(type).Name;
                    eatTimer += dt;
                    if (eatTimer < 1.5f) return JobStatus.Running;
                    eatTimer = 0f;
                    c.Inventory.Remove(type, 1);
                    c.Needs.Eat(ItemRegistry.Get(type).Nutrition);
                    if (c.Needs.Food >= 0.92f) return JobStatus.Done;
                    return JobStatus.Running;
                }
            }
        }
    }

    /// <summary>Sleep in the nearest free bed, or on the ground if there is none.</summary>
    public sealed class SleepJob : Job
    {
        private Int3? bed;
        private bool searched;
        private bool sleeping;

        public override string Label => "Sleeping";

        public override JobStatus Tick(ColonistCore c, float dt)
        {
            var ctx = c.Ctx;
            if (!searched)
            {
                searched = true;
                if (ctx.FindNearestBlock(c.Cell, BlockType.Bed, p => !IsBedTaken(ctx, p, c), out var b)) bed = b;
            }

            if (!sleeping)
            {
                if (bed.HasValue)
                {
                    c.Activity = "Going to bed";
                    var r = c.MoveTo(bed.Value, MoveMode.Adjacent);
                    if (r == MoveResult.Failed) bed = null;
                    else if (r != MoveResult.Arrived) return JobStatus.Running;
                }
                sleeping = true;
                c.IsSleeping = true;
                c.InBed = bed.HasValue;
                c.BedCell = bed;
            }

            c.Activity = c.InBed ? "Sleeping in bed" : "Sleeping on the ground";
            if (c.Needs.Rest >= 0.98f) return JobStatus.Done;
            if (c.Needs.IsStarving && c.Needs.Rest > 0.4f) return JobStatus.Done;
            return JobStatus.Running;
        }

        private static bool IsBedTaken(GameContext ctx, Int3 bedCell, ColonistCore self)
        {
            foreach (var o in ctx.Colonists)
                if (o != self && o.BedCell.HasValue && o.BedCell.Value == bedCell) return true;
            return false;
        }

        public override void OnEnd(ColonistCore c, JobStatus status)
        {
            c.IsSleeping = false;
            c.InBed = false;
            c.BedCell = null;
        }
    }

    /// <summary>Wait a moment, then wander a few cells. Keeps idle colonists looking alive.</summary>
    public sealed class IdleJob : Job
    {
        private float wait;
        private float elapsed;
        private bool wandering;
        private Int3 target;

        public override string Label => "Idle";

        public override void OnStart(ColonistCore c)
        {
            wait = 1f + (float)c.Ctx.Random.NextDouble() * 2.5f;
        }

        public override JobStatus Tick(ColonistCore c, float dt)
        {
            elapsed += dt;
            c.Activity = "Idle";
            if (elapsed < wait) return JobStatus.Running;
            if (elapsed > 10f) return JobStatus.Done;

            if (!wandering)
            {
                var rnd = c.Ctx.Random;
                int range = Scale.BlocksPerMetre * 3;
                var offset = new Int3(rnd.Next(-range, range + 1), 0, rnd.Next(-range, range + 1));
                var guess = c.Cell + offset;
                bool found = false;
                for (int dy = NavRules.StepUp * 2; dy >= -NavRules.MaxFall && !found; dy--)
                {
                    var p = guess + new Int3(0, dy, 0);
                    if (NavRules.IsStandable(c.Ctx.World, p)) { target = p; found = true; }
                }
                if (!found) return JobStatus.Done;
                wandering = true;
            }

            c.Activity = "Wandering";
            var r = c.MoveTo(target, MoveMode.Exact);
            return r == MoveResult.Moving ? JobStatus.Running : JobStatus.Done;
        }
    }
}
