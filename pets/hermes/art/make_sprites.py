"""
Builds the Hermes pet's sprite atlas from source.jpg (Hermes-chan, sitting).

Pipeline: cut her out of the white background and drop the floor shadow ->
paint the white rim light along hair and boots black -> 6x line-preserving
downscale -> 24-colour k-means palette -> outline -> redraw what the downscale
loses (headphone, "N" choker) -> face variants. She only animates her face
(blink, look up, smile, happy, sleep); the body stays still.

Outputs: ../sprites/atlas.png, atlas.txt, icon.ico and preview/*.png
Run:  python pets/hermes/art/make_sprites.py   (or build.ps1 -Art)
"""
import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[3] / "art"))
import pixelkit as kit  # noqa: E402

HERE, SPRITES, PREVIEW = kit.pet_dirs(__file__)

FACTOR = 6
PALETTE_SIZE = 24
OUTLINE = (20, 18, 24)
BASE_X, BASE_Y = 1, 1
FRAME_W, FRAME_H = 87 + 2 * BASE_X, 157 + BASE_Y


# ----------------------------------------------------------------- source prep

def cut_out(rgb):
    h, w, _ = rgb.shape
    lum = rgb @ [0.299, 0.587, 0.114]
    sat = rgb.max(axis=2) - rgb.min(axis=2)
    bg = kit.flood(np.abs(rgb - 255).max(axis=2) <= 14, kit.border_seeds(h, w))

    # floor shadow: light, unsaturated grey touching the background below the knees
    # (darker right under the dress, where no boot highlight can be caught)
    yy, xx = np.mgrid[0:h, 0:w]
    shadowish = ~bg & (sat < 18) & (yy > 900) & ((lum > 120) | ((lum > 55) & (xx > 330)))
    seeds = [tuple(p) for p in np.argwhere(bg & kit.grow(shadowish))]
    shadow = kit.flood(shadowish | bg, seeds) & ~bg
    fg = kit.largest_blob(~bg & ~kit.grow(kit.grow(shadow)))

    # the drawing's white rim light along hair and boots turns into noisy dots when
    # downscaled; below the headband it becomes plain black hair
    near_edge = ~fg
    for _ in range(9):
        near_edge = kit.grow(near_edge)
    rim = fg & near_edge & (lum > 110) & (sat < 30) & (yy > 300)
    rgb = rgb.copy()
    rgb[rim] = (24, 22, 27)
    return rgb, fg


# ----------------------------------------------------------------- hand-drawn details

COLORS = {
    "k": (5, 4, 5),        # hair black
    "d": (18, 18, 19),     # hair dark
    "g": (50, 46, 48),     # hair grey
    "c": (104, 100, 106),  # headphone rim
    "h": (242, 239, 236),  # white (headband, choker)
    "L": (176, 172, 172),  # choker shade
    "s": (254, 227, 210),  # skin
    "q": (234, 213, 198),  # skin light shade
    "p": (231, 194, 176),  # skin shade
    "o": (205, 179, 164),  # skin shadow
    "x": (196, 156, 139),  # neck shadow
    "n": (38, 24, 22),     # lash line
    "t": (86, 60, 52),     # iris
    "u": (150, 112, 98),   # iris lower / lighter
    "w": (255, 250, 244),  # eye highlight
    "b": (247, 176, 166),  # blush
    "l": (184, 124, 110),  # mouth line
    "r": (150, 72, 66),    # open mouth
}


def patch(img, x0, y0, rows):
    return kit.patch(img, x0, y0, rows, COLORS)


HEADPHONE = (58, 18)   # top-left of the headphone patch
CHOKER = (46, 44)      # top-left of the choker plate patch


def details(base):
    img = patch(base, *HEADPHONE, rows=[
        "...h...",
        "..chc..",
        "..gcg..",
        ".gkkkg.",
        "gkkkkkg",
        "ckkkkkg",
        "ckkwkkg",
        "ckkkkkg",
        ".ckkkg.",
        "..ccg..",
    ])
    return patch(img, *CHOKER, rows=CHOKER_PLATE)


CHOKER_PLATE = [
    "LhhhhhL",
    "hkhhhkh",
    "hkkhhkh",
    "hkhkhkh",
    "hkhhkkh",
    "LhhhhhL",
]


# face rows: skin and hair around the eyes (x 36..55, y 28..33) and the mouth (x 42..51, y 38..41)
EYES_AT = (36, 28)
MOUTH_AT = (42, 38)

EYE_SKIN = [
    "kkokkkkkkoppkkkkkkkk",
    "kkkkkssssssssssssskk",
    "kkkkksssssssssssssqk",
    "kkkksssssssssssssssk",
    "kpsssssssssssssssssk",
    "kqsssssssssssssssssk",
]

