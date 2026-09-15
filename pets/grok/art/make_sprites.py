"""
Builds the Grok pet's sprite atlas from source.png (Grok-chan winking with a peace sign).

Pipeline: cut the character out of the white background, including the many
gaps between her curls -> paint the shirt lettering "grok" over with plain shirt
black -> shrink to 0.55 (the drawing is big; this keeps her the size of the
previous Grok) -> 3x line-preserving downscale -> 28-colour k-means palette ->
outline -> faces (normal with both eyes open, blink, look, hover = the wink from
the drawing, happy, sleep) -> animation frames (breathing, swaying hair tips,
ahoge, the peace sign bobbing on hover and click, bounce) -> mirrored set ->
xAI logo on the shirt -> effect sprites (sparks, Zzz, speech bubble with an X
prompt and a turning-line spinner).

Outputs: ../sprites/atlas.png, atlas.txt, icon.ico and preview/*.png
Run:  python pets/grok/art/make_sprites.py   (or build.ps1 -Art)
"""
import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

sys.path.insert(0, str(Path(__file__).resolve().parents[3] / "art"))
import pixelkit as kit  # noqa: E402

HERE, SPRITES, PREVIEW = kit.pet_dirs(__file__)

ENLARGE = 0.55      # 1024 x 900 is much bigger than the other drawings
FACTOR = 3          # (resized) source pixels per sprite pixel
PALETTE_SIZE = 28
OUTLINE = (16, 14, 18)
FRAME_W, FRAME_H = 122, 166  # character cell; base sprite (118 x 162) sits at (2, 4)
BASE_X, BASE_Y = 2, 4
PHASES = 8


# ----------------------------------------------------------------- source prep

# white areas inside the figure that are not background: hair streak, face (eye, teeth), shirt lettering, hair clip
KEEP_WHITE = [(378, 70, 446, 268), (420, 150, 600, 300), (430, 385, 575, 435), (515, 95, 580, 155)]


def cut_out(rgb):
    h, w, _ = rgb.shape
    dist = np.abs(rgb - 255).max(axis=2)
    fg = kit.largest_blob(~kit.flood(dist <= 16, kit.border_seeds(h, w)))

    # her curls enclose lots of background: every pure white patch outside KEEP_WHITE is a hole
    white = fg & (dist <= 10)
    keep = np.zeros_like(white)
    for x0, y0, x1, y1 in KEEP_WHITE:
        keep[y0:y1, x0:x1] = True
    seen = np.zeros_like(white)
    holes = np.zeros_like(white)
    for y, x in zip(*np.where(white & ~keep)):
        if seen[y, x]:
            continue
        blob = kit.flood(white, [(y, x)])
        seen |= blob
        if blob.sum() >= 12 and not (blob & keep).any():
            holes |= blob
    return fg & ~kit.grow(holes)


def remove_lettering(rgb):
    """Paint the white 'grok' over with the plain shirt black of the rows around it (diffusing leaves a ghost)."""
    y0, y1, x0, x1 = 386, 436, 432, 574
    shirt = np.concatenate([rgb[372:384, x0:x1].reshape(-1, 3), rgb[438:450, x0:x1].reshape(-1, 3)])
    shirt = np.median(shirt[shirt @ [0.299, 0.587, 0.114] < 90], axis=0)
    fill = rgb.copy()
    fill[y0:y1, x0:x1] = shirt
    return fill


def resize(rgb, fg):
    h, w = fg.shape
    size = (round(w * ENLARGE), round(h * ENLARGE))
    small = Image.fromarray(rgb.clip(0, 255).round().astype(np.uint8)).resize(size, Image.LANCZOS)
    mask = Image.fromarray(fg.astype(np.uint8) * 255).resize(size, Image.BILINEAR)
    return np.array(small).astype(np.float64), np.array(mask) >= 128


# ----------------------------------------------------------------- faces

COLORS = {
    "a": (8, 6, 7),        # line
    "d": (27, 24, 27),     # iris dark
    "m": (61, 58, 61),     # iris
    "q": (93, 83, 85),     # iris light
    "B": (238, 236, 237),  # highlight
    "z": (253, 220, 204),  # skin
    "y": (249, 205, 187),  # skin shade
    "x": (246, 188, 169),  # skin shadow / blush
    "p": (240, 150, 150),  # happy blush
    "k": (87, 39, 32),     # mouth line
}


