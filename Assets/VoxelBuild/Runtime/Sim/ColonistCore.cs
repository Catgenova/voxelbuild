using System;
using System.Collections.Generic;

namespace VoxelBuild.Sim
{
    using VoxelBuild.Core;
    using VoxelBuild.Nav;
    using VoxelBuild.Sim.Jobs;

    public enum MoveMode
    {
        /// <summary>Stand exactly on the target cell.</summary>
        Exact,
        /// <summary>Stand anywhere the target block can be worked from.</summary>
        Reach,
        /// <summary>Stand on or next to the target cell (used for item piles and beds).</summary>
        Adjacent,
    }

    public enum MoveResult
    {
        Moving,
        Arrived,
        Failed,
    }

    public enum WorkType
    {
        Build = 0,
        Mine = 1,
        Craft = 2,
        Haul = 3,
        Count = 4,
    }

    /// <summary>RimWorld-style work priorities: 0 = never, 1 = first, 4 = last.</summary>
    public sealed class WorkPriorities
    {
        public const int MaxPriority = 4;
        private readonly int[] priority = { 2, 2, 3, 3 };

        public int Get(WorkType w) => priority[(int)w];

        public void Set(WorkType w, int p) => priority[(int)w] = Math.Max(0, Math.Min(MaxPriority, p));

        /// <summary>Advance to the next priority value, wrapping 4 back to disabled.</summary>
        public void Cycle(WorkType w) => priority[(int)w] = (priority[(int)w] + 1) % (MaxPriority + 1);

        public IEnumerable<WorkType> Ordered()
        {
            for (int p = 1; p <= MaxPriority; p++)
                for (int i = 0; i < (int)WorkType.Count; i++)
                    if (priority[i] == p) yield return (WorkType)i;
        }
    }

    /// <summary>
    /// Engine-independent colonist: needs, pack, work settings, movement, and the brain that picks jobs.
    /// The Unity layer wraps one of these in a view.
    /// </summary>
    public sealed class ColonistCore
    {
        public readonly int Id;
        public string Name;
        public ColorRgb Color = ColorRgb.White;
        public readonly GameContext Ctx;
        public readonly Needs Needs = new Needs();
        public readonly Inventory Inventory = new Inventory(24);
        public readonly WorkPriorities Work = new WorkPriorities();
        public readonly PathFollower Mover = new PathFollower();

        public Job CurrentJob { get; private set; }
        public string Activity = "Idle";
        public bool IsSleeping;
        public bool InBed;
        public Int3? BedCell;
        public float FoodSearchFailedAt = -100f;

        public float WalkSpeedCells = 3.6f;
        public float FacingX => Mover.FacingX;
        public float FacingZ => Mover.FacingZ;

        private float jobCooldown;
        private readonly List<Int3> pathBuffer = new List<Int3>();
        private readonly Dictionary<Int3, float> pileBlacklist = new Dictionary<Int3, float>();

        // Movement request state.
        private bool moveActive;
        private bool moveRequestedThisTick;
        private bool needReplan;
        private Int3 moveTarget;
        private MoveMode moveMode;
        private float replanDelay;

        public event Action<ColonistCore> JobChanged;

        public ColonistCore(GameContext ctx, int id, string name)
        {
            Ctx = ctx;
            Id = id;
            Name = name;
        }

        public Int3 Cell => Mover.Cell;

        public void Spawn(Int3 cell)
        {
            Mover.Teleport(cell);
        }

        public bool OccupiesCell(Int3 cell)
        {
            var f = Cell;
            return f.x == cell.x && f.z == cell.z && cell.y >= f.y && cell.y < f.y + NavRules.Clearance;
        }

        public void FaceTowards(Int3 target)
        {
            float dx = target.x + 0.5f - Mover.PosX, dz = target.z + 0.5f - Mover.PosZ;
            float l = (float)Math.Sqrt(dx * dx + dz * dz);
            if (l < 0.01f) return;
            Mover.FacingX = dx / l;
            Mover.FacingZ = dz / l;
        }

        public void BlacklistPile(Int3 cell) => pileBlacklist[cell] = Ctx.Now + 12f;

