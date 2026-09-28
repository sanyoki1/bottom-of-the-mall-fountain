// Content registry: builds every definition table once and indexes them by id.
using System.Collections.Generic;

namespace WishExtractor.Core
{
    public static partial class Content
    {
        public static readonly MallDef[] Malls;
        public static readonly ItemType[] Items;
        public static readonly CarryDef[] Carry;
        public static readonly ToolDef[] GrabTools;
        public static readonly ToolDef[] DigTools;
        public static readonly TechDef[] Techs;
        public static readonly ArchetypeDef[] Archetypes;
        public static readonly AchievementDef[] Achievements;
        public static readonly ObjectiveDef[] Objectives;

        public static readonly Dictionary<string, int> AchievementIndex = new Dictionary<string, int>();
        public static readonly Dictionary<string, WishDef> WishById = new Dictionary<string, WishDef>();
        public static readonly Dictionary<string, RelicDef> RelicById = new Dictionary<string, RelicDef>();
        public static readonly int TotalWishes;
        public static readonly int TotalRelics;

        static Content()
        {
            Malls = BuildMalls();
            BuildCoins();
            BuildOddities();
            BuildMallTypes();
            Items = types.ToArray();
            Carry = BuildCarry();
            GrabTools = BuildGrabTools();
            DigTools = BuildDigTools();
            Techs = BuildTechs();
            Archetypes = BuildArchetypes();
            Achievements = BuildAchievements();
            Objectives = BuildObjectives();

            for (int i = 0; i < Achievements.Length; i++) { Achievements[i].Index = i; AchievementIndex[Achievements[i].Id] = i; }
            foreach (var mall in Malls)
            {
                foreach (var w in mall.Wishes) { WishById[w.Id] = w; TotalWishes++; }
                foreach (var r in mall.Relics) { RelicById[r.Id] = r; TotalRelics++; }
            }
        }

        /// <summary>Registry index for an item id, or -1.</summary>
        public static int TypeOrNone(string id) => id != null && ItemIndex.TryGetValue(id, out int i) ? i : -1;
    }
}
