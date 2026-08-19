#!/usr/bin/env python3
"""Keep Maiden Base body; place clothes-only art scaled to Base shoulders.

Expects:
  assets/maiden_clothes_only.png  (AI clothes layer, light gray bg OK)
  assets/maiden_base_single.png    (underclothes body concept)
  Art/PaperMaiden.png             (original head source)

Writes:
  Chars/Maiden/Base.png, Line.png
  Chars/Maiden/Outfits/Teal/{Outfit,Mask}.png
"""
from __future__ import annotations

from collections import deque
from pathlib import Path

from PIL import Image, ImageFilter

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / "HyperPuzzle2D/Assets/Resources/Art"
CHAR = ART / "Chars/Maiden"
OUTFIT_DIR = CHAR / "Outfits/Teal"
CLOTHES = Path(
    "/Users/a/.cursor/projects/Users-a-Develop-project-unity/assets/maiden_clothes_only.png"
)
GEN = Path(
    "/Users/a/.cursor/projects/Users-a-Develop-project-unity/assets/maiden_base_single.png"
)
SRC = ART / "PaperMaiden.png"
CENTRE = 510
SHOULDER = 327


def near(c, r, t=1600):
    return (c[0] - r[0]) ** 2 + (c[1] - r[1]) ** 2 + (c[2] - r[2]) ** 2 <= t


def build_fig(px, w, h, bg):
    bgm = [[False] * w for _ in range(h)]
    q = deque()

    def seed(x, y):
        if not bgm[y][x] and near(px[x, y], bg, 1600):
            bgm[y][x] = True
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
            if 0 <= yy < h and 0 <= xx < w and not bgm[yy][xx]:
                c = px[xx, yy]
                if near(c, bg, 1600) or (near(c, px[x, y], 1000) and near(c, bg, 3200)):
                    bgm[yy][xx] = True
                    q.append((xx, yy))
    return [[(not bgm[y][x]) and px[x, y][3] > 8 for x in range(w)] for y in range(h)]


def rebuild_base(w, h, opw, fig, bg):
    gen = Image.open(GEN).convert("RGBA")
    gp0 = gen.load()
    gw, gh = gen.size
    avg = tuple(
        sum(gp0[x, y][i] for x, y in ((2, 2), (gw - 3, 2), (2, gh - 3), (gw - 3, gh - 3))) // 4
        for i in range(3)
    )
    bgm = [[False] * gw for _ in range(gh)]
    q = deque()

    def seed(x, y):
        if not bgm[y][x] and near(gp0[x, y], avg, 2800):
            bgm[y][x] = True
            q.append((x, y))

    for x in range(gw):
        seed(x, 0)
        seed(x, gh - 1)
    for y in range(gh):
        seed(0, y)
        seed(gw - 1, y)
    while q:
        x, y = q.popleft()
        for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1)):
            yy, xx = y + dy, x + dx
            if 0 <= yy < gh and 0 <= xx < gw and not bgm[yy][xx]:
                if near(gp0[xx, yy], avg, 3200) or (
                    near(gp0[xx, yy], gp0[x, y], 1400) and near(gp0[xx, yy], avg, 5000)
                ):
                    bgm[yy][xx] = True
                    q.append((xx, yy))
    gen2 = Image.new("RGBA", (gw, gh), (0, 0, 0, 0))
    g2 = gen2.load()
    for y in range(gh):
        for x in range(gw):
            if not bgm[y][x] and gp0[x, y][3] > 8:
                g2[x, y] = gp0[x, y]
    gbb = gen2.split()[-1].getbbox()
    gc = gen2.crop(gbb)
    top = next(y for y in range(h) if any(fig[y][x] for x in range(w)))
    bot = next(y for y in range(h - 1, -1, -1) if any(fig[y][x] for x in range(w)))
    scale = (bot - top) / gc.size[1] * 0.97
    nw, nh = int(gc.size[0] * scale), int(gc.size[1] * scale)
    gs = gc.resize((nw, nh), Image.LANCZOS)
    base = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    base.alpha_composite(gs, (CENTRE - nw // 2, top - 2))
    head = Image.new("L", (w, h), 0)
    hp = head.load()
    q = deque()
    seen = [[False] * w for _ in range(h)]
    for y in range(0, SHOULDER + 6):
        for x in range(w):
            if fig[y][x]:
                hp[x, y] = 255
                seen[y][x] = True
                q.append((x, y))
    while q:
        x, y = q.popleft()
        for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1)):
            yy, xx = y + dy, x + dx
            if not (0 <= yy < h and 0 <= xx < w) or seen[yy][xx] or not fig[yy][xx]:
                continue
            if yy > SHOULDER + 200:
                continue
            c = opw[xx, yy]
            hair = c[0] > 185 and c[1] > 155 and c[2] > 110 and c[1] < c[0] + 25
            if hair and (abs(xx - CENTRE) > 70 or yy < SHOULDER + 50):
                seen[yy][xx] = True
                hp[xx, yy] = 255
                q.append((xx, yy))
    hf = head.filter(ImageFilter.GaussianBlur(1.2)).load()
    bp0 = base.load()
    for y in range(0, SHOULDER + 40):
        for x in range(max(0, CENTRE - 130), min(w, CENTRE + 131)):
            if hf[x, y] > 40 and abs(x - CENTRE) < 110:
                bp0[x, y] = (0, 0, 0, 0)
    head_rgba = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    hr = head_rgba.load()
    for y in range(h):
        for x in range(w):
            a = hf[x, y]
            if a > 0 and fig[y][x]:
                r, g, b, _ = opw[x, y]
                hr[x, y] = (r, g, b, a)
    return Image.alpha_composite(base, head_rgba)


