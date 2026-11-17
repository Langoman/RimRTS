using HarmonyLib;
using RimWorld;
using RimRTS.Skirmish;
using Verse;

namespace RimRTS.Patches
{
    /// <summary>
    /// Replaces the main menu's "New colony" action with the skirmish entry routine.
    ///
    /// Vanilla's New Colony button is a compiler-generated lambda whose entire body is
    /// <c>Find.WindowStack.Add(new Page_SelectScenario())</c>, so there is no named method
    /// to patch. Intercepting WindowStack.Add for that one page type is the narrowest
    /// Unity-free hook: anything else that adds a window passes through untouched.
    /// </summary>
    [HarmonyPatch(typeof(WindowStack))]
    [HarmonyPatch("Add")]
    [HarmonyPatch(new[] { typeof(Window) })]
    internal static class MainMenuNewGamePatch
    {
        [HarmonyPrefix]
        private static bool StartSkirmishInstead(Window window)
        {
            if (!(window is Page_SelectScenario))
            {
                return true;
            }

            // Returning false skips the vanilla Add: the scenario picker never opens,
            // and our entry routine builds the match-setup flow instead.
            ScenarioEntry.Start();
            return false;
        }
    }
}
