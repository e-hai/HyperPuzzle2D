#!/usr/bin/env python3
"""Maiden Line → Base (v3): geometric keep, no colour-as-clothes.

Keep only:
  - figure above the collar line (head + hair)
  - figure below the hem (legs)
  - hand islands on the sides

Everything else in the torso band is wiped and replaced by a body drawn
from measured centre + tapered half-widths, with ink Line.png as the
authority outline.
"""

from __future__ import annotations

import math
import os
from collections import deque
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.join(os.path.dirname(__file__), "..", "HyperPuzzle2D", "Assets", "Resources", "Art")
SRC = os.path.join(ROOT, "PaperMaiden.png")
OUT_DIR = os.path.join(ROOT, "Chars", "Maiden")

SHOULDER_V, WAIST_V, HIP_END_V, HEM_V = 0.787, 0.64, 0.54, 0.43

INK = (42, 32, 28, 255)
SKIN = (241, 203, 148, 255)
SKIN_SHADE = (226, 172, 124, 255)
TANK = (250, 240, 226, 255)
TANK_SHADE = (232, 214, 194, 255)
SHORTS = (112, 102, 108, 255)
SHORTS_SHADE = (88, 82, 88, 255)


def v_to_y(v, h):
    return int(round((1.0 - v) * (h - 1)))


def near(c, ref, tol2):
    dr, dg, db = c[0] - ref[0], c[1] - ref[1], c[2] - ref[2]
    return dr * dr + dg * dg + db * db <= tol2


def build_figure(px, w, h):
    bg = px[2, 2]
    bg_mask = [[False] * w for _ in range(h)]
    q = deque()

    def seed(x, y):
        if not bg_mask[y][x] and near(px[x, y], bg, 1600):
            bg_mask[y][x] = True
            q.append((x, y))

    for x in range(w):
        seed(x, 0)
        seed(x, h - 1)
    for y in range(h):
        seed(0, y)
        seed(w - 1, y)

    while q:
        x, y = q.popleft()
        for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1)):
            yy, xx = y + dy, x + dx
            if 0 <= yy < h and 0 <= xx < w and not bg_mask[yy][xx]:
                c = px[xx, yy]
                if near(c, bg, 1600) or (near(c, px[x, y], 1000) and near(c, bg, 3200)):
                    bg_mask[yy][xx] = True
                    q.append((xx, yy))

    return [[(not bg_mask[y][x]) and px[x, y][3] > 8 for x in range(w)] for y in range(h)]


def dilate(mask, w, h, n=1):
    cur = mask
    for _ in range(n):
        nxt = [row[:] for row in cur]
        for y in range(h):
            for x in range(w):
                if cur[y][x]:
                    continue
                for dy in (-1, 0, 1):
                    for dx in (-1, 0, 1):
                        yy, xx = y + dy, x + dx
                        if 0 <= yy < h and 0 <= xx < w and cur[yy][xx]:
                            nxt[y][x] = True
                            break
                    else:
                        continue
                    break
        cur = nxt
    return cur


def is_skin(c):
    r, g, b = c[0], c[1], c[2]
    if r < 175 or g < 125 or b < 95:
        return False
    if r <= g + 8 or r <= b + 15:
        return False
    # Reject teal / orange / gold.
    if g > r - 10 and b > 140:
        return False
    if r > 200 and g < 150 and b < 110:
        return False
    return True


def row_edges(fig, y, w):
    xs = [x for x in range(w) if fig[y][x]]
    return (xs[0], xs[-1]) if xs else None


def build_hands(fig, px, w, h):
    y0, y1 = v_to_y(0.58, h), v_to_y(0.45, h)
    if y0 > y1:
        y0, y1 = y1, y0
    hand = [[False] * w for _ in range(h)]
    for y in range(y0, y1 + 1):
        e = row_edges(fig, y, w)
        if not e:
            continue
        for x in list(range(e[0], min(w, e[0] + 70))) + list(range(max(0, e[1] - 70), e[1] + 1)):
            if fig[y][x] and is_skin(px[x, y]):
                hand[y][x] = True

    q = deque((x, y) for y in range(h) for x in range(w) if hand[y][x])
    seen = [[False] * w for _ in range(h)]
    for x, y in q:
        seen[y][x] = True
    while q:
        x, y = q.popleft()
        for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1)):
            yy, xx = y + dy, x + dx
            if not (0 <= yy < h and 0 <= xx < w) or seen[yy][xx]:
                continue
            if not fig[yy][xx] or not is_skin(px[xx, yy]):
                continue
            if 0.34 * w < xx < 0.66 * w:
                continue
            if yy < y0 - 20 or yy > y1 + 40:
                continue
            seen[yy][xx] = True
            hand[yy][xx] = True
            q.append((xx, yy))
    return dilate(hand, w, h, 2)