def extract_clothes(path: Path, w, h):
    raw = Image.open(path).convert("RGBA")
    rp = raw.load()
    avg_bg = tuple(
        sum(rp[x, y][i] for x, y in ((2, 2), (w - 3, 2), (2, h - 3), (w - 3, h - 3))) // 4
        for i in range(3)
    )
    bgm = [[False] * w for _ in range(h)]
    q = deque()

    def seed(x, y):
        if not bgm[y][x] and near(rp[x, y], avg_bg, 2800):
            bgm[y][x] = True
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
            if 0 <= yy < h and 0 <= xx < w and not bgm[yy][xx]:
                if near(rp[xx, yy], avg_bg, 3200) or (
                    near(rp[xx, yy], rp[x, y], 900) and near(rp[xx, yy], avg_bg, 5000)
                ):
                    bgm[yy][xx] = True
                    q.append((xx, yy))
    cut = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    cp = cut.load()
    for y in range(h):
        for x in range(w):
            if not bgm[y][x]:
                cp[x, y] = rp[x, y]
    return cut.crop(cut.split()[-1].getbbox()), avg_bg


def main():
    orig = Image.open(SRC).convert("RGBA")
    w, h = orig.size
    opw = orig.load()
    fig = build_fig(opw, w, h, opw[2, 2])
    base = rebuild_base(w, h, opw, fig, opw[2, 2])
    base.save(CHAR / "Base.png")
    bp = base.load()

    cc, avg_bg = extract_clothes(CLOTHES, w, h)
    # Base is slimmer than the paper-art clothes; shrink to ~78% width / 82% height.
    orig_o = Image.open(OUTFIT_DIR / "Outfit.png").convert("RGBA")
    obb = orig_o.split()[-1].getbbox()
    target_w = int((obb[2] - obb[0]) * 0.78)
    target_h = int(cc.size[1] * (target_w / cc.size[0]) * (0.82 / 0.78))
    cs = cc.resize((target_w, target_h), Image.LANCZOS)
    x0 = CENTRE - target_w // 2
    y0 = SHOULDER
    outfit = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    outfit.alpha_composite(cs, (x0, y0))
    op = outfit.load()
    for y in range(h):
        for x in range(w):
            if op[x, y][3] < 30 or near(op[x, y], avg_bg, 2000):
                op[x, y] = (0, 0, 0, 0)
    # keep face clear
    for y in range(0, SHOULDER - 15):
        for x in range(CENTRE - 90, CENTRE + 91):
            if op[x, y][3] > 40:
                op[x, y] = (0, 0, 0, 0)

    outfit.save(OUTFIT_DIR / "Outfit.png")
    mask = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    mp = mask.load()
    for y in range(h):
        for x in range(w):
            if op[x, y][3] > 40:
                mp[x, y] = (255, 255, 255, 255)
    mask.save(OUTFIT_DIR / "Mask.png")

    line = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    lp = line.load()
    ink = (42, 32, 28, 255)
    for y in range(1, h - 1):
        for x in range(1, w - 1):
            if bp[x, y][3] > 50 and (
                bp[x - 1, y][3] < 40
                or bp[x + 1, y][3] < 40
                or bp[x, y - 1][3] < 40
                or bp[x, y + 1][3] < 40
            ):
                lp[x, y] = ink
    line.filter(ImageFilter.MaxFilter(3)).save(CHAR / "Line.png")
    print("wrote Base / Outfit / Mask / Line")


if __name__ == "__main__":
    main()