def patch(img, x0, y0, rows):
    return kit.patch(img, x0, y0, rows, COLORS)


# the drawing: her left eye (left in the picture) open at x 37..45, y 36..39; her right eye winks at x 54..61, y 31..33;
# the open mouth x 48..55, y 42..47
LEFT_EYE, RIGHT_EYE, MOUTH_AT = (37, 36), (54, 30), (47, 42)
LEFT_SKIN = [".zzzzzzz."] * 4   # the outer columns belong to the hair and the lash tip
RIGHT_SKIN = ["........", "zzzzzzzz", "zzzzzzzz", "zzzzzzzz"]
MOUTH_SKIN = [".zzzzzzzz.", "zzzzzzzzzz", "zzzzzzzzzz", "zzzzzzzzzz", ".zzzzzzzz.", "..zzzzzz.."]

RIGHT_OPEN = ["......a.", "..aaaaaa", ".aaBmmaa", ".adBqmd.", "..yqqy.."]
LEFT_EYES = {
    "closed": [".........", ".a.....a.", "..aaaaa..", "........."],
    "happy": [".........", "..aaaaa..", ".a.....a.", "........."],
    "asleep": [".........", ".........", ".aaaaaaa.", "..yyyyy.."],
}
RIGHT_EYES = {
    "closed": ["........", "........", "a......a", ".aaaaaa."],
    "happy": ["........", ".aaaaaa.", "a......a", "........"],
    "asleep": ["........", "........", "........", "aaaaaaaa"],
}
MOUTHS = {
    "smile": ["..........", "..........", ".k......k.", "..kkkkkk..", "..........", ".........."],
    "sleepy": ["..........", "..........", "..........", "....kk....", "..........", ".........."],
}


def eyes(img, left=None, right=None):
    if left is not None:
        img = patch(img, *LEFT_EYE, rows=LEFT_SKIN)
        img = patch(img, *LEFT_EYE, rows=left)
    if right is not None:
        img = patch(img, RIGHT_EYE[0], RIGHT_EYE[1], rows=RIGHT_SKIN)
        img = patch(img, *RIGHT_EYE, rows=right)
    return img


def mouth(img, name):
    img = patch(img, *MOUTH_AT, rows=MOUTH_SKIN)
    return patch(img, *MOUTH_AT, rows=MOUTHS[name])


def face_normal(base):
    """Both eyes open (the drawing winks) and a smile."""
    return mouth(eyes(base, right=RIGHT_OPEN), "smile")


def face_blink(base):
    return mouth(eyes(base, LEFT_EYES["closed"], RIGHT_EYES["closed"]), "smile")


def face_look(base):
    """Both eyes open with the laugh from the drawing."""
    return eyes(base, right=RIGHT_OPEN)


def face_happy(base):
    img = eyes(base, LEFT_EYES["happy"], RIGHT_EYES["happy"])
    img = patch(img, 41, 41, ["pp"])
    return patch(img, 57, 35, ["pp"])


def face_sleep(base):
    return mouth(eyes(base, LEFT_EYES["asleep"], RIGHT_EYES["asleep"]), "sleepy")


# ----------------------------------------------------------------- animation

def hairish(px):
    """Her hair is black with white highlights; everything black counts, the span length keeps it to the tips."""
    if px[3] == 0:
        return False
    r, g, b = int(px[0]), int(px[1]), int(px[2])
    lum = 0.299 * r + 0.587 * g + 0.114 * b
    return lum < 80 or (lum > 185 and max(r, g, b) - min(r, g, b) < 25)


