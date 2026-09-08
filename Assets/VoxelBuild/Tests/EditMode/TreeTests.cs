using NUnit.Framework;
using VoxelBuild.Core;
using VoxelBuild.Sim;
using VoxelBuild.World;

namespace VoxelBuild.Tests
{
    public class TreeTests
    {
        private const int Floor = 8;
        private static Int3 Feet(int x, int z) => new Int3(x, Floor + 1, z);

        /// <summary>A trunk of <paramref name="height"/> logs with a leaf ball on top, standing on the floor.</summary>
        private static Int3 PlantTree(VoxelWorld world, int x, int z, int height)
        {
            var basePos = Feet(x, z);
            for (int i = 0; i < height; i++) world.SetBlockRaw(basePos + new Int3(0, i, 0), BlockType.Log);
            int top = basePos.y + height;
            for (int dy = -2; dy <= 1; dy++)
                for (int dz = -2; dz <= 2; dz++)
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        var p = new Int3(x + dx, top + dy, z + dz);
                        if (world.GetBlock(p) == BlockType.Air) world.SetBlockRaw(p, BlockType.Leaves);
                    }
            return basePos;
        }

        [Test]
        public void CuttingTheBaseFellsTheWholeTreeAndDropsLogsAlongTheFallLine()
        {
            var world = TestWorld.Flat(2, 3, Floor);
            var basePos = PlantTree(world, 10, 10, 8);
            var ctx = TestWorld.Context(world);
            int leavesBefore = world.CountBlocks(BlockType.Leaves);
            Assert.Greater(leavesBefore, 0);

            world.SetBlock(basePos, BlockType.Air);
            var fall = ctx.Trees.OnLogRemoved(basePos, awayFrom: Feet(8, 10));
            Assert.IsNotNull(fall, "an unsupported trunk falls");
            Assert.AreEqual(Int3.Right, fall.Direction, "it falls away from the woodcutter");
            Assert.AreEqual(7, fall.LogCount);
            Assert.AreEqual(0, world.CountBlocks(BlockType.Log), "the trunk is gone from the world");
            Assert.AreEqual(0, world.CountBlocks(BlockType.Leaves), "and so is its canopy");
            Assert.AreEqual(7, ctx.Trees.PendingDrops);
            Assert.AreEqual(0, ctx.Items.TotalOf(ItemType.Log), "logs are still in the air");

            ctx.Tick(fall.Duration + 0.1f);
            Assert.AreEqual(0, ctx.Trees.PendingDrops);
            Assert.AreEqual(7, ctx.Items.TotalOf(ItemType.Log), "every log can be picked up");
            int onFallLine = 0;
            for (int i = 1; i <= 8; i++) onFallLine += ctx.Items.Count(Feet(10 + i, 10), ItemType.Log);
            Assert.AreEqual(7, onFallLine, "logs lie along the ground where the trunk landed");
        }

        [Test]
        public void CuttingTheMiddleDropsOnlyTheTop()
        {
            var world = TestWorld.Flat(2, 3, Floor);
            var basePos = PlantTree(world, 10, 10, 8);
            var ctx = TestWorld.Context(world);
            var cut = basePos + new Int3(0, 3, 0);
            world.SetBlock(cut, BlockType.Air);
            var fall = ctx.Trees.OnLogRemoved(cut, Feet(10, 12));
            Assert.IsNotNull(fall);
            Assert.AreEqual(Int3.Back, fall.Direction);
            Assert.AreEqual(4, fall.LogCount);
            Assert.AreEqual(3, world.CountBlocks(BlockType.Log), "the stump stays");
        }

        [Test]
        public void SupportedLogsDoNotFall()
        {
            var world = TestWorld.Flat(2, 3, Floor);
            // A log lintel: two posts with a beam across, all resting on the ground through the posts.
            for (int i = 0; i < 3; i++)
            {
                world.SetBlockRaw(Feet(4, 4) + new Int3(0, i, 0), BlockType.Log);
                world.SetBlockRaw(Feet(6, 4) + new Int3(0, i, 0), BlockType.Log);
            }
            world.SetBlockRaw(Feet(5, 4) + new Int3(0, 3, 0), BlockType.Log);
            world.SetBlockRaw(Feet(4, 4) + new Int3(0, 3, 0), BlockType.Log);
            world.SetBlockRaw(Feet(6, 4) + new Int3(0, 3, 0), BlockType.Log);
            var ctx = TestWorld.Context(world);

            var cut = Feet(4, 4);
            world.SetBlock(cut, BlockType.Air);
            Assert.IsNull(ctx.Trees.OnLogRemoved(cut, Feet(2, 4)), "the beam still stands on the other post");
            Assert.AreEqual(8, world.CountBlocks(BlockType.Log));
        }

        [Test]
        public void WoodcutterJobFellsTreesAndLogsCanBeHauled()
        {
            var world = TestWorld.Flat(2, 3, Floor);
            var basePos = PlantTree(world, 12, 12, 6);
            var ctx = TestWorld.Context(world);
            var c = TestWorld.Colonist(ctx, Feet(3, 3));
            c.Work.Set(WorkType.Mine, 1);
            Assert.IsTrue(ctx.Jobs.AddMine(basePos));
            Assert.IsTrue(TestWorld.RunUntil(ctx, () => world.GetBlock(basePos) == BlockType.Air, 60f), c.Activity);
            Assert.AreEqual(0, world.CountBlocks(BlockType.Log));
            Assert.IsTrue(TestWorld.RunUntil(ctx, () => ctx.TotalItems(ItemType.Log) == 6, 10f), "one log in the pack, five on the ground");
        }
    }
}
