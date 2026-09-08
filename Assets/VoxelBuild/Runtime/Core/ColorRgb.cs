namespace VoxelBuild.Core
{
    /// <summary>Linear RGB colour in 0..1 (or above for HDR emissive values). Engine independent.</summary>
    public struct ColorRgb
    {
        public float r, g, b;

        public ColorRgb(float r, float g, float b)
        {
            this.r = r;
            this.g = g;
            this.b = b;
        }

        public static readonly ColorRgb Black = new ColorRgb(0, 0, 0);
        public static readonly ColorRgb White = new ColorRgb(1, 1, 1);

        /// <summary>Build from 0..255 byte components (sRGB-ish authoring values).</summary>
        public static ColorRgb Bytes(int r, int g, int b) => new ColorRgb(r / 255f, g / 255f, b / 255f);

        public static ColorRgb operator *(ColorRgb c, float s) => new ColorRgb(c.r * s, c.g * s, c.b * s);
        public static ColorRgb operator +(ColorRgb a, ColorRgb b) => new ColorRgb(a.r + b.r, a.g + b.g, a.b + b.b);

        public static ColorRgb Lerp(ColorRgb a, ColorRgb b, float t) =>
            new ColorRgb(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t);

        public bool IsBlack => r <= 0f && g <= 0f && b <= 0f;
    }
}
