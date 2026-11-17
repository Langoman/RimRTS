using RimWorld;
using Verse;

namespace RimRTS.Skirmish
{
    /// <summary>
    /// Entry routine for skirmish mode.
    ///
    /// Called from the main menu instead of the vanilla scenario picker. It starts a NEW
    /// game from the RimRTS_Skirmish scenario and lands directly on the match-setup screen
    /// that lets the player pick the biome to drop into.
    ///
    /// Vanilla walks the player through storyteller -> planet settings -> starting site ->
    /// ideology preset. This routine replaces all four screens with a single biome screen:
    ///  - the storyteller is assigned in code (a plain field, no screen needed);
    ///  - the planet-settings page is driven headlessly, because it owns world generation
    ///    (same defaults, factions and generation code as vanilla, no copied logic);
    ///  - the starting-site page is replaced by <see cref="Page_ChooseBiome"/>;
    ///  - the ideology preset page is never shown, but the state it would have created is
    ///    produced by <see cref="ApplyIdeologySetup"/>.
    /// After the biome screen the vanilla scenario config pages continue (the starting-pawn
    /// choice), then <see cref="Page_EquipSquad"/> arms those pawns, and the match starts
    /// through the regular PageUtility.InitGameStart() path.
    /// </summary>
    public static class ScenarioEntry
    {
        public const string ScenarioDefName = "RimRTS_Skirmish";

        public static void Start()
        {
            ScenarioDef def = DefDatabase<ScenarioDef>.GetNamed(ScenarioDefName);
            if (def == null)
            {
                Log.Error("[RimRTS] Scenario def '" + ScenarioDefName + "' is missing; cannot start a match.");
                return;
            }

            // BeginScenarioConfiguration() is the vanilla entry point for a new game:
            // it clears caches, creates Game + GameInitData, assigns the scenario,
            // runs the scenario's PreConfigure(), and links the vanilla config-page
            // chain onto the page we pass as the origin.
            Page_SelectScenario origin = new Page_SelectScenario();
            Page_SelectScenario.BeginScenarioConfiguration(def.scenario, origin);

            // Storyteller is a required Game field (map generation, incidents and the UI
            // read it). The skipped storyteller page assigns it, so we assign the same
            // vanilla defaults here.
            Current.Game.storyteller = new Storyteller(StorytellerDefOf.Cassandra, DifficultyDefOf.Rough);

            // Walk the vanilla chain until the first page that still belongs to match
            // setup (normally a scenario config page or the end of the chain). The pages
            // we skip are: storyteller, planet settings, starting site, ideology preset.
            Page firstConfigPage = FindFirstPageAfterVanillaSetup(origin.next);

            // Equipment comes last, after the vanilla config pages (the pawn choice): the
            // player picks pawns first, arms them second, and only then starts the match.
            // Splicing at the end of the chain keeps this independent of how many config
            // pages the scenario has.
            Page_EquipSquad equipPage = new Page_EquipSquad();
            if (firstConfigPage != null)
            {
                Page lastConfigPage = LastPage(firstConfigPage);
                equipPage.next = lastConfigPage.next;
                equipPage.nextAct = lastConfigPage.nextAct;
                equipPage.prev = lastConfigPage;
                lastConfigPage.next = equipPage;
                lastConfigPage.nextAct = null;
            }
            else
            {
                equipPage.nextAct = PageUtility.InitGameStart;
            }

            // The biome screen opens the remaining chain: config pages, then equipment.
            Page afterBiomePage = firstConfigPage ?? equipPage;
            Page_ChooseBiome biomePage = new Page_ChooseBiome { next = afterBiomePage };
            afterBiomePage.prev = biomePage;

            // Generate the planet with vanilla code. PreOpen() fills the page's planet
            // defaults (seed, coverage, rainfall, factions, ...) exactly as the real page
            // would; GenerateWorld() queues the same vanilla world-generation long event
            // the "Generate world" button uses. When generation finishes, vanilla opens
            // page.next, which is our biome screen (see HeadlessWorldParams comment).
            HeadlessWorldParams worldGeneration = new HeadlessWorldParams { next = biomePage };
            worldGeneration.PreOpen();
            worldGeneration.GenerateWorld();
        }

        /// <summary>
        /// Creates the ideology state that the removed ideoligion screen used to create:
        /// the player's classic ideoligion plus generated ideos for the other factions.
        /// Without it a new game has no player ideoligion and pawn generation misbehaves.
        /// Must run after <see cref="Start"/> (Game and storyteller exist) and after the
        /// planet exists; the biome screen calls it when the player confirms the match.
        /// </summary>
        internal static void ApplyIdeologySetup()
        {
            if (!ModsConfig.IdeologyActive)
            {
                return;
            }

            // PostOpen() is where the vanilla page does the generation work. We never add
            // the page to the window stack, so only this data step runs, no screen.
            new Page_ChooseIdeoPreset().PostOpen();

            // With no preset selection the player is in classic ideology mode; vanilla's
            // DoClassic() path sets the same flag and runs the scenario hook.
            Find.IdeoManager.classicMode = true;
            Find.Scenario.PostIdeoChosen();
        }

        private static Page FindFirstPageAfterVanillaSetup(Page page)
        {
            for (; page != null; page = page.next)
            {
                if (!IsVanillaSetupPage(page))
                {
                    return page;
                }
            }
            return null;
        }

        private static Page LastPage(Page page)
        {
            while (page.next != null)
            {
                page = page.next;
            }
            return page;
        }

        private static bool IsVanillaSetupPage(Page page)
        {
            return page is Page_SelectStoryteller
                || page is Page_CreateWorldParams
                || page is Page_SelectStartingSite
                || page is Page_ChooseIdeoPreset;
        }

        /// <summary>
        /// Page_CreateWorldParams.CanDoNext() is protected because it is a page action:
        /// it queues planet generation and deliberately returns false (the page does not
        /// advance on its own; the long event opens the next page when generation ends).
        /// Exposing it through a one-line subclass is cleaner and less fragile than
        /// reflecting on a protected method by string name.
        /// </summary>
        private sealed class HeadlessWorldParams : Page_CreateWorldParams
        {
            public void GenerateWorld()
            {
                CanDoNext();
            }
        }
    }
}
