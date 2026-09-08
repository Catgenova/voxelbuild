using System;
using System.Collections.Generic;

namespace VoxelBuild.Nav
{
    using VoxelBuild.Core;
    using VoxelBuild.World;

    /// <summary>
    /// A* over standable cells. Supports arbitrary goal predicates so callers can ask for "any cell from which
    /// block X can be reached" instead of a single destination.
    /// </summary>
    public sealed class Pathfinder
    {
        private readonly IBlockQuery world;

        public int MaxExpansions = 40000;

        private struct Record
        {
            public float G;
            public Int3 Parent;
            public bool HasParent;
            public bool Closed;
        }

        private readonly Dictionary<Int3, Record> records = new Dictionary<Int3, Record>();
        private readonly MinHeap open = new MinHeap();
        private readonly List<Int3> neighbourBuffer = new List<Int3>(16);
        private readonly List<float> costBuffer = new List<float>(16);

        public int LastExpansions { get; private set; }

        public Pathfinder(IBlockQuery world)
        {
            this.world = world;
        }

        public bool FindPathTo(Int3 start, Int3 goal, List<Int3> path) =>
            FindPath(start, c => c == goal, goal, path);

        /// <summary>Path to any standable cell from which <paramref name="target"/> can be worked on.</summary>
        public bool FindPathToReach(Int3 start, Int3 target, List<Int3> path) =>
            FindPath(start, c => NavRules.CanReach(c, target), target, path);

        /// <summary>Path to any standable cell adjacent to (or on) <paramref name="target"/>.</summary>
        public bool FindPathAdjacent(Int3 start, Int3 target, List<Int3> path) =>
            FindPath(start, c => c.HorizontalDistance(target) <= NavRules.ReachHorizontal && Math.Abs(c.y - target.y) <= NavRules.ReachDown, target, path);

        /// <summary>
        /// General A*. <paramref name="path"/> receives the cells from start (inclusive) to goal (inclusive).
        /// Returns false if no path exists within the expansion budget.
        /// </summary>
        public bool FindPath(Int3 start, Func<Int3, bool> isGoal, Int3 heuristicTarget, List<Int3> path)
        {
            path.Clear();
            records.Clear();
            open.Clear();
            LastExpansions = 0;

            if (!NavRules.IsStandable(world, start))
            {
                if (!NavRules.FindGround(world, start, NavRules.MaxFall, out start)) return false;
            }

            if (isGoal(start))
            {
                path.Add(start);
                return true;
            }

            records[start] = new Record { G = 0f };
            open.Push(start, Heuristic(start, heuristicTarget));

            while (open.Count > 0)
            {
                var current = open.Pop();
                var rec = records[current];
                if (rec.Closed) continue;
                rec.Closed = true;
                records[current] = rec;

                if (isGoal(current))
                {
                    Reconstruct(current, path);
                    return true;
                }

                if (++LastExpansions > MaxExpansions) return false;

                GetNeighbours(current, neighbourBuffer, costBuffer);
                for (int i = 0; i < neighbourBuffer.Count; i++)
                {
                    var n = neighbourBuffer[i];
                    float g = rec.G + costBuffer[i];
                    if (records.TryGetValue(n, out var nr))
                    {
                        if (nr.Closed || g >= nr.G) continue;
                    }
                    records[n] = new Record { G = g, Parent = current, HasParent = true };
                    open.Push(n, g + Heuristic(n, heuristicTarget));
                }
            }
            return false;
        }

        private void Reconstruct(Int3 end, List<Int3> path)
        {
            var c = end;
            path.Add(c);
            while (records.TryGetValue(c, out var r) && r.HasParent)
            {
                c = r.Parent;
                path.Add(c);
            }
            path.Reverse();
        }

        private static float Heuristic(Int3 a, Int3 b)
        {
            int dx = Math.Abs(a.x - b.x), dz = Math.Abs(a.z - b.z), dy = Math.Abs(a.y - b.y);
            int dmin = Math.Min(dx, dz), dmax = Math.Max(dx, dz);
            return dmax + 0.4142f * dmin + dy * 0.5f;
        }

