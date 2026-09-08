using System;
using System.Collections.Generic;

namespace VoxelBuild.Sim
{
    using VoxelBuild.Core;
    using VoxelBuild.World;

    /// <summary>A tree (or the upper part of one) that has just been cut loose.</summary>
    public sealed class TreeFall
    {
        /// <summary>Every block of the falling part, in world cells, with its type.</summary>
        public readonly List<(Int3 cell, BlockType type)> Blocks = new List<(Int3, BlockType)>();
        /// <summary>Lowest cell of the falling trunk; the tree hinges at its bottom edge.</summary>
        public Int3 Base;
        /// <summary>Horizontal unit direction the tree falls in.</summary>
        public Int3 Direction;
        /// <summary>Seconds the fall takes; logs appear on the ground when it ends.</summary>
        public float Duration = 1.4f;
        public int LogCount;
    }

    /// <summary>
    /// When a log is removed, any logs above it that no longer rest on the ground topple as one piece, together with
    /// the leaves attached to them. The logs land along the fall line as pick-up items once the fall finishes.
    /// </summary>
    public sealed class TreeFeller
    {
        private readonly GameContext ctx;
        private readonly List<(float time, Int3 cell, ItemStack stack)> pending = new List<(float, Int3, ItemStack)>();

        public const int MaxTreeBlocks = 1200;
        public const int MaxLeafDistance = 7;

        public event Action<TreeFall> Felled;

        public TreeFeller(GameContext ctx)
        {
            this.ctx = ctx;
        }

        public int PendingDrops => pending.Count;

        /// <summary>Call after a log at <paramref name="removed"/> has been taken out of the world.</summary>
        public TreeFall OnLogRemoved(Int3 removed, Int3 awayFrom)
        {
            var world = ctx.World;
            var above = removed + Int3.Up;
            if (world.GetBlock(above) != BlockType.Log) return null;

            var logs = CollectLogs(above);
            if (logs == null || logs.Count == 0) return null;
            if (IsSupported(logs)) return null;

            var leaves = CollectLeaves(logs);
            var fall = new TreeFall { Base = above, Direction = FallDirection(above, awayFrom), LogCount = logs.Count };
            foreach (var l in logs) fall.Blocks.Add((l, BlockType.Log));
            foreach (var l in leaves) fall.Blocks.Add((l, BlockType.Leaves));

            // Take the tree out of the world now; the renderer shows it falling and the logs land later.
            foreach (var (cell, _) in fall.Blocks) world.SetBlock(cell, BlockType.Air);
            ScheduleDrops(fall);
            Felled?.Invoke(fall);
            ctx.Log($"A tree with {logs.Count} logs came down");
            return fall;
        }

        /// <summary>Releases logs whose fall has finished.</summary>
        public void Tick()
        {
            if (pending.Count == 0) return;
            float now = ctx.Now;
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                var p = pending[i];
                if (p.time > now) continue;
                ctx.Items.AddNear(p.cell, p.stack.Type, p.stack.Count);
                pending.RemoveAt(i);
            }
        }

        private void ScheduleDrops(TreeFall fall)
        {
            float land = ctx.Now + fall.Duration;
            // A log i cells up the trunk lands i+1 cells out along the fall direction, at ground level.
            foreach (var (cell, type) in fall.Blocks)
            {
                if (type != BlockType.Log) continue;
                int height = cell.y - fall.Base.y;
                var lateral = new Int3(cell.x - fall.Base.x, 0, cell.z - fall.Base.z);
                var landing = fall.Base + fall.Direction * (height + 1) + lateral;
                landing = ctx.Items.FindDropCell(new Int3(landing.x, fall.Base.y, landing.z));
                pending.Add((land, landing, BlockRegistry.Get(BlockType.Log).Drop));
            }
        }

        private HashSet<Int3> CollectLogs(Int3 start)
        {
            var world = ctx.World;
            var set = new HashSet<Int3>();
            var queue = new Queue<Int3>();
            queue.Enqueue(start);
            set.Add(start);
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                if (set.Count > MaxTreeBlocks) return null; // not a tree, a log building: leave it alone
                for (int i = 0; i < 6; i++)
                {
                    var n = c + Directions.Offsets[i];
                    if (world.GetBlock(n) != BlockType.Log || set.Contains(n)) continue;
                    set.Add(n);
                    queue.Enqueue(n);
                }
            }
            return set;
        }

        /// <summary>A trunk stands if any of its logs rests on a solid block that is not part of the tree.</summary>
        private bool IsSupported(HashSet<Int3> logs)
        {
            var world = ctx.World;
            foreach (var l in logs)
            {
                var below = l + Int3.Down;
                if (logs.Contains(below)) continue;
                var t = world.GetBlock(below);
                if (t == BlockType.Leaves) continue;
                if (BlockRegistry.IsSolid(t)) return true;
            }
            return false;
        }

        private HashSet<Int3> CollectLeaves(HashSet<Int3> logs)
        {
            var world = ctx.World;
            var leaves = new HashSet<Int3>();
            var queue = new Queue<(Int3 cell, int dist)>();
            foreach (var l in logs)
                for (int i = 0; i < 6; i++)
                {
                    var n = l + Directions.Offsets[i];
                    if (world.GetBlock(n) == BlockType.Leaves && leaves.Add(n)) queue.Enqueue((n, 1));
                }
            while (queue.Count > 0)
            {
                var (c, dist) = queue.Dequeue();
                if (dist >= MaxLeafDistance || leaves.Count > MaxTreeBlocks) continue;
                for (int i = 0; i < 6; i++)
                {
                    var n = c + Directions.Offsets[i];
                    if (world.GetBlock(n) != BlockType.Leaves || leaves.Contains(n)) continue;
                    // Leaves touching another standing trunk belong to that tree.
                    if (TouchesOtherLog(n, logs)) continue;
                    leaves.Add(n);
                    queue.Enqueue((n, dist + 1));
                }
            }
            return leaves;
        }

        private bool TouchesOtherLog(Int3 leaf, HashSet<Int3> logs)
        {
            for (int i = 0; i < 6; i++)
            {
                var n = leaf + Directions.Offsets[i];
                if (ctx.World.GetBlock(n) == BlockType.Log && !logs.Contains(n)) return true;
            }
            return false;
        }

        private Int3 FallDirection(Int3 baseCell, Int3 awayFrom)
        {
            int dx = baseCell.x - awayFrom.x, dz = baseCell.z - awayFrom.z;
            if (dx == 0 && dz == 0)
            {
                int r = ctx.Random.Next(4);
                return Directions.Horizontal[r];
            }
            if (Math.Abs(dx) >= Math.Abs(dz)) return dx > 0 ? Int3.Right : Int3.Left;
            return dz > 0 ? Int3.Forward : Int3.Back;
        }
    }
}
