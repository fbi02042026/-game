# -*- coding: utf-8 -*-
"""批量检查待删除的 6 个 Editor 脚本的类名在全工程 .cs 里的引用情况。"""
import os, re

TARGETS = ["AutoConfigEditor", "SortingLayerSetup", "URPSetupTool",
           "MonsterPreviewTool", "BattleSceneDiagnose", "BattleLaneAreaEditor",
           "BattlePrefabGenerator"]

ROOT = r"E:\xiangsumaoxian\Assets"

files = []
for root, dirs, fs in os.walk(ROOT):
    for f in fs:
        if f.endswith(".cs"):
            files.append(os.path.join(root, f))
print("扫描 .cs 数量:", len(files))

for t in TARGETS:
    owners = []
    others = []
    for p in files:
        try:
            s = open(p, "r", encoding="utf-8", errors="ignore").read()
        except Exception:
            continue
        if not re.search(r"\b" + t + r"\b", s):
            continue
        rel = os.path.relpath(p, r"E:\xiangsumaoxian")
        is_owner = os.path.basename(p)[:-3] == t
        # 逐行看，区分注释 vs 真调用
        real = False
        for line in s.splitlines():
            if t in line:
                code = line.split("//")[0]
                if re.search(r"\b" + t + r"\b", code):
                    real = True
                    break
        (owners if is_owner else others).append((rel, real))
    print("\n###", t)
    for rel, real in owners:
        print("   [自身]", rel)
    for rel, real in others:
        print(("   [真调用] " if real else "   [仅注释] ") + rel)
