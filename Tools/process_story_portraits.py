# -*- coding: utf-8 -*-
"""Sync story portraits from 佣兵立绘 -> Art/UI/Story + Resources/Story/Portraits.

IMPORTANT: Do NOT run black-key transparency here. The old process_rgba(threshold=30)
punched holes in dark shadows (arms/body gaps). Always copy source PNG bytes as-is.
"""
import os
import shutil

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
ART = os.path.join(ROOT, "Assets", "Art", "UI", "Story")
RES = os.path.join(ROOT, "Assets", "Resources", "Story", "Portraits")
MERC_ART = os.path.join(ROOT, "Assets", "Art", "UI", "Icons", "\u4f63\u5175\u7acb\u7ed8")
MERC_RES = os.path.join(ROOT, "Assets", "Resources", "Icons", "MercStand")

# merc filename -> story portrait key (Resources/Story/Portraits/{key}.png)
MERC_TO_STORY = [
    ("\u4f1a\u957f\u2014\u2014\u5927\u4f17.png", "guildmaster"),
    ("\u4f1a\u957f\u2014\u2014\u9634\u6697.png", "guildmaster_hidden"),
    ("\u524d\u53f0\u5c0f\u59d0.png", "receptionist"),
    ("\u4f63\u5175\u7acb\u7ed8_\u73a9\u5bb6.png", "player"),
    ("\u4f63\u5175\u7acb\u7ed8_H001.png", "laodun"),
    ("\u4f63\u5175\u7acb\u7ed8_C001.png", "xiaomei"),
    ("\u4f63\u5175\u7acb\u7ed8_C002.png", "altor"),
    ("\u4f63\u5175\u7acb\u7ed8_C003.png", "hunter"),
]


def copy_png(src_path, dest_path):
    os.makedirs(os.path.dirname(dest_path), exist_ok=True)
    shutil.copy2(src_path, dest_path)


def main():
    if not os.path.isdir(MERC_ART):
        print("missing", MERC_ART)
        return 1
    os.makedirs(ART, exist_ok=True)
    os.makedirs(RES, exist_ok=True)
    n = 0
    for merc_name, key in MERC_TO_STORY:
        src = os.path.join(MERC_ART, merc_name)
        if not os.path.isfile(src):
            print("skip missing", merc_name)
            continue
        dest_art = os.path.join(ART, "portrait_" + key + ".png")
        dest_res = os.path.join(RES, key + ".png")
        copy_png(src, dest_art)
        copy_png(src, dest_res)
        print("%s -> portrait_%s (%s)" % (merc_name, key, os.path.getsize(src)))
        n += 1
        if key == "player":
            player_merc = os.path.join(MERC_RES, "player.png")
            copy_png(src, player_merc)
            print("player merc stand synced")
    print("done %d portraits (raw copy, no alpha key)" % n)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
