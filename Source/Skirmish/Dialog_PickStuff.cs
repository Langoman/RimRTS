using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RimRTS.Skirmish
{
    /// <summary>
    /// Material ("stuff") chooser for items that are made from a material, such as melee
    /// weapons and most apparel. It mirrors <see cref="Dialog_PickThing"/> so both steps of a
    /// gear pick feel the same: an icon grid, one click picks and closes.
    ///
    /// A sample of the finished item is built for every material to show what the piece will
    /// actually cost, because vanilla market value depends on the material
    /// (StatWorker_MarketValue.CalculatedBaseMarketValue is called with the stuff def).
    /// </summary>
    public sealed class Dialog_PickStuff : Window
    {
        private const float CellWidth = 200f;
        private const float CellHeight = 100f;
        private const float CellSpacing = 6f;
        private const float HeaderHeight = 40f;
        private const float IconSize = 56f;

        private readonly List<StuffOption> options = new List<StuffOption>();
        private readonly Action<ThingDef> onPick;
        private readonly string header;
        private Vector2 scrollPosition;

        private struct StuffOption
        {
            public ThingDef Stuff;
            public float ItemValue;
        }

        public Dialog_PickStuff(
            ThingDef thingDef,
            List<ThingDef> stuffs,
            Action<ThingDef> onPick,
            string header)
        {
            this.onPick = onPick;
            this.header = header;

            foreach (ThingDef stuff in stuffs)
            {
                options.Add(new StuffOption
                {
                    Stuff = stuff,
                    ItemValue = ThingMaker.MakeThing(thingDef, stuff).MarketValue,
                });
            }

            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
            doCloseX = true;
            onlyOneOfTypeAllowed = true;
        }

        public override Vector2 InitialSize => new Vector2(760f, 540f);

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 32f), header);
            Text.Font = GameFont.Small;

            Rect gridRect = new Rect(0f, HeaderHeight, inRect.width, inRect.height - HeaderHeight);
            float viewWidth = gridRect.width - 20f;
            int perRow = Mathf.Max(1, Mathf.FloorToInt(viewWidth / (CellWidth + CellSpacing)));
            int rows = Mathf.CeilToInt(options.Count / (float)perRow);
            Rect viewRect = new Rect(
                0f,
                0f,
                viewWidth,
                Mathf.Max(gridRect.height, rows * (CellHeight + CellSpacing)));

            Widgets.BeginScrollView(gridRect, ref scrollPosition, viewRect);

            for (int i = 0; i < options.Count; i++)
            {
                int column = i % perRow;
                int row = i / perRow;
                DrawCell(
                    new Rect(
                        column * (CellWidth + CellSpacing),
                        row * (CellHeight + CellSpacing),
                        CellWidth,
                        CellHeight),
                    options[i]);
            }

            Widgets.EndScrollView();
        }

        private void DrawCell(Rect cell, StuffOption option)
        {
            Widgets.DrawBoxSolid(cell, new Color(0.16f, 0.16f, 0.16f, 0.55f));
            Widgets.DrawBox(cell, 1);

            if (Mouse.IsOver(cell))
            {
                Widgets.DrawHighlight(cell);
            }

            Rect iconRect = new Rect(
                cell.x + 8f,
                cell.y + (cell.height - IconSize) * 0.5f,
                IconSize,
                IconSize);
            Widgets.ThingIcon(iconRect, option.Stuff);

            float textX = iconRect.xMax + 8f;
            float textWidth = cell.xMax - textX - 6f;
            Widgets.LabelEllipses(new Rect(textX, cell.y + 18f, textWidth, 22f), option.Stuff.LabelCap);

            Text.Font = GameFont.Tiny;
            Widgets.Label(
                new Rect(textX, cell.y + 44f, textWidth, 18f),
                Mathf.RoundToInt(option.ItemValue) + " silver");
            Text.Font = GameFont.Small;

            TooltipHandler.TipRegion(
                cell,
                option.Stuff.LabelCap + "\n" + option.Stuff.stuffProps.stuffAdjective + " item\n"
                    + Mathf.RoundToInt(option.ItemValue) + " silver");

            if (Widgets.ButtonInvisible(cell))
            {
                onPick?.Invoke(option.Stuff);
                Close(true);
            }
        }
    }
}
