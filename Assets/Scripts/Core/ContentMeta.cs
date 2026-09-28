// Achievements (each +2% sale value forever) and the guided objective chain.
using System.Collections.Generic;

namespace WishExtractor.Core
{
    public static partial class Content
    {
        static AchievementDef[] BuildAchievements()
        {
            var a = new List<AchievementDef>();
            void A(string id, string name, string desc, System.Func<Sim, bool> check) => a.Add(new AchievementDef(id, name, desc, check));

            A("click_1", "First Dig", "Dig something. Anything.", s => s.S.clicks >= 1);
            A("click_1k", "Sticky Fingers", "Click the crust 1,000 times.", s => s.S.clicks >= 1000);
            A("click_10k", "Carpal Tunnel Speedrun", "Click the crust 10,000 times.", s => s.S.clicks >= 1e4);
            A("click_100k", "The Clicking Dead", "Click the crust 100,000 times.", s => s.S.clicks >= 1e5);
            A("dug_1k", "Pocket Change", "Dig up 1,000 items.", s => s.S.totalDug >= 1e3);
            A("dug_1m", "Coin Hoarder", "Dig up 1 million items.", s => s.S.totalDug >= 1e6);
            A("dug_1b", "Fountain Floor Inspector", "Dig up 1 billion items.", s => s.S.totalDug >= 1e9);
            A("dug_1t", "Continental Drift", "Dig up 1 trillion items.", s => s.S.totalDug >= 1e12);
            A("dug_1qa", "Geological Event", "Dig up 1 quadrillion items.", s => s.S.totalDug >= 1e15);
            A("dug_1qi", "Planetary Excavation", "Dig up 1 quintillion items.", s => s.S.totalDug >= 1e18);
            A("dug_1sx", "Hollow Earth Theory", "Dig up 1 sextillion items.", s => s.S.totalDug >= 1e21);
            A("cash_100", "Hundred-Dollar Bill", "Earn $100 in total.", s => s.S.lifetimeCash >= 100);
            A("cash_10k", "Ten Grand of Change", "Earn $10,000 in total.", s => s.S.lifetimeCash >= 1e4);
            A("cash_1m", "Millionaire of the Mall", "Earn $1 million in total.", s => s.S.lifetimeCash >= 1e6);
            A("cash_1b", "Billionaire of Wishes", "Earn $1 billion in total.", s => s.S.lifetimeCash >= 1e9);
            A("cash_1t", "Trillionaire Treasure Hunter", "Earn $1 trillion in total.", s => s.S.lifetimeCash >= 1e12);
            A("cash_1qa", "Quadrillion Quarters", "Earn $1 quadrillion in total.", s => s.S.lifetimeCash >= 1e15);
            A("cash_1qi", "Fountain Tycoon", "Earn $1 quintillion in total.", s => s.S.lifetimeCash >= 1e18);
            A("cash_1sx", "Owner of Every Mall", "Earn $1 sextillion in total.", s => s.S.lifetimeCash >= 1e21);
            A("cash_1sp", "Beyond Money", "Earn $1 septillion in total.", s => s.S.lifetimeCash >= 1e24);
            A("cash_1oc", "Post-Scarcity Plumber", "Earn $1 octillion in total.", s => s.S.lifetimeCash >= 1e27);
            A("wish_1", "Make a Wish", "Catch a True Wish.", s => s.S.wishesCaught >= 1);
            A("wish_25", "Wish Collector", "Catch 25 wishes.", s => s.S.wishesCaught >= 25);
            A("wish_100", "Genie Energy", "Catch 100 wishes.", s => s.S.wishesCaught >= 100);
            A("wish_500", "Professional Wish Catcher", "Catch 500 wishes.", s => s.S.wishesCaught >= 500);
            A("wish_2000", "Keeper of Dreams", "Catch 2,000 wishes.", s => s.S.wishesCaught >= 2000);
            A("journal_10", "Dear Diary", "Record 10 different wishes in the Wish Journal.", s => s.UniqueWishCount >= 10);
            A("journal_40", "Wish Historian", "Record 40 different wishes.", s => s.UniqueWishCount >= 40);
            A("journal_80", "Archivist of Longing", "Record 80 different wishes.", s => s.UniqueWishCount >= 80);
            A("journal_all", "Every Wish Ever Made", "Record every wish in every mall.", s => s.UniqueWishCount >= TotalWishes);
            A("wish_legend", "Once in a Lifetime", "Catch a Legendary wish.", s => s.S.legendaryWish);
            A("compress_100", "Nostalgia Bricklayer", "Let the Wish Compressor press 100 wishes.", s => s.S.wishesCompressed >= 100);
            A("relic_1", "Rare Find!", "Find a relic.", s => s.S.relicsFound >= 1);
            A("relic_25", "Museum Quality", "Find 25 relics.", s => s.S.relicsFound >= 25);
            A("relic_100", "Curator", "Find 100 relics.", s => s.S.relicsFound >= 100);
            A("relic_500", "Relic Baron", "Find 500 relics.", s => s.S.relicsFound >= 500);
            A("relic_legend", "Legendary Find", "Find a Legendary relic.", s => s.S.legendaryRelic);
            A("set_1", "Full Display Case", "Complete a mall's relic collection.", s => s.SetsComplete >= 1);
            A("set_3", "A Whole Wing", "Complete three relic collections.", s => s.SetsComplete >= 3);
            A("set_6", "The Whole Museum", "Complete all six relic collections.", s => s.SetsComplete >= 6);
            A("golden_1", "Lucky Penny", "Click a Golden Penny.", s => s.S.goldenClicked >= 1);
            A("golden_25", "Charmed Life", "Click 25 Golden Pennies.", s => s.S.goldenClicked >= 25);
            A("golden_100", "Midas Touch", "Click 100 Golden Pennies.", s => s.S.goldenClicked >= 100);
            A("rat_1", "Mall Rat Wrangler", "Catch a mall rat running off with loot.", s => s.S.ratsCaught >= 1);
            A("rat_20", "Pied Piper of the Food Court", "Catch 20 mall rats.", s => s.S.ratsCaught >= 20);
            A("combo", "C-C-C-Combo!", "Fill the click combo meter.", s => s.S.comboFilled);
            A("mach_10", "Small Business", "Own 10 machines at once.", s => s.TotalMachines >= 10);
            A("mach_100", "Industrial Revolution", "Own 100 machines at once.", s => s.TotalMachines >= 100);
            A("mach_500", "Mega-Factory", "Own 500 machines at once.", s => s.TotalMachines >= 500);
            A("mach_1500", "Industrial Devastation", "Own 1,500 machines at once.", s => s.TotalMachines >= 1500);
            A("pigeons_100", "The Pigeon Mafia", "Own 100 Pigeon Perches.", s => s.MachineCount("pigeons") >= 100);
            A("walkers_100", "Mall Walk of Fame", "Own 100 Mall Walker Brigades.", s => s.MachineCount("walkers") >= 100);
            A("tumbler_100", "Soda Sommelier", "Own 100 Diet Cola Tumblers.", s => s.MachineCount("tumbler") >= 100);
            A("upgrades_50", "Tech Tree Climber", "Own 50 upgrades in a single mall.", s => s.UpgradesOwnedCount >= 50);
            A("upgrades_100", "Research Department", "Own 100 upgrades in a single mall.", s => s.UpgradesOwnedCount >= 100);
            A("pre90s", "Deep History", "Reach the Pre-90s Stratum.", s => s.S.mallIndex > 0 || s.S.maxStratum >= 6);
            A("mall_1", "Bare Concrete", "Clear Crestview Commons.", s => s.S.maxMallCleared >= 0);
            A("mall_2", "Game Over, Man", "Clear the Neon Galaxy Mega-Mall.", s => s.S.maxMallCleared >= 1);
            A("mall_3", "Old Money", "Clear Galleria Aurelia.", s => s.S.maxMallCleared >= 2);
            A("mall_4", "Final Boarding Call", "Clear Skyport Terminal C.", s => s.S.maxMallCleared >= 3);
            A("mall_5", "Break the Bank", "Clear The Lucky Lagoon.", s => s.S.maxMallCleared >= 4);
            A("mall_6", "The First Wish", "Clear Eternity Plaza.", s => s.S.maxMallCleared >= 5);
            A("remodel_1", "Under New Management", "Clear a Remodel contract.", s => s.S.remodelsDone >= 1);
            A("remodel_6", "Serial Renovator", "Clear six Remodel contracts.", s => s.S.remodelsDone >= 6);
            A("tool_max", "Trident Wielder", "Wield the Trident of Neptune.", s => s.S.tool >= Tools.Length - 1);
            A("big_sale", "Big Spender", "Sell a single pocketful worth $1 million or more.", s => s.S.biggestSale >= 1e6);
            A("ho_10", "Corporate Ladder", "Buy 10 levels of Head Office perks.", s => s.HOTotalLevels >= 10);
            A("ho_50", "Board of Directors", "Buy 50 levels of Head Office perks.", s => s.HOTotalLevels >= 50);
            A("play_1h", "Just One More Dig", "Play for an hour.", s => s.S.playTime >= 3600);
            A("play_8h", "Full Shift", "Play for 8 hours.", s => s.S.playTime >= 8 * 3600);
            A("play_24h", "Dedicated Dredger", "Play for 24 hours.", s => s.S.playTime >= 24 * 3600);
            return a.ToArray();
        }

