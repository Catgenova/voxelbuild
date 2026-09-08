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
        /// <summary>Block that must be adjacent to the crafter. Air means it can be done by hand anywhere.</summary>
        public BlockType Station = BlockType.Air;

        public bool ByHand => Station == BlockType.Air;

        public override string ToString() => Name;
    }

    public static class RecipeRegistry
    {
        private static readonly List<Recipe> Recipes = new List<Recipe>();

        static RecipeRegistry()
        {
            Recipes.Add(new Recipe
            {
                Id = "planks", Name = "Saw Planks",
                Inputs = new[] { new ItemStack(ItemType.Log, 1) },
                Output = new ItemStack(ItemType.Planks, 4), WorkSeconds = 3f,
            });
            Recipes.Add(new Recipe
            {
                Id = "stonebrick", Name = "Cut Stone Brick",
                Inputs = new[] { new ItemStack(ItemType.Stone, 2) },
                Output = new ItemStack(ItemType.StoneBrick, 1), WorkSeconds = 4f,
            });
            Recipes.Add(new Recipe
            {
                Id = "workbench", Name = "Build Workbench Kit",
                Inputs = new[] { new ItemStack(ItemType.Planks, 4), new ItemStack(ItemType.Stone, 1) },
                Output = new ItemStack(ItemType.Workbench, 1), WorkSeconds = 8f,
            });
            Recipes.Add(new Recipe
            {
                Id = "bed", Name = "Craft Bed",
                Inputs = new[] { new ItemStack(ItemType.Planks, 6) },
                Output = new ItemStack(ItemType.Bed, 1), WorkSeconds = 10f, Station = BlockType.Workbench,
            });
            Recipes.Add(new Recipe
            {
                Id = "torch", Name = "Craft Torches",
                Inputs = new[] { new ItemStack(ItemType.Planks, 1), new ItemStack(ItemType.Coal, 1) },
                Output = new ItemStack(ItemType.Torch, 4), WorkSeconds = 3f, Station = BlockType.Workbench,
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
    }
}
