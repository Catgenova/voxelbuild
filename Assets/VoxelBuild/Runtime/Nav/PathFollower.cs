using System;
using System.Collections.Generic;

namespace VoxelBuild.Nav
{
    using VoxelBuild.Core;
    using VoxelBuild.World;

    /// <summary>Walks a cell path with smooth interpolation. Positions are in cell units (feet at the cell floor).</summary>
    public sealed class PathFollower
    {
        private readonly List<Int3> path = new List<Int3>();
        private int nextIndex;

        public float SpeedCellsPerSecond = 3.6f;

        public float PosX, PosY, PosZ;
        /// <summary>The cell the feet are logically in.</summary>
        public Int3 Cell { get; private set; }

        public bool HasPath => nextIndex < path.Count;
        public IReadOnlyList<Int3> Path => path;
        public Int3 Destination => path.Count > 0 ? path[path.Count - 1] : Cell;

        /// <summary>Normalised facing direction on the XZ plane, for the view.</summary>
        public float FacingX = 0f, FacingZ = 1f;

        public void Teleport(Int3 cell)
        {
            Cell = cell;
            PosX = cell.x + 0.5f;
            PosY = cell.y;
            PosZ = cell.z + 0.5f;
            path.Clear();
            nextIndex = 0;
        }

        public void SetPath(List<Int3> newPath)
        {
            path.Clear();
            path.AddRange(newPath);
            nextIndex = 0;
            // Skip the start cell if we are already standing in it.
            if (path.Count > 0 && path[0] == Cell) nextIndex = 1;
        }

        public void Stop()
        {
            path.Clear();
            nextIndex = 0;
            SnapToCell();
        }

        private void SnapToCell()
        {
            PosX = Cell.x + 0.5f;
            PosY = Cell.y;
            PosZ = Cell.z + 0.5f;
        }

        /// <summary>
        /// Advances along the path. Returns false if the next waypoint is no longer standable (caller should re-plan).
        /// </summary>
        public bool Tick(float dt, IBlockQuery world)
        {
            if (!HasPath) return true;
            float budget = SpeedCellsPerSecond * dt;
            while (budget > 0f && HasPath)
            {
                var target = path[nextIndex];
                if (!NavRules.IsStandable(world, target))
                {
                    Stop();
                    return false;
                }
                float tx = target.x + 0.5f, ty = target.y, tz = target.z + 0.5f;
                float dx = tx - PosX, dy = ty - PosY, dz = tz - PosZ;
                float dist = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
                if (dist > 0.0001f)
                {
                    float hx = dx, hz = dz;
                    float hl = (float)Math.Sqrt(hx * hx + hz * hz);
                    if (hl > 0.001f)
                    {
                        FacingX = hx / hl;
                        FacingZ = hz / hl;
                    }
                }
                if (dist <= budget)
                {
                    PosX = tx; PosY = ty; PosZ = tz;
                    Cell = target;
                    nextIndex++;
                    budget -= dist;
                }
                else
                {
                    float f = budget / dist;
                    PosX += dx * f; PosY += dy * f; PosZ += dz * f;
                    // Claim the target cell once we are more than halfway there so reach checks feel responsive.
                    if (dist < 0.5f) Cell = target;
                    budget = 0f;
                }
            }
            return true;
        }
    }
}
