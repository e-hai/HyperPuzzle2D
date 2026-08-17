#!/usr/bin/env python3
"""Checks LevelLibrary structures for pieces that would fall at spawn.

Mirrors GameDirector.SpawnStructure: every cell is its own rigid body, so a piece
needs something directly beneath it. Only a beam spans a gap, and even a beam needs
at least one support somewhere under its run.
"""

import re
import sys
from pathlib import Path

LIBRARY = Path(__file__).resolve().parents[1] / "HyperPuzzle2D/Assets/Scripts/Board/LevelLibrary.cs"

# Must match GameDirector.
SHELF_CENTER_X = 1.1
SHELF_MARGIN = 0.4
SHELF_MIN_WIDTH = 2.6
BLOCK_PITCH = 0.9
BLOCK_SIZE = 0.9
FIELD_HALF_WIDTH = 3.7

LAYOUT_RE = re.compile(
    r'new LevelLayout\(\s*"(?P<name>[A-Z]+)",\s*"(?P<loadout>[BCD]+)",'
    r'\s*\d+,\s*\d+,\s*\d+,\s*(?P<rows>(?:"[^"]*",?\s*)+)\)',
    re.MULTILINE,
)


def parse_layouts(source):
    layouts = []
    for match in LAYOUT_RE.finditer(source):
        rows = re.findall(r'"([^"]*)"', match.group("rows"))
        layouts.append((match.group("name"), len(match.group("loadout")), rows))
    return layouts


def occupied(rows_bottom_up, col, row):
    if row < 0 or row >= len(rows_bottom_up):
        return False
    line = rows_bottom_up[row]
    return col < len(line) and line[col] != "."


def check(name, ammo, rows_top_down):
    rows = list(reversed(rows_top_down))  # row 0 == rests on the shelf
    width = max(len(r) for r in rows)
    problems = []
    targets = 0

    for row_index, line in enumerate(rows):
        col = 0
        while col < len(line):
            glyph = line[col]
            if glyph == ".":
                col += 1
                continue

            span = 1
            if glyph == "=":
                while col + span < len(line) and line[col + span] == "=":
                    span += 1

            targets += 1

            if row_index > 0:
                supports = [c for c in range(col, col + span) if occupied(rows, c, row_index - 1)]
                if not supports:
                    problems.append(
                        f"floating '{glyph}' at column {col} row {row_index} (nothing beneath)"
                    )
                elif glyph == "=":
                    # A beam tips unless its supports straddle the run's centre of mass.
                    centre = col + (span - 1) / 2
                    if min(supports) > centre or max(supports) < centre:
                        problems.append(
                            f"unbalanced beam at columns {col}-{col + span - 1} row {row_index} "
                            f"(supports at {supports}, centre {centre})"
                        )
            col += span

    # The pad is sized to the columns the layout actually fills, and the stack is centred on it,
    # so the check that matters is whether that pad still fits the camera's fixed field width.
    filled = [c for row in rows for c, glyph in enumerate(row) if glyph != "."]
    if not filled:
        problems.append("empty layout")
        return targets, 0.0, problems

    first, last = min(filled), max(filled)
    footprint = last - first + 1
    origin_x = SHELF_CENTER_X - (first + last) / 2 * BLOCK_PITCH
    left = origin_x + first * BLOCK_PITCH - BLOCK_SIZE / 2
    right = origin_x + last * BLOCK_PITCH + BLOCK_SIZE / 2

    shelf_width = max(SHELF_MIN_WIDTH, footprint * BLOCK_PITCH + SHELF_MARGIN)
    shelf_left = SHELF_CENTER_X - shelf_width / 2
    shelf_right = SHELF_CENTER_X + shelf_width / 2
    if shelf_left < -FIELD_HALF_WIDTH or shelf_right > FIELD_HALF_WIDTH:
        problems.append(
            f"pad leaves the field: {shelf_left:.2f}..{shelf_right:.2f} "
            f"vs +/-{FIELD_HALF_WIDTH:.2f}"
        )

    return targets, round(right - left, 2), problems


def main():
    layouts = parse_layouts(LIBRARY.read_text())
    if not layouts:
        print("no layouts parsed", file=sys.stderr)
        return 1

    failed = False
    print(f"{'LAYOUT':<12}{'AMMO':>5}{'TARGETS':>9}{'WIDTH':>7}   NOTES")
    for name, ammo, rows in layouts:
        targets, width, problems = check(name, ammo, rows)
        note = "ok" if not problems else "; ".join(problems)
        if problems:
            failed = True
        print(f"{name:<12}{ammo:>5}{targets:>9}{width:>7}   {note}")
        print(f"{'':<12}{'':>5}{'':>9}{'':>7}   {targets / ammo:.1f} targets per shot")

    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
