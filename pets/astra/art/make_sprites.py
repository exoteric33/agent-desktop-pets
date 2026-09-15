"""
Builds the Astra pet's sprite atlas from source.png (Astra-chan, the Codex pet).

Pipeline: cut the character out of the white background -> paint away the shirt
lettering "ASTRA 6", the sleeve text and the watermark -> enlarge 1.2x (so she
matches Claude's size) -> 3x line-preserving downscale -> 28-colour k-means
palette -> outline -> face variants (normal, blink, look, hover, happy, sleep) ->
animation frames (breathing, hair wave, twinkling stars in her galaxy hair,
ahoge, perked cat ears, bounce) -> mirrored set -> OpenAI logo on the shirt ->
effect sprites (star sparks, Zzz, speech bubble).

Outputs: ../sprites/atlas.png, atlas.txt, icon.ico and preview/*.png
Run:  python pets/astra/art/make_sprites.py   (or build.ps1 -Art)
"""
import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[3] / "art"))
import pixelkit as kit  # noqa: E402

HERE, SPRITES, PREVIEW = kit.pet_dirs(__file__)

ENLARGE = 1.2       # the drawing is cropped tighter than Claude's; this evens out the pixel size
FACTOR = 3          # (enlarged) source pixels per sprite pixel
PALETTE_SIZE = 28
OUTLINE = (24, 16, 30)
FRAME_W, FRAME_H = 88, 124   # character cell; base sprite (84 x 120) sits at (2, 4)
BASE_X, BASE_Y = 2, 4
PHASES = 8


# ----------------------------------------------------------------- source prep

def cut_out(rgb):
    h, w, _ = rgb.shape
    dist = np.abs(rgb - 253).max(axis=2)
    fg = ~kit.flood(dist <= 14, kit.border_seeds(h, w))

    # keep the largest blob: drops the faint "<" mark in the bottom left corner
    fg = kit.largest_blob(fg)

    # background visible between the hand on her hip, the skirt and her hair
    pockets = [(227, 174)]
    hole = kit.flood(fg & (dist <= 20), pockets)
    return fg & ~kit.grow(hole)


def remove_lettering(rgb):
    """Diffuse the shirt white into 'ASTRA 6' and the sleeve text; the watermark gets the plain skirt purple."""
    lum = rgb @ [0.299, 0.587, 0.114]
    sat = rgb.max(axis=2) - rgb.min(axis=2)
    mask = np.zeros(lum.shape, bool)
    inbox = np.zeros(lum.shape, bool)
    # x0, y0, x1, y1, lum from, lum to
    for x0, y0, x1, y1, lo, hi in ((92, 149, 148, 168, 0, 236),       # ASTRA 6
                                   (173, 151, 182, 160, 100, 238),    # sleeve text, upper half
                                   (167, 155, 176, 163, 100, 238)):   # sleeve text, lower half
        box = (slice(y0, y1), slice(x0, x1))
        mask[box] |= (lum[box] >= lo) & (lum[box] < hi) & (sat[box] < 45)
        inbox[box] = True
    mask |= kit.grow(mask) & inbox & (lum > 100) & (lum < 240)

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

    # "@thaflex" on the skirt: lighter than the purple around it; diffusing would smear the pleat lines in
    box = (slice(250, 257), slice(80, 113))
    mark = np.zeros(lum.shape, bool)
    mark[box] = lum[box] >= 111
    fill[mark] = np.median(rgb[240:248, 85:98].reshape(-1, 3), axis=0)
    return fill


def enlarge(rgb, fg):
    h, w = fg.shape
    size = (round(w * ENLARGE), round(h * ENLARGE))
    big = Image.fromarray(rgb.clip(0, 255).round().astype(np.uint8)).resize(size, Image.LANCZOS)
    mask = Image.fromarray(fg.astype(np.uint8) * 255).resize(size, Image.BILINEAR)
    return np.array(big).astype(np.float64), np.array(mask) >= 128


# ----------------------------------------------------------------- faces

