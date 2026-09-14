"""
aipets.ico: the tray and exe icon, a little speech bubble with a prompt like the pets show.
Drawn at 16 px and scaled up by whole numbers so it stays crisp.
Run:  python art/app_icon.py   (or build.ps1 -Art)
"""
from pathlib import Path

import numpy as np
from PIL import Image

HERE = Path(__file__).resolve().parent

COLORS = {
    "#": (38, 30, 28),     # outline
    "c": (250, 249, 245),  # bubble
    "o": (217, 119, 87),   # prompt (Claude orange)
    "g": (255, 191, 0),    # cursor (Hermes gold)
}

ICON = [
    "................",
    "..############..",
    ".#cccccccccccc#.",
    "#cccccccccccccc#",
    "#ccoocccccccccc#",
    "#cccooccccccccc#",
    "#ccccoocccccccc#",
    "#cccooccccccccc#",
    "#ccoocccggggccc#",
    "#cccccccggggccc#",
    ".#cccccccccccc#.",
    "..###cc#######..",
    "....#cc#........",
    "...#cc#.........",
    "...##...........",
    "................",
]


def build():
    img = np.zeros((16, 16, 4), np.uint8)
    for y, row in enumerate(ICON):
        for x, ch in enumerate(row):
            if ch in COLORS:
                img[y, x] = COLORS[ch] + (255,)
    base = Image.fromarray(img, "RGBA")
    sizes = [256, 64, 48, 32, 24, 20, 16]
    frames = []
    for s in sizes:
        k = max(1, s // 16)
        big = base.resize((16 * k, 16 * k), Image.NEAREST)
        canvas = Image.new("RGBA", (s, s))
        canvas.alpha_composite(big, ((s - big.width) // 2, (s - big.height) // 2))
        frames.append(canvas)
    # BMP entries: System.Drawing cannot read PNG-compressed icons
    frames[0].save(HERE / "aipets.ico", format="ICO", sizes=[(s, s) for s in sizes], append_images=frames[1:],
                   bitmap_format="bmp")


if __name__ == "__main__":
    build()
