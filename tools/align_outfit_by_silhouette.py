#!/usr/bin/env python3
"""Align outfit detail to Base via solid silhouette mats on green screen.

Pipeline (as intended for authoring):
  1. Base mat  = solid fill of body/clothable region on chroma green
  2. Outfit mat = solid fill of garment region on chroma green
  3. Solve scale+translate so outfit mat fits the Base clothable bbox
  4. Apply the same transform to the detailed Outfit pixels
  5. Rebuild Mask from Outfit alpha

This avoids guessing scale against shaded art; registration is silhouette-first.
"""
from __future__ import annotations

import io
import subprocess
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
CHAR = ROOT / "HyperPuzzle2D/Assets/Resources/Art/Chars/Maiden"
OUTFIT_PATH = CHAR / "Outfits/Teal/Outfit.png"
MASK_PATH = CHAR / "Outfits/Teal/Mask.png"
BASE_PATH = CHAR / "Base.png"
REF = CHAR / "Ref"

GREEN = (0, 255, 0, 255)
BASE_FILL = (255, 80, 180, 255)  # magenta body mat
OUTFIT_FILL = (60, 140, 255, 255)  # blue clothes mat
CENTRE = 510
SHOULDER = 327
SLEEVE_PAD = 0.10
SHORTS_Y_MAX = 780
# Ignore face/collar noise above this when scanning for tank
NECKLINE_MIN_Y = 310


def load_head_outfit() -> Image.Image:
    """Prefer committed (pre-tweak) Teal outfit as detail source."""
    data = subprocess.check_output(
        ["git", "show", "HEAD:HyperPuzzle2D/Assets/Resources/Art/Chars/Maiden/Outfits/Teal/Outfit.png"]
    )
    return Image.open(io.BytesIO(data)).convert("RGBA")


def is_hair(c) -> bool:
    r, g, b, a = c
    if a < 40:
        return False
    return r > 190 and g > 155 and b > 100 and g < r + 25 and b < r - 15


def is_dark_trim(c) -> bool:
    r, g, b, a = c
    if a < 40:
        return False
    return r < 140 and g < 130 and b < 120 and abs(r - g) < 25


def is_tank_body(c) -> bool:
    """Warm off-white tank fill (not skin-pink dominant)."""
    r, g, b, a = c
    if a < 40:
        return False
    if r < 200 or g < 190 or b < 170:
        return False
    if abs(r - g) > 40:
        return False
    # exclude strong skin (r much higher than b saturation)
    if r > 240 and b < 200 and g < 210:
        return False
    return True


def is_shorts(c) -> bool:
    r, g, b, a = c
    if a < 40:
        return False
    return 70 < r < 165 and abs(r - g) < 22 and abs(g - b) < 22 and r + g + b < 430


def find_neckline_y(base: Image.Image) -> int:
    """First row with dark tank trim near centre."""
    bp = base.load()
    w, _h = base.size
    for y in range(NECKLINE_MIN_Y, SHOULDER + 80):
        n = sum(
            1
            for x in range(CENTRE - 90, CENTRE + 91)
            if is_dark_trim(bp[x, y])
        )
        if n >= 8:
            return y
    return SHOULDER


def rebuild_base_mat_clean(base: Image.Image) -> Image.Image:
    """Magenta = tank+shorts (+ light upper-arm sleeve zone) on green."""
    w, h = base.size
    bp = base.load()
    mat = Image.new("RGBA", (w, h), GREEN)
    mp = mat.load()
    neck_y = find_neckline_y(base)
    print("detected neckline y", neck_y)
    for y in range(neck_y, SHORTS_Y_MAX):
        for x in range(w):
            c = bp[x, y]
            if c[3] < 40 or is_hair(c):
                continue
            if is_tank_body(c) or is_shorts(c) or is_dark_trim(c):
                mp[x, y] = BASE_FILL
            elif (
                neck_y <= y <= neck_y + 140
                and 90 < abs(x - CENTRE) < 180
                and c[0] > 165
                and c[1] > 115
                and c[0] > c[1] + 2
            ):
                # upper-arm sleeve pad (skin)
                mp[x, y] = BASE_FILL
    return mat


def build_outfit_mat(outfit: Image.Image) -> Image.Image:
    w, h = outfit.size
    op = outfit.load()
    mat = Image.new("RGBA", (w, h), GREEN)
    mp = mat.load()
    for y in range(h):
        for x in range(w):
            if op[x, y][3] > 40:
                mp[x, y] = OUTFIT_FILL
    return mat


def mat_bbox(mat: Image.Image, fill_rgb) -> tuple[int, int, int, int]:
    px = mat.load()
    w, h = mat.size
    xs, ys = [], []
    fr, fg, b = fill_rgb
    for y in range(h):
        for x in range(w):
            r, g, bb, _a = px[x, y]
            if (r, g, bb) == (fr, fg, b):
                xs.append(x)
                ys.append(y)
    if not xs:
        raise RuntimeError("empty mat")
    return min(xs), min(ys), max(xs) + 1, max(ys) + 1


