using System;
using System.Collections.Generic;

namespace VoxelBuild.World
{
    using VoxelBuild.Core;

    /// <summary>Plain mesh buffers produced by the mesher. Positions are in world units (block size applied).</summary>
    public sealed class MeshData
    {
        public readonly List<float> Positions = new List<float>();
        public readonly List<float> Normals = new List<float>();
        public readonly List<float> Uvs = new List<float>();
        public readonly List<int> Triangles = new List<int>();

        public int VertexCount => Positions.Count / 3;
        public int QuadCount => Triangles.Count / 6;

        public void Clear()
        {
            Positions.Clear();
            Normals.Clear();
            Uvs.Clear();
            Triangles.Clear();
        }
    }

    /// <summary>Fill level of fluid cells, 1..MaxLevel; 0 for cells without fluid.</summary>
    public interface IFluidLevels
    {
        int MaxLevel { get; }
        int Level(Int3 cell);
    }

    /// <summary>
    /// Builds culled cube meshes for one chunk: opaque terrain, translucent solids (crystal) and fluid surfaces.
    /// Blocks above <c>sliceY</c> are treated as air so the player can look underground; the cut exposes the
    /// top faces of whatever sits at the slice level.
    /// </summary>
    public static class ChunkMesher
    {
        [System.ThreadStatic] private static MeshData scratchA, scratchB;

        /// <summary>Opaque-only build (tests and tools).</summary>
        public static void Build(IBlockQuery world, Int3 chunkCoord, int sliceY, float blockSize, MeshData data)
        {
            scratchA ??= new MeshData();
            scratchB ??= new MeshData();
            Build(world, chunkCoord, sliceY, blockSize, null, data, scratchA, scratchB);
        }

        // Corner positions per face, wound so cross(p1-p0, p2-p0) equals the outward normal (Unity clockwise front faces).
        private static readonly float[][] FaceCorners =
        {
            new float[] { 1,0,0, 1,1,0, 1,1,1, 1,0,1 }, // +X
            new float[] { 0,0,1, 0,1,1, 0,1,0, 0,0,0 }, // -X
            new float[] { 0,1,0, 0,1,1, 1,1,1, 1,1,0 }, // +Y
            new float[] { 0,0,0, 1,0,0, 1,0,1, 0,0,1 }, // -Y
            new float[] { 1,0,1, 1,1,1, 0,1,1, 0,0,1 }, // +Z
            new float[] { 0,0,0, 0,1,0, 1,1,0, 1,0,0 }, // -Z
        };

        private static readonly float[][] FaceNormals =
        {
            new float[] { 1, 0, 0 }, new float[] { -1, 0, 0 },
            new float[] { 0, 1, 0 }, new float[] { 0, -1, 0 },
            new float[] { 0, 0, 1 }, new float[] { 0, 0, -1 },
        };

        /// <summary>Non-solid decorative blocks (torches) render as a shrunken box with these extents.</summary>
        private const float DecorMinXZ = 0.35f, DecorMaxXZ = 0.65f, DecorMaxY = 0.7f;

        /// <summary>Full build: opaque faces, translucent (crystal) faces and fluid surfaces into separate buffers.</summary>
        public static void Build(IBlockQuery world, Int3 chunkCoord, int sliceY, float blockSize, IFluidLevels fluids,
            MeshData data, MeshData translucent, MeshData water)
        {
            data.Clear();
            translucent.Clear();
            water.Clear();
            AtlasLayout.EnsureInit();
            Int3 origin = chunkCoord * Chunk.Size;
            if (origin.y > sliceY) return;

            for (int ly = 0; ly < Chunk.Size; ly++)
            {
                int wy = origin.y + ly;
                if (wy > sliceY) break;
                for (int lz = 0; lz < Chunk.Size; lz++)
                    for (int lx = 0; lx < Chunk.Size; lx++)
                    {
                        var p = new Int3(origin.x + lx, wy, origin.z + lz);
                        var type = world.GetBlock(p);
                        if (type == BlockType.Air) continue;
                        var def = BlockRegistry.Get(type);

                        if (def.IsFluid)
                        {
                            AddFluid(world, fluids, p, lx, ly, lz, sliceY, blockSize, water);
                            continue;
                        }

                        if (def.IsTranslucent)
                        {
                            for (int f = 0; f < 6; f++)
                            {
                                var n = p + Directions.Offsets[f];
                                if (n.y <= sliceY && world.InBounds(n) && world.GetBlock(n) == type) continue; // internal face
                                if (!IsFaceVisible(world, n, sliceY)) continue;
                                AddFace(translucent, lx, ly, lz, f, def.TileFor((Direction)f), blockSize, false, RotationFor(def, p, f));
                            }
                            continue;
                        }

                        if (!def.IsOpaque)
                        {
                            for (int f = 0; f < 6; f++)
                                AddFace(data, lx, ly, lz, f, def.TileFor((Direction)f), blockSize, true, 0);
                            continue;
                        }

                        for (int f = 0; f < 6; f++)
                        {
                            var n = p + Directions.Offsets[f];
                            if (!IsFaceVisible(world, n, sliceY)) continue;
                            AddFace(data, lx, ly, lz, f, def.TileFor((Direction)f), blockSize, false, RotationFor(def, p, f));
                        }
                    }
            }
        }

