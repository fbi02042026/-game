# -*- coding: utf-8 -*-
"""One-shot: remap themes to identity gameChapter==monsterChapter==folder.
Order: 1 Forest, 2 Undead, 3 Jungle, 4 Field, 5 Sea, 6 Cave, 7 Devil, 8 Ice.
Preserves .meta GUID (rename only; never rewrite guid)."""
from __future__ import print_function
import io
import os
import re
import shutil

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))

THEME = {
    "forest": {"old": 4, "new": 1, "name": "Forest"},
    "undead": {"old": 1, "new": 2, "name": "Undead"},
    "jungle": {"old": 2, "new": 3, "name": "Jungle"},
    "field": {"old": 5, "new": 4, "name": "Field"},
    "sea": {"old": 3, "new": 5, "name": "Sea"},
    "cave": {"old": 6, "new": 6, "name": "Cave"},
    "devil": {"old": 7, "new": 7, "name": "Devil"},
    "ice": {"old": 8, "new": 8, "name": "Ice"},
}
OLD_FOLDER_TO_NEW = {
    "{0} {1}".format(v["old"], v["name"]): "{0} {1}".format(v["new"], v["name"])
    for v in THEME.values()
}
OLD_CH_TO_NEW = {v["old"]: v["new"] for v in THEME.values()}
ID_RE = re.compile(
    r"^(forest|undead|jungle|sea|field|cave|devil|ice)_(\d)(\d{2})$", re.I
)


def remap_id(s):
    m = ID_RE.match(s)
    if not m:
        return s
    theme = m.group(1).lower()
    sprite = m.group(3)
    return "{0}_{1}{2}".format(theme, THEME[theme]["new"], sprite)


def rename_path(src, dst):
    if src == dst:
        return False
    if not os.path.exists(src):
        print("MISSING", src)
        return False
    if os.path.exists(dst):
        print("DST EXISTS", dst)
        return False
    os.rename(src, dst)
    print("REN", os.path.relpath(src, ROOT), "->", os.path.relpath(dst, ROOT))
    return True


def two_phase_folder_rename(parent, mapping):
    temps = {}
    for old, new in mapping.items():
        if old == new:
            continue
        src = os.path.join(parent, old)
        if not os.path.isdir(src):
            print("skip missing folder", src)
            continue
        tmp = os.path.join(parent, "__tmp__" + old.replace(" ", "_"))
        rename_path(src, tmp)
        meta = src + ".meta"
        if os.path.isfile(meta):
            rename_path(meta, tmp + ".meta")
        temps[tmp] = os.path.join(parent, new)
    for tmp, final in temps.items():
        rename_path(tmp, final)
        if os.path.isfile(tmp + ".meta"):
            rename_path(tmp + ".meta", final + ".meta")


def rename_files_in_tree(root):
    if not os.path.isdir(root):
        return
    for dirpath, _dirnames, filenames in os.walk(root):
        for fn in list(filenames):
            if fn.endswith(".meta"):
                continue
            base, ext = os.path.splitext(fn)
            new_base = remap_id(base)
            if new_base == base:
                continue
            src = os.path.join(dirpath, fn)
            dst = os.path.join(dirpath, new_base + ext)
            rename_path(src, dst)
            meta = src + ".meta"
            if os.path.isfile(meta):
                rename_path(meta, dst + ".meta")


def write_utf8(path, text):
    with io.open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


def read_utf8(path):
    with io.open(path, "r", encoding="utf-8") as f:
        return f.read()


def remap_csv_ids_and_chapter(path, id_col=0, chapter_col=None):
    if not os.path.isfile(path):
        print("no csv", path)
        return
    lines = read_utf8(path).splitlines()
    out = []
    for line in lines:
        if not line.strip() or line.startswith("#"):
            out.append(line)
            continue
        if line.startswith("id,") or line.startswith("monsterChapter,") or line.startswith(
            "gameChapter,"
        ):
            out.append(line)
            continue
        parts = line.split(",")
        if len(parts) <= id_col:
            out.append(line)
            continue
        if id_col == 0 and ID_RE.match(parts[0]):
            parts[0] = remap_id(parts[0])
            if chapter_col is not None and len(parts) > chapter_col:
                try:
                    old_ch = int(parts[chapter_col])
                    parts[chapter_col] = str(OLD_CH_TO_NEW.get(old_ch, old_ch))
                except ValueError:
                    pass
            out.append(",".join(parts))
            continue
        out.append(line)
    write_utf8(path, "\n".join(out) + "\n")
    print("CSV", path)


def cook_plain(src_csv, dst_bytes):
    text = read_utf8(src_csv)
    write_utf8(dst_bytes, text)
    print("COOK", dst_bytes)