        /// <summary>Enumerates legal moves from a standing cell: flat walks, diagonal walks, single steps up, and drops.</summary>
        public void GetNeighbours(Int3 cur, List<Int3> outCells, List<float> outCosts)
        {
            outCells.Clear();
            outCosts.Clear();

            // Cardinal moves: flat, step up, or drop.
            for (int i = 0; i < 4; i++)
            {
                var off = Directions.Horizontal[i];
                var h = cur + off;
                if (!world.InBounds(h)) continue;

                if (NavRules.IsStandable(world, h))
                {
                    Add(outCells, outCosts, h, 1f);
                    continue;
                }

                if (world.IsSolid(h))
                {
                    // Step up one or more cells: the column above the current cell must stay clear while rising,
                    // and the landing cell must be standable.
                    for (int k = 1; k <= NavRules.StepUp; k++)
                    {
                        var headRoom = cur + new Int3(0, NavRules.Clearance + k - 1, 0);
                        if (world.InBounds(headRoom) && world.IsSolid(headRoom)) break;
                        var up = h + new Int3(0, k, 0);
                        if (NavRules.IsStandable(world, up))
                        {
                            Add(outCells, outCosts, up, 1f + 0.6f * k);
                            break;
                        }
                    }
                    continue;
                }

                // Air with nothing to stand on: drop down.
                if (NavRules.HasClearance(world, h))
                {
                    for (int d = 1; d <= NavRules.MaxFall; d++)
                    {
                        var below = h + new Int3(0, -d, 0);
                        if (!world.InBounds(below) || world.IsSolid(below)) break;
                        if (NavRules.IsStandable(world, below))
                        {
                            Add(outCells, outCosts, below, 1f + d * 0.6f);
                            break;
                        }
                    }
                }
            }

            // Diagonal moves on the same level only, and only if both corners are open (no corner cutting).
            for (int i = 4; i < 8; i++)
            {
                var off = Directions.Horizontal8[i];
                var h = cur + off;
                if (!NavRules.IsStandable(world, h)) continue;
                var a = cur + new Int3(off.x, 0, 0);
                var b = cur + new Int3(0, 0, off.z);
                if (!NavRules.HasClearance(world, a) || !NavRules.HasClearance(world, b)) continue;
                Add(outCells, outCosts, h, 1.4142f);
            }
        }

        private static void Add(List<Int3> cells, List<float> costs, Int3 c, float cost)
        {
            cells.Add(c);
            costs.Add(cost);
        }

        /// <summary>Binary min-heap keyed on float priority.</summary>
        private sealed class MinHeap
        {
            private readonly List<Int3> items = new List<Int3>();
            private readonly List<float> keys = new List<float>();

            public int Count => items.Count;

            public void Clear()
            {
                items.Clear();
                keys.Clear();
            }

            public void Push(Int3 item, float key)
            {
                items.Add(item);
                keys.Add(key);
                int i = items.Count - 1;
                while (i > 0)
                {
                    int p = (i - 1) / 2;
                    if (keys[p] <= keys[i]) break;
                    Swap(i, p);
                    i = p;
                }
            }

            public Int3 Pop()
            {
                var top = items[0];
                int last = items.Count - 1;
                items[0] = items[last];
                keys[0] = keys[last];
                items.RemoveAt(last);
                keys.RemoveAt(last);
                int i = 0;
                int n = items.Count;
                while (true)
                {
                    int l = 2 * i + 1, r = l + 1, s = i;
                    if (l < n && keys[l] < keys[s]) s = l;
                    if (r < n && keys[r] < keys[s]) s = r;
                    if (s == i) break;
                    Swap(i, s);
                    i = s;
                }
                return top;
            }

            private void Swap(int a, int b)
            {
                (items[a], items[b]) = (items[b], items[a]);
                (keys[a], keys[b]) = (keys[b], keys[a]);
            }
        }
    }
}
