"""
Builds the Grok pet's sprite atlas from source.png (Grok-chan with glasses and tablet).

Pipeline: cut the character out of the white background -> paint the shirt
lettering "grok" over with plain shirt black -> shrink to 0.7 (the drawing is
big; this makes her about as tall as Hermes) -> 3x line-preserving downscale ->
28-colour k-means palette -> outline -> take the watch off her wrist -> faces
(normal, blink, look with a glint on her glasses, hover with a wink, happy,
sleep) -> animation frames (breathing, swaying hair tips, ahoge, bounce) ->
mirrored set -> xAI logo on the shirt -> effect sprites (sparks, Zzz, speech
bubble with an X prompt and a turning-line spinner).

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

ENLARGE = 0.7       # 548 x 704 is much bigger than the other drawings
FACTOR = 3          # (resized) source pixels per sprite pixel
PALETTE_SIZE = 28
OUTLINE = (16, 14, 18)
FRAME_W, FRAME_H = 119, 167  # character cell; base sprite (115 x 163) sits at (2, 4)
BASE_X, BASE_Y = 2, 4
PHASES = 8


# ----------------------------------------------------------------- source prep

def cut_out(rgb):
    h, w, _ = rgb.shape
    dist = np.abs(rgb - 253).max(axis=2)
    fg = ~kit.flood(dist <= 14, kit.border_seeds(h, w))
    return kit.largest_blob(fg)


def remove_lettering(rgb):
    """Paint the white 'grok' and its dark rim over with the plain shirt black of the rows around it."""
    y0, y1, x0, x1 = 294, 340, 200, 322
    shirt = np.concatenate([rgb[284:293, x0:x1].reshape(-1, 3), rgb[341:350, x0:x1].reshape(-1, 3)])
    shirt = np.median(shirt[shirt @ [0.299, 0.587, 0.114] < 80], axis=0)
    fill = rgb.copy()
    fill[y0:y1, x0:x1] = shirt
    return fill


def resize(rgb, fg):
    h, w = fg.shape
    size = (round(w * ENLARGE), round(h * ENLARGE))
    small = Image.fromarray(rgb.clip(0, 255).round().astype(np.uint8)).resize(size, Image.LANCZOS)
    mask = Image.fromarray(fg.astype(np.uint8) * 255).resize(size, Image.BILINEAR)
    return np.array(small).astype(np.float64), np.array(mask) >= 128


# ----------------------------------------------------------------- hand-drawn details

COLORS = {
    "a": (13, 11, 11),     # line
    "m": (80, 59, 55),     # soft line
    "A": (254, 230, 218),  # skin
    "y": (251, 221, 207),  # skin shade
    "x": (243, 205, 190),  # skin shadow
    "p": (242, 168, 168),  # blush
    "C": (249, 249, 249),  # glint
}


def patch(img, x0, y0, rows):
    return kit.patch(img, x0, y0, rows, COLORS)


def take_off_watch(base):
    """The user wants her without the watch: skin over the case and band, the wrist outline carried through."""
    return patch(base, 80, 110, [
        "....A.......",
        ".AAAAA......",
        ".yAAAAA.....",
        ".yyAAAA.....",
        ".xyyAAAAAm..",
        "..xyyAAm....",
        "...xmm......",
    ])


# her right eye (higher, on the right in the picture) sits under the top rim of its lens, x 53..61, y 29..32;
# the left eye x 37..47, y 35..37
RIGHT_EYE, LEFT_EYE = (53, 29), (37, 35)
RIGHT_SKIN = [".AAAAAAA.", "AAAAAAAAA", "AAAAAAAAA", ".AAAAAAA."]
LEFT_SKIN = [".AAAAAAAAA.", "AAAAAAAAAAA", ".AAAAAAAAA."]
EYES = {
    "closed": ([".........", ".........", ".a.....a.", "..aaaaa.."],
               ["...........", ".a.......a.", "..aaaaaaa.."]),
    "happy": ([".........", "..aaaaa..", ".a.....a.", "........."],
              ["..aaaaaaa..", ".a.......a.", "..........."]),
    "asleep": ([".........", ".........", ".........", ".aaaaaaa."],
               ["...........", "...........", ".aaaaaaaaa."]),
}
MOUTH_AT = (49, 41)
MOUTH_SKIN = ["AAAAAAAA", "AAAAAAAA", "AAAAAAAA", "AAAAAAAA"]


def eyes(img, right=None, left=None):
    if right:
        img = patch(img, *RIGHT_EYE, rows=RIGHT_SKIN)
        img = patch(img, *RIGHT_EYE, rows=right)
    if left:
        img = patch(img, *LEFT_EYE, rows=LEFT_SKIN)
        img = patch(img, *LEFT_EYE, rows=left)
    return img


def face_blink(base):
    right, left = EYES["closed"]
    return eyes(base, right, left)


def face_look(base):
    """A glint runs over her glasses."""
    img = patch(base, 60, 27, ["..C", ".C.", "C.."])
    return patch(img, 44, 33, [".C", "C."])


def face_hover(base):
    """A wink with the right eye."""
    right, _ = EYES["happy"]
    return patch(eyes(base, right=right), 60, 35, ["pp"])


def face_happy(base):
    right, left = EYES["happy"]
    img = eyes(base, right, left)
    img = patch(img, 60, 35, ["pp"])
    return patch(img, 40, 40, ["pp"])


def face_sleep(base):
    right, left = EYES["asleep"]
    img = eyes(base, right, left)
    img = patch(img, *MOUTH_AT, rows=MOUTH_SKIN)
    return patch(img, *MOUTH_AT, rows=["........", "..mmm...", "........", "........"])


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
    """The hair tips sway: on the left below the tablet, on the right below the head."""
    out = img.copy()
    for side, y_start, y_full, y_end in (("left", 96, 106, 128), ("right", 50, 64, 136)):
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
        seg = img[y, 34:70].copy()
        out[y, 34:70] = 0
        out[y, 34 + s:70 + s] = seg
    return out


def to_cell(img):
    return kit.to_cell(img, FRAME_W, FRAME_H, BASE_X)


BREATH = [0, 0, 1, 1, 1, 1, 0, 0]
AHOGE = [0, 0, 0, 1, 1, 0, 0, -1]
BREATH_ROW = 92
STRETCH_ROWS = [92, 130, 150]


def idle_frame(face, phase):
    """(cell, how far everything above the waist moved up) for one idle phase."""
    img = hair_wave(face, phase)
    img = wiggle_ahoge(img, AHOGE[phase])
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


LOGO_AT = (49, 67)   # top-left in base coordinates, where "grok" was
LOGO = xai_logo(12, 13, (46, 45, 44))


def with_logo(cell, lift, mirrored):
    """Paints the logo onto a finished cell; mirrored cells get it unmirrored, so it never reads backwards."""
    x = BASE_X + LOGO_AT[0]
    if mirrored:
        x = FRAME_W - x - LOGO.shape[1]
    y = BASE_Y + LOGO_AT[1] - lift
    out = cell.copy()
    solid = LOGO[..., 3] > 0
    out[y:y + LOGO.shape[0], x:x + LOGO.shape[1]][solid] = LOGO[solid]
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
    """A terminal spinner: | / - \\ turning."""
    shapes = [
        ["...o...", "...o...", "...o...", "...o...", "...o...", "...o...", "...o..."],
        ["......o", ".....o.", "....o..", "...o...", "..o....", ".o.....", "o......"],
        [".......", ".......", ".......", "ooooooo", ".......", ".......", "......."],
        ["o......", ".o.....", "..o....", "...o...", "....o..", ".....o.", "......o"],
    ]
    return {"spin%d" % i: kit.grid_sprite(shapes[i % 4], {"o": INK}) for i in range(8)}


def bubble_contents():
    """An xAI-style X with a cursor (hover), | / - \\ spinner, '?' and check."""
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
    base = take_off_watch(base)

    faces = {"normal": base, "blink": face_blink(base), "look": face_look(base),
             "hover": face_hover(base), "happy": face_happy(base), "sleep": face_sleep(base)}
    cells = {}
    for name, img in faces.items():
        for p in range(PHASES):
            cells["%s_%d" % (name, p)] = idle_frame(img, p)
    for n in (-2, -1, 1, 2, 3):
        cells["bounce_%d" % n] = bounce_frame(faces["happy"], n)
    frames = {}
    for name, (cell, lift) in cells.items():
        frames[name] = with_logo(cell, lift, False)
    for name, (cell, lift) in cells.items():
        frames["m_" + name] = with_logo(cell[:, ::-1].copy(), lift, True)

    sprites, sprite_tips = effects()
    # anchors in cell coordinates of the unmirrored frames; she looks to the left
    logo_center = (BASE_X + LOGO_AT[0] + LOGO.shape[1] // 2, BASE_Y + LOGO_AT[1] + LOGO.shape[0] // 2)
    anchors = {"bubble": (BASE_X + 34, BASE_Y + 10), "zzz": (BASE_X + 44, BASE_Y + 6),
               "logo": logo_center, "head": (BASE_X + 54, BASE_Y + 30)}
    extra = [
        "facing left",
        "anim twinkle star1 star1 star2 star2 star1 spark0",
        "anim twinkle2 xmark1 xmark1 xmark2 xmark1 spark0",
        "anim spin " + " ".join("spin%d" % f for f in range(8)),
    ]
    kit.write_atlas(SPRITES, frames, sprites, sprite_tips, anchors, (FRAME_W, FRAME_H), extra)
    kit.make_icon(base[0:54, 30:84], SPRITES / "icon.ico")
    previews(faces, frames, sprites)


def previews(faces, frames, sprites):
    PREVIEW.mkdir(exist_ok=True)
    light = (170, 180, 195)
    face_box = (32, 20, 72, 52)
    tiles = [kit.on_bg(f[face_box[1]:face_box[3], face_box[0]:face_box[2]], light, 8) for f in faces.values()]
    kit.strip(tiles).save(PREVIEW / "faces.png")
    kit.strip([kit.on_bg(frames["normal_%d" % p], light, 2) for p in range(PHASES)]).save(PREVIEW / "idle.png")
    kit.strip([kit.on_bg(frames[n], light, 2) for n in ("bounce_-2", "bounce_-1", "happy_0", "bounce_1", "bounce_2", "bounce_3", "m_normal_0", "m_happy_0")]).save(PREVIEW / "bounce.png")
    kit.strip([kit.on_bg(s, (200, 205, 210), 8) for s in sprites.values()], bg=(90, 90, 90)).save(PREVIEW / "fx.png")
    wrist = kit.on_bg(frames["normal_0"][BASE_Y + 100:BASE_Y + 125, BASE_X + 64:BASE_X + 104], light, 10)
    wrist.save(PREVIEW / "wrist.png")


if __name__ == "__main__":
    build()
