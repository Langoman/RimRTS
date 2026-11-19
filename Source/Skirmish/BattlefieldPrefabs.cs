using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RimRTS.Skirmish
{
    /// <summary>
    /// Catalog of the hand-authored battlefields shipped with the mod.
    ///
    /// Any <see cref="PrefabDef"/> in the mod whose defName starts with <see cref="DefNamePrefix"/>
    /// is offered on the match screen to replace the procedural surface of a map. Restricting by
    /// prefix keeps the list to layouts we authored instead of every prefab DLCs expose (props,
    /// ancient rooms, quest sites), and it degrades cleanly when those DLCs are absent: the list
    /// is simply empty.
    /// </summary>
    internal static class BattlefieldPrefabs
    {
        public const string DefNamePrefix = "RimRTS_";

        public static List<PrefabDef> All()
        {
            return DefDatabase<PrefabDef>.AllDefs
                .Where(def => def.defName.StartsWith(DefNamePrefix, StringComparison.OrdinalIgnoreCase))
                .OrderBy(Label, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Readable name for the screen; a prefab author may omit the def's label, so fall back
        /// to the defName (which already carries the shared prefix).
        /// </summary>
        public static string Label(PrefabDef prefab)
        {
            return string.IsNullOrEmpty(prefab.label) ? prefab.defName : prefab.label;
        }
    }
}
