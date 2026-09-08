using System;
using System.Collections.Generic;

namespace VoxelBuild.World
{
    using VoxelBuild.Core;

    /// <summary>
    /// Procedural terrain: bedrock floor, stone body with ore veins and caves, soil cap with grass,
    /// sand near low ground, plus trees and berry bushes on the surface.
    /// All tunables are in metres so the look survives changes to the block size.
    /// </summary>
    public sealed class WorldGenerator
    {
        public int Seed = 1337;
        /// <summary>Average ground height in metres.</summary>
        public float BaseHeightMetres = 11f;
        /// <summary>Height variation in metres.</summary>
        public float AmplitudeMetres = 6f;
        /// <summary>Hill noise frequency per metre.</summary>
        public float HillFrequency = 0.05f;
        public float SoilDepthMetres = 1f;
        /// <summary>Trees per square metre of grass.</summary>
        public float TreesPerSquareMetre = 0.05f;
        public float BushesPerSquareMetre = 0.025f;
        public bool Caves = true;

        /// <summary>Ore and cave noise is evaluated on a coarser grid (in blocks) to keep generation fast.</summary>
        private const int CoarseCell = 2;

        private enum Vein : byte { None, Coal, Iron, Gold, Crystal }

        public void Generate(VoxelWorld world)
        {
            var size = world.SizeInBlocks;
            float s = Scale.BlocksPerMetre;
            int[] heights = new int[size.x * size.z];

            for (int z = 0; z < size.z; z++)
                for (int x = 0; x < size.x; x++)
                {
                    float mx = x / s, mz = z / s;
                    float h = Noise.Fbm2D(mx, mz, Seed, 4, HillFrequency);
                    float ridged = Noise.Fbm2D(mx + 500f, mz + 500f, Seed + 7, 2, HillFrequency * 0.5f);
                    float metres = BaseHeightMetres + (h - 0.5f) * 2f * AmplitudeMetres + (ridged - 0.5f) * AmplitudeMetres * 0.5f;
                    int hi = Clamp((int)Math.Round(metres * s), 3, size.y - Scale.ColonistClearance - 1);
                    heights[z * size.x + x] = hi;
                }

            // Coarse ore / cave field.
            int cx = (size.x + CoarseCell - 1) / CoarseCell;
            int cy = (size.y + CoarseCell - 1) / CoarseCell;
            int cz = (size.z + CoarseCell - 1) / CoarseCell;
            var veins = new Vein[cx * cy * cz];
            var caves = new bool[cx * cy * cz];
            for (int j = 0; j < cy; j++)
                for (int k = 0; k < cz; k++)
                    for (int i = 0; i < cx; i++)
                    {
                        float mx = i * CoarseCell / s, my = j * CoarseCell / s, mz = k * CoarseCell / s;
                        int idx = (j * cz + k) * cx + i;
                        veins[idx] = VeinAt(mx, my, mz);
                        if (Caves) caves[idx] = Noise.Fbm3D(mx, my * 1.6f, mz, Seed + 31, 2, 0.18f) > 0.71f;
                    }

            int soil = Math.Max(1, Scale.Metres(SoilDepthMetres));
            int lowlandBelow = (int)((BaseHeightMetres - AmplitudeMetres * 0.45f) * s);
            int ironDepth = Scale.Metres(4f), goldDepth = Scale.Metres(7f), crystalBelow = Scale.Metres(5f);
            int caveMargin = Scale.Metres(1.5f);

            for (int z = 0; z < size.z; z++)
                for (int x = 0; x < size.x; x++)
                {
                    int h = heights[z * size.x + x];
                    bool lowland = h < lowlandBelow;
                    float patch = Noise.Fbm2D(x / s, z / s, Seed + 99, 2, 0.3f);
                    for (int y = 0; y < h; y++)
                    {
                        BlockType t;
                        if (y == 0) t = BlockType.Bedrock;
                        else if (y < h - soil)
                        {
                            int idx = ((y / CoarseCell) * cz + z / CoarseCell) * cx + x / CoarseCell;
                            t = BlockType.Stone;
                            switch (veins[idx])
                            {
                                case Vein.Coal: t = BlockType.CoalOre; break;
                                case Vein.Iron: if (y < h - ironDepth) t = BlockType.IronOre; break;
                                case Vein.Gold: if (y < h - goldDepth) t = BlockType.GoldOre; break;
                                case Vein.Crystal: if (y < crystalBelow) t = BlockType.Crystal; break;
                            }
                            if (caves[idx] && y > 2 && y < h - soil - caveMargin) continue; // air
                        }
                        else if (y < h - 1) t = lowland && y >= h - 2 ? BlockType.Sand : BlockType.Dirt;
                        else t = lowland ? BlockType.Sand : BlockType.Grass;

                        if (patch > 0.78f)
                        {
                            if (t == BlockType.Grass) t = BlockType.Gravel;
                            else if (t == BlockType.Dirt) t = BlockType.Clay;
                        }

                        world.SetBlockRaw(new Int3(x, y, z), t);
                    }
                }

            PlaceVegetation(world, heights);
        }