        public bool IsPileBlacklisted(Int3 cell) => pileBlacklist.TryGetValue(cell, out var t) && Ctx.Now < t;

        // ---------------------------------------------------------------- Tick

        public void Tick(float dt)
        {
            Needs.Tick(dt, Ctx.Clock.DayLengthSeconds, IsSleeping, InBed);
            ResolveStanding();

            moveRequestedThisTick = false;
            if (CurrentJob == null)
            {
                jobCooldown -= dt;
                if (jobCooldown <= 0f) StartJob(ChooseJob());
            }

            if (CurrentJob != null)
            {
                JobStatus status;
                try
                {
                    status = CurrentJob.Tick(this, dt);
                }
                catch (Exception e)
                {
                    Ctx.Log($"{Name}: job {CurrentJob.Label} crashed: {e.Message}");
                    status = JobStatus.Failed;
                }
                if (status != JobStatus.Running)
                {
                    EndJob(status);
                    jobCooldown = status == JobStatus.Failed ? 0.75f : 0.1f;
                }
            }

            if (moveActive && !moveRequestedThisTick) StopMoving();
            if (moveActive)
            {
                Mover.SpeedCellsPerSecond = WalkSpeedCells * Needs.Efficiency;
                if (!Mover.Tick(dt, Ctx.World)) needReplan = true;
            }
            if (replanDelay > 0f) replanDelay -= dt;
        }

        private void StartJob(Job job)
        {
            CurrentJob = job;
            job?.OnStart(this);
            JobChanged?.Invoke(this);
        }

        private void EndJob(JobStatus status)
        {
            var j = CurrentJob;
            CurrentJob = null;
            j?.OnEnd(this, status);
            StopMoving();
            JobChanged?.Invoke(this);
        }

        /// <summary>Drops the current job (used by the player's cancel orders).</summary>
        public void Interrupt()
        {
            if (CurrentJob != null) EndJob(JobStatus.Failed);
        }

        /// <summary>Keeps the colonist on solid ground when the world changes under (or into) them.</summary>
        private void ResolveStanding()
        {
            var world = Ctx.World;
            var cell = Cell;
            if (world.IsSolid(cell))
            {
                // Buried: push upward to the first free standable cell.
                for (int i = 1; i < 12; i++)
                {
                    var up = cell + new Int3(0, i, 0);
                    if (!world.InBounds(up)) break;
                    if (NavRules.IsStandable(world, up))
                    {
                        Mover.Teleport(up);
                        StopMoving();
                        return;
                    }
                }
                return;
            }
            if (!moveActive && !NavRules.IsStandable(world, cell))
            {
                if (NavRules.FindGround(world, cell, 64, out var ground) && ground != cell)
                {
                    Mover.Teleport(ground);
                    StopMoving();
                }
            }
        }

        // ---------------------------------------------------------------- Movement

        public MoveResult MoveTo(Int3 target, MoveMode mode)
        {
            moveRequestedThisTick = true;
            bool sameRequest = moveActive && target == moveTarget && mode == moveMode;

            if (sameRequest && !needReplan)
            {
                if (Mover.HasPath) return MoveResult.Moving;
                if (GoalSatisfied(Cell, target, mode)) return MoveResult.Arrived;
                needReplan = true;
            }

            if (needReplan && replanDelay > 0f) return MoveResult.Moving;
            needReplan = false;
            moveTarget = target;
            moveMode = mode;

            if (GoalSatisfied(Cell, target, mode) && NavRules.IsStandable(Ctx.World, Cell))
            {
                moveActive = true;
                Mover.Stop();
                return MoveResult.Arrived;
            }

            bool ok = Ctx.Pathfinder.FindPath(Cell, c => GoalSatisfied(c, target, mode), target, pathBuffer);
            if (!ok)
            {
                moveActive = false;
                replanDelay = 1f;
                return MoveResult.Failed;
            }
            Mover.SetPath(pathBuffer);
            moveActive = true;
            return Mover.HasPath ? MoveResult.Moving : MoveResult.Arrived;
        }

