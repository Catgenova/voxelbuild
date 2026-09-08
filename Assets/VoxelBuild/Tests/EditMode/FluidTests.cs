using NUnit.Framework;
using VoxelBuild.Core;
using VoxelBuild.Nav;
using VoxelBuild.Sim;
using VoxelBuild.World;

namespace VoxelBuild.Tests
{
    public class FluidTests
    {
        private const int Floor = 8;
        private static Int3 Feet(int x, int z) => new Int3(x, Floor + 1, z);

        private static int Settle(FluidSim sim, int maxSteps = 400)
        {
            int steps = 0;
            while (sim.ActiveCount > 0 && steps < maxSteps) { sim.Step(); steps++; }
            return steps;
        }

        [Test]
        public void WaterFallsAndSpreadsConservingVolume()
        {
            var world = TestWorld.Flat();
            var sim = new FluidSim(world);
            var top = new Int3(10, Floor + 4, 10);
            sim.AddWater(top, FluidSim.Max);
            Settle(sim);

            Assert.AreEqual(BlockType.Air, world.GetBlock(top), "water fell off the spawn cell");
            int total = 0;
            foreach (var c in new IntBox(new Int3(0, Floor + 1, 0), new Int3(31, Floor + 4, 31)).Cells()) total += sim.Level(c);
            Assert.AreEqual(FluidSim.Max, total, "volume is conserved");
            Assert.Greater(sim.CellCount, 1, "water spread over the floor");
            foreach (var c in new IntBox(new Int3(0, Floor + 1, 0), new Int3(31, Floor + 1, 31)).Cells())
                if (sim.Level(c) > 0) Assert.AreEqual(BlockType.Water, world.GetBlock(c));
        }

        [Test]
        public void WaterFlowsIntoAHoleDugBesideIt()
        {
            var world = TestWorld.Flat();
            // A small basin two deep filled with water.
            var basin = new IntBox(new Int3(4, Floor - 1, 4), new Int3(6, Floor, 6));
            foreach (var c in basin.Cells()) world.SetBlockRaw(c, BlockType.Water);
            var sim = new FluidSim(world);
            Assert.AreEqual(basin.Volume, sim.CellCount);
            Assert.AreEqual(0, sim.ActiveCount, "still lakes do not simulate");

            // Dig out the wall next to it, one block down.
            var hole = new Int3(7, Floor, 5);
            world.SetBlock(hole, BlockType.Air);
            Assert.Greater(sim.ActiveCount, 0, "digging next to water wakes it");
            Settle(sim);
            Assert.Greater(sim.Level(hole), 0, "water flowed into the hole");
        }

        [Test]
        public void BuildingIntoWaterDisplacesIt()
        {
            var world = TestWorld.Flat();
            var cell = Feet(5, 5);
            world.SetBlockRaw(cell, BlockType.Water);
            var sim = new FluidSim(world);
            Assert.AreEqual(FluidSim.Max, sim.Level(cell));
            world.SetBlock(cell, BlockType.Planks);
            Assert.AreEqual(0, sim.Level(cell));
            Assert.AreEqual(BlockType.Planks, world.GetBlock(cell));
        }

        [Test]
        public void ColonistsWadeShallowWaterButNotDeep()
        {
            var world = TestWorld.Flat();
            var shallow = Feet(3, 3);
            world.SetBlockRaw(shallow, BlockType.Water);
            Assert.IsTrue(NavRules.IsStandable(world, shallow), "one block of water is wadable");

            // Two deep: feet cell is water over water over floor.
            var deepBottom = new Int3(6, Floor, 6);
            world.SetBlockRaw(deepBottom, BlockType.Air);
            world.SetBlockRaw(deepBottom, BlockType.Water);
            world.SetBlockRaw(deepBottom + Int3.Up, BlockType.Water);
            Assert.IsTrue(NavRules.IsStandable(world, deepBottom + Int3.Up), "two blocks of water is still wadable");

            var col = new Int3(9, Floor - 2, 9);
            for (int y = Floor - 2; y <= Floor + 2; y++) world.SetBlockRaw(new Int3(9, y, 9), BlockType.Water);
            for (int y = Floor - 2; y <= Floor + 2; y++)
                Assert.IsFalse(NavRules.IsStandable(world, new Int3(9, y, 9)), $"cannot stand under deep water at y={y}");
            Assert.IsFalse(NavRules.IsStandable(world, new Int3(9, Floor + 3, 9)), "cannot float on the surface");
        }

