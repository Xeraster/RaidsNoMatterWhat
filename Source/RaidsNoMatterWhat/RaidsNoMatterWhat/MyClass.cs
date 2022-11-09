using System;
using System.Xml;
using RimWorld;
using System.Linq;
using System.Collections.Generic;
using RimWorld.QuestGen;
using Verse;
using UnityEngine;
using HarmonyLib;

namespace RaidsNoMatterWhat
{

    public static class RaidsNoMatterWhatLoader
    {
        public static int staticMax = 1;
        public static int staticMin = 0;
        public static long ticksTillNextRaid = 60000;
        public static Map thisMap;
        public static string lastDocumentElementName;

        static RaidsNoMatterWhatLoader()
        {
            Log.Message("[Raids No Matter What] loaded");
            var harmony = new Harmony("RaidsNoMatterWhat");
            harmony.PatchAll();
            Log.Message("patched harmony assembly for [RaidsNoMatterWhat]");
        }

    }

    //in order to ensure "raids no matter what" lives up to its name, it's hijacking the game tick bypassing any and all possible exterior mechanics. There is virtually no way to prevent this mod from working
    [HarmonyPatch(typeof(TickManager))]
    [HarmonyPatch(nameof(TickManager.DoSingleTick))]
    static class HiJackGameTicker
    {
        static void Prefix()
        {
            //run this on every tick
            RaidsNoMatterWhatLoader.ticksTillNextRaid--;
            if (RaidsNoMatterWhatLoader.ticksTillNextRaid <= 0)
            {
                //make a ship show up
                //IncidentParms paramss = new IncidentParms();
                StorytellerComp storytellerComp = Find.Storyteller.storytellerComps.First((StorytellerComp x) => x is StorytellerComp_OnOffCycle || x is StorytellerComp_RandomMain);
                IncidentParms paramss = storytellerComp.GenerateParms(IncidentCategoryDefOf.ThreatBig, Find.CurrentMap);
                //player

                //if player has multiple bases, pick one at random.
                //otherwise, spawn a trade ship on the 1 map owned by the player
                Map thisMap = Find.RandomPlayerHomeMap;
                if (thisMap != null)
                {
                    //figure out raid points
                    paramss.target = thisMap;
                    float points = StorytellerUtility.DefaultThreatPointsNow(paramss.target);

                    //set up attacking faction
                    Faction attackingFaction = Find.FactionManager.RandomEnemyFaction();
                    paramss.points = Mathf.Max(points, attackingFaction.def.MinPointsToGeneratePawnGroup(PawnGroupKindDefOf.Combat));

                    //set forced = true because that's what QuestGen_Threat.Raid() does
                    paramss.forced = true;

                    //set the group seed
                    paramss.pawnGroupMakerSeed = Rand.Int;

                    //set arrival mode and raid strategy
                    //sappers and breach raids are op so fuck 'em. If you want balanced combat, play SOS2
                    paramss.raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn;
                    paramss.raidStrategy = RaidStrategyDefOf.ImmediateAttack;

                    //ok hopefully that should work
                    RaidsNoMatterWhatLoader.thisMap = thisMap;
                    IncidentDefOf.RaidEnemy.Worker.TryExecute(paramss);
                    //Gen_a
                    Log.Message("Attempted to generate a raid");
                    
                }
                else
                {
                    Log.Message("[RaidsShipsNoMatterWhat] player does not own a map valid map tile. A raid cannot occur");
                }
                //regenerate random number
                System.Random rNum = new System.Random();
                RaidsNoMatterWhatLoader.ticksTillNextRaid = rNum.Next(RaidsNoMatterWhatLoader.staticMin * 60000, RaidsNoMatterWhatLoader.staticMax * 60000);
                Log.Message("There will be " + RaidsNoMatterWhatLoader.ticksTillNextRaid + " ticks until the next raid happens");

            }
        }
    }

    //============================================================================================================================================================================
    //I cannot understate how much I hate this hack. It haunts me in my sleep even if I haven't played rimworld in 6 months
    //I however have not found a cleaner or easier way to do this and it has been extremely reliable so I guess I'm still stuck with it
    //============================================================================================================================================================================

