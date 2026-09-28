// Achievements (each +1% deposit value forever) and the guided objective chain.
using System.Collections.Generic;

namespace WishExtractor.Core
{
    public static partial class Content
    {
        static AchievementDef[] BuildAchievements()
        {
            var a = new List<AchievementDef>();
            void A(string id, string name, string desc, System.Func<Sim, bool> check) => a.Add(new AchievementDef(id, name, desc, check));

            A("pick_1", "Finders Keepers", "Pick up something from the fountain.", s => s.S.itemsPicked >= 1);
            A("pick_100", "Sticky Fingers", "Pick up 100 items.", s => s.S.itemsPicked >= 100);
            A("pick_10k", "Professional Fountain Diver", "Pick up 10,000 items.", s => s.S.itemsPicked >= 1e4);
            A("pick_1m", "The Human Dredge", "Pick up a million items.", s => s.S.itemsPicked >= 1e6);
            A("dep_1", "Cha-Ching", "Feed the COIN-O-MATIC 3000.", s => s.S.deposits >= 1);
            A("dep_100", "Regular Customer", "Make 100 deposits.", s => s.S.deposits >= 100);
            A("dep_1000", "The Machine Knows Your Name", "Make 1,000 deposits.", s => s.S.deposits >= 1000);
            A("big_dep", "Heavy Pockets", "Deposit $1,000 in one trip.", s => s.S.biggestDeposit >= 1000);
            A("big_dep2", "Armoured Car Energy", "Deposit $1 million in one trip.", s => s.S.biggestDeposit >= 1e6);
            A("cash_100", "Hundred-Dollar Bill", "Earn $100 in total.", s => s.S.lifetimeCash >= 100);
            A("cash_10k", "Ten Grand of Change", "Earn $10,000 in total.", s => s.S.lifetimeCash >= 1e4);
            A("cash_1m", "Millionaire of the Mall", "Earn $1 million in total.", s => s.S.lifetimeCash >= 1e6);
            A("cash_1b", "Billionaire of Wishes", "Earn $1 billion in total.", s => s.S.lifetimeCash >= 1e9);
            A("walk_1k", "Mall Walker", "Walk a kilometre.", s => s.S.distance >= 1000);
            A("walk_42k", "Mall Marathon", "Walk 42 kilometres. The mall walkers salute you.", s => s.S.distance >= 42195);
            A("carry_cup", "Upgraded Hands", "Carry more than one thing at a time.", s => s.CarryTier >= 1);
            A("carry_fanny", "Fashion Statement", "Wear the Fanny Pack of Holding.", s => s.CarryTier >= 4);
            A("carry_cart", "No Jumping in the Mall", "Push a shopping cart.", s => s.CarryTier >= 6);
            A("carry_max", "Walking Coin Silo", "Suit up in the Industrial Hopper Suit.", s => s.CarryTier >= Carry.Length - 1);
            A("grab_max", "Keep Away From ATMs", "Wield the Industrial Magnet Glove.", s => s.GrabTier >= GrabTools.Length - 1);
            A("tech_10", "Tech Tree Climber", "Buy 10 tech levels.", s => s.TechLevelsOwned >= 10);
            A("tech_50", "Research Department", "Buy 50 tech levels.", s => s.TechLevelsOwned >= 50);
            A("tech_150", "Maintenance Legend", "Buy 150 tech levels.", s => s.TechLevelsOwned >= 150);
            A("toss_100", "Crowd Pleaser", "Watch shoppers toss 100 things into your fountain.", s => s.S.tosses >= 100);
            A("toss_10k", "Wishing Well-Known", "10,000 tosses.", s => s.S.tosses >= 1e4);
            A("odd_1", "Wait, What?", "Somebody threw something ridiculous in.", s => s.S.oddities >= 1);
            A("odd_100", "Lost & Found Department", "Fish out 100 ridiculous things.", s => s.S.oddities >= 100);
            A("fish_1", "Catch and Release", "Return a goldfish to the water.", s => s.S.fishReturned >= 1);
            A("wish_1", "Make a Wish", "Catch a True Wish.", s => s.S.wishesCaught >= 1);
            A("wish_100", "Genie Energy", "Catch 100 wishes.", s => s.S.wishesCaught >= 100);
            A("journal_10", "Dear Diary", "Record 10 different wishes in the Wish Journal.", s => s.UniqueWishCount >= 10);
            A("journal_50", "Wish Historian", "Record 50 different wishes.", s => s.UniqueWishCount >= 50);
            A("journal_all", "Every Wish Ever Made", "Record every wish in every mall.", s => s.UniqueWishCount >= TotalWishes);
            A("wish_legend", "Once in a Lifetime", "Catch a Legendary wish.", s => s.S.legendaryWish);
            A("relic_1", "Rare Find!", "Find a relic.", s => s.S.relicsFound >= 1);
            A("relic_50", "Museum Quality", "Find 50 relics.", s => s.S.relicsFound >= 50);
            A("relic_legend", "Legendary Find", "Find a Legendary relic.", s => s.S.legendaryRelic);
            A("set_1", "Full Display Case", "Complete a mall's relic collection.", s => s.SetsComplete >= 1);
            A("set_6", "The Whole Museum", "Complete all six relic collections.", s => s.SetsComplete >= 6);
            A("dig_1", "Groundbreaking", "Dig into the crust.", s => s.S.scoops >= 1);
            A("dig_10k", "Excavator", "Dig 10,000 scoops of crust.", s => s.S.scoops >= 1e4);
            A("find_1", "X Marks the Spot", "Open something the crust gave up.", s => s.S.findsOpened >= 1);
            A("find_50", "Professional Treasure Hunter", "Open 50 buried finds.", s => s.S.findsOpened >= 50);
            A("frenzy_1", "Frenzy!", "Set off a frenzy.", s => s.S.frenzies >= 1);
            A("wonder_stage", "Some Assembly Required", "Finish a stage of a mall's Wonder.", s => s.S.wonderStages >= 1);
            A("wonder_1", "Wonder of the Mall", "Finish a mall's Wonder.", s => s.S.wonders.Count >= 1);
            A("wonder_6", "Seven Wonders (Minus One)", "Finish all six Wonders.", s => s.S.wonders.Count >= 6);
            A("fine_1", "Please Exit the Fountain", "Get fined by mall security.", s => s.S.finesPaid >= 1);
            A("rival_1", "Territorial", "Chase off the rival fountain diver.", s => s.S.rivalsChased >= 1);
            A("rival_25", "This Fountain Ain't Big Enough", "Chase off the rival 25 times.", s => s.S.rivalsChased >= 25);
            A("mall_1", "Bare Concrete", "Clear Crestview Commons.", s => s.S.maxMallCleared >= 0);
            A("mall_2", "Game Over, Man", "Clear the Neon Galaxy Mega-Mall.", s => s.S.maxMallCleared >= 1);
            A("mall_3", "Old Money", "Clear Galleria Aurelia.", s => s.S.maxMallCleared >= 2);
            A("mall_4", "Final Boarding Call", "Clear Skyport Terminal C.", s => s.S.maxMallCleared >= 3);
            A("mall_5", "Break the Bank", "Clear The Lucky Lagoon.", s => s.S.maxMallCleared >= 4);
            A("mall_6", "The First Wish", "Clear Eternity Plaza.", s => s.S.maxMallCleared >= 5);
            A("play_1h", "Just One More Trip", "Play for an hour.", s => s.S.playTime >= 3600);
            A("play_8h", "Full Shift", "Play for 8 hours.", s => s.S.playTime >= 8 * 3600);
            A("play_24h", "Dedicated Diver", "Play for 24 hours.", s => s.S.playTime >= 24 * 3600);
            return a.ToArray();
        }

