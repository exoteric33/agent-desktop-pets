"""
Builds the ClaudePet sprite atlas from art/source.png.

Pipeline: cut the character out of the white background -> paint away the shirt
lettering -> punch out enclosed background pockets -> 3x line-preserving
downscale -> 20-colour k-means palette -> outline -> face variants (normal,
blink, happy) -> animation frames (hair wave, breathing, ahoge, bounce) ->
mirrored set -> effect sprites (sparkles, Zzz, speech bubble).

Outputs in art/build/: atlas.png, atlas.txt, icon.ico, preview_*.png
Run:  python art/make_sprites.py
"""
import math
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image

HERE = Path(__file__).resolve().parent
OUT = HERE / "build"

FACTOR = 3          # source pixels per sprite pixel
PALETTE_SIZE = 20
OUTLINE = (46, 22, 20)
FRAME_W, FRAME_H = 72, 122   # character cell; base sprite sits at (2, 4)
BASE_X, BASE_Y = 2, 4
PHASES = 8


# ----------------------------------------------------------------- source prep

def flood(mask_ok, seeds, conn8=False):
    h, w = mask_ok.shape
    hit = np.zeros_like(mask_ok)
    q = deque()
    for y, x in seeds:
        if mask_ok[y, x] and not hit[y, x]:
            hit[y, x] = True
            q.append((y, x))
    steps = [(1, 0), (-1, 0), (0, 1), (0, -1)]
    if conn8:
        steps += [(1, 1), (1, -1), (-1, 1), (-1, -1)]
    while q:
        y, x = q.popleft()
        for dy, dx in steps:
            ny, nx = y + dy, x + dx
            if 0 <= ny < h and 0 <= nx < w and mask_ok[ny, nx] and not hit[ny, nx]:
                hit[ny, nx] = True
                q.append((ny, nx))
    return hit


def cut_out(rgb):
    h, w, _ = rgb.shape
    dist = np.abs(rgb - 253).max(axis=2)
    border = [(y, x) for x in range(w) for y in (0, h - 1)] + [(y, x) for y in range(h) for x in (0, w - 1)]
    fg = ~flood(dist <= 14, border)

    # keep the largest blob: drops the "^" and "<" UI marks around the drawing
    best = None
    seen = np.zeros_like(fg)
    for y in range(h):
        for x in range(w):
            if fg[y, x] and not seen[y, x]:
                blob = flood(fg, [(y, x)], conn8=True)
                seen |= blob
                if best is None or blob.sum() > best.sum():
                    best = blob
    fg = best

    # background visible through gaps (between hair strands, arm and waist),
    # grown by a pixel so their anti-aliased rims go too
    pockets = [(252, 190), (114, 58), (128, 211), (160, 43), (243, 193)]
    hole = flood(fg & (dist <= 20), pockets)
    grown = hole.copy()
    grown[1:] |= hole[:-1]; grown[:-1] |= hole[1:]; grown[:, 1:] |= hole[:, :-1]; grown[:, :-1] |= hole[:, 1:]
    return fg & ~grown


def remove_lettering(rgb):
    """Diffuse the shirt colour into 'Fable 5.1', the sleeve text and the watermark."""
    lum = rgb @ [0.299, 0.587, 0.114]
    sat = rgb.max(axis=2) - rgb.min(axis=2)
    boxes = [(99, 159, 162, 180, 232), (181, 153, 205, 167, 228), (130, 198, 172, 216, 222)]
    mask = np.zeros(lum.shape, bool)
    inbox = np.zeros(lum.shape, bool)
    for x0, y0, x1, y1, thr in boxes:
        mask[y0:y1, x0:x1] |= (lum[y0:y1, x0:x1] < thr) & (sat[y0:y1, x0:x1] < 45)
        inbox[y0:y1, x0:x1] = True
    grown = mask.copy()
    grown[1:] |= mask[:-1]; grown[:-1] |= mask[1:]; grown[:, 1:] |= mask[:, :-1]; grown[:, :-1] |= mask[:, 1:]
    mask |= grown & inbox & (sat < 45)

    fill = rgb.copy()
    known = ~mask
    h, w = mask.shape
    while not known.all():
        acc = np.zeros_like(rgb)
        cnt = np.zeros((h, w))
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                if dy or dx:
                    k = np.roll(np.roll(known, dy, 0), dx, 1)
                    acc += np.roll(np.roll(fill, dy, 0), dx, 1) * k[..., None]
                    cnt += k
        ready = ~known & (cnt >= 3)
        fill[ready] = acc[ready] / cnt[ready][:, None]
        known |= ready
    for _ in range(6):
        blur = sum(np.roll(np.roll(fill, dy, 0), dx, 1) for dy in (-1, 0, 1) for dx in (-1, 0, 1)) / 9
        fill[mask] = blur[mask]
    return fill


