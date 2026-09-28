// Content definition types. Pure C# (no UnityEngine) so the balance simulator in
// Tools/BalanceSim can compile the exact same rules the game runs.
using System;

namespace WishExtractor.Core
{
    public enum Rarity { Common = 0, Uncommon = 1, Rare = 2, Epic = 3, Legendary = 4 }

    /// <summary>Procedural mesh used for an item (see View/Loot.cs).</summary>
    public enum ItemShape
    {
        Coin, Wad, Stick, Paper, Gem, Chip, Cube,
        // v2 shapes
        Chunk, Roll, Bag, Bar, Brick, Keys, Teeth, Phone, Ball, Toaster, Fish, Trophy, Shoe, Wallet, Duck, Vending, Diamond
    }

    /// <summary>What an item is, for routing, processing and value rules.</summary>
    public enum ItemCat { Coin, Oddity, Gunk, Washed, Loot, Relic, Roll, Bag, Bar, Brick, Junk, Pallet }

    /// <summary>Every physical thing that can lie in the fountain, sit on a belt or be carried.</summary>
    public sealed class ItemType
    {
        public int Index;
        public string Id;
        public string Name;
        public string Desc;
        public ItemCat Cat;
        public ItemShape Shape;
        public uint Color;
        public float Scale = 1f;       // visual scale of the Loot mesh
        public double BaseValue;       // Crestview dollars per item before multipliers (tosses, loot)
        public double Units = 1;       // coins represented (rolls, bags, pallets)
        public int Tier = -1;          // coin tier (tosses), -1 otherwise
        public int Mall = -1;          // mall index for mall-specific loot/relic/gunk types
        public int Stratum = -1;       // gunk: stratum it came from
        public Rarity Rarity;          // oddities / relics
        public bool Heavy;             // takes two carry slots
    }

    public sealed class ItemKind
    {
        public string Name;
        public double Value;
        public double Weight;
        public ItemShape Shape;
        public uint Color;
        public ItemKind(string name, double value, double weight, ItemShape shape, uint color)
        { Name = name; Value = value; Weight = weight; Shape = shape; Color = color; }
    }

    public sealed class StratumDef
    {
        public string Name;
        public string Flavor;
        public double StartFrac;   // depth fraction where this layer begins
        public double ValueMult;   // multiplies the loot value of this layer
        public bool Loose;         // loose layers shovel straight into coins (no washing)
        public uint Color;         // crust tint
        public uint Speck;         // glint / debris tint
        public StratumDef(string name, double start, double valueMult, bool loose, uint color, uint speck, string flavor)
        { Name = name; StartFrac = start; ValueMult = valueMult; Loose = loose; Color = color; Speck = speck; Flavor = flavor; }
    }

    public sealed class WishDef
    {
        public string Id;
        public string Text;
        public Rarity Rarity;
        public double BaseValue;
        public int MallIndex;
        public WishDef(Rarity r, double value, string text) { Rarity = r; BaseValue = value; Text = text; }
    }

    public sealed class RelicDef
    {
        public string Id;
        public string Name;
        public string Desc;
        public Rarity Rarity;
        public double BaseValue;
        public ItemShape Shape;
        public uint Color;
        public int MallIndex;
        public int ItemType;           // registry index of this relic's physical item
        public RelicDef(Rarity r, double value, string name, string desc, ItemShape shape, uint color)
        { Rarity = r; BaseValue = value; Name = name; Desc = desc; Shape = shape; Color = color; }
    }

    public sealed class ThemeDef
    {
        public uint FloorA, FloorB, BasinTileA, BasinTileB, Rim, Wall, WallTrim, Ceiling, Pillar;
        public uint LightColor, AmbientSky, AmbientEquator, AmbientGround, FogColor, Accent;
        public uint NeonA, NeonB, Plant;
        public float LightIntensity = 1.1f, FogDensity = 0.012f, SunPitch = 55f, SunYaw = 35f;
        public float NeonStrength = 1f;
        public bool Night;
        public string[] Signs;
        public uint[] SignColors;
        // procedural music
        public int MusicRoot = 60;      // MIDI note
        public float MusicTempo = 84f;
        public int MusicMode;           // 0 = major 7ths, 1 = dorian, 2 = synthwave minor, 3 = lounge, 4 = casino swing, 5 = ancient
    }

    public sealed class MallEventDef
    {
        public string Name;
        public string Desc;
        public string Effect; // "toss", "wish", "relic", "sell", "golden", "all"
        public double Mult;
        public MallEventDef(string name, string effect, double mult, string desc) { Name = name; Effect = effect; Mult = mult; Desc = desc; }
    }

