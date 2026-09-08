using System;

namespace VoxelBuild.World
{
    /// <summary>Deterministic value noise with fractal layering. No engine dependency so generation is testable.</summary>
    public static class Noise
    {
        public static uint HashU(int x, int y, int z, int seed)
        {
            unchecked
            {
                uint h = (uint)seed * 0x9E3779B1u;
                h ^= (uint)x * 0x85EBCA6Bu;
                h = (h << 13) | (h >> 19);
                h ^= (uint)y * 0xC2B2AE35u;
                h = (h << 17) | (h >> 15);
                h ^= (uint)z * 0x27D4EB2Fu;
                h *= 0x165667B1u;
                h ^= h >> 15;
                h *= 0x2C1B3C6Du;
                h ^= h >> 12;
                return h;
            }
        }

        /// <summary>Uniform hash in [0,1).</summary>
        public static float Hash(int x, int y, int z, int seed) => (HashU(x, y, z, seed) & 0xFFFFFF) / 16777216f;

        public static float Hash(int x, int z, int seed) => Hash(x, 0, z, seed);

        private static float Smooth(float t) => t * t * (3f - 2f * t);

        public static float Value2D(float x, float z, int seed)
        {
            int x0 = FloorToInt(x), z0 = FloorToInt(z);
            float tx = Smooth(x - x0), tz = Smooth(z - z0);
            float a = Hash(x0, z0, seed), b = Hash(x0 + 1, z0, seed);
            float c = Hash(x0, z0 + 1, seed), d = Hash(x0 + 1, z0 + 1, seed);
            float top = a + (b - a) * tx;
            float bottom = c + (d - c) * tx;
            return top + (bottom - top) * tz;
        }

        public static float Value3D(float x, float y, float z, int seed)
        {
            int x0 = FloorToInt(x), y0 = FloorToInt(y), z0 = FloorToInt(z);
            float tx = Smooth(x - x0), ty = Smooth(y - y0), tz = Smooth(z - z0);
            float c000 = Hash(x0, y0, z0, seed), c100 = Hash(x0 + 1, y0, z0, seed);
            float c010 = Hash(x0, y0 + 1, z0, seed), c110 = Hash(x0 + 1, y0 + 1, z0, seed);
            float c001 = Hash(x0, y0, z0 + 1, seed), c101 = Hash(x0 + 1, y0, z0 + 1, seed);
            float c011 = Hash(x0, y0 + 1, z0 + 1, seed), c111 = Hash(x0 + 1, y0 + 1, z0 + 1, seed);
            float x00 = c000 + (c100 - c000) * tx, x10 = c010 + (c110 - c010) * tx;
            float x01 = c001 + (c101 - c001) * tx, x11 = c011 + (c111 - c011) * tx;
            float y0v = x00 + (x10 - x00) * ty, y1v = x01 + (x11 - x01) * ty;
            return y0v + (y1v - y0v) * tz;
        }

        /// <summary>Fractal 2D noise in [0,1].</summary>
        public static float Fbm2D(float x, float z, int seed, int octaves, float frequency, float persistence = 0.5f)
        {
            float sum = 0f, amp = 1f, norm = 0f, freq = frequency;
            for (int i = 0; i < octaves; i++)
            {
                sum += Value2D(x * freq, z * freq, seed + i * 101) * amp;
                norm += amp;
                amp *= persistence;
                freq *= 2f;
            }
            return sum / norm;
        }

        /// <summary>Fractal 3D noise in [0,1].</summary>
        public static float Fbm3D(float x, float y, float z, int seed, int octaves, float frequency, float persistence = 0.5f)
        {
            float sum = 0f, amp = 1f, norm = 0f, freq = frequency;
            for (int i = 0; i < octaves; i++)
            {
                sum += Value3D(x * freq, y * freq, z * freq, seed + i * 131) * amp;
                norm += amp;
                amp *= persistence;
                freq *= 2f;
            }
            return sum / norm;
        }

        private static int FloorToInt(float v)
        {
            int i = (int)v;
            return v < i ? i - 1 : i;
        }
    }
}