def torso_core_bbox(base_mat: Image.Image) -> tuple[int, int, int, int]:
    """Tight bbox of clothable mat; pad width slightly for short sleeves."""
    w, h = base_mat.size
    px = base_mat.load()
    xs, ys = [], []
    for y in range(h):
        for x in range(CENTRE - 170, CENTRE + 171):
            if px[x, y][:3] == BASE_FILL[:3]:
                xs.append(x)
                ys.append(y)
    if not xs:
        return mat_bbox(base_mat, BASE_FILL[:3])
    x0, x1 = min(xs), max(xs) + 1
    y0, y1 = min(ys), max(ys) + 1
    half = (x1 - x0) / 2
    pad = int(half * SLEEVE_PAD)
    return max(0, x0 - pad), y0, min(w, x1 + pad), y1


def align_outfit(
    outfit: Image.Image,
    src_bbox: tuple[int, int, int, int],
    dst_bbox: tuple[int, int, int, int],
    canvas_size: tuple[int, int],
) -> Image.Image:
    """Width-fit to clothable; pin garment top to Base neckline."""
    sx0, sy0, sx1, sy1 = src_bbox
    dx0, dy0, dx1, dy1 = dst_bbox
    crop = outfit.crop((sx0, sy0, sx1, sy1))
    dw = dx1 - dx0
    sw, sh = crop.size
    scale = dw / sw
    nw, nh = max(1, int(sw * scale)), max(1, int(sh * scale))
    if dy0 + nh > canvas_size[1] - 40:
        scale = (canvas_size[1] - 40 - dy0) / sh
        nw, nh = max(1, int(sw * scale)), max(1, int(sh * scale))
    scaled = crop.resize((nw, nh), Image.LANCZOS)
    out = Image.new("RGBA", canvas_size, (0, 0, 0, 0))
    x = dx0 + (dw - nw) // 2
    y = dy0
    out.alpha_composite(scaled, (x, y))
    return out


def clear_face(outfit: Image.Image, neck_y: int) -> None:
    op = outfit.load()
    for y in range(0, max(0, neck_y - 25)):
        for x in range(CENTRE - 110, CENTRE + 111):
            if op[x, y][3] > 40:
                op[x, y] = (0, 0, 0, 0)


def main() -> None:
    REF.mkdir(parents=True, exist_ok=True)
    base = Image.open(BASE_PATH).convert("RGBA")
    outfit_src = load_head_outfit()
    w, h = base.size

    base_mat = rebuild_base_mat_clean(base)
    neck_y = find_neckline_y(base)
    outfit_mat = build_outfit_mat(outfit_src)
    base_mat.save(REF / "BaseMat.png")
    outfit_mat.save(REF / "OutfitMat.png")

    src_bb = mat_bbox(outfit_mat, OUTFIT_FILL[:3])
    dst_bb = torso_core_bbox(base_mat)
    print("outfit mat bbox", src_bb)
    print("base clothable bbox (padded)", dst_bb)

    aligned = align_outfit(outfit_src, src_bb, dst_bb, (w, h))
    clear_face(aligned, neck_y)
    aligned.save(OUTFIT_PATH)

    # aligned mat preview
    aligned_mat = build_outfit_mat(aligned)
    # overlay mats for QA: base magenta + outfit blue on green
    overlay = base_mat.copy()
    op = overlay.load()
    am = aligned_mat.load()
    for y in range(h):
        for x in range(w):
            if am[x, y][:3] == OUTFIT_FILL[:3]:
                # blend blue over magenta where both
                if op[x, y][:3] == BASE_FILL[:3]:
                    op[x, y] = (180, 100, 220, 255)
                else:
                    op[x, y] = OUTFIT_FILL
    overlay.save(REF / "AlignOverlay.png")

    mask = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    mp = mask.load()
    ap = aligned.load()
    for y in range(h):
        for x in range(w):
            if ap[x, y][3] > 40:
                mp[x, y] = (255, 255, 255, 255)
    mask.save(MASK_PATH)

    paper = (239, 226, 203, 255)
    torn = Image.alpha_composite(Image.new("RGBA", (w, h), paper), base)
    clothed = Image.alpha_composite(torn, aligned)
    clothed.crop((160, 180, 860, 1150)).resize((460, 860)).save(REF / "ClothedPreview.png")
    torn.crop((160, 180, 860, 1150)).resize((460, 860)).save(REF / "TornPreview.png")
    clothed.crop((120, 280, 900, 700)).resize((520, 280)).save(REF / "SleeveDetail.png")

    # side-by-side mats
    bm = base_mat.crop((200, 280, 820, 1000)).resize((300, 520))
    om = aligned_mat.crop((200, 280, 820, 1000)).resize((300, 520))
    canvas = Image.new("RGB", (620, 520), (0, 180, 0))
    canvas.paste(bm, (0, 0))
    canvas.paste(om, (320, 0))
    canvas.save(REF / "MatCompare.png")
    print("wrote Outfit/Mask + Ref mats/previews")


if __name__ == "__main__":
    main()
