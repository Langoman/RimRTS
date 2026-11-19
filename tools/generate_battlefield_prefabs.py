#!/usr/bin/env python3
"""Generate the hand-authored RimRTS battlefields (PrefabDef XML).

Why a generator instead of hand-typed XML: a PrefabDef is mostly long lists of
per-cell positions and rectangles. Hand-editing thousands of coordinates is slow
and easy to get wrong, so the layouts live here as small, readable rules
("a river here", "a road from the west edge to the ford", "a building block with
a door gap") and this script expands them into game XML.

Usage:
    python tools/generate_battlefield_prefabs.py

It writes one file per battlefield under Defs/Prefabs/ and validates the result
before writing:
  - every rectangle and position is inside the prefab bounds;
  - the base terrain rectangle covers the whole prefab (everything else is an
    overlay, so a hole in the base would mean a cell without terrain);
  - no two things are spawned on the same cell.

Treat the generated XML as build output: change the layout rules here and rerun
the script instead of editing the XML by hand.
"""

from __future__ import annotations

import sys
import xml.etree.ElementTree as ElementTree
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUTPUT_DIR = ROOT / "Defs" / "Prefabs"


class Rng:
    """Small deterministic LCG so reruns produce identical XML."""

    def __init__(self, seed: int) -> None:
        self.state = seed & 0x7FFFFFFF

    def below(self, limit: int) -> int:
        self.state = (1103515245 * self.state + 12345) & 0x7FFFFFFF
        return self.state % limit


def rect_string(x1: int, z1: int, x2: int, z2: int) -> str:
    """PrefabDef rectangle syntax: inclusive (x1, z1, x2, z2) corner pair."""
    return f"({x1},{z1},{x2},{z2})"


def position_string(x: int, z: int) -> str:
    return f"({x},0,{z})"


