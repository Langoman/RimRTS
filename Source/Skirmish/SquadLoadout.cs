using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RimRTS.Skirmish
{
    /// <summary>
    /// Loadout rules for the player's skirmish squad: which pawns are on the team, which
    /// weapons they may take, and what the team is worth.
    ///
    /// No UI in here - <see cref="Page_EquipSquad"/> owns the widgets. Keeping the rules
    /// separate means the same budgets and weapon pool can be reused later for the enemy
    /// team, which is generated from the same silver number.
    ///
    /// Budget = vanilla market value of the squad (pawns plus the gear we hand them). It is
    /// recomputed live, so every weapon swap immediately moves the number the enemy is
    /// built against.
    /// </summary>
    public static class SquadLoadout
    {
        /// <summary>
        /// Weapons up to this tech level are selectable. The tech-level step will turn this
        /// into a per-team choice, so it is a single named constant instead of scattered
        /// comparisons.
        /// </summary>
        public const TechLevel MaxTechLevel = TechLevel.Industrial;

        /// <summary>
        /// The pawns the vanilla starting-pawns page kept for the match. The config page
        /// leaves more candidates than <see cref="GameInitData.startingPawnCount"/> in the
        /// list; only the first count pawns actually land, so those are the ones we equip.
        /// </summary>
        public static List<Pawn> Squad()
        {
            GameInitData init = Find.GameInitData;
            if (init == null || init.startingAndOptionalPawns == null)
            {
                return new List<Pawn>();
            }

            int count = init.startingPawnCount;
            if (count > init.startingAndOptionalPawns.Count)
            {
                count = init.startingAndOptionalPawns.Count;
            }

            return init.startingAndOptionalPawns.Take(count).ToList();
        }

        public static List<ThingDef> AvailableWeapons()
        {
            return DefDatabase<ThingDef>.AllDefsListForReading
                .Where(def => def.IsWeapon && def.techLevel <= MaxTechLevel)
                .OrderBy(def => def.label)
                .ToList();
        }

        public static float SquadBudget()
        {
            return Squad().Sum(pawn => pawn.MarketValue);
        }

        public static string WeaponLabel(Pawn pawn)
        {
            ThingDef weapon = pawn != null && pawn.equipment != null && pawn.equipment.Primary != null
                ? pawn.equipment.Primary.def
                : null;
            return weapon != null ? weapon.label : "Unarmed";
        }

        /// <summary>
        /// Puts a single weapon in a pawn's hands (or empties them when weaponDef is null).
        /// Pawns are generated with an equipment tracker already created
        /// (PawnComponentsUtility.CreateInitialComponents is called during generation), so
        /// this works before the map exists. The map-spawn step does not strip equipment,
        /// so the choice survives into the match.
        /// </summary>
        public static void Equip(Pawn pawn, ThingDef weaponDef)
        {
            if (pawn == null || pawn.equipment == null)
            {
                Log.Error("[RimRTS] Cannot equip pawn: no equipment tracker.");
                return;
            }

            if (pawn.equipment.Primary != null)
            {
                pawn.equipment.DestroyEquipment(pawn.equipment.Primary);
            }

            if (weaponDef == null)
            {
                return;
            }

            if (!(ThingMaker.MakeThing(weaponDef) is ThingWithComps weapon))
            {
                Log.Error("[RimRTS] Weapon def '" + weaponDef.defName + "' is not a ThingWithComps.");
                return;
            }

            CompQuality quality = weapon.GetComp<CompQuality>();
            if (quality != null)
            {
                quality.SetQuality(QualityCategory.Normal, null);
            }

            pawn.equipment.AddEquipment(weapon);
        }
    }
}