    [HarmonyPatch(typeof(ScribeSaver))]
    [HarmonyPatch(nameof(ScribeSaver.InitSaving))]
    static class saveInfoGetter
    {
        static void Prefix(string filePath, string documentElementName)
        {
            RaidsNoMatterWhatLoader.lastDocumentElementName = documentElementName;
        }
    }

    [HarmonyPatch(typeof(ScribeSaver))]
    [HarmonyPatch(nameof(ScribeSaver.FinalizeSaving))]
    static class SaveGameFinalizeMod
    {
        static void Prefix()
        {
            if (RaidsNoMatterWhatLoader.lastDocumentElementName == "savegame")
            {
                //Log.Message("attemtping to inject via FinalizeSaving()");
                Scribe.saver.EnterNode("RNMW");
                Scribe.saver.WriteAttribute("ticksTillNextRaid", RaidsNoMatterWhatLoader.ticksTillNextRaid.ToString());
                Scribe.saver.WriteAttribute("min", RaidsNoMatterWhatLoader.staticMin.ToString());
                Scribe.saver.WriteAttribute("max", RaidsNoMatterWhatLoader.staticMax.ToString());

                Scribe.saver.ExitNode();
                Log.Message("[Raids No Matter What] done saving");
            }
            else
            {
                Log.Message("[Raids No Matter What] open file is of type " + RaidsNoMatterWhatLoader.lastDocumentElementName + "and not a save game *.rws file. This doesn't indicate an error or anything but if you do get errors, not putting this in the log makes it really hard to find the problem. Sorry for the log spam though.");
            }

        }
    }

    [HarmonyPatch(typeof(ScribeLoader))]
    [HarmonyPatch(nameof(ScribeLoader.FinalizeLoading))]
    static class LoadGameFinalizeMod
    {
        static void Prefix()
        {
            try
            {
                //Log.Message("[Raids No Matter What] Attempting to load values from save file and inside xmlnode " + Scribe.loader.curXmlParent);
                if (Scribe.loader.curXmlParent.Name == "game")
                {
                    //Log.Message("[Raids No Matter What] Exiting the game xml node");
                    //Log.Message("[RNMW] step 1");
                    Scribe.loader.ExitNode();
                }
                if (Scribe.loader.curXmlParent.Name == "savegame")
                {
                    //Log.Message("[RNMW] step 2");
                    //Log.Message("curXmlParent Name = " + Scribe.loader.curXmlParent.Name);
                    //Scribe.loader.EnterNode("savegame");
                    int tempMin = Convert.ToInt32(Scribe.loader.curXmlParent["RNMW"].Attributes["min"].Value);
                    //Log.Message("[RNMW] step 2");
                    int tempMax = Convert.ToInt32(Scribe.loader.curXmlParent["RNMW"].Attributes["max"].Value);
                    //Log.Message("[RNMW] step 3");
                    RaidsNoMatterWhatLoader.ticksTillNextRaid = Convert.ToInt64(Scribe.loader.curXmlParent["RNMW"].Attributes["ticksTillNextRaid"].Value);
                    //Log.Message("[RNMW] step 3");
                    //Scribe.loader.ExitNode(); //don't do this that can fuck things up
                    //Log.Message("[RNMW] step 4");
                    Log.Message("[Raids No Matter What] value loading done. tempmax = " + tempMax);
                    //Log.Message("[RNMW] step 5");

                    if (tempMin != RaidsNoMatterWhatLoader.staticMin || tempMax != RaidsNoMatterWhatLoader.staticMax)
                    {
                        //Log.Message("[RNMW] step 6");
                        //the value got changed while the save wasn't loaded
                        //Log.Message("[Raids No Matter What] has had it's settings changed since last time this save was run. Regenerating timeTillNextRaid");
                        //regenerate random number
                        System.Random rNum = new System.Random();
                        //Log.Message("[RNMW] step 7");
                        RaidsNoMatterWhatLoader.ticksTillNextRaid = rNum.Next(RaidsNoMatterWhatLoader.staticMin * 60000, RaidsNoMatterWhatLoader.staticMax * 60000);
                        //Log.Message("[RNMW] step 8");
                        Log.Message("There will be " + RaidsNoMatterWhatLoader.ticksTillNextRaid + " ticks until the next raid happens");
                        //Log.Message("[RNMW] step 9");
                    }
                    Log.Message("ticksTillNextRaid = " + RaidsNoMatterWhatLoader.ticksTillNextRaid);
                }


            }
            catch (Exception e)
            {
                //do nothing. Generating less spam is nessecary
                //Log.Message("[Raids No Matter What] Not loading a save game (exception caught). This doesn't indicate an error, it's just part of the loading process. Believe it or not, this is the most reliable way of doing this in a way that never causes red errors. The only disadvantage is that it prints this annoying log message");
                //Log.Message("exception = " + e.Message);
            }
        }
    }

