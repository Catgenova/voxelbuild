using System;
using System.Collections.Generic;

namespace VoxelBuild.Core
{
    /// <summary>Every block type that can exist in the voxel world.</summary>
    public enum BlockType : ushort
    {
        Air = 0,
        Bedrock,
        Stone,
        Dirt,
        Grass,
        Sand,
        Gravel,
        Clay,
        CoalOre,
        IronOre,
        GoldOre,
        Crystal,
        Log,
        Leaves,
        BerryBush,
        Planks,
        StoneBrick,
        Workbench,
        Bed,
        Torch,
        Count,
    }

    public enum BlockCategory
    {
        Natural,
        Ore,
        Plant,
        Construction,
        Furniture,
    }

    public sealed class BlockDefinition
    {
        public BlockType Type;
        public string Name;
        public BlockCategory Category;

        /// <summary>Blocks movement and supports standing on.</summary>
        public bool IsSolid = true;
        /// <summary>Hides neighbouring faces when rendering.</summary>
        public bool IsOpaque = true;
        /// <summary>Whether it can be mined at all (bedrock cannot).</summary>
        public bool IsMinable = true;
        /// <summary>Work seconds required to mine it.</summary>
        public float MineSeconds = 1.5f;
        /// <summary>What mining yields.</summary>
        public ItemStack Drop;
        /// <summary>Whether a player may designate this block for building.</summary>
        public bool IsBuildable;
        /// <summary>Item consumed to build the block.</summary>
        public ItemStack BuildCost;
        /// <summary>Work seconds required to place it.</summary>
        public float BuildSeconds = 1.0f;

        public ColorRgb TopColor;
        public ColorRgb SideColor;
        public ColorRgb BottomColor;
        /// <summary>Amount of per-pixel brightness noise in the generated texture tile.</summary>
        public float TextureNoise = 0.08f;
        /// <summary>HDR emissive colour (black = none).</summary>
        public ColorRgb Emissive = ColorRgb.Black;

        // Atlas tile indices, assigned by AtlasLayout.
        public int TileTop = -1;
        public int TileSide = -1;
        public int TileBottom = -1;

        public bool IsEmissive => !Emissive.IsBlack;

        public int TileFor(Direction face)
        {
            switch (face)
            {
                case Direction.PosY: return TileTop;
                case Direction.NegY: return TileBottom;
                default: return TileSide;
            }
        }
    }

    public static class BlockRegistry
    {
        private static readonly BlockDefinition[] Defs = new BlockDefinition[(int)BlockType.Count];

