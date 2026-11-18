using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RimRTS.Skirmish
{
    /// <summary>
    /// Match-setup screen that replaces the vanilla starting-site (planet) page: the player
    /// picks a biome instead of clicking a tile on the planet.
    ///
    /// The screen is a plain vanilla <see cref="Page"/> using vanilla widgets. It only owns
    /// UI and the two decisions of this step (which biome, set the landing tile); world
    /// queries live in <see cref="BiomeOption"/> and new-game flow lives in
    /// <see cref="ScenarioEntry"/>. That keeps the later move to procedural battle maps a
    /// change behind this screen rather than inside it.
    /// </summary>
    public sealed class Page_ChooseBiome : Page
    {
        private const float HintHeight = 24f;
        private const float ListPadding = 6f;

        private List<BiomeOption> options;
        private BiomeOption selected;
        private Vector2 scrollPosition;

        public override string PageTitle => "Choose a biome";

        public override Vector2 InitialSize => new Vector2(640f, 620f);

        public override void PreOpen()
        {
            base.PreOpen();

            options = BiomeOption.CollectFrom(Find.World.grid);
            selected = options.Count > 0 ? options[0] : null;
        }

        protected override bool CanDoNext()
        {
            return selected != null;
        }

        protected override void DoNext()
        {
            if (selected == null)
            {
                return;
            }

            if (!selected.TryGetRandomTile(out PlanetTile tile))
            {
                Log.Error("[RimRTS] Biome '" + selected.Def.defName + "' has no flat landable surface tile.");
                return;
            }

            Find.GameInitData.startingTile = tile;

            // The removed ideoligion screen also created ideology state for the new game.
            ScenarioEntry.ApplyIdeologySetup();

            base.DoNext();
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, Page.TitleAreaHeight), PageTitle);
            Text.Font = GameFont.Small;

            Widgets.Label(
                new Rect(0f, Page.TitleAreaHeight, inRect.width, HintHeight),
                "Pick the biome to land in. Only flat tiles are offered, so the battle map stays open and mountain-free.");

            float listTop = Page.TitleAreaHeight + HintHeight + ListPadding;
            Rect listRect = new Rect(0f, listTop, inRect.width, inRect.height - listTop - Page.BottomButHeight);

            if (options == null || options.Count == 0)
            {
                Widgets.Label(listRect, "No flat landable biomes were generated for this planet.");
            }
            else
            {
                DrawBiomeList(listRect);
            }

            DoBottomButtons(inRect, "Start match");
        }

        private void DrawBiomeList(Rect listRect)
        {
            const float rowHeight = 28f;
            float contentHeight = Mathf.Max(listRect.height, options.Count * rowHeight);
            Rect viewRect = new Rect(0f, 0f, listRect.width - 20f, contentHeight);

            Widgets.BeginScrollView(listRect, ref scrollPosition, viewRect);

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(viewRect);
            foreach (BiomeOption option in options)
            {
                string biomeName = option.Def.LabelCap;
                string label = biomeName + " (" + option.TileCount + " tiles)";
                if (listing.RadioButton(label, option == selected))
                {
                    selected = option;
                }
            }
            listing.End();

            Widgets.EndScrollView();
        }
    }
}
