using HarmonyLib;
using RimWorld;
using Verse;

namespace RimRTS.Patches
{
    /// <summary>
    /// Shows the "about this mod" window on the title screen, once per game launch.
    ///
    /// How the hook was chosen: the vanilla main menu is drawn by
    /// MainMenuDrawer.MainMenuOnGUI(). We append our window after it has drawn,
    /// so the popup always appears on top of the menu, and we never have to
    /// touch vanilla layout code.
    /// </summary>
    [HarmonyPatch(typeof(MainMenuDrawer))]
    [HarmonyPatch("MainMenuOnGUI")]
    internal static class MainMenuPatch
    {
        // OnGUI runs many times per second, so without this flag the window
        // would be added again and again. Reset only when the game process
        // restarts, which gives us exactly "once per launch".
        private static bool shown;

        [HarmonyPostfix]
        private static void ShowPopupOnce()
        {
            if (shown)
            {
                return;
            }

            shown = true;
            Find.WindowStack.Add(new Dialog_RimRTSInfo());
        }
    }
}
