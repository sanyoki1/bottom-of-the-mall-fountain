// Shared materials, created once from the shaders in Resources/Shaders.
using UnityEngine;

namespace WishExtractor.View
{
    public static class Mats
    {
        static Material lit, gloss, glow, crust, text, belt, acid, water;

        static Shader Find(string name)
        {
            var s = Shader.Find(name);
            if (s == null)
            {
                Debug.LogWarning($"[WishExtractor] shader '{name}' missing, falling back");
                s = Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
            }
            return s;
        }

        public static Material Lit => lit ??= new Material(Find("WE/Lit")) { name = "WE Lit", enableInstancing = true };

        public static Material Gloss
        {
            get
            {
                if (gloss == null)
                {
                    gloss = new Material(Find("WE/Lit")) { name = "WE Gloss", enableInstancing = true };
                    gloss.SetFloat("_Glossiness", 0.72f);
                    gloss.SetFloat("_Metallic", 0.55f);
                }
                return gloss;
            }
        }

        public static Material Glow
        {
            get
            {
                if (glow == null)
                {
                    glow = new Material(Find("WE/Glow")) { name = "WE Glow" };
                    glow.mainTexture = TexKit.SoftDot;
                    glow.SetFloat("_Intensity", 1.6f);
                }
                return glow;
            }
        }

        /// <summary>Glow material with its own texture/intensity (not shared).</summary>
        public static Material NewGlow(Texture tex, float intensity, Color color)
        {
            var m = new Material(Find("WE/Glow")) { name = "WE Glow Custom" };
            m.mainTexture = tex;
            m.SetFloat("_Intensity", intensity);
            m.SetColor("_Color", color);
            return m;
        }

        public static Material NewSoft(Texture tex)
        {
            var m = new Material(Find("WE/Soft")) { name = "WE Soft" };
            m.mainTexture = tex;
            return m;
        }

        public static Material Crust
        {
            get
            {
                if (crust == null)
                {
                    crust = new Material(Find("WE/Crust")) { name = "WE Crust" };
                    crust.mainTexture = TexKit.CoinCrust;
                }
                return crust;
            }
        }

        public static Material NewText(Font font, Color color, float intensity)
        {
            var m = new Material(Find("WE/Text")) { name = "WE Text" };
            m.mainTexture = font.material.mainTexture;
            m.SetColor("_Color", color);
            m.SetFloat("_Intensity", intensity);
            return m;
        }

        public static Material Belt
        {
            get
            {
                if (belt == null)
                {
                    belt = new Material(Find("WE/Scroll")) { name = "WE Belt" };
                    belt.mainTexture = TexKit.BeltStripes;
                    belt.SetFloat("_Speed", 0.6f);
                    belt.SetFloat("_Glow", 0f);
                    belt.SetFloat("_Glossiness", 0.25f);
                }
                return belt;
            }
        }

        public static Material Acid
        {
            get
            {
                if (acid == null)
                {
                    acid = new Material(Find("WE/Scroll")) { name = "WE Acid" };
                    acid.mainTexture = TexKit.Ripples;
                    acid.SetColor("_Color", new Color(0.45f, 1f, 0.35f));
                    acid.SetFloat("_Speed", 0.35f);
                    acid.SetFloat("_Glow", 1.6f);
                    acid.SetFloat("_Glossiness", 0.9f);
                }
                return acid;
            }
        }

        public static Material Water
        {
            get
            {
                if (water == null)
                {
                    water = new Material(Find("WE/Scroll")) { name = "WE Water" };
                    water.mainTexture = TexKit.Ripples;
                    water.SetColor("_Color", new Color(0.55f, 0.8f, 1f));
                    water.SetFloat("_Speed", 0.12f);
                    water.SetFloat("_Glow", 0.25f);
                    water.SetFloat("_Glossiness", 0.95f);
                }
                return water;
            }
        }

        public static Material ScrollCustom(Texture tex, Color color, float speed, float glowAmount)
        {
            var m = new Material(Find("WE/Scroll")) { name = "WE Scroll Custom" };
            m.mainTexture = tex;
            m.SetColor("_Color", color);
            m.SetFloat("_Speed", speed);
            m.SetFloat("_Glow", glowAmount);
            return m;
        }
    }
}
