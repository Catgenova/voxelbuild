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
        Water,
        CarpentryBench,
        /// <summary>Floor layer tiles: occupy the floor plane of a cell, never a full block.</summary>
        PlankFloor,
        StoneTiles,
        Carpet,
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

    /// <summary>Procedural surface look used by the texture painter.</summary>
    public enum SurfaceStyle
    {
        Flat,
        Dirt,
        GrassTop,
        GrassSide,
        Stone,
        Sand,
        Gravel,
        Clay,
        Bedrock,
        Ore,
        Crystal,
        Bark,
        LogEnd,
        Leaves,
        Bush,
        Planks,
        Brick,
        WorkbenchTop,
        BedTop,
        Fabric,
        TorchSide,
        TorchTop,
        CarpentryTop,
        Tiles,
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
        /// <summary>Solid but see-through (crystal): rendered in the refractive submesh.</summary>
        public bool IsTranslucent;
        /// <summary>A liquid handled by the fluid simulation.</summary>
        public bool IsFluid;
        /// <summary>Lives on the floor plane of a cell (thin slab colonists walk on) rather than filling the cell.</summary>
        public bool IsFloor;
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

        public SurfaceStyle TopStyle = SurfaceStyle.Flat;
        public SurfaceStyle SideStyle = SurfaceStyle.Flat;
        public SurfaceStyle BottomStyle = SurfaceStyle.Flat;
        /// <summary>Secondary colour: ore nuggets, berries, mortar, pillow.</summary>
        public ColorRgb Accent = ColorRgb.White;
        /// <summary>Base smoothness (0 rough .. 1 mirror) before per-pixel variation.</summary>
        public float Smoothness = 0.15f;
        /// <summary>Metallic value applied to the accent areas of Ore tiles.</summary>
        public float AccentMetallic = 0f;
        public float AccentSmoothness = 0.5f;
        /// <summary>Allow the mesher to rotate top/bottom faces per block to hide repetition.</summary>
        public bool RotateTopBottom;
        /// <summary>Allow the mesher to rotate side faces per block.</summary>
        public bool RotateSides;

        public SurfaceStyle StyleFor(Direction face)
        {
            switch (face)
            {
                case Direction.PosY: return TopStyle;
                case Direction.NegY: return BottomStyle;
                default: return SideStyle;
            }
        }

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
                IsBuildable = true, BuildCost = new ItemStack(ItemType.Coal, 1),
                TopColor = ColorRgb.Bytes(95, 95, 95), SideColor = ColorRgb.Bytes(95, 95, 95), BottomColor = ColorRgb.Bytes(95, 95, 95),
                TextureNoise = 0.35f,
            });
            Add(new BlockDefinition
            {
                Type = BlockType.IronOre, Name = "Iron Ore", Category = BlockCategory.Ore, MineSeconds = 4.0f,
                Drop = new ItemStack(ItemType.IronOre, 1),
                IsBuildable = true, BuildCost = new ItemStack(ItemType.IronOre, 1),
                TopColor = ColorRgb.Bytes(170, 140, 120), SideColor = ColorRgb.Bytes(170, 140, 120), BottomColor = ColorRgb.Bytes(170, 140, 120),
                TextureNoise = 0.30f,
            });
            Add(new BlockDefinition
            {
                Type = BlockType.GoldOre, Name = "Gold Ore", Category = BlockCategory.Ore, MineSeconds = 5.0f,
                Drop = new ItemStack(ItemType.GoldOre, 1),
                IsBuildable = true, BuildCost = new ItemStack(ItemType.GoldOre, 1),
                TopColor = ColorRgb.Bytes(200, 170, 80), SideColor = ColorRgb.Bytes(200, 170, 80), BottomColor = ColorRgb.Bytes(200, 170, 80),
                TextureNoise = 0.30f,
            });
            Add(new BlockDefinition
            {
                Type = BlockType.Crystal, Name = "Crystal", Category = BlockCategory.Ore, MineSeconds = 6.0f,
                IsOpaque = false, IsTranslucent = true,
                Drop = new ItemStack(ItemType.Crystal, 1),
                IsBuildable = true, BuildCost = new ItemStack(ItemType.Crystal, 1),
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
                Drop = new ItemStack(ItemType.Planks, 4),
                IsBuildable = true, BuildCost = new ItemStack(ItemType.Planks, 4), BuildSeconds = 3.0f,
                TopColor = ColorRgb.Bytes(150, 110, 70), SideColor = ColorRgb.Bytes(120, 85, 50), BottomColor = ColorRgb.Bytes(110, 80, 45),
                TextureNoise = 0.10f,
            });
            Add(new BlockDefinition
            {
                Type = BlockType.Bed, Name = "Bed", Category = BlockCategory.Furniture, MineSeconds = 1.0f,
                Drop = new ItemStack(ItemType.Planks, 6),
                IsBuildable = true, BuildCost = new ItemStack(ItemType.Planks, 6), BuildSeconds = 3.0f,
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
            Add(new BlockDefinition
            {
                Type = BlockType.CarpentryBench, Name = "Carpentry Bench", Category = BlockCategory.Furniture, MineSeconds = 1.5f,
                Drop = new ItemStack(ItemType.Log, 4),
                IsBuildable = true, BuildCost = new ItemStack(ItemType.Log, 4), BuildSeconds = 3.0f,
                TopColor = ColorRgb.Bytes(175, 135, 85), SideColor = ColorRgb.Bytes(110, 78, 45), BottomColor = ColorRgb.Bytes(100, 72, 42),
                TextureNoise = 0.1f,
            });
            Add(new BlockDefinition
            {
                Type = BlockType.PlankFloor, Name = "Plank Floor", Category = BlockCategory.Construction, MineSeconds = 0.8f,
                IsFloor = true, IsSolid = false, IsOpaque = false,
                Drop = new ItemStack(ItemType.Planks, 1),
                IsBuildable = true, BuildCost = new ItemStack(ItemType.Planks, 1), BuildSeconds = 0.8f,
                TopColor = ColorRgb.Bytes(196, 156, 100), SideColor = ColorRgb.Bytes(170, 130, 80), BottomColor = ColorRgb.Bytes(150, 115, 70),
                TextureNoise = 0.06f,
            });
            Add(new BlockDefinition
            {
                Type = BlockType.StoneTiles, Name = "Stone Tiles", Category = BlockCategory.Construction, MineSeconds = 1.2f,
                IsFloor = true, IsSolid = false, IsOpaque = false,
                Drop = new ItemStack(ItemType.StoneBrick, 1),
                IsBuildable = true, BuildCost = new ItemStack(ItemType.StoneBrick, 1), BuildSeconds = 1.2f,
                TopColor = ColorRgb.Bytes(150, 148, 150), SideColor = ColorRgb.Bytes(120, 118, 120), BottomColor = ColorRgb.Bytes(110, 110, 112),
                TextureNoise = 0.05f,
            });
            Add(new BlockDefinition
            {
                Type = BlockType.Carpet, Name = "Carpet", Category = BlockCategory.Construction, MineSeconds = 0.4f,
                IsFloor = true, IsSolid = false, IsOpaque = false,
                Drop = new ItemStack(ItemType.Carpet, 1),
                IsBuildable = true, BuildCost = new ItemStack(ItemType.Carpet, 1), BuildSeconds = 0.5f,
                TopColor = ColorRgb.Bytes(170, 50, 60), SideColor = ColorRgb.Bytes(140, 40, 50), BottomColor = ColorRgb.Bytes(120, 40, 45),
                TextureNoise = 0.05f,
            });
            Add(new BlockDefinition
            {
                Type = BlockType.Water, Name = "Water", Category = BlockCategory.Natural,
                IsSolid = false, IsOpaque = false, IsFluid = true, IsMinable = false,
                TopColor = ColorRgb.Bytes(40, 90, 140), SideColor = ColorRgb.Bytes(40, 90, 140), BottomColor = ColorRgb.Bytes(40, 90, 140),
                TextureNoise = 0.03f,
            });
        }

        public static bool IsFluid(BlockType t) => Get(t).IsFluid;
        public static bool IsFloor(BlockType t) => Get(t).IsFloor;

        private static void Add(BlockDefinition def)
        {
            ApplyStyle(def);
            Defs[(int)def.Type] = def;
        }

        /// <summary>Surface look per block. Kept in one place so the art direction is easy to read and tweak.</summary>
        private static void ApplyStyle(BlockDefinition d)
        {
            switch (d.Type)
            {
                case BlockType.Bedrock:
                    Set(d, SurfaceStyle.Bedrock, SurfaceStyle.Bedrock, SurfaceStyle.Bedrock, 0.2f, true, true); break;
                case BlockType.Stone:
                    Set(d, SurfaceStyle.Stone, SurfaceStyle.Stone, SurfaceStyle.Stone, 0.22f, true, true); break;
                case BlockType.Dirt:
                    Set(d, SurfaceStyle.Dirt, SurfaceStyle.Dirt, SurfaceStyle.Dirt, 0.08f, true, true); break;
                case BlockType.Grass:
                    Set(d, SurfaceStyle.GrassTop, SurfaceStyle.GrassSide, SurfaceStyle.Dirt, 0.12f, true, false);
                    d.Accent = ColorRgb.Bytes(121, 85, 58); break;
                case BlockType.Sand:
                    Set(d, SurfaceStyle.Sand, SurfaceStyle.Sand, SurfaceStyle.Sand, 0.1f, true, true); break;
                case BlockType.Gravel:
                    Set(d, SurfaceStyle.Gravel, SurfaceStyle.Gravel, SurfaceStyle.Gravel, 0.25f, true, true); break;
                case BlockType.Clay:
                    Set(d, SurfaceStyle.Clay, SurfaceStyle.Clay, SurfaceStyle.Clay, 0.45f, true, true); break;
                case BlockType.CoalOre:
                    Set(d, SurfaceStyle.Ore, SurfaceStyle.Ore, SurfaceStyle.Ore, 0.22f, true, true);
                    d.Accent = ColorRgb.Bytes(28, 28, 30); d.AccentMetallic = 0.1f; d.AccentSmoothness = 0.65f; break;
                case BlockType.IronOre:
                    Set(d, SurfaceStyle.Ore, SurfaceStyle.Ore, SurfaceStyle.Ore, 0.22f, true, true);
                    d.Accent = ColorRgb.Bytes(205, 150, 110); d.AccentMetallic = 0.95f; d.AccentSmoothness = 0.6f; break;
                case BlockType.GoldOre:
                    Set(d, SurfaceStyle.Ore, SurfaceStyle.Ore, SurfaceStyle.Ore, 0.22f, true, true);
                    d.Accent = ColorRgb.Bytes(255, 205, 80); d.AccentMetallic = 1f; d.AccentSmoothness = 0.88f; break;
                case BlockType.Crystal:
                    Set(d, SurfaceStyle.Crystal, SurfaceStyle.Crystal, SurfaceStyle.Crystal, 0.9f, true, true);
                    d.Accent = ColorRgb.Bytes(200, 245, 255); d.AccentMetallic = 0.15f; d.AccentSmoothness = 0.95f; break;
                case BlockType.Log:
                    Set(d, SurfaceStyle.LogEnd, SurfaceStyle.Bark, SurfaceStyle.LogEnd, 0.2f, true, false);
                    d.Accent = ColorRgb.Bytes(70, 48, 28); break;
                case BlockType.Leaves:
                    Set(d, SurfaceStyle.Leaves, SurfaceStyle.Leaves, SurfaceStyle.Leaves, 0.3f, true, true); break;
                case BlockType.BerryBush:
                    Set(d, SurfaceStyle.Bush, SurfaceStyle.Bush, SurfaceStyle.Leaves, 0.3f, true, true);
                    d.Accent = ColorRgb.Bytes(190, 40, 80); d.AccentSmoothness = 0.7f; break;
                case BlockType.Planks:
                    Set(d, SurfaceStyle.Planks, SurfaceStyle.Planks, SurfaceStyle.Planks, 0.35f, false, false);
                    d.Accent = ColorRgb.Bytes(120, 90, 55); break;
                case BlockType.StoneBrick:
                    Set(d, SurfaceStyle.Brick, SurfaceStyle.Brick, SurfaceStyle.Brick, 0.3f, false, false);
                    d.Accent = ColorRgb.Bytes(150, 150, 150); break;
                case BlockType.Workbench:
                    Set(d, SurfaceStyle.WorkbenchTop, SurfaceStyle.Planks, SurfaceStyle.Planks, 0.3f, false, false);
                    d.Accent = ColorRgb.Bytes(170, 170, 175); d.AccentMetallic = 0.9f; d.AccentSmoothness = 0.7f; break;
                case BlockType.Bed:
                    Set(d, SurfaceStyle.BedTop, SurfaceStyle.Planks, SurfaceStyle.Planks, 0.15f, false, false);
                    d.Accent = ColorRgb.Bytes(235, 230, 220); break;
                case BlockType.Torch:
                    Set(d, SurfaceStyle.TorchTop, SurfaceStyle.TorchSide, SurfaceStyle.Bark, 0.3f, false, false);
                    d.Accent = ColorRgb.Bytes(255, 170, 60); break;
                case BlockType.CarpentryBench:
                    Set(d, SurfaceStyle.CarpentryTop, SurfaceStyle.Bark, SurfaceStyle.LogEnd, 0.3f, false, false);
                    d.Accent = ColorRgb.Bytes(190, 190, 200); d.AccentMetallic = 0.95f; d.AccentSmoothness = 0.75f; break;
                case BlockType.PlankFloor:
                    Set(d, SurfaceStyle.Planks, SurfaceStyle.Planks, SurfaceStyle.Planks, 0.4f, false, false);
                    d.Accent = ColorRgb.Bytes(120, 90, 55); break;
                case BlockType.StoneTiles:
                    Set(d, SurfaceStyle.Tiles, SurfaceStyle.Stone, SurfaceStyle.Stone, 0.45f, true, false);
                    d.Accent = ColorRgb.Bytes(90, 88, 90); break;
                case BlockType.Carpet:
                    Set(d, SurfaceStyle.Fabric, SurfaceStyle.Fabric, SurfaceStyle.Fabric, 0.1f, true, false);
                    d.Accent = ColorRgb.Bytes(230, 200, 120); break;
                default:
                    break;
            }
        }

        private static void Set(BlockDefinition d, SurfaceStyle top, SurfaceStyle side, SurfaceStyle bottom, float smoothness, bool rotTopBottom, bool rotSides)
        {
            d.TopStyle = top;
            d.SideStyle = side;
            d.BottomStyle = bottom;
            d.Smoothness = smoothness;
            d.RotateTopBottom = rotTopBottom;
            d.RotateSides = rotSides;
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