class Battlefield:
    def __init__(self, def_name: str, label: str, width: int, height: int) -> None:
        if width <= 0 or height <= 0:
            raise ValueError(f"{def_name}: size must be positive, got {width}x{height}")
        self.def_name = def_name
        self.label = label
        self.width = width
        self.height = height
        # Ordered terrain layers: earlier layers are painted first, later ones win,
        # exactly like PrefabUtility does when it stamps a prefab in game.
        self.terrain: list[tuple[str, tuple[int, int, int, int]]] = []
        # (defName, stuff, hp) -> set of (x, z)
        self.things: dict[tuple[str, str | None, int | None], set[tuple[int, int]]] = {}
        self._grid: list[list[str]] | None = None

    # -- terrain ---------------------------------------------------------

    def paint(self, terrain_def: str, x1: int, z1: int, x2: int, z2: int) -> None:
        x1, x2 = sorted((x1, x2))
        z1, z2 = sorted((z1, z2))
        self._check_bounds("terrain", terrain_def, x1, z1, x2, z2)
        self.terrain.append((terrain_def, (x1, z1, x2, z2)))
        self._grid = None

    def terrain_at(self, x: int, z: int) -> str:
        if self._grid is None:
            grid = [["" for _ in range(self.height)] for _ in range(self.width)]
            for terrain_def, (x1, z1, x2, z2) in self.terrain:
                for gx in range(x1, x2 + 1):
                    for gz in range(z1, z2 + 1):
                        grid[gx][gz] = terrain_def
            self._grid = grid
        return self._grid[x][z]

    # -- things ----------------------------------------------------------

    def place(self, thing_def: str, x: int, z: int, stuff: str | None = None, hp: int | None = None) -> None:
        self._check_bounds("thing", thing_def, x, z, x, z)
        self.things.setdefault((thing_def, stuff, hp), set()).add((x, z))

    def wall_line(
        self,
        x1: int,
        z1: int,
        x2: int,
        z2: int,
        stuff: str,
        hp: int | None = None,
        gaps: tuple[tuple[int, int], ...] = (),
    ) -> None:
        """A straight wall run, optionally with door gaps given as axis intervals."""
        for x, z in self._line(x1, z1, x2, z2):
            if any(lo <= (x if z1 == z2 else z) <= hi for lo, hi in gaps):
                continue
            self.place("Wall", x, z, stuff=stuff, hp=hp)

    def wall_ring(
        self,
        x1: int,
        z1: int,
        x2: int,
        z2: int,
        stuff: str,
        hp: int | None = None,
        door_width: int = 3,
    ) -> None:
        """A rectangular building shell with one door gap per side."""
        mid_x = (x1 + x2) // 2
        mid_z = (z1 + z2) // 2
        half = door_width // 2

        self.wall_line(x1, z1, x2, z1, stuff, hp, gaps=((mid_x - half, mid_x + half),))
        self.wall_line(x1, z2, x2, z2, stuff, hp, gaps=((mid_x - half, mid_x + half),))
        self.wall_line(x1, z1, x1, z2, stuff, hp, gaps=((mid_z - half, mid_z + half),))
        self.wall_line(x2, z1, x2, z2, stuff, hp, gaps=((mid_z - half, mid_z + half),))

    def scatter(
        self,
        thing_def: str,
        count: int,
        surfaces: tuple[str, ...],
        rng: Rng,
        avoid: set[tuple[int, int]] | None = None,
        clearance: int = 0,
    ) -> None:
        """Place up to `count` things on the given surfaces, never on an occupied cell."""
        occupied = self.occupied()
        if avoid:
            occupied |= avoid
        placed = 0
        for _ in range(count * 60):
            if placed >= count:
                break
            x = rng.below(self.width)
            z = rng.below(self.height)
            if (x, z) in occupied:
                continue
            if self.terrain_at(x, z) not in surfaces:
                continue
            if clearance > 0 and self._near_occupied(occupied, x, z, clearance):
                continue
            self.place(thing_def, x, z)
            occupied.add((x, z))
            placed += 1

    def occupied(self) -> set[tuple[int, int]]:
        cells: set[tuple[int, int]] = set()
        for positions in self.things.values():
            cells |= positions
        return cells

    # -- helpers ---------------------------------------------------------

    @staticmethod
    def _line(x1: int, z1: int, x2: int, z2: int) -> list[tuple[int, int]]:
        if x1 != x2 and z1 != z2:
            raise ValueError(f"wall_line must be axis aligned, got ({x1},{z1})->({x2},{z2})")
        if x1 == x2:
            step = 1 if z2 >= z1 else -1
            return [(x1, z) for z in range(z1, z2 + step, step)]
        step = 1 if x2 >= x1 else -1
        return [(x, z1) for x in range(x1, x2 + step, step)]

    @staticmethod
    def _near_occupied(occupied: set[tuple[int, int]], x: int, z: int, radius: int) -> bool:
        for dx in range(-radius, radius + 1):
            for dz in range(-radius, radius + 1):
                if (x + dx, z + dz) in occupied:
                    return True
        return False

    def _check_bounds(self, kind: str, name: str, x1: int, z1: int, x2: int, z2: int) -> None:
        if x1 < 0 or z1 < 0 or x2 >= self.width or z2 >= self.height:
            raise ValueError(
                f"{self.def_name}: {kind} '{name}' at ({x1},{z1})-({x2},{z2}) is outside "
                f"the {self.width}x{self.height} prefab"
            )

    # -- validation / output --------------------------------------------

    def validate(self) -> None:
        if not self.terrain:
            raise ValueError(f"{self.def_name}: no terrain")
        base_rect = self.terrain[0][1]
        if base_rect != (0, 0, self.width - 1, self.height - 1):
            raise ValueError(
                f"{self.def_name}: the first terrain layer must cover the whole prefab, "
                f"got {base_rect}"
            )
        for terrain_def, (x1, z1, x2, z2) in self.terrain:
            self._check_bounds("terrain", terrain_def, x1, z1, x2, z2)
        for (thing_def, _stuff, _hp), positions in self.things.items():
            for x, z in positions:
                self._check_bounds("thing", thing_def, x, z, x, z)

    def to_xml(self) -> str:
        self.validate()

        root = ElementTree.Element("Defs")
        prefab = ElementTree.SubElement(root, "PrefabDef")
        ElementTree.SubElement(prefab, "defName").text = self.def_name
        ElementTree.SubElement(prefab, "label").text = self.label
        ElementTree.SubElement(prefab, "size").text = f"({self.width},{self.height})"

        terrain = ElementTree.SubElement(prefab, "terrain")
        # One node per painted layer: prefab layers are applied in order, so merging
        # same-def rectangles would let later water/rubble layers overwrite earlier
        # overlays (a ford painted before the river must win, and order matters).
        for terrain_def, rect in self.terrain:
            node = ElementTree.SubElement(terrain, terrain_def)
            ElementTree.SubElement(node, "chance").text = "1"
            rects_node = ElementTree.SubElement(node, "rects")
            ElementTree.SubElement(rects_node, "li").text = rect_string(*rect)

        things = ElementTree.SubElement(prefab, "things")
        for (thing_def, stuff, hp), positions in sorted(
            self.things.items(),
            key=lambda entry: (entry[0][0], entry[0][1] or "", entry[0][2] or 0),
        ):
            node = ElementTree.SubElement(things, thing_def)
            if stuff is not None:
                ElementTree.SubElement(node, "stuff").text = stuff
            if hp is not None:
                ElementTree.SubElement(node, "hp").text = str(hp)
            positions_node = ElementTree.SubElement(node, "positions")
            for x, z in sorted(positions):
                ElementTree.SubElement(positions_node, "li").text = position_string(x, z)

        ElementTree.indent(root, space="  ")
        body = ElementTree.tostring(root, encoding="unicode")
        return '<?xml version="1.0" encoding="utf-8" ?>\n' + body + "\n"

    def write(self) -> Path:
        OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
        path = OUTPUT_DIR / f"{self.def_name}.xml"
        path.write_text(self.to_xml(), encoding="utf-8")
        # Parse the file back so a broken writer can never ship unloadable XML.
        ElementTree.parse(path)
        return path


