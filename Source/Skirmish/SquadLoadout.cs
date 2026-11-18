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
        /// Highest gear tier the player's team may use. Chosen per match on the squad screen
        /// (<see cref="MatchSetup.PlayerTech"/>); the enemy team has its own tier stored in
        /// <see cref="MatchSetup.EnemyTech"/>.
        /// </summary>
        public static TechLevel MaxTechLevel => MatchSetup.WeaponTechLevel(MatchSetup.PlayerTech);

        /// <summary>
        /// Squad pawns get this level in every skill except the two the player picks on the
        /// squad page. RimWorld skills range 0..20; the vanilla UI caps player-made values at
        /// 20, so the same cap applies here.
        /// </summary>
        public const int DefaultSkillLevel = 5;

        public const int MinSkillLevel = 0;

        public const int MaxSkillLevel = 20;

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

        /// <summary>
        /// Applies the squad skill template: Shooting and Melee get the player's chosen levels,
        /// every other skill is reset to <see cref="DefaultSkillLevel"/>. SkillRecord.Level has
        /// a public setter (verified in Assembly-CSharp), and resetting xpSinceLastLevel keeps
        /// the pawn from jumping to the next level as soon as it shoots once.
        /// </summary>
        public static void ApplySkillLevels(Pawn pawn, int shooting, int melee)
        {
            if (pawn == null || pawn.skills == null)
            {
                Log.Error("[RimRTS] Cannot set skills: pawn has no skill tracker.");
                return;
            }

            int shootingLevel = ClampLevel(shooting);
            int meleeLevel = ClampLevel(melee);

            foreach (SkillRecord skill in pawn.skills.skills)
            {
                if (skill.def == SkillDefOf.Shooting)
                {
                    skill.Level = shootingLevel;
                }
                else if (skill.def == SkillDefOf.Melee)
                {
                    skill.Level = meleeLevel;
                }
                else
                {
                    skill.Level = DefaultSkillLevel;
                }

                skill.xpSinceLastLevel = 0f;
            }
        }

        /// <summary>
        /// Re-rolls a pawn's traits using the game's own generator: it generates a throwaway pawn
        /// of the same kind and copies its traits over. PawnGenerator.GenerateTraits is internal
        /// in 1.6, so going through GeneratePawn keeps this patch-free and lets vanilla decide
        /// trait count, commonality weights, degrees and conflicts.
        /// </summary>
        public static void RandomizeTraits(Pawn pawn)
        {
            if (pawn == null || pawn.story == null || pawn.story.traits == null)
            {
                Log.Error("[RimRTS] Cannot randomize traits: pawn has no trait tracker.");
                return;
            }

            Pawn roll = PawnGenerator.GeneratePawn(pawn.kindDef, pawn.Faction, null);
            List<Trait> rolled = roll.story.traits.TraitsSorted.ToList();

            ClearTraits(pawn);

            foreach (Trait trait in rolled)
            {
                pawn.story.traits.GainTrait(new Trait(trait.def, trait.Degree, false), true);
            }
        }

        /// <summary>Removes every trait from the pawn.</summary>
        public static void ClearTraits(Pawn pawn)
        {
            if (pawn == null || pawn.story == null || pawn.story.traits == null)
            {
                return;
            }

            foreach (Trait trait in pawn.story.traits.TraitsSorted.ToList())
            {
                pawn.story.traits.RemoveTrait(trait, false);
            }
        }

        private static int ClampLevel(int level)
        {
            return System.Math.Max(MinSkillLevel, System.Math.Min(MaxSkillLevel, level));
        }

        public static List<ThingDef> AvailableWeapons()        {
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

        /// <summary>
        /// True for items built from a material (steel swords, plasteel armor, cloth shirts...).
        /// Only these get the material chooser; guns have a fixed composition.
        /// </summary>
        public static bool CanBeMadeFromStuff(ThingDef def)
        {
            return def != null
                && def.MadeFromStuff
                && def.stuffCategories != null
                && def.stuffCategories.Count > 0;
        }

        /// <summary>
        /// Materials the item may be made from, capped by the player's tech tier. Vanilla's
        /// GenStuff does the category matching and the generation-eligibility check.
        /// </summary>
        public static List<ThingDef> AvailableStuffs(ThingDef def)
        {
            if (!CanBeMadeFromStuff(def))
            {
                return new List<ThingDef>();
            }

            return GenStuff.AllowedStuffsFor(def, MaxTechLevel)
                .OrderBy(stuff => stuff.label)
                .ToList();
        }

        /// <summary>Vanilla's default material for the item, used when no explicit pick was made.</summary>
        public static ThingDef DefaultStuff(ThingDef def)
        {
            return CanBeMadeFromStuff(def) ? GenStuff.DefaultStuffFor(def) : null;
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
        ///
        /// Items with <see cref="CanBeMadeFromStuff"/> are built from <paramref name="stuff"/>,
        /// or from the vanilla default material when the caller did not pick one, because
        /// ThingMaker with a null stuff on a MadeFromStuff def produces an invalid item.
        /// </summary>
        public static void EquipWeapon(Pawn pawn, ThingDef weaponDef, ThingDef stuff = null)
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

            ThingDef resolvedStuff = CanBeMadeFromStuff(weaponDef)
                ? stuff ?? DefaultStuff(weaponDef)
                : null;

            if (!(ThingMaker.MakeThing(weaponDef, resolvedStuff) is ThingWithComps weapon))
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
        /// layer/body part, so picking a helmet after a hat simply replaces it. Stuffed apparel
        /// (cloth, leathers, metals) is built from <paramref name="stuff"/>, or the vanilla
        /// default material when none was picked.
        /// </summary>
        public static void WearApparel(Pawn pawn, ThingDef apparelDef, ThingDef stuff = null)
        {
            if (pawn == null || pawn.apparel == null)
            {
                Log.Error("[RimRTS] Cannot wear apparel: no apparel tracker.");
                return;
            }

            if (apparelDef == null)
            {
                return;
            }

            ThingDef resolvedStuff = CanBeMadeFromStuff(apparelDef)
                ? stuff ?? DefaultStuff(apparelDef)
                : null;

            if (!(ThingMaker.MakeThing(apparelDef, resolvedStuff) is Apparel apparel))
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

        /// <summary>
        /// Drops any carried weapon or worn apparel above the team's tech tier. Runs when the
        /// player lowers the squad's tech level after picking gear, so the match can never start
        /// with illegal equipment. A material above the tier also disqualifies the piece: a
        /// plasteel longsword is out for a medieval team even though the def itself is medieval.
        /// </summary>
        public static void RemoveAboveTechLevel(Pawn pawn, TechLevel maxTechLevel)
        {
            if (pawn == null)
            {
                return;
            }

            if (pawn.equipment != null && pawn.equipment.Primary != null)
            {
                ThingWithComps weapon = pawn.equipment.Primary;
                if (weapon.def.techLevel > maxTechLevel
                    || (weapon.Stuff != null && weapon.Stuff.techLevel > maxTechLevel))
                {
                    pawn.equipment.DestroyEquipment(weapon);
                }
            }

            if (pawn.apparel == null)
            {
                return;
            }

            foreach (Apparel apparel in pawn.apparel.WornApparel.ToList())
            {
                if (apparel.def.techLevel > maxTechLevel
                    || (apparel.Stuff != null && apparel.Stuff.techLevel > maxTechLevel))
                {
                    pawn.apparel.Remove(apparel);
                }
            }
        }
    }
}
