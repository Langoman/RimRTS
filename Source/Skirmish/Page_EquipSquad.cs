using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimRTS.Skirmish
{
    /// <summary>
    /// Team-selection screen: one row per squad pawn with a weapon picker and its current
    /// silver value, plus the squad budget at the bottom. The enemy team is generated
    /// against that same budget, so the number is the player's army cost, not a private
    /// points system invented by the mod.
    ///
    /// This page only draws; <see cref="SquadLoadout"/> owns the rules and the equipping.
    /// It runs after the vanilla starting-pawns page, so by the time it opens the player has
    /// already chosen the pawns and only the gear decision is left.
    /// </summary>
    public sealed class Page_EquipSquad : Page
    {
        private const float HintHeight = 24f;
        private const float RowHeight = 56f;
        private const float RowSpacing = 8f;
        private const float BudgetHeight = 26f;

        private List<Pawn> squad;
        private List<ThingDef> weapons;
        private Vector2 scrollPosition;

        public override string PageTitle => "Select your squad";

        public override Vector2 InitialSize => new Vector2(680f, 680f);

        public override void PreOpen()
        {
            base.PreOpen();

            squad = SquadLoadout.Squad();
            weapons = SquadLoadout.AvailableWeapons();
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, Page.TitleAreaHeight), PageTitle);
            Text.Font = GameFont.Small;

            Widgets.Label(
                new Rect(0f, Page.TitleAreaHeight, inRect.width, HintHeight),
                "Give every soldier a weapon. The enemy is built with the same silver budget.");

            float listTop = Page.TitleAreaHeight + HintHeight;
            float listBottom = inRect.height - Page.BottomButHeight - BudgetHeight;
            Rect listRect = new Rect(0f, listTop, inRect.width, listBottom - listTop);

            if (squad == null || squad.Count == 0)
            {
                Widgets.Label(listRect, "No squad pawns were created. Go back and choose starting pawns.");
            }
            else
            {
                DrawSquadList(listRect);
            }

            Widgets.Label(
                new Rect(0f, listBottom + 4f, inRect.width, BudgetHeight),
                "Squad budget: " + Mathf.RoundToInt(SquadLoadout.SquadBudget()) + " silver (enemy budget: same)");

            DoBottomButtons(inRect, "Start match");
        }

        private void DrawSquadList(Rect listRect)
        {
            float contentHeight = Mathf.Max(listRect.height, squad.Count * RowHeight);
            Rect viewRect = new Rect(0f, 0f, listRect.width - 20f, contentHeight);

            Widgets.BeginScrollView(listRect, ref scrollPosition, viewRect);

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(viewRect);
            foreach (Pawn pawn in squad)
            {
                DrawSoldierRow(listing, pawn);
            }
            listing.End();

            Widgets.EndScrollView();
        }

        private void DrawSoldierRow(Listing_Standard listing, Pawn pawn)
        {
            string value = Mathf.RoundToInt(pawn.MarketValue) + " silver";
            listing.Label(pawn.LabelShort + "  -  " + value);

            if (listing.ButtonText("Weapon: " + SquadLoadout.WeaponLabel(pawn)))
            {
                OpenWeaponMenu(pawn);
            }

            listing.Gap(RowSpacing);
        }

        private void OpenWeaponMenu(Pawn pawn)
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>
            {
                new FloatMenuOption("Unarmed", delegate { SquadLoadout.Equip(pawn, null); }),
            };

            if (weapons != null)
            {
                foreach (ThingDef weapon in weapons)
                {
                    ThingDef chosen = weapon;
                    string label = chosen.label + "  (" + Mathf.RoundToInt(chosen.BaseMarketValue) + " silver)";
                    options.Add(new FloatMenuOption(label, delegate { SquadLoadout.Equip(pawn, chosen); }));
                }
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }
    }
}
