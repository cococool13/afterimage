#!/usr/bin/env python3
"""Draw the Afterimage mark and write PNG + ICO."""

from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "Assets"
OUT.mkdir(exist_ok=True)

CANVAS = (4, 5, 6, 255)
CORE = (230, 230, 230, 255)
ECHO = (255, 99, 99, 140)
RING = (54, 55, 57, 255)


def mark(size: int) -> Image.Image:
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    pad = max(1, size // 16)
    draw.rounded_rectangle(
        [pad, pad, size - pad - 1, size - pad - 1],
        radius=max(2, size // 5),
        fill=CANVAS,
        outline=RING,
        width=max(1, size // 32),
    )
    # Ghost disc, then solid core — reads at 16px as a dot with a trail.
    s = size / 256
    echo = [round(x * s) for x in (118, 108, 198, 188)]
    core = [round(x * s) for x in (78, 78, 158, 158)]
    draw.ellipse(echo, fill=ECHO)
    draw.ellipse(core, fill=CORE)
    return img


def main() -> None:
    master = mark(1024)
    master.save(OUT / "afterimage.png")
    sizes = [(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)]
    master.save(OUT / "afterimage.ico", format="ICO", sizes=sizes)
    print("wrote", OUT / "afterimage.png", "and", OUT / "afterimage.ico")


if __name__ == "__main__":
    main()