        /// <summary>Per-block pseudo-random quarter-turn of a face's texture, to hide tiling repetition.</summary>
        public static int RotationFor(BlockDefinition def, Int3 p, int face)
        {
            bool vertical = face == (int)Direction.PosY || face == (int)Direction.NegY;
            if (vertical ? !def.RotateTopBottom : !def.RotateSides) return 0;
            return (int)(Noise.HashU(p.x, p.y, p.z, 977 + face) & 3u);
        }

        private static bool IsFaceVisible(IBlockQuery world, Int3 neighbour, int sliceY)
        {
            if (neighbour.y > sliceY) return true;
            if (!world.InBounds(neighbour)) return neighbour.y >= 0; // hide the underside of the world floor
            return !BlockRegistry.IsOpaque(world.GetBlock(neighbour));
        }

        // ------------------------------------------------------------------ Fluids

        /// <summary>Surface height (0..1 of a block) of the fluid in a cell, 0 if none, 1 if fluid continues above.</summary>
        private static float FluidHeight(IBlockQuery world, IFluidLevels fluids, Int3 cell, int sliceY)
        {
            if (cell.y > sliceY || !world.InBounds(cell)) return 0f;
            if (!BlockRegistry.IsFluid(world.GetBlock(cell))) return 0f;
            var above = cell + Int3.Up;
            if (above.y <= sliceY && world.InBounds(above) && BlockRegistry.IsFluid(world.GetBlock(above))) return 1f;
            if (fluids == null) return 1f;
            int level = fluids.Level(cell);
            if (level <= 0) return 1f;
            return Math.Max(0.1f, (float)level / fluids.MaxLevel);
        }

        /// <summary>World-space tiling of the water surface texture, in blocks per repeat.</summary>
        private const float WaterUvBlocks = 8f;

        private static void AddFluid(IBlockQuery world, IFluidLevels fluids, Int3 p, int lx, int ly, int lz, int sliceY, float blockSize, MeshData water)
        {
            float h = FluidHeight(world, fluids, p, sliceY);
            if (h <= 0f) return;
            bool coveredAbove = h >= 1f && FluidHeight(world, fluids, p + Int3.Up, sliceY) > 0f;

            // Top surface.
            if (!coveredAbove)
                AddFluidQuad(water, lx, ly, lz, Direction.PosY, 0f, h, blockSize, p);

            // Sides: full height against air, only the exposed strip against lower neighbouring water.
            for (int i = 0; i < 4; i++)
            {
                var off = Directions.Horizontal[i];
                var n = p + off;
                if (n.y <= sliceY && world.InBounds(n))
                {
                    var nt = world.GetBlock(n);
                    if (BlockRegistry.IsOpaque(nt) || BlockRegistry.IsSolid(nt)) continue;
                    float nh = FluidHeight(world, fluids, n, sliceY);
                    if (nh >= h) continue;
                    AddFluidQuad(water, lx, ly, lz, DirectionOf(off), nh, h, blockSize, p);
                }
                else if (!world.InBounds(n) && n.y >= 0)
                {
                    AddFluidQuad(water, lx, ly, lz, DirectionOf(off), 0f, h, blockSize, p);
                }
            }

            // Bottom: only when there is nothing under the water (falling water).
            var below = p + Int3.Down;
            if (world.InBounds(below))
            {
                var bt = world.GetBlock(below);
                if (!BlockRegistry.IsSolid(bt) && !BlockRegistry.IsFluid(bt))
                    AddFluidQuad(water, lx, ly, lz, Direction.NegY, 0f, h, blockSize, p);
            }
        }

