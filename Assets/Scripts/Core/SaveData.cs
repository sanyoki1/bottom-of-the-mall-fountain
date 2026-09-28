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

    /// <summary>A pile of one item type (in hand, in a machine buffer).</summary>
    [Serializable]
    public class SavedStack
    {
        public int type;          // index into SaveData.typeIds
        public int count;
        public double value;      // total base value of the pile
    }

    [Serializable]
    public class SavedBuilding
    {
        public string id;
        public int x, z, rot;
        public List<SavedStack> buf = new List<SavedStack>();
    }

    [Serializable]
    public class SavedBeltItem
    {
        public int b;             // index into SaveData.buildings
        public int type;          // index into SaveData.typeIds
        public float pos;
        public double value;
    }

    [Serializable]
    public class SaveData
    {
        public int version = 2;
        public long lastSaveUnix;

        // economy
        public double cash;
        public double runCash;
        public double lifetimeCash;
        public double wishTokens;
        public double luckyPennies;
        public double lifetimeLP;

        // current mall run
        public int mallIndex;            // 0..5 are the six malls; 6+ are endless remodels
        public int maxMallCleared = -1;  // highest mall index ever cleared
        public bool mallCleared;
        public double dug;               // crust scoops removed in this mall
        public int maxStratum;
        public int objective;
        public double runTime;
        public List<IdCount> tech = new List<IdCount>();

        // the player
        public float px, py = 0.05f, pz = -18f, yaw, pitch;
        public List<SavedStack> carried = new List<SavedStack>();

        // loose items in the fountain (parallel lists; types index into typeIds)
        public List<string> typeIds = new List<string>();
        public List<int> looseType = new List<int>();
        public List<float> looseX = new List<float>();
        public List<float> looseZ = new List<float>();
        public List<double> looseValue = new List<double>();

        // the factory
        public List<SavedBuilding> buildings = new List<SavedBuilding>();
        public List<SavedBeltItem> beltItems = new List<SavedBeltItem>();

        // permanent
        public List<IdCount> headOffice = new List<IdCount>();
        public List<string> wishes = new List<string>();
        public List<IdCount> relics = new List<IdCount>();
        public List<string> achievements = new List<string>();
        public List<string> treasures = new List<string>();

        // stats
        public double itemsPicked, itemsDeposited, deposits, tosses, oddities, scoops, swings;
        public double wishesCaught, wishesSeen, relicsFound, playTime, biggestDeposit, remodelsDone, distance;
        public double finesPaid, rivalsChased, fishReturned;
        public double built, hopperItems, hopperCash, machinePicked;
        public double washed, sorted, bundles, wishesCompressed;
        public bool legendaryWish, legendaryRelic, endingSeen;

        // settings
        public float musicVol = 0.55f;
        public float sfxVol = 0.8f;
        public int notation;             // 0 = short suffixes, 1 = scientific
        public bool screenShake = true;
        public int quality = 2;          // 0 low, 1 medium, 2 high
        public float mouseSens = 1f;
        public float fov = 75f;
        public bool invertY;
        public bool headBob = true;
    }
}
