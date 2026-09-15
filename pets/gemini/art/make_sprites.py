"""
Builds the Gemini pet's sprite atlas from source.png (Gemini-chan, waving).

Pipeline: cut the character out of the off-white background -> paint away the
shirt lettering "3.8 Flash" -> enlarge 1.5x (so her head matches the other
pets) -> 3x line-preserving downscale -> 32-colour k-means palette -> outline ->
redraw the brooch -> faces (the drawing has her eyes closed, so open eyes are
drawn: normal, blink, look, hover, happy, sleep) -> animation frames
(breathing, swaying hair, ahoge, a wave of the raised hand on hover and click,
bounce) -> mirrored set -> Google "G" on the shirt and the four-colour sparkle
clip in her hair -> effect sprites (Gemini sparkles, Zzz, speech bubble).

Outputs: ../sprites/atlas.png, atlas.txt, icon.ico and preview/*.png
Run:  python pets/gemini/art/make_sprites.py   (or build.ps1 -Art)
"""
import math
import re
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

sys.path.insert(0, str(Path(__file__).resolve().parents[3] / "art"))
import pixelkit as kit  # noqa: E402

HERE, SPRITES, PREVIEW = kit.pet_dirs(__file__)

ENLARGE = 1.5       # the drawing is small (229 x 257); this makes her head as big as Astra's
FACTOR = 3          # (enlarged) source pixels per sprite pixel
PALETTE_SIZE = 32   # the hair runs from blue through pink to violet
OUTLINE = (22, 20, 34)
FRAME_W, FRAME_H = 115, 134  # character cell; base sprite (111 x 130) sits at (2, 4)
BASE_X, BASE_Y = 2, 4
PHASES = 8


# ----------------------------------------------------------------- source prep

def cut_out(rgb):
    h, w, _ = rgb.shape
    dist = np.abs(rgb - 250).max(axis=2)   # the background is a slightly noisy off-white
    fg = ~kit.flood(dist <= 12, kit.border_seeds(h, w))

    # keep the largest blob: drops specks along the edges
    fg = kit.largest_blob(fg)

    # background showing between hair strands on both sides
    pockets = [(160, 42), (185, 214)]
    hole = kit.flood(fg & (dist <= 18), pockets)
    return fg & ~kit.grow(hole)


def remove_lettering(rgb):
    """Diffuse the shirt white into '3.8 Flash' (blue digits, dark letters)."""
    lum = rgb @ [0.299, 0.587, 0.114]
    sat = rgb.max(axis=2) - rgb.min(axis=2)
    box = (slice(123, 145), slice(93, 147))
    mask = np.zeros(lum.shape, bool)
    inbox = np.zeros(lum.shape, bool)
    mask[box] = (lum[box] < 215) | (sat[box] > 38)
    inbox[box] = True
    mask |= kit.grow(mask) & inbox & ((lum < 242) | (sat > 18))

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


def enlarge(rgb, fg):
    h, w = fg.shape
    size = (round(w * ENLARGE), round(h * ENLARGE))
    big = Image.fromarray(rgb.clip(0, 255).round().astype(np.uint8)).resize(size, Image.LANCZOS)
    mask = Image.fromarray(fg.astype(np.uint8) * 255).resize(size, Image.BILINEAR)
    return np.array(big).astype(np.float64), np.array(mask) >= 128


# ----------------------------------------------------------------- hand-drawn details

COLORS = {
    "h": (30, 22, 40),     # line
    "u": (246, 214, 195),  # skin
    "t": (228, 184, 168),  # skin shade
    "s": (244, 178, 176),  # blush
    "p": (238, 146, 150),  # happy blush
    "z": (252, 252, 255),  # eye white / highlight
    "E": (24, 52, 132),    # pupil, top of the iris
    "e": (46, 112, 222),   # iris
    "L": (120, 184, 250),  # iris, lower light
    "m": (150, 60, 70),    # open mouth
    "r": (236, 120, 130),  # tongue
    "k": (112, 70, 60),    # soft mouth line
    "o": (122, 78, 32),    # brooch rim
    "g": (238, 186, 64),   # brooch gold
    "y": (255, 228, 140),  # brooch shine
    "b": (58, 120, 230),   # brooch gem
}