        private Vein VeinAt(float mx, float my, float mz)
        {
            if (Noise.Fbm3D(mx, my, mz, Seed + 11, 2, 0.26f) > 0.72f) return Vein.Coal;
            if (Noise.Fbm3D(mx + 77f, my, mz - 77f, Seed + 13, 2, 0.30f) > 0.745f) return Vein.Iron;
            if (Noise.Fbm3D(mx - 33f, my + 33f, mz, Seed + 17, 2, 0.34f) > 0.78f) return Vein.Gold;
            if (Noise.Fbm3D(mx + 200f, my - 50f, mz + 200f, Seed + 19, 2, 0.38f) > 0.79f) return Vein.Crystal;
            return Vein.None;
        }

        private void PlaceVegetation(VoxelWorld world, int[] heights)
        {
            var size = world.SizeInBlocks;
            float s = Scale.BlocksPerMetre;
            float columnArea = 1f / (s * s);
            float treeChance = TreesPerSquareMetre * columnArea;
            float bushChance = BushesPerSquareMetre * columnArea;
            int margin = Scale.Metres(1.5f);

            for (int z = margin; z < size.z - margin; z++)
                for (int x = margin; x < size.x - margin; x++)
                {
                    int h = heights[z * size.x + x];
                    var ground = new Int3(x, h - 1, z);
                    if (world.GetBlock(ground) != BlockType.Grass) continue;
                    if (!world.IsAir(ground + Int3.Up)) continue;

                    float r = Noise.Hash(x, z, Seed + 41);
                    if (r < treeChance) PlaceTree(world, ground + Int3.Up, x, z);
                    else if (r < treeChance + bushChance) PlaceBush(world, ground + Int3.Up);
                }
        }

        private void PlaceBush(VoxelWorld world, Int3 basePos)
        {
            // A bush is roughly half a metre across and high.
            int r = Math.Max(0, Scale.Metres(0.5f) / 2 - 1);
            int height = Math.Max(1, Scale.Metres(0.5f));
            for (int dy = 0; dy < height; dy++)
                for (int dz = -r; dz <= r; dz++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        var p = basePos + new Int3(dx, dy, dz);
                        if (world.GetBlock(p) == BlockType.Air) world.SetBlockRaw(p, BlockType.BerryBush);
                    }
        }

        private void PlaceTree(VoxelWorld world, Int3 basePos, int x, int z)
        {
            float s = Scale.BlocksPerMetre;
            int trunk = Scale.Metres(2.5f + Noise.Hash(x, z, Seed + 43) * 2f);
            int spacing = Scale.Metres(1.5f);
            // Keep trees off each other.
            for (int dz = -spacing; dz <= spacing; dz++)
                for (int dx = -spacing; dx <= spacing; dx++)
                    for (int dy = 0; dy < trunk + 3; dy += 2)
                        if (world.GetBlock(basePos + new Int3(dx, dy, dz)) == BlockType.Log) return;

            int trunkRadius = Math.Max(0, Scale.Metres(0.3f) / 2);
            for (int i = 0; i < trunk; i++)
                for (int dz = -trunkRadius; dz <= trunkRadius; dz++)
                    for (int dx = -trunkRadius; dx <= trunkRadius; dx++)
                        world.SetBlockRaw(basePos + new Int3(dx, i, dz), BlockType.Log);

            int top = basePos.y + trunk;
            float radiusMetres = 1f + (trunk > Scale.Metres(3.5f) ? 0.4f : 0f);
            int radius = Scale.Metres(radiusMetres);
            int vertical = Scale.Metres(radiusMetres * 0.8f);
            for (int dy = -vertical; dy <= vertical; dy++)
                for (int dz = -radius; dz <= radius; dz++)
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        float d = (float)Math.Sqrt(dx * dx + dz * dz + dy * dy * 1.6f) / s;
                        if (d > radiusMetres + 0.1f) continue;
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
                        for (int i = 1; i <= Scale.ColonistClearance && clear; i++)
                            if (!world.IsAir(ground + new Int3(0, i, 0))) clear = false;
                        if (clear) return ground + Int3.Up;
                    }
            }
            return best;
        }

        private static int Clamp(int v, int min, int max) => v < min ? min : (v > max ? max : v);
    }
}
