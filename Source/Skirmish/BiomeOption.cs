using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimRTS.Skirmish
{
    /// <summary>
    /// One landable biome plus every surface tile of the generated planet that uses it.
    ///
    /// This is the data layer of the biome screen: it knows how to inspect the planet and
    /// hand out a landing tile, and it knows nothing about UI, so procedural battle maps
    /// can later replace the tile choice without touching the screen.
    /// </summary>
    internal sealed class BiomeOption
    {
        /// <summary>
        /// Biomes that make readable skirmish battlefields: open ground with usable cover,
        /// no arctic/water/space/underground dead ends. This is a defName allowlist so it
        /// works with or without the DLCs installed: a missing biome is simply never found,
        /// and a planet made entirely of unlisted biomes falls back to every landable biome.
        /// To bring a biome back, add its defName here.
        /// </summary>
        private static readonly HashSet<string> BattlefieldBiomes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "TemperateForest",
            "TemperateSwamp",
            "TropicalRainforest",
            "TropicalSwamp",
            "BorealForest",
            "ColdBog",
            "Grasslands",
            "Glowforest",
            "AridShrubland",
            "Desert",
            "Scarlands",
        };

        private readonly List<PlanetTile> tiles;

        private BiomeOption(BiomeDef def, List<PlanetTile> tiles)
        {
            Def = def;
            this.tiles = tiles;
        }

        public BiomeDef Def { get; }

        public int TileCount => tiles.Count;

        /// <summary>
        /// Builds the biome list from the generated planet, ordered by label so the screen
        /// is deterministic. Water, background and impassable biomes are not landing sites,
        /// and only Flat world tiles are collected: vanilla map generation turns hills and
        /// mountains into rock, and a skirmish battlefield should be open ground.
        /// </summary>
        public static List<BiomeOption> CollectFrom(WorldGrid grid)
        {
            var battlefieldTiles = new Dictionary<BiomeDef, List<PlanetTile>>();
            var landableTiles = new Dictionary<BiomeDef, List<PlanetTile>>();

            foreach (Tile tile in grid.Surface.Tiles)
            {
                BiomeDef biome = tile.PrimaryBiome;
                if (!IsLandable(biome) || tile.hilliness != Hilliness.Flat)
                {
                    continue;
                }

                // Kept separately so a planet that contains none of the curated biomes
                // still offers every flat landable biome instead of an empty screen.
                AddTile(landableTiles, biome, tile.tile);

                if (IsBattlefieldBiome(biome))
                {
                    AddTile(battlefieldTiles, biome, tile.tile);
                }
            }

            Dictionary<BiomeDef, List<PlanetTile>> chosen =
                battlefieldTiles.Count > 0 ? battlefieldTiles : landableTiles;

            return chosen
                .Select(pair => new BiomeOption(pair.Key, pair.Value))
                .OrderBy(option => option.Def.label)
                .ToList();
        }

        public bool TryGetRandomTile(out PlanetTile tile)
        {
            if (tiles.Count == 0)
            {
                tile = PlanetTile.Invalid;
                return false;
            }

            tile = tiles.RandomElement();
            return true;
        }

        private static void AddTile(Dictionary<BiomeDef, List<PlanetTile>> tilesByBiome, BiomeDef biome, PlanetTile tile)
        {
            if (!tilesByBiome.TryGetValue(biome, out List<PlanetTile> biomeTiles))
            {
                biomeTiles = new List<PlanetTile>();
                tilesByBiome.Add(biome, biomeTiles);
            }

            biomeTiles.Add(tile);
        }

        private static bool IsBattlefieldBiome(BiomeDef biome)
        {
            return BattlefieldBiomes.Contains(biome.defName);
        }

        private static bool IsLandable(BiomeDef biome)
        {
            return biome != null
                && !biome.impassable
                && !biome.isWaterBiome
                && !biome.isBackgroundBiome;
        }
    }
}
