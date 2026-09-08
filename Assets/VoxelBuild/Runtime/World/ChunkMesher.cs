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

    /// <summary>
    /// Builds a culled cube mesh for one chunk. Blocks above <c>sliceY</c> are treated as air so the player can look
    /// underground; the cut exposes the top faces of whatever sits at the slice level.
    /// </summary>
    public static class ChunkMesher
    {
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

        public static void Build(IBlockQuery world, Int3 chunkCoord, int sliceY, float blockSize, MeshData data)
        {
            data.Clear();
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
