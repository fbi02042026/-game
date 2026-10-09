# -*- coding: utf-8 -*-
"""只读统计：装备模板按 slotType 分布（查「抽奖总是鞋」是否因为防具模板只做了鞋）"""
import os, re, io, collections

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DIR = os.path.join(ROOT, "Assets", "Resources", "Config", "Equips")

cnt = collections.Counter()
rar = collections.Counter()
for fn in sorted(os.listdir(DIR)):
    if not fn.endswith(".asset"):
        continue
    with io.open(os.path.join(DIR, fn), "r", encoding="utf-8", errors="ignore") as f:
        txt = f.read()
    m = re.search(r"^\s*slotType:\s*(\d+)", txt, re.M)
    slot = m.group(1) if m else "?"
    cnt[slot] += 1
    m2 = re.search(r"^\s*baseRarity:\s*(\d+)", txt, re.M)
    if m2:
        rar[m2.group(1)] += 1

print("EquipSlotType 枚举顺序通常是 0=Head 1=Chest 2=Hands 3=Feet 4=MainHand 5=OffHand（按实际枚举核对）")
print("slotType 分布:", dict(sorted(cnt.items())))
print("baseRarity 分布:", dict(sorted(rar.items())))
print("模板总数:", sum(cnt.values()))
