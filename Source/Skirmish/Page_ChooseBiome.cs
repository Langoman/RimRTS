using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RimRTS.Skirmish
{
    /// <summary>
    /// Match-setup screen that replaces the vanilla starting-site (planet) page: the player
    /// picks a battlefield instead of clicking a tile on the planet.
    ///
    /// The screen is a plain vanilla <see cref="Page"/> using vanilla widgets. It only owns
    /// UI and the two decisions of this step (which biome, set the landing tile); world
    /// queries live in <see cref="BiomeOption"/> and new-game flow lives in
    /// <see cref="ScenarioEntry"/>. That keeps the later move to procedural battle maps a
    /// change behind this screen rather than inside it.
    ///
    /// The list is drawn as image cards instead of radio rows: each card shows the biome's
    /// world-map texture (or a plain swatch when the biome ships without one), its name, how
    /// many flat tiles the planet has for it, and its description on hover.
    /// </summary>
    public sealed class Page_ChooseBiome : Page
    {
        private const float HintHeight = 24f;
        private const float ListPadding = 6f;
        private const float CardHeight = 124f;
        private const float CardSpacing = 8f;
        private const float CardPadding = 6f;
        private const float ThumbnailWidth = 104f;
        private const int CardColumns = 2;

        private List<BiomeOption> options;
        private BiomeOption selected;
        private Vector2 scrollPosition;
        private readonly Dictionary<BiomeDef, Texture2D> thumbnails = new Dictionary<BiomeDef, Texture2D>();

        public override string PageTitle => "Choose your battlefield";

        public override Vector2 InitialSize => new Vector2(960f, 700f);

        public override void PreOpen()
        {
            base.PreOpen();

            options = BiomeOption.CollectFrom(Find.World.grid);
            selected = options.Count > 0 ? options[0] : null;

            thumbnails.Clear();
            foreach (BiomeOption option in options)
            {
                thumbnails[option.Def] = LoadThumbnail(option.Def);
            }
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
                "Pick a battlefield. Only mountain-free tiles are offered. Hover a card for a description.");

            float listTop = Page.TitleAreaHeight + HintHeight + ListPadding;
            Rect listRect = new Rect(0f, listTop, inRect.width, inRect.height - listTop - Page.BottomButHeight);

            if (options == null || options.Count == 0)
            {
                Widgets.Label(listRect, "No flat landable biomes were generated for this planet.");
            }
            else
            {
                DrawBiomeGrid(listRect);
            }

            DoBottomButtons(inRect, "Start match");
        }

        private void DrawBiomeGrid(Rect listRect)
        {
            float cardWidth = (listRect.width - 20f - CardSpacing) / CardColumns;
            float rows = Mathf.CeilToInt((float)options.Count / CardColumns);
            float contentHeight = Mathf.Max(listRect.height, rows * (CardHeight + CardSpacing));
            Rect viewRect = new Rect(0f, 0f, listRect.width - 20f, contentHeight);

            Widgets.BeginScrollView(listRect, ref scrollPosition, viewRect);

            for (int i = 0; i < options.Count; i++)
            {
                int column = i % CardColumns;
                int row = i / CardColumns;
                Rect card = new Rect(
                    column * (cardWidth + CardSpacing),
                    row * (CardHeight + CardSpacing),
                    cardWidth,
                    CardHeight);
                DrawCard(card, options[i]);
            }

            Widgets.EndScrollView();
        }

        private void DrawCard(Rect card, BiomeOption option)
        {
            bool isSelected = option == selected;
            Widgets.DrawBoxSolid(
                card,
                isSelected ? new Color(0.16f, 0.32f, 0.5f, 0.6f) : new Color(0.12f, 0.12f, 0.12f, 0.35f));

            Rect thumbnailRect = new Rect(
                card.x + CardPadding,
                card.y + CardPadding,
                ThumbnailWidth,
                card.height - CardPadding * 2f);
            DrawThumbnail(thumbnailRect, option);

            float textX = thumbnailRect.xMax + 10f;
            float textWidth = card.xMax - textX - CardPadding;

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(textX, card.y + CardPadding, textWidth, 28f), option.Def.LabelCap);

            Text.Font = GameFont.Small;
            string tilesLabel = option.TileCount + (option.TileCount == 1 ? " flat tile" : " flat tiles");
            Widgets.Label(
                new Rect(textX, card.y + CardPadding + 30f, textWidth, 22f),
                tilesLabel + (isSelected ? "  |  selected" : string.Empty));

            Text.Font = GameFont.Tiny;
            Widgets.Label(
                new Rect(textX, card.y + CardPadding + 54f, textWidth, card.height - CardPadding * 2f - 54f),
                option.Def.description);
            Text.Font = GameFont.Small;

            TooltipHandler.TipRegion(card, option.Def.description);
            if (Widgets.ButtonInvisible(card))
            {
                selected = option;
            }
        }

        private void DrawThumbnail(Rect rect, BiomeOption option)
        {
            if (thumbnails.TryGetValue(option.Def, out Texture2D texture) && texture != null)
            {
                Widgets.DrawTextureFitted(rect, texture, 1f, 1f);
            }
            else
            {
                Widgets.DrawBoxSolid(rect, new Color(0.22f, 0.26f, 0.22f));
            }
        }

        private static Texture2D LoadThumbnail(BiomeDef biome)
        {
            if (string.IsNullOrEmpty(biome.texture))
            {
                return null;
            }

            // Missing texture files are expected for modded biomes, so stay silent.
            return ContentFinder<Texture2D>.Get(biome.texture, false);
        }
    }
}
