using HarmonyLib;
using RimWorld;
using Verse;

namespace RimRTS.Patches
{
    /// Shows the "about this mod" dialog on the title screen, once per game launch.
    ///
    /// This version deliberately references no UnityEngine types, so it builds with
    /// only Assembly-CSharp.dll + 0Harmony.dll. It uses vanilla's Verse.Dialog_MessageBox,
    /// whose public constructor takes only Verse.TaggedString/string/Action/bool types.
    [HarmonyPatch(typeof(MainMenuDrawer))]
    [HarmonyPatch("MainMenuOnGUI")]
    internal static class MainMenuPatch
    {
        private const string AboutText =
            "RimRTS is a total-conversion mod that turns RimWorld into a real-time strategy game: " +
            "squads, direct unit control, and skirmish matches instead of colony management.\n\n" +
            "Currently in development. First milestone: a skirmish vertical slice — " +
            "pick a team, drop into a map, fight, win or lose, restart.\n\n" +
            "v0.1.0 · github.com/Langoman/RimRTS";

        // OnGUI runs many times per second; without this guard the dialog would be
        // queued over and over. It resets on process restart = "once per launch".
        private static bool shown;

        [HarmonyPostfix]
        private static void ShowPopupOnce()
        {
            if (shown)
            {
                return;
            }

            shown = true;

            // TaggedString has an implicit conversion from string, and the dialog's
            // other parameters are optional, hence this compact call.
            Find.WindowStack.Add(new Dialog_MessageBox(
                AboutText,
                buttonAText: "Close",
                title: "RimRTS"));
        }
    }
}
