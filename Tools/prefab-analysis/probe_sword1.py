# -*- coding: utf-8 -*-
"""专查 Sword_1（初始武器）链路：图标资源 / 配置引用 / 预制体引用"""
import os
import re

PROJ = r"E:\xiangsumaoxian"
GUID = re.compile(r"guid:\s*([0-9a-zA-Z+/=]{16,})")


def meta_guid(p):
    if not os.path.isfile(p + ".meta"):
        return None
    with open(p + ".meta", encoding="utf-8", errors="ignore") as fh:
        m = re.search(r"^guid:\s*(\S+)", fh.read(6000), re.M)
    return m.group(1) if m else None


def build_index():
    idx = {}
    for base in ["Assets", "Packages", os.path.join("Library", "PackageCache")]:
        for dp, _dn, fn in os.walk(os.path.join(PROJ, base)):
            for f in fn:
                if not f.endswith(".meta"):
                    continue
                p = os.path.join(dp, f)
                g = meta_guid(p[:-5])
                if g:
                    idx[g] = os.path.relpath(p[:-5], PROJ).replace("\\", "/")
    return idx


idx = build_index()
print("索引 %d" % len(idx))

print("\n--- Sword_1 图标资源 ---")
for p in ["Assets/Art/UI/Icons/EquipIcons/Sword_1.png",
          "Assets/Resources/UI/EquipIcons/Sword_1.png",
          "Assets/SPUM/Resources/Addons/Legacy/0_Unit/0_Sprite/6_Weapons/0_Sword/Sword_1.png"]:
    full = os.path.join(PROJ, p)
    print("  %-84s 存在=%s guid=%s" % (p, os.path.isfile(full), meta_guid(full)))

print("\n--- 装备配置引用 ---")
for p in ["Assets/Resources/Config/Equips/equip_sword_1.asset",
          "Assets/Resources/Config/Equips/equip_offhand_sword_1.asset"]:
    full = os.path.join(PROJ, p)
    if not os.path.isfile(full):
        print("  缺:", p)
        continue
    with open(full, encoding="utf-8", errors="ignore") as fh:
        text = fh.read()
    for line in text.splitlines():
        if "guid:" in line:
            g = GUID.search(line).group(1)
            tgt = idx.get(g, "【悬空】")
            print("  %s\n     %s -> %s" % (os.path.basename(p), line.strip()[:70], tgt))

print("\n--- Sword_1.prefab 引用 ---")
full = os.path.join(PROJ, "Assets/Art/RPG Props and Items 370+/1/Sword_1.prefab")
with open(full, encoding="utf-8", errors="ignore") as fh:
    text = fh.read()
for g in sorted(set(GUID.findall(text))):
    print("  %s -> %s" % (g, idx.get(g, "【悬空】")))
