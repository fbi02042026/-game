# -*- coding: utf-8 -*-
"""第二批菜单清理：备份 + 删除 6 个一次性配置类 Editor 脚本（含 .meta）。"""
import os, shutil

SRC = r"E:\xiangsumaoxian\Assets\Editor"
DST = r"E:\xiangsumaoxian\Tools\_menu_archive_2026-09-25\Assets\Editor"

FILES = [
    "AutoConfigEditor.cs",
    "SortingLayerSetup.cs",
    "URPSetupTool.cs",
    "MonsterPreviewTool.cs",
    "BattleSceneDiagnose.cs",
    "BattleLaneAreaEditor.cs",
]

os.makedirs(DST, exist_ok=True)

moved, missing = [], []
for f in FILES:
    for name in (f, f + ".meta"):
        s = os.path.join(SRC, name)
        d = os.path.join(DST, name)
        if not os.path.exists(s):
            missing.append(name)
            continue
        if os.path.exists(d):
            # 备份里已有同名（不太可能），加后缀避免覆盖
            d = d + ".bak2"
        shutil.move(s, d)
        moved.append(name)

print("已移入备份:", len(moved))
for m in moved:
    print("   ", m)
print("Assets 下已不存在:", len(missing))
for m in missing:
    print("   ", m)

# 复查孤儿 .meta
bad = []
for root, dirs, fs in os.walk(r"E:\xiangsumaoxian\Assets"):
    for f in fs:
        if f.endswith(".meta") and not os.path.exists(os.path.join(root, f[:-5])):
            bad.append(os.path.join(root, f))
print("孤儿 .meta 数量:", len(bad))
for b in bad:
    print("   ", b)
