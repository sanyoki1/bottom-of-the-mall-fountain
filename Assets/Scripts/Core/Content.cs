// Content registry: builds every definition table once and indexes them by id.
using System.Collections.Generic;

namespace WishExtractor.Core
{
    public static partial class Content
    {
        public static readonly MallDef[] Malls;
        public static readonly MachineDef[] Machines;
        public static readonly ToolDef[] Tools;
        public static readonly UpgradeDef[] Upgrades;
        public static readonly HeadOfficeDef[] HeadOffice;
        public static readonly AchievementDef[] Achievements;
        public static readonly ObjectiveDef[] Objectives;

        public static readonly Dictionary<string, int> MachineIndex = new Dictionary<string, int>();
        public static readonly Dictionary<string, int> UpgradeIndex = new Dictionary<string, int>();
        public static readonly Dictionary<string, int> HOIndex = new Dictionary<string, int>();
        public static readonly Dictionary<string, int> AchievementIndex = new Dictionary<string, int>();
        public static readonly Dictionary<string, int> ToolIndex = new Dictionary<string, int>();
        public static readonly Dictionary<string, WishDef> WishById = new Dictionary<string, WishDef>();
        public static readonly Dictionary<string, RelicDef> RelicById = new Dictionary<string, RelicDef>();
        public static readonly int CompressorIndex;
        public static readonly int TotalWishes;
        public static readonly int TotalRelics;

        static Content()
        {
            Malls = BuildMalls();
            Machines = BuildMachines();
            Tools = BuildTools();
            Upgrades = BuildUpgrades();
            HeadOffice = BuildHeadOffice();
            Achievements = BuildAchievements();
            Objectives = BuildObjectives();

            foreach (var m in Machines) MachineIndex[m.Id] = m.Index;
            foreach (var u in Upgrades) UpgradeIndex[u.Id] = u.Index;
            foreach (var h in HeadOffice) HOIndex[h.Id] = h.Index;
            foreach (var t in Tools) ToolIndex[t.Id] = t.Index;
            for (int i = 0; i < Achievements.Length; i++) { Achievements[i].Index = i; AchievementIndex[Achievements[i].Id] = i; }
            CompressorIndex = MachineIndex["compressor"];
            foreach (var mall in Malls)
            {
                foreach (var w in mall.Wishes) { WishById[w.Id] = w; TotalWishes++; }
                foreach (var r in mall.Relics) { RelicById[r.Id] = r; TotalRelics++; }
            }
        }

        /// <summary>Returns the shop entry (machine, tool or upgrade) name for an id, for UI hints.</summary>
        public static string NameOf(string id)
        {
            if (id == null) return "";
            if (MachineIndex.TryGetValue(id, out int m)) return Machines[m].Name;
            if (ToolIndex.TryGetValue(id, out int t)) return Tools[t].Name;
            if (UpgradeIndex.TryGetValue(id, out int u)) return Upgrades[u].Name;
            if (HOIndex.TryGetValue(id, out int h)) return HeadOffice[h].Name;
            return id;
        }
    }
}
