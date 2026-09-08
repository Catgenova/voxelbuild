using NUnit.Framework;
using VoxelBuild.Core;
using VoxelBuild.Sim;
using VoxelBuild.World;

namespace VoxelBuild.Tests
{
    public class SimulationTests
    {
        private const int Floor = 8;
        private static Int3 Feet(int x, int z) => new Int3(x, Floor + 1, z);

        [Test]
        public void InventoryRespectsCapacity()
        {
            var inv = new Inventory(5);
            Assert.AreEqual(5, inv.Add(ItemType.Stone, 8));
            Assert.IsTrue(inv.IsFull);
            Assert.AreEqual(0, inv.Add(ItemType.Dirt, 1));
            Assert.AreEqual(3, inv.Remove(ItemType.Stone, 3));
            Assert.AreEqual(2, inv.Count(ItemType.Stone));
            Assert.AreEqual(2, inv.Remove(ItemType.Stone, 10));
            Assert.IsTrue(inv.IsEmpty);
        }

        [Test]
        public void GroundItemsFallAndRiseWithBlockChanges()
        {
            var world = TestWorld.Flat();
            var ctx = TestWorld.Context(world);
            var cell = Feet(4, 4);
            ctx.Items.Add(cell, ItemType.Log, 3);

            world.SetBlock(cell, BlockType.Stone);
            Assert.AreEqual(0, ctx.Items.Count(cell, ItemType.Log));
            Assert.AreEqual(3, ctx.Items.Count(cell + Int3.Up, ItemType.Log), "pile is pushed up when built over");

            world.SetBlock(cell, BlockType.Air);
            Assert.AreEqual(3, ctx.Items.Count(cell, ItemType.Log), "pile falls when the block under it is removed");
        }

        [Test]
        public void JobBoardValidatesOrders()
        {
            var world = TestWorld.Flat();
            var jobs = new JobBoard(world);
            Assert.IsFalse(jobs.AddMine(Feet(1, 1)), "cannot mine air");
            Assert.IsFalse(jobs.AddMine(new Int3(1, 0, 1)), "cannot mine bedrock");
            Assert.IsTrue(jobs.AddMine(new Int3(1, Floor, 1)));
            Assert.IsFalse(jobs.AddBuild(new Int3(1, Floor, 1), BlockType.Planks), "cannot build into stone");
            Assert.IsTrue(jobs.AddBuild(Feet(1, 1), BlockType.Planks));
            Assert.AreEqual(2, jobs.DesignationCount);

            world.SetBlock(new Int3(1, Floor, 1), BlockType.Air);
            Assert.AreEqual(1, jobs.DesignationCount, "mining order disappears once the block is gone");
            Assert.AreEqual(1, jobs.CancelBox(new IntBox(Int3.Zero, new Int3(30, 30, 30))));
            Assert.AreEqual(0, jobs.DesignationCount);
        }

        [Test]
        public void ColonistMinesADesignatedBlock()
        {
            var world = TestWorld.Flat();
            var ctx = TestWorld.Context(world);
            var c = TestWorld.Colonist(ctx, Feet(3, 3));
            var target = new Int3(10, Floor, 10);
            Assert.IsTrue(ctx.Jobs.AddMine(target));

            Assert.IsTrue(TestWorld.RunUntil(ctx, () => world.GetBlock(target) == BlockType.Air, 60f),
                $"block should be mined; colonist at {c.Cell} doing {c.Activity}");
            Assert.AreEqual(1, c.Inventory.Count(ItemType.Stone));
            Assert.AreEqual(0, ctx.Jobs.DesignationCount);
        }

        [Test]
        public void ColonistFetchesMaterialAndBuilds()
        {
            var world = TestWorld.Flat();
            var ctx = TestWorld.Context(world);
            TestWorld.Colonist(ctx, Feet(3, 3));
            ctx.Items.Add(Feet(6, 3), ItemType.Planks, 5);
            var site = Feet(12, 12);
            Assert.IsTrue(ctx.Jobs.AddBuild(site, BlockType.Planks));

            Assert.IsTrue(TestWorld.RunUntil(ctx, () => world.GetBlock(site) == BlockType.Planks, 90f));
            Assert.AreEqual(4, ctx.TotalItems(ItemType.Planks), "one plank was consumed");
            Assert.AreEqual(0, ctx.Items.TotalOf(ItemType.Planks), "the builder took the whole small pile along");
        }

        [Test]
        public void ColonistCraftsByHandAndHaulsToStockpile()
        {
            var world = TestWorld.Flat();
            var ctx = TestWorld.Context(world);
            var c = TestWorld.Colonist(ctx, Feet(3, 3));
            c.Work.Set(WorkType.Craft, 1);
            c.Work.Set(WorkType.Haul, 2);
            ctx.Items.Add(Feet(5, 3), ItemType.Log, 1);
            ctx.Stockpiles.Add(Feet(20, 20));
            ctx.Jobs.AddBill(RecipeRegistry.Find("planks"), null, 1);

            Assert.IsTrue(TestWorld.RunUntil(ctx, () => ctx.TotalItems(ItemType.Planks) == 4, 90f));
            Assert.AreEqual(0, ctx.TotalItems(ItemType.Log));
            Assert.AreEqual(0, ctx.Jobs.Bills.Count);
            Assert.IsTrue(TestWorld.RunUntil(ctx, () => ctx.Items.Count(Feet(20, 20), ItemType.Planks) == 4, 90f),
                "planks end up in the stockpile");
        }

