// Small types shared across the view: the context handed to view pieces, the click-target tag
// on interactable colliders, and rarity colours.
using UnityEngine;
using WishExtractor.Core;

namespace WishExtractor.View
{
    public sealed class ViewContext
    {
        public Sim Sim;
        public FountainView Fountain;
        public FX Fx;
        public float Time;
        public MallDef Mall;
    }

    /// <summary>Tag on anything you can interact with in the world.</summary>
    public sealed class ClickTarget : MonoBehaviour
    {
        public string Kind;    // "kiosk", "terminal", "wish", ...
        public string Id;
        public int Uid;
    }

    public static class RarityColors
    {
        public static readonly Color[] Orb =
        {
            new Color(0.75f, 0.9f, 1f),     // common: pale blue
            new Color(0.45f, 1f, 0.6f),     // uncommon: mint
            new Color(0.35f, 0.65f, 1f),    // rare: blue
            new Color(0.78f, 0.45f, 1f),    // epic: violet
            new Color(1f, 0.82f, 0.25f),    // legendary: gold
        };
        public static readonly string[] Names = { "Common", "Uncommon", "Rare", "Epic", "Legendary" };
    }

    /// <summary>Physics layers used by the first-person game.</summary>
    public static class Layers
    {
        public const int Default = 0;
        public const int IgnoreRaycast = 2;
    }
}
