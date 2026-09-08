using NUnit.Framework;
using VoxelBuild.Core;
using VoxelBuild.Nav;
using VoxelBuild.Sim;
using VoxelBuild.World;

namespace VoxelBuild.Tests
{
    public class FloorTests
    {
        private const int Floor = 8;
        private static Int3 Feet(int x, int z) => new Int3(x, Floor + 1, z);

        /// <summary>Digs a pit so that cells over it have air beneath; returns a cell above the pit.</summary>
        private static Int3 DigPit(VoxelWorld world, int x0, int z0, int size)
        {
            for (int z = z0; z < z0 + size; z++)
                for (int x = x0; x < x0 + size; x++)
                    for (int y = Floor - 3; y <= Floor; y++)
                        world.SetBlockRaw(new Int3(x, y, z), BlockType.Air);
            return new Int3(x0 + 1, Floor + 1, z0 + 1);
        }

        [Test]
        public void FloorsMakeAirWalkableAndStopFalls()
        {
            var world = TestWorld.Flat();
            var over = DigPit(world, 10, 10, 4);
            Assert.IsFalse(NavRules.IsStandable(world, over), "nothing under the feet");
            world.SetFloor(over, BlockType.PlankFloor);
            Assert.IsTrue(NavRules.IsStandable(world, over), "a floor tile carries a colonist");
            Assert.IsTrue(NavRules.FindGround(world, over + new Int3(0, 3, 0), 6, out var ground));
            Assert.AreEqual(over, ground, "a fall lands on the floor, not in the pit");

            var ctx = TestWorld.Context(world);
            ctx.Items.Add(over + new Int3(0, 2, 0), ItemType.Stone, 1);
            Assert.AreEqual(0, ctx.Items.Count(over, ItemType.Stone));
            ctx.Items.AddNear(over + new Int3(0, 2, 0), ItemType.Stone, 1);
            Assert.AreEqual(1, ctx.Items.Count(over, ItemType.Stone), "dropped items rest on floors");
        }

        [Test]
        public void FloorAboveTheHeadIsACeiling()
        {
            var world = TestWorld.Flat();
            var feet = Feet(5, 5);
            Assert.IsTrue(NavRules.IsStandable(world, feet));
            world.SetFloor(feet + new Int3(0, 3, 0), BlockType.StoneTiles);
            Assert.IsFalse(NavRules.IsStandable(world, feet), "a floor three cells up blocks a seven-cell-tall colonist");
        }

        [Test]
        public void FloorsNeedSupportWithinThreeCells()
        {
            var world = TestWorld.Flat(3, 2, Floor);
            // A pit 10 wide: cells 10..19 have no floor beneath.
            DigPit(world, 10, 10, 10);
            var edge = new Int3(9, Floor + 1, 15);                 // solid block below at x=9
            Assert.IsTrue(FloorRules.CanPlace(world, edge));
            Assert.IsTrue(FloorRules.CanPlace(world, new Int3(12, Floor + 1, 15)), "3 cells from the block at x=9");
            Assert.IsFalse(FloorRules.CanPlace(world, new Int3(13, Floor + 1, 15)), "4 cells out is too far");
            Assert.IsFalse(FloorRules.CanPlace(world, new Int3(5, Floor, 5)), "cannot put a floor inside solid stone");

            var jobs = new JobBoard(world);
            Assert.IsTrue(jobs.AddBuild(new Int3(12, Floor + 1, 15), BlockType.PlankFloor));
            Assert.IsFalse(jobs.AddBuild(new Int3(13, Floor + 1, 15), BlockType.PlankFloor), "orders respect the support rule");
        }

        [Test]
        public void FloorsCollapseWhenSupportIsMined()
        {
            var world = TestWorld.Flat(3, 2, Floor);
            DigPit(world, 10, 10, 10);
            var ctx = TestWorld.Context(world);
            var cell = new Int3(12, Floor + 1, 15);
            world.SetFloor(cell, BlockType.StoneTiles);
            // Remove the whole supporting column of blocks at x=9 near z=15 (the only support within 3 cells).
            for (int z = 12; z <= 18; z++) world.SetBlock(new Int3(9, Floor, z), BlockType.Air);
            Assert.AreEqual(BlockType.Air, world.GetFloor(cell), "unsupported floor fell");
            Assert.AreEqual(1, ctx.Floors.Collapsed);
            Assert.AreEqual(1, ctx.Items.TotalOf(ItemType.StoneBrick), "the tile can be picked up again");
        }

        [Test]
        public void ColonistLaysAndRemovesFloorAndSolidBlocksDisplaceIt()
        {
            var world = TestWorld.Flat();
            var ctx = TestWorld.Context(world);
            var c = TestWorld.Colonist(ctx, Feet(3, 3));
            c.Work.Set(WorkType.Build, 1);
            c.Work.Set(WorkType.Mine, 1);
            ctx.Items.Add(Feet(4, 3), ItemType.Carpet, 2);
            ctx.Items.Add(Feet(4, 4), ItemType.Stone, 2);
            var cell = Feet(10, 10);

            Assert.IsTrue(ctx.Jobs.AddBuild(cell, BlockType.Carpet));
            Assert.IsTrue(TestWorld.RunUntil(ctx, () => world.GetFloor(cell) == BlockType.Carpet, 60f), $"carpet laid; {c.Activity}");
            Assert.AreEqual(BlockType.Air, world.GetBlock(cell), "the cell itself stays open");

            Assert.IsTrue(ctx.Jobs.AddMine(cell), "a floor can be ordered removed");
            Assert.IsTrue(TestWorld.RunUntil(ctx, () => world.GetFloor(cell) == BlockType.Air, 60f), $"carpet removed; {c.Activity}");
            Assert.AreEqual(2, ctx.TotalItems(ItemType.Carpet), "the tile came back as an item");

            world.SetFloor(cell, BlockType.Carpet);
            Assert.IsTrue(ctx.Jobs.AddBuild(cell, BlockType.Stone));
            Assert.IsTrue(TestWorld.RunUntil(ctx, () => world.GetBlock(cell) == BlockType.Stone, 60f), $"stone built; {c.Activity}");
            Assert.AreEqual(BlockType.Air, world.GetFloor(cell), "a solid block pushes the floor out");
            Assert.AreEqual(3, ctx.TotalItems(ItemType.Carpet), "and it drops as an item");
        }

        [Test]
        public void MesherDrawsFloorSlabs()
        {
            var world = new VoxelWorld(new Int3(1, 1, 1));
            world.SetBlockRaw(new Int3(5, 4, 5), BlockType.Stone);
            world.SetFloor(new Int3(5, 5, 5), BlockType.PlankFloor);
            var data = new MeshData();
            ChunkMesher.Build(world, Int3.Zero, 15, 1f, data);
            // Stone: 6 faces. Slab: top + 4 sides (bottom hidden by the opaque stone).
            Assert.AreEqual(6 + 5, data.QuadCount);
            world.SetFloor(new Int3(8, 8, 8), BlockType.Carpet);
            ChunkMesher.Build(world, Int3.Zero, 15, 1f, data);
            Assert.AreEqual(6 + 5 + 6, data.QuadCount, "a slab over air also shows its underside");
            float top = float.MinValue;
            for (int i = 0; i < data.VertexCount; i++) top = System.Math.Max(top, data.Positions[i * 3 + 1]);
            Assert.AreEqual(8f + ChunkMesher.FloorThickness, top, 1e-4f, "slabs are thin");
        }
    }
}
