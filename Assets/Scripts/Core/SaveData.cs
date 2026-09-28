// Everything persisted between sessions. Plain public fields so Unity's JsonUtility
// can serialize it; content is referenced by string id so saves survive re-ordering.
using System;
using System.Collections.Generic;

namespace WishExtractor.Core
{
    [Serializable]
    public class IdCount
    {
        public string id;
        public int count;
        public IdCount() { }
        public IdCount(string id, int count) { this.id = id; this.count = count; }
    }

    [Serializable]
    public class SaveData
    {
        public int version = 1;
        public long lastSaveUnix;

        // economy
        public double cash;
        public double runCash;
        public double lifetimeCash;
        public double luckyPennies;
        public double lifetimeLP;

        // current mall run
        public int mallIndex;            // 0..5 are the six malls; 6+ are endless remodels
        public int maxMallCleared = -1;  // highest mall index ever cleared
        public bool mallCleared;
        public double dug;
        public int maxStratum;
        public double hopperCount, hopperValue;  // raw clumps
        public double trayCount, trayValue;      // washed, unsorted
        public double pocketCount, pocketValue;  // sorted, ready to sell
        public int tool;
        public List<IdCount> machines = new List<IdCount>();
        public List<string> upgrades = new List<string>();
        public int objective;
        public double runTime;

        // permanent
        public List<IdCount> headOffice = new List<IdCount>();
        public List<string> wishes = new List<string>();
        public List<IdCount> relics = new List<IdCount>();
        public List<string> achievements = new List<string>();
        public List<string> treasures = new List<string>();

        // stats
        public double clicks, totalDug, wishesCaught, wishesCompressed, wishesSeen, relicsFound;
        public double goldenClicked, ratsCaught, playTime, biggestSale, remodelsDone, bestCombo, sales;
        public bool legendaryWish, legendaryRelic, comboFilled, endingSeen;

        // settings
        public float musicVol = 0.55f;
        public float sfxVol = 0.8f;
        public int notation;             // 0 = short suffixes, 1 = scientific
        public bool screenShake = true;
        public bool autoSellOn = true;
        public int quality = 2;          // 0 low, 1 medium, 2 high
    }
}