def hair_span(img, y, side, reach):
    """Loose hair from the outer edge inwards, at most `reach` pixels (shirt and skirt are black too)."""
    xs = np.where(img[y, :, 3] > 0)[0]
    if len(xs) == 0:
        return None
    w = img.shape[1]
    start, step = (xs.min(), 1) if side == "left" else (xs.max(), -1)
    x = start
    while 0 < x + 2 * step < w - 1 and abs(x + step - start) < reach:
        if not hairish(img[y, x + step]) and not hairish(img[y, x + 2 * step]):
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
    """The hair tips sway: on the left below the peace sign, on the right below the head."""
    out = img.copy()
    for side, y_start, y_full, y_end in (("left", 74, 86, 140), ("right", 44, 58, 140)):
        for y in range(y_start, y_end):
            amp = 1.25 * min(1.0, (y - y_start) / (y_full - y_start))
            s = math.floor(amp * math.sin(2 * math.pi * phase / PHASES - 0.16 * (y - 44)) + 0.5)
            span = hair_span(img, y, side, 9)
            if span:
                shift_span(img, out, y, span[0], span[1], s, side)
    return out


def wiggle_ahoge(img, s):
    """The loose strands on top of her head (rows 0..3) swing sideways."""
    if s == 0:
        return img
    out = img.copy()
    for y in range(0, 4):
        seg = img[y, 30:84].copy()
        out[y, 30:84] = 0
        out[y, 30 + s:84 + s] = seg
    return out


PEACE = (2, 26, 45, 64)          # x from, x to, y from, y to: the fingers and palm of her peace sign
BOB = [0, -1, -1, 0, 0, -1, -1, 0]


def bob_peace(img, phase):
    """Hover and click: the peace sign bobs up, bending at the wrist."""
    s = BOB[phase]
    if s == 0:
        return img
    x0, x1, y0, y1 = PEACE
    out = img.copy()
    block = img[y0:y1, x0:x1].copy()
    out[y0:y1, x0:x1] = 0
    solid = block[..., 3] > 0
    out[y0 + s:y1 + s, x0:x1][solid] = block[solid]
    out[y1 - 1, x0:x1] = img[y1 - 1, x0:x1]   # keep the wrist row so the arm stays attached
    return out


def to_cell(img):
    return kit.to_cell(img, FRAME_W, FRAME_H, BASE_X)


BREATH = [0, 0, 1, 1, 1, 1, 0, 0]
AHOGE = [0, 0, 0, 1, 1, 0, 0, -1]
BREATH_ROW = 94
STRETCH_ROWS = [94, 130, 150]


def idle_frame(face, phase, bobbing):
    """(cell, how far everything above the waist moved up) for one idle phase."""
    img = hair_wave(face, phase)
    img = wiggle_ahoge(img, AHOGE[phase])
    if bobbing:
        img = bob_peace(img, phase)
    lift = BREATH[phase]
    return to_cell(kit.restretch(img, dup=[BREATH_ROW] if lift else [])), lift


def bounce_frame(face, n):
    rows = STRETCH_ROWS[:abs(n)]
    if n >= 0:
        return to_cell(kit.restretch(face, dup=rows)), len(rows)
    return to_cell(kit.restretch(face, drop=rows)), -len(rows)


# ----------------------------------------------------------------- xAI logo

# the xAI mark as four polygons (841.89 x 595.28 view box); the user chose it over the word "grok"
XAI_POLYGONS = [
    [(557.09, 211.99), (565.40, 538.36), (631.96, 538.36), (640.28, 93.18)],
    [(640.28, 56.91), (538.72, 56.91), (379.35, 284.53), (430.13, 357.05)],
    [(201.61, 538.37), (303.17, 538.37), (353.96, 465.85), (303.17, 393.32)],
    [(201.61, 211.99), (430.13, 538.37), (531.69, 538.37), (303.17, 211.99)],
]
XAI_BOX = (201.61, 56.91, 640.28, 538.37)


def xai_logo(w, h, shirt, ink=(236, 236, 238), ss=24):
    """w x h RGBA xAI mark in light ink; edge pixels are mixed with the shirt."""
    x0, y0, x1, y1 = XAI_BOX
    im = Image.new("L", (w * ss, h * ss), 0)
    draw = ImageDraw.Draw(im)
    for poly in XAI_POLYGONS:
        draw.polygon([((x - x0) * w * ss / (x1 - x0), (y - y0) * h * ss / (y1 - y0)) for x, y in poly], fill=255)
    cover = (np.array(im) > 0).reshape(h, ss, w, ss).mean(axis=(1, 3))
    img = np.zeros((h, w, 4), np.uint8)
    for y in range(h):
        for x in range(w):
            if cover[y, x] >= 0.3:
                a = 1.0 if cover[y, x] >= 0.6 else 0.55
                img[y, x, :3] = (np.array(shirt) * (1 - a) + np.array(ink) * a).round()
                img[y, x, 3] = 255
    return img


