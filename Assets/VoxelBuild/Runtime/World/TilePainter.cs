using System;

namespace VoxelBuild.World
{
    using VoxelBuild.Core;

    /// <summary>Per-pixel PBR data for one block face, produced by <see cref="TilePainter"/>.</summary>
    public sealed class TilePixels
    {
        public readonly int Size;
        public readonly ColorRgb[] Albedo;
        public readonly float[] Height;
        public readonly float[] Metallic;
        public readonly float[] Smoothness;
        public readonly float[] AO;
        public readonly ColorRgb[] Emissive;
        /// <summary>How strongly the height field bends normals.</summary>
        public float NormalStrength = 1f;

        public TilePixels(int size)
        {
            Size = size;
            int n = size * size;
            Albedo = new ColorRgb[n];
            Height = new float[n];
            Metallic = new float[n];
            Smoothness = new float[n];
            AO = new float[n];
            Emissive = new ColorRgb[n];
        }

        public int Index(int x, int y) => y * Size + x;
    }

    /// <summary>
    /// Paints tileable procedural surfaces: chunky dirt with pebbles, cracked stone, glossy metal nuggets in ore,
    /// faceted glowing crystal, bark, planks, bricks and so on. Engine independent; the renderer packs the result
    /// into albedo, normal, mask and emissive atlases.
    /// </summary>
    public static class TilePainter
    {
        private struct Sample
        {
            public ColorRgb Albedo;
            public float Height;
            public float Metallic;
            public float Smoothness;
            public ColorRgb Emissive;
        }

        public static TilePixels Paint(AtlasLayout.Tile tile, int tileIndex, int size = AtlasLayout.FacePixels)
        {
            var px = new TilePixels(size);
            int seed = tileIndex * 131 + 17;
            var def = tile.Definition;
            px.NormalStrength = StrengthFor(tile.Style);

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
                    var s = Shade(tile.Style, u, v, seed, tile, def);
                    int i = px.Index(x, y);
                    px.Albedo[i] = ClampColor(s.Albedo);
                    px.Height[i] = Clamp01(s.Height);
                    px.Metallic[i] = Clamp01(s.Metallic);
                    px.Smoothness[i] = Clamp01(s.Smoothness);
                    px.Emissive[i] = ClampColor(s.Emissive);
                }

            ComputeAmbientOcclusion(px);
            return px;
        }

