# -*- coding: utf-8 -*-
"""
包体优化第二/三批：按目录批量下调 maxTextureSize（顶层 + platformSettings 所有平台块）。
只改 .meta 导入设置，不动源图、不动 prefab、不动代码。可逆（备份在 .workbuddy/tmp/meta_backup_20261101/）。
背景类全屏大图 2048→1024；装备小图标 2048→256；loading 2048→1280。
用法：python Tools/ApplyMaxTextureSize_20261101.py
"""
import os, re, shutil, sys

ROOT = r"E:\xiangsumaoxian"
BACKUP = os.path.join(ROOT, r".workbuddy\tmp\meta_backup_20261101")

# (目录或单文件, 目标 maxTextureSize)
TARGETS = [
    # 全屏背景/舞台/分享大图 → 1024
    (r"Assets\Resources\UI\Art\Adventure\StageIcons\Big", 1024),
    (r"Assets\Resources\UI\Art\background\新战斗背景", 1024),
    (r"Assets\Resources\UI\Art\分享", 1024),
    (r"Assets\Resources\UI\Art\background\other", 1024),
    (r"Assets\Resources\UI\Art\小游戏\接东西", 1024),
    (r"Assets\Resources\Story\Backgrounds", 1024),
    # 对战页两张整幅舞台图（单文件，不是精灵表）
    (r"Assets\Resources\UI\Art\对战\梦幻天空圣殿舞台.jpg", 1024),
    (r"Assets\Resources\UI\Art\对战\梦幻浮空城堡像素舞台.jpg", 1024),
    # loading 底图 → 1280
    (r"Assets\Resources\UI\loading", 1280),
    # 装备小图标（源 128×128）→ 256
    (r"Assets\Resources\UI\EquipIcons", 256),
]

def iter_metas(path):
    if os.path.isfile(path):
        if path.endswith((".png", ".jpg", ".jpeg")):
            yield path + ".meta"
        return
    for dirpath, _, files in os.walk(path):
        for f in files:
            if f.endswith((".png", ".jpg", ".jpeg")):
                yield os.path.join(dirpath, f) + ".meta"

def main():
    os.makedirs(BACKUP, exist_ok=True)
    total_changed = 0
    for rel, size in TARGETS:
        p = os.path.join(ROOT, rel)
        if not os.path.exists(p):
            print("SKIP 不存在: %s" % rel)
            continue
        changed = 0
        for meta in iter_metas(p):
            with open(meta, "r", encoding="utf-8") as fh:
                text = fh.read()
            # 顶层与所有 platformSettings 块里的 2048 一并替换（平台块的值才是生效值）
            new_text, n = re.subn(r"maxTextureSize: 2048\b", "maxTextureSize: %d" % size, text)
            if n > 0:
                bname = os.path.relpath(meta, ROOT).replace(os.sep, "__")
                shutil.copy2(meta, os.path.join(BACKUP, bname))
                with open(meta, "w", encoding="utf-8", newline="") as fh:
                    fh.write(new_text)
                changed += 1
        total_changed += changed
        print("OK %-55s -> %4d  (%d 个 meta)" % (rel, size, changed))
    print("合计修改 %d 个 .meta（备份目录 %s）" % (total_changed, BACKUP))

if __name__ == "__main__":
    main()
