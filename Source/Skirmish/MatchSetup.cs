using RimWorld;

namespace RimRTS.Skirmish
{
    /// <summary>
    /// The tech tiers a team can be set to before a match. The first two entries are not tech
    /// levels on purpose: insectoids and mechanoids are enemy-only army types, and the squad
    /// picker must never offer their gear to the player.
    /// </summary>
    public enum TeamTech
    {
        Insectoid,
        Tribal,
        Medieval,
        Industrial,
        Spacer,
        Ultratech,
        Mechanoid,
    }

    /// <summary>
    /// Per-match team setup. The player's tier caps the weapons and apparel the squad screen
    /// offers; the enemy tier is stored now and will drive enemy generation, exactly like the
    /// squad budget does. <see cref="Reset"/> runs when a new skirmish starts so a previous
    /// match's choices never leak into the next one.
    /// </summary>
    public static class MatchSetup
    {
        public static readonly TeamTech[] PlayerChoices =
        {
            TeamTech.Tribal,
            TeamTech.Medieval,
            TeamTech.Industrial,
            TeamTech.Spacer,
            TeamTech.Ultratech,
        };

        public static readonly TeamTech[] EnemyChoices =
        {
            TeamTech.Insectoid,
            TeamTech.Tribal,
            TeamTech.Medieval,
            TeamTech.Industrial,
            TeamTech.Spacer,
            TeamTech.Ultratech,
            TeamTech.Mechanoid,
        };

        public static TeamTech PlayerTech { get; private set; } = TeamTech.Industrial;

        public static TeamTech EnemyTech { get; private set; } = TeamTech.Industrial;

        public static void Reset()
        {
            PlayerTech = TeamTech.Industrial;
            EnemyTech = TeamTech.Industrial;
        }

        public static void SetPlayerTech(TeamTech tech)
        {
            PlayerTech = tech;
        }

        public static void SetEnemyTech(TeamTech tech)
        {
            EnemyTech = tech;
        }

        public static string Label(TeamTech tech)
        {
            switch (tech)
            {
                case TeamTech.Insectoid:
                    return "Insectoid";
                case TeamTech.Tribal:
                    return "Tribal";
                case TeamTech.Medieval:
                    return "Medieval";
                case TeamTech.Industrial:
                    return "Industrial";
                case TeamTech.Spacer:
                    return "Spacer";
                case TeamTech.Ultratech:
                    return "Ultratech";
                case TeamTech.Mechanoid:
                    return "Mechanoid";
                default:
                    return tech.ToString();
            }
        }

        /// <summary>
        /// Highest gear tech level a team may use. The two enemy-only army types return
        /// Industrial so a bad call site can never open spacer gear for them; the selection UI
        /// simply never offers them for the player.
        /// </summary>
        public static TechLevel WeaponTechLevel(TeamTech tech)
        {
            switch (tech)
            {
                case TeamTech.Tribal:
                    return TechLevel.Neolithic;
                case TeamTech.Medieval:
                    return TechLevel.Medieval;
                case TeamTech.Spacer:
                    return TechLevel.Spacer;
                case TeamTech.Ultratech:
                    return TechLevel.Ultra;
                default:
                    return TechLevel.Industrial;
            }
        }

        /// <summary>Faction whose icon represents this tier in the team buttons.</summary>
        public static FactionDef IconFaction(TeamTech tech)
        {
            switch (tech)
            {
                case TeamTech.Insectoid:
                    return FactionDefOf.Insect;
                case TeamTech.Tribal:
                    return FactionDefOf.TribeRough;
                case TeamTech.Medieval:
                    return FactionDefOf.TribeCivil;
                case TeamTech.Spacer:
                case TeamTech.Ultratech:
                    return FactionDefOf.Empire;
                case TeamTech.Mechanoid:
                    return FactionDefOf.Mechanoid;
                default:
                    return FactionDefOf.Pirate;
            }
        }
    }
}