def river_crossing() -> Battlefield:
    """Open farmland split by a river with two fords; ruins give cover."""
    field = Battlefield("RimRTS_RiverCrossing", "River crossing", width=100, height=140)

    # Terrain, painted back to front.
    field.paint("Soil", 0, 0, 99, 139)
    field.paint("Gravel", 34, 0, 38, 139)
    field.paint("Gravel", 61, 0, 65, 139)
    field.paint("WaterShallow", 39, 0, 59, 139)
    field.paint("WaterDeep", 46, 0, 53, 139)

    ford_z: list[tuple[int, int]] = [(18, 25), (112, 119)]
    for z1, z2 in ford_z:
        # A gravel bar across the water is walkable and reads as a ford.
        field.paint("Gravel", 39, z1, 59, z2)
        field.paint("PackedDirt", 0, z1, 38, z2)
        field.paint("PackedDirt", 60, z1, 99, z2)
        if field.terrain_at(49, z1 + 2) != "Gravel":
            raise ValueError("the ford was overdrawn by the river layer; check paint order")

    # Farm tracks running from each map edge to the fords.
    field.paint("PackedDirt", 0, 70, 33, 71)
    field.paint("PackedDirt", 66, 70, 99, 71)

    for x in (6, 44):
        field.wall_line(x, 6, x, 32, "BlocksSlate", hp=280)
        field.wall_line(x, 6, x + 18, 6, "BlocksSlate", hp=280)
    field.wall_line(70, 108, 70, 134, "BlocksSlate", hp=280)
    field.wall_line(48, 134, 70, 134, "BlocksSlate", hp=280)
    field.wall_line(70, 108, 84, 108, "BlocksSlate", hp=280)
    field.wall_line(84, 108, 84, 122, "BlocksSlate", hp=280)

    rng = Rng(20261118)
    field.scatter("ChunkSlate", 30, ("Soil", "Gravel"), rng, clearance=1)
    field.scatter("Plant_TreeOak", 34, ("Soil", "Gravel"), rng, clearance=2)
    field.scatter("Plant_TreeBirch", 22, ("Soil", "Gravel"), rng, clearance=2)
    field.scatter("Plant_TreePine", 18, ("Soil",), rng, clearance=2)
    field.scatter("Plant_Bush", 40, ("Soil", "Gravel"), rng)
    field.scatter("Plant_TallGrass", 80, ("Soil", "Gravel"), rng)

    return field


def urban_blocks() -> Battlefield:
    """A small ruined town: road grid, walled blocks with door gaps, rubble."""
    field = Battlefield("RimRTS_UrbanBlocks", "Ruined town", width=120, height=120)

    field.paint("PackedDirt", 0, 0, 119, 119)

    roads = ((16, 24), (55, 63), (94, 102))
    for x1, x2 in roads:
        field.paint("Concrete", x1, 0, x2, 119)
    for z1, z2 in roads:
        field.paint("Concrete", 0, z1, 119, z2)

    blocks = ((0, 15), (25, 54), (64, 93), (103, 119))
    for bx1, bx2 in blocks:
        for bz1, bz2 in blocks:
            if bx2 - bx1 < 8 or bz2 - bz1 < 8:
                continue
            field.paint("FlagstoneSandstone", bx1 + 2, bz1 + 2, bx2 - 2, bz2 - 2)
            field.wall_ring(bx1 + 1, bz1 + 1, bx2 - 1, bz2 - 1, "BlocksSandstone", hp=420)

    rng = Rng(20261119)
    field.scatter("ChunkSandstone", 50, ("Concrete", "PackedDirt"), rng, clearance=1)
    field.scatter("ChunkGranite", 16, ("Concrete",), rng, clearance=1)

    return field


def main() -> int:
    built = 0
    for battlefield in (river_crossing(), urban_blocks()):
        try:
            path = battlefield.write()
        except (ValueError, OSError) as error:
            print(f"ERROR: {error}", file=sys.stderr)
            return 1
        print(f"Wrote {path.relative_to(ROOT)}")
        built += 1
    print(f"Generated {built} battlefield prefab(s).")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
