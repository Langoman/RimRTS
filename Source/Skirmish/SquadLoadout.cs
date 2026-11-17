using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RimRTS.Skirmish
{
    /// <summary>
    /// Loadout rules for the player's skirmish squad: which pawns are candidates, which
    /// weapons and apparel may be taken, how to equip them, and what the team is worth.
    ///
    /// No UI in here - <see cref="Page_EquipSquad"/> owns the widgets. Keeping the rules
    /// separate means the same pools and budget can be reused later for the enemy team,
    /// which is generated from the same silver number.
    ///
    /// Budget = vanilla market value of the chosen pawns plus the gear we hand them. Vanilla
    /// pawn market value does not include carried gear (verified in StatWorker_MarketValue:
    /// it only calls PriceUtility pawn quality factors), so gear is added explicitly here and
    /// every weapon/apparel swap immediately moves the number the enemy is built against.
    /// </summary>
    public static class SquadLoadout
    {
        public const string PackageId = "Lango.RimRTS";
        public const string WeaponBlacklistFileName = "WeaponBlacklist.txt";

        /// <summary>
        /// Weapons and apparel up to this tech level are selectable. The tech-level step will
        /// turn this into a per-team choice, so it stays a single named constant.
        /// </summary>
        public const TechLevel MaxTechLevel = TechLevel.Industrial;

        /// <summary>
        /// The candidate pawns the scenario generated. The player selects the actual squad
        /// from this pool on the equipment page; the unchosen ones are dropped in
        /// <see cref="ApplySquad"/>, so no stray colonists are left behind in the match.
        /// </summary>
        public static List<Pawn> PawnPool()
        {
            GameInitData init = Find.GameInitData;
            if (init == null || init.startingAndOptionalPawns == null)
            {
                return new List<Pawn>();
            }
            return init.startingAndOptionalPawns.ToList();
        }

        /// <summary>
        /// Stores the player's final squad on GameInitData so vanilla's PrepForMapGen spawns
        /// exactly these pawns (it skips any extra candidates beyond startingPawnCount).
        /// </summary>
        public static void ApplySquad(List<Pawn> squad)
        {
            GameInitData init = Find.GameInitData;
            init.startingAndOptionalPawns = new List<Pawn>(squad);
            init.startingPawnCount = squad.Count;
        }

        public static List<ThingDef> AvailableWeapons()
        {
            HashSet<string> blacklist = WeaponBlacklist.DefNames();
            return DefDatabase<ThingDef>.AllDefsListForReading
                .Where(def => def.IsWeapon
                    && def.techLevel <= MaxTechLevel
                    && !blacklist.Contains(def.defName))
                .OrderBy(def => def.label)
                .ToList();
        }

        public static List<ThingDef> AvailableApparel()
        {
            return DefDatabase<ThingDef>.AllDefsListForReading
                .Where(def => def.IsApparel && def.techLevel <= MaxTechLevel)
                .OrderBy(def => def.label)
                .ToList();
        }

        public static float Budget(IEnumerable<Pawn> squad)
        {
            return squad.Sum(PawnValue);
        }

        /// <summary>
        /// A soldier's worth: the pawn plus the gear it currently carries. Used both for the
        /// displayed budget and, later, as the number the enemy team is generated from.
        /// </summary>
        public static float PawnValue(Pawn pawn)
        {
            float value = pawn.MarketValue;

            if (pawn.equipment != null && pawn.equipment.Primary != null)
            {
                value += pawn.equipment.Primary.MarketValue;
            }

            if (pawn.apparel != null)
            {
                foreach (Apparel apparel in pawn.apparel.WornApparel)
                {
                    value += apparel.MarketValue;
                }
            }

            return value;
        }

        public static string WeaponLabel(Pawn pawn)
        {
            ThingDef weapon = pawn != null && pawn.equipment != null && pawn.equipment.Primary != null
                ? pawn.equipment.Primary.def
                : null;
            return weapon != null ? weapon.label : "none";
        }

        public static string ApparelLabel(Pawn pawn)
        {
            int count = pawn != null && pawn.apparel != null ? pawn.apparel.WornApparel.Count : 0;
            return count == 0 ? "none" : count + " worn";
        }

        /// <summary>
        /// Puts a single weapon in a pawn's hands (or empties them when weaponDef is null).
        /// Pawns are generated with an equipment tracker already created
        /// (PawnComponentsUtility.CreateInitialComponents is called during generation), so
        /// this works before the map exists. The map-spawn step does not strip equipment,
        /// so the choice survives into the match.
        /// </summary>
        public static void EquipWeapon(Pawn pawn, ThingDef weaponDef)
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

        /// <summary>
        /// Puts one piece of apparel on a pawn. Vanilla Wear() drops whatever shares the same
        /// layer/body part, so picking a helmet after a hat simply replaces it.
        /// </summary>
        public static void WearApparel(Pawn pawn, ThingDef apparelDef)
        {
            if (pawn == null || pawn.apparel == null)
            {
                Log.Error("[RimRTS] Cannot wear apparel: no apparel tracker.");
                return;
            }

            if (!(ThingMaker.MakeThing(apparelDef) is Apparel apparel))
            {
                Log.Error("[RimRTS] Apparel def '" + apparelDef.defName + "' is not an Apparel.");
                return;
            }

            CompQuality quality = apparel.GetComp<CompQuality>();
            if (quality != null)
            {
                quality.SetQuality(QualityCategory.Normal, null);
            }

            pawn.apparel.Wear(apparel, true, false);
        }

        public static void RemoveApparel(Pawn pawn)
        {
            if (pawn == null || pawn.apparel == null)
            {
                return;
            }

            foreach (Apparel apparel in pawn.apparel.WornApparel.ToList())
            {
                pawn.apparel.Remove(apparel);
            }
        }
    }
}