        static ObjectiveDef[] BuildObjectives()
        {
            var o = new List<ObjectiveDef>();
            void O(string id, string text, string hint, double flat, double secs, string focus, System.Func<Sim, bool> check)
                => o.Add(new ObjectiveDef(id, text, hint, flat, secs, focus, check));

            O("dig1", "Click the crusty fountain floor to dig", "Your gum-on-a-string pulls up one thing per click.", 0.05, 0, null, s => s.S.clicks >= 1);
            O("dig25", "Dig up 25 items", "Keep clicking! Click fast to build your combo.", 0.10, 0, null, s => s.S.totalDug >= 25);
            O("sell1", "Sell your pocket change", "Click the greasy vending machine, or press SELL.", 0.25, 0, null, s => s.S.sales >= 1);
            O("knife", "Buy the Butter Knife", "Tools tab. Pries loose five coins per click.", 0.5, 0, "knife", s => s.S.tool >= 1);
            O("pogo", "Buy a Metal-Detecting Pogo Stick", "Machines dig for you, even while you rest.", 1, 0, "pogo", s => s.MachineCount("pogo") >= 1);
            O("shovel", "Buy the Plastic Sandbox Shovel", "25 items per scoop.", 2, 0, "shovel", s => s.S.tool >= 2);
            O("pogo5", "Own 5 Pogo Sticks", "More bouncing, more crust.", 4, 0, "pogo", s => s.MachineCount("pogo") >= 5);
            O("seal", "Dig down to the Syrup Seal", "Below 3 ft, everything is glued together with forty years of syrup.", 8, 0, null, s => s.S.mallIndex > 0 || s.S.maxStratum >= 1);
            O("tumbler", "Build a Diet Cola Tumbler", "Sticky clumps are almost worthless until they're washed.", 10, 0, "tumbler", s => s.MachineCount("tumbler") >= 1);
            O("wish", "Catch a floating True Wish", "Washing releases glowing wishes. Click one before it drifts away!", 10, 0, null, s => s.S.wishesCaught >= 1);
            O("pigeons", "Hire a Pigeon Perch", "Sorted loot is worth far more than unsorted.", 15, 0, "pigeons", s => s.MachineCount("pigeons") >= 1);
            O("coinop", "Install the Coin-Op Hookup", "Upgrades tab. Sorted loot will sell itself.", 20, 0, "coinop", s => s.AutoSellOwned);
            O("walkers", "Hire the Mall Walker Brigade", "Tracksuits. Stomping. Profit.", 30, 0, "walkers", s => s.MachineCount("walkers") >= 1);
            O("relic", "Find a relic while sorting", "Sorting machines occasionally turn up lost treasures.", 30, 0, null, s => s.S.relicsFound >= 1);
            O("tier2", "Build a Dishwasher and a Coin Star Junction", "Balance your pipeline: dig, wash and sort should keep up with each other.", 100, 30, "dishwasher", s => s.MachineCount("dishwasher") >= 1 && s.MachineCount("coinstar") >= 1);
            O("golden", "Click a Golden Penny", "Every minute or two, one glints somewhere in the crust.", 100, 30, null, s => s.S.goldenClicked >= 1);
            O("upgrade5", "Buy 5 upgrades", "Upgrades tab. Each one is a big permanent boost for this mall.", 150, 30, null, s => s.UpgradesOwnedCount >= 5);
            O("tenft", "Dig down to 10 feet", "The deeper the crust, the older and more valuable the loot.", 300, 45, null, s => s.S.mallIndex > 0 || s.DepthFeet >= 10);
            O("mach25", "Own 25 machines", "Owning 25 of one machine doubles its speed.", 500, 45, null, s => s.TotalMachines >= 25);
            O("beanie", "Break into the Beanie Baby Boom Layer", "Heavy machinery awaits below.", 2000, 60, null, s => s.S.mallIndex > 0 || s.S.maxStratum >= 5);
            O("jackhammer", "Build the Jackhammer Excavator", "Phase three: industrial devastation.", 5000, 60, "jackhammer", s => s.MachineCount("jackhammer") >= 1);
            O("acid", "Build the Acid-Wash River", "Industrial citric acid. Very glowy.", 8000, 60, "acid", s => s.MachineCount("acid") >= 1);
            O("lasers", "Install the Optical Laser Scanners", "Robot arms snatch the good stuff out of the air.", 1e4, 60, "lasers", s => s.MachineCount("lasers") >= 1);
            O("pre90s", "Reach the Pre-90s Stratum", "Wheat pennies. Silver dimes. Real money.", 3e4, 90, null, s => s.S.mallIndex > 0 || s.S.maxStratum >= 6);
            O("compressor", "Build the Wish Compressor", "Catches the wishes you miss and presses them into bricks.", 5e4, 90, "compressor", s => s.MachineCount("compressor") >= 1 || s.S.mallIndex > 0);
            O("concrete", "Hit bare concrete!", "Clear the entire fountain to finish your contract.", 0, 0, null, s => s.S.mallCleared || s.S.mallIndex > 0);
            O("contract", "Sign a contract with a new mall", "Open the contract card or Head Office. You keep Lucky Pennies, journals and relics.", 0, 0, null, s => s.S.mallIndex >= 1);
            O("headoffice", "Spend Lucky Pennies at Head Office", "Permanent perks for every future mall.", 0, 60, null, s => s.HOTotalLevels >= 1);
            O("claw", "Build a Giant Claw Machine", "New tier-4 machines are available in the Mega-Mall.", 0, 120, "claw", s => s.MachineCount("claw") >= 1 || s.S.mallIndex > 1);
            O("mall2", "Clear the Neon Galaxy Mega-Mall", "The Golden Arcade Token waits at 45 ft.", 0, 0, null, s => s.S.maxMallCleared >= 1);
            O("borer", "Build a Gilded Tunnel Borer", "Tier-5 machines are available in the Galleria.", 0, 120, "borer", s => s.MachineCount("borer") >= 1 || s.S.mallIndex > 2);
            O("mall3", "Clear Galleria Aurelia", "The Platinum Membership Card waits at 60 ft.", 0, 0, null, s => s.S.maxMallCleared >= 2);
            O("carousel", "Build the Baggage Carousel Loop", "Megaprojects boost every machine you own.", 0, 120, "carousel", s => s.MachineCount("carousel") >= 1 || s.S.mallIndex > 3);
            O("mall4", "Clear Skyport Terminal C", "The Lost Passport of Everyone waits at 75 ft.", 0, 0, null, s => s.S.maxMallCleared >= 3);
            O("slots", "Build the Slot Machine of Fortune", "The house has decided you win.", 0, 120, "slots", s => s.MachineCount("slots") >= 1 || s.S.mallIndex > 4);
            O("mall5", "Clear The Lucky Lagoon", "The Lucky Die waits at 90 ft.", 0, 0, null, s => s.S.maxMallCleared >= 4);
            O("wishengine", "Build The Wish Engine", "Something ancient hums beneath Eternity Plaza.", 0, 120, "wishengine", s => s.MachineCount("wishengine") >= 1 || s.S.mallIndex > 5);
            O("mall6", "Find the First Wish", "At the very bottom of Eternity Plaza, 120 ft down.", 0, 0, null, s => s.S.maxMallCleared >= 5);
            return o.ToArray();
        }
    }
}
