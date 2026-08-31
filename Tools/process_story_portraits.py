# -*- coding: utf-8 -*-
"""Story portrait: black background -> transparent, trim padding, sync to Resources."""
import os
import shutil

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
ART = os.path.join(ROOT, "Assets", "Art", "UI", "Story")
RES = os.path.join(ROOT, "Assets", "Resources", "Story", "Portraits")
MERC_ART = os.path.join(ROOT, "Assets", "Art", "UI", "Icons", "\u4f63\u5175\u7acb\u7ed8")
MERC_RES = os.path.join(ROOT, "Assets", "Resources", "Icons", "MercStand")

try:
    from PIL import Image
except ImportError:
    raise SystemExit("pip install pillow")


def process_rgba(src_path, dest_path, threshold=30):
    im = Image.open(src_path).convert("RGBA")
    px = im.load()
    w, h = im.size
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if r <= threshold and g <= threshold and b <= threshold:
                px[x, y] = (0, 0, 0, 0)
    bbox = im.getbbox()
    if bbox:
        im = im.crop(bbox)
    os.makedirs(os.path.dirname(dest_path), exist_ok=True)
    im.save(dest_path, "PNG")
    return im.size


def copy_meta(src, dest):
    meta_src = src + ".meta"
    meta_dest = dest + ".meta"
    if os.path.isfile(meta_src) and os.path.isfile(meta_dest):
        return
    if os.path.isfile(meta_src):
        shutil.copy2(meta_src, meta_dest)


def main():
    if not os.path.isdir(ART):
        print("missing", ART)
        return 1
    os.makedirs(RES, exist_ok=True)
    n = 0
    for name in sorted(os.listdir(ART)):
        if not name.startswith("portrait_") or not name.endswith(".png"):
            continue
        key = name[len("portrait_") : -4]
        src = os.path.join(ART, name)
        dest_art = src
        dest_res = os.path.join(RES, key + ".png")
        size = process_rgba(src, dest_art)
        process_rgba(src, dest_res)
        copy_meta(dest_art, dest_res)
        print("%s -> %s" % (key, size))
        n += 1
        if key == "player":
            player_art = os.path.join(MERC_ART, "\u4f63\u5175\u7acb\u7ed8_\u73a9\u5bb6.png")
            player_res = os.path.join(MERC_RES, "player.png")
            process_rgba(src, player_art)
            process_rgba(src, player_res)
            print("player merc stand synced")
    print("done %d portraits" % n)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
