using System;
using System.Collections.Generic;

namespace VoxelBuild.World
{
    using VoxelBuild.Core;

    /// <summary>
    /// Procedural terrain: bedrock floor, stone body with ore veins and caves, soil cap with grass,
    /// sand near low ground, plus trees and berry bushes on the surface.
    /// </summary>
    public sealed class WorldGenerator
    {
        public int Seed = 1337;
        /// <summary>Average ground height in blocks.</summary>
        public float BaseHeight = 22f;
        /// <summary>Height variation in blocks.</summary>
        public float Amplitude = 12f;
        public float HillFrequency = 0.025f;
        public int SoilDepth = 4;
        public float TreeChance = 0.012f;
        public float BushChance = 0.006f;
        public bool Caves = true;

        public void Generate(VoxelWorld world)
        {
            var size = world.SizeInBlocks;
            int[] heights = new int[size.x * size.z];

            for (int z = 0; z < size.z; z++)
                for (int x = 0; x < size.x; x++)
                {
                    float h = Noise.Fbm2D(x, z, Seed, 4, HillFrequency);
                    float ridged = Noise.Fbm2D(x + 500, z + 500, Seed + 7, 2, HillFrequency * 0.5f);
                    float height = BaseHeight + (h - 0.5f) * 2f * Amplitude + (ridged - 0.5f) * Amplitude * 0.5f;
                    int hi = Clamp((int)Math.Round(height), 3, size.y - 8);
                    heights[z * size.x + x] = hi;
                }

            for (int z = 0; z < size.z; z++)
                for (int x = 0; x < size.x; x++)
                {
                    int h = heights[z * size.x + x];
                    bool lowland = h < BaseHeight - Amplitude * 0.45f;
                    for (int y = 0; y < size.y; y++)
                    {
                        BlockType t = BlockType.Air;
                        if (y == 0) t = BlockType.Bedrock;
                        else if (y < h - SoilDepth) t = StoneOrOre(x, y, z, h);
                        else if (y < h - 1) t = lowland && y >= h - 2 ? BlockType.Sand : BlockType.Dirt;
                        else if (y == h - 1) t = lowland ? BlockType.Sand : BlockType.Grass;

                        if (t == BlockType.Grass || t == BlockType.Dirt)
                        {
                            float patch = Noise.Fbm2D(x, z, Seed + 99, 2, 0.15f);
                            if (patch > 0.78f) t = t == BlockType.Grass ? BlockType.Gravel : BlockType.Clay;
                        }

                        if (Caves && t != BlockType.Air && t != BlockType.Bedrock && y > 2 && y < h - 3)
                        {
                            float cave = Noise.Fbm3D(x, y * 1.6f, z, Seed + 31, 2, 0.09f);
                            if (cave > 0.71f) t = BlockType.Air;
                        }

                        if (t != BlockType.Air) world.SetBlockRaw(new Int3(x, y, z), t);
                    }
                }

            PlaceVegetation(world, heights);
        }

        private BlockType StoneOrOre(int x, int y, int z, int surface)
        {
            float coal = Noise.Fbm3D(x, y, z, Seed + 11, 2, 0.13f);
            if (coal > 0.72f) return BlockType.CoalOre;
            float iron = Noise.Fbm3D(x + 77, y, z - 77, Seed + 13, 2, 0.15f);
            if (iron > 0.745f && y < surface - 8) return BlockType.IronOre;
            float gold = Noise.Fbm3D(x - 33, y + 33, z, Seed + 17, 2, 0.17f);
            if (gold > 0.78f && y < surface - 14) return BlockType.GoldOre;
            float crystal = Noise.Fbm3D(x + 200, y - 50, z + 200, Seed + 19, 2, 0.19f);
            if (crystal > 0.79f && y < 10) return BlockType.Crystal;
            return BlockType.Stone;
        }

        private void PlaceVegetation(VoxelWorld world, int[] heights)
        {
            var size = world.SizeInBlocks;
            for (int z = 2; z < size.z - 2; z++)
                for (int x = 2; x < size.x - 2; x++)
                {
                    int h = heights[z * size.x + x];
                    var ground = new Int3(x, h - 1, z);
                    if (world.GetBlock(ground) != BlockType.Grass) continue;
                    if (!world.IsAir(ground + Int3.Up)) continue;

                    float r = Noise.Hash(x, z, Seed + 41);
                    if (r < TreeChance)
                    {
                        PlaceTree(world, ground + Int3.Up, x, z);
                    }
                    else if (r < TreeChance + BushChance)
                    {
                        world.SetBlockRaw(ground + Int3.Up, BlockType.BerryBush);
                    }
                }
        }

        private void PlaceTree(VoxelWorld world, Int3 basePos, int x, int z)
        {
            int trunk = 5 + (int)(Noise.Hash(x, z, Seed + 43) * 4f);
            // Keep trees off each other: skip if a log is within 2 blocks horizontally.
            for (int dz = -2; dz <= 2; dz++)
                for (int dx = -2; dx <= 2; dx++)
                    for (int dy = 0; dy < trunk + 3; dy++)
                        if (world.GetBlock(basePos + new Int3(dx, dy, dz)) == BlockType.Log) return;

            for (int i = 0; i < trunk; i++)
                world.SetBlockRaw(basePos + new Int3(0, i, 0), BlockType.Log);

            int top = basePos.y + trunk;
            int radius = 2 + (trunk > 6 ? 1 : 0);
            for (int dy = -2; dy <= 2; dy++)
                for (int dz = -radius; dz <= radius; dz++)
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        float d = (float)Math.Sqrt(dx * dx + dz * dz + dy * dy * 1.6f);
                        if (d > radius + 0.3f) continue;
                        var p = new Int3(basePos.x + dx, top + dy, basePos.z + dz);
                        if (world.GetBlock(p) == BlockType.Air) world.SetBlockRaw(p, BlockType.Leaves);
                    }
        }

        /// <summary>Finds a flat-ish grass cell near the world centre to spawn colonists on. Returns the feet cell.</summary>
        public static Int3 FindSpawn(VoxelWorld world)
        {
            var size = world.SizeInBlocks;
            int cx = size.x / 2, cz = size.z / 2;
            Int3 best = new Int3(cx, world.SurfaceY(cx, cz) + 1, cz);
            for (int r = 0; r < Math.Min(size.x, size.z) / 2 - 2; r++)
            {
                for (int dz = -r; dz <= r; dz++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Math.Abs(dx) != r && Math.Abs(dz) != r) continue;
                        int x = cx + dx, z = cz + dz;
                        int y = world.SurfaceY(x, z);
                        if (y < 0) continue;
                        var ground = new Int3(x, y, z);
                        if (world.GetBlock(ground) != BlockType.Grass && world.GetBlock(ground) != BlockType.Sand) continue;
                        bool clear = true;
                        for (int i = 1; i <= 4 && clear; i++)
                            if (!world.IsAir(ground + new Int3(0, i, 0))) clear = false;
                        if (clear) return ground + Int3.Up;
                    }
            }
            return best;
        }

        private static int Clamp(int v, int min, int max) => v < min ? min : (v > max ? max : v);
    }
}
