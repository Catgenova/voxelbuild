using NUnit.Framework;
using VoxelBuild.Core;

namespace VoxelBuild.Tests
{
    public class CoreTests
    {
        [Test]
        public void FloorDivAndModHandleNegatives()
        {
            Assert.AreEqual(-1, Int3.FloorDiv(-1, 16));
            Assert.AreEqual(0, Int3.FloorDiv(15, 16));
            Assert.AreEqual(1, Int3.FloorDiv(16, 16));
            Assert.AreEqual(15, Int3.FloorMod(-1, 16));
            Assert.AreEqual(0, Int3.FloorMod(16, 16));
        }

        [Test]
        public void IntBoxNormalisesCornersAndCountsCells()
        {
            var box = new IntBox(new Int3(3, 2, 1), new Int3(1, 2, 3));
            Assert.AreEqual(new Int3(1, 2, 1), box.Min);
            Assert.AreEqual(new Int3(3, 2, 3), box.Max);
            Assert.AreEqual(9, box.Volume);
            int n = 0;
            foreach (var _ in box.Cells()) n++;
            Assert.AreEqual(9, n);
            Assert.IsTrue(box.Contains(new Int3(2, 2, 2)));
            Assert.IsFalse(box.Contains(new Int3(2, 3, 2)));
        }

        [Test]
        public void EveryBlockHasADefinitionAndAtlasTiles()
        {
            World.AtlasLayout.EnsureInit();
            for (int i = 1; i < (int)BlockType.Count; i++)
            {
                var def = BlockRegistry.Get((BlockType)i);
                Assert.AreEqual((BlockType)i, def.Type, $"Missing definition for {(BlockType)i}");
                Assert.GreaterOrEqual(def.TileTop, 0);
                Assert.GreaterOrEqual(def.TileSide, 0);
                Assert.GreaterOrEqual(def.TileBottom, 0);
            }
            Assert.AreEqual(((int)BlockType.Count - 1) * 3, World.AtlasLayout.TileCount);
        }

        [Test]
        public void BuildableBlocksCostAnItemThatCanBeObtained()
        {
            foreach (var def in BlockRegistry.Buildable())
            {
                Assert.AreNotEqual(ItemType.None, def.BuildCost.Type, def.Name);
                Assert.Greater(def.BuildCost.Count, 0, def.Name);
            }
        }

        [Test]
        public void RecipesProduceSomethingAndHaveInputs()
        {
            foreach (var r in RecipeRegistry.All)
            {
                Assert.IsNotEmpty(r.Inputs, r.Id);
                Assert.Greater(r.Output.Count, 0, r.Id);
                Assert.Greater(r.WorkSeconds, 0f, r.Id);
            }
            Assert.IsNotNull(RecipeRegistry.Find("planks"));
            Assert.AreEqual(BlockType.CarpentryBench, RecipeRegistry.Find("planks").Station, "wood is processed at the carpentry bench");
            foreach (var r in RecipeRegistry.All) Assert.IsFalse(r.ByHand, $"{r.Id}: all processing happens at a workshop");
            Assert.IsTrue(RecipeRegistry.IsWorkshop(BlockType.CarpentryBench));
            Assert.IsTrue(RecipeRegistry.IsWorkshop(BlockType.Workbench));
            Assert.IsFalse(RecipeRegistry.IsWorkshop(BlockType.Stone));
        }

        [Test]
        public void RawBlocksCanBePlacedBackFromTheirDrops()
        {
            foreach (var type in new[] { BlockType.Dirt, BlockType.Stone, BlockType.Sand, BlockType.Gravel, BlockType.Clay, BlockType.Log,
                                         BlockType.CoalOre, BlockType.IronOre, BlockType.GoldOre, BlockType.Crystal })
            {
                var def = BlockRegistry.Get(type);
                Assert.IsTrue(def.IsBuildable, $"{def.Name} is placeable");
                Assert.AreEqual(def.Drop.Type, def.BuildCost.Type, $"{def.Name} is rebuilt from what it drops");
            }
            var bench = BlockRegistry.Get(BlockType.CarpentryBench);
            Assert.AreEqual(ItemType.Log, bench.BuildCost.Type, "the first workshop needs only raw logs");
            Assert.AreEqual(ItemType.Planks, BlockRegistry.Get(BlockType.Workbench).BuildCost.Type, "the workbench needs processed planks");
        }
    }
}
