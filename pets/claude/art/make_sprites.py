"""
Builds the Claude pet's sprite atlas from source.png.

Pipeline: cut the character out of the white background -> paint away the shirt
lettering -> punch out enclosed background pockets -> 3x line-preserving
downscale -> 20-colour k-means palette -> outline -> face variants (normal,
blink, happy) -> animation frames (hair wave, breathing, ahoge, bounce) ->
mirrored set -> effect sprites (sparkles, Zzz, speech bubble).

Outputs: ../sprites/atlas.png, atlas.txt, icon.ico and preview/*.png
Run:  python pets/claude/art/make_sprites.py   (or build.ps1 -Art)
"""
import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[3] / "art"))
import pixelkit as kit  # noqa: E402

HERE, SPRITES, PREVIEW = kit.pet_dirs(__file__)

FACTOR = 3          # source pixels per sprite pixel
PALETTE_SIZE = 20
OUTLINE = (46, 22, 20)
FRAME_W, FRAME_H = 72, 122   # character cell; base sprite sits at (2, 4)
BASE_X, BASE_Y = 2, 4
PHASES = 8


# ----------------------------------------------------------------- source prep

def cut_out(rgb):
    h, w, _ = rgb.shape
    dist = np.abs(rgb - 253).max(axis=2)
    fg = ~kit.flood(dist <= 14, kit.border_seeds(h, w))

    # keep the largest blob: drops the "^" and "<" UI marks around the drawing
    fg = kit.largest_blob(fg)

    # background visible through gaps (between hair strands, arm and waist),
    # grown by a pixel so their anti-aliased rims go too
    pockets = [(252, 190), (114, 58), (128, 211), (160, 43), (243, 193)]
    hole = kit.flood(fg & (dist <= 20), pockets)
    return fg & ~kit.grow(hole)


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
    mask |= kit.grow(mask) & inbox & (sat < 45)

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
    return kit.patch(img, x0, y0, rows, COLORS)


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


def to_cell(img):
    return kit.to_cell(img, FRAME_W, FRAME_H, BASE_X)


BREATH = [0, 0, 1, 1, 1, 1, 0, 0]
AHOGE = [0, 0, 0, 1, 1, 0, 0, -1]
BREATH_ROW = 62
STRETCH_ROWS = [62, 96, 110]


def idle_frame(face, phase):
    img = hair_wave(face, phase)
    img = wiggle_ahoge(img, AHOGE[phase])
    return to_cell(kit.restretch(img, dup=[BREATH_ROW] if BREATH[phase] else []))


def bounce_frame(face, n):
    if n >= 0:
        return to_cell(kit.restretch(face, dup=STRETCH_ROWS[:n]))
    return to_cell(kit.restretch(face, drop=STRETCH_ROWS[:-n]))


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


def effects():
    sc = {"o": ORANGE, "c": ORANGE_LIGHT}
    z = {"z": ZCOL}
    sprites = {
        "spark3": kit.grid_sprite(["o..o..o", ".o.o.o.", "..oco..", "ooocooo", "..oco..", ".o.o.o.", "o..o..o"], sc),
        "spark2": kit.grid_sprite(["o.o.o", ".oco.", "occco", ".oco.", "o.o.o"], sc),
        "spark1": kit.grid_sprite([".o.", "oco", ".o."], sc),
        "spark0": kit.grid_sprite(["o"], sc),
        "z2": kit.shadowed(kit.grid_sprite(["zzzzz", "...z.", "..z..", ".z...", "zzzzz"], z), OUTLINE),
        "z1": kit.shadowed(kit.grid_sprite(["zzzz", "..z.", ".z..", "zzzz"], z), OUTLINE),
        "z0": kit.shadowed(kit.grid_sprite(["zzz", ".z.", "zzz"], z), OUTLINE),
    }
    bubble_sprites, tips = kit.bubbles(bubble_contents(), OUTLINE, CREAM)
    sprites.update(bubble_sprites)
    return sprites, tips


def bubble_contents():
    """What the bubble can say: '>_' prompt (hover), Claude spinner (working), '?' (needs input), check (done)."""
    o = {"o": ORANGE, "c": CURSOR}
    prompt = ["oo..........", ".oo.........", "..oo........", "...oo.......", "..oo........", ".oo....ccccc", "oo.....ccccc"]
    contents = {
        "on": kit.grid_sprite(prompt, o),
        "off": kit.grid_sprite([row.replace("c", ".") for row in prompt], o),
        # pulses like the Claude Code spinner (· ✢ ✳ ✶ ✻)
        "spin0": kit.grid_sprite(["o"], o),
        "spin1": kit.grid_sprite([".o.", "ooo", ".o."], o),
        "spin2": kit.grid_sprite(["..o..", "..o..", "ooooo", "..o..", "..o.."], o),
        "spin3": kit.grid_sprite(["...o...", ".o.o.o.", "..ooo..", "ooooooo", "..ooo..", ".o.o.o.", "...o..."], o),
        "spin4": kit.grid_sprite(["o..o..o", ".o.o.o.", "..ooo..", "ooooooo", "..ooo..", ".o.o.o.", "o..o..o"], o),
        "wait": kit.grid_sprite([".ooo.", "oo.oo", "...oo", "..oo.", "..oo.", ".....", "..oo."], o),
        "done": kit.grid_sprite(["......g", ".....gg", "g...gg.", "gg.gg..", ".ggg...", "..g...."], {"g": GREEN}),
    }
    return contents


# ----------------------------------------------------------------- assembly

def build():
    rgb = np.array(Image.open(HERE / "source.png").convert("RGB")).astype(np.float64)
    fg = cut_out(rgb)
    rgb = remove_lettering(rgb)
    base, _ = kit.pixelise(rgb, fg, FACTOR, PALETTE_SIZE, OUTLINE)
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
    # anchors in cell coordinates of the unmirrored frames
    anchors = {"bubble": (BASE_X + 56, BASE_Y + 9), "zzz": (BASE_X + 47, BASE_Y + 3),
               "logo": (BASE_X + LOGO_CENTER[0], BASE_Y + LOGO_CENTER[1]), "head": (BASE_X + 40, BASE_Y + 20)}
    kit.write_atlas(SPRITES, frames, sprites, sprite_tips, anchors, (FRAME_W, FRAME_H))
    kit.make_icon(base[0:44, 17:61], SPRITES / "icon.ico")
    previews(faces, frames, sprites)


def previews(faces, frames, sprites):
    PREVIEW.mkdir(exist_ok=True)
    dark = (58, 74, 92)
    face_box = (22, 14, 58, 42)
    tiles = [kit.on_bg(f[face_box[1]:face_box[3], face_box[0]:face_box[2]], dark, 10) for f in faces.values()]
    kit.strip(tiles).save(PREVIEW / "faces.png")
    kit.strip([kit.on_bg(frames["normal_%d" % p], dark, 3) for p in range(PHASES)]).save(PREVIEW / "idle.png")
    kit.strip([kit.on_bg(frames[n], dark, 3) for n in ("bounce_-2", "bounce_-1", "happy_0", "bounce_1", "bounce_2", "bounce_3", "m_normal_0", "m_happy_0")]).save(PREVIEW / "bounce.png")
    kit.strip([kit.on_bg(s, (200, 205, 210), 8) for s in sprites.values()], bg=(90, 90, 90)).save(PREVIEW / "fx.png")


if __name__ == "__main__":
    build()
