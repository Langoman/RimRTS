using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimRTS.Skirmish
{
    /// <summary>
    /// Team-selection screen: choose the soldiers from the generated candidate pool, pick their
    /// Shooting/Melee levels, re-roll their traits, arm them and look at the result: a portrait of
    /// the selected soldier, weapon and apparel icons, and the squad's silver value. The enemy
    /// team is built against that same value, so the number is the player's army cost.
    ///
    /// This page only draws and holds UI state; <see cref="SquadLoadout"/> owns the rules and
    /// <see cref="Dialog_PickThing"/> supplies the visual item grid. It replaces the vanilla
    /// starting-pawns page, so the player picks both how many soldiers and which ones.
    /// </summary>
    public sealed class Page_EquipSquad : Page
    {
        private const float HintHeight = 24f;
        private const float RowHeight = 68f;
        private const float RowSpacing = 4f;
        private const float BudgetHeight = 26f;
        private const int DefaultSquadSize = 3;
        private const float RandomizeAllButtonWidth = 220f;
        private const float LeftColumnWidth = 336f;
        private const float PortraitSize = 140f;
        private const float SlotSize = 64f;
        private const int MaxPreviewApparel = 5;

        private List<Pawn> pool;
        private List<ThingDef> weapons;
        private List<ThingDef> apparelDefs;
        private HashSet<Pawn> selected;
        private Dictionary<Pawn, SoldierSetup> setups;
        private Pawn focused;
        private Vector2 scrollPosition;

        /// <summary>Per-soldier choices that are not stored on the pawn itself.</summary>
        private sealed class SoldierSetup
        {
            public int Shooting = SquadLoadout.DefaultSkillLevel;
            public int Melee = SquadLoadout.DefaultSkillLevel;
        }

        public override string PageTitle => "Select your squad";

        public override Vector2 InitialSize => new Vector2(1060f, 740f);

        public override void PreOpen()
        {
            base.PreOpen();

            pool = SquadLoadout.PawnPool();
            weapons = SquadLoadout.AvailableWeapons();
            apparelDefs = SquadLoadout.AvailableApparel();
            selected = new HashSet<Pawn>(pool.Take(DefaultSquadSize));
            setups = new Dictionary<Pawn, SoldierSetup>();
            focused = SelectedPawns().FirstOrDefault() ?? pool.FirstOrDefault();
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
            foreach (Pawn pawn in SelectedPawns())
            {
                SoldierSetup setup = GetSetup(pawn);
                SquadLoadout.ApplySkillLevels(pawn, setup.Shooting, setup.Melee);
            }

            SquadLoadout.ApplySquad(SelectedPawns());
            base.DoNext();
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, Page.TitleAreaHeight), PageTitle);
            Text.Font = GameFont.Small;

            Widgets.Label(
                new Rect(0f, Page.TitleAreaHeight, inRect.width - RandomizeAllButtonWidth - 8f, HintHeight),
                "Tick soldiers on the left, click one to inspect it, and set Shooting/Melee here (other skills are 5).");

            if (Widgets.ButtonText(
                    new Rect(inRect.width - RandomizeAllButtonWidth, Page.TitleAreaHeight, RandomizeAllButtonWidth, HintHeight),
                    "Randomize traits (all)"))
            {
                foreach (Pawn pawn in SelectedPawns())
                {
                    SquadLoadout.RandomizeTraits(pawn);
                }
            }

            float top = Page.TitleAreaHeight + HintHeight;
            float bottom = inRect.height - Page.BottomButHeight - BudgetHeight;

            DrawPool(new Rect(0f, top, LeftColumnWidth, bottom - top));
            DrawSoldier(new Rect(LeftColumnWidth + 10f, top, inRect.width - LeftColumnWidth - 10f, bottom - top));

            Widgets.Label(
                new Rect(0f, bottom + 4f, inRect.width, BudgetHeight),
                "Squad: " + selected.Count + " soldiers  |  used budget: "
                    + Mathf.RoundToInt(SquadLoadout.Budget(SelectedPawns())) + " silver (enemy gets the same)");

            DoBottomButtons(inRect, "Start match");
        }

        private void DrawPool(Rect rect)
        {
            if (pool.Count == 0)
            {
                Widgets.Label(rect, "No candidate pawns were generated. Go back and try again.");
                return;
            }

            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 20f), "Candidates (" + pool.Count + ")");

            Rect listRect = new Rect(rect.x, rect.y + 22f, rect.width, rect.height - 22f);
            Rect viewRect = new Rect(0f, 0f, listRect.width - 20f, Mathf.Max(listRect.height, pool.Count * (RowHeight + RowSpacing)));

            Widgets.BeginScrollView(listRect, ref scrollPosition, viewRect);

            float y = 0f;
            foreach (Pawn pawn in pool)
            {
                DrawPoolRow(new Rect(0f, y, viewRect.width, RowHeight), pawn);
                y += RowHeight + RowSpacing;
            }

            Widgets.EndScrollView();
        }

        private void DrawPoolRow(Rect row, Pawn pawn)
        {
            if (focused == pawn)
            {
                Widgets.DrawHighlightSelected(row);
            }
            else if (Mouse.IsOver(row))
            {
                Widgets.DrawHighlight(row);
            }

            bool isSelected = selected.Contains(pawn);
            Widgets.Checkbox(new Vector2(row.x + 4f, row.y + 22f), ref isSelected);
            if (isSelected != selected.Contains(pawn))
            {
                if (isSelected)
                {
                    selected.Add(pawn);
                    SoldierSetup setup = GetSetup(pawn);
                    SquadLoadout.ApplySkillLevels(pawn, setup.Shooting, setup.Melee);
                    focused = pawn;
                }
                else
                {
                    selected.Remove(pawn);
                    if (focused == pawn)
                    {
                        focused = SelectedPawns().FirstOrDefault() ?? pool.FirstOrDefault();
                    }
                }
            }

            Widgets.Label(new Rect(row.x + 30f, row.y + 6f, 190f, 24f), pawn.LabelShort);

            Text.Font = GameFont.Tiny;
            Widgets.Label(
                new Rect(row.x + 30f, row.y + 32f, 190f, 18f),
                Mathf.RoundToInt(SquadLoadout.PawnValue(pawn)) + " silver");
            Text.Font = GameFont.Small;

            DrawGearPreview(pawn, row.x + 232f, row.y + 6f);

            if (Widgets.ButtonInvisible(row))
            {
                focused = pawn;
            }
        }

        private void DrawGearPreview(Pawn pawn, float x, float y)
        {
            if (pawn.equipment != null && pawn.equipment.Primary != null)
            {
                Widgets.ThingIcon(new Rect(x, y, 28f, 28f), pawn.equipment.Primary.def);
            }

            int shown = 0;
            if (pawn.apparel != null)
            {
                foreach (Apparel apparel in pawn.apparel.WornApparel)
                {
                    if (shown >= MaxPreviewApparel)
                    {
                        break;
                    }

                    Widgets.ThingIcon(new Rect(x + shown * 22f, y + 34f, 20f, 20f), apparel.def);
                    shown++;
                }
            }

            if (shown == 0 && (pawn.equipment == null || pawn.equipment.Primary == null))
            {
                Text.Font = GameFont.Tiny;
                Widgets.Label(new Rect(x, y + 6f, 70f, 18f), "no gear");
                Text.Font = GameFont.Small;
            }
        }

        private void DrawSoldier(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.12f, 0.12f, 0.12f, 0.35f));
            Widgets.DrawBox(rect, 1);

            if (focused == null)
            {
                Widgets.Label(new Rect(rect.x + 12f, rect.y + 12f, rect.width - 24f, 24f), "Tick a soldier to inspect it.");
                return;
            }

            SoldierSetup setup = GetSetup(focused);
            float y = rect.y + 10f;

            Rect portraitRect = new Rect(rect.x + 10f, y, PortraitSize, PortraitSize);
            GUI.DrawTexture(
                portraitRect,
                PortraitsCache.Get(focused, new Vector2(PortraitSize, PortraitSize), Rot4.South),
                ScaleMode.ScaleToFit);
            Widgets.DrawBox(portraitRect, 1);

            float infoX = portraitRect.xMax + 12f;
            float infoWidth = rect.xMax - infoX - 10f;

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(infoX, y, infoWidth, 28f), focused.LabelShort);
            Text.Font = GameFont.Small;
            Widgets.Label(
                new Rect(infoX, y + 28f, infoWidth, 20f),
                "Worth " + Mathf.RoundToInt(SquadLoadout.PawnValue(focused)) + " silver");

            float skillY = y + 54f;
            DrawSkillRow(new Rect(infoX, skillY, 240f, 26f), focused, true, "Shooting", setup.Shooting);
            DrawSkillRow(new Rect(infoX, skillY + 30f, 240f, 26f), focused, false, "Melee", setup.Melee);

            Text.Font = GameFont.Tiny;
            Widgets.Label(
                new Rect(infoX, skillY + 62f, infoWidth, 18f),
                "All other skills: " + SquadLoadout.DefaultSkillLevel);
            Text.Font = GameFont.Small;

            Widgets.Label(new Rect(infoX, skillY + 82f, 80f, 20f), "Traits:");
            Widgets.Label(new Rect(infoX + 46f, skillY + 82f, infoWidth - 46f, 40f), TraitSummary(focused));

            if (Widgets.ButtonText(new Rect(infoX, skillY + 126f, 150f, 26f), "Randomize traits"))
            {
                SquadLoadout.RandomizeTraits(focused);
            }

            if (Widgets.ButtonText(new Rect(infoX + 158f, skillY + 126f, 120f, 26f), "Clear traits"))
            {
                SquadLoadout.ClearTraits(focused);
            }

            float gearY = y + PortraitSize + 18f;
            DrawWeaponSlot(rect, gearY);
            DrawApparelSlots(rect, gearY + 104f);
        }

        private void DrawSkillRow(Rect row, Pawn pawn, bool shooting, string label, int level)
        {
            if (Widgets.ButtonText(new Rect(row.x, row.y, 26f, 26f), "-"))
            {
                AdjustSkill(pawn, shooting, -1);
            }

            Widgets.Label(new Rect(row.x + 32f, row.y + 4f, 150f, 22f), label + ": " + level);

            if (Widgets.ButtonText(new Rect(row.x + 186f, row.y, 26f, 26f), "+"))
            {
                AdjustSkill(pawn, shooting, 1);
            }
        }

        private void DrawWeaponSlot(Rect rect, float y)
        {
            Widgets.Label(new Rect(rect.x + 10f, y, 200f, 20f), "Weapon");

            Thing weapon = focused.equipment != null ? focused.equipment.Primary : null;
            Rect iconRect = new Rect(rect.x + 10f, y + 22f, SlotSize, SlotSize);

            if (weapon != null)
            {
                Widgets.ThingIcon(iconRect, weapon.def);
                TooltipHandler.TipRegion(iconRect, weapon.LabelCap + "\n" + Mathf.RoundToInt(weapon.MarketValue) + " silver");
            }

            Widgets.DrawBox(iconRect, 1);

            Widgets.Label(
                new Rect(iconRect.xMax + 10f, y + 26f, 300f, 24f),
                weapon != null ? (string)weapon.def.LabelCap : "No weapon");

            if (Widgets.ButtonText(new Rect(iconRect.xMax + 10f, y + 54f, 150f, 26f), "Choose weapon"))
            {
                OpenWeaponPicker(focused);
            }
        }

        private void DrawApparelSlots(Rect rect, float y)
        {
            Widgets.Label(new Rect(rect.x + 10f, y, 240f, 20f), "Clothing and armor");

            float x = rect.x + 10f;
            int shown = 0;

            if (focused.apparel != null)
            {
                foreach (Apparel apparel in focused.apparel.WornApparel)
                {
                    if (shown >= 6)
                    {
                        break;
                    }

                    Rect iconRect = new Rect(x, y + 22f, 56f, 56f);
                    Widgets.ThingIcon(iconRect, apparel.def);
                    Widgets.DrawBox(iconRect, 1);
                    TooltipHandler.TipRegion(iconRect, apparel.LabelCap + "\n" + Mathf.RoundToInt(apparel.MarketValue) + " silver");
                    x += 60f;
                    shown++;
                }
            }

            if (shown == 0)
            {
                Widgets.Label(new Rect(rect.x + 10f, y + 30f, 300f, 24f), "Nothing worn.");
            }

            if (Widgets.ButtonText(new Rect(rect.x + 10f, y + 84f, 160f, 26f), "Add / replace gear"))
            {
                OpenApparelPicker(focused);
            }

            if (shown > 0 && Widgets.ButtonText(new Rect(rect.x + 178f, y + 84f, 120f, 26f), "Take all off"))
            {
                SquadLoadout.RemoveApparel(focused);
            }
        }

        private SoldierSetup GetSetup(Pawn pawn)
        {
            if (!setups.TryGetValue(pawn, out SoldierSetup setup))
            {
                setup = new SoldierSetup();
                setups[pawn] = setup;
            }

            return setup;
        }

        private void AdjustSkill(Pawn pawn, bool shooting, int delta)
        {
            SoldierSetup setup = GetSetup(pawn);

            if (shooting)
            {
                setup.Shooting = Mathf.Clamp(setup.Shooting + delta, SquadLoadout.MinSkillLevel, SquadLoadout.MaxSkillLevel);
            }
            else
            {
                setup.Melee = Mathf.Clamp(setup.Melee + delta, SquadLoadout.MinSkillLevel, SquadLoadout.MaxSkillLevel);
            }

            SquadLoadout.ApplySkillLevels(pawn, setup.Shooting, setup.Melee);
        }

        private static string TraitSummary(Pawn pawn)
        {
            List<Trait> traits = pawn.story.traits.TraitsSorted;
            return traits.Count == 0 ? "none" : string.Join(", ", traits.Select(trait => trait.Label));
        }

        private List<Pawn> SelectedPawns()
        {
            return pool.Where(pawn => selected.Contains(pawn)).ToList();
        }

        private void OpenWeaponPicker(Pawn pawn)
        {
            Find.WindowStack.Add(new Dialog_PickThing(
                weapons,
                def => SquadLoadout.EquipWeapon(pawn, def),
                "Choose a weapon",
                true,
                () => SquadLoadout.EquipWeapon(pawn, null)));
        }

        private void OpenApparelPicker(Pawn pawn)
        {
            Find.WindowStack.Add(new Dialog_PickThing(
                apparelDefs,
                def => SquadLoadout.WearApparel(pawn, def),
                "Choose clothing or armor",
                false));
        }
    }
}