def patch(img, x0, y0, rows):
    return kit.patch(img, x0, y0, rows, COLORS)


def brooch(base):
    """The downscale leaves a brown blob of the golden brooch with its blue gem."""
    return patch(base, 58, 52, [
        ".ooo.",
        "oygyo",
        "ogbgo",
        "oggoo",
        ".ooo.",
    ])


# the pixelised face has her eyes closed as in the drawing: paint those and the mouth over with skin
def clean_face(img):
    img = patch(img, 49, 36, ["uuuuuuuu", "uuuuuuuu", "uuuuuuuu", "uuuuu...", ".uu....."])
    img = patch(img, 62, 33, ["...uuuuuu", "uuuuuuuuu", "uuuuuuuuu", ".uuuuuuuu", "uuuuuuuuu"])
    return patch(img, 57, 40, ["uuuuuuuu", "uuuuuuuu", "uuuuuuuu", "uuuuuuuu", "uuuuuuu."])


# her head is tilted: the right eye sits higher than the left one
LEFT_EYE, RIGHT_EYE, MOUTH_AT = (49, 35), (63, 32), (57, 41)

EYES = {
    "open": ([".....hh", ".hhhhhh", "hzEEEz.", ".zeLez.", ".zLLLz.", "..uuu.."],
             ["......hh", "..hhhhhh", ".hzEEEz.", "..zeLez.", "..zLLLz.", "...uuu.."]),
    # looking at you: pupils a little bigger, shine up
    "look": ([".....hh", ".hhhhhh", "hzEzEz.", ".zEEEz.", ".zLLLz.", "..uuu.."],
             ["......hh", "..hhhhhh", ".hzEzEz.", "..zEEEz.", "..zLLLz.", "...uuu.."]),
    "closed": (["........", "........", "........", ".h....h.", "..hhhh..", "........"],
               ["........", "........", "........", "..h....h", "...hhhh.", "........"]),
    # ^ ^ like in the drawing
    "happy": (["........", "........", "..hhhh..", ".h....h.", "........", "........"],
              ["........", "........", "...hhhh.", "..h....h", "........", "........"]),
    "asleep": (["........", "........", "........", "........", ".hhhhhh.", "..tttt.."],
               ["........", "........", "........", "........", "..hhhhhh", "...tttt."]),
}
MOUTHS = {
    "smile": ["........", "..h..h..", "...hh...", "........"],
    "tongue": ["........", "..hhhh..", "..hmmh..", "...rr...", "........"],   # the drawing's "tehe"
    "sleepy": ["........", "........", "...kk...", "........"],
}


def face(base, eyes, mouth, blush="s"):
    left, right = EYES[eyes]
    img = clean_face(base)
    img = patch(img, *LEFT_EYE, rows=left)
    img = patch(img, *RIGHT_EYE, rows=right)
    img = patch(img, *MOUTH_AT, rows=MOUTHS[mouth])
    img = patch(img, 49, 41, [blush * 2])
    return patch(img, 68, 38, [blush * 2])


FACES = {
    "normal": dict(eyes="open", mouth="smile"),
    "blink": dict(eyes="closed", mouth="smile"),
    "look": dict(eyes="look", mouth="smile"),
    "hover": dict(eyes="look", mouth="tongue"),
    "happy": dict(eyes="happy", mouth="tongue", blush="p"),
    "sleep": dict(eyes="asleep", mouth="sleepy"),
}


# ----------------------------------------------------------------- animation