COLORS = {
    "h": (14, 11, 12),     # line black
    "u": (75, 65, 62),     # soft line (lower lash, mouth)
    "v": (250, 224, 202),  # skin
    "w": (231, 210, 194),  # skin shade
    "t": (210, 186, 172),  # skin shadow
    "s": (249, 204, 187),  # blush
    "p": (240, 168, 160),  # happy blush
    "r": (178, 72, 84),    # open mouth
    "z": (249, 248, 249),  # eye white / highlight
    "A": (228, 224, 236),  # eye white shade
    "m": (62, 42, 106),    # iris
    "e": (28, 14, 44),     # pupil
    "B": (122, 90, 176),   # iris, lower light
}


def patch(img, x0, y0, rows):
    return kit.patch(img, x0, y0, rows, COLORS)


# her left eye (on the left in the picture) x 28..35, y 30..35; her right eye x 42..50, y 30..37
LEFT_EYE, RIGHT_EYE = (28, 30), (42, 30)
LEFT_SKIN = ["wwwwwwww", "vvvvvvvv", "vvvvvvvv", "vvvvvvvv", "vvvvvvvv", "vvvvvvvv"]   # shaded under her bangs
RIGHT_SKIN = ["vvvvvvvvv"] * 8
MOUTH_AT = (35, 38)
MOUTH_SKIN = ["vvvvvvv", "vvvvvvv", "vvvvvvv", "vvvvvvv"]


def eyes(img, left, right):
    img = patch(img, *LEFT_EYE, rows=LEFT_SKIN)
    img = patch(img, *LEFT_EYE, rows=left)
    img = patch(img, *RIGHT_EYE, rows=RIGHT_SKIN)
    return patch(img, *RIGHT_EYE, rows=right)


def mouth(img, rows):
    img = patch(img, *MOUTH_AT, rows=MOUTH_SKIN)
    return patch(img, *MOUTH_AT, rows=rows)


SMUG = [".......", ".h...h.", "..huh..", "......."]
CAT = [".......", ".h.h.h.", "..h.h..", "......."]     # ":3" while you hover over her
OPEN = [".......", ".hhhhh.", ".hrrrh.", "..hhh.."]
SLEEPY = [".......", ".......", "...uu..", "......."]


def face_normal(base):
    # the downscale keeps her side glance; only give the irises a catch-light
    img = patch(base, 30, 31, ["z"])
    img = patch(img, 43, 33, ["z"])
    return mouth(img, SMUG)


def face_blink(base):
    img = eyes(base,
               ["........", "........", "........", "h......h", ".hhhhhh.", "........"],
               [".........", ".........", ".........", ".........", "h.......h", ".hhhhhhh.", ".........", "........."])
    return mouth(img, SMUG)


LOOK_LEFT = ["hhhhhhhh", "hAzmmzAh", ".zmezmz.", ".zmmmmz.", ".AmBBmA.", "..uuuu.."]
LOOK_RIGHT = ["uhhhhhhh.", "hhhhhhhhh", ".AzmmzmA.", ".zzmezmz.", ".zzmmmmz.", ".zzmBBmz.", "..AmmmA..", "...uuu..."]


def face_look(base):
    """Looks straight at you: irises in the middle of the eyes."""
    return mouth(eyes(base, LOOK_LEFT, LOOK_RIGHT), SMUG)


def face_hover(base):
    return mouth(eyes(base, LOOK_LEFT, LOOK_RIGHT), CAT)


def face_happy(base):
    img = eyes(base,
               ["........", "........", "..hhhh..", ".h....h.", "h......h", "........"],
               [".........", ".........", ".........", "..hhhhh..", ".h.....h.", "h.......h", ".........", "........."])
    img = patch(img, 28, 36, ["pppp"])
    img = patch(img, 45, 39, ["pppp"])
    return mouth(img, OPEN)


def face_sleep(base):
    img = eyes(base,
               ["........", "........", "........", "........", "hhhhhhhh", ".tttttt."],
               [".........", ".........", ".........", ".........", ".........", "hhhhhhhhh", ".ttttttt.", "........."])
    return mouth(img, SLEEPY)


# ----------------------------------------------------------------- animation

