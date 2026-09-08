using System;

namespace VoxelBuild.Core
{
    /// <summary>Integer 3D vector used for block coordinates. Engine independent.</summary>
    [Serializable]
    public struct Int3 : IEquatable<Int3>
    {
        public int x;
        public int y;
        public int z;

        public Int3(int x, int y, int z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static readonly Int3 Zero = new Int3(0, 0, 0);
        public static readonly Int3 One = new Int3(1, 1, 1);
        public static readonly Int3 Up = new Int3(0, 1, 0);
        public static readonly Int3 Down = new Int3(0, -1, 0);
        public static readonly Int3 Right = new Int3(1, 0, 0);
        public static readonly Int3 Left = new Int3(-1, 0, 0);
        public static readonly Int3 Forward = new Int3(0, 0, 1);
        public static readonly Int3 Back = new Int3(0, 0, -1);

        public static Int3 operator +(Int3 a, Int3 b) => new Int3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Int3 operator -(Int3 a, Int3 b) => new Int3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Int3 operator -(Int3 a) => new Int3(-a.x, -a.y, -a.z);
        public static Int3 operator *(Int3 a, int s) => new Int3(a.x * s, a.y * s, a.z * s);
        public static bool operator ==(Int3 a, Int3 b) => a.x == b.x && a.y == b.y && a.z == b.z;
        public static bool operator !=(Int3 a, Int3 b) => !(a == b);

        public bool Equals(Int3 other) => this == other;
        public override bool Equals(object obj) => obj is Int3 other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = x * 73856093;
                h ^= y * 19349663;
                h ^= z * 83492791;
                return h;
            }
        }

        public override string ToString() => $"({x}, {y}, {z})";

        public int ManhattanDistance(Int3 other) => Math.Abs(x - other.x) + Math.Abs(y - other.y) + Math.Abs(z - other.z);

        public int ChebyshevDistance(Int3 other) =>
            Math.Max(Math.Abs(x - other.x), Math.Max(Math.Abs(y - other.y), Math.Abs(z - other.z)));

        /// <summary>Horizontal Chebyshev distance (ignores Y).</summary>
        public int HorizontalDistance(Int3 other) => Math.Max(Math.Abs(x - other.x), Math.Abs(z - other.z));

        public float EuclideanDistance(Int3 other)
        {
            float dx = x - other.x, dy = y - other.y, dz = z - other.z;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public static Int3 Min(Int3 a, Int3 b) => new Int3(Math.Min(a.x, b.x), Math.Min(a.y, b.y), Math.Min(a.z, b.z));
        public static Int3 Max(Int3 a, Int3 b) => new Int3(Math.Max(a.x, b.x), Math.Max(a.y, b.y), Math.Max(a.z, b.z));

        public static Int3 FloorDiv(Int3 a, int d) => new Int3(FloorDiv(a.x, d), FloorDiv(a.y, d), FloorDiv(a.z, d));

        public static int FloorDiv(int a, int d)
        {
            int q = a / d;
            if ((a % d != 0) && ((a < 0) != (d < 0))) q--;
            return q;
        }

        public static int FloorMod(int a, int d)
        {
            int m = a % d;
            return m < 0 ? m + d : m;
        }
    }

    /// <summary>Axis-aligned integer box, inclusive on both ends.</summary>
    public struct IntBox
    {
        public Int3 Min;
        public Int3 Max;

        public IntBox(Int3 a, Int3 b)
        {
            Min = Int3.Min(a, b);
            Max = Int3.Max(a, b);
        }

        public bool Contains(Int3 p) =>
            p.x >= Min.x && p.x <= Max.x && p.y >= Min.y && p.y <= Max.y && p.z >= Min.z && p.z <= Max.z;

        public int Volume => (Max.x - Min.x + 1) * (Max.y - Min.y + 1) * (Max.z - Min.z + 1);

        public Int3 Size => new Int3(Max.x - Min.x + 1, Max.y - Min.y + 1, Max.z - Min.z + 1);

        public System.Collections.Generic.IEnumerable<Int3> Cells()
        {
            for (int y = Min.y; y <= Max.y; y++)
                for (int z = Min.z; z <= Max.z; z++)
                    for (int x = Min.x; x <= Max.x; x++)
                        yield return new Int3(x, y, z);
        }
    }
}
