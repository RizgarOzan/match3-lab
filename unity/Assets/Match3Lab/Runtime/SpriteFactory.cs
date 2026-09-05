using System.Collections.Generic;
using Match3Lab.Core;
using UnityEngine;

namespace Match3Lab.Unity
{
    /// <summary>
    /// Draws every sprite the board needs at runtime — no art assets, no import settings, nothing
    /// to keep in sync. Deliberately simple shapes: the point of the demo is the rules and the
    /// tooling, and a designer can replace any of these with a drawn sprite later without touching
    /// the views.
    /// </summary>
    public sealed class SpriteFactory
    {
        public const int Size = 96;

        public static readonly Color[] Palette =
        {
            new Color(0.91f, 0.30f, 0.33f), // 0 red
            new Color(0.25f, 0.55f, 0.95f), // 1 blue
            new Color(0.32f, 0.75f, 0.42f), // 2 green
            new Color(0.97f, 0.80f, 0.25f), // 3 yellow
            new Color(0.66f, 0.42f, 0.90f), // 4 purple
            new Color(0.98f, 0.58f, 0.22f), // 5 orange
        };

        private readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

        public Sprite Tile => Get("tile", () => RoundedSquare(0.94f, 0.12f, Color.white));
        public Sprite Gem => Get("gem", () => RoundedSquare(0.78f, 0.28f, Color.white, shade: true));
        public Sprite Circle => Get("circle", () => Disc(0.40f, Color.white, shade: true));
        public Sprite Ring => Get("ring", () => Rainbow());
        public Sprite Rocket => Get("rocket", () => RocketShape());
        public Sprite Square => Get("square", () => RoundedSquare(1f, 0.02f, Color.white));

        public Sprite For(Piece piece)
        {
            switch (piece.Type)
            {
                case PieceType.Normal: return Gem;
                case PieceType.RocketH:
                case PieceType.RocketV: return Rocket;
                case PieceType.Bomb: return Circle;
                case PieceType.Rainbow: return Ring;
                default: return null;
            }
        }

        public static Color ColorOf(Piece piece)
        {
            switch (piece.Type)
            {
                case PieceType.Normal: return Palette[piece.Color % Palette.Length];
                case PieceType.Bomb: return new Color(0.20f, 0.20f, 0.24f);
                case PieceType.RocketH:
                case PieceType.RocketV: return new Color(0.95f, 0.95f, 0.97f);
                default: return Color.white;
            }
        }

        private Sprite Get(string key, System.Func<Sprite> make)
        {
            if (!_cache.TryGetValue(key, out var s))
            {
                s = make();
                _cache[key] = s;
            }
            return s;
        }

        private static Sprite ToSprite(Color[] px)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            tex.SetPixels(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
        }

        /// <summary>Signed distance to a rounded square of half-extent <paramref name="half"/> (in 0..1 units) with corner radius r.</summary>
        private static float RoundedSquareSdf(float x, float y, float half, float r)
        {
            float qx = Mathf.Abs(x) - half + r;
            float qy = Mathf.Abs(y) - half + r;
            float outside = new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude;
            float inside = Mathf.Min(Mathf.Max(qx, qy), 0);
            return outside + inside - r;
        }

        private static Sprite RoundedSquare(float extent, float radius, Color color, bool shade = false)
        {
            var px = new Color[Size * Size];
            float half = extent * 0.5f;
            float aa = 1.5f / Size;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float u = (x + 0.5f) / Size - 0.5f;
                    float v = (y + 0.5f) / Size - 0.5f;
                    float d = RoundedSquareSdf(u, v, half, radius * half);
                    float a = Mathf.Clamp01(-d / aa);
                    var c = color;
                    if (shade)
                    {
                        // A soft highlight toward the top-left reads as volume even on a flat colour.
                        float light = Mathf.Clamp01(0.75f + 0.5f * (-(u) - (-v)) );
                        c = Color.Lerp(color * 0.72f, color * 1.15f, light);
                        c.a = 1f;
                    }
                    c.a *= a;
                    px[y * Size + x] = c;
                }
            }
            return ToSprite(px);
        }

        private static Sprite Disc(float radius, Color color, bool shade = false)
        {
            var px = new Color[Size * Size];
            float aa = 1.5f / Size;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float u = (x + 0.5f) / Size - 0.5f;
                    float v = (y + 0.5f) / Size - 0.5f;
                    float d = Mathf.Sqrt(u * u + v * v) - radius;
                    float a = Mathf.Clamp01(-d / aa);
                    var c = color;
                    if (shade)
                    {
                        float light = Mathf.Clamp01(0.8f + 0.6f * (-u + v));
                        c = Color.Lerp(color * 0.7f, color * 1.2f, light);
                        c.a = 1f;
                    }
                    c.a *= a;
                    px[y * Size + x] = c;
                }
            }
            return ToSprite(px);
        }

        private static Sprite Rainbow()
        {
            var px = new Color[Size * Size];
            float aa = 1.5f / Size;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float u = (x + 0.5f) / Size - 0.5f;
                    float v = (y + 0.5f) / Size - 0.5f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float a = Mathf.Clamp01((0.42f - r) / aa);
                    float hue = (Mathf.Atan2(v, u) / (2f * Mathf.PI) + 1f) % 1f;
                    var c = Color.HSVToRGB(hue, 0.75f, 1f);
                    float core = Mathf.Clamp01((0.16f - r) / aa);
                    c = Color.Lerp(c, Color.white, core);
                    c.a = a;
                    px[y * Size + x] = c;
                }
            }
            return ToSprite(px);
        }

        private static Sprite RocketShape()
        {
            // A vertical capsule with a coloured nose; BoardView rotates it for horizontal rockets.
            var px = new Color[Size * Size];
            float aa = 1.5f / Size;
            var body = new Color(0.95f, 0.95f, 0.97f);
            var nose = new Color(0.91f, 0.30f, 0.33f);
            var fin = new Color(0.25f, 0.55f, 0.95f);
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float u = (x + 0.5f) / Size - 0.5f;
                    float v = (y + 0.5f) / Size - 0.5f;
                    float capsule = Mathf.Max(Mathf.Abs(u) - 0.14f, 0f);
                    float dy = Mathf.Max(Mathf.Abs(v) - 0.22f, 0f);
                    float d = Mathf.Sqrt(capsule * capsule + dy * dy) - 0.0f;
                    float inBody = Mathf.Clamp01((0.14f - Mathf.Sqrt(Mathf.Max(Mathf.Abs(u) - 0f, 0) * 0 + 0)) / aa);
                    float a = Mathf.Clamp01((0.001f - d) / aa) * inBody;
                    var c = body;
                    if (v > 0.18f) c = nose;
                    else if (v < -0.18f && Mathf.Abs(u) > 0.06f) c = fin;
                    c.a = a;
                    px[y * Size + x] = c;
                }
            }
            return ToSprite(px);
        }
    }
}
