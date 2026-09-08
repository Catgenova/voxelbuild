using System;
using System.Collections.Generic;

namespace VoxelBuild.Core
{
    /// <summary>Every carryable item in the game. Block resources and crafted goods share this enum.</summary>
    public enum ItemType : ushort
    {
        None = 0,
        Dirt,
        Stone,
        Sand,
        Gravel,
        Clay,
        Coal,
        IronOre,
        GoldOre,
        Crystal,
        Log,
        Planks,
        StoneBrick,
        Berries,
        Workbench,
        Bed,
        Torch,
        Bucket,
        WaterBucket,
        Count,
    }

    public enum ItemCategory
    {
        RawMaterial,
        Material,
        Food,
        Furniture,
        Tool,
    }

    public sealed class ItemDefinition
    {
        public ItemType Type;
        public string Name;
        public ItemCategory Category;
        public ColorRgb Color;
        /// <summary>Nutrition restored when eaten (0 = not edible).</summary>
        public float Nutrition;
        /// <summary>Max items of this type in one ground stack / one inventory slot.</summary>
        public int StackSize = 50;
    }

    public struct ItemStack
    {
        public ItemType Type;
        public int Count;

        public ItemStack(ItemType type, int count)
        {
            Type = type;
            Count = count;
        }

        public bool IsEmpty => Type == ItemType.None || Count <= 0;
        public override string ToString() => $"{Count} {ItemRegistry.Get(Type).Name}";
    }

    public static class ItemRegistry
    {
        private static readonly ItemDefinition[] Defs = new ItemDefinition[(int)ItemType.Count];

        static ItemRegistry()
        {
            Add(ItemType.Dirt, "Dirt", ItemCategory.RawMaterial, ColorRgb.Bytes(121, 85, 58));
            Add(ItemType.Stone, "Stone", ItemCategory.RawMaterial, ColorRgb.Bytes(128, 128, 128));
            Add(ItemType.Sand, "Sand", ItemCategory.RawMaterial, ColorRgb.Bytes(219, 205, 150));
            Add(ItemType.Gravel, "Gravel", ItemCategory.RawMaterial, ColorRgb.Bytes(140, 135, 130));
            Add(ItemType.Clay, "Clay", ItemCategory.RawMaterial, ColorRgb.Bytes(160, 160, 175));
            Add(ItemType.Coal, "Coal", ItemCategory.RawMaterial, ColorRgb.Bytes(35, 35, 35));
            Add(ItemType.IronOre, "Iron Ore", ItemCategory.RawMaterial, ColorRgb.Bytes(190, 160, 130));
            Add(ItemType.GoldOre, "Gold Ore", ItemCategory.RawMaterial, ColorRgb.Bytes(240, 200, 60));
            Add(ItemType.Crystal, "Crystal", ItemCategory.RawMaterial, ColorRgb.Bytes(120, 220, 255));
            Add(ItemType.Log, "Log", ItemCategory.RawMaterial, ColorRgb.Bytes(110, 80, 45));
            Add(ItemType.Planks, "Planks", ItemCategory.Material, ColorRgb.Bytes(190, 150, 95));
            Add(ItemType.StoneBrick, "Stone Brick", ItemCategory.Material, ColorRgb.Bytes(110, 110, 115));
            Add(ItemType.Berries, "Berries", ItemCategory.Food, ColorRgb.Bytes(170, 40, 90), nutrition: 0.35f);
            Add(ItemType.Workbench, "Workbench", ItemCategory.Furniture, ColorRgb.Bytes(150, 110, 70), stack: 5);
            Add(ItemType.Bed, "Bed", ItemCategory.Furniture, ColorRgb.Bytes(200, 60, 60), stack: 5);
            Add(ItemType.Torch, "Torch", ItemCategory.Furniture, ColorRgb.Bytes(255, 190, 80), stack: 20);
            Add(ItemType.Bucket, "Bucket", ItemCategory.Tool, ColorRgb.Bytes(150, 150, 160), stack: 4);
            Add(ItemType.WaterBucket, "Water Bucket", ItemCategory.Tool, ColorRgb.Bytes(60, 120, 200), stack: 4);
        }

        private static void Add(ItemType type, string name, ItemCategory cat, ColorRgb color, float nutrition = 0f, int stack = 50)
        {
            Defs[(int)type] = new ItemDefinition
            {
                Type = type,
                Name = name,
                Category = cat,
                Color = color,
                Nutrition = nutrition,
                StackSize = stack,
            };
        }

        public static ItemDefinition Get(ItemType type)
        {
            var d = Defs[(int)type];
            if (d == null)
            {
                d = new ItemDefinition { Type = type, Name = type.ToString(), Category = ItemCategory.RawMaterial, Color = ColorRgb.White };
                Defs[(int)type] = d;
            }
            return d;
        }

        public static IEnumerable<ItemDefinition> All()
        {
            for (int i = 1; i < (int)ItemType.Count; i++)
                yield return Get((ItemType)i);
        }

        public static bool IsEdible(ItemType type) => type != ItemType.None && Get(type).Nutrition > 0f;
    }
}