# ----------------------------------------------------------------- pixelisation

def srgb_to_lab(c):
    c = c / 255.0
    c = np.where(c > 0.04045, ((c + 0.055) / 1.055) ** 2.4, c / 12.92)
    m = np.array([[0.4124, 0.3576, 0.1805], [0.2126, 0.7152, 0.0722], [0.0193, 0.1192, 0.9505]])
    xyz = c @ m.T / [0.95047, 1.0, 1.08883]
    f = np.where(xyz > 0.008856, np.cbrt(xyz), 7.787 * xyz + 16 / 116)
    return np.stack([116 * f[..., 1] - 16, 500 * (f[..., 0] - f[..., 1]), 200 * (f[..., 1] - f[..., 2])], -1)


def kmeans(data, k, iters=40, seed=3):
    rng = np.random.default_rng(seed)
    cent = [data[rng.integers(len(data))]]
    for _ in range(1, k):
        d = np.min([((data - c) ** 2).sum(1) for c in cent], axis=0)
        cent.append(data[rng.choice(len(data), p=d / d.sum())])
    cent = np.array(cent)
    for _ in range(iters):
        lab = ((data[:, None, :] - cent[None]) ** 2).sum(2).argmin(1)
        for j in range(k):
            if (lab == j).any():
                cent[j] = data[lab == j].mean(0)
    return lab


