"""
Shared sprite pipeline for all pets (pets/<id>/art/make_sprites.py imports this).

Generic steps: flood fill, line-preserving downscale with a k-means palette and
outline, pixel patches, frame cells, speech bubbles, atlas + icon output and
preview sheets. Everything image-specific (coordinates, faces, animation rows)
stays in the pet's own make_sprites.py.
"""
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]


def pet_dirs(script_file):
    """(art dir, sprites dir, preview dir) for a pets/<id>/art/make_sprites.py."""
    art = Path(script_file).resolve().parent
    return art, art.parent / "sprites", art / "preview"


# ----------------------------------------------------------------- masks

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


def border_seeds(h, w):
    return [(y, x) for x in range(w) for y in (0, h - 1)] + [(y, x) for y in range(h) for x in (0, w - 1)]


def largest_blob(fg):
    best = None
    seen = np.zeros_like(fg)
    for y in range(fg.shape[0]):
        for x in range(fg.shape[1]):
            if fg[y, x] and not seen[y, x]:
                blob = flood(fg, [(y, x)], conn8=True)
                seen |= blob
                if best is None or blob.sum() > best.sum():
                    best = blob
    return best


def grow(mask):
    """4-neighbour dilation by one pixel."""
    g = mask.copy()
    g[1:] |= mask[:-1]; g[:-1] |= mask[1:]; g[:, 1:] |= mask[:, :-1]; g[:, :-1] |= mask[:, 1:]
    return g


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


