// Number and time formatting shared by the UI and the balance report.
using System;
using System.Globalization;

namespace WishExtractor.Core
{
    public static class Fmt
    {
        public static int Notation; // 0 = K/M/B suffixes, 1 = scientific

        static readonly string[] Suffix =
        {
            "", "K", "M", "B", "T", "Qa", "Qi", "Sx", "Sp", "Oc", "No", "Dc",
            "UDc", "DDc", "TDc", "QaDc", "QiDc", "SxDc", "SpDc", "OcDc", "NoDc", "Vg"
        };

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string Money(double v) => (v < 0 ? "-$" : "$") + Num(Math.Abs(v), true);

        public static string Num(double v, bool money = false)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "∞";
            if (v < 0) return "-" + Num(-v, money);
            if (v < 1000)
            {
                if (money) return v.ToString(v < 100 ? "0.00" : "0", Inv);
                if (v < 10 && v != Math.Floor(v)) return v.ToString("0.#", Inv);
                return Math.Floor(v + 1e-9).ToString("0", Inv);
            }
            int exp = (int)Math.Floor(Math.Log10(v) / 3);
            if (Notation == 1 || exp >= Suffix.Length) return v.ToString("0.00e0", Inv).Replace("e", "e");
            double s = v / Math.Pow(1000, exp);
            string fmt = s < 10 ? "0.00" : s < 100 ? "0.0" : "0";
            string txt = s.ToString(fmt, Inv);
            if (txt == "1000" || txt == "1000.0")
            {
                exp++;
                if (exp >= Suffix.Length) return v.ToString("0.00e0", Inv);
                txt = "1.00";
            }
            return txt + Suffix[exp];
        }

        public static string Int(double v) => v < 1e6 ? Math.Floor(v).ToString("#,0", Inv) : Num(v);

        public static string Rate(double v) => Num(v) + "/s";

        public static string Time(double seconds)
        {
            if (seconds < 0 || double.IsNaN(seconds)) return "—";
            if (double.IsInfinity(seconds) || seconds > 3.15e9) return "forever";
            long s = (long)Math.Round(seconds);
            if (s < 60) return s + "s";
            if (s < 3600) return $"{s / 60}m {s % 60:00}s";
            if (s < 86400) return $"{s / 3600}h {(s % 3600) / 60:00}m";
            return $"{s / 86400}d {(s % 86400) / 3600}h";
        }

        public static string Feet(double ft) => ft < 10 ? ft.ToString("0.0", Inv) + " ft" : ft.ToString("0", Inv) + " ft";

        public static string Pct(double f) => (f * 100).ToString(f < 0.1 ? "0.0" : "0", Inv) + "%";
    }
}
