// Every tunable number that is not part of a specific content entry lives here.
// Tools/BalanceSim runs the real rules against these; tune here, re-run the sim.
namespace WishExtractor.Core
{
    public static class Balance
    {
        // ── the fountain basin (metres; the view builds the same shape) ─────────────
        public const float BasinRadius = 8.0f;          // inner wall
        public const float LandMinR = 1.25f, LandMaxR = 7.35f;  // where tosses land (clear of the centrepiece and the wall)

        // ── NPC tosses ───────────────────────────────────────────────────────────
        // interval between tosses = TossBaseInterval / (1 + wishability * TossPerWish), then × event and tech
        public const double TossBaseInterval = 5.0;
        public const double TossPerWish = 0.06;
        public const double TossFlight = 1.05;          // seconds a toss spends in the air
        public const double SinkTime = 0.9;             // seconds to sink to the crust
        public const int MaxLoose = 2500;               // oldest pennies dissolve into the crust beyond this
        public const double SeedCoins = 400;            // loose coins waiting in a fresh fountain (the carry ladder's first job)

        // Coin tier curve: tier i is allowed once wishability >= its MinWish; the weight of each allowed tier
        // is exp(-(i - mu)^2 / (2 sigma^2)) with mu = wishability * TierPerWish (+ archetype bias).
        public const double TierPerWish = 0.08;
        public const double TierSigma = 1.35;
        public const double OddityBaseChance = 0.02;    // per toss once any oddity is unlocked
        public const double OddityPerWish = 0.0006;
        public const double OddityMaxChance = 0.2;

        // ── the crowd (shoppers walk in, stand at the fountain, toss, leave) ─────────
        public const double CrowdBase = 3, CrowdPerWish = 0.2, CrowdMax = 36;
        public const float StandRadius = 10.7f;         // where shoppers stand to toss (outside the rim and stepping stones)
        public const double WindUp = 0.55;              // seconds from "raise arm" to release
        public const double SpawnInterval = 1.2;        // seconds between arrivals while below the crowd target
        public const double Patience = 14;              // seconds a shopper waits at the rim for their turn
        public const int MaxPendingTosses = 4;
        public const double PortalShare = 0.25;         // wormhole tosses as a share of the crowd's toss rate

        // ── manual collection ────────────────────────────────────────────────────
        public const float WalkSpeed = 4.6f, SprintMult = 1.55f, WadeMult = 0.72f;

        // ── wishes ───────────────────────────────────────────────────────────────
        public const double WishChanceBase = 0.05;      // per toss, before tier bonus
        public const double WishChancePerTier = 0.03;
        public const double WishLife = 14.0;
        public const double WishValueScale = 0.05;      // v1 wish values were sized for billions; first-person money is modest
        public static readonly double[] WishTokens = { 1, 2, 4, 8, 25 };
        public const float WishReach = 7f;              // catch distance for floating wishes
        public const int MaxActiveWishes = 8;
        public static readonly double[] WishRarityWeight = { 60, 25, 10.5, 3.8, 0.7 };
        public const double UndiscoveredBias = 0.6;

        // ── crust ────────────────────────────────────────────────────────────────
        public const double LooseLayerScoops = 400;     // scoops in each mall's loose top layer
        public static readonly double[] StratumStart = { 0.0, 0.10, 0.22, 0.35, 0.48, 0.60, 0.72, 0.86 };
        public static readonly double[] StratumValue = { 1.0, 1.25, 1.5, 1.8, 2.2, 2.7, 3.3, 4.0 };

        // Value realised when depositing each processing stage (fraction of full sorted value).
        public const double GunkRate = 0.10;
        public const double WashedRate = 0.35;
        public const float DigReach = 3.4f;
        public const double ChunkValue = 1.6;           // a gunk chunk carries this many scoops' worth of sorted loot

        public const int RubbleCap = 300;               // hand digging stops while this many gunk chunks are lying around or carried

        // ── processing ───────────────────────────────────────────────────────────
        public const double FactoryStep = 1.0 / 60;     // the factory ticks at this fixed step (see Sim.Tick)
        public const int RollSize = 50, BagSize = 20, PalletSize = 40;
        public const double RollMult = 1.10, BagMult = 1.15, PalletMult = 1.25, MeltMult = 1.35;
        public const double CompressorValue = 0.6;      // a pressed wish brick is worth this share of the wish
        public const double RelicChance = 0.004;        // per item sorted, × (1 + 0.35 × stratum)
        public const double RelicValueScale = 0.05;
        public static readonly double[] RelicRarityWeight = { 55, 27, 12, 5, 1 };

        // ── mall events ──────────────────────────────────────────────────────────
        public const double EventMin = 420, EventMax = 780, EventDuration = 60;

        // ── remodel contracts (after the sixth mall) ───────────────────────────────
        public const double RemodelValueGrowth = 3.0, RemodelCrustGrowth = 1.6;

        // ── permanent bonuses (each group multiplies sale value) ─────────────────
        public const double AchievementBonus = 0.01;
        public const double WishJournalBonus = 0.01;
        public const double RelicSetBonus = 0.10;
        public const double TreasureBonus = 0.25;

        public const double AutosaveSeconds = 30;
    }
}
