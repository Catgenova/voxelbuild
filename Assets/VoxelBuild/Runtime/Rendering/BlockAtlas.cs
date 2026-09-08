using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace VoxelBuild.Rendering
{
    using VoxelBuild.Core;
    using VoxelBuild.World;

    /// <summary>Generates the block texture atlas and the HDRP materials used by chunks, items and overlays.</summary>
    public sealed class BlockAtlas
    {
        public Texture2D Albedo { get; private set; }
        public Texture2D Emissive { get; private set; }
        public Material TerrainMaterial { get; private set; }

        public static Color ToColor(ColorRgb c) => new Color(c.r, c.g, c.b, 1f);

        public static BlockAtlas Create()
        {
            AtlasLayout.EnsureInit();
            var atlas = new BlockAtlas();
            int per = AtlasLayout.TilesPerRow;
            int size = per * AtlasLayout.TilePixels;

            var albedo = new Texture2D(size, size, TextureFormat.RGBA32, true, false) { name = "BlockAtlas" };
            var emissive = new Texture2D(size, size, TextureFormat.RGBA32, true, true) { name = "BlockAtlasEmissive" };
            var albedoPixels = new Color32[size * size];
            var emissivePixels = new Color32[size * size];
            bool anyEmissive = false;

            for (int t = 0; t < AtlasLayout.TileCount; t++)
            {
                var tile = AtlasLayout.GetTile(t);
                int tx = (t % per) * AtlasLayout.TilePixels;
                int ty = (t / per) * AtlasLayout.TilePixels;
                var baseCol = ToColor(tile.Color);
                var emit = ToColor(tile.Emissive);
                if (!tile.Emissive.IsBlack) anyEmissive = true;
                int period = AtlasLayout.TilePixels / 2;
                for (int y = 0; y < AtlasLayout.TilePixels; y++)
                    for (int x = 0; x < AtlasLayout.TilePixels; x++)
                    {
                        // The noise repeats every half tile so the padded border mirrors the inner face.
                        float n = Noise.Hash(x % period, y % period, t, 7) - 0.5f;
                        float shade = 1f + n * tile.Noise * 2f;
                        var c = baseCol * shade;
                        c.a = 1f;
                        int i = (ty + y) * size + tx + x;
                        albedoPixels[i] = c;
                        float glow = tile.Emissive.IsBlack ? 0f : 0.7f + 0.3f * Noise.Hash(x % period, y % period, t, 9);
                        var e = new Color(Mathf.Min(1f, emit.r * glow), Mathf.Min(1f, emit.g * glow), Mathf.Min(1f, emit.b * glow), 1f);
                        emissivePixels[i] = e;
                    }
            }

            albedo.SetPixels32(albedoPixels);
            albedo.filterMode = FilterMode.Trilinear;
            albedo.anisoLevel = 4;
            albedo.wrapMode = TextureWrapMode.Clamp;
            albedo.Apply(true, false);

            emissive.SetPixels32(emissivePixels);
            emissive.filterMode = FilterMode.Bilinear;
            emissive.wrapMode = TextureWrapMode.Clamp;
            emissive.Apply(true, false);

            atlas.Albedo = albedo;
            atlas.Emissive = emissive;

            var mat = CreateLitMaterial("Terrain");
            mat.SetTexture("_BaseColorMap", albedo);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0.15f);
            mat.SetFloat("_Metallic", 0f);
            if (anyEmissive)
            {
                mat.SetTexture("_EmissiveColorMap", emissive);
                mat.SetColor("_EmissiveColor", Color.white * 2.5f);
                mat.SetFloat("_EmissiveExposureWeight", 0.15f);
                mat.EnableKeyword("_EMISSIVE_COLOR_MAP");
            }
            HDMaterial.ValidateMaterial(mat);
            atlas.TerrainMaterial = mat;
            return atlas;
        }

        /// <summary>A fresh HDRP Lit material, cloned from the pipeline default so the shader is always available in builds.</summary>
        public static Material CreateLitMaterial(string name)
        {
            Material mat = null;
            var pipeline = GraphicsSettings.currentRenderPipeline;
            if (pipeline != null && pipeline.defaultMaterial != null)
                mat = new Material(pipeline.defaultMaterial);
            if (mat == null)
            {
                var shader = Shader.Find("HDRP/Lit");
                mat = new Material(shader != null ? shader : Shader.Find("Standard"));
            }
            mat.name = name;
            return mat;
        }

        /// <summary>Flat-coloured lit material (colonists, item piles).</summary>
        public static Material CreateSolidMaterial(string name, Color color, float smoothness = 0.3f)
        {
            var mat = CreateLitMaterial(name);
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", 0f);
            HDMaterial.ValidateMaterial(mat);
            return mat;
        }

        /// <summary>
        /// Material that renders a constant colour regardless of scene exposure, for overlays and outlines.
        /// Uses HDRP emissive with exposure weight 0 so it stays legible at noon and at night.
        /// </summary>
        public static Material CreateOverlayMaterial(string name, Color color)
        {
            var mat = CreateLitMaterial(name);
            mat.SetColor("_BaseColor", Color.black);
            mat.SetFloat("_Smoothness", 0f);
            mat.SetFloat("_Metallic", 0f);
            mat.SetColor("_EmissiveColor", color);
            mat.SetFloat("_EmissiveExposureWeight", 0f);
            mat.SetFloat("_UseEmissiveIntensity", 0f);
            HDMaterial.ValidateMaterial(mat);
            return mat;
        }
    }
}
