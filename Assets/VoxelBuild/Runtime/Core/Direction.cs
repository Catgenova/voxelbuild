namespace VoxelBuild.Core
{
    /// <summary>The six block face directions.</summary>
    public enum Direction
    {
        PosX = 0,
        NegX = 1,
        PosY = 2,
        NegY = 3,
        PosZ = 4,
        NegZ = 5,
    }

    public static class Directions
    {
        public static readonly Direction[] All =
        {
            Direction.PosX, Direction.NegX, Direction.PosY, Direction.NegY, Direction.PosZ, Direction.NegZ,
        };

        public static readonly Int3[] Offsets =
        {
            new Int3(1, 0, 0),
            new Int3(-1, 0, 0),
            new Int3(0, 1, 0),
            new Int3(0, -1, 0),
            new Int3(0, 0, 1),
            new Int3(0, 0, -1),
        };

        /// <summary>The four horizontal (cardinal) offsets.</summary>
        public static readonly Int3[] Horizontal =
        {
            new Int3(1, 0, 0),
            new Int3(-1, 0, 0),
            new Int3(0, 0, 1),
            new Int3(0, 0, -1),
        };

        /// <summary>The eight horizontal offsets including diagonals.</summary>
        public static readonly Int3[] Horizontal8 =
        {
            new Int3(1, 0, 0),
            new Int3(-1, 0, 0),
            new Int3(0, 0, 1),
            new Int3(0, 0, -1),
            new Int3(1, 0, 1),
            new Int3(1, 0, -1),
            new Int3(-1, 0, 1),
            new Int3(-1, 0, -1),
        };

        public static Int3 Offset(Direction d) => Offsets[(int)d];

        public static Direction Opposite(Direction d)
        {
            switch (d)
            {
                case Direction.PosX: return Direction.NegX;
                case Direction.NegX: return Direction.PosX;
                case Direction.PosY: return Direction.NegY;
                case Direction.NegY: return Direction.PosY;
                case Direction.PosZ: return Direction.NegZ;
                default: return Direction.PosZ;
            }
        }
    }
}