    public sealed class MallDef
    {
        public string Id;
        public string Name;
        public string Tagline;
        public string Intro;
        public double DepthFeet;
        public double CrustScoops;     // total crust work (scoops) from the top to bare concrete
        public double[] Bounds;        // cumulative scoops at the top of each stratum; last entry = CrustScoops
        public double ValueScale = 1;  // every value and price in this mall is (Crestview amount) × ValueScale
        public int LuckyPennies;       // prestige reward for clearing it
        public StratumDef[] Strata;
        public ItemKind[] Items;       // crust loot
        public WishDef[] Wishes;
        public RelicDef[] Relics;
        public string TreasureName;
        public string TreasureDesc;
        public MallEventDef Event;
        public ThemeDef Theme;
        public double BaseEV;          // computed from Items
        public int[] LootTypes;        // registry index per Items entry
        public int[] GunkTypes;        // registry index per stratum

        public void ComputeEV()
        {
            double w = 0, v = 0;
            foreach (var it in Items) { w += it.Weight; v += it.Weight * it.Value; }
            BaseEV = w > 0 ? v / w : 0;
        }
    }

    /// <summary>A rung of the carry ladder: how many items one trip holds.</summary>
    public sealed class CarryDef
    {
        public string Id;
        public string Name;
        public string Desc;
        public int Capacity;
        public double Cost;
        public float SpeedMult = 1f;    // wheelbarrows and carts slow you down
        public bool NoJump;             // pushing a cart
        public float AutoRadius;        // shop-vac: vacuums items within this radius while held
        public int Index;
    }

    /// <summary>A pickup tool: reach, grab area, grab rate, crust digging.</summary>
    public sealed class ToolDef
    {
        public string Id;
        public string Name;
        public string Desc;
        public double Cost;
        public float Reach = 2.6f;      // metres from the eye
        public float Area;              // grab radius at the aim point (0 = single item)
        public float Rate = 4f;         // grabs per second while held
        public double DigPower;         // crust scoops per swing (0 = can't dig)
        public int Index;
    }

    /// <summary>Who throws what into the fountain.</summary>
    public sealed class ArchetypeDef
    {
        public string Id;
        public string Name;
        public uint Shirt, Pants, Hat, Skin;
        public bool HardHat, Headband;
        public float Scale = 1f;
        public double MinWish;          // wishability needed before they show up
        public double Weight = 1;
        public double TierBias;         // + shifts their tosses toward richer tiers
        public string[] Oddities;       // oddity item ids they sometimes throw
        public string[] Barks;          // what they say while throwing
        public string Prop;             // what they carry: "phone", "briefcase", "cane", "balloon", "rose", "camera", "tophat", "selfie", "dumbbell"
        public int Tosses = 1;          // tosses per visit (before rate bonuses)
        public float Speed = 1.3f;      // walking speed, m/s
        public double JunkChance;       // chance a toss is worthless junk (teens and their gum)
        public double OddityBoost = 1;  // × oddity chance
        public int Index;
    }

    public enum TechKind
    {
        Carry,          // sets carry tier = Value
        Tool,           // sets tool tier = Value
        Wishability,    // + Value wishability per level
        ValueMult,      // × (1 + Value) sale value per level
        TossRate,       // × (1 + Value) toss frequency per level
        WalkSpeed,      // × (1 + Value) walk speed per level
        Reach,          // + Value metres reach per level
        GrabRate,       // × (1 + Value) grab speed per level
        WishLife,       // × (1 + Value) wish orb lifetime per level
        Unlock,         // unlocks a buildable (Target) or feature
        MachineSpeed,   // × (1 + Value) speed for buildables in Target category ("" = all)
        BeltSpeed,      // belt tier = level
        DepositMult,    // × (1 + Value) for a category (Target = ItemCat name)
        GuardFine,      // × (1 - Value) security fines per level
    }

    public enum TechBranch { Carry, Tools, Fountain, Power, Intake, Logistics, Processing, Security }

    /// <summary>A node on the Maintenance Terminal's tech tree. Levelled nodes repeat with growing cost.</summary>
    public sealed class TechDef
    {
        public string Id;
        public string Name;
        public string Desc;
        public TechBranch Branch;
        public TechKind Kind;
        public string Target;
        public double Value;
        public double Cost;             // first level, Crestview dollars (× mall ValueScale)
        public double CostGrowth = 2.2;
        public int MaxLevel = 1;
        public bool WishTokens;         // paid in Wish Tokens instead of cash
        public string[] Requires = Array.Empty<string>();
        public int UnlockMall;          // first mall where it can appear
        public int Col, Row;            // layout position in the tree view
        public int Index;
        public double CostAt(int level, double scale) => Math.Round(Cost * Math.Pow(CostGrowth, level) * (WishTokens ? 1 : scale), WishTokens ? 0 : 2);
    }

    public sealed class AchievementDef
    {
        public string Id;
        public string Name;
        public string Desc;
        public Func<Sim, bool> Check;
        public int Index;
        public AchievementDef(string id, string name, string desc, Func<Sim, bool> check) { Id = id; Name = name; Desc = desc; Check = check; }
    }

    public sealed class ObjectiveDef
    {
        public string Id;
        public string Text;
        public string Hint;
        public Func<Sim, bool> Check;
        public double Reward;
        public ObjectiveDef(string id, string text, string hint, double reward, Func<Sim, bool> check)
        { Id = id; Text = text; Hint = hint; Reward = reward; Check = check; }
    }
}