        private bool GoalSatisfied(Int3 cell, Int3 target, MoveMode mode)
        {
            switch (mode)
            {
                case MoveMode.Exact:
                    return cell == target;
                case MoveMode.Reach:
                    if (!NavRules.CanReach(cell, target)) return false;
                    // Cannot stand inside the block being worked on.
                    return !(cell.x == target.x && cell.z == target.z && target.y >= cell.y && target.y < cell.y + NavRules.Clearance);
                default:
                    return cell.HorizontalDistance(target) <= 1 && Math.Abs(cell.y - target.y) <= 1;
            }
        }

        public void StopMoving()
        {
            moveActive = false;
            needReplan = false;
            if (Mover.HasPath) Mover.Stop();
        }

        // ---------------------------------------------------------------- Brain

        private bool FoodAvailable()
        {
            if (Inventory.HasAnyEdible()) return true;
            if (Ctx.Now - FoodSearchFailedAt < 20f) return false;
            return true; // EatJob searches piles and bushes itself
        }

        private Job ChooseJob()
        {
            if (Needs.IsStarving && FoodAvailable()) return new EatJob();
            if (Needs.IsExhausted) return new SleepJob();
            if (Needs.IsHungry && FoodAvailable()) return new EatJob();
            if (Ctx.Clock.IsNight && Needs.Rest < 0.75f) return new SleepJob();
            if (Needs.IsTired) return new SleepJob();

            // Full pack: unload before anything else, otherwise mining and hauling stall.
            if (Inventory.FreeSpace < 4) return new DepositJob();

            foreach (var w in Work.Ordered())
            {
                var job = TryWork(w);
                if (job != null) return job;
            }

            if (!Inventory.IsEmpty && Ctx.Stockpiles.HasFreeSpace()) return new DepositJob();
            return new IdleJob();
        }

        private Job TryWork(WorkType w)
        {
            var jobs = Ctx.Jobs;
            float now = Ctx.Now;
            switch (w)
            {
                case WorkType.Mine:
                {
                    var d = jobs.FindNearest(Cell, DesignationKind.Mine, this, now,
                        x => BlockRegistry.Get(Ctx.World.GetBlock(x.Cell)).IsMinable);
                    return d != null ? new MineJob(d) : null;
                }
                case WorkType.Build:
                {
                    var d = jobs.FindNearest(Cell, DesignationKind.Build, this, now, x =>
                    {
                        var cost = BlockRegistry.Get(x.BuildType).BuildCost;
                        if (Ctx.World.GetBlock(x.Cell) != BlockType.Air) return false;
                        return Inventory.Has(cost.Type, cost.Count) || Ctx.Items.TotalOf(cost.Type) >= cost.Count;
                    });
                    return d != null ? new BuildJob(d) : null;
                }
                case WorkType.Craft:
                {
                    var bill = jobs.FindBill(this, now, b =>
                    {
                        foreach (var input in b.Recipe.Inputs)
                            if (Inventory.Count(input.Type) + Ctx.Items.TotalOf(input.Type) < input.Count) return false;
                        return true;
                    });
                    return bill != null ? new CraftJob(bill) : null;
                }
                case WorkType.Haul:
                {
                    if (Ctx.Stockpiles.Count == 0) return null;
                    if (!Inventory.IsEmpty && Ctx.Stockpiles.HasFreeSpace()) return new DepositJob();
                    if (!Ctx.Stockpiles.HasFreeSpace()) return null;
                    bool found = Ctx.Items.FindNearest(Cell, (cell, stacks) =>
                        !Ctx.Stockpiles.IsStored(cell) && !IsPileBlacklisted(cell) && !IsPileReserved(cell), out var pile);
                    return found ? new HaulJob(pile) : null;
                }
            }
            return null;
        }

        private bool IsPileReserved(Int3 cell)
        {
            // Cheap check: another colonist already walking to haul this pile.
            foreach (var o in Ctx.Colonists)
                if (o != this && o.CurrentJob is HaulJob h && h.Pile == cell) return true;
            return false;
        }

        public override string ToString() => Name;
    }
}
