using HarmonyLib;
using Verse;
 
namespace RimRTS
{
    [StaticConstructorOnStartup]
    public static class RimRTS
    {
        static RimRTS()
        {
            var harmony = new Harmony("Lango.RimRTS");
            harmony.PatchAll();
            Log.Message("[RimRTS] loaded Harmony patches.");
        }
    }
}