def hairish(px):
    """Loose hair: dark lines or the blue-pink-violet strands (not skin, shirt, cape or skirt)."""
    if px[3] == 0:
        return False
    r, g, b = int(px[0]), int(px[1]), int(px[2])
    if 0.299 * r + 0.587 * g + 0.114 * b < 60:
        return True
    return (b >= 150 and b >= g and b >= r - 15 and max(r, g, b) - min(r, g, b) > 40) or (b > r + 10 and b > g + 20)


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
    """The long hair sways: on the left only below her raised arm, on the right below the face."""
    out = img.copy()
    for side, y_start, y_full, y_end in (("left", 58, 72, 112), ("right", 46, 60, 118)):
        for y in range(y_start, y_end):
            amp = 1.25 * min(1.0, (y - y_start) / (y_full - y_start))
            s = math.floor(amp * math.sin(2 * math.pi * phase / PHASES - 0.16 * (y - 44)) + 0.5)
            span = hair_span(img, y, side)
            if span:
                shift_span(img, out, y, span[0], span[1], s, side)
    return out


def wiggle_ahoge(img, s):
    """The upper curl of her long ahoge (rows 0..6) swings sideways."""
    if s == 0:
        return img
    out = img.copy()
    for y in range(0, 7):
        seg = img[y, 40:72].copy()
        out[y, 40:72] = 0
        out[y, 40 + s:72 + s] = seg
    return out


HAND = (0, 12, 23, 37)          # x from, x to, y from, y to: the fingers of her raised hand
WAVE = [0, -1, 0, 1, 0, -1, 0, 1]


def wave_hand(img, phase):
    """Hover and click: the raised hand flaps up and down, bending at the wrist."""
    s = WAVE[phase]
    if s == 0:
        return img
    x0, x1, y0, y1 = HAND
    out = img.copy()
    out[y0:y1, x0:x1] = 0
    out[y0 + s:y1 + s, x0:x1] = img[y0:y1, x0:x1]
    return out


def to_cell(img):
    return kit.to_cell(img, FRAME_W, FRAME_H, BASE_X)


BREATH = [0, 0, 1, 1, 1, 1, 0, 0]
AHOGE = [0, 0, 0, 1, 1, 0, 0, -1]
BREATH_ROW = 80
STRETCH_ROWS = [80, 104, 120]


def idle_frame(face_img, phase, waving):
    """(cell, how far everything above the chest moved up) for one idle phase."""
    img = hair_wave(face_img, phase)
    img = wiggle_ahoge(img, AHOGE[phase])
    if waving:
        img = wave_hand(img, phase)
    lift = BREATH[phase]
    return to_cell(kit.restretch(img, dup=[BREATH_ROW] if lift else [])), lift


def bounce_frame(face_img, n):
    rows = STRETCH_ROWS[:abs(n)]
    if n >= 0:
        return to_cell(kit.restretch(face_img, dup=rows)), len(rows)
    return to_cell(kit.restretch(face_img, drop=rows)), -len(rows)


# ----------------------------------------------------------------- Google logo and hair clip

GOOGLE_RED, GOOGLE_BLUE, GOOGLE_YELLOW, GOOGLE_GREEN = (234, 67, 53), (66, 133, 244), (251, 188, 5), (52, 168, 83)

# the four paths of the Google "G" (48 x 48 view box)
G_PATHS = [
    (GOOGLE_RED, "M24 9.5c3.54 0 6.71 1.22 9.21 3.6l6.85-6.85C35.9 2.38 30.47 0 24 0 14.62 0 6.51 5.38 2.56 13.22"
                 "l7.98 6.19C12.43 13.72 17.74 9.5 24 9.5z"),
    (GOOGLE_BLUE, "M46.98 24.55c0-1.57-.15-3.09-.38-4.55H24v9.02h12.94c-.58 2.96-2.26 5.48-4.78 7.18l7.73 6"
                  "c4.51-4.18 7.09-10.36 7.09-17.65z"),
    (GOOGLE_YELLOW, "M10.53 28.59c-.48-1.45-.76-2.99-.76-4.59s.27-3.14.76-4.59l-7.98-6.19C.92 16.46 0 20.12 0 24"
                    "c0 3.88.92 7.54 2.56 10.78l7.97-6.19z"),
    (GOOGLE_GREEN, "M24 48c6.48 0 11.93-2.13 15.89-5.81l-7.73-6c-2.15 1.45-4.92 2.3-8.16 2.3-6.26 0-11.57-4.22"
                   "-13.47-9.91l-7.98 6.19C6.51 42.62 14.62 48 24 48z"),
]