        private static Direction DirectionOf(Int3 off)
        {
            if (off.x > 0) return Direction.PosX;
            if (off.x < 0) return Direction.NegX;
            return off.z > 0 ? Direction.PosZ : Direction.NegZ;
        }

        /// <summary>A fluid face between heights <paramref name="y0"/> and <paramref name="y1"/> (block fractions), world-space UVs.</summary>
        private static void AddFluidQuad(MeshData water, int lx, int ly, int lz, Direction face, float y0, float y1, float blockSize, Int3 worldPos)
        {
            int f = (int)face;
            var corners = FaceCorners[f];
            var normal = FaceNormals[f];
            int baseIndex = water.VertexCount;
            for (int i = 0; i < 4; i++)
            {
                float cx = corners[i * 3], cy = corners[i * 3 + 1], cz = corners[i * 3 + 2];
                if (face == Direction.PosY) cy = y1;
                else if (face == Direction.NegY) cy = y0;
                else cy = cy < 0.5f ? y0 : y1;
                water.Positions.Add((lx + cx) * blockSize);
                water.Positions.Add((ly + cy) * blockSize);
                water.Positions.Add((lz + cz) * blockSize);
                water.Normals.Add(normal[0]);
                water.Normals.Add(normal[1]);
                water.Normals.Add(normal[2]);
                // UVs in world space so the surface texture flows continuously across chunks.
                float wx = worldPos.x + cx, wy = worldPos.y + cy, wz = worldPos.z + cz;
                if (face == Direction.PosY || face == Direction.NegY) { water.Uvs.Add(wx / WaterUvBlocks); water.Uvs.Add(wz / WaterUvBlocks); }
                else if (face == Direction.PosX || face == Direction.NegX) { water.Uvs.Add(wz / WaterUvBlocks); water.Uvs.Add(wy / WaterUvBlocks); }
                else { water.Uvs.Add(wx / WaterUvBlocks); water.Uvs.Add(wy / WaterUvBlocks); }
            }
            water.Triangles.Add(baseIndex);
            water.Triangles.Add(baseIndex + 1);
            water.Triangles.Add(baseIndex + 2);
            water.Triangles.Add(baseIndex);
            water.Triangles.Add(baseIndex + 2);
            water.Triangles.Add(baseIndex + 3);
        }

        private static void AddFace(MeshData data, int lx, int ly, int lz, int face, int tile, float blockSize, bool decor, int rotation)
        {
            int baseIndex = data.VertexCount;
            var corners = FaceCorners[face];
            var normal = FaceNormals[face];
            AtlasLayout.GetUvRect(tile, out float u0, out float v0, out float u1, out float v1);

            for (int i = 0; i < 4; i++)
            {
                float cx = corners[i * 3], cy = corners[i * 3 + 1], cz = corners[i * 3 + 2];
                if (decor)
                {
                    cx = cx < 0.5f ? DecorMinXZ : DecorMaxXZ;
                    cz = cz < 0.5f ? DecorMinXZ : DecorMaxXZ;
                    cy = cy < 0.5f ? 0f : DecorMaxY;
                }
                data.Positions.Add((lx + cx) * blockSize);
                data.Positions.Add((ly + cy) * blockSize);
                data.Positions.Add((lz + cz) * blockSize);
                data.Normals.Add(normal[0]);
                data.Normals.Add(normal[1]);
                data.Normals.Add(normal[2]);
            }

            // Corner UVs, rotated by quarter turns when the block allows it.
            for (int i = 0; i < 4; i++)
            {
                int c = (i + rotation) & 3;
                data.Uvs.Add(c == 0 || c == 1 ? u0 : u1);
                data.Uvs.Add(c == 1 || c == 2 ? v1 : v0);
            }

            data.Triangles.Add(baseIndex);
            data.Triangles.Add(baseIndex + 1);
            data.Triangles.Add(baseIndex + 2);
            data.Triangles.Add(baseIndex);
            data.Triangles.Add(baseIndex + 2);
            data.Triangles.Add(baseIndex + 3);
        }
    }
}
