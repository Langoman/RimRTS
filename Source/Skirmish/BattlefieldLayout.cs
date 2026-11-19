using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RimRTS.Skirmish
{
    /// <summary>
    /// The map the match screen asked for: its size, plus optionally a hand-authored
    /// <see cref="PrefabDef"/> that replaces the procedural content of that map.
    ///
    /// <see cref="Page_ChooseBiome"/> calls <see cref="Begin"/> when the player presses
    /// "Start match". <see cref="Patches.MapGeneratorPatch"/> then consumes the layout while
    /// vanilla builds the initial map: the prefix swaps the square vanilla size for ours, and
    /// the postfix stamps the prefab (clearing the generated things first). The layout is
    /// consumed exactly once, so later maps (quests, pocket maps, caravans) are untouched.
    ///
    /// This class knows nothing about UI, which keeps a future "start a match from code"
    /// path able to use authored maps without going through the biome page.
    /// </summary>
    public sealed class BattlefieldLayout
    {
        private static BattlefieldLayout pending;

        private readonly IntVec3 size;
        private readonly PrefabDef prefab;

        private BattlefieldLayout(IntVec3 size, PrefabDef prefab)
        {
            this.size = size;
            this.prefab = prefab;
        }

        public IntVec3 Size => size;

        public PrefabDef Prefab => prefab;

        /// <summary>
        /// Registers the layout for the next generated map. A null prefab means "procedural
        /// terrain, just use this size".
        /// </summary>
        public static void Begin(IntVec3 size, PrefabDef prefab)
        {
            pending = new BattlefieldLayout(size, prefab);
        }

        public static void ApplySize(ref IntVec3 mapSize)
        {
            if (pending != null)
            {
                mapSize = pending.size;
            }
        }

        /// <summary>
        /// Called once vanilla has finished generating (and initializing) the map. Consumes the
        /// pending layout even when the prefab cannot be stamped, so a failed hand-authored map
        /// never leaks into a later generation.
        /// </summary>
        public static void Apply(Map map)
        {
            BattlefieldLayout layout = pending;
            pending = null;

            if (layout == null || layout.prefab == null || map == null)
            {
                return;
            }

            layout.Stamp(map);
        }

        private void Stamp(Map map)
        {
            IntVec2 prefabSize = prefab.size;
            if (prefabSize.x <= 0 || prefabSize.z <= 0)
            {
                Log.Error("[RimRTS] Prefab '" + prefab.defName + "' has an invalid size " + prefabSize + ".");
                return;
            }

            if (prefabSize.x > map.Size.x || prefabSize.z > map.Size.z)
            {
                Log.Error(
                    "[RimRTS] Prefab '" + prefab.defName + "' is " + prefabSize.x + "x" + prefabSize.z
                    + " but the map is " + map.Size.x + "x" + map.Size.z + "; skipping the authored layout.");
                return;
            }

            IntVec3 origin = new IntVec3((map.Size.x - prefabSize.x) / 2, 0, (map.Size.z - prefabSize.z) / 2);

            // SpawnPrefab treats the position it receives as the prefab's CENTER cell, not its
            // top-left corner: internally it builds GenAdj.OccupiedRect(anchor, rot, size) and
            // takes one of that rect's corners as the root. Passing the corner origin directly
            // shifted the whole prefab up-left by roughly half its size, which is why terrain
            // ended up at negative coordinates. The reverse of that formula is anchor = corner +
            // (size - 1) / 2, matching the integer division used by OccupiedRect.
            IntVec3 anchor = new IntVec3(
                origin.x + (prefabSize.x - 1) / 2,
                0,
                origin.z + (prefabSize.z - 1) / 2);

            // The prefab is meant to define the whole battlefield, so whatever procedural
            // generation produced (plants, rocks, ancient dangers, roofs over those rooms,
            // terrain decorations) has to go first, otherwise prefab walls/cover can end up
            // buried under it or the authored open ground ends up under a generated roof.
            ClearGeneratedContent(map);

            try
            {
                // SpawnPrefab writes the prefab terrain before spawning its things, so authored
                // layouts may place things on ground that the prefab itself introduces.
                PrefabUtility.SpawnPrefab(
                    prefab,
                    map,
                    anchor,
                    Rot4.North,
                    Faction.OfPlayerSilentFail,
                    null,
                    null,
                    null,
                    false);
            }
            catch (Exception exception)
            {
                Log.Error("[RimRTS] Failed to stamp prefab '" + prefab.defName + "': " + exception);
            }
        }

        private static void ClearGeneratedContent(Map map)
        {
            List<Thing> things = map.listerThings.AllThings.ToList();
            for (int i = things.Count - 1; i >= 0; i--)
            {
                things[i].Destroy(DestroyMode.Vanish);
            }

            // Roofs outlive the things under them (ancient-danger rooms are roofed), so a
            // cleared map would keep invisible roofs over open ground.
            foreach (IntVec3 cell in map.AllCells)
            {
                if (map.roofGrid.RoofAt(cell) != null)
                {
                    map.roofGrid.SetRoof(cell, null);
                }
            }
        }
    }
}