        static ObjectiveDef[] BuildObjectives()
        {
            var o = new List<ObjectiveDef>();
            void O(string id, string text, string hint, double reward, System.Func<Sim, bool> check) => o.Add(new ObjectiveDef(id, text, hint, reward, check));

            O("pick1", "Pick up a coin from the fountain", "Hop over the rim, look at a coin and press E or left-click.", 0, s => s.S.itemsPicked >= 1);
            O("dep1", "Cash it in at the COIN-O-MATIC 3000", "The grimy kiosk by the entrance. Look at it and press E.", 0.05, s => s.S.deposits >= 1);
            O("cup", "Buy a Paper Cup", "At the Maintenance Terminal: the laptop on the folding table by the entrance. Five coins per trip!", 0.10, s => s.CarryTier >= 1);
            O("scrub", "Approve 'Scrub the Grime'", "The Fountain Improvement Plan on the easel by the rim (or the terminal's Fountain tab). A cleaner fountain gets more, richer shoppers.", 0.10, s => s.TechLevel("fountain_scrub") > 0);
            O("dep10", "Make 10 deposits", "Trips add up.", 0.25, s => s.S.deposits >= 10);
            O("wish1", "Catch a True Wish", "Some tosses come with a wish. It rises out of the water where the coin lands. Look at it and press E, quick!", 0.25, s => s.S.wishesCaught >= 1);
            O("grabber", "Buy the Litter Grabber", "More reach: fish coins out from the rim without getting your socks wet.", 0.25, s => s.GrabTier >= 1);
            O("jets", "Fix the water jets", "Next job on the Fountain Improvement Plan.", 0.5, s => s.TechLevel("fountain_jets") > 0);
            O("pail", "Get the Sand Pail", "Twenty coins per trip.", 0.5, s => s.CarryTier >= 2);
            O("toss50", "Watch shoppers toss in 50 things", "The fancier the fountain, the faster they throw.", 0, s => s.S.tosses >= 50);
            O("lights", "Install the coloured lights", "Dimes start flying once the fountain has mood lighting.", 1, s => s.TechLevel("fountain_lights") > 0);
            O("hamster", "Build a Hamster Wheel", "Research it in the terminal's Power tab, then press 3 for build mode and click on the floor.", 1, s => s.CountBuilt("gen_hamster") > 0);
            O("skimmer", "Build a Pool Skimmer Bot at the fountain's edge", "Intake tab. The dock has to stand right by the rim; it turns to face the water by itself.", 2, s => s.CountBuilt("intake_skimmer") > 0);
            O("line", "Belt the skimmer's coins into a Deposit Hopper", "Research Conveyor Belts and Wired Deposit, then drag a belt from the back of the dock to a hopper.", 3, s => s.S.hopperItems >= 1);
            O("net", "Buy the Pool Skimmer Net", "Scoops everything in a small circle. Hold the button and sweep.", 1, s => s.GrabTier >= 2);
            O("bucket", "Upgrade to the Mop Bucket", "Sixty coins per trip. The mop stays in the closet.", 2, s => s.CarryTier >= 3);
            O("neon", "Put up the 'MAKE A WISH' neon", "Quarters at wishability 12, loonies at 20.", 3, s => s.TechLevel("fountain_neon") > 0);
            O("shovel", "Buy a Sandbox Shovel and dig", "Tools tab. Press 2, aim at the crust and click. The fountain has forty years of coins packed under the water.", 2, s => s.S.scoops >= 10 || s.S.mallIndex > 0);
            O("cherub", "Buy the Cherub Statue with Wish Tokens", "Catch wishes to earn tokens. The cherub only accepts wishes.", 0, s => s.TechLevel("fountain_cherub") > 0 || s.S.mallIndex > 0);
            O("seal", "Dig down to the Syrup Seal", "Below the loose coins everything is glued together with forty years of syrup.", 5, s => s.S.maxStratum >= 1 || s.S.mallIndex > 0);
            O("tumbler", "Build a Rinse Tumbler", "Gunk chunks are worth 10% as they are, 35% washed, full price sorted. Processing tab.", 10, s => s.CountBuilt("proc_tumbler") > 0 || s.S.mallIndex > 0);
            O("sorter", "Sort washed loot (Pigeon Sorter)", "Sorting pays full price, and sometimes turns up a relic.", 20, s => s.S.sorted >= 10 || s.S.mallIndex > 0);
            O("digrig", "Build a Crust Jackhammer Rig", "Let a machine do the digging. Belt its chunks through the tumbler and sorter to a hopper.", 50, s => s.CountBuilt("dig_rig") > 0 || s.S.mallIndex > 0);
            O("relic", "Find a relic", "Sorting machines occasionally turn up lost treasures. They're worth a lot at the kiosk.", 50, s => s.S.relicsFound >= 1);
            O("wonder1", "Build the first stage of the Penny Chandelier", "The Wonder Plan easel by the kiosk lists what it needs. Hoppers and the COIN-O-MATIC set those goods aside for it; then approve the stage at the easel.", 50,
                s => s.S.wonderStages >= 1 || s.S.mallIndex > 0);
            O("concrete", "Hit bare concrete", "Clear the whole crust. The mall's bottom treasure is waiting.", 0, s => s.S.mallCleared || s.S.mallIndex > 0);
            O("contract", "Sign the next contract", "Press C (or wait for the contract). You keep Lucky Pennies, relics and the journal.", 0, s => s.S.mallIndex >= 1);
            O("headoffice", "Spend Lucky Pennies at Head Office", "Maintenance Terminal, Head Office tab. Perks last forever.", 0, s => s.S.headOffice.Count > 0 || s.S.mallIndex > 1);
            O("mall2", "Clear the Neon Galaxy Mega-Mall", "The Golden Arcade Token waits at 45 ft.", 0, s => s.S.maxMallCleared >= 1);
            O("mall3", "Clear Galleria Aurelia", "The Platinum Membership Card waits at 60 ft.", 0, s => s.S.maxMallCleared >= 2);
            O("mall4", "Clear Skyport Terminal C", "The Lost Passport of Everyone waits at 75 ft.", 0, s => s.S.maxMallCleared >= 3);
            O("mall5", "Clear The Lucky Lagoon", "The Lucky Die waits at 90 ft.", 0, s => s.S.maxMallCleared >= 4);
            O("mall6", "Find the First Wish", "At the very bottom of Eternity Plaza, 120 ft down.", 0, s => s.S.maxMallCleared >= 5);
            return o.ToArray();
        }
    }
}
