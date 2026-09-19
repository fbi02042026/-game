# -*- coding: utf-8 -*-
"""
2026-09-19 道具图标与本命碎片生成（只读源图，输出新资源，不修改任何已有文件）

1) 从 Art/UI/Icons/Common 拷贝已有图标到 Resources/Icons/Item
2) 缺失的图标生成占位图（纯色块 + 边框，命名带 TODO_ART 登记）
3) 本命碎片：MercFragmentBase（普通/稀有/传奇 底图） + MercHead（佣兵头像）
   → 合成到 Resources/Icons/MercBirthFragment/frag_{headId}_{档位}.png

用法：
  <venv python> Tools/prefab-analysis/gen_item_icons.py
"""
import os
import shutil
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
ART = os.path.join(ROOT, "Assets", "Art", "UI", "Icons")
RES = os.path.join(ROOT, "Assets", "Resources", "Icons")
ITEM_OUT = os.path.join(RES, "Item")
FRAG_OUT = os.path.join(RES, "MercBirthFragment")
BASE_DIR = os.path.join(RES, "MercFragmentBase")
HEAD_DIR = os.path.join(RES, "MercHead")

TIERS = ["普通", "稀有", "传奇"]

# 需要拷贝的已有图标：Art/UI/Icons/Common/xx.png -> Resources/Icons/Item/xx.png
COPY_LIST = [
    "icon_talent_stone",
    "icon_decompose_mat",
    "icon_enchant_stone",
    "icon_hire_scroll",
]

# 需要生成占位图的图标（项目里没有，等主人补资源）
PLACEHOLDER = {
    "icon_skill_scroll": (150, 90, 220, 255),
    "icon_log_fragment": (200, 170, 110, 255),
    "icon_merc_grow": (90, 170, 140, 255),
    "icon_potion_hp": (210, 70, 90, 255),
}


def ensure(d):
    os.makedirs(d, exist_ok=True)


def step1_copy():
    ensure(ITEM_OUT)
    n = 0
    for name in COPY_LIST:
        src = os.path.join(ART, "Common", name + ".png")
        dst = os.path.join(ITEM_OUT, name + ".png")
        if not os.path.exists(src):
            print("  [MISS] 源图不存在:", src)
            continue
        if os.path.exists(dst):
            print("  [SKIP] 已存在:", name)
            continue
        shutil.copy2(src, dst)
        n += 1
        print("  [COPY]", name)
    return n


def step2_placeholder():
    ensure(ITEM_OUT)
    n = 0
    for name, (r, g, b, a) in PLACEHOLDER.items():
        dst = os.path.join(ITEM_OUT, name + ".png")
        if os.path.exists(dst):
            print("  [SKIP] 已存在:", name)
            continue
        img = Image.new("RGBA", (128, 128), (r, g, b, a))
        # 画一圈深色边框，方便一眼认出是占位图
        for i in range(0, 128):
            for w in range(0, 4):
                img.putpixel((i, w), (40, 40, 40, 255))
                img.putpixel((i, 127 - w), (40, 40, 40, 255))
                img.putpixel((w, i), (40, 40, 40, 255))
                img.putpixel((127 - w, i), (40, 40, 40, 255))
        img.save(dst)
        n += 1
        print("  [PLACEHOLDER]", name, "-> 需主人补正式资源")
    return n


def load_base(tier):
    p = os.path.join(BASE_DIR, tier + "碎片.png")
    if not os.path.exists(p):
        print("  [MISS] 碎片底图不存在:", p)
        return None
    return Image.open(p).convert("RGBA")


def step3_fragments():
    ensure(FRAG_OUT)
    heads = sorted(f for f in os.listdir(HEAD_DIR) if f.lower().endswith(".png"))
    heads = [h[:-4] for h in heads]
    if not heads:
        print("  [MISS] MercHead 为空")
        return 0

    n = 0
    for tier in TIERS:
        base = load_base(tier)
        if base is None:
            continue
        bw, bh = base.size
        # 底图 alpha 作为遮罩：只保留碎片形状内部
        mask = base.split()[-1]
        for hid in heads:
            hp = os.path.join(HEAD_DIR, hid + ".png")
            head = Image.open(hp).convert("RGBA")
            # 头像等比填满底图
            head = head.resize((bw, bh), Image.LANCZOS)
            # 用碎片形状裁头像
            shaped = Image.new("RGBA", (bw, bh), (0, 0, 0, 0))
            shaped.paste(head, (0, 0), mask)
            # 再叠一层底图（保留碎片自身的花纹/描边）
            out = Image.alpha_composite(shaped, base)
            dst = os.path.join(FRAG_OUT, "frag_%s_%s.png" % (hid, tier))
            out.save(dst)
            n += 1
        print("  [FRAG] %s 档：%d 张（底图 %dx%d）" % (tier, len(heads), bw, bh))
    return n


def main():
    print("ROOT:", ROOT)
    print("\n[1] 拷贝已有图标")
    c = step1_copy()
    print("\n[2] 生成占位图标（缺美术，待主人补）")
    p = step2_placeholder()
    print("\n[3] 合成佣兵本命碎片")
    f = step3_fragments()
    print("\n完成：拷贝 %d，占位 %d，本命碎片 %d 张" % (c, p, f))
    print("输出目录:", ITEM_OUT)
    print("输出目录:", FRAG_OUT)


if __name__ == "__main__":
    main()