        [Test]
        public void GeneratorMakesLakesThatStayDryUnderHills()
        {
            var world = new VoxelWorld(new Int3(3, 5, 3));
            var gen = new WorldGenerator { Seed = 7, Caves = false, BaseHeightMetres = 8f };
            gen.Generate(world);
            int water = world.CountBlocks(BlockType.Water);
            Assert.Greater(water, 0, "lowlands fill with water");
            int sea = Scale.Metres(gen.SeaLevelMetres);
            var size = world.SizeInBlocks;
            for (int y = sea; y < size.y; y++)
                for (int z = 0; z < size.z; z++)
                    for (int x = 0; x < size.x; x++)
                        Assert.AreNotEqual(BlockType.Water, world.GetBlock(new Int3(x, y, z)), $"no water above sea level at {x},{y},{z}");
            var spawn = WorldGenerator.FindSpawn(world);
            Assert.IsTrue(NavRules.IsStandable(world, spawn));
            Assert.IsFalse(NavRules.IsInWater(world, spawn), "colonists spawn on dry land");
        }

        [Test]
        public void SpringsRefillAndDrainOrdersRemoveThem()
        {
            var world = TestWorld.Flat();
            var sim = new FluidSim(world);
            var spring = Feet(5, 5);
            sim.SetSource(spring, true);
            Assert.IsTrue(sim.IsSource(spring));
            Assert.AreEqual(FluidSim.Max, sim.Level(spring));

            Assert.AreEqual(FluidSim.Max, sim.Draw(spring), "a bucket from a spring is always full");
            Assert.AreEqual(FluidSim.Max, sim.Level(spring), "and the spring stays full");

            Settle(sim, 200);
            Assert.Greater(sim.CellCount, 5, "a spring keeps feeding its surroundings");
            Assert.AreEqual(FluidSim.Max, sim.Level(spring));

            Assert.AreEqual(FluidSim.Max, sim.Scoop(spring));
            Assert.IsFalse(sim.IsSource(spring), "scooping removes the spring");
            Assert.AreEqual(0, sim.Level(spring));
        }

        [Test]
        public void FullCellBetweenTwoSpringsBecomesASpring()
        {
            var world = TestWorld.Flat();
            var sim = new FluidSim(world);
            var a = Feet(5, 5);
            var b = Feet(7, 5);
            var middle = Feet(6, 5);
            sim.SetSource(a, true);
            sim.SetSource(b, true);
            sim.Pour(middle, FluidSim.Max);
            Settle(sim, 50);
            Assert.IsTrue(sim.IsSource(middle), "an infinite pool forms between two springs");
        }

        [Test]
        public void LakeSurfacesAreSpringsAndBucketsPourWater()
        {
            var world = TestWorld.Flat();
            var basin = new IntBox(new Int3(4, Floor - 1, 4), new Int3(6, Floor, 6));
            foreach (var c in basin.Cells()) world.SetBlockRaw(c, BlockType.Water);
            var sim = new FluidSim(world);
            Assert.AreEqual(9, sim.SourceCount, "only the surface layer is infinite");
            Assert.IsTrue(sim.IsSource(new Int3(5, Floor, 5)));
            Assert.IsFalse(sim.IsSource(new Int3(5, Floor - 1, 5)));

            var target = Feet(15, 15);
            Assert.AreEqual(FluidSim.Max, sim.Pour(target, FluidSim.Max));
            Assert.AreEqual(BlockType.Water, world.GetBlock(target));
            Assert.AreEqual(0, sim.Pour(target, 1), "a full cell takes nothing");
        }

        [Test]
        public void MesherSplitsCrystalAndWaterFromTerrain()
        {
            var world = new VoxelWorld(new Int3(1, 1, 1));
            world.SetBlockRaw(new Int3(4, 4, 4), BlockType.Stone);
            world.SetBlockRaw(new Int3(5, 4, 4), BlockType.Crystal);
            world.SetBlockRaw(new Int3(8, 4, 4), BlockType.Water);
            var sim = new FluidSim(world);
            var opaque = new MeshData();
            var translucent = new MeshData();
            var water = new MeshData();
            ChunkMesher.Build(world, Int3.Zero, 15, 1f, sim, opaque, translucent, water);
            Assert.AreEqual(6, opaque.QuadCount, "stone shows all faces, including the one behind the crystal");
            Assert.AreEqual(5, translucent.QuadCount, "crystal hides only the face against the stone");
            Assert.AreEqual(6, water.QuadCount, "isolated full water cell draws top, four sides and bottom");
        }
    }
}
