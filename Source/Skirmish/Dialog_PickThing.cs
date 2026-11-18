using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RimRTS.Skirmish
{
    /// <summary>
    /// Visual item picker: a scrollable grid of icons (vanilla item art) with labels and prices.
    /// One click picks the item and closes the dialog. Shared by the weapon and apparel slots on
    /// <see cref="Page_EquipSquad"/> so both previews look and behave the same.
    /// </summary>
    public sealed class Dialog_PickThing : Window
    {
        private const float CellWidth = 150f;
        private const float CellHeight = 112f;
        private const float CellSpacing = 6f;
        private const float HeaderHeight = 40f;
        private const float IconSize = 64f;

        private readonly List<ThingDef> defs;
        private readonly Action<ThingDef> onPick;
        private readonly Action onNone;
        private readonly bool allowNone;
        private readonly string header;
        private readonly Dictionary<ThingDef, float> previewPrices = new Dictionary<ThingDef, float>();
        private Vector2 scrollPosition;

        public Dialog_PickThing(
            List<ThingDef> defs,
            Action<ThingDef> onPick,
            string header,
            bool allowNone,
            Action onNone = null)
        {
            this.defs = defs ?? new List<ThingDef>();
            this.onPick = onPick;
            this.header = header;
            this.allowNone = allowNone;
            this.onNone = onNone;

            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
            doCloseX = true;
            onlyOneOfTypeAllowed = true;
        }

        public override Vector2 InitialSize => new Vector2(820f, 600f);

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 32f), header);
            Text.Font = GameFont.Small;

            Rect gridRect = new Rect(0f, HeaderHeight, inRect.width, inRect.height - HeaderHeight);
            float viewWidth = gridRect.width - 20f;
            int perRow = Mathf.Max(1, Mathf.FloorToInt(viewWidth / (CellWidth + CellSpacing)));
            int cellCount = defs.Count + (allowNone ? 1 : 0);
            int rows = Mathf.CeilToInt(cellCount / (float)perRow);
            Rect viewRect = new Rect(0f, 0f, viewWidth, Mathf.Max(gridRect.height, rows * (CellHeight + CellSpacing)));

            Widgets.BeginScrollView(gridRect, ref scrollPosition, viewRect);

            int index = 0;
            if (allowNone)
            {
                DrawCell(CellRect(viewRect, index, perRow), null);
                index++;
            }

            foreach (ThingDef def in defs)
            {
                DrawCell(CellRect(viewRect, index, perRow), def);
                index++;
            }

            Widgets.EndScrollView();
        }

        private static Rect CellRect(Rect viewRect, int index, int perRow)
        {
            int column = index % perRow;
            int row = index / perRow;
            return new Rect(
                column * (CellWidth + CellSpacing),
                row * (CellHeight + CellSpacing),
                CellWidth,
                CellHeight);
        }

        /// <summary>
        /// Grid price. Stuffed items are shown at the vanilla default material here; the next
        /// step (the material chooser) shows the exact price for every alternative.
        /// </summary>
        private float PreviewPrice(ThingDef def)
        {
            if (previewPrices.TryGetValue(def, out float price))
            {
                return price;
            }

            ThingDef stuff = SquadLoadout.CanBeMadeFromStuff(def) ? SquadLoadout.DefaultStuff(def) : null;
            price = stuff != null
                ? ThingMaker.MakeThing(def, stuff).MarketValue
                : def.BaseMarketValue;

            previewPrices[def] = price;
            return price;
        }

        private void DrawCell(Rect cell, ThingDef def)        {
            Widgets.DrawBoxSolid(cell, new Color(0.16f, 0.16f, 0.16f, 0.55f));
            Widgets.DrawBox(cell, 1);

            if (Mouse.IsOver(cell))
            {
                Widgets.DrawHighlight(cell);
            }

            Rect iconRect = new Rect(
                cell.x + (cell.width - IconSize) * 0.5f,
                cell.y + 6f,
                IconSize,
                IconSize);

            if (def != null)
            {
                Widgets.ThingIcon(iconRect, def);
                Widgets.LabelEllipses(new Rect(cell.x + 6f, cell.y + 74f, cell.width - 12f, 20f), def.LabelCap);
                Text.Font = GameFont.Tiny;
                Widgets.Label(
                    new Rect(cell.x + 6f, cell.y + 94f, cell.width - 12f, 16f),
                    Mathf.RoundToInt(PreviewPrice(def)) + " silver");
                Text.Font = GameFont.Small;
                TooltipHandler.TipRegion(cell, def.LabelCap + "\n" + Mathf.RoundToInt(PreviewPrice(def)) + " silver");
            }
            else
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(iconRect, "None");
                Text.Anchor = TextAnchor.UpperLeft;
                TooltipHandler.TipRegion(cell, "Empty the slot.");
            }

            if (Widgets.ButtonInvisible(cell))
            {
                if (def != null)
                {
                    onPick?.Invoke(def);
                }
                else
                {
                    onNone?.Invoke();
                }

                Close(true);
            }
        }
    }
}
