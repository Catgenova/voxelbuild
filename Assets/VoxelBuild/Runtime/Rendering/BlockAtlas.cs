using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace VoxelBuild.Rendering
{
    using VoxelBuild.Core;
    using VoxelBuild.World;

    /// <summary>
    /// Packs the procedurally painted block faces into PBR atlases (albedo, tangent-space normal, HDRP mask, emissive)
    /// and builds the HDRP Lit materials used by chunks, items and overlays.
    /// </summary>
    public sealed class BlockAtlas
    {
        public Texture2D Albedo { get; private set; }
        public Texture2D Normal { get; private set; }
        public Texture2D Mask { get; private set; }
        public Texture2D Emissive { get; private set; }
        public Texture2D Height { get; private set; }
        public Material TerrainMaterial { get; private set; }

        public static Color ToColor(ColorRgb c) => new Color(c.r, c.g, c.b, 1f);

        /// <summary>Options for the terrain material.</summary>
        public struct Options
        {
            /// <summary>Enable HDRP pixel displacement (parallax occlusion mapping) on block faces.</summary>
            public bool ParallaxOcclusion;
            /// <summary>How deep the relief appears to sink into the face, in metres.</summary>
            public float ReliefDepth;
            /// <summary>Ray-march sample bounds; more samples cost more but avoid stepping artefacts at grazing angles.</summary>
            public int MinSamples;
            public int MaxSamples;

            public static Options Default => new Options { ParallaxOcclusion = true, ReliefDepth = 0.015f, MinSamples = 6, MaxSamples = 24 };
        }

        public static BlockAtlas Create() => Create(Options.Default);

        public static BlockAtlas Create(Options options)
        {
            AtlasLayout.EnsureInit();
            var atlas = new BlockAtlas();
            int per = AtlasLayout.TilesPerRow;
            int tile = AtlasLayout.TilePixels;
            int face = AtlasLayout.FacePixels;
            int size = per * tile;

            var albedo = new Color32[size * size];
            var normal = new Color32[size * size];
            var mask = new Color32[size * size];
            var emissive = new Color32[size * size];
            var height = new Color32[size * size];
            bool anyEmissive = false;

            for (int t = 0; t < AtlasLayout.TileCount; t++)
            {
                var info = AtlasLayout.GetTile(t);
                var px = TilePainter.Paint(info, t, face);
                var normals = ComputeNormals(px);
                if (!info.Emissive.IsBlack) anyEmissive = true;

                int tx = (t % per) * tile;
                int ty = (t / per) * tile;
                int pad = (tile - face) / 2;
                for (int y = 0; y < tile; y++)
                    for (int x = 0; x < tile; x++)
                    {
                        // The face occupies the inner region; the border repeats it so mipmaps never sample a neighbour.
                        int fx = ((x - pad) % face + face) % face;
                        int fy = ((y - pad) % face + face) % face;
                        int src = px.Index(fx, fy);
                        int dst = (ty + y) * size + tx + x;
                        var a = px.Albedo[src];
                        albedo[dst] = new Color(Mathf.Min(1f, a.r), Mathf.Min(1f, a.g), Mathf.Min(1f, a.b), 1f);
                        normal[dst] = normals[src];
                        mask[dst] = new Color(px.Metallic[src], px.AO[src], 0f, px.Smoothness[src]);
                        var e = px.Emissive[src];
                        emissive[dst] = new Color(Mathf.Min(1f, e.r), Mathf.Min(1f, e.g), Mathf.Min(1f, e.b), 1f);
                        float h = px.Height[src];
                        height[dst] = new Color(h, h, h, 1f);
                    }
            }

            atlas.Albedo = MakeTexture("BlockAtlas", size, albedo, false, FilterMode.Trilinear);
            atlas.Normal = MakeTexture("BlockAtlasNormal", size, normal, true, FilterMode.Trilinear);
            atlas.Mask = MakeTexture("BlockAtlasMask", size, mask, true, FilterMode.Trilinear);
            atlas.Emissive = MakeTexture("BlockAtlasEmissive", size, emissive, true, FilterMode.Bilinear);
            atlas.Height = MakeTexture("BlockAtlasHeight", size, height, true, FilterMode.Trilinear);

            var mat = CreateLitMaterial("Terrain");
            mat.SetTexture("_BaseColorMap", atlas.Albedo);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetTexture("_NormalMap", atlas.Normal);
            mat.SetFloat("_NormalScale", 1f);
            mat.SetTexture("_MaskMap", atlas.Mask);
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Smoothness", 1f);
            mat.SetFloat("_MetallicRemapMin", 0f);
            mat.SetFloat("_MetallicRemapMax", 1f);
            mat.SetFloat("_SmoothnessRemapMin", 0f);
            mat.SetFloat("_SmoothnessRemapMax", 1f);
            mat.SetFloat("_AORemapMin", 0f);
            mat.SetFloat("_AORemapMax", 1f);
            mat.EnableKeyword("_NORMALMAP");
            mat.EnableKeyword("_MASKMAP");
            if (options.ParallaxOcclusion) EnableParallax(mat, atlas.Height, options, per);
            if (anyEmissive)
            {
                mat.SetTexture("_EmissiveColorMap", atlas.Emissive);
                mat.SetColor("_EmissiveColor", Color.white * 3f);
                mat.SetFloat("_EmissiveExposureWeight", 0.2f);
                mat.EnableKeyword("_EMISSIVE_COLOR_MAP");
            }
            HDMaterial.ValidateMaterial(mat);
            atlas.TerrainMaterial = mat;
            return atlas;
        }

        /// <summary>
        /// HDRP pixel displacement (parallax occlusion mapping). The relief is recessed into the face (height centre = 1)
        /// so block silhouettes stay intact and the ray march never leaves the padded tile.
        /// </summary>
        private static void EnableParallax(Material mat, Texture2D heightMap, Options options, int tilesPerRow)
        {
            mat.SetTexture("_HeightMap", heightMap);
            mat.SetFloat("_DisplacementMode", 2f);          // 0 none, 1 vertex, 2 pixel
            mat.SetFloat("_DisplacementLockObjectScale", 1f);
            mat.SetFloat("_DisplacementLockTilingScale", 1f);
            mat.SetFloat("_DepthOffsetEnable", 0f);
            mat.SetFloat("_HeightMapParametrization", 1f);  // amplitude mode
            mat.SetFloat("_HeightAmplitude", Mathf.Max(0.001f, options.ReliefDepth));
            mat.SetFloat("_HeightCenter", 1f);
            mat.SetFloat("_HeightPoMAmplitude", options.ReliefDepth * 100f); // inspector mirror, in cm
            mat.SetFloat("_HeightOffset", 0f);
            mat.SetFloat("_PPDMinSamples", Mathf.Max(1, options.MinSamples));
            mat.SetFloat("_PPDMaxSamples", Mathf.Max(options.MinSamples, options.MaxSamples));
            mat.SetFloat("_PPDLodThreshold", 5f);
            // World size covered by the 0..1 UV range: one face is Scale.BlockSize and the atlas holds tilesPerRow tiles,
            // each of which shows two faces' worth of pattern (face plus padding).
            float uvSpanMetres = tilesPerRow * 2f * Scale.BlockSize;
            mat.SetFloat("_PPDPrimitiveLength", uvSpanMetres);
            mat.SetFloat("_PPDPrimitiveWidth", uvSpanMetres);
            mat.SetVector("_InvPrimScale", new Vector4(1f / uvSpanMetres, 1f / uvSpanMetres, 0f, 0f));
            mat.EnableKeyword("_HEIGHTMAP");
            mat.EnableKeyword("_PIXEL_DISPLACEMENT");
            mat.EnableKeyword("_PIXEL_DISPLACEMENT_LOCK_OBJECT_SCALE");
        }

        private static Texture2D MakeTexture(string name, int size, Color32[] pixels, bool linear, FilterMode filter)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, linear) { name = name };
            tex.SetPixels32(pixels);
            tex.filterMode = filter;
            tex.anisoLevel = 8;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.Apply(true, false);
            return tex;
        }

        /// <summary>Tangent-space normals from the tile's height field (wrapping, so tiles stay seamless).</summary>
        private static Color32[] ComputeNormals(TilePixels px)
        {
            int n = px.Size;
            var result = new Color32[n * n];
            // A full height step across ~1/10 of the face gives a 45 degree slope at strength 1.
            float k = n * 0.1f * px.NormalStrength;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float hl = px.Height[px.Index((x - 1 + n) % n, y)];
                    float hr = px.Height[px.Index((x + 1) % n, y)];
                    float hd = px.Height[px.Index(x, (y - 1 + n) % n)];
                    float hu = px.Height[px.Index(x, (y + 1) % n)];
                    var nrm = new Vector3(-(hr - hl) * 0.5f * k, -(hu - hd) * 0.5f * k, 1f).normalized;
                    result[px.Index(x, y)] = new Color(nrm.x * 0.5f + 0.5f, nrm.y * 0.5f + 0.5f, nrm.z * 0.5f + 0.5f, 1f);
                }
            return result;
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
        public static Material CreateSolidMaterial(string name, Color color, float smoothness = 0.3f, float metallic = 0f)
        {
            var mat = CreateLitMaterial(name);
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", metallic);
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