def head_width(fig, w, h, y_shoulder, centre):
    """Face capsule width only — twin tails must not inflate the body."""
    widths = []
    # Face band: between eyes/cheeks, ignore far pigtail pixels.
    y0, y1 = int(h * 0.14), int(h * 0.22)
    for y in range(y0, y1):
        xs = [x for x in range(w) if fig[y][x] and abs(x - centre) < w * 0.16]
        if len(xs) >= 8:
            widths.append(max(xs) - min(xs))
    widths.sort()
    if not widths:
        return 210
    return widths[len(widths) // 2]


def make_halves(w, h, y_shoulder, y_hem, head_w):
    """Calibrated Maiden underclothes half-widths (px) for this 1024 sheet."""
    y_waist = v_to_y(WAIST_V, h)
    y_hip = v_to_y(HIP_END_V, h)
    shoulder, waist, hip, hem = 98.0, 70.0, 90.0, 84.0
    halves = [None] * h
    for y in range(y_shoulder, y_hem + 1):
        if y <= y_waist:
            t = (y - y_shoulder) / max(1, y_waist - y_shoulder)
            sh = shoulder * (1.0 - 0.05 * min(1.0, t * 2.5))
            halves[y] = sh * (1 - t) + waist * t
        elif y <= y_hip:
            t = (y - y_waist) / max(1, y_hip - y_waist)
            halves[y] = waist * (1 - t) + hip * t
        else:
            t = (y - y_hip) / max(1, y_hem - y_hip)
            halves[y] = hip * (1 - t) + hem * t
    for _ in range(3):
        for y in range(y_shoulder + 1, y_hem):
            if halves[y - 1] and halves[y + 1]:
                halves[y] = 0.25 * halves[y - 1] + 0.5 * halves[y] + 0.25 * halves[y + 1]
    return halves, y_waist, y_hip


def ellipse_put(body, w, h, cx, cy, rx, ry):
    for yy in range(max(0, int(cy - ry)), min(h, int(cy + ry) + 1)):
        for xx in range(max(0, int(cx - rx)), min(w, int(cx + rx) + 1)):
            if ((xx - cx) / rx) ** 2 + ((yy - cy) / ry) ** 2 <= 1.0:
                body[yy][xx] = True


def make_body(w, h, centre, halves, y_shoulder, y_hem, neck_depth):
    body = [[False] * w for _ in range(h)]
    for y in range(y_shoulder, y_hem + 1):
        half = halves[y]
        if half is None:
            continue
        if y < y_shoulder + neck_depth:
            t = (y - y_shoulder) / max(1, neck_depth)
            open_h = half * (0.22 + 0.78 * t)
            for x in range(int(centre - half), int(centre - open_h) + 1):
                if 0 <= x < w:
                    body[y][x] = True
            for x in range(int(centre + open_h), int(centre + half) + 1):
                if 0 <= x < w:
                    body[y][x] = True
        else:
            for x in range(int(centre - half), int(centre + half) + 1):
                if 0 <= x < w:
                    body[y][x] = True

    hh = halves[y_hem] or 60
    notch = int(hh * 0.85)
    for y in range(y_hem - notch, y_hem + 1):
        t = (y - (y_hem - notch)) / max(1, notch)
        gap = int(hh * 0.34 * t)
        for x in range(centre - gap, centre + gap + 1):
            if 0 <= x < w:
                body[y][x] = False
    return body


def add_arms(body, hand, w, h, centre, y_shoulder, halves):
    def centroid(lo, hi):
        sx = sy = n = 0
        for y in range(h):
            for x in range(lo, hi):
                if hand[y][x]:
                    sx += x
                    sy += y
                    n += 1
        return (sx / n, sy / n, n) if n > 40 else None

    sh = halves[min(h - 1, y_shoulder + 18)] or 80
    for side, lo, hi in (("L", 0, int(w * 0.36)), ("R", int(w * 0.64), w)):
        c = centroid(lo, hi)
        if not c:
            continue
        hx, hy, _ = c
        x0 = centre + (-sh if side == "L" else sh) * 0.9
        y0 = y_shoulder + 20
        steps = int(max(abs(hx - x0), abs(hy - y0), 1)) + 1
        for i in range(steps):
            t = i / (steps - 1)
            x = x0 + (hx - x0) * t
            y = y0 + (hy - y0) * t
            outward = -1 if side == "L" else 1
            x += outward * math.sin(t * math.pi) * 16
            r = 26 * (1 - t) + 12 * t
            ellipse_put(body, w, h, x, y, r * 0.72, r)
    return body


def draw_line(w, h, keep, body, y_shoulder, y_waist, y_hem, centre, halves):
    union = [[keep[y][x] or body[y][x] for x in range(w)] for y in range(h)]
    line = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    lp = line.load()
    for y in range(1, h - 1):
        for x in range(1, w - 1):
            if union[y][x] and not (
                union[y - 1][x] and union[y + 1][x] and union[y][x - 1] and union[y][x + 1]
            ):
                lp[x, y] = INK

    draw = ImageDraw.Draw(line)
    neck_r = int((halves[min(h - 1, y_shoulder + 22)] or 70) * 0.48)
    draw.arc(
        [centre - neck_r, y_shoulder + 2, centre + neck_r, y_shoulder + int(neck_r * 1.2)],
        12,
        168,
        fill=INK,
        width=2,
    )
    hw = halves[y_waist] or 50
    draw.line([(centre - hw + 2, y_waist), (centre + hw - 2, y_waist)], fill=INK, width=2)
    # Tank bottom edge above midriff.
    y_tank = y_waist - int(h * 0.012)
    hw2 = halves[y_tank] or hw
    draw.line([(centre - hw2 + 2, y_tank), (centre + hw2 - 2, y_tank)], fill=INK, width=2)
    hh = halves[y_hem] or 60
    gap = int(hh * 0.18)
    draw.line([(centre - hh, y_hem), (centre - gap, y_hem)], fill=INK, width=2)
    draw.line([(centre + gap, y_hem), (centre + hh, y_hem)], fill=INK, width=2)
    return line.filter(ImageFilter.MaxFilter(3))


def fill_base(px, keep, body, w, h, y_shoulder, y_waist, y_hem, centre, halves):
    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    op = out.load()
    for y in range(h):
        for x in range(w):
            if keep[y][x]:
                op[x, y] = px[x, y]

    mid0 = y_waist - int(h * 0.014)
    mid1 = y_waist + int(h * 0.006)

    for y in range(h):
        half = halves[y] if y < len(halves) and halves[y] else None
        for x in range(w):
            if not body[y][x] or keep[y][x]:
                continue
            t = abs(x - centre) / max(1.0, half or 80)
            if t > 1.05:
                op[x, y] = SKIN if t < 1.4 else SKIN_SHADE
            elif y_shoulder <= y < mid0:
                op[x, y] = TANK if t < 0.8 else TANK_SHADE
            elif mid0 <= y <= mid1:
                op[x, y] = SKIN if t < 0.82 else SKIN_SHADE
            elif y <= y_hem:
                op[x, y] = SHORTS if t < 0.78 else SHORTS_SHADE
    return out


def stamp_ink(base, line):
    bp, lp = base.load(), line.load()
    w, h = base.size
    for y in range(h):
        for x in range(w):
            if lp[x, y][3] > 100 and bp[x, y][3] > 0:
                r, g, b, a = bp[x, y]
                bp[x, y] = (
                    int(r * 0.45 + INK[0] * 0.55),
                    int(g * 0.45 + INK[1] * 0.55),
                    int(b * 0.45 + INK[2] * 0.55),
                    a,
                )
    return base


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    src = Image.open(SRC).convert("RGBA")
    w, h = src.size
    px = src.load()
    fig = build_figure(px, w, h)

    y_shoulder = v_to_y(SHOULDER_V, h)
    y_hem = v_to_y(HEM_V, h)
    centres = []
    for y in range(int(h * 0.12), y_shoulder):
        e = row_edges(fig, y, w)
        if e and (e[1] - e[0]) < w * 0.45:  # head rows, not pigtail extremes alone
            centres.append((e[0] + e[1]) * 0.5)
    centre = int(sum(centres) / len(centres)) if centres else w // 2

    hand = build_hands(fig, px, w, h)

    # Geometric keep — never keep mid-band clothes.
    keep = [[False] * w for _ in range(h)]
    for y in range(h):
        for x in range(w):
            if not fig[y][x]:
                continue
            if y < y_shoulder or y > y_hem or hand[y][x]:
                keep[y][x] = True

    hw = head_width(fig, w, h, y_shoulder, centre)
    halves, y_waist, y_hip = make_halves(w, h, y_shoulder, y_hem, hw)
    print(
        f"centre={centre} head_w={hw} "
        f"half shoulder={halves[y_shoulder+20]:.1f} waist={halves[y_waist]:.1f} hem={halves[y_hem]:.1f}"
    )

    body = make_body(w, h, centre, halves, y_shoulder, y_hem, int(h * 0.03))
    body = add_arms(body, hand, w, h, centre, y_shoulder, halves)
    body = dilate(body, w, h, 1)

    line = draw_line(w, h, keep, body, y_shoulder, y_waist, y_hem, centre, halves)
    base = fill_base(px, keep, body, w, h, y_shoulder, y_waist, y_hem, centre, halves)
    base = stamp_ink(base, line)

    line.save(os.path.join(OUT_DIR, "Line.png"))
    base.save(os.path.join(OUT_DIR, "Base.png"))

    paper = (239, 226, 203, 255)
    torn = Image.alpha_composite(Image.new("RGBA", (w, h), paper), base)
    torn.crop((200, 40, 820, 1200)).resize((420, 780)).save("/tmp/maiden_torn_preview.png")
    # Line on paper for readability
    line_vis = Image.alpha_composite(Image.new("RGBA", (w, h), (250, 244, 232, 255)), line)
    line_vis.crop((200, 40, 820, 1200)).resize((420, 780)).save("/tmp/maiden_new_line_torso.png")
    outfit = Image.open(os.path.join(OUT_DIR, "Outfits", "Teal", "Outfit.png")).convert("RGBA")
    Image.alpha_composite(torn, outfit).crop((200, 40, 820, 1200)).resize((420, 780)).save(
        "/tmp/maiden_base_under_outfit.png"
    )
    print("wrote Line.png + Base.png")


if __name__ == "__main__":
    main()