def hairish(px):
    """Loose hair: black, or the blue glow of her galaxy strands (not skin, shirt or the purple skirt)."""
    if px[3] == 0:
        return False
    r, g, b = int(px[0]), int(px[1]), int(px[2])
    return 0.299 * r + 0.587 * g + 0.114 * b < 70 or (b > 110 and r < 100)


def hair_span(img, y, side):
    """Horizontal run of loose hair from the outer edge inwards in row y."""
    xs = np.where(img[y, :, 3] > 0)[0]
    if len(xs) == 0:
        return None
    w = img.shape[1]
    start, step = (xs.min(), 1) if side == "left" else (xs.max(), -1)
    x = start
    while 0 < x + 2 * step < w - 1 and abs(x + step - start) < w // 2:
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
    """The long hair below her face sways; above that it frames the face and stays put."""
    out = img.copy()
    for side, y_start, y_full, y_end in (("left", 46, 62, 88), ("right", 46, 60, 92)):
        for y in range(y_start, y_end):
            amp = 1.25 * min(1.0, (y - y_start) / (y_full - y_start))
            s = math.floor(amp * math.sin(2 * math.pi * phase / PHASES - 0.16 * (y - 44)) + 0.5)
            span = hair_span(img, y, side)
            if span:
                shift_span(img, out, y, span[0], span[1], s, side)
    return out


# the white stars in her galaxy strands, each with its own point in the twinkle cycle
STARS = [((57, 23), 0), ((20, 42), 5), ((65, 45), 2), ((72, 62), 6), ((59, 74), 3), ((9, 80), 1), ((65, 86), 4)]
TWINKLE = [0, 1, 2, 1, 0, 0, 0, 0]           # 0 as drawn, 1 glowing, 2 four-point sparkle
STAR_WHITE = (246, 246, 255)
STAR_GLOW = (150, 170, 250)
STAR_RAY = (92, 118, 238)                    # blue enough to still count as hair for the wave


def twinkle_stars(img, phase):
    out = img.copy()
    for (x, y), offset in STARS:
        level = TWINKLE[(phase + offset) % PHASES]
        if level == 0:
            continue
        out[y, x] = STAR_WHITE + (255,)
        rays = [(1, 0), (-1, 0), (0, 1), (0, -1)]
        if level == 2:
            rays += [(2, 0), (-2, 0), (0, 2), (0, -2)]
        for dx, dy in rays:
            if img[y + dy, x + dx, 3] > 0 and hairish(img[y + dy, x + dx]):
                out[y + dy, x + dx] = (STAR_GLOW if abs(dx) + abs(dy) == 1 else STAR_RAY) + (255,)
    return out


def wiggle_ahoge(img, s):
    """The curl on top of her head (rows 0..3) swings sideways."""
    if s == 0:
        return img
    out = img.copy()
    for y in range(0, 4):
        seg = img[y, 38:54].copy()
        out[y, 38:54] = 0
        out[y, 38 + s:54 + s] = seg
    return out


EARS = [(21, 36, 10), (52, 67, 12)]   # x from, x to, base row of each cat ear


def perk_ears(img):
    """Ears up by a pixel (hover, happy, bounce)."""
    out = img.copy()
    for x0, x1, base in EARS:
        out[0:base, x0:x1] = img[1:base + 1, x0:x1]
    return out


def to_cell(img):
    return kit.to_cell(img, FRAME_W, FRAME_H, BASE_X)


BREATH = [0, 0, 1, 1, 1, 1, 0, 0]
AHOGE = [0, 0, 0, 1, 1, 0, 0, -1]
BREATH_ROW = 70
STRETCH_ROWS = [70, 98, 110]


def idle_frame(face, phase):
    """(cell, how far the shirt moved up) for one idle phase."""
    img = twinkle_stars(face, phase)
    img = hair_wave(img, phase)
    img = wiggle_ahoge(img, AHOGE[phase])
    lift = BREATH[phase]
    return to_cell(kit.restretch(img, dup=[BREATH_ROW] if lift else [])), lift


def bounce_frame(face, n):
    rows = STRETCH_ROWS[:abs(n)]
    below = sum(1 for r in rows if r > LOGO_AT[1])
    if n >= 0:
        return to_cell(kit.restretch(face, dup=rows)), below
    return to_cell(kit.restretch(face, drop=rows)), -below


