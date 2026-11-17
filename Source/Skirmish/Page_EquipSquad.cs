using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimRTS.Skirmish
{
    /// <summary>
    /// Team-selection screen: choose the soldiers from the generated candidate pool, arm each
    /// with a weapon and apparel, and watch the squad's silver value. The enemy team is built
    /// against that same value, so the number is the player's army cost, not a private points
    /// system invented by the mod.
    ///
    /// This page only draws and holds UI state; <see cref="SquadLoadout"/> owns the rules and
    /// the equipping. It replaces the vanilla starting-pawns page, so the player picks both
    /// how many soldiers and which ones.
    /// </summary>
    public sealed class Page_EquipSquad : Page
    {
        private const float HintHeight = 24f;
        private const float RowHeight = 36f;
        private const float RowSpacing = 4f;
        private const float BudgetHeight = 26f;
        private const int DefaultSquadSize = 3;

        private List<Pawn> pool;
        private List<ThingDef> weapons;
        private List<ThingDef> apparelDefs;
        private HashSet<Pawn> selected;
        private Vector2 scrollPosition;

        public override string PageTitle => "Select your squad";

        public override Vector2 InitialSize => new Vector2(720f, 700f);

        public override void PreOpen()
        {
            base.PreOpen();

            pool = SquadLoadout.PawnPool();
            weapons = SquadLoadout.AvailableWeapons();
            apparelDefs = SquadLoadout.AvailableApparel();
            selected = new HashSet<Pawn>(pool.Take(DefaultSquadSize));
        }

        protected override bool CanDoNext()
        {
            if (!base.CanDoNext())
            {
                return false;
            }

            if (selected.Count == 0)
            {
                Messages.Message("Select at least one soldier for the squad.", MessageTypeDefOf.RejectInput, false);
                return false;
            }

            return true;
        }

        protected override void DoNext()
        {
            SquadLoadout.ApplySquad(SelectedPawns());
            base.DoNext();
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, Page.TitleAreaHeight), PageTitle);
            Text.Font = GameFont.Small;

            Widgets.Label(
                new Rect(0f, Page.TitleAreaHeight, inRect.width, HintHeight),
                "Tick the soldiers you want. Each one, and its gear, adds to the silver the enemy is built with.");

            float listTop = Page.TitleAreaHeight + HintHeight;
            float listBottom = inRect.height - Page.BottomButHeight - BudgetHeight;
            Rect listRect = new Rect(0f, listTop, inRect.width, listBottom - listTop);

            DrawPool(listRect);

            Widgets.Label(
                new Rect(0f, listBottom + 4f, inRect.width, BudgetHeight),
                "Squad: " + selected.Count + " soldiers  |  used budget: "
                    + Mathf.RoundToInt(SquadLoadout.Budget(SelectedPawns())) + " silver (enemy gets the same)");

            DoBottomButtons(inRect, "Start match");
        }

        private void DrawPool(Rect listRect)
        {
            if (pool.Count == 0)
            {
                Widgets.Label(listRect, "No candidate pawns were generated. Go back and try again.");
                return;
            }

            float contentHeight = Mathf.Max(listRect.height, pool.Count * (RowHeight + RowSpacing));
            Rect viewRect = new Rect(0f, 0f, listRect.width - 20f, contentHeight);

            Widgets.BeginScrollView(listRect, ref scrollPosition, viewRect);

            float y = 0f;
            foreach (Pawn pawn in pool)
            {
                DrawPawnRow(new Rect(0f, y, viewRect.width, RowHeight), pawn);
                y += RowHeight + RowSpacing;
            }

            Widgets.EndScrollView();
        }

        private void DrawPawnRow(Rect row, Pawn pawn)
        {
            bool isSelected = selected.Contains(pawn);
            Widgets.Checkbox(new Vector2(row.x, row.y + 7f), ref isSelected);
            if (isSelected != selected.Contains(pawn))
            {
                if (isSelected)
                {
                    selected.Add(pawn);
                }
                else
                {
                    selected.Remove(pawn);
                }
            }

            Widgets.Label(
                new Rect(row.x + 28f, row.y + 5f, 210f, 24f),
                pawn.LabelShort + "  (" + Mathf.RoundToInt(SquadLoadout.PawnValue(pawn)) + ")");

            if (!isSelected)
            {
                return;
            }

            if (Widgets.ButtonText(new Rect(row.x + 250f, row.y, 150f, 28f), "Weapon: " + SquadLoadout.WeaponLabel(pawn)))
            {
                OpenWeaponMenu(pawn);
            }

            if (Widgets.ButtonText(new Rect(row.x + 410f, row.y, 150f, 28f), "Gear: " + SquadLoadout.ApparelLabel(pawn)))
            {
                OpenApparelMenu(pawn);
            }
        }

        private List<Pawn> SelectedPawns()
        {
            return pool.Where(pawn => selected.Contains(pawn)).ToList();
        }

        private void OpenWeaponMenu(Pawn pawn)
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>
            {
                new FloatMenuOption("No weapon", delegate { SquadLoadout.EquipWeapon(pawn, null); }),
            };

            foreach (ThingDef weapon in weapons)
            {
                ThingDef chosen = weapon;
                string label = chosen.label + "  (" + Mathf.RoundToInt(chosen.BaseMarketValue) + " silver)";
                options.Add(new FloatMenuOption(label, delegate { SquadLoadout.EquipWeapon(pawn, chosen); }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void OpenApparelMenu(Pawn pawn)
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>
            {
                new FloatMenuOption("Take all gear off", delegate { SquadLoadout.RemoveApparel(pawn); }),
            };

            foreach (ThingDef apparel in apparelDefs)
            {
                ThingDef chosen = apparel;
                string label = chosen.label + "  (" + Mathf.RoundToInt(chosen.BaseMarketValue) + " silver)";
                options.Add(new FloatMenuOption(label, delegate { SquadLoadout.WearApparel(pawn, chosen); }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }
    }
}
