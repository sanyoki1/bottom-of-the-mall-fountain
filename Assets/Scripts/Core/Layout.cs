// Where things are in the mall hall (metres, y up, fountain at the origin). Shared by the view
// (which builds props there) and the crowd simulation (which walks shoppers around them).
namespace WishExtractor.Core
{
    public static class Layout
    {
        public const float SpawnX = 0, SpawnZ = -18;
        public const float KioskX = -6.5f, KioskZ = -15f;
        public const float TerminalX = 6.5f, TerminalZ = -15f;
        public const float BoardX = 3.6f, BoardZ = -12.4f;

        /// <summary>Doors shoppers come in and leave by (storefronts and the entrance).</summary>
        public static readonly (float x, float z)[] Doors =
        {
            (0, -29), (-14, -29), (14, -29), (-27, -29), (27, -29),
            (-20, 31), (-7, 31), (7, 31), (20, 31),
            (-35, -22), (35, -22), (35, 12),
        };

        /// <summary>Round-ish obstacles shoppers steer around: x, z, radius.</summary>
        public static readonly (float x, float z, float r)[] Obstacles =
        {
            (15.4f, 11.8f, 2.5f), (-15.4f, 11.8f, 2.5f), (-16.6f, -10.6f, 2.5f), (16.6f, -10.6f, 2.5f),
            (-17, 10, 1.5f), (17, 10, 1.5f), (-23, -9, 1.5f), (23, -9, 1.5f),
            (KioskX, KioskZ, 1.2f), (TerminalX, TerminalZ, 1.4f), (BoardX, BoardZ, 0.9f),
            (-27, 26.6f, 1f), (-13.5f, 26.6f, 1f), (0, 26.6f, 1f), (13.5f, 26.6f, 1f), (27, 26.6f, 1f),
            // fountain decor (only there once bought, but it costs nothing to avoid empty floor)
            (-1.6f, 12.2f, 0.4f), (1.6f, 12.2f, 0.4f), (-10.9f, 4.5f, 0.7f), (11.3f, 5.1f, 1.6f), (-4.2f, -11.4f, 0.9f),
            (-29.5f, -1f, 1.7f), (-29.5f, 3f, 1.7f), (-29.5f, 7f, 1.7f), (-29.5f, 11f, 1.7f), (-29.5f, 15f, 1.7f), (-29.5f, 19f, 1.7f), (-29.5f, 23f, 1.7f),
        };

        /// <summary>Angles (degrees) of the stepping stones around the rim; shoppers don't stand on them.</summary>
        public static readonly float[] StoneAngles = { -90, -30, -150, 30, 150, 90 };
    }
}