# ----------------------------------------------------------------- OpenAI logo

LOGO_AT = (34, 56)   # top-left in base coordinates, where "ASTRA 6" was

# the blossom rendered at 12 px from the vector logo, point-symmetric, three inks on the white shirt
LOGO = [
    "...lkkkl....",
    "..lk..kkkk..",
    ".mk.lkl...k.",
    "mmk.k.mkklml",
    "k.k.kkkl.kkl",
    "k.k.m..km.mm",
    "mm.mk..m.k.k",
    "lkk.lkkk.k.k",
    "lmlkkm.k.kmm",
    ".k...lkl.km.",
    "..kkkk..kl..",
    "....lkkkl...",
]
INK = np.array((22, 18, 28))
SHIRT = np.array((250, 249, 249))
LOGO_COLORS = {"k": tuple(INK), "m": tuple((SHIRT * 0.38 + INK * 0.62).round().astype(int)),
               "l": tuple((SHIRT * 0.7 + INK * 0.3).round().astype(int))}


def with_logo(cell, lift, mirrored):
    """Paints the logo onto a finished cell; mirrored cells get it unmirrored, so it never reads backwards."""
    x = BASE_X + LOGO_AT[0]
    if mirrored:
        x = FRAME_W - x - len(LOGO[0])
    return kit.patch(cell, x, BASE_Y + LOGO_AT[1] - lift, LOGO, LOGO_COLORS)


# ----------------------------------------------------------------- effects

VIOLET = (122, 83, 183)        # her ears and skirt
LAVENDER = (196, 172, 255)
STARLIGHT = (246, 246, 255)
GALAXY = (78, 97, 195)         # the glow in her hair
BUBBLE = (250, 248, 255)
CURSOR = (74, 69, 65)
ZCOL = (236, 242, 255)
GREEN = (84, 158, 92)

STAR7 = ["...o...", "...o...", "..olo..", "oolcloo", "..olo..", "...o...", "...o..."]
STAR5 = ["..o..", ".olo.", "olclo", ".olo.", "..o.."]
CROSS7 = ["o.....o", ".o...o.", "..olo..", "..lcl..", "..olo..", ".o...o.", "o.....o"]
CROSS5 = ["o...o", ".olo.", ".lcl.", ".olo.", "o...o"]


def effects():
    sc = {"o": VIOLET, "l": LAVENDER, "c": STARLIGHT}
    blue = {"o": GALAXY, "l": LAVENDER, "c": STARLIGHT}
    z = {"z": ZCOL}
    sprites = {
        # click burst: four-point stars
        "spark3": kit.grid_sprite(STAR7, sc),
        "spark2": kit.grid_sprite(STAR5, sc),
        "spark1": kit.grid_sprite([".o.", "oco", ".o."], sc),
        "spark0": kit.grid_sprite(["c"], sc),
        "z2": kit.shadowed(kit.grid_sprite(["zzzzz", "...z.", "..z..", ".z...", "zzzzz"], z), OUTLINE),
        "z1": kit.shadowed(kit.grid_sprite(["zzzz", "..z.", ".z..", "zzzz"], z), OUTLINE),
        "z0": kit.shadowed(kit.grid_sprite(["zzz", ".z.", "zzz"], z), OUTLINE),
        # little stars drifting up from the logo while Codex works
        "star2": kit.shadowed(kit.grid_sprite(STAR5, blue), OUTLINE),
        "star1": kit.shadowed(kit.grid_sprite([".o.", "oco", ".o."], blue), OUTLINE),
        "star0": kit.shadowed(kit.grid_sprite(["c"], blue), OUTLINE),
        "vstar2": kit.shadowed(kit.grid_sprite(CROSS5, sc), OUTLINE),
        "vstar1": kit.shadowed(kit.grid_sprite(["o.o", ".c.", "o.o"], sc), OUTLINE),
    }
    bubble_sprites, tips = kit.bubbles(bubble_contents(), OUTLINE, BUBBLE)
    sprites.update(bubble_sprites)
    return sprites, tips