def main():
    # 1) Background — current mixed names by content
    bg = os.path.join(ROOT, "Assets", "Art", "UI", "background")
    bg_map = {
        "1 Forest": "1 Forest",
        "2 Jungle": "3 Jungle",
        "3 Sea": "5 Sea",
        "4 Undead": "2 Undead",
        "5 Field": "4 Field",
        "6 Cave": "6 Cave",
        "7 Devil": "7 Devil",
        "8 Ice": "8 Ice",
    }
    print("=== BG ===")
    two_phase_folder_rename(bg, bg_map)

    # 2) MonsterSpriteRegistry
    reg = os.path.join(ROOT, "Assets", "Resources", "Config", "MonsterSpriteRegistry")
    print("=== REG ===")
    two_phase_folder_rename(reg, OLD_FOLDER_TO_NEW)
    rename_files_in_tree(reg)

    # 3) Art pack Icons
    icons_bases = [
        os.path.join(
            ROOT,
            "Assets",
            "Art",
            "Effects",
            "2D Pixel RPG Monster Pack",
            "Icons",
            "default size",
            "no shadow",
        ),
        os.path.join(
            ROOT,
            "Assets",
            "Art",
            "Effects",
            "2D Pixel RPG Monster Pack",
            "Icons",
            "min size",
            "no shadow",
        ),
        os.path.join(
            ROOT,
            "Assets",
            "Art",
            "Effects",
            "2D Pixel RPG Monster Pack",
            "Icons",
            "min size",
            "shadow",
        ),
    ]
    for parent in icons_bases:
        print("=== ICONS", parent)
        if os.path.isdir(parent):
            two_phase_folder_rename(parent, OLD_FOLDER_TO_NEW)
            rename_files_in_tree(parent)

    # 4) MonsterConfig SO
    mons = os.path.join(ROOT, "Assets", "Resources", "Config", "Monsters")
    print("=== SO ===")
    assets = [f for f in os.listdir(mons) if f.endswith(".asset")]
    temps = []
    for fn in assets:
        base = fn[:-6]
        new_base = remap_id(base)
        if new_base == base:
            continue
        src = os.path.join(mons, fn)
        tmp = os.path.join(mons, "__tmp__" + new_base + ".asset")
        rename_path(src, tmp)
        meta = src + ".meta"
        if os.path.isfile(meta):
            rename_path(meta, tmp + ".meta")
        temps.append((tmp, os.path.join(mons, new_base + ".asset"), new_base, base))
    for tmp, final, new_base, old_base in temps:
        rename_path(tmp, final)
        if os.path.isfile(tmp + ".meta"):
            rename_path(tmp + ".meta", final + ".meta")
        text = read_utf8(final)
        text2 = text.replace("m_Name: " + old_base, "m_Name: " + new_base)
        text2 = text2.replace("id: " + old_base, "id: " + new_base)
        if text2 != text:
            write_utf8(final, text2)
            print("PATCH", new_base)

    # 5) Tables
    tables = os.path.join(ROOT, "Assets", "Data", "Source", "Tables")
    stats = os.path.join(tables, "monster_stats.csv")
    opaque = os.path.join(tables, "monster_sprite_opaque.csv")
    style = os.path.join(tables, "monster_attack_style.csv")
    theme = os.path.join(tables, "chapter_theme_map.csv")

    print("=== TABLES ===")
    remap_csv_ids_and_chapter(stats, id_col=0, chapter_col=1)
    remap_csv_ids_and_chapter(opaque, id_col=0, chapter_col=None)

    # attack_style: remap monsterChapter column; update header comment
    if os.path.isfile(style):
        lines = read_utf8(style).splitlines()
        out = []
        for line in lines:
            if line.startswith("#"):
                if "1 Undead" in line or "不是游戏章节" in line:
                    out.append(
                        "# monsterChapter = 素材章(=游戏章)：1 Forest … 4 Field … 5 Sea … 8 Ice"
                    )
                else:
                    out.append(line)
                continue
            if line.startswith("monsterChapter,"):
                out.append(line)
                continue
            parts = line.split(",")
            if not parts or not parts[0].strip().isdigit():
                out.append(line)
                continue
            old_ch = int(parts[0])
            parts[0] = str(OLD_CH_TO_NEW.get(old_ch, old_ch))
            out.append(",".join(parts))
        write_utf8(style, "\n".join(out) + "\n")
        print("CSV", style)

    theme_text = "\n".join(
        [
            "# gameChapter=游戏章；monsterChapter=素材章（恒等）",
            "gameChapter,monsterChapter,folderName,mapName,bgFolder",
            "1,1,1 Forest,暮影森林,1 Forest",
            "2,2,2 Undead,幽冥墓园,2 Undead",
            "3,3,3 Jungle,翡翠秘境,3 Jungle",
            "4,4,4 Field,晨曦草原,4 Field",
            "5,5,5 Sea,海岛遗迹,5 Sea",
            "6,6,6 Cave,巨岩深窟,6 Cave",
            "7,7,7 Devil,赤焰炼狱,7 Devil",
            "8,8,8 Ice,永霜雪境,8 Ice",
            "",
        ]
    )
    write_utf8(theme, theme_text)
    print("CSV", theme)

    out_dir = os.path.join(ROOT, "Assets", "Resources", "Data", "Tables")
    cook_plain(stats, os.path.join(out_dir, "monster_stats.bytes"))
    cook_plain(style, os.path.join(out_dir, "monster_attack_style.bytes"))
    cook_plain(opaque, os.path.join(out_dir, "monster_sprite_opaque.bytes"))
    cook_plain(theme, os.path.join(out_dir, "chapter_theme_map.bytes"))

    print("BG now", sorted(d for d in os.listdir(bg) if os.path.isdir(os.path.join(bg, d))))
    print("REG now", sorted(d for d in os.listdir(reg) if os.path.isdir(os.path.join(reg, d))))
    print("DONE")


if __name__ == "__main__":
    main()