LOGO_AT = (48, 66)   # top-left in base coordinates, where "grok" was
SHIRT = (41, 39, 42)


def with_logo(cell, lift, mirrored, logo):
    """Paints the logo onto a finished cell; mirrored cells get it unmirrored, so it never reads backwards."""
    x = BASE_X + LOGO_AT[0]
    if mirrored:
        x = FRAME_W - x - logo.shape[1]
    y = BASE_Y + LOGO_AT[1] - lift
    out = cell.copy()
    solid = logo[..., 3] > 0
    out[y:y + logo.shape[0], x:x + logo.shape[1]][solid] = logo[solid]
    return out


# ----------------------------------------------------------------- effects

INK = (22, 22, 26)
SILVER = (150, 150, 158)
WHITE = (246, 246, 248)
BUBBLE = (250, 250, 250)
CURSOR = (74, 69, 65)
ZCOL = (236, 242, 255)
GREEN = (84, 158, 92)

STAR7 = ["...o...", "...o...", "..olo..", "oolcloo", "..olo..", "...o...", "...o..."]
STAR5 = ["..o..", ".olo.", "olclo", ".olo.", "..o.."]


def effects():
    sc = {"o": SILVER, "l": WHITE, "c": WHITE}
    z = {"z": ZCOL}
    sprites = {
        # click burst: silver stars with a dark edge, readable on light and dark desktops
        "spark3": kit.shadowed(kit.grid_sprite(STAR7, sc), OUTLINE),
        "spark2": kit.shadowed(kit.grid_sprite(STAR5, sc), OUTLINE),
        "spark1": kit.shadowed(kit.grid_sprite([".o.", "olo", ".o."], sc), OUTLINE),
        "spark0": kit.shadowed(kit.grid_sprite(["l"], sc), OUTLINE),
        "z2": kit.shadowed(kit.grid_sprite(["zzzzz", "...z.", "..z..", ".z...", "zzzzz"], z), OUTLINE),
        "z1": kit.shadowed(kit.grid_sprite(["zzzz", "..z.", ".z..", "zzzz"], z), OUTLINE),
        "z0": kit.shadowed(kit.grid_sprite(["zzz", ".z.", "zzz"], z), OUTLINE),
        # small x marks and stars drifting up from the logo
        "xmark2": kit.shadowed(kit.grid_sprite(["l...l", ".l.l.", "..l..", ".l.l.", "l...l"], sc), OUTLINE),
        "xmark1": kit.shadowed(kit.grid_sprite(["l.l", ".l.", "l.l"], sc), OUTLINE),
        "star2": kit.shadowed(kit.grid_sprite(STAR5, sc), OUTLINE),
        "star1": kit.shadowed(kit.grid_sprite([".o.", "olo", ".o."], sc), OUTLINE),
    }
    bubble_sprites, tips = kit.bubbles(bubble_contents(), OUTLINE, BUBBLE)
    sprites.update(bubble_sprites)
    return sprites, tips


def spinner_frames():
    """A terminal spinner: a line turning through | / - and back-slash."""
    shapes = [
        ["...o...", "...o...", "...o...", "...o...", "...o...", "...o...", "...o..."],
        ["......o", ".....o.", "....o..", "...o...", "..o....", ".o.....", "o......"],
        [".......", ".......", ".......", "ooooooo", ".......", ".......", "......."],
        ["o......", ".o.....", "..o....", "...o...", "....o..", ".....o.", "......o"],
    ]
    return {"spin%d" % i: kit.grid_sprite(shapes[i % 4], {"o": INK}) for i in range(8)}


