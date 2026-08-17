#!/usr/bin/env python3
"""Suggest two/three star score bars for every stage in LevelLibrary.

Star bars were originally eyeballed from a handful of playtest scores, which does not
survive retuning: one change to a material's break score silently moves every rating in
the game. This derives them from the same numbers the runtime scores with instead.

Scoring model (mirrors GameLoop.RegisterDestruction):
    every destruction in a shot increments the combo, and pays breakScore * combo.
So a single shot that takes all N pieces pays sum(k=1..N) base_k * k. The bases differ
per material, but the multiplier assignment depends on collapse order, which the player
cannot pick; using the mean base makes the total order-independent:

    full_clear ~= mean_base * N * (N + 1) / 2   (+ the unspent ammo bonus)

The bars are then a fraction of that ceiling: two stars for a shot that takes a good
chunk of the board, three for something close to a total collapse.
"""
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
LIBRARY = ROOT / "HyperPuzzle2D/Assets/Scripts/Board/LevelLibrary.cs"

# breakScore per material, from DestructibleBlock.Configure.
BREAK_SCORE = {
    "#": 20,   # Normal
    "G": 25,   # Brittle
    "!": 35,   # Explosive
    "X": 40,   # Heavy
    "O": 25,   # Ball
    "|": 25,   # Support
    "=": 30,   # Beam
}

AMMO_BONUS_PER_SHOT = 20

# Fractions of a one-shot full clear. Two stars is a good hit, three is near-total.
TWO_STAR_FRACTION = 0.40
THREE_STAR_FRACTION = 0.72

LAYOUT_RE = re.compile(
    r'new LevelLayout\(\s*"(?P<name>[A-Z]+)",\s*"(?P<loadout>[BCD]+)",'
    r'\s*(?P<target>\d+),\s*(?P<two>\d+),\s*(?P<three>\d+),\s*(?P<rows>(?:"[^"]*",?\s*)+)\)',
    re.MULTILINE,
)


def pieces(rows_top_down):
    """Break scores of every independent body, merging each run of beam cells into one."""
    result = []
    for line in rows_top_down:
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

            result.append(BREAK_SCORE[glyph])
            col += span
    return result


def main():
    source = LIBRARY.read_text()
    matches = list(LAYOUT_RE.finditer(source))
    if not matches:
        print("No layouts parsed; the LevelLayout signature probably changed.")
        return 1

    print(f"{'STAGE':<10}{'N':>3}{'CEILING':>9}{'2*':>7}{'3*':>7}   {'CURRENT 2*/3*':>14}")
    for match in matches:
        bases = pieces(re.findall(r'"([^"]*)"', match.group("rows")))
        count = len(bases)
        mean_base = sum(bases) / count
        ammo = len(match.group("loadout"))

        ceiling = mean_base * count * (count + 1) / 2 + (ammo - 1) * AMMO_BONUS_PER_SHOT
        two = int(round(ceiling * TWO_STAR_FRACTION, -1))
        three = int(round(ceiling * THREE_STAR_FRACTION, -1))

        current = f"{match.group('two')}/{match.group('three')}"
        print(
            f"{match.group('name'):<10}{count:>3}{ceiling:>9.0f}{two:>7}{three:>7}   {current:>14}"
        )

    return 0


if __name__ == "__main__":
    sys.exit(main())
