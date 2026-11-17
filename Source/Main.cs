using HarmonyLib;
using Verse;
using System.Reflection;
using System;
using RimWorld;
using RimWorld.Planet;
 
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