def spinner_frames():
    """A star that grows, turns from + to x and shrinks again (violet: white would vanish in the bubble)."""
    sc = {"o": VIOLET, "l": VIOLET, "c": LAVENDER}
    shapes = [["o"], [".o.", "oco", ".o."], STAR5, STAR7, CROSS7, CROSS5, ["o.o", ".o.", "o.o"], ["o"]]
    return {"spin%d" % i: kit.grid_sprite(rows, sc) for i, rows in enumerate(shapes)}


def bubble_contents():
    """'›_' prompt like the Codex composer (hover), twinkling star (working), '?' (needs you), check (done)."""
    o = {"o": VIOLET, "c": CURSOR}
    prompt = ["o...........", "oo..........", ".oo.........", "..oo........", ".oo.........", "oo.....ccccc", "o......ccccc"]
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
    rgb, fg = enlarge(rgb, fg)
    base, _ = kit.pixelise(rgb, fg, FACTOR, PALETTE_SIZE, OUTLINE)
    return base


def build():
    base = base_sprite()
    print("base sprite", base.shape[1], "x", base.shape[0])
    assert base.shape[1] + 2 * BASE_X <= FRAME_W and base.shape[0] + BASE_Y <= FRAME_H

    faces = {
        "normal": face_normal(base), "blink": face_blink(base), "look": face_look(base),
        "hover": perk_ears(face_hover(base)), "happy": perk_ears(face_happy(base)), "sleep": face_sleep(base),
    }
    cells = {}
    for name, face in faces.items():
        for p in range(PHASES):
            cells["%s_%d" % (name, p)] = idle_frame(face, p)
    for n in (-2, -1, 1, 2, 3):
        cells["bounce_%d" % n] = bounce_frame(faces["happy"], n)
    frames = {}
    for name, (cell, lift) in cells.items():
        frames[name] = with_logo(cell, lift, False)
    for name, (cell, lift) in cells.items():
        frames["m_" + name] = with_logo(cell[:, ::-1].copy(), lift, True)

    sprites, sprite_tips = effects()
    # anchors in cell coordinates of the unmirrored frames; she looks to the left
    logo_center = (BASE_X + LOGO_AT[0] + len(LOGO[0]) // 2, BASE_Y + LOGO_AT[1] + len(LOGO) // 2)
    anchors = {"bubble": (BASE_X + 20, BASE_Y + 12), "zzz": (BASE_X + 36, BASE_Y + 6),
               "logo": logo_center, "head": (BASE_X + 42, BASE_Y + 28)}
    extra = [
        "facing left",
        "anim twinkle star1 star1 star2 star2 star1 star0",
        "anim twinkle2 vstar1 vstar1 vstar2 vstar1 star0",
        "anim spin " + " ".join("spin%d" % f for f in range(8)),
    ]
    kit.write_atlas(SPRITES, frames, sprites, sprite_tips, anchors, (FRAME_W, FRAME_H), extra)
    kit.make_icon(faces["normal"][0:50, 17:67], SPRITES / "icon.ico")
    previews(faces, frames, sprites)


def previews(faces, frames, sprites):
    PREVIEW.mkdir(exist_ok=True)
    dark = (58, 74, 92)
    face_box = (18, 16, 62, 48)
    tiles = [kit.on_bg(f[face_box[1]:face_box[3], face_box[0]:face_box[2]], dark, 8) for f in faces.values()]
    kit.strip(tiles).save(PREVIEW / "faces.png")
    kit.strip([kit.on_bg(frames["normal_%d" % p], dark, 3) for p in range(PHASES)]).save(PREVIEW / "idle.png")
    kit.strip([kit.on_bg(frames[n], dark, 3) for n in ("bounce_-2", "bounce_-1", "happy_0", "bounce_1", "bounce_2", "bounce_3", "m_normal_0", "m_happy_0")]).save(PREVIEW / "bounce.png")
    kit.strip([kit.on_bg(s, (200, 205, 210), 8) for s in sprites.values()], bg=(90, 90, 90)).save(PREVIEW / "fx.png")


if __name__ == "__main__":
    build()
