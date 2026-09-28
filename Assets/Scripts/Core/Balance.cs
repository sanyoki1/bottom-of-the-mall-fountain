// Every tunable number that is not part of a specific content entry lives here.
// Tools/BalanceSim runs the real Sim against these; tune here, re-run the sim.
namespace WishExtractor.Core
{
    public static class Balance
    {
        // Value realised when selling from each buffer (fraction of full sorted value).
        public const double RawRate = 0.10;
        public const double WashedRate = 0.35;

        // Wishes float up while the wash stage is running.
        public const double WishBaseInterval = 11.0;
        public const double WishLife = 13.0;
        public const int MaxActiveWishes = 10;
        public static readonly double[] WishRarityWeight = { 60, 25, 10.5, 3.8, 0.7 };
        public static readonly double[] WishIncomeSeconds = { 4, 10, 30, 90, 360 };
        public const double UndiscoveredBias = 0.6;
        public const double CompressorBaseCapture = 0.30;
        public const double CompressorCapturePerLevel = 0.02;
        public const double CompressorMaxCapture = 0.95;
        public const double CompressorValue = 0.8;

        // Relics roll while the sort stage is running.
        public const double RelicBaseInterval = 80.0;
        public static readonly double[] RelicRarityWeight = { 55, 27, 12.5, 4.5, 1.0 };
        public static readonly double[] RelicIncomeSeconds = { 20, 45, 120, 300, 900 };

        // Random clickables and timed events.
        public const double GoldenMin = 75, GoldenMax = 170, GoldenLife = 13;
        public const double RatMin = 200, RatMax = 380, RatLife = 11;
        public const double EventMin = 300, EventMax = 420, EventDuration = 40;

        // Click combo: each click adds one charge, the meter drains after a pause.
        public const double ComboCap = 30;
        public const double ComboBaseMax = 2.0;
        public const double ComboDecayDelay = 0.9;

        // Owning this many of one machine doubles its output (cumulative).
        public static readonly int[] Milestones = { 25, 50, 100, 150, 200, 250, 300, 350, 400, 450, 500, 600, 700, 800, 900, 1000 };
        public const double MilestoneMult = 2.0;

        // Offline progress.
        public const double OfflineBaseHours = 2;
        public const double OfflineBaseEff = 0.5;

        // Permanent bonuses (each group multiplies sale value).
        public const double AchievementBonus = 0.01;
        public const double WishJournalBonus = 0.01;
        public const double RelicSetBonus = 0.10;
        public const double TreasureBonus = 0.25;

        // Endless remodels after the last mall: each lap re-runs the six malls bigger and pricier.
        public const double RemodelSizeGrowth = 3;
        public const double RemodelCostGrowth = 1e24;
        public const double RemodelGenerosity = 1.5;
        public const double RemodelLuckyGrowth = 1.6;

        // Stratum value multipliers (shared by every mall).
        public static readonly double[] StratumValue = { 1.0, 1.25, 1.5, 1.8, 2.2, 2.7, 3.3, 4.0 };

        // Items in each mall's loose top layer (the first 10% of depth); fixes the crust density curve.
        public const double LooseLayerItems = 40000;
        public static readonly double[] StratumStart = { 0.0, 0.10, 0.22, 0.35, 0.48, 0.60, 0.72, 0.86 };

        // Stratum at which each machine tier unlocks, per mall (column = mall 1..6, remodels use the last).
        // Crestview (column 0) uses each machine's own UnlockStratum instead.
        public static readonly int[,] TierUnlock =
        {
            //        m1  m2  m3  m4  m5  m6
            /*T1*/ {  1,  1,  1,  1,  1,  1 },
            /*T2*/ {  2,  1,  1,  1,  1,  1 },
            /*T3*/ {  5,  3,  2,  2,  2,  2 },
            /*T4*/ { 99,  5,  4,  3,  3,  3 },
            /*T5*/ { 99, 99,  6,  5,  4,  4 },
        };
        public static readonly int[] CompressorUnlock = { 5, 4, 3, 3, 3, 3 };
        public const int MegaUnlockStratum = 3;

        public const double AutosaveSeconds = 30;
    }
}
