using System.Collections.Generic;

namespace VoxelBuild.Core
{
    public sealed class Recipe
    {
        public string Id;
        public string Name;
        public ItemStack[] Inputs;
        public ItemStack Output;
        public float WorkSeconds;
        /// <summary>Workshop block that must be within reach of the crafter. Every recipe needs one.</summary>
        public BlockType Station = BlockType.Air;

        public bool ByHand => Station == BlockType.Air;

        public override string ToString() => Name;
    }

    /// <summary>
    /// Raw blocks can be placed back as they are; turning them into anything else happens at a workshop.
    /// The Carpentry Bench processes wood, the Workbench processes stone.
    /// </summary>
    public static class RecipeRegistry
    {
        private static readonly List<Recipe> Recipes = new List<Recipe>();

        static RecipeRegistry()
        {
            // Carpentry Bench: wood.
            Recipes.Add(new Recipe
            {
                Id = "planks", Name = "Saw Planks",
                Inputs = new[] { new ItemStack(ItemType.Log, 1) },
                Output = new ItemStack(ItemType.Planks, 4), WorkSeconds = 3f, Station = BlockType.CarpentryBench,
            });
            Recipes.Add(new Recipe
            {
                Id = "bucket", Name = "Make Bucket",
                Inputs = new[] { new ItemStack(ItemType.Planks, 3) },
                Output = new ItemStack(ItemType.Bucket, 1), WorkSeconds = 6f, Station = BlockType.CarpentryBench,
            });
            Recipes.Add(new Recipe
            {
                Id = "torch", Name = "Make Torches",
                Inputs = new[] { new ItemStack(ItemType.Planks, 1), new ItemStack(ItemType.Coal, 1) },
                Output = new ItemStack(ItemType.Torch, 4), WorkSeconds = 3f, Station = BlockType.CarpentryBench,
            });

            Recipes.Add(new Recipe
            {
                Id = "carpet", Name = "Weave Carpet",
                Inputs = new[] { new ItemStack(ItemType.Planks, 1), new ItemStack(ItemType.Berries, 1) },
                Output = new ItemStack(ItemType.Carpet, 2), WorkSeconds = 4f, Station = BlockType.CarpentryBench,
            });

            // Workbench: stone.
            Recipes.Add(new Recipe
            {
                Id = "stonebrick", Name = "Cut Stone Brick",
                Inputs = new[] { new ItemStack(ItemType.Stone, 2) },
                Output = new ItemStack(ItemType.StoneBrick, 1), WorkSeconds = 4f, Station = BlockType.Workbench,
            });
        }

        public static IReadOnlyList<Recipe> All => Recipes;

        public static Recipe Find(string id)
        {
            foreach (var r in Recipes)
                if (r.Id == id) return r;
            return null;
        }

        public static IEnumerable<Recipe> ForStation(BlockType station)
        {
            foreach (var r in Recipes)
                if (r.Station == station) yield return r;
        }

        /// <summary>True if the block is a workshop with at least one recipe.</summary>
        public static bool IsWorkshop(BlockType block)
        {
            if (block == BlockType.Air) return false;
            foreach (var r in Recipes)
                if (r.Station == block) return true;
            return false;
        }
    }
}