def svg_polygon(d, steps=24):
    """Points along an SVG path with M, L, H, V, C, S and Z commands (absolute or relative)."""
    toks = re.findall(r"[A-Za-z]|-?(?:\d+\.?\d*|\.\d+)", d)
    i, x, y, sx, sy, ctrl, cmd = 0, 0.0, 0.0, 0.0, 0.0, None, None
    pts = []

    def num():
        nonlocal i
        i += 1
        return float(toks[i - 1])

    while i < len(toks):
        if toks[i].isalpha():
            cmd = toks[i]
            i += 1
            if cmd in "zZ":
                x, y = sx, sy
                continue
        rel, c = cmd.islower(), cmd.upper()
        ox, oy = (x, y) if rel else (0.0, 0.0)
        if c in "ML":
            x, y = ox + num(), oy + num()
            if c == "M":
                sx, sy = x, y
                cmd = "l" if rel else "L"
            pts.append((x, y))
            ctrl = None
        elif c == "H":
            x = ox + num()
            pts.append((x, y))
            ctrl = None
        elif c == "V":
            y = oy + num()
            pts.append((x, y))
            ctrl = None
        elif c in "CS":
            if c == "C":
                x1, y1 = ox + num(), oy + num()
            else:
                x1, y1 = (2 * x - ctrl[0], 2 * y - ctrl[1]) if ctrl else (x, y)
            x2, y2, x3, y3 = ox + num(), oy + num(), ox + num(), oy + num()
            for k in range(1, steps + 1):
                t = k / steps
                a, b, cc, dd = (1 - t) ** 3, 3 * (1 - t) ** 2 * t, 3 * (1 - t) * t * t, t ** 3
                pts.append((a * x + b * x1 + cc * x2 + dd * x3, a * y + b * y1 + cc * y2 + dd * y3))
            ctrl = (x2, y2)
            x, y = x3, y3
        else:
            raise ValueError("unsupported path command " + cmd)
    return pts


def google_g(size, shirt, ss=24):
    """size x size RGBA 'G': each pixel takes its dominant colour, faint edge pixels are mixed with the shirt."""
    big = size * ss
    cover = []
    for _, d in G_PATHS:
        im = Image.new("L", (big, big), 0)
        ImageDraw.Draw(im).polygon([(px * big / 48.0, py * big / 48.0) for px, py in svg_polygon(d)], fill=255)
        cover.append((np.array(im) > 0).reshape(size, ss, size, ss).mean(axis=(1, 3)))
    cover = np.array(cover)
    total = cover.sum(axis=0)
    img = np.zeros((size, size, 4), np.uint8)
    for y in range(size):
        for x in range(size):
            if total[y, x] < 0.35:
                continue
            color = np.array(G_PATHS[cover[:, y, x].argmax()][0], float)
            a = 1.0 if total[y, x] >= 0.6 else 0.6
            img[y, x, :3] = (np.array(shirt) * (1 - a) + color * a).round()
            img[y, x, 3] = 255
    return img


