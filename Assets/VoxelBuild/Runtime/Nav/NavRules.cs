namespace VoxelBuild.Nav
{
    using VoxelBuild.Core;
    using VoxelBuild.World;

    /// <summary>
    /// Movement rules for colonists on the block grid. A "cell" is where the feet are; the colonist occupies
    /// <see cref="Clearance"/> cells upward from there.
    /// </summary>
    public static class NavRules
    {
        /// <summary>Vertical cells a colonist needs free (feet cell included).</summary>
        public const int Clearance = Scale.ColonistClearance;
        /// <summary>Maximum cells a colonist can step up in one move.</summary>
        public const int StepUp = Scale.StepUp;
        /// <summary>Maximum cells a colonist will drop without a path being rejected.</summary>
        public const int MaxFall = Scale.MaxFall;
        /// <summary>Horizontal reach (Chebyshev) for mining/building from a standing cell.</summary>
        public const int ReachHorizontal = Scale.Reach;
        public const int ReachDown = Scale.ReachDown;
        public const int ReachUp = Scale.ReachUp;
        /// <summary>Water depth (cells) a colonist can wade through; deeper water is impassable.</summary>
        public const int WadeDepth = 2;

        public static bool IsPassable(IBlockQuery world, Int3 cell)
        {
            if (!world.InBounds(cell)) return false;
            return !world.IsSolid(cell);
        }

        /// <summary>True if the column from the cell upward is clear for a colonist. Water is allowed up to wading depth.</summary>
        public static bool HasClearance(IBlockQuery world, Int3 feet)
        {
            for (int i = 0; i < Clearance; i++)
            {
                var c = feet + new Int3(0, i, 0);
                if (!world.InBounds(c)) return i > 0; // allow heads poking above the world ceiling
                var t = world.GetBlock(c);
                if (BlockRegistry.IsSolid(t)) return false;
                if (i >= WadeDepth && BlockRegistry.IsFluid(t)) return false;
            }
            return true;
        }

        /// <summary>
        /// A cell is standable if it has clearance and rests on a solid block, or on shallow water that itself
        /// rests on a solid block (wading).
        /// </summary>
        public static bool IsStandable(IBlockQuery world, Int3 feet)
        {
            if (!world.InBounds(feet)) return false;
            if (!HasClearance(world, feet)) return false;
            var below = feet + Int3.Down;
            if (!world.InBounds(below)) return false;
            if (world.IsSolid(below)) return true;
            if (WadeDepth >= 2 && BlockRegistry.IsFluid(world.GetBlock(below)))
            {
                var bed = below + Int3.Down;
                return world.InBounds(bed) && world.IsSolid(bed);
            }
            return false;
        }

        public static bool IsInWater(IBlockQuery world, Int3 feet) => BlockRegistry.IsFluid(world.GetBlock(feet));

        /// <summary>Whether a colonist standing at <paramref name="feet"/> can work on <paramref name="target"/>.</summary>
        public static bool CanReach(Int3 feet, Int3 target)
        {
            int dy = target.y - feet.y;
            if (dy < -ReachDown || dy > ReachUp) return false;
            return feet.HorizontalDistance(target) <= ReachHorizontal;
        }

        /// <summary>Finds the nearest standable cell at or below a position (used when a colonist's floor vanishes).</summary>
        public static bool FindGround(IBlockQuery world, Int3 from, int maxDrop, out Int3 result)
        {
            for (int d = 0; d <= maxDrop; d++)
            {
                var c = from + new Int3(0, -d, 0);
                if (!world.InBounds(c)) break;
                if (world.IsSolid(c)) break;
                if (IsStandable(world, c))
                {
                    result = c;
                    return true;
                }
            }
            result = from;
            return false;
        }
    }
}