    //==============================================================================================================================================================
    //end of the stupid hack that I hate
    //================================================================================================================================================================

    public class RaidsNoMatterWhatSettings : ModSettings
    {
        public int loadID = 0;
        public static bool init = false;
        /// <summary>
        /// The three settings our mod has.
        /// </summary>
        /// 
        // the feature to force uranium traders doesn't work. It's too well locked down with private variables on everything to perform this programmatically
        //public bool forceUraniumTraders;
        //public float exampleFloat = 200f;
        public int minDays = 3;
        public int maxDays = 7;
        public bool saveFileFunctionality;
        //public List<Pawn> exampleListOfPawns = new List<Pawn>();

        /// <summary>
        /// The part that writes our settings to file. Note that saving is by ref.
        /// </summary>
        public override void ExposeData()
        {
            Scribe_Values.Look(ref minDays, "minDays", 3);
            Scribe_Values.Look(ref maxDays, "maxDays", 7);
            base.ExposeData();
            RaidsNoMatterWhatLoader.staticMax = maxDays;
            RaidsNoMatterWhatLoader.staticMin = minDays;
            System.Random rNum = new System.Random();
            RaidsNoMatterWhatLoader.ticksTillNextRaid = rNum.Next(minDays * 60000, maxDays * 60000);
            Log.Message("There will be " + RaidsNoMatterWhatLoader.ticksTillNextRaid + " ticks until the next raid happens");
        }
    }

    public class RaidsNoMatterWhat : Mod
    {

        /// <summary>
        /// A reference to our settings.
        /// </summary>
        public RaidsNoMatterWhatSettings settings;

        public static Vector2 scrollPosition;
        /// <summary>
        /// A mandatory constructor which resolves the reference to our settings.
        /// </summary>
        /// <param name="content"></param>
        public RaidsNoMatterWhat(ModContentPack content) : base(content)
        {
            this.settings = GetSettings<RaidsNoMatterWhatSettings>();
        }

        /// <summary>
        /// The (optional) GUI part to set your settings.
        /// </summary>
        /// <param name="inRect">A Unity Rect with the size of the settings window.</param>
        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);

            listingStandard.Label("Minimum days between raids: " + settings.minDays);
            settings.minDays = Convert.ToInt32(Math.Floor(listingStandard.Slider(settings.minDays, 0, 119)));
            if (settings.maxDays <= settings.minDays) settings.maxDays = settings.minDays + 1;
            listingStandard.Label("Maximum days between raids: " + settings.maxDays);
            settings.maxDays = Convert.ToInt32(Math.Floor(listingStandard.Slider(settings.maxDays, 1, 120)));
            if (settings.minDays >= settings.maxDays) settings.minDays = settings.maxDays - 1;
            listingStandard.CheckboxLabeled("disable save file xml entry", ref settings.saveFileFunctionality, "Raids No Matter What saves an xml node to your save file to avoid regenerating the raid countdown time every time time you load the game. This is the only thing could could possibly break compatibility in future rimworld versions/other similar mods/etc. If you are having problems, turn this option off. It must be noted that you do not have to start a new save for the changes to take effect.");


            listingStandard.End();
            base.DoSettingsWindowContents(inRect);
        }

        /// <summary>
        /// Override SettingsCategory to show up in the list of settings.
        /// Using .Translate() is optional, but does allow for localisation.
        /// </summary>
        /// <returns>The (translated) mod name.</returns>
        public override string SettingsCategory()
        {
            return "Raids No Matter What";
        }
    }
}
