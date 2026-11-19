using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RimRTS.Skirmish
{
    /// <summary>
    /// Final match-setup page: picks the battlefield the squad drops into.
    ///
    /// Two modes share one screen:
    /// - "Biome battlefield": a curated list of interesting flat biomes plus a rectangular map size.
    /// - "Hand-authored map": an authored <see cref="PrefabDef"/> from the mod, stamped onto a
    ///   procedurally generated map of the prefab's own size (see <see cref="BattlefieldLayout"/>).
    ///
    /// The page only decides what the map should look like; <see cref="Patches.MapGeneratorPatch"/>
    /// applies that decision while vanilla generates the initial map.
    /// </summary>
    public sealed class Page_ChooseBiome : Page
    {
        private const float HintHeight = 24f;
        private const float SetupRowHeight = 34f;
        private const float ContentGap = 6f;

        private const float CardHeight = 124f;
        private const float CardSpacing = 8f;
        private const float CardPadding = 6f;
        private const float ThumbnailWidth = 104f;
        private const int BiomeColumns = 2;

        private const float PrefabCardHeight = 92f;
        private const int PrefabColumns = 3;

        private const int DefaultMapSize = 150;
        private const int MinMapSize = 60;
        private const int MaxMapSize = 250;
        private const int MapSizeStep = 10;
        private const int MinPrefabSize = 20;
        private const int MaxPrefabSize = 400;

        private List<BiomeOption> options;
        private BiomeOption selectedBiome;
        private List<PrefabDef> prefabs;
        private PrefabDef selectedPrefab;
        private bool usePrefab;
        private int mapWidth = DefaultMapSize;
        private int mapHeight = DefaultMapSize;
        private Vector2 scrollPosition;
        private readonly Dictionary<BiomeDef, Texture2D> thumbnails = new Dictionary<BiomeDef, Texture2D>();

        public override string PageTitle => "Choose your battlefield";

        public override Vector2 InitialSize => new Vector2(980f, 700f);

        public override void PreOpen()
        {
            base.PreOpen();

            options = BiomeOption.CollectFrom(Find.World.grid);
            selectedBiome = options.Count > 0 ? options[0] : null;

            prefabs = BattlefieldPrefabs.All();
            selectedPrefab = prefabs.Count > 0 ? prefabs[0] : null;

            thumbnails.Clear();
            foreach (BiomeOption option in options)
            {
                thumbnails[option.Def] = LoadThumbnail(option.Def);
            }
        }

        protected override bool CanDoNext()
        {
            if (options.Count == 0)
            {
                return false;
            }
            return !usePrefab || selectedPrefab != null;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0f, TitleAreaHeight, inRect.width, HintHeight), Hint());

            Rect setupRect = new Rect(0f, TitleAreaHeight + HintHeight + 2f, inRect.width, SetupRowHeight);
            DrawSetupRow(setupRect);

            float contentTop = setupRect.yMax + ContentGap;
            Rect contentRect = new Rect(0f, contentTop, inRect.width, inRect.height - contentTop - BottomButHeight);
            if (usePrefab)
            {
                DrawPrefabGrid(contentRect);
            }
            else
            {
                DrawBiomeGrid(contentRect);
            }

            DoBottomButtons(inRect, "Start match");
        }

        protected override void DoNext()
        {
            if (options.Count == 0)
            {
                Messages.Message("No flat landable biomes were generated for this planet.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            if (!selectedBiome.TryGetRandomTile(out PlanetTile startingTile))
            {
                Messages.Message(
                    "The chosen biome has no flat landing tiles on this planet.",
                    MessageTypeDefOf.RejectInput,
                    false);
                return;
            }

            IntVec3 size;
            PrefabDef prefab = null;

            if (usePrefab)
            {
                if (selectedPrefab == null)
                {
                    Messages.Message("Choose a hand-authored map first.", MessageTypeDefOf.RejectInput, false);
                    return;
                }
                if (selectedPrefab.size.x < MinPrefabSize
                    || selectedPrefab.size.z < MinPrefabSize
                    || selectedPrefab.size.x > MaxPrefabSize
                    || selectedPrefab.size.z > MaxPrefabSize)
                {
                    Messages.Message(
                        "That map's size (" + selectedPrefab.size.x + "x" + selectedPrefab.size.z
                        + ") is outside the supported " + MinPrefabSize + "-" + MaxPrefabSize + " cells.",
                        MessageTypeDefOf.RejectInput,
                        false);
                    return;
                }

                prefab = selectedPrefab;
                size = new IntVec3(prefab.size.x, 1, prefab.size.z);
            }
            else
            {
                int width = Mathf.Clamp(mapWidth, MinMapSize, MaxMapSize);
                int height = Mathf.Clamp(mapHeight, MinMapSize, MaxMapSize);
                size = new IntVec3(width, 1, height);
            }

            BattlefieldLayout.Begin(size, prefab);

            Find.GameInitData.startingTile = startingTile;
            ScenarioEntry.ApplyIdeologySetup();
            base.DoNext();
        }

        private string Hint()
        {
            return usePrefab
                ? "An authored layout replaces the whole surface of the map. The match still lands on one of the flat planet tiles listed under the biome mode."
                : "Pick an interesting flat biome, then set the map shape. Hover a card for its description.";
        }

        private void DrawSetupRow(Rect rect)
        {
            bool hasPrefabs = prefabs != null && prefabs.Count > 0;

            if (Widgets.ButtonText(new Rect(0f, rect.y, 180f, 30f), (usePrefab ? "" : "- ") + "Biome battlefield"))
            {
                usePrefab = false;
                scrollPosition = Vector2.zero;
            }

            bool switchToPrefab = Widgets.ButtonText(
                new Rect(188f, rect.y, 196f, 30f),
                hasPrefabs ? (usePrefab ? "- Hand-authored map" : "Hand-authored map") : "Hand-authored map (none)",
                true,
                true,
                hasPrefabs);
            if (switchToPrefab)
            {
                usePrefab = true;
                scrollPosition = Vector2.zero;
            }

            if (usePrefab)
            {
                Widgets.Label(
                    new Rect(396f, rect.y + 6f, 400f, 24f),
                    selectedPrefab == null
                        ? "No map selected."
                        : "Map size: " + selectedPrefab.size.x + " x " + selectedPrefab.size.z + " cells.");
                DrawMapSizeNote(rect);
                return;
            }

            float x = 396f;
            DrawSizeStepper(new Rect(x, rect.y, 124f, 30f), "Width", ref mapWidth);
            x += 130f;
            DrawSizeStepper(new Rect(x, rect.y, 124f, 30f), "Height", ref mapHeight);
            x += 130f;

            if (Widgets.ButtonText(new Rect(x, rect.y, 84f, 30f), "Square"))
            {
                mapWidth = DefaultMapSize;
                mapHeight = DefaultMapSize;
            }
            x += 88f;
            if (Widgets.ButtonText(new Rect(x, rect.y, 74f, 30f), "Wide"))
            {
                mapWidth = 220;
                mapHeight = 120;
            }
            x += 78f;
            if (Widgets.ButtonText(new Rect(x, rect.y, 74f, 30f), "Tall"))
            {
                mapWidth = 120;
                mapHeight = 220;
            }
        }

        private void DrawMapSizeNote(Rect rect)
        {
            Widgets.Label(new Rect(rect.width - 120f, rect.y + 6f, 116f, 24f), "Rects allowed");
        }

        private void DrawSizeStepper(Rect rect, string label, ref int value)
        {
            Widgets.Label(new Rect(rect.x, rect.y + 6f, 40f, 24f), label);

            if (Widgets.ButtonText(new Rect(rect.x + 42f, rect.y, 22f, 30f), "-"))
            {
                value = Mathf.Max(MinMapSize, value - MapSizeStep);
            }
            Widgets.Label(new Rect(rect.x + 66f, rect.y + 6f, 34f, 24f), value.ToString());
            if (Widgets.ButtonText(new Rect(rect.x + 100f, rect.y, 22f, 30f), "+"))
            {
                value = Mathf.Min(MaxMapSize, value + MapSizeStep);
            }
        }

        private void DrawBiomeGrid(Rect area)
        {
            Rect contentRect = area;
            int rows = (options.Count + BiomeColumns - 1) / BiomeColumns;
            float viewHeight = Mathf.Max(contentRect.height - 16f, rows * (CardHeight + CardSpacing));
            Rect viewRect = new Rect(0f, 0f, contentRect.width - 16f, viewHeight);
            float cardWidth = (viewRect.width - CardSpacing * (BiomeColumns - 1)) / BiomeColumns;

            Widgets.BeginScrollView(contentRect, ref scrollPosition, viewRect);
            try
            {
                for (int i = 0; i < options.Count; i++)
                {
                    int column = i % BiomeColumns;
                    int row = i / BiomeColumns;
                    Rect card = new Rect(
                        column * (cardWidth + CardSpacing),
                        row * (CardHeight + CardSpacing),
                        cardWidth,
                        CardHeight);
                    DrawBiomeCard(card, options[i]);
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        private void DrawBiomeCard(Rect card, BiomeOption option)
        {
            bool selected = option == selectedBiome;

            Widgets.DrawBoxSolid(card, selected ? new Color(0.14f, 0.30f, 0.48f) : new Color(0.12f, 0.12f, 0.12f));
            Widgets.DrawBox(card, 1);

            Rect thumbnail = new Rect(
                card.x + CardPadding,
                card.y + CardPadding,
                ThumbnailWidth,
                card.height - CardPadding * 2f);
            Widgets.DrawBoxSolid(thumbnail, Color.black);
            Texture2D texture = ThumbnailFor(option);
            if (texture != null)
            {
                Widgets.DrawTextureFitted(thumbnail.ContractedBy(2f), texture, 1f, 1f);
            }

            float textX = thumbnail.xMax + 10f;
            float textWidth = card.xMax - textX - 10f;

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(textX, card.y + 8f, textWidth, 28f), option.Def.LabelCap);
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(textX, card.y + 38f, textWidth, 22f), option.TileCount + " flat tiles on this planet");

            Text.Font = GameFont.Tiny;
            Widgets.Label(
                new Rect(textX, card.y + 62f, textWidth, card.height - 68f),
                option.Def.description ?? string.Empty);
            Text.Font = GameFont.Small;

            if (selected)
            {
                Widgets.Label(new Rect(card.xMax - 86f, card.yMax - 28f, 78f, 20f), "selected");
            }

            TooltipHandler.TipRegion(
                card,
                (string)(option.Def.LabelCap + "\n\n" + (option.Def.description ?? string.Empty)));
            if (Widgets.ButtonInvisible(card))
            {
                selectedBiome = option;
            }
        }

        private void DrawPrefabGrid(Rect area)
        {
            Rect contentRect = area;
            int rows = (prefabs.Count + PrefabColumns - 1) / PrefabColumns;
            float viewHeight = Mathf.Max(contentRect.height - 16f, rows * (PrefabCardHeight + CardSpacing));
            Rect viewRect = new Rect(0f, 0f, contentRect.width - 16f, viewHeight);
            float cardWidth = (viewRect.width - CardSpacing * (PrefabColumns - 1)) / PrefabColumns;

            Widgets.BeginScrollView(contentRect, ref scrollPosition, viewRect);
            try
            {
                for (int i = 0; i < prefabs.Count; i++)
                {
                    int column = i % PrefabColumns;
                    int row = i / PrefabColumns;
                    Rect card = new Rect(
                        column * (cardWidth + CardSpacing),
                        row * (PrefabCardHeight + CardSpacing),
                        cardWidth,
                        PrefabCardHeight);
                    DrawPrefabCard(card, prefabs[i]);
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        private void DrawPrefabCard(Rect card, PrefabDef prefab)
        {
            bool selected = prefab == selectedPrefab;

            Widgets.DrawBoxSolid(card, selected ? new Color(0.14f, 0.30f, 0.48f) : new Color(0.12f, 0.12f, 0.12f));
            Widgets.DrawBox(card, 1);

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(card.x + 8f, card.y + 8f, card.width - 16f, 26f), BattlefieldPrefabs.Label(prefab));
            Text.Font = GameFont.Small;
            Widgets.Label(
                new Rect(card.x + 8f, card.y + 36f, card.width - 16f, 22f),
                prefab.size.x + " x " + prefab.size.z + " cells");
            Text.Font = GameFont.Tiny;
            Widgets.Label(
                new Rect(card.x + 8f, card.y + 58f, card.width - 16f, 26f),
                "Authored layout; replaces the biome surface.");
            Text.Font = GameFont.Small;

            TooltipHandler.TipRegion(
                card,
                "Hand-authored battlefield defined in PrefabDefs (defName " + prefab.defName + ").");
            if (Widgets.ButtonInvisible(card))
            {
                selectedPrefab = prefab;
            }
        }

        private Texture2D ThumbnailFor(BiomeOption option)
        {
            thumbnails.TryGetValue(option.Def, out Texture2D texture);
            return texture;
        }

        /// <summary>
        /// Biome art is optional data: missing or mistyped textures must not break the screen,
        /// so a miss is reported once here (and produces an empty thumbnail slot) instead of
        /// letting ContentFinder throw while the page is drawing.
        /// </summary>
        private static Texture2D LoadThumbnail(BiomeDef biome)
        {
            if (string.IsNullOrEmpty(biome.texture))
            {
                return null;
            }
            return ContentFinder<Texture2D>.Get(biome.texture, false);
        }
    }
}