LOGO_AT = (52, 62)   # top-left of the 12 px "G" in base coordinates, where "3.8 Flash" was
LOGO = google_g(12, (239, 235, 239))
CLIP_AT = (68, 16)   # the sparkle clip in her hair
CLIP = kit.grid_sprite([
    ".hRh.",
    "hYRBh",
    "YYwBB",
    "hGGBh",
    ".hGh.",
], {"h": OUTLINE, "R": GOOGLE_RED, "Y": GOOGLE_YELLOW, "B": GOOGLE_BLUE, "G": GOOGLE_GREEN, "w": (255, 255, 255)})


def paste(cell, sprite, x, y):
    out = cell.copy()
    solid = sprite[..., 3] > 0
    out[y:y + sprite.shape[0], x:x + sprite.shape[1]][solid] = sprite[solid]
    return out


def with_logos(cell, lift, mirrored):
    """Paints the G and the clip onto a finished cell; mirrored cells get them unmirrored."""
    for sprite, (x, y) in ((LOGO, LOGO_AT), (CLIP, CLIP_AT)):
        x += BASE_X
        if mirrored:
            x = FRAME_W - x - sprite.shape[1]
        cell = paste(cell, sprite, x, BASE_Y + y - lift)
    return cell


# ----------------------------------------------------------------- effects

GEM_BLUE = (66, 133, 244)      # the Gemini sparkle runs from blue through violet to pink
GEM_VIOLET = (155, 114, 203)
GEM_PINK = (217, 101, 112)
STARLIGHT = (250, 250, 255)
BUBBLE = (250, 250, 255)
CURSOR = (74, 69, 65)
ZCOL = (236, 242, 255)
GREEN = (84, 158, 92)

SPARKLE7 = ["...b...", "...b...", "..bvv..", "bbvwvpp", "..vpp..", "...p...", "...p..."]
SPARKLE5 = ["..b..", ".bvv.", "bvwvp", ".vpp.", "..p.."]
SPARKLE3 = [".b.", "bwp", ".p."]


def effects():
    sc = {"b": GEM_BLUE, "v": GEM_VIOLET, "p": GEM_PINK, "w": STARLIGHT}
    gc = {"b": GOOGLE_BLUE, "v": GOOGLE_RED, "p": GOOGLE_GREEN, "w": GOOGLE_YELLOW}
    z = {"z": ZCOL}
    sprites = {
        # click burst: Gemini sparkles
        "spark3": kit.grid_sprite(SPARKLE7, sc),
        "spark2": kit.grid_sprite(SPARKLE5, sc),
        "spark1": kit.grid_sprite(SPARKLE3, sc),
        "spark0": kit.grid_sprite(["w"], sc),
        "z2": kit.shadowed(kit.grid_sprite(["zzzzz", "...z.", "..z..", ".z...", "zzzzz"], z), OUTLINE),
        "z1": kit.shadowed(kit.grid_sprite(["zzzz", "..z.", ".z..", "zzzz"], z), OUTLINE),
        "z0": kit.shadowed(kit.grid_sprite(["zzz", ".z.", "zzz"], z), OUTLINE),
        # little sparkles drifting up from the G
        "gem2": kit.shadowed(kit.grid_sprite(SPARKLE5, sc), OUTLINE),
        "gem1": kit.shadowed(kit.grid_sprite(SPARKLE3, sc), OUTLINE),
        "gem0": kit.shadowed(kit.grid_sprite(["w"], sc), OUTLINE),
        "goo2": kit.shadowed(kit.grid_sprite(SPARKLE5, gc), OUTLINE),
        "goo1": kit.shadowed(kit.grid_sprite(SPARKLE3, gc), OUTLINE),
    }
    bubble_sprites, tips = kit.bubbles(bubble_contents(), OUTLINE, BUBBLE)
    sprites.update(bubble_sprites)
    return sprites, tips


def spinner_frames():
    """A pulsing Gemini sparkle (she has no status source; the atlas needs a spinner all the same)."""
    sc = {"b": GEM_BLUE, "v": GEM_VIOLET, "p": GEM_PINK, "w": GEM_VIOLET}
    shapes = [["v"], SPARKLE3, SPARKLE5, SPARKLE7, SPARKLE7, SPARKLE5, SPARKLE3, ["v"]]
    return {"spin%d" % i: kit.grid_sprite(rows, sc) for i, rows in enumerate(shapes)}


