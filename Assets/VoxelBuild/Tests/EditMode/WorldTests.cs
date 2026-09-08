using System.Collections.Generic;
using NUnit.Framework;
using VoxelBuild.Core;
using VoxelBuild.Nav;
using VoxelBuild.World;

namespace VoxelBuild.Tests
{
    public class WorldTests
    {
        [Test]
        public void SetAndGetRoundTripAcrossChunks()
        {
            var world = new VoxelWorld(new Int3(2, 2, 2));
            var p = new Int3(17, 3, 20);
            Assert.IsTrue(world.SetBlock(p, BlockType.Dirt));
            Assert.AreEqual(BlockType.Dirt, world.GetBlock(p));
            Assert.IsFalse(world.SetBlock(p, BlockType.Dirt), "setting the same value is not a change");
            Assert.AreEqual(BlockType.Air, world.GetBlock(new Int3(-1, 0, 0)));
            Assert.AreEqual(BlockType.Air, world.GetBlock(new Int3(0, 99, 0)));
            Assert.AreEqual(1, world.GetChunk(new Int3(1, 0, 1)).NonAirCount);
        }

        [Test]
        public void EdgeChangesDirtyNeighbouringChunks()
        {
            var world = new VoxelWorld(new Int3(2, 1, 1));
            var dirtied = new List<Int3>();
            world.ChunkDirtied += c => dirtied.Add(c);
            world.SetBlock(new Int3(15, 5, 5), BlockType.Stone);
            CollectionAssert.Contains(dirtied, new Int3(0, 0, 0));
            CollectionAssert.Contains(dirtied, new Int3(1, 0, 0));
        }

        [Test]
        public void GeneratorIsDeterministicAndSane()
        {
            var a = new VoxelWorld(new Int3(2, 3, 2));
            var b = new VoxelWorld(new Int3(2, 3, 2));
            new WorldGenerator { Seed = 42 }.Generate(a);
            new WorldGenerator { Seed = 42 }.Generate(b);

            for (int x = 0; x < a.SizeInBlocks.x; x++)
                for (int z = 0; z < a.SizeInBlocks.z; z++)
                {
                    Assert.AreEqual(BlockType.Bedrock, a.GetBlock(new Int3(x, 0, z)));
                    Assert.AreEqual(a.SurfaceY(x, z), b.SurfaceY(x, z));
                }
            Assert.Greater(a.CountBlocks(BlockType.Grass), 0);
            Assert.Greater(a.CountBlocks(BlockType.Stone), 0);

            var spawn = WorldGenerator.FindSpawn(a);
            Assert.IsTrue(NavRules.IsStandable(a, spawn), $"spawn {spawn} must be standable");
        }

        [Test]
        public void MesherCullsHiddenFacesAndRespectsSlice()
        {
            var world = new VoxelWorld(new Int3(1, 1, 1));
            var data = new MeshData();

            world.SetBlockRaw(new Int3(5, 5, 5), BlockType.Stone);
            ChunkMesher.Build(world, Int3.Zero, 15, 1f, data);
            Assert.AreEqual(6, data.QuadCount, "isolated block shows all six faces");

            world.SetBlockRaw(new Int3(6, 5, 5), BlockType.Stone);
            ChunkMesher.Build(world, Int3.Zero, 15, 1f, data);
            Assert.AreEqual(10, data.QuadCount, "two touching blocks hide the shared faces");

            ChunkMesher.Build(world, Int3.Zero, 4, 1f, data);
            Assert.AreEqual(0, data.QuadCount, "blocks above the slice are cut away");

            world.SetBlockRaw(new Int3(5, 4, 5), BlockType.Dirt);
            ChunkMesher.Build(world, Int3.Zero, 4, 1f, data);
            Assert.AreEqual(6, data.QuadCount, "the block at the slice exposes its top even though something sits on it");

            world.SetBlockRaw(new Int3(0, 0, 0), BlockType.Bedrock);
            ChunkMesher.Build(world, Int3.Zero, 0, 1f, data);
            Assert.AreEqual(5, data.QuadCount, "the world floor's underside is never drawn");
        }

        [Test]
        public void MeshDataIsConsistent()
        {
            var world = TestWorld.Flat(1, 1, 3);
            var data = new MeshData();
            ChunkMesher.Build(world, Int3.Zero, 15, 0.5f, data);
            Assert.AreEqual(data.VertexCount * 3, data.Normals.Count);
            Assert.AreEqual(data.VertexCount * 2, data.Uvs.Count);
            foreach (var i in data.Triangles) Assert.Less(i, data.VertexCount);
            // Only the top faces of a flat slab are visible (sides face out of the world at x/z edges and are drawn).
            Assert.AreEqual(16 * 16 + 4 * 16 * 4, data.QuadCount);
        }
    }
}
