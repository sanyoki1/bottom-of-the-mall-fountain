// Content definition types. Pure C# (no UnityEngine) so the balance simulator in
// Tools/BalanceSim can compile the exact same rules the game runs.
using System;

namespace WishExtractor.Core
{
    public enum Stage { Dig = 0, Wash = 1, Sort = 2 }

    public enum Rarity { Common = 0, Uncommon = 1, Rare = 2, Epic = 3, Legendary = 4 }

    public enum ItemShape { Coin, Wad, Stick, Paper, Gem, Chip, Cube }

    public enum UpgradeKind
    {
        MachineMult,    // Target = machine id, Value = multiplier
        StageMult,      // Target = stage name, Value = multiplier
        AllRateMult,    // Value = multiplier on every machine
        ValueMult,      // Value = multiplier on all sale value
        ClickMult,      // Value = multiplier on tool click power
        ClickPctOfDig,  // Value = fraction of auto dig/s added to every click
        ComboMax,       // Value = added to the combo multiplier cap
        WishFreq,       // Value = multiplier on wish spawn rate
        WishValue,      // Value = multiplier on wish value
        WishLife,       // Value = multiplier on wish lifetime
        RelicRate,      // Value = multiplier on relic find rate
        RelicValue,     // Value = multiplier on relic value
        GoldenFreq,     // Value = multiplier on golden penny frequency
        GoldenPower,    // Value = multiplier on golden penny effect strength
        AutoSell,       // unlocks automatic selling of sorted loot
        ScrapRate,      // Value = added to raw/washed dump rates
        CompressorEff,  // Value = added capture chance for the wish compressor
    }

    public enum HOKind
    {
        SeedMoney, StartTool, RateMult, ValueMult, WishLife, WishValue, GoldenFreq,
        OfflineHours, AutoSellStart, VeteranCrew, ScrapRate, RelicRate, ComboMax,
        CostDiscount, GoldenPower, ClickMult, OfflineEff, CompressorStart
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
        public double ValueMult;   // multiplies the mall's base item value
        public bool Loose;         // loose layers skip washing and sorting
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
        public string Effect; // "dig", "wish", "relic", "sell", "golden", "all"
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
        public double TotalItems;
        public double[] Bounds;        // cumulative items at the top of each stratum; last entry = TotalItems
        public double[] RemodelBounds; // the same for the first Remodel lap (later laps grow from it)
        public double CostScale = 1;   // every price in this mall is (Crestview price) × CostScale
        public double Generosity = 1;  // sale value per item relative to Crestview, after scaling
        public double ContractRate;    // multiplier on every sale in this mall (derived in BuildMalls)
        public int LuckyPennies;       // prestige reward for clearing it
        public StratumDef[] Strata;
        public ItemKind[] Items;
        public WishDef[] Wishes;
        public RelicDef[] Relics;
        public string TreasureName;
        public string TreasureDesc;
        public MallEventDef Event;
        public ThemeDef Theme;
        public double BaseEV;          // computed from Items

        public void ComputeEV()
        {
            double w = 0, v = 0;
            foreach (var it in Items) { w += it.Weight; v += it.Weight * it.Value; }
            BaseEV = w > 0 ? v / w : 0;
        }
    }

    public sealed class MachineDef
    {
        public string Id;
        public string Name;
        public string Desc;
        public Stage Stage;
        public int Tier;
        public double BaseCost;
        public double BaseRate;       // items per second per unit
        public double CostGrowth = 1.15;
        public int UnlockMall;        // first mall (0-based) it can be built in
        public int UnlockStratum;     // stratum in the unlock mall that reveals it
        public bool IsCompressor;
        public bool IsMega;           // single-structure global boosters
        public string MegaEffect;     // "rate", "value", "wish"
        public double MegaPerLevel;
        public int MaxCount = int.MaxValue;
        public int Index;
    }

    public sealed class ToolDef
    {
        public string Id;
        public string Name;
        public string Desc;
        public double Cost;
        public double Power;          // items per click
        public int Index;
    }

    public sealed class UpgradeDef
    {
        public string Id;
        public string Name;
        public string Desc;
        public double Cost;
        public UpgradeKind Kind;
        public string Target;
        public double Value;
        // requirements
        public string ReqMachine;
        public int ReqCount;
        public int ReqMall = 0;       // must be in this mall or later
        public bool MallOnly;         // themed upgrade: only sold inside ReqMall (and its remodels)
        public int ReqStratum = -1;   // reached this stratum in the current mall (only if ReqMall == current)
        public int ReqTool = -1;
        public int Index;
    }

    public sealed class HeadOfficeDef
    {
        public string Id;
        public string Name;
        public string Desc;
        public HOKind Kind;
        public double Value;          // effect per level
        public int MaxLevel;
        public double BaseCost;       // lucky pennies
        public double CostGrowth;
        public int Index;
        public double CostAt(int level) { return Math.Ceiling(BaseCost * Math.Pow(CostGrowth, level)); }
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
        public double RewardSeconds;  // reward = max(flat, income * seconds)
        public double RewardFlat;
        public string Focus;          // shop item id the UI can highlight
        public ObjectiveDef(string id, string text, string hint, double flat, double seconds, string focus, Func<Sim, bool> check)
        { Id = id; Text = text; Hint = hint; RewardFlat = flat; RewardSeconds = seconds; Focus = focus; Check = check; }
    }
}