def pixelise(rgb, fg, factor, palette_size, outline, outline_bottom=False, dark_share=0.22, coverage=0.45):
    """
    Block downscale by `factor` that keeps thin dark line art (a block with enough
    dark pixels takes their colour), k-means palette, speck merge, 1px outline.
    outline_bottom=False: the figure is cut off at the bottom edge (stands behind the taskbar).
    Returns (RGBA sprite, palette).
    """
    er = fg.copy()   # erode: the outer ring still carries background anti-aliasing
    er[1:] &= fg[:-1]; er[:-1] &= fg[1:]; er[:, 1:] &= fg[:, :-1]; er[:, :-1] &= fg[:, 1:]
    ys, xs = np.where(er)
    x0, x1, y0, y1 = xs.min(), xs.max() + 1, ys.min(), ys.max() + 1
    lum_src = rgb @ [0.299, 0.587, 0.114]
    w, h = -(-(x1 - x0) // factor), -(-(y1 - y0) // factor)
    col = np.zeros((h, w, 3))
    cov = np.zeros((h, w))
    for j in range(h):
        for i in range(w):
            sy, sx = y0 + j * factor, x0 + i * factor
            m = er[sy:sy + factor, sx:sx + factor]
            cov[j, i] = m.sum() / factor ** 2
            if not m.any():
                continue
            px = rgb[sy:sy + factor, sx:sx + factor][m]
            lum = lum_src[sy:sy + factor, sx:sx + factor][m]
            c = px.mean(0)
            dark = lum < min(lum.mean() - 45, 110)   # keep thin line art alive
            if dark.mean() >= dark_share:
                c = px[dark].mean(0)
            col[j, i] = c
    alpha = cov >= coverage
    col = np.pad(col, ((1, 1), (1, 1), (0, 0)))
    alpha = np.pad(alpha, 1)
    h, w = alpha.shape

    lab = kmeans(srgb_to_lab(col[alpha]), palette_size)
    pal = np.array([col[alpha][lab == j].mean(0) for j in range(palette_size)]).round().clip(0, 255)
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
    ring = grow(op) & ~op
    last = np.where(op.any(axis=1))[0].max()
    if not outline_bottom:
        ring[last + 1:] = False
    img[ring] = tuple(outline) + (255,)
    return img[: last + (2 if outline_bottom else 1)], pal


# ----------------------------------------------------------------- drawing helpers

def patch(img, x0, y0, rows, colors):
    """Paint rows of characters at (x0, y0); '.' keeps the pixel, other chars map through `colors`."""
    out = img.copy()
    for j, row in enumerate(rows):
        for i, ch in enumerate(row):
            if ch != ".":
                out[y0 + j, x0 + i] = tuple(colors[ch]) + (255,)
    return out


def grid_sprite(rows, colors):
    h, w = len(rows), max(len(r) for r in rows)
    img = np.zeros((h, w, 4), np.uint8)
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch in colors:
                img[y, x] = tuple(colors[ch]) + (255,)
    return img


def shadowed(img, outline):
    """1px drop shadow down-right: keeps tiny glyphs readable on light and dark desktops."""
    h, w = img.shape[:2]
    out = np.zeros((h + 1, w + 1, 4), np.uint8)
    op = img[..., 3] > 0
    out[1:, 1:][op] = tuple(outline) + (255,)
    out[:h, :w][op] = img[op]
    return out


def restretch(img, dup=(), drop=()):
    """Duplicate / delete whole rows (breathing, squash and stretch)."""
    rows = list(range(img.shape[0]))
    for r in sorted(drop, reverse=True):
        rows.remove(r)
    for r in sorted(dup, reverse=True):
        rows.insert(rows.index(r), r)
    return img[rows]


def to_cell(img, cell_w, cell_h, base_x):
    """Bottom-aligned into a cell: the pet's feet (or cut-off edge) stay on the cell's bottom row."""
    cell = np.zeros((cell_h, cell_w, 4), np.uint8)
    h, w = img.shape[:2]
    cell[cell_h - h:, base_x:base_x + w] = img[max(0, h - cell_h):]
    return cell


def bubble(side, content, outline, fill):
    """Speech bubble with content centred inside; tail points down towards the head."""
    w, h = 22, 17
    body_h = 13
    img = np.zeros((h, w, 4), np.uint8)
    bx0, bx1 = 2, w - 1          # body columns incl. outline (tail on the left)
    outline, fill = tuple(outline), tuple(fill)
    for y in range(body_h):
        for x in range(bx0, bx1):
            edge = y in (0, body_h - 1) or x in (bx0, bx1 - 1)
            corner = (y in (0, body_h - 1)) and (x in (bx0, bx1 - 1))
            if corner:
                continue
            img[y, x] = (outline if edge else fill) + (255,)
    for (x, y) in ((bx0 + 1, 1), (bx1 - 2, 1), (bx0 + 1, body_h - 2), (bx1 - 2, body_h - 2)):
        img[y, x] = outline + (255,)   # rounded corners
    # tail: opening in the bottom edge, then a stair down to the tip
    for x, y, c in ((4, 12, fill), (5, 12, fill), (3, 13, outline), (4, 13, fill), (5, 13, fill),
                    (6, 13, outline), (2, 14, outline), (3, 14, fill), (4, 14, outline),
                    (1, 15, outline), (2, 15, outline), (0, 16, outline)):
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


def bubbles(contents, outline, fill):
    """bubble_<r|l>_<name> sprites plus their tips for every bubble content."""
    sprites, tips = {}, {}
    for side in ("r", "l"):
        for name, content in contents.items():
            img, tip = bubble(side, content, outline, fill)
            sprites["bubble_%s_%s" % (side, name)] = img
            tips["bubble_%s_%s" % (side, name)] = tip
    return sprites, tips


# ----------------------------------------------------------------- output

def write_atlas(out, frames, sprites, sprite_tips, anchors, cell, extra_lines=(), cols=8):
    """
    atlas.png + atlas.txt. Frames go into a grid of cells, effect sprites into one
    row below. `anchors` are cell coordinates of the unmirrored frames.
    """
    out.mkdir(parents=True, exist_ok=True)
    cell_w, cell_h = cell
    names = list(frames)
    frame_rows = -(-len(names) // cols)
    fx_w = sum(s.shape[1] + 1 for s in sprites.values())
    fx_h = max(s.shape[0] for s in sprites.values())
    atlas_w = max(cols * cell_w, fx_w)
    atlas = np.zeros((frame_rows * cell_h + fx_h, atlas_w, 4), np.uint8)
    lines = ["cell %d %d" % (cell_w, cell_h)]
    for i, name in enumerate(names):
        x, y = (i % cols) * cell_w, (i // cols) * cell_h
        atlas[y:y + cell_h, x:x + cell_w] = frames[name]
        lines.append("frame %s %d %d" % (name, x, y))
    x, y = 0, frame_rows * cell_h
    for name, spr in sprites.items():
        h, w = spr.shape[:2]
        atlas[y:y + h, x:x + w] = spr
        tip = sprite_tips.get(name, (w // 2, h // 2))
        lines.append("sprite %s %d %d %d %d %d %d" % (name, x, y, w, h, tip[0], tip[1]))
        x += w + 1
    for name, (ax, ay) in anchors.items():
        lines.append("anchor %s %d %d" % (name, ax, ay))
    lines.extend(extra_lines)

    Image.fromarray(atlas, "RGBA").save(out / "atlas.png", optimize=True)
    (out / "atlas.txt").write_text("\n".join(lines) + "\n", encoding="ascii")
    print("atlas", atlas.shape[1], "x", atlas.shape[0], "frames", len(names))


def make_icon(head, path):
    """
    Multi-size .ico from a square-ish head crop (nearest-neighbour where it fits).
    BMP entries: System.Drawing (tray menu, settings window) cannot read PNG-compressed ones.
    """
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
    imgs[0].save(path, format="ICO", sizes=[(s, s) for s in sizes], append_images=imgs[1:], bitmap_format="bmp")


def on_bg(img, bg, scale):
    im = Image.fromarray(img, "RGBA")
    canvas = Image.new("RGBA", im.size, tuple(bg) + (255,))
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