def bubble_contents():
    """Sparkle and a text cursor like Gemini's input box (hover), sparkle spinner, '?' and check."""
    o = {"b": GEM_BLUE, "v": GEM_VIOLET, "p": GEM_PINK, "c": CURSOR}
    prompt = ["..b.........", "..b.........", ".bvv........", "bbvvp.......", ".vpp........", "..p....ccccc", "..p....ccccc"]
    contents = {
        "on": kit.grid_sprite(prompt, o),
        "off": kit.grid_sprite([row.replace("c", ".") for row in prompt], o),
    }
    contents.update(spinner_frames())
    contents["wait"] = kit.grid_sprite([".vvv.", "vv.vv", "...vv", "..vv.", "..vv.", ".....", "..vv."], o)
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
    base = brooch(base)

    faces = {name: face(base, **spec) for name, spec in FACES.items()}
    cells = {}
    for name, img in faces.items():
        for p in range(PHASES):
            cells["%s_%d" % (name, p)] = idle_frame(img, p, name in ("hover", "happy"))
    for n in (-2, -1, 1, 2, 3):
        cells["bounce_%d" % n] = bounce_frame(faces["happy"], n)
    frames = {}
    for name, (cell, lift) in cells.items():
        frames[name] = with_logos(cell, lift, False)
    for name, (cell, lift) in cells.items():
        frames["m_" + name] = with_logos(cell[:, ::-1].copy(), lift, True)

    sprites, sprite_tips = effects()
    # anchors in cell coordinates of the unmirrored frames; she presents to the left
    logo_center = (BASE_X + LOGO_AT[0] + LOGO.shape[1] // 2, BASE_Y + LOGO_AT[1] + LOGO.shape[0] // 2)
    anchors = {"bubble": (BASE_X + 40, BASE_Y + 12), "zzz": (BASE_X + 50, BASE_Y + 8),
               "logo": logo_center, "head": (BASE_X + 60, BASE_Y + 34)}
    extra = [
        "facing left",
        "anim twinkle gem1 gem1 gem2 gem2 gem1 gem0",
        "anim twinkle2 goo1 goo1 goo2 goo1 gem0",
        "anim spin " + " ".join("spin%d" % f for f in range(8)),
    ]
    kit.write_atlas(SPRITES, frames, sprites, sprite_tips, anchors, (FRAME_W, FRAME_H), extra)
    icon = with_logos(to_cell(faces["normal"]), 0, False)
    kit.make_icon(icon[BASE_Y:BASE_Y + 50, BASE_X + 36:BASE_X + 86], SPRITES / "icon.ico")
    previews(faces, frames, sprites)


def previews(faces, frames, sprites):
    PREVIEW.mkdir(exist_ok=True)
    dark = (58, 74, 92)
    face_box = (40, 22, 84, 52)
    tiles = [kit.on_bg(f[face_box[1]:face_box[3], face_box[0]:face_box[2]], dark, 8) for f in faces.values()]
    kit.strip(tiles).save(PREVIEW / "faces.png")
    kit.strip([kit.on_bg(frames["normal_%d" % p], dark, 3) for p in range(PHASES)]).save(PREVIEW / "idle.png")
    kit.strip([kit.on_bg(frames["hover_%d" % p], dark, 3) for p in range(4)]
              + [kit.on_bg(frames[n], dark, 3) for n in ("bounce_-2", "bounce_3", "m_normal_0", "m_happy_0")]).save(PREVIEW / "bounce.png")
    kit.strip([kit.on_bg(s, (200, 205, 210), 8) for s in sprites.values()], bg=(90, 90, 90)).save(PREVIEW / "fx.png")


if __name__ == "__main__":
    build()
