// The Maintenance Terminal's tech tree. Ladder rungs (carry, grab, dig) are generated from
// the ladders in ContentWorld so prices live in one place.
using System.Collections.Generic;

namespace WishExtractor.Core
{
    public static partial class Content
    {
        public static readonly Dictionary<string, int> TechIndex = new Dictionary<string, int>();

        static TechDef[] BuildTechs()
        {
            var list = new List<TechDef>();
            TechDef T(string id, string name, TechBranch branch, TechKind kind, double value, double cost, string desc, params string[] req)
            {
                var t = new TechDef { Id = id, Name = name, Branch = branch, Kind = kind, Value = value, Cost = cost, Desc = desc, Requires = req, Index = list.Count };
                list.Add(t);
                return t;
            }

            // carry ladder
            for (int i = 1; i < Carry.Length; i++)
            {
                var c = Carry[i];
                var t = T("carry_" + c.Id, c.Name, TechBranch.Carry, TechKind.Carry, i, c.Cost, c.Desc, i > 1 ? new[] { "carry_" + Carry[i - 1].Id } : new string[0]);
                t.Col = i - 1; t.Row = 0;
            }
            // grab tools
            for (int i = 1; i < GrabTools.Length; i++)
            {
                var g = GrabTools[i];
                var t = T("grab_" + g.Id, g.Name, TechBranch.Tools, TechKind.Tool, i, g.Cost, g.Desc, i > 1 ? new[] { "grab_" + GrabTools[i - 1].Id } : new string[0]);
                t.Target = "grab"; t.Col = i - 1; t.Row = 0;
            }
            // dig tools
            for (int i = 1; i < DigTools.Length; i++)
            {
                var d = DigTools[i];
                var t = T("dig_" + d.Id, d.Name, TechBranch.Tools, TechKind.Tool, i, d.Cost, d.Desc, i > 1 ? new[] { "dig_" + DigTools[i - 1].Id } : new string[0]);
                t.Target = "dig"; t.Col = i - 1; t.Row = 1;
            }

            var arr = list.ToArray();
            foreach (var t in arr) TechIndex[t.Id] = t.Index;
            return arr;
        }
    }
}