        [Test]
        public void HungryColonistEatsFromAPile()
        {
            var world = TestWorld.Flat();
            var ctx = TestWorld.Context(world);
            var c = TestWorld.Colonist(ctx, Feet(3, 3));
            c.Needs.Food = 0.2f;
            ctx.Items.Add(Feet(8, 8), ItemType.Berries, 6);

            Assert.IsTrue(TestWorld.RunUntil(ctx, () => c.Needs.Food > 0.85f, 90f), $"colonist should eat; {c.Activity}");
            Assert.Less(ctx.TotalItems(ItemType.Berries), 6);
        }

        [Test]
        public void ExhaustedColonistSleepsAndRecovers()
        {
            var world = TestWorld.Flat();
            var ctx = TestWorld.Context(world);
            ctx.Clock.DayLengthSeconds = 60f;
            var c = TestWorld.Colonist(ctx, Feet(3, 3));
            c.Needs.Rest = 0.05f;
            c.Needs.Food = 1f;

            Assert.IsTrue(TestWorld.RunUntil(ctx, () => c.IsSleeping, 30f));
            Assert.IsTrue(TestWorld.RunUntil(ctx, () => c.Needs.Rest > 0.9f, 120f));
        }

        [Test]
        public void ColonistDrainsAndPoursWithABucket()
        {
            var world = TestWorld.Flat();
            var ctx = TestWorld.Context(world);
            var c = TestWorld.Colonist(ctx, Feet(3, 3));
            c.Work.Set(WorkType.Water, 1);
            ctx.Items.Add(Feet(4, 3), ItemType.Bucket, 1);
            var puddle = Feet(8, 8);
            ctx.Fluids.Pour(puddle, FluidSim.Max);
            var target = Feet(14, 14);

            Assert.IsTrue(ctx.Jobs.AddDrain(puddle));
            Assert.IsTrue(ctx.Jobs.AddPour(target));
            Assert.IsTrue(TestWorld.RunUntil(ctx, () => ctx.Fluids.Level(puddle) == 0, 60f), $"puddle drained; {c.Activity}");
            Assert.IsTrue(TestWorld.RunUntil(ctx, () => ctx.Fluids.Level(target) > 0, 60f), $"water poured at the target; {c.Activity}");
            Assert.AreEqual(1, ctx.TotalItems(ItemType.Bucket), "the bucket is empty again");
            Assert.AreEqual(0, ctx.Jobs.DesignationCount);
        }

        [Test]
        public void ColonistFillsBucketAtASpringForAPourOrder()
        {
            var world = TestWorld.Flat();
            var ctx = TestWorld.Context(world);
            var c = TestWorld.Colonist(ctx, Feet(3, 3));
            c.Work.Set(WorkType.Water, 1);
            c.Inventory.Add(ItemType.Bucket, 1);
            ctx.Fluids.SetSource(Feet(6, 6), true);
            var target = Feet(20, 3);
            Assert.IsTrue(ctx.Jobs.AddPour(target));
            Assert.IsTrue(TestWorld.RunUntil(ctx, () => ctx.Fluids.Level(target) > 0, 90f), $"water arrives; {c.Activity}");
            Assert.IsTrue(ctx.Fluids.IsSource(Feet(6, 6)), "the spring is untouched");
        }

        [Test]
        public void BlockIndexTracksBedsAndBushes()
        {
            var world = TestWorld.Flat();
            world.SetBlockRaw(Feet(5, 5), BlockType.Bed);
            var ctx = TestWorld.Context(world);
            Assert.AreEqual(1, ctx.Index.Count(BlockType.Bed));
            Assert.IsTrue(ctx.FindNearestBlock(Feet(1, 1), BlockType.Bed, null, out var bed));
            Assert.AreEqual(Feet(5, 5), bed);

            world.SetBlock(Feet(5, 5), BlockType.Air);
            world.SetBlock(Feet(9, 9), BlockType.BerryBush);
            Assert.AreEqual(0, ctx.Index.Count(BlockType.Bed));
            Assert.IsTrue(ctx.FindNearestBlock(Feet(1, 1), BlockType.BerryBush, null, out var bush));
            Assert.AreEqual(Feet(9, 9), bush);
        }

        [Test]
        public void ScaleConstantsAreConsistent()
        {
            Assert.AreEqual(1f, Scale.BlockSize * Scale.BlocksPerMetre, 1e-5f);
            Assert.GreaterOrEqual(Scale.ColonistClearance * Scale.BlockSize, 1.6f, "colonists need ~1.7 m of head room");
            Assert.Less(Scale.StepUp, Scale.ColonistClearance);
            Assert.Less(Scale.StepUp, Scale.MaxFall);
        }

        [Test]
        public void PrioritiesOrderWorkAndZeroDisables()
        {
            var w = new WorkPriorities();
            w.Set(WorkType.Haul, 1);
            w.Set(WorkType.Mine, 0);
            var ordered = new System.Collections.Generic.List<WorkType>(w.Ordered());
            Assert.AreEqual(WorkType.Haul, ordered[0]);
            CollectionAssert.Contains(ordered, WorkType.Water);
            CollectionAssert.DoesNotContain(ordered, WorkType.Mine);
            w.Cycle(WorkType.Mine);
            Assert.AreEqual(1, w.Get(WorkType.Mine));
        }
    }
}