def pixelise(rgb, fg):
    er = fg.copy()   # erode: the outer ring still carries white anti-aliasing
    er[1:] &= fg[:-1]; er[:-1] &= fg[1:]; er[:, 1:] &= fg[:, :-1]; er[:, :-1] &= fg[:, 1:]
    ys, xs = np.where(er)
    x0, x1, y0, y1 = xs.min(), xs.max() + 1, ys.min(), ys.max() + 1
    lum_src = rgb @ [0.299, 0.587, 0.114]
    w, h = -(-(x1 - x0) // FACTOR), -(-(y1 - y0) // FACTOR)
    col = np.zeros((h, w, 3))
    cov = np.zeros((h, w))
    for j in range(h):
        for i in range(w):
            sy, sx = y0 + j * FACTOR, x0 + i * FACTOR
            m = er[sy:sy + FACTOR, sx:sx + FACTOR]
            cov[j, i] = m.sum() / FACTOR ** 2
            if not m.any():
                continue
            px = rgb[sy:sy + FACTOR, sx:sx + FACTOR][m]
            lum = lum_src[sy:sy + FACTOR, sx:sx + FACTOR][m]
            c = px.mean(0)
            dark = lum < min(lum.mean() - 45, 110)   # keep thin line art alive
            if dark.mean() >= 0.22:
                c = px[dark].mean(0)
            col[j, i] = c
    alpha = cov >= 0.45
    col = np.pad(col, ((1, 1), (1, 1), (0, 0)))
    alpha = np.pad(alpha, 1)
    h, w = alpha.shape

    lab = kmeans(srgb_to_lab(col[alpha]), PALETTE_SIZE)
    pal = np.array([col[alpha][lab == j].mean(0) for j in range(PALETTE_SIZE)]).round().clip(0, 255)
    idx = np.full((h, w), -1)
    idx[alpha] = lab

    # merge lone specks into a similar neighbour colour
    for _ in range(2):
        new = idx.copy()
        for y in range(1, h - 1):
            for x in range(1, w - 1):
                v = idx[y, x]
                if v < 0:
                    continue
                nb = [idx[y + dy, x + dx] for dy in (-1, 0, 1) for dx in (-1, 0, 1) if dy or dx]
                if v in nb:
                    continue
                vals, cnt = np.unique([n for n in nb if n >= 0], return_counts=True)
                if len(cnt) and cnt.max() >= 5 and np.abs(pal[vals[cnt.argmax()]] - pal[v]).max() < 60:
                    new[y, x] = vals[cnt.argmax()]
        idx = new

    img = np.zeros((h, w, 4), np.uint8)
    op = idx >= 0
    img[op, :3] = pal[idx[op]]
    img[op, 3] = 255
    ring = np.zeros_like(op)
    ring[1:] |= op[:-1]; ring[:-1] |= op[1:]; ring[:, 1:] |= op[:, :-1]; ring[:, :-1] |= op[:, 1:]
    ring &= ~op
    # the drawing is cut at the thighs: no outline along the bottom edge
    last = np.where(op.any(axis=1))[0].max()
    ring[last + 1:] = False
    img[ring] = OUTLINE + (255,)
    return img[: last + 1], pal


# ----------------------------------------------------------------- face variants

COLORS = {
    "d": (42, 21, 15),     # lash / line
    "l": (91, 47, 39),     # softer line
    "b": (129, 80, 66),    # mouth line
    "s": (250, 217, 197),  # skin
    "h": (229, 181, 163),  # skin shade / blush
    "g": (207, 182, 169),  # skin shadow
    "p": (238, 150, 140),  # happy blush
    "r": (178, 72, 58),    # open mouth
    "w": (248, 238, 237),  # eye white / highlight
    "o": (217, 119, 87),   # Claude orange (shirt logo)
}


def patch(img, x0, y0, rows):
    out = img.copy()
    for j, row in enumerate(rows):
        for i, ch in enumerate(row):
            if ch != ".":
                out[y0 + j, x0 + i] = COLORS[ch] + (255,)
    return out


LOGO_CENTER = (33, 53)   # base sprite coordinates


def shirt_logo(base):
    """The downscale leaves only a pale 4px smudge of the asterisk; draw a proper 7x7 one."""
    img = base.copy()
    img[51:55, 31:35] = base[57, 33]   # paint the smudge over with plain shirt white
    cx, cy = LOGO_CENTER
    return patch(img, cx - 3, cy - 3, [
        "...o...",
        ".o.o.o.",
        "..ooo..",
        "ooooooo",
        "..ooo..",
        ".o.o.o.",
        "...o...",
    ])


def face_normal(base):
    img = patch(base, 36, 25, ["w"])                 # catch-light in the left iris
    return patch(img, 39, 33, ["b...b", ".bbb."])    # the little smug smile


def face_blink(base):
    img = patch(base, 30, 24, [
        "lhhhhhh",
        ".sssssss",
        "dds...sd",
        ".sddddds",
        "..hhhhh",
    ])
    img = patch(img, 44, 27, [
        "ssssss",
        "ssssss",
        "dsssssd",
        "sdddds",
        ".hhhh",
    ])
    return patch(img, 39, 33, ["b...b", ".bbb."])


def face_happy(base):
    img = patch(base, 30, 24, [
        "lhhhhhh",
        ".ssdddss",
        ".sdsssds",
        ".dsssssd",
        "..hpphh",
    ])
    img = patch(img, 44, 27, [
        "ssssss",
        "ssddds",
        "sdsssd",
        "dsssss.",
        ".hpph",
    ])
    img = patch(img, 32, 29, ["pp"])
    return patch(img, 39, 33, [".lll.", ".lrl.", "..l.."])


# ----------------------------------------------------------------- animation

def light(px):
    return px[3] > 0 and tuple(px[:3]) != OUTLINE and px[:3] @ [0.299, 0.587, 0.114] >= 160


def hair_span(img, y, side):
    """Horizontal run of loose hair on one side of the body in row y."""
    xs = np.where(img[y, :, 3] > 0)[0]
    if len(xs) == 0:
        return None
    w = img.shape[1]
    if side == "left":
        start, step, cap = xs.min(), 1, (15 if y >= 76 else w // 2)
    else:
        start, step, cap = xs.max(), -1, w // 2
    x = start
    while 0 <= x + step < w and (x + step <= cap if step > 0 else x + step >= cap):
        if light(img[y, x + step]) and 0 <= x + 2 * step < w and light(img[y, x + 2 * step]):
            break
        x += step
    return (start, x) if side == "left" else (x, start)


def shift_span(src, dst, y, a, b, s, side):
    if s == 0 or b - a < 2:
        return
    row = src[y].copy()
    if s < 0:   # move left
        lo = max(a - 1, 0)
        dst[y, lo:b] = row[lo + 1:b + 1]
        if side == "right":
            dst[y, b] = 0
    else:       # move right
        hi = min(b + 1, dst.shape[1] - 1)
        dst[y, a + 1:hi + 1] = row[a:hi]
        if side == "left":
            dst[y, a] = 0


def hair_wave(img, phase):
    out = img.copy()
    for side, y_start, y_full, y_end in (("left", 46, 66, 86), ("right", 44, 62, 73)):
        for y in range(y_start, min(y_end, img.shape[0])):
            amp = 1.25 * min(1.0, (y - y_start) / (y_full - y_start))
            s = math.floor(amp * math.sin(2 * math.pi * phase / PHASES - 0.16 * (y - 44)) + 0.5)
            span = hair_span(img, y, side)
            if span:
                shift_span(img, out, y, span[0], span[1], s, side)
    return out


def wiggle_ahoge(img, s):
    if s == 0:
        return img
    out = img.copy()
    for y in (0, 1):
        seg = img[y, 36:50].copy()
        out[y, 36:50] = 0
        out[y, 36 + s:50 + s] = seg
    return out


def restretch(img, dup=(), drop=()):
    rows = list(range(img.shape[0]))
    for r in sorted(drop, reverse=True):
        rows.remove(r)
    for r in sorted(dup, reverse=True):
        rows.insert(rows.index(r), r)
    return img[rows]


def to_cell(img):
    cell = np.zeros((FRAME_H, FRAME_W, 4), np.uint8)
    h, w = img.shape[:2]
    cell[FRAME_H - h:, BASE_X:BASE_X + w] = img[max(0, h - FRAME_H):]
    return cell


BREATH = [0, 0, 1, 1, 1, 1, 0, 0]
AHOGE = [0, 0, 0, 1, 1, 0, 0, -1]
BREATH_ROW = 62
STRETCH_ROWS = [62, 96, 110]


def idle_frame(face, phase):
    img = hair_wave(face, phase)
    img = wiggle_ahoge(img, AHOGE[phase])
    return to_cell(restretch(img, dup=[BREATH_ROW] if BREATH[phase] else []))


def bounce_frame(face, n):
    if n >= 0:
        return to_cell(restretch(face, dup=STRETCH_ROWS[:n]))
    return to_cell(restretch(face, drop=STRETCH_ROWS[:-n]))


def mirror(cell):
    m = cell[:, ::-1].copy()
    # re-flip the hair clip so its "A\" logo does not read backwards
    y0, y1 = BASE_Y + 13, BASE_Y + 18
    x0, x1 = BASE_X + 26, BASE_X + 32
    mx0, mx1 = FRAME_W - x1, FRAME_W - x0
    m[y0:y1, mx0:mx1] = cell[y0:y1, x0:x1]
    return m


# ----------------------------------------------------------------- effects

ORANGE = (217, 119, 87)
ORANGE_LIGHT = (242, 176, 150)
CREAM = (250, 249, 245)
CURSOR = (74, 69, 65)
ZCOL = (236, 242, 255)
GREEN = (84, 158, 92)


def grid_sprite(rows, colors):
    h, w = len(rows), max(len(r) for r in rows)
    img = np.zeros((h, w, 4), np.uint8)
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch in colors:
                img[y, x] = colors[ch] + (255,)
    return img


def shadowed(img):
    """1px drop shadow down-right: keeps tiny glyphs readable on light and dark desktops."""
    h, w = img.shape[:2]
    out = np.zeros((h + 1, w + 1, 4), np.uint8)
    op = img[..., 3] > 0
    out[1:, 1:][op] = OUTLINE + (255,)
    out[:h, :w][op] = img[op]
    return out


def effects():
    sc = {"o": ORANGE, "c": ORANGE_LIGHT}
    z = {"z": ZCOL}
    sprites = {
        "spark3": grid_sprite(["o..o..o", ".o.o.o.", "..oco..", "ooocooo", "..oco..", ".o.o.o.", "o..o..o"], sc),
        "spark2": grid_sprite(["o.o.o", ".oco.", "occco", ".oco.", "o.o.o"], sc),
        "spark1": grid_sprite([".o.", "oco", ".o."], sc),
        "spark0": grid_sprite(["o"], sc),
        "z2": shadowed(grid_sprite(["zzzzz", "...z.", "..z..", ".z...", "zzzzz"], z)),
        "z1": shadowed(grid_sprite(["zzzz", "..z.", ".z..", "zzzz"], z)),
        "z0": shadowed(grid_sprite(["zzz", ".z.", "zzz"], z)),
    }
    anchors = {}
    for side in ("r", "l"):
        for name, content in bubble_contents().items():
            img, tip = bubble(side, content)
            sprites["bubble_%s_%s" % (side, name)] = img
            anchors["bubble_%s_%s" % (side, name)] = tip
    return sprites, anchors


def bubble_contents():
    """What the bubble can say: '>_' prompt (hover), Claude spinner (working), '?' (needs input), check (done)."""
    o = {"o": ORANGE, "c": CURSOR}
    prompt = ["oo..........", ".oo.........", "..oo........", "...oo.......", "..oo........", ".oo....ccccc", "oo.....ccccc"]
    contents = {
        "on": grid_sprite(prompt, o),
        "off": grid_sprite([row.replace("c", ".") for row in prompt], o),
        # pulses like the Claude Code spinner (· ✢ ✳ ✶ ✻)
        "spin0": grid_sprite(["o"], o),
        "spin1": grid_sprite([".o.", "ooo", ".o."], o),
        "spin2": grid_sprite(["..o..", "..o..", "ooooo", "..o..", "..o.."], o),
        "spin3": grid_sprite(["...o...", ".o.o.o.", "..ooo..", "ooooooo", "..ooo..", ".o.o.o.", "...o..."], o),
        "spin4": grid_sprite(["o..o..o", ".o.o.o.", "..ooo..", "ooooooo", "..ooo..", ".o.o.o.", "o..o..o"], o),
        "wait": grid_sprite([".ooo.", "oo.oo", "...oo", "..oo.", "..oo.", ".....", "..oo."], o),
        "done": grid_sprite(["......g", ".....gg", "g...gg.", "gg.gg..", ".ggg...", "..g...."], {"g": GREEN}),
    }
    return contents


def bubble(side, content):
    """Speech bubble with content centred inside; tail points down towards the head."""
    w, h = 22, 17
    body_h = 13
    img = np.zeros((h, w, 4), np.uint8)
    bx0, bx1 = 2, w - 1          # body columns incl. outline (tail on the left)
    for y in range(body_h):
        for x in range(bx0, bx1):
            edge = y in (0, body_h - 1) or x in (bx0, bx1 - 1)
            corner = (y in (0, body_h - 1)) and (x in (bx0, bx1 - 1))
            if corner:
                continue
            img[y, x] = (OUTLINE if edge else CREAM) + (255,)
    for (x, y) in ((bx0 + 1, 1), (bx1 - 2, 1), (bx0 + 1, body_h - 2), (bx1 - 2, body_h - 2)):
        img[y, x] = OUTLINE + (255,)   # rounded corners
    # tail: opening in the bottom edge, then a stair down to the tip
    for x, y, c in ((4, 12, CREAM), (5, 12, CREAM), (3, 13, OUTLINE), (4, 13, CREAM), (5, 13, CREAM),
                    (6, 13, OUTLINE), (2, 14, OUTLINE), (3, 14, CREAM), (4, 14, OUTLINE),
                    (1, 15, OUTLINE), (2, 15, OUTLINE), (0, 16, OUTLINE)):
        img[y, x] = c + (255,)
    tip = (0, 16)
    if side == "l":
        img = img[:, ::-1].copy()
        tip = (w - 1, 16)
    inner_x = (bx0 + 1) if side == "r" else (w - 1 - (bx1 - 1)) + 1
    inner_w, inner_h = bx1 - bx0 - 2, body_h - 2
    ch, cw = content.shape[:2]
    x0, y0 = inner_x + (inner_w - cw) // 2, 1 + (inner_h - ch) // 2
    solid = content[..., 3] > 0
    img[y0:y0 + ch, x0:x0 + cw][solid] = content[solid]
    return img, tip


# ----------------------------------------------------------------- assembly

def build():
    OUT.mkdir(exist_ok=True)
    rgb = np.array(Image.open(HERE / "source.png").convert("RGB")).astype(np.float64)
    fg = cut_out(rgb)
    rgb = remove_lettering(rgb)
    base, _ = pixelise(rgb, fg)
    base = shirt_logo(base)
    print("base sprite", base.shape[1], "x", base.shape[0])
    assert base.shape[1] + 2 * BASE_X <= FRAME_W and base.shape[0] + BASE_Y <= FRAME_H

    faces = {"normal": face_normal(base), "blink": face_blink(base), "happy": face_happy(base)}
    frames = {}
    for name, face in faces.items():
        for p in range(PHASES):
            frames["%s_%d" % (name, p)] = idle_frame(face, p)
    for n in (-2, -1, 1, 2, 3):
        frames["bounce_%d" % n] = bounce_frame(faces["happy"], n)
    for name in list(frames):
        frames["m_" + name] = mirror(frames[name])

    sprites, sprite_tips = effects()

    cols = 8
    names = list(frames)
    frame_rows = -(-len(names) // cols)
    fx_w = sum(s.shape[1] + 1 for s in sprites.values())
    fx_h = max(s.shape[0] for s in sprites.values())
    atlas_w = max(cols * FRAME_W, fx_w)
    atlas = np.zeros((frame_rows * FRAME_H + fx_h, atlas_w, 4), np.uint8)
    lines = ["cell %d %d" % (FRAME_W, FRAME_H)]
    for i, name in enumerate(names):
        x, y = (i % cols) * FRAME_W, (i // cols) * FRAME_H
        atlas[y:y + FRAME_H, x:x + FRAME_W] = frames[name]
        lines.append("frame %s %d %d" % (name, x, y))
    x, y = 0, frame_rows * FRAME_H
    for name, spr in sprites.items():
        h, w = spr.shape[:2]
        atlas[y:y + h, x:x + w] = spr
        tip = sprite_tips.get(name, (w // 2, h // 2))
        lines.append("sprite %s %d %d %d %d %d %d" % (name, x, y, w, h, tip[0], tip[1]))
        x += w + 1
    # anchors in cell coordinates of the unmirrored frames
    anchors = {"bubble": (BASE_X + 56, BASE_Y + 9), "zzz": (BASE_X + 47, BASE_Y + 3),
               "logo": (BASE_X + LOGO_CENTER[0], BASE_Y + LOGO_CENTER[1]), "head": (BASE_X + 40, BASE_Y + 20)}
    for name, (ax, ay) in anchors.items():
        lines.append("anchor %s %d %d" % (name, ax, ay))

    Image.fromarray(atlas, "RGBA").save(OUT / "atlas.png", optimize=True)
    (OUT / "atlas.txt").write_text("\n".join(lines) + "\n", encoding="ascii")
    make_icon(base)
    previews(base, faces, frames, sprites)
    print("atlas", atlas.shape[1], "x", atlas.shape[0], "frames", len(names))


def make_icon(base):
    head = base[0:44, 17:61]
    sizes = [256, 64, 48, 32, 24, 16]
    imgs = []
    for s in sizes:
        k = max(1, s // head.shape[0])
        big = Image.fromarray(head, "RGBA").resize((head.shape[1] * k, head.shape[0] * k), Image.NEAREST)
        if big.width > s:
            big = Image.fromarray(head, "RGBA").resize((s, s), Image.LANCZOS)
        canvas = Image.new("RGBA", (s, s))
        canvas.alpha_composite(big, ((s - big.width) // 2, (s - big.height) // 2))
        imgs.append(canvas)
    imgs[0].save(OUT / "icon.ico", format="ICO", sizes=[(s, s) for s in sizes], append_images=imgs[1:])


def on_bg(img, bg, scale):
    im = Image.fromarray(img, "RGBA")
    canvas = Image.new("RGBA", im.size, bg + (255,))
    canvas.alpha_composite(im)
    return canvas.convert("RGB").resize((im.width * scale, im.height * scale), Image.NEAREST)


def strip(tiles, gap=6, bg=(24, 24, 24)):
    w = sum(t.width for t in tiles) + gap * (len(tiles) - 1)
    h = max(t.height for t in tiles)
    sheet = Image.new("RGB", (w, h), bg)
    x = 0
    for t in tiles:
        sheet.paste(t, (x, 0))
        x += t.width + gap
    return sheet


def previews(base, faces, frames, sprites):
    dark = (58, 74, 92)
    face_box = (22, 14, 58, 42)
    tiles = [on_bg(f[face_box[1]:face_box[3], face_box[0]:face_box[2]], dark, 10) for f in faces.values()]
    strip(tiles).save(OUT / "preview_faces.png")
    strip([on_bg(frames["normal_%d" % p], dark, 3) for p in range(PHASES)]).save(OUT / "preview_idle.png")
    strip([on_bg(frames[n], dark, 3) for n in ("bounce_-2", "bounce_-1", "happy_0", "bounce_1", "bounce_2", "bounce_3", "m_normal_0", "m_happy_0")]).save(OUT / "preview_bounce.png")
    strip([on_bg(s, (200, 205, 210), 8) for s in sprites.values()], bg=(90, 90, 90)).save(OUT / "preview_fx.png")


if __name__ == "__main__":
    build()