MOUTH_SKIN = [
    "..ssssssss",
    "..sssssss.",
    "..sssssss.",
    "...ssssq..",
]


def face(base, eyes, mouth, blush=False):
    img = patch(base, *EYES_AT, rows=EYE_SKIN)
    img = patch(img, *EYES_AT, rows=eyes)
    img = patch(img, *MOUTH_AT, rows=MOUTH_SKIN)
    img = patch(img, *MOUTH_AT, rows=mouth)
    if blush:
        img = patch(img, 38, 33, ["bb..........bb"])
    return img


FACES = {
    # calm, eyes half closed, looking down to the side like in the drawing
    "normal": dict(
        eyes=[
            "....................",
            "............nnnnnn..",
            ".nnnnn.......ttwtn..",
            "..ttwtn.......uu....",
            "...uu...............",
            "....................",
        ],
        mouth=[
            "..........",
            "...ll.....",
            "..........",
            "..........",
        ]),
    "blink": dict(
        eyes=[
            "....................",
            "....................",
            "............qqqqqq..",
            ".qqqqq......nnnnnn..",
            ".nnnnnn.............",
            "....................",
        ],
        mouth=[
            "..........",
            "...ll.....",
            "..........",
            "..........",
        ]),
    # looks up at you: irises centred, eyes a bit more open
    "look": dict(
        eyes=[
            "............nnnnnn..",
            "............ntwttn..",
            ".nnnnnn......tuut...",
            ".ntwttn.............",
            "..tuut..............",
            "....................",
        ],
        mouth=[
            "..........",
            "...ll.....",
            "..........",
            "..........",
        ]),
    # the same, with a small smile (mouse over her)
    "hover": dict(
        eyes=[
            "............nnnnnn..",
            "............ntwttn..",
            ".nnnnnn......tuut...",
            ".ntwttn.............",
            "..tuut..............",
            "....................",
        ],
        mouth=[
            "..........",
            "..l..l....",
            "...ll.....",
            "..........",
        ]),
    "happy": dict(
        eyes=[
            "....................",
            ".............nnnn...",
            "..nnnn.......n..n...",
            "..n..n..............",
            "....................",
            "....................",
        ],
        mouth=[
            "..........",
            "..llll....",
            "..lrrl....",
            "...ll.....",
        ],
        blush=True),
    "sleep": dict(
        eyes=[
            "....................",
            "....................",
            "....................",
            "............nnnnnn..",
            ".nnnnnn......qqqq...",
            "..qqqq..............",
        ],
        mouth=[
            "..........",
            "..........",
            "...lr.....",
            "..........",
        ]),
}


def mirror(cell):
    m = cell[:, ::-1].copy()
    # re-flip the choker plate so the "N" does not read backwards
    x0, y0 = BASE_X + CHOKER[0], BASE_Y + CHOKER[1]
    x1, y1 = x0 + len(CHOKER_PLATE[0]), y0 + len(CHOKER_PLATE)
    m[y0:y1, FRAME_W - x1:FRAME_W - x0] = cell[y0:y1, x0:x1]
    return m


# ----------------------------------------------------------------- effects

GOLD = (255, 191, 0)          # Hermes default skin: ui_accent #FFBF00
GOLD_LIGHT = (255, 215, 0)    # banner_title #FFD700
BRONZE = (205, 127, 50)       # banner_border #CD7F32
CORNSILK = (255, 248, 220)    # banner_text #FFF8DC
CURSOR = (74, 69, 65)
ZCOL = (236, 242, 255)
GREEN = (84, 158, 92)


def effects():
    sc = {"o": GOLD, "c": CORNSILK}
    z = {"z": ZCOL}
    nc = {"n": GOLD_LIGHT, "b": BRONZE}
    sprites = {
        "spark3": kit.grid_sprite(["o..o..o", ".o.o.o.", "..oco..", "ooocooo", "..oco..", ".o.o.o.", "o..o..o"], sc),
        "spark2": kit.grid_sprite(["o.o.o", ".oco.", "occco", ".oco.", "o.o.o"], sc),
        "spark1": kit.grid_sprite([".o.", "oco", ".o."], sc),
        "spark0": kit.grid_sprite(["o"], sc),
        "z2": kit.shadowed(kit.grid_sprite(["zzzzz", "...z.", "..z..", ".z...", "zzzzz"], z), OUTLINE),
        "z1": kit.shadowed(kit.grid_sprite(["zzzz", "..z.", ".z..", "zzzz"], z), OUTLINE),
        "z0": kit.shadowed(kit.grid_sprite(["zzz", ".z.", "zzz"], z), OUTLINE),
        # music notes drifting out of her headphones
        "note1": kit.shadowed(kit.grid_sprite(["..nn.", "..nbn", "..n.n", "..n..", "nnn..", "nnb..", ".n..."], nc), OUTLINE),
        "note2": kit.shadowed(kit.grid_sprite(["..nnnn", "..nbbn", "..n..n", "..n..n", "nnnnnn", "nbnnbn"], nc), OUTLINE),
        "note0": kit.shadowed(kit.grid_sprite([".n", "nn"], nc), OUTLINE),
    }
    bubble_sprites, tips = kit.bubbles(bubble_contents(), OUTLINE, CORNSILK)
    sprites.update(bubble_sprites)
    return sprites, tips