def bubble_contents():
    """An xAI-style X with a cursor (hover), turning-line spinner, '?' and check."""
    o = {"o": INK, "c": CURSOR}
    prompt = ["oo...oo.....", ".oo.oo......", "..ooo.......", "..ooo.......", ".oo.oo......", "oo...oo.cccc", "o.....o.cccc"]
    contents = {
        "on": kit.grid_sprite(prompt, o),
        "off": kit.grid_sprite([row.replace("c", ".") for row in prompt], o),
    }
    contents.update(spinner_frames())
    contents["wait"] = kit.grid_sprite([".ooo.", "oo.oo", "...oo", "..oo.", "..oo.", ".....", "..oo."], o)
    contents["done"] = kit.grid_sprite(["......g", ".....gg", "g...gg.", "gg.gg..", ".ggg...", "..g...."], {"g": GREEN})
    return contents


# ----------------------------------------------------------------- assembly

def base_sprite():
    rgb = np.array(Image.open(HERE / "source.png").convert("RGB")).astype(np.float64)
    fg = cut_out(rgb)
    rgb = remove_lettering(rgb)
    rgb, fg = resize(rgb, fg)
    base, _ = kit.pixelise(rgb, fg, FACTOR, PALETTE_SIZE, OUTLINE)
    return base


def build():
    base = base_sprite()
    print("base sprite", base.shape[1], "x", base.shape[0])
    assert base.shape[1] + 2 * BASE_X <= FRAME_W and base.shape[0] + BASE_Y <= FRAME_H
    shirt = tuple(int(c) for c in base[LOGO_AT[1] + 6, LOGO_AT[0] + 6, :3])
    logo = xai_logo(12, 13, shirt)

    faces = {"normal": face_normal(base), "blink": face_blink(base), "look": face_look(base),
             "hover": base, "happy": face_happy(base), "sleep": face_sleep(base)}
    cells = {}
    for name, img in faces.items():
        for p in range(PHASES):
            cells["%s_%d" % (name, p)] = idle_frame(img, p, name in ("hover", "happy"))
    for n in (-2, -1, 1, 2, 3):
        cells["bounce_%d" % n] = bounce_frame(faces["happy"], n)
    frames = {}
    for name, (cell, lift) in cells.items():
        frames[name] = with_logo(cell, lift, False, logo)
    for name, (cell, lift) in cells.items():
        frames["m_" + name] = with_logo(cell[:, ::-1].copy(), lift, True, logo)

    sprites, sprite_tips = effects()
    # anchors in cell coordinates of the unmirrored frames; she looks to the left
    logo_center = (BASE_X + LOGO_AT[0] + logo.shape[1] // 2, BASE_Y + LOGO_AT[1] + logo.shape[0] // 2)
    anchors = {"bubble": (BASE_X + 32, BASE_Y + 10), "zzz": (BASE_X + 44, BASE_Y + 6),
               "logo": logo_center, "head": (BASE_X + 50, BASE_Y + 30)}
    extra = [
        "facing left",
        "anim twinkle star1 star1 star2 star2 star1 spark0",
        "anim twinkle2 xmark1 xmark1 xmark2 xmark1 spark0",
        "anim spin " + " ".join("spin%d" % f for f in range(8)),
    ]
    kit.write_atlas(SPRITES, frames, sprites, sprite_tips, anchors, (FRAME_W, FRAME_H), extra)
    kit.make_icon(faces["normal"][0:54, 26:80], SPRITES / "icon.ico")
    previews(faces, frames, sprites)


def previews(faces, frames, sprites):
    PREVIEW.mkdir(exist_ok=True)
    light = (170, 180, 195)
    face_box = (30, 20, 70, 52)
    tiles = [kit.on_bg(f[face_box[1]:face_box[3], face_box[0]:face_box[2]], light, 8) for f in faces.values()]
    kit.strip(tiles).save(PREVIEW / "faces.png")
    kit.strip([kit.on_bg(frames["normal_%d" % p], light, 2) for p in range(PHASES)]).save(PREVIEW / "idle.png")
    kit.strip([kit.on_bg(frames["hover_%d" % p], light, 2) for p in range(4)]
              + [kit.on_bg(frames[n], light, 2) for n in ("bounce_-2", "bounce_3", "m_normal_0", "m_happy_0")]).save(PREVIEW / "bounce.png")
    kit.strip([kit.on_bg(s, (200, 205, 210), 8) for s in sprites.values()], bg=(90, 90, 90)).save(PREVIEW / "fx.png")


if __name__ == "__main__":
    build()
