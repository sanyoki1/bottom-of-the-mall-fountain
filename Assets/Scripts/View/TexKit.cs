// Procedural textures generated once at startup (no art assets in the project).
using UnityEngine;

namespace WishExtractor.View
{
    public static class TexKit
    {
        static Texture2D softDot, ring, coinCrust, beltStripes, ripples, sparkle, star;

        static Texture2D New(int w, int h, bool mips = true, TextureWrapMode wrap = TextureWrapMode.Clamp)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, mips, true) { wrapMode = wrap, filterMode = FilterMode.Bilinear, anisoLevel = 4 };
            return t;
        }

        /// <summary>Radial soft dot (white, alpha falloff) for halos and particles.</summary>
        public static Texture2D SoftDot
        {
            get
            {
                if (softDot != null) return softDot;
                const int n = 128;
                softDot = New(n, n);
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = (x + 0.5f) / n * 2 - 1, dy = (y + 0.5f) / n * 2 - 1;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01(1 - d);
                        a = a * a * (3 - 2 * a);
                        a = Mathf.Pow(a, 1.6f);
                        px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
                    }
                softDot.SetPixels32(px);
                softDot.Apply();
                return softDot;
            }
        }

        /// <summary>Thin bright ring (for click ripples and golden penny glints).</summary>
        public static Texture2D Ring
        {
            get
            {
                if (ring != null) return ring;
                const int n = 128;
                ring = New(n, n);
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = (x + 0.5f) / n * 2 - 1, dy = (y + 0.5f) / n * 2 - 1;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01(1 - Mathf.Abs(d - 0.8f) / 0.14f);
                        a = a * a;
                        px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
                    }
                ring.SetPixels32(px);
                ring.Apply();
                return ring;
            }
        }

        /// <summary>Four-point star sparkle.</summary>
        public static Texture2D Sparkle
        {
            get
            {
                if (sparkle != null) return sparkle;
                const int n = 128;
                sparkle = New(n, n);
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = (x + 0.5f) / n * 2 - 1, dy = (y + 0.5f) / n * 2 - 1;
                        float ax = Mathf.Abs(dx), ay = Mathf.Abs(dy);
                        float cross = Mathf.Max(Mathf.Clamp01(1 - ax * 9) * Mathf.Clamp01(1 - ay), Mathf.Clamp01(1 - ay * 9) * Mathf.Clamp01(1 - ax));
                        float core = Mathf.Clamp01(1 - Mathf.Sqrt(dx * dx + dy * dy) * 2.2f);
                        float a = Mathf.Clamp01(cross * cross + core * core);
                        px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
                    }
                sparkle.SetPixels32(px);
                sparkle.Apply();
                return sparkle;
            }
        }

        /// <summary>
        /// Tileable coin-crust detail: R = coin mask, G = coin shading (rim light), B = grime noise, A = sparkle seed.
        /// </summary>
        public static Texture2D CoinCrust
        {
            get
            {
                if (coinCrust != null) return coinCrust;
                const int n = 512;
                coinCrust = New(n, n, true, TextureWrapMode.Repeat);
                var r = new float[n * n];
                var g = new float[n * n];
                var b = new float[n * n];
                var a = new float[n * n];
                var rng = new System.Random(7);
                for (int i = 0; i < n * n; i++)
                {
                    int x = i % n, y = i / n;
                    b[i] = Tile(x, y, n, 0.022f, 3) * 0.7f + Tile(x + 77, y + 31, n, 0.08f, 2) * 0.3f;
                }
                // scatter overlapping coins (later coins sit on top)
                for (int c = 0; c < 190; c++)
                {
                    float cx = (float)rng.NextDouble() * n, cy = (float)rng.NextDouble() * n;
                    float rad = 7 + (float)rng.NextDouble() * 12;
                    float seed = (float)rng.NextDouble();
                    float lightDir = (float)rng.NextDouble() * 6.28f;
                    int r0 = Mathf.CeilToInt(rad + 1);
                    for (int oy = -r0; oy <= r0; oy++)
                        for (int ox = -r0; ox <= r0; ox++)
                        {
                            float d = Mathf.Sqrt(ox * ox + oy * oy);
                            if (d > rad + 0.8f) continue;
                            int px = ((int)cx + ox + n) % n, py = ((int)cy + oy + n) % n;
                            int idx = py * n + px;
                            float edge = Mathf.Clamp01(rad + 0.8f - d);
                            float rim = Mathf.Clamp01(1 - Mathf.Abs(d - rad * 0.82f) / 1.6f);
                            float facing = 0.5f + 0.5f * Mathf.Cos(Mathf.Atan2(oy, ox) - lightDir);
                            float shade = Mathf.Clamp01(0.45f + 0.35f * facing * (d / rad) + 0.35f * rim);
                            r[idx] = Mathf.Max(r[idx] * (1 - edge), edge);
                            g[idx] = Mathf.Lerp(g[idx], shade, edge);
                            a[idx] = Mathf.Lerp(a[idx], seed, edge);
                        }
                }
                var px32 = new Color32[n * n];
                for (int i = 0; i < n * n; i++)
                    px32[i] = new Color32((byte)(r[i] * 255), (byte)(g[i] * 255), (byte)(Mathf.Clamp01(b[i]) * 255), (byte)(a[i] * 255));
                coinCrust.SetPixels32(px32);
                coinCrust.Apply();
                return coinCrust;
            }
        }

        /// <summary>Rubber conveyor stripes.</summary>
        public static Texture2D BeltStripes
        {
            get
            {
                if (beltStripes != null) return beltStripes;
                const int w = 64, h = 16;
                beltStripes = New(w, h, true, TextureWrapMode.Repeat);
                var px = new Color32[w * h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        bool stripe = (x % 16) < 3;
                        byte v = (byte)(stripe ? 70 : 42);
                        px[y * w + x] = new Color32(v, v, (byte)(v + 4), 255);
                    }
                beltStripes.SetPixels32(px);
                beltStripes.Apply();
                return beltStripes;
            }
        }

        /// <summary>Soft caustic ripples for acid and water.</summary>
        public static Texture2D Ripples
        {
            get
            {
                if (ripples != null) return ripples;
                const int n = 128;
                ripples = New(n, n, true, TextureWrapMode.Repeat);
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float v = Tile(x, y, n, 0.06f, 3);
                        float c = 1 - Mathf.Abs(v - 0.5f) * 2;
                        c = Mathf.Pow(c, 3);
                        byte bv = (byte)(150 + 105 * c);
                        px[y * n + x] = new Color32(bv, bv, bv, 255);
                    }
                ripples.SetPixels32(px);
                ripples.Apply();
                return ripples;
            }
        }

        /// <summary>Tileable value noise (sum of octaves), 0..1.</summary>
        static float Tile(int x, int y, int n, float freq, int octaves)
        {
            float sum = 0, amp = 1, norm = 0;
            float f = freq;
            for (int o = 0; o < octaves; o++)
            {
                int period = Mathf.Max(1, Mathf.RoundToInt(n * f));
                float fx = x * period / (float)n, fy = y * period / (float)n;
                sum += amp * ValueNoise(fx, fy, period, o * 17);
                norm += amp;
                amp *= 0.5f;
                f *= 2;
            }
            return sum / norm;
        }

        static float ValueNoise(float x, float y, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float tx = x - x0, ty = y - y0;
            tx = tx * tx * (3 - 2 * tx);
            ty = ty * ty * (3 - 2 * ty);
            float a = Hash((x0 % period + period) % period, (y0 % period + period) % period, seed);
            float b = Hash(((x0 + 1) % period + period) % period, (y0 % period + period) % period, seed);
            float c = Hash((x0 % period + period) % period, ((y0 + 1) % period + period) % period, seed);
            float d = Hash(((x0 + 1) % period + period) % period, ((y0 + 1) % period + period) % period, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        public static float Hash(int x, int y, int seed)
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / (float)0xFFFFFF;
        }

        /// <summary>Smooth 2D noise usable anywhere (not tiled).</summary>
        public static float Noise(float x, float y) => Mathf.PerlinNoise(x + 1000.3f, y + 523.7f);
    }
}
