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
        public const double TossBaseInterval = 7.0;
        public const double TossPerWish = 0.06;
        public const double TossFlight = 1.05;          // seconds a toss spends in the air
        public const double SinkTime = 0.9;             // seconds to sink to the crust
        public const int MaxLoose = 2500;               // oldest pennies dissolve into the crust beyond this
        public const double SeedCoins = 180;            // loose coins waiting in a fresh fountain

        // Coin tier curve: tier i is allowed once wishability >= its MinWish; the weight of each allowed tier
        // is exp(-(i - mu)^2 / (2 sigma^2)) with mu = wishability * TierPerWish (+ archetype bias).
        public const double TierPerWish = 0.075;
        public const double TierSigma = 1.35;
        public const double OddityBaseChance = 0.02;    // per toss once any oddity is unlocked
        public const double OddityPerWish = 0.0006;

        // ── manual collection ────────────────────────────────────────────────────
        public const float WalkSpeed = 4.6f, SprintMult = 1.55f, WadeMult = 0.72f;

        // ── wishes ───────────────────────────────────────────────────────────────
        public const double WishChanceBase = 0.05;      // per toss, before tier bonus
        public const double WishChancePerTier = 0.03;
        public const double WishLife = 14.0;
        public static readonly double[] WishRarityWeight = { 60, 25, 10.5, 3.8, 0.7 };
        public const double UndiscoveredBias = 0.6;

        // ── crust ────────────────────────────────────────────────────────────────
        public const double LooseLayerScoops = 400;     // scoops in each mall's loose top layer
        public static readonly double[] StratumStart = { 0.0, 0.10, 0.22, 0.35, 0.48, 0.60, 0.72, 0.86 };
        public static readonly double[] StratumValue = { 1.0, 1.25, 1.5, 1.8, 2.2, 2.7, 3.3, 4.0 };

        // Value realised when depositing each processing stage (fraction of full sorted value).
        public const double GunkRate = 0.10;
        public const double WashedRate = 0.35;

        // ── permanent bonuses (each group multiplies sale value) ─────────────────
        public const double AchievementBonus = 0.01;
        public const double WishJournalBonus = 0.01;
        public const double RelicSetBonus = 0.10;
        public const double TreasureBonus = 0.25;

        public const double AutosaveSeconds = 30;
    }
}