        /// <summary>A tileable ripple height field for the water surface normal map.</summary>
        public static TilePixels PaintWater(int size, int seed = 4242)
        {
            var px = new TilePixels(size) { NormalStrength = 0.9f };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
                    float broad = Fbm(u, v, 3, seed, 2);
                    float ripples = Fbm(u, v, 12, seed + 3, 3);
                    float chop = Aniso(u, v, 24, 6, seed + 9, 2);
                    int i = px.Index(x, y);
                    px.Height[i] = Clamp01(0.5f + 0.3f * (broad - 0.5f) + 0.35f * (ripples - 0.5f) + 0.2f * (chop - 0.5f));
                    px.Albedo[i] = ColorRgb.White;
                    px.Smoothness[i] = 0.95f;
                    px.AO[i] = 1f;
                }
            return px;
        }

        private static float StrengthFor(SurfaceStyle style)
        {
            switch (style)
            {
                case SurfaceStyle.Dirt: return 2.4f;
                case SurfaceStyle.GrassTop: return 1.6f;
                case SurfaceStyle.GrassSide: return 2.2f;
                case SurfaceStyle.Stone: return 2.2f;
                case SurfaceStyle.Sand: return 1.2f;
                case SurfaceStyle.Gravel: return 3f;
                case SurfaceStyle.Clay: return 0.8f;
                case SurfaceStyle.Bedrock: return 3.2f;
                case SurfaceStyle.Ore: return 2.4f;
                case SurfaceStyle.Crystal: return 1.8f;
                case SurfaceStyle.Bark: return 3f;
                case SurfaceStyle.LogEnd: return 1.6f;
                case SurfaceStyle.Leaves: return 2.4f;
                case SurfaceStyle.Bush: return 2.6f;
                case SurfaceStyle.Planks: return 1.3f;
                case SurfaceStyle.Brick: return 2.4f;
                case SurfaceStyle.WorkbenchTop: return 1.5f;
                case SurfaceStyle.CarpentryTop: return 1.6f;
                case SurfaceStyle.Tiles: return 1.8f;
                case SurfaceStyle.BedTop: return 1.2f;
                default: return 1f;
            }
        }

        // ------------------------------------------------------------------ Styles

        private static Sample Shade(SurfaceStyle style, float u, float v, int seed, AtlasLayout.Tile tile, BlockDefinition def)
        {
            var baseColor = tile.Color;
            switch (style)
            {
                case SurfaceStyle.Dirt: return Dirt(u, v, seed, baseColor, def.Smoothness);
                case SurfaceStyle.GrassTop: return GrassTop(u, v, seed, baseColor, def.Smoothness);
                case SurfaceStyle.GrassSide: return GrassSide(u, v, seed, baseColor, def.Accent, def.TopColor, def.Smoothness);
                case SurfaceStyle.Stone: return Stone(u, v, seed, baseColor, def.Smoothness, 1f);
                case SurfaceStyle.Bedrock: return Bedrock(u, v, seed, baseColor);
                case SurfaceStyle.Sand: return Sand(u, v, seed, baseColor, def.Smoothness);
                case SurfaceStyle.Gravel: return Gravel(u, v, seed, baseColor, def.Smoothness);
                case SurfaceStyle.Clay: return Clay(u, v, seed, baseColor, def.Smoothness);
                case SurfaceStyle.Ore: return Ore(u, v, seed, baseColor, def);
                case SurfaceStyle.Crystal: return Crystal(u, v, seed, baseColor, def);
                case SurfaceStyle.Bark: return Bark(u, v, seed, baseColor, def.Accent, def.Smoothness);
                case SurfaceStyle.LogEnd: return LogEnd(u, v, seed, baseColor, def.Accent, def.Smoothness);
                case SurfaceStyle.Leaves: return Leaves(u, v, seed, baseColor, def.Smoothness);
                case SurfaceStyle.Bush: return Bush(u, v, seed, baseColor, def);
                case SurfaceStyle.Planks: return Planks(u, v, seed, baseColor, def.Accent, def.Smoothness);
                case SurfaceStyle.Brick: return Brick(u, v, seed, baseColor, def.Accent, def.Smoothness);
                case SurfaceStyle.WorkbenchTop: return WorkbenchTop(u, v, seed, baseColor, def);
                case SurfaceStyle.CarpentryTop: return CarpentryTop(u, v, seed, baseColor, def);
                case SurfaceStyle.Tiles: return Tiles(u, v, seed, baseColor, def.Accent, def.Smoothness);
                case SurfaceStyle.BedTop: return BedTop(u, v, seed, baseColor, def.Accent);
                case SurfaceStyle.Fabric: return Fabric(u, v, seed, baseColor, 0.15f);
                case SurfaceStyle.TorchSide: return TorchSide(u, v, seed, baseColor, def);
                case SurfaceStyle.TorchTop: return TorchTop(u, v, seed, def);
                default: return Flat(u, v, seed, baseColor, tile.Noise, def.Smoothness);
            }
        }

        private static Sample Flat(float u, float v, int seed, ColorRgb c, float noise, float smooth)
        {
            float n = Fbm(u, v, 16, seed, 3) - 0.5f;
            return new Sample { Albedo = c * (1f + n * noise * 2f), Height = 0.5f + n * 0.3f, Smoothness = smooth };
        }

        private static Sample Dirt(float u, float v, int seed, ColorRgb c, float smooth)
        {
            float grain = Fbm(u, v, 32, seed, 3);
            float clumps = Fbm(u, v, 5, seed + 5, 2);
            float shade = 0.72f + 0.4f * grain + 0.25f * (clumps - 0.5f);
            var s = new Sample
            {
                Albedo = c * shade,
                Height = 0.25f + 0.35f * grain + 0.25f * clumps,
                Smoothness = smooth * (0.6f + 0.8f * grain),
            };
            // Small stones and clods embedded in the soil.
            Noise.PeriodicWorley(u * 9f, v * 9f, 9, seed + 9, out float d1, out _, out uint id);
            float r = 0.16f + 0.16f * H(id, 1);
            if (d1 < r && H(id, 2) > 0.35f)
            {
                float t = 1f - (d1 / r) * (d1 / r);
                float tone = 0.75f + 0.5f * H(id, 3);
                var pebble = ColorRgb.Lerp(c * (tone * 0.9f), ColorRgb.Bytes(150, 140, 125) * tone, 0.5f * H(id, 4));
                s.Albedo = ColorRgb.Lerp(s.Albedo, pebble, Clamp01(t * 1.5f));
                s.Height += 0.55f * t;
                s.Smoothness = 0.25f + 0.2f * H(id, 5);
            }
            return s;
        }

        private static Sample GrassTop(float u, float v, int seed, ColorRgb c, float smooth)
        {
            float clumps = Fbm(u, v, 6, seed + 3, 3);
            float blades = Aniso(u, v, 40, 12, seed + 7, 3);   // short streaks
            float fine = Fbm(u, v, 48, seed + 11, 2);
            float shade = 0.65f + 0.45f * blades + 0.25f * (clumps - 0.5f) + 0.15f * (fine - 0.5f);
            var col = c * shade;
            // Occasional dry, yellower tufts.
            Noise.PeriodicWorley(u * 5f, v * 5f, 5, seed + 13, out float d1, out _, out uint id);
            if (d1 < 0.35f && H(id, 1) > 0.7f)
                col = ColorRgb.Lerp(col, ColorRgb.Bytes(150, 160, 70) * shade, 0.5f * (1f - d1 / 0.35f));
            return new Sample
            {
                Albedo = col,
                Height = 0.3f + 0.5f * blades + 0.2f * fine,
                Smoothness = smooth * (0.5f + blades),
            };
        }

        private static Sample GrassSide(float u, float v, int seed, ColorRgb c, ColorRgb dirt, ColorRgb grass, float smooth)
        {
            // Dirt with a ragged overhang of grass along the top edge (v = 1 is the top of the face).
            float edge = 0.70f + 0.16f * Fbm(u, 0.37f, 10, seed + 21, 2);
            if (v > edge)
            {
                float depth = (v - edge) / (1f - edge);
                var g = GrassTop(u, v, seed + 2, grass, smooth);
                g.Albedo = g.Albedo * (0.75f + 0.35f * depth);
                g.Height = 0.55f + 0.35f * depth + 0.15f * (g.Height - 0.5f);
                return g;
            }
            var d = Dirt(u, v, seed, dirt, smooth);
            float rim = Clamp01((edge - v) / 0.06f);
            d.Albedo = d.Albedo * (0.7f + 0.3f * rim);
            d.Height *= 0.6f;
            return d;
        }

        private static Sample Stone(float u, float v, int seed, ColorRgb c, float smooth, float crackScale)
        {
            float mottle = Fbm(u, v, 5, seed, 3);
            float grain = Fbm(u, v, 40, seed + 3, 2);
            float veins = Fbm(u, v, 7, seed + 11, 3);
            float ridge = Math.Abs(veins * 2f - 1f);
            float shade = 0.78f + 0.32f * mottle + 0.14f * (grain - 0.5f);
            var s = new Sample
            {
                Albedo = c * shade,
                Height = 0.45f + 0.25f * mottle + 0.15f * grain,
                Smoothness = smooth * (0.7f + 0.9f * mottle),
            };
            float crackWidth = 0.05f * crackScale;
            if (ridge < crackWidth)
            {
                float t = 1f - ridge / crackWidth;
                s.Albedo = s.Albedo * (1f - 0.5f * t);
                s.Height -= 0.5f * t;
                s.Smoothness *= 0.6f;
            }
            return s;
        }

        private static Sample Bedrock(float u, float v, int seed, ColorRgb c)
        {
            var s = Stone(u, v, seed, c, 0.2f, 1.8f);
            float coarse = Fbm(u, v, 3, seed + 31, 2);
            s.Albedo = s.Albedo * (0.7f + 0.5f * coarse);
            s.Height = 0.3f + 0.5f * coarse + 0.3f * (s.Height - 0.5f);
            return s;
        }

        private static Sample Sand(float u, float v, int seed, ColorRgb c, float smooth)
        {
            float grain = Fbm(u, v, 48, seed, 2);
            float drift = Fbm(u, v, 4, seed + 2, 2);
            float ripple = (float)Math.Sin((u + 0.25f * drift) * Math.PI * 2.0 * 3.0) * 0.5f + 0.5f;
            float shade = 0.85f + 0.2f * grain + 0.08f * (ripple - 0.5f) + 0.1f * (drift - 0.5f);
            var s = new Sample
            {
                Albedo = c * shade,
                Height = 0.35f + 0.3f * ripple + 0.2f * grain,
                Smoothness = smooth * (0.6f + 0.8f * grain),
            };
            // Glittering quartz grains.
            if (Noise.Hash((int)(u * 64f), (int)(v * 64f), seed + 5) > 0.985f)
            {
                s.Albedo = s.Albedo * 1.25f;
                s.Smoothness = 0.85f;
            }
            return s;
        }

        private static Sample Gravel(float u, float v, int seed, ColorRgb c, float smooth)
        {
            Noise.PeriodicWorley(u * 7f, v * 7f, 7, seed, out float d1, out float d2, out uint id);
            float edge = d2 - d1;
            float grain = Fbm(u, v, 40, seed + 3, 2);
            if (edge > 0.07f)
            {
                float dome = (float)Math.Sqrt(Clamp01((edge - 0.07f) / 0.5f));
                float tone = 0.65f + 0.6f * H(id, 1);
                var tint = new ColorRgb(1f + 0.15f * (H(id, 2) - 0.5f), 1f + 0.1f * (H(id, 3) - 0.5f), 1f + 0.15f * (H(id, 4) - 0.5f));
                var col = new ColorRgb(c.r * tint.r, c.g * tint.g, c.b * tint.b) * (tone * (0.9f + 0.2f * grain));
                return new Sample
                {
                    Albedo = col,
                    Height = 0.25f + 0.7f * dome + 0.05f * grain,
                    Smoothness = smooth * (0.5f + H(id, 5)),
                };
            }
            return new Sample
            {
                Albedo = ColorRgb.Bytes(70, 60, 50) * (0.8f + 0.4f * grain),
                Height = 0.15f * grain,
                Smoothness = 0.08f,
            };
        }

        private static Sample Clay(float u, float v, int seed, ColorRgb c, float smooth)
        {
            float mottle = Fbm(u, v, 4, seed, 3);
            float fine = Fbm(u, v, 24, seed + 3, 2);
            float veins = Math.Abs(Fbm(u, v, 5, seed + 9, 3) * 2f - 1f);
            var s = new Sample
            {
                Albedo = c * (0.85f + 0.25f * mottle + 0.06f * (fine - 0.5f)),
                Height = 0.45f + 0.2f * mottle + 0.05f * fine,
                Smoothness = smooth * (0.8f + 0.4f * mottle),
            };
            if (veins < 0.03f)
            {
                s.Albedo = s.Albedo * 0.85f;
                s.Height -= 0.15f;
            }
            return s;
        }

        private static Sample Ore(float u, float v, int seed, ColorRgb c, BlockDefinition def)
        {
            var s = Stone(u, v, seed, c, def.Smoothness, 1f);
            Noise.PeriodicWorley(u * 8f, v * 8f, 8, seed + 22, out float d1, out _, out uint id);
            float blob = Fbm(u, v, 12, seed + 21, 2);
            float r = 0.22f + 0.22f * H(id, 1);
            float dist = d1 + 0.12f * (blob - 0.5f);
            if (dist < r && H(id, 2) > 0.3f)
            {
                float t = Clamp01((r - dist) / 0.08f);
                float grain = Fbm(u, v, 48, seed + 27, 2);
                var nugget = def.Accent * (0.8f + 0.4f * grain);
                s.Albedo = ColorRgb.Lerp(s.Albedo, nugget, t);
                s.Metallic = def.AccentMetallic * t;
                s.Smoothness = Lerp(s.Smoothness, def.AccentSmoothness * (0.85f + 0.3f * grain), t);
                s.Height += 0.4f * t * (1f - dist / r);
            }
            return s;
        }

        private static Sample Crystal(float u, float v, int seed, ColorRgb c, BlockDefinition def)
        {
            Noise.PeriodicWorley(u * 5f, v * 5f, 5, seed, out float d1, out float d2, out uint id);
            float edge = d2 - d1;
            float facet = H(id, 1);
            var tint = ColorRgb.Lerp(c, def.Accent, H(id, 2) * 0.6f);
            float shade = 0.6f + 0.6f * facet;
            var s = new Sample
            {
                Albedo = tint * shade,
                Height = 0.2f + 0.7f * facet,
                Metallic = def.AccentMetallic,
                Smoothness = def.AccentSmoothness,
                Emissive = def.Emissive * (0.35f + 0.9f * facet),
            };
            if (edge < 0.05f)
            {
                float t = 1f - edge / 0.05f;
                s.Albedo = s.Albedo * (1f + 0.4f * t);
                s.Emissive = s.Emissive + def.Emissive * (1.2f * t);
            }
            return s;
        }

        private static Sample Bark(float u, float v, int seed, ColorRgb c, ColorRgb groove, float smooth)
        {
            float ridges = Aniso(u, v, 20, 3, seed, 3);       // long vertical ridges
            float knots = Fbm(u, v, 6, seed + 5, 2);
            float fine = Fbm(u, v, 40, seed + 9, 2);
            float r = Math.Abs(ridges * 2f - 1f);
            float height = 0.25f + 0.6f * r + 0.15f * fine;
            var col = ColorRgb.Lerp(groove, c, Clamp01(r * 1.3f)) * (0.8f + 0.3f * fine + 0.2f * (knots - 0.5f));
            return new Sample { Albedo = col, Height = height, Smoothness = smooth * (0.6f + 0.8f * r) };
        }

        private static Sample LogEnd(float u, float v, int seed, ColorRgb c, ColorRgb bark, float smooth)
        {
            float dx = u - 0.5f, dy = v - 0.5f;
            float dist = (float)Math.Sqrt(dx * dx + dy * dy) * 2f;
            float wobble = 0.05f * (Fbm(u, v, 6, seed, 2) - 0.5f);
            float rr = dist + wobble;
            if (rr > 0.9f)
                return Bark(u, v, seed + 3, bark, bark * 0.6f, smooth);
            float ring = (float)(0.5 + 0.5 * Math.Sin(rr * Math.PI * 2.0 * 7.0));
            float grain = Fbm(u, v, 32, seed + 4, 2);
            var col = c * (0.8f + 0.28f * ring + 0.1f * (grain - 0.5f));
            return new Sample
            {
                Albedo = col,
                Height = 0.45f + 0.3f * ring + 0.1f * grain,
                Smoothness = smooth * (0.7f + 0.6f * ring),
            };
        }

        private static Sample Leaves(float u, float v, int seed, ColorRgb c, float smooth)
        {
            Noise.PeriodicWorley(u * 9f, v * 9f, 9, seed, out float d1, out _, out uint id);
            float fine = Fbm(u, v, 32, seed + 3, 2);
            float r = 0.4f + 0.2f * H(id, 1);
            if (d1 < r)
            {
                float dome = (float)Math.Sqrt(Clamp01(1f - (d1 / r) * (d1 / r)));
                float tone = 0.7f + 0.5f * H(id, 2);
                var tint = new ColorRgb(1f + 0.2f * (H(id, 3) - 0.5f), 1f, 1f + 0.1f * (H(id, 4) - 0.5f));
                var col = new ColorRgb(c.r * tint.r, c.g * tint.g, c.b * tint.b) * (tone * (0.9f + 0.2f * fine));
                return new Sample { Albedo = col, Height = 0.2f + 0.7f * dome, Smoothness = smooth * (0.6f + 0.8f * dome) };
            }
            return new Sample { Albedo = c * 0.35f, Height = 0.1f * fine, Smoothness = 0.1f };
        }

        private static Sample Bush(float u, float v, int seed, ColorRgb c, BlockDefinition def)
        {
            var s = Leaves(u, v, seed, c, def.Smoothness);
            Noise.PeriodicWorley(u * 6f, v * 6f, 6, seed + 40, out float d1, out _, out uint id);
            if (d1 < 0.17f && H(id, 1) > 0.5f)
            {
                float t = 1f - (d1 / 0.17f) * (d1 / 0.17f);
                float highlight = Clamp01(1f - d1 / 0.06f);
                s.Albedo = ColorRgb.Lerp(s.Albedo, def.Accent * (0.8f + 0.6f * highlight), Clamp01(t * 2f));
                s.Height = Math.Max(s.Height, 0.5f + 0.5f * (float)Math.Sqrt(t));
                s.Smoothness = def.AccentSmoothness;
            }
            return s;
        }

        private static Sample Planks(float u, float v, int seed, ColorRgb c, ColorRgb seam, float smooth)
        {
            const int boards = 4;
            int board = (int)(v * boards);
            float within = v * boards - board;
            float shift = H((uint)(board + 1), seed);
            float grain = Aniso(u + shift, v, 48, 6, seed + board, 3);
            float knots = Fbm(u + shift, v, 5, seed + 3, 2);
            float tone = 0.85f + 0.2f * H((uint)(board + 1), seed + 7);
            var col = c * (tone * (0.8f + 0.3f * grain + 0.15f * (knots - 0.5f)));
            var s = new Sample { Albedo = col, Height = 0.55f + 0.2f * grain, Smoothness = smooth * (0.7f + 0.6f * grain) };
            float seamWidth = 0.06f;
            if (within < seamWidth || within > 1f - seamWidth)
            {
                float t = within < seamWidth ? 1f - within / seamWidth : 1f - (1f - within) / seamWidth;
                s.Albedo = ColorRgb.Lerp(s.Albedo, seam, Clamp01(t * 1.5f));
                s.Height -= 0.5f * t;
                s.Smoothness *= 0.6f;
            }
            return s;
        }

        /// <summary>Square floor tiles with grout lines, each tile a slightly different shade and polish.</summary>
        private static Sample Tiles(float u, float v, int seed, ColorRgb c, ColorRgb grout, float smooth)
        {
            const int n = 3;
            int tx = (int)(u * n), ty = (int)(v * n);
            float wu = u * n - tx, wv = v * n - ty;
            const float g = 0.05f;
            if (wu < g || wu > 1f - g || wv < g || wv > 1f - g)
            {
                float grit = Fbm(u, v, 40, seed + 5, 2);
                return new Sample { Albedo = grout * (0.8f + 0.3f * grit), Height = 0.15f + 0.1f * grit, Smoothness = 0.1f };
            }
            uint id = Noise.HashU(tx, ty, 0, seed);
            float veinsA = Fbm(u, v, 6, seed + 1, 3);
            float ridge = Math.Abs(veinsA * 2f - 1f);
            float polish = 0.7f + 0.6f * H(id, 2);
            var col = c * ((0.85f + 0.25f * H(id, 1)) * (0.9f + 0.2f * veinsA));
            if (ridge < 0.04f) col = col * 0.8f;
            float bevel = Math.Min(Math.Min(wu - g, 1f - g - wu), Math.Min(wv - g, 1f - g - wv));
            return new Sample
            {
                Albedo = col,
                Height = 0.5f + 0.4f * Clamp01(bevel / 0.06f),
                Smoothness = smooth * polish,
            };
        }

        private static Sample Brick(float u, float v, int seed, ColorRgb c, ColorRgb mortar, float smooth)
        {
            const int rows = 2;
            int row = (int)(v * rows);
            float offset = (row & 1) == 0 ? 0f : 0.5f;
            float bu = u + offset;
            bu -= (float)Math.Floor(bu);
            int col = (int)(bu * 2f);
            float wu = bu * 2f - col;
            float wv = v * rows - row;
            const float m = 0.07f;
            if (wu < m || wu > 1f - m || wv < m || wv > 1f - m)
            {
                float grit = Fbm(u, v, 40, seed + 5, 2);
                return new Sample { Albedo = mortar * (0.8f + 0.3f * grit), Height = 0.1f + 0.1f * grit, Smoothness = 0.15f };
            }
            uint id = Noise.HashU(row, col, 0, seed);
            var s = Stone(u, v, seed + 1, c * (0.85f + 0.3f * H(id, 1)), smooth, 0.6f);
            float bevel = Math.Min(Math.Min(wu - m, 1f - m - wu), Math.Min(wv - m, 1f - m - wv));
            s.Height = 0.45f + 0.4f * Clamp01(bevel / 0.08f) + 0.15f * (s.Height - 0.5f);
            return s;
        }

        private static Sample WorkbenchTop(float u, float v, int seed, ColorRgb c, BlockDefinition def)
        {
            var s = Planks(u, v, seed, c * 0.9f, def.Accent * 0.3f, def.Smoothness);
            // A steel plate with a couple of bolt holes on one side of the bench.
            if (u > 0.55f && u < 0.92f && v > 0.18f && v < 0.5f)
            {
                float scratches = Aniso(u, v, 48, 4, seed + 9, 2);
                s.Albedo = def.Accent * (0.8f + 0.3f * scratches);
                s.Metallic = def.AccentMetallic;
                s.Smoothness = def.AccentSmoothness * (0.85f + 0.3f * scratches);
                s.Height = 0.8f;
                float hx = u < 0.735f ? 0.62f : 0.85f;
                float dx = u - hx, dy = v - 0.34f;
                if (dx * dx + dy * dy < 0.0009f)
                {
                    s.Albedo = s.Albedo * 0.3f;
                    s.Height = 0.4f;
                    s.Smoothness = 0.3f;
                }
            }
            return s;
        }

        /// <summary>Plank top with a circular saw blade set into a slot and a clamp rail along one edge.</summary>
        private static Sample CarpentryTop(float u, float v, int seed, ColorRgb c, BlockDefinition def)
        {
            var s = Planks(u, v, seed, c, def.Accent * 0.25f, def.Smoothness);
            // Sawdust dusting.
            float dust = Fbm(u, v, 20, seed + 5, 2);
            if (dust > 0.62f) s.Albedo = ColorRgb.Lerp(s.Albedo, ColorRgb.Bytes(225, 200, 150), (dust - 0.62f) * 2f);

            // Saw blade: a metal disc with teeth, centred on the right half.
            float dx = u - 0.66f, dy = v - 0.5f;
            float r = (float)Math.Sqrt(dx * dx + dy * dy);
            float angle = (float)Math.Atan2(dy, dx);
            float teeth = 0.22f + 0.025f * (float)Math.Sin(angle * 24f);
            if (r < teeth)
            {
                float radial = Aniso(r * 3f, angle / 6.2832f + 0.5f, 3, 64, seed + 11, 2);
                s.Albedo = def.Accent * (0.75f + 0.35f * radial);
                s.Metallic = def.AccentMetallic;
                s.Smoothness = def.AccentSmoothness * (0.85f + 0.3f * radial);
                s.Height = 0.62f;
                if (r < 0.05f)
                {
                    s.Albedo = s.Albedo * 0.35f;
                    s.Height = 0.4f;
                }
            }
            else if (r < teeth + 0.02f)
            {
                s.Albedo = s.Albedo * 0.35f; // slot shadow
                s.Height = 0.2f;
            }

            // Clamp rail along the left edge.
            if (u < 0.18f && v > 0.12f && v < 0.88f)
            {
                float rail = Aniso(u, v, 4, 24, seed + 13, 2);
                s.Albedo = def.Accent * 0.7f * (0.8f + 0.3f * rail);
                s.Metallic = def.AccentMetallic * 0.8f;
                s.Smoothness = 0.5f;
                s.Height = 0.85f;
                float slot = (float)Math.Abs(Math.Sin(v * Math.PI * 2.0 * 6.0));
                if (slot < 0.15f) s.Height = 0.6f;
            }
            return s;
        }

        private static Sample BedTop(float u, float v, int seed, ColorRgb blanket, ColorRgb pillow)
        {
            if (v > 0.72f)
            {
                float px = Clamp01((u - 0.08f) / 0.05f) * Clamp01((0.92f - u) / 0.05f);
                float py = Clamp01((v - 0.74f) / 0.05f) * Clamp01((0.98f - v) / 0.05f);
                float dome = (float)Math.Sqrt(px * py);
                var f = Fabric(u, v, seed + 3, pillow, 0.2f);
                f.Height = 0.4f + 0.6f * dome;
                f.Albedo = f.Albedo * (0.85f + 0.2f * dome);
                return f;
            }
            var s = Fabric(u, v, seed, blanket, 0.15f);
            // Folds in the blanket.
            float fold = Aniso(u, v, 2, 10, seed + 8, 2);
            s.Albedo = s.Albedo * (0.85f + 0.3f * fold);
            s.Height = 0.35f + 0.3f * fold + 0.15f * (s.Height - 0.5f);
            return s;
        }

        private static Sample Fabric(float u, float v, int seed, ColorRgb c, float smooth)
        {
            float weave = (float)(Math.Sin(u * Math.PI * 2.0 * 32.0) * Math.Sin(v * Math.PI * 2.0 * 32.0)) * 0.5f + 0.5f;
            float fuzz = Fbm(u, v, 40, seed, 2);
            return new Sample
            {
                Albedo = c * (0.85f + 0.2f * weave + 0.1f * (fuzz - 0.5f)),
                Height = 0.4f + 0.2f * weave + 0.1f * fuzz,
                Smoothness = smooth,
            };
        }

        private static Sample TorchSide(float u, float v, int seed, ColorRgb c, BlockDefinition def)
        {
            if (v < 0.55f)
            {
                var wood = Bark(u, v * 2f, seed, c * 0.7f, c * 0.35f, def.Smoothness);
                wood.Height *= 0.5f;
                return wood;
            }
            float t = (v - 0.55f) / 0.45f;
            float flicker = Fbm(u, v, 8, seed + 3, 2);
            float width = 0.5f - 0.35f * t;
            float core = Clamp01(1f - Math.Abs(u - 0.5f) / width);
            var flame = ColorRgb.Lerp(def.Accent, ColorRgb.Bytes(255, 240, 200), core * 0.6f);
            return new Sample
            {
                Albedo = flame * (0.8f + 0.4f * flicker),
                Height = 0.5f + 0.4f * core,
                Smoothness = 0.2f,
                Emissive = def.Emissive * ((0.6f + 0.8f * core) * (0.8f + 0.4f * flicker)),
            };
        }

        private static Sample TorchTop(float u, float v, int seed, BlockDefinition def)
        {
            float dx = u - 0.5f, dy = v - 0.5f;
            float core = Clamp01(1f - (float)Math.Sqrt(dx * dx + dy * dy) * 2.2f);
            float flicker = Fbm(u, v, 8, seed + 3, 2);
            var flame = ColorRgb.Lerp(def.Accent, ColorRgb.Bytes(255, 245, 210), core);
            return new Sample
            {
                Albedo = flame,
                Height = 0.5f + 0.5f * core,
                Smoothness = 0.2f,
                Emissive = def.Emissive * ((0.8f + 1.2f * core) * (0.8f + 0.4f * flicker)),
            };
        }

        // ------------------------------------------------------------------ Helpers

        /// <summary>Tileable fbm over the unit square with <paramref name="cells"/> lattice cells per face.</summary>
        private static float Fbm(float u, float v, int cells, int seed, int octaves) =>
            Noise.PeriodicFbm2D(u * cells, v * cells, cells, seed, octaves);

        /// <summary>Tileable anisotropic fbm: different lattice density per axis, for streaks and grain.</summary>
        private static float Aniso(float u, float v, int cellsX, int cellsY, int seed, int octaves)
        {
            float sum = 0f, amp = 1f, norm = 0f, freq = 1f;
            int px = cellsX, py = cellsY;
            for (int i = 0; i < octaves; i++)
            {
                sum += Noise.PeriodicValue2D(u * cellsX * freq, v * cellsY * freq, px, py, seed + i * 101) * amp;
                norm += amp;
                amp *= 0.5f;
                freq *= 2f;
                px *= 2;
                py *= 2;
            }
            return sum / norm;
        }

        /// <summary>Per-cell random value in [0,1) derived from a Worley cell id.</summary>
        private static float H(uint id, int salt) => Noise.Hash((int)(id & 0xFFFF), (int)(id >> 16), salt);

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        private static ColorRgb ClampColor(ColorRgb c) =>
            new ColorRgb(Math.Max(0f, c.r), Math.Max(0f, c.g), Math.Max(0f, c.b));

        /// <summary>Cavity-style ambient occlusion: pixels below their blurred neighbourhood are darkened.</summary>
        private static void ComputeAmbientOcclusion(TilePixels px)
        {
            int n = px.Size;
            const int radius = 3;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float sum = 0f;
                    int count = 0;
                    for (int dy = -radius; dy <= radius; dy++)
                        for (int dx = -radius; dx <= radius; dx++)
                        {
                            int sx = (x + dx + n) % n, sy = (y + dy + n) % n;
                            sum += px.Height[px.Index(sx, sy)];
                            count++;
                        }
                    float avg = sum / count;
                    float h = px.Height[px.Index(x, y)];
                    float cavity = Math.Max(0f, avg - h);
                    px.AO[px.Index(x, y)] = Clamp01(1f - cavity * 1.6f * px.NormalStrength * 0.5f) * 0.9f + 0.1f;
                }
        }
    }
}