        static BlockRegistry()
        {
            Defs[(int)BlockType.Air] = new BlockDefinition
            {
                Type = BlockType.Air, Name = "Air", IsSolid = false, IsOpaque = false, IsMinable = false,
            };

            Add(new BlockDefinition
            {
                Type = BlockType.Bedrock, Name = "Bedrock", Category = BlockCategory.Natural, IsMinable = false,
                TopColor = ColorRgb.Bytes(40, 40, 45), SideColor = ColorRgb.Bytes(40, 40, 45), BottomColor = ColorRgb.Bytes(40, 40, 45),
                TextureNoise = 0.15f,
            });
            Add(new BlockDefinition
            {
                Type = BlockType.Stone, Name = "Stone", Category = BlockCategory.Natural, MineSeconds = 2.5f,
                Drop = new ItemStack(ItemType.Stone, 1),
                IsBuildable = true, BuildCost = new ItemStack(ItemType.Stone, 1),
                TopColor = ColorRgb.Bytes(128, 128, 130), SideColor = ColorRgb.Bytes(120, 120, 122), BottomColor = ColorRgb.Bytes(110, 110, 112),
                TextureNoise = 0.10f,
            });
            Add(new BlockDefinition
            {
                Type = BlockType.Dirt, Name = "Dirt", Category = BlockCategory.Natural, MineSeconds = 1.0f,
                Drop = new ItemStack(ItemType.Dirt, 1),
                IsBuildable = true, BuildCost = new ItemStack(ItemType.Dirt, 1),
                TopColor = ColorRgb.Bytes(121, 85, 58), SideColor = ColorRgb.Bytes(121, 85, 58), BottomColor = ColorRgb.Bytes(110, 76, 50),
            });
            Add(new BlockDefinition
            {
                Type = BlockType.Grass, Name = "Grass", Category = BlockCategory.Natural, MineSeconds = 1.0f,
                Drop = new ItemStack(ItemType.Dirt, 1),
                TopColor = ColorRgb.Bytes(95, 160, 60), SideColor = ColorRgb.Bytes(121, 95, 58), BottomColor = ColorRgb.Bytes(110, 76, 50),
                TextureNoise = 0.10f,
            });
            Add(new BlockDefinition
            {
                Type = BlockType.Sand, Name = "Sand", Category = BlockCategory.Natural, MineSeconds = 0.8f,
                Drop = new ItemStack(ItemType.Sand, 1),
                IsBuildable = true, BuildCost = new ItemStack(ItemType.Sand, 1),
                TopColor = ColorRgb.Bytes(219, 205, 150), SideColor = ColorRgb.Bytes(212, 198, 142), BottomColor = ColorRgb.Bytes(205, 190, 135),
                TextureNoise = 0.05f,
            });
            Add(new BlockDefinition
            {
                Type = BlockType.Gravel, Name = "Gravel", Category = BlockCategory.Natural, MineSeconds = 1.2f,
                Drop = new ItemStack(ItemType.Gravel, 1),
                IsBuildable = true, BuildCost = new ItemStack(ItemType.Gravel, 1),
                TopColor = ColorRgb.Bytes(140, 135, 130), SideColor = ColorRgb.Bytes(135, 130, 125), BottomColor = ColorRgb.Bytes(130, 125, 120),
                TextureNoise = 0.18f,
            });
            Add(new BlockDefinition
            {
                Type = BlockType.Clay, Name = "Clay", Category = BlockCategory.Natural, MineSeconds = 1.2f,
                Drop = new ItemStack(ItemType.Clay, 1),
                IsBuildable = true, BuildCost = new ItemStack(ItemType.Clay, 1),
                TopColor = ColorRgb.Bytes(160, 160, 175), SideColor = ColorRgb.Bytes(155, 155, 170), BottomColor = ColorRgb.Bytes(150, 150, 165),
                TextureNoise = 0.04f,
            });
            Add(new BlockDefinition
            {
                Type = BlockType.CoalOre, Name = "Coal Ore", Category = BlockCategory.Ore, MineSeconds = 3.0f,
                Drop = new ItemStack(ItemType.Coal, 1),
                TopColor = ColorRgb.Bytes(95, 95, 95), SideColor = ColorRgb.Bytes(95, 95, 95), BottomColor = ColorRgb.Bytes(95, 95, 95),
                TextureNoise = 0.35f,
            });
            Add(new BlockDefinition
            {
                Type = BlockType.IronOre, Name = "Iron Ore", Category = BlockCategory.Ore, MineSeconds = 4.0f,
                Drop = new ItemStack(ItemType.IronOre, 1),
                TopColor = ColorRgb.Bytes(170, 140, 120), SideColor = ColorRgb.Bytes(170, 140, 120), BottomColor = ColorRgb.Bytes(170, 140, 120),
                TextureNoise = 0.30f,
            });
            Add(new BlockDefinition
            {
                Type = BlockType.GoldOre, Name = "Gold Ore", Category = BlockCategory.Ore, MineSeconds = 5.0f,
                Drop = new ItemStack(ItemType.GoldOre, 1),
                TopColor = ColorRgb.Bytes(200, 170, 80), SideColor = ColorRgb.Bytes(200, 170, 80), BottomColor = ColorRgb.Bytes(200, 170, 80),
                TextureNoise = 0.30f,
            });
            Add(new BlockDefinition
            {
                Type = BlockType.Crystal, Name = "Crystal", Category = BlockCategory.Ore, MineSeconds = 6.0f,
                Drop = new ItemStack(ItemType.Crystal, 1),
                TopColor = ColorRgb.Bytes(120, 220, 255), SideColor = ColorRgb.Bytes(120, 220, 255), BottomColor = ColorRgb.Bytes(120, 220, 255),
                TextureNoise = 0.20f,
                Emissive = new ColorRgb(0.25f, 0.6f, 0.9f),
            });
            Add(new BlockDefinition
            {
                Type = BlockType.Log, Name = "Log", Category = BlockCategory.Plant, MineSeconds = 2.0f,
                Drop = new ItemStack(ItemType.Log, 1),
                IsBuildable = true, BuildCost = new ItemStack(ItemType.Log, 1),
                TopColor = ColorRgb.Bytes(170, 140, 95), SideColor = ColorRgb.Bytes(100, 72, 42), BottomColor = ColorRgb.Bytes(170, 140, 95),
                TextureNoise = 0.12f,
            });
            Add(new BlockDefinition
            {
                Type = BlockType.Leaves, Name = "Leaves", Category = BlockCategory.Plant, MineSeconds = 0.4f,
                Drop = new ItemStack(ItemType.None, 0),
                TopColor = ColorRgb.Bytes(60, 130, 50), SideColor = ColorRgb.Bytes(55, 120, 45), BottomColor = ColorRgb.Bytes(50, 110, 40),
                TextureNoise = 0.20f,
            });
            Add(new BlockDefinition
            {
                Type = BlockType.BerryBush, Name = "Berry Bush", Category = BlockCategory.Plant, MineSeconds = 0.8f,
                Drop = new ItemStack(ItemType.Berries, 4),
                TopColor = ColorRgb.Bytes(70, 120, 55), SideColor = ColorRgb.Bytes(80, 120, 60), BottomColor = ColorRgb.Bytes(70, 110, 50),
                TextureNoise = 0.30f,
            });
            Add(new BlockDefinition
            {
                Type = BlockType.Planks, Name = "Planks", Category = BlockCategory.Construction, MineSeconds = 1.2f,
                Drop = new ItemStack(ItemType.Planks, 1),
                IsBuildable = true, BuildCost = new ItemStack(ItemType.Planks, 1),
                TopColor = ColorRgb.Bytes(190, 150, 95), SideColor = ColorRgb.Bytes(185, 145, 90), BottomColor = ColorRgb.Bytes(180, 140, 85),
                TextureNoise = 0.06f,
            });
            Add(new BlockDefinition
            {
                Type = BlockType.StoneBrick, Name = "Stone Brick", Category = BlockCategory.Construction, MineSeconds = 3.0f,
                Drop = new ItemStack(ItemType.StoneBrick, 1),
                IsBuildable = true, BuildCost = new ItemStack(ItemType.StoneBrick, 1), BuildSeconds = 1.5f,
                TopColor = ColorRgb.Bytes(115, 115, 120), SideColor = ColorRgb.Bytes(105, 105, 110), BottomColor = ColorRgb.Bytes(100, 100, 105),
                TextureNoise = 0.05f,
            });
            Add(new BlockDefinition
            {
                Type = BlockType.Workbench, Name = "Workbench", Category = BlockCategory.Furniture, MineSeconds = 1.5f,
                Drop = new ItemStack(ItemType.Workbench, 1),
                IsBuildable = true, BuildCost = new ItemStack(ItemType.Workbench, 1), BuildSeconds = 2.0f,
                TopColor = ColorRgb.Bytes(150, 110, 70), SideColor = ColorRgb.Bytes(120, 85, 50), BottomColor = ColorRgb.Bytes(110, 80, 45),
                TextureNoise = 0.10f,
            });
            Add(new BlockDefinition
            {
                Type = BlockType.Bed, Name = "Bed", Category = BlockCategory.Furniture, MineSeconds = 1.0f,
                Drop = new ItemStack(ItemType.Bed, 1),
                IsBuildable = true, BuildCost = new ItemStack(ItemType.Bed, 1), BuildSeconds = 2.0f,
                TopColor = ColorRgb.Bytes(200, 60, 60), SideColor = ColorRgb.Bytes(160, 120, 80), BottomColor = ColorRgb.Bytes(140, 100, 60),
                TextureNoise = 0.05f,
            });
            Add(new BlockDefinition
            {
                Type = BlockType.Torch, Name = "Torch", Category = BlockCategory.Furniture, MineSeconds = 0.3f,
                IsSolid = false, IsOpaque = false,
                Drop = new ItemStack(ItemType.Torch, 1),
                IsBuildable = true, BuildCost = new ItemStack(ItemType.Torch, 1), BuildSeconds = 0.5f,
                TopColor = ColorRgb.Bytes(255, 200, 90), SideColor = ColorRgb.Bytes(200, 140, 70), BottomColor = ColorRgb.Bytes(120, 90, 50),
                TextureNoise = 0.10f,
                Emissive = new ColorRgb(1.2f, 0.7f, 0.25f),
            });
        }

        private static void Add(BlockDefinition def)
        {
            Defs[(int)def.Type] = def;
        }

        public static BlockDefinition Get(BlockType type)
        {
            var d = Defs[(int)type];
            return d ?? Defs[(int)BlockType.Air];
        }

        public static IEnumerable<BlockDefinition> All()
        {
            for (int i = 0; i < (int)BlockType.Count; i++)
                if (Defs[i] != null) yield return Defs[i];
        }

        public static IEnumerable<BlockDefinition> Buildable()
        {
            foreach (var d in All())
                if (d.IsBuildable) yield return d;
        }

        public static bool IsSolid(BlockType t) => Get(t).IsSolid;
        public static bool IsOpaque(BlockType t) => Get(t).IsOpaque;
    }
}
