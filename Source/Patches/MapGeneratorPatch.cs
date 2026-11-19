using HarmonyLib;
using RimRTS.Skirmish;
using Verse;

namespace RimRTS.Patches
{
    /// <summary>
    /// Turns the vanilla square initial map into the layout the match screen requested:
    /// the prefix replaces the size argument, and the postfix stamps a hand-authored
    /// <see cref="PrefabDef"/> once the map is generated and initialized.
    ///
    /// Both halves read the same one-shot <see cref="BattlefieldLayout"/>; it is only set when
    /// the player presses "Start match", so every other map in the game is generated untouched.
    /// </summary>
    [HarmonyPatch(typeof(MapGenerator), nameof(MapGenerator.GenerateMap))]
    internal static class MapGeneratorPatch
    {
        private static void Prefix(ref IntVec3 mapSize)
        {
            BattlefieldLayout.ApplySize(ref mapSize);
        }

        private static void Postfix(Map __result)
        {
            BattlefieldLayout.Apply(__result);
        }
    }
}
