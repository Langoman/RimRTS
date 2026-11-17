using RimWorld;
using Verse;

namespace RimRTS.Skirmish
{
    /// <summary>
    /// Entry routine for skirmish mode.
    ///
    /// Called from the main menu instead of the vanilla scenario picker. It starts a
    /// NEW game from the RimRTS_Skirmish scenario and lands directly on the vanilla
    /// "select starting site" page, which is the match-setup step that chooses the map
    /// to drop into.
    ///
    /// Vanilla normally walks the player through storyteller -> planet settings ->
    /// starting site. This routine skips the first two pages by driving the vanilla
    /// planet-settings page headlessly: that page owns world generation, so reusing it
    /// keeps our planet identical to a vanilla one (same defaults, same factions, same
    /// generation code) without copying any generation logic into this mod.
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

            // We skip the storyteller page, but Storyteller is a required Game field
            // (map generation, incidents and the UI all read it). Assign the default
            // that the skipped page would have assigned.
            Current.Game.storyteller = new Storyteller(StorytellerDefOf.Cassandra, DifficultyDefOf.Rough);

            // The chain is: storyteller, planet settings, starting site, [ideo], [scenario
            // config pages]. We only need the starting-site page; it already carries the
            // rest of the chain (next / nextAct) that vanilla built for us.
            Page_SelectStartingSite sitePage = FindPage<Page_SelectStartingSite>(origin.next);
            if (sitePage == null)
            {
                // Defensive fallback: if the chain ever changes shape, still let the
                // player through the normal flow instead of leaving them stuck.
                Log.Error("[RimRTS] Could not find the vanilla starting-site page; opening the normal setup flow instead.");
                if (origin.next != null)
                {
                    Find.WindowStack.Add(origin.next);
                }
                return;
            }

            // Generate the planet with vanilla code, then let vanilla open the site page.
            // PreOpen() fills the page's planet defaults (seed, coverage, rainfall, factions,
            // ...) exactly as the real page would; GenerateWorld() queues the same vanilla
            // world-generation long event the "Generate world" button uses.
            HeadlessWorldParams worldGeneration = new HeadlessWorldParams { next = sitePage };
            sitePage.prev = worldGeneration;
            worldGeneration.PreOpen();
            worldGeneration.GenerateWorld();
        }

        private static T FindPage<T>(Page first) where T : Page
        {
            for (Page page = first; page != null; page = page.next)
            {
                if (page is T match)
                {
                    return match;
                }
            }
            return null;
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