def spinner_frames():
    """A dot circling like a small orbit, with a fading tail."""
    ring = [(4, 0), (7, 1), (8, 4), (7, 7), (4, 8), (1, 7), (0, 4), (1, 1)]
    frames = {}
    for f in range(8):
        img = np.zeros((9, 9, 4), np.uint8)
        for back, col in ((2, BRONZE), (1, GOLD), (0, GOLD_LIGHT)):
            x, y = ring[(f - back) % 8]
            for dx, dy in ((0, 0), (1, 0), (0, 1), (1, 1)):
                if x + dx < 9 and y + dy < 9:
                    img[y + dy, x + dx] = col + (255,)
        frames["spin%d" % f] = img
    return frames


def bubble_contents():
    """'❯_' prompt (hover), orbit spinner (working), '?' (needs you), check (done)."""
    o = {"o": GOLD, "c": CURSOR}
    prompt = ["oo..........", "ooo.........", ".ooo........", "..ooo.......", ".ooo........", "ooo....ccccc", "oo.....ccccc"]
    contents = {
        "on": kit.grid_sprite(prompt, o),
        "off": kit.grid_sprite([row.replace("c", ".") for row in prompt], o),
    }
    contents.update(spinner_frames())
    contents["wait"] = kit.grid_sprite([".ooo.", "oo.oo", "...oo", "..oo.", "..oo.", ".....", "..oo."], o)
    contents["done"] = kit.grid_sprite(["......g", ".....gg", "g...gg.", "gg.gg..", ".ggg...", "..g...."], {"g": GREEN})
    return contents


# ----------------------------------------------------------------- assembly

def build():
    rgb = np.array(Image.open(HERE / "source.jpg").convert("RGB")).astype(np.float64)
    rgb, fg = cut_out(rgb)
    base, _ = kit.pixelise(rgb, fg, FACTOR, PALETTE_SIZE, OUTLINE, outline_bottom=True)
    print("base sprite", base.shape[1], "x", base.shape[0])
    assert base.shape[1] + 2 * BASE_X <= FRAME_W and base.shape[0] + BASE_Y <= FRAME_H
    base = details(base)

    faces = {name: face(base, **spec) for name, spec in FACES.items()}
    frames = {}
    for name, img in faces.items():
        frames["%s_0" % name] = kit.to_cell(img, FRAME_W, FRAME_H, BASE_X)
    for name in list(frames):
        frames["m_" + name] = mirror(frames[name])

    sprites, sprite_tips = effects()
    anchors = {"bubble": (BASE_X + 33, BASE_Y + 9), "zzz": (BASE_X + 40, BASE_Y + 2),
               "logo": (BASE_X + 62, BASE_Y + 22), "head": (BASE_X + 46, BASE_Y + 22)}
    extra = [
        "facing left",
        "anim twinkle note1 note1 note1 note1 note0",
        "anim twinkle2 note2 note2 note2 note2 note0",
        "anim spin " + " ".join("spin%d" % f for f in range(8)),
    ]
    kit.write_atlas(SPRITES, frames, sprites, sprite_tips, anchors, (FRAME_W, FRAME_H), extra)
    kit.make_icon(base[0:46, 24:70], SPRITES / "icon.ico")
    previews(faces, frames, sprites)


def previews(faces, frames, sprites):
    PREVIEW.mkdir(exist_ok=True)
    dark = (58, 74, 92)
    box = (30, 16, 68, 52)   # x0, y0, x1, y1 in base coordinates
    tiles = [kit.on_bg(f[box[1]:box[3], box[0]:box[2]], dark, 8) for f in faces.values()]
    kit.strip(tiles).save(PREVIEW / "faces.png")
    kit.strip([kit.on_bg(frames[n], dark, 2) for n in ("normal_0", "look_0", "happy_0", "m_normal_0")]).save(PREVIEW / "frames.png")
    kit.strip([kit.on_bg(s, (200, 205, 210), 8) for s in sprites.values()], bg=(90, 90, 90)).save(PREVIEW / "fx.png")


if __name__ == "__main__":
    build()
