#!/usr/bin/env python3
"""
generate_placeholder_maps.py
Run this once to create placeholder map images in Assets/Maps/
Replace them with real R6 map images later.
Usage: python generate_placeholder_maps.py
"""

import os
from pathlib import Path

try:
    from PIL import Image, ImageDraw, ImageFont
    HAS_PIL = True
except ImportError:
    HAS_PIL = False

MAPS = {
    "Border":     ["1F", "2F"],
    "Bank":       ["B", "1F", "2F"],
    "Clubhouse":  ["B", "1F", "2F"],
    "Consulate":  ["B", "1F", "2F"],
    "Kafe":       ["1F", "2F", "3F"],
    "Oregon":     ["B", "1F", "2F"],
}

COLORS = {
    "B":  (30, 20, 45),
    "1F": (20, 30, 40),
    "2F": (20, 40, 30),
    "3F": (40, 30, 20),
}

BASE = Path(__file__).parent / "Assets" / "Maps"

def make_placeholder(map_name: str, floor: str, path: Path):
    path.parent.mkdir(parents=True, exist_ok=True)
    if not HAS_PIL:
        # Write a 1x1 pixel PNG as fallback
        # minimal valid PNG bytes
        import struct, zlib
        def png_chunk(name, data):
            c = zlib.crc32(name + data) & 0xFFFFFFFF
            return struct.pack(">I", len(data)) + name + data + struct.pack(">I", c)

        w, h = 1024, 768
        raw = b"\x00" + bytes([30, 30, 50] * w)  # filter byte + RGB per row
        raw_rows = raw * h
        compressed = zlib.compress(raw_rows)

        png = b"\x89PNG\r\n\x1a\n"
        png += png_chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0))
        png += png_chunk(b"IDAT", compressed)
        png += png_chunk(b"IEND", b"")
        path.write_bytes(png)
        print(f"  Written (minimal PNG): {path}")
        return

    W, H = 1024, 768
    bg = COLORS.get(floor, (25, 35, 45))
    img = Image.new("RGB", (W, H), bg)
    draw = ImageDraw.Draw(img)

    # Grid
    for x in range(0, W, 64):
        draw.line([(x, 0), (x, H)], fill=(bg[0]+15, bg[1]+15, bg[2]+15), width=1)
    for y in range(0, H, 64):
        draw.line([(0, y), (W, y)], fill=(bg[0]+15, bg[1]+15, bg[2]+15), width=1)

    # Border
    draw.rectangle([(0, 0), (W-1, H-1)], outline=(88, 166, 255), width=3)

    # Label
    text = f"{map_name}  ·  {floor}"
    draw.text((W//2, H//2 - 20), text, fill=(140, 180, 255), anchor="mm")
    draw.text((W//2, H//2 + 20),
              "Replace with real map image (1024×768 recommended)",
              fill=(80, 100, 130), anchor="mm")

    img.save(path)
    print(f"  Saved: {path}")

if __name__ == "__main__":
    print("Generating placeholder map images…")
    for map_name, floors in MAPS.items():
        for floor in floors:
            dest = BASE / map_name / f"{floor}.png"
            if dest.exists():
                print(f"  Skipping (exists): {dest}")
                continue
            make_placeholder(map_name, floor, dest)
    print("\nDone. Replace placeholder images with real R6 floor-plan PNGs.")
    print("Image path format: Assets/Maps/<MapName>/<Floor>.png")
    print("e.g.  Assets/Maps/Border/1F.png")
