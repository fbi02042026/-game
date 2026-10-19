#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
技能特效 Pixel Craft 换装工具（对应 Docs/技能特效PixelCraft换装_待办任务清单_2026-10-18.md）

- 把 28 个拿错包（ETFX）的技能特效 prefab 覆盖为 Pixel Craft VFX URP 同语义单次播放版。
- 新建 Ally/iron_wall.prefab（铁壁防御技，原缺特效错放刀光）。
- 零代码改动：技能 .asset 的 vfxPrefab 全部是 {fileID:0}，特效按技能 id 走文件名路径解析。

安全前提（已逐条验证，见清单 §三）：
1) 28 个目标 .asset 的 vfxPrefab 全是 {fileID:0} -> 覆盖 prefab 零断链。
2) 28 个目标文件已入库（git tracked）-> 随时 git checkout 回滚。
3) 铁律 22：28 个目标自批产提交 9ab94dd2(2026-09-15) 后零手改 -> 覆盖不丢手工活。
4) 源 prefab 内部材质/纹理仍指向原 Pixel Craft 包 -> 原包不可删。

用法：
  python Tools/copy_pixelcraft_skill_vfx.py --check    # 只读验收：列出哪些已换/待换
  python Tools/copy_pixelcraft_skill_vfx.py            # 真正执行覆盖 + 新建 iron_wall
必须在仓库根目录运行。
"""
import os
import sys
import subprocess
import uuid

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

SRC_ROOT = os.path.join(REPO_ROOT, "Assets", "Art", "Effects", "Pixel Craft VFX URP", "VFX")
DST_ROOT = os.path.join(REPO_ROOT, "Assets", "Resources", "VFX", "Skills")

# 目标(相对 DST_ROOT) -> 源(相对 SRC_ROOT)。路径已用 Glob 逐条核实存在。
MAP = {
    "Ally/ally_heal.prefab":        "Heal/Particles/VFX_Heal_01_Color_Static.prefab",
    "Ally/ally_shield.prefab":      "Shield/Particles/VFX_Shield_01_Color_Static.prefab",
    "Ally/ally_atk_up.prefab":      "Level Up/Particles/VFX_Level_Up_01_Color_Static.prefab",
    "Ally/ally_atk_speed.prefab":   "Projectile Wind/Particles/VFX_Projectile_Wind_01_Color_Static.prefab",
    "Ally/ally_crit_up.prefab":     "Critical Attack/Particles/VFX_Critical_Attack_01_Color.prefab",
    "Ally/ally_thunder.prefab":     "Lightning/Particles/VFX_Lightning_01_Color_Static.prefab",
    "Merc/SK001.prefab":            "Sword Slash/Particles/VFX_Sword_Slash_01_Color_Static.prefab",
    "Merc/SK002.prefab":            "Impact Sparks/Particles/VFX_Impact_Sparks_01_Color_Static.prefab",
    "Merc/SK003.prefab":            "Sword Slash/Particles/VFX_Sword_Slash_01_Color_Static.prefab",
    "Merc/SK004.prefab":            "Heal/Particles/VFX_Heal_01_Color_Static.prefab",
    "Merc/SK005.prefab":            "Fireball/Particles/VFX_Fireball_Impact_01_Color_Static.prefab",
    "Merc/SK006.prefab":            "Impact Sparks/Particles/VFX_Impact_Sparks_01_Color_Static.prefab",
    "Merc/SK007.prefab":            "Shield/Particles/VFX_Shield_01_Color_Static.prefab",
    "Merc/SK008.prefab":            "Shield/Particles/VFX_Shield_01_Color_Static.prefab",
    "Merc/SK009.prefab":            "Shield/Particles/VFX_Shield_01_Color_Static.prefab",
    "Merc/SK010.prefab":            "Respawn/Particles/VFX_Respawn_01_Color_Static.prefab",
    "Merc/SK011.prefab":            "Heal/Particles/VFX_Heal_01_Color_Static.prefab",
    "Merc/SK012.prefab":            "Critical Attack/Particles/VFX_Critical_Attack_01_Color.prefab",
    "Merc/SK013.prefab":            "Heal/Particles/VFX_Heal_01_Color_Static.prefab",
    "Merc/SK014.prefab":            "Heal/Particles/VFX_Heal_01_Color_Static.prefab",
    "Merc/SK015.prefab":            "Heal/Particles/VFX_Heal_01_Color_Static.prefab",
    "Merc/SK016.prefab":            "Magic Impact/Particles/VFX_Magic_Impact_01_Color_Static.prefab",
    "Merc/SK017.prefab":            "Projectile Burst/Particles/VFX_Projectile_Burst_01_Color_Static.prefab",
    "Merc/SK018.prefab":            "Poison Debuff/Particles/VFX_Poison_Debuff_01_Color_Static.prefab",
    "Merc/SK019.prefab":            "Poison Debuff/Particles/VFX_Poison_Debuff_01_Color_Static.prefab",
    "Merc/SK020.prefab":            "Poison Debuff/Particles/VFX_Poison_Debuff_01_Color_Static.prefab",
    "Monster/mon_slam_multi.prefab":"Impact Sparks/Particles/VFX_Impact_Sparks_01_Color_Static.prefab",
    "Monster/mon_magic_burst.prefab":"Magic Impact/Particles/VFX_Magic_Impact_01_Color_Static.prefab",
}

# 新建项：不在 28 覆盖之列，源同 ally_shield
NEW_TARGETS = {
    "Ally/iron_wall.prefab":        "Shield/Particles/VFX_Shield_01_Color_Static.prefab",
}

META_TEMPLATE = """fileFormatVersion: 2
guid: {guid}
PrefabImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def is_tracked(rel_path):
    """安全闸：目标必须已入库（iron_wall 除外，它是新建）。
    rel_path 相对 DST_ROOT，git ls-files 需要仓库根相对路径，故换算。"""
    try:
        repo_rel = os.path.relpath(os.path.join(DST_ROOT, rel_path), REPO_ROOT)
        r = subprocess.run(
            ["git", "ls-files", "--error-unmatch", repo_rel],
            cwd=REPO_ROOT, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
        )
        return r.returncode == 0
    except Exception:
        return False


def read_text(path):
    with open(path, "r", encoding="utf-8") as f:
        return f.read()


def check_mode():
    print("=== --check 只读验收（比对 目标 vs 源）===")
    pending, done, bad = [], [], []
    for dst, src in {**MAP, **NEW_TARGETS}.items():
        src_path = os.path.join(SRC_ROOT, src)
        dst_path = os.path.join(DST_ROOT, dst)
        if not os.path.exists(src_path):
            bad.append((dst, src, "源缺失"))
            continue
        if not os.path.exists(dst_path):
            pending.append((dst, src, "目标不存在(待建)"))
            continue
        if read_text(src_path) == read_text(dst_path):
            done.append(dst)
        else:
            pending.append((dst, src, "内容不同(待换)"))
    for d in done:
        print("  [已换] " + d)
    for d, s, note in pending:
        print("  [待换] %s  (%s)" % (d, note))
    for d, s, note in bad:
        print("  [异常] %s  -> %s  %s" % (d, s, note))
    print("---")
    print("已换 %d / 待换 %d / 异常 %d" % (len(done), len(pending), len(bad)))
    if bad:
        print("⚠ 存在异常项，先解决再 apply。")


def apply_mode():
    print("=== apply 执行覆盖 + 新建 iron_wall ===")
    changed, skipped, created = [], [], []
    # 28 覆盖
    for dst, src in MAP.items():
        src_path = os.path.join(SRC_ROOT, src)
        dst_path = os.path.join(DST_ROOT, dst)
        if not os.path.exists(src_path):
            print("  [跳过] 源缺失: %s" % src)
            skipped.append(dst)
            continue
        if not is_tracked(dst):
            print("  [跳过] 目标未入库(安全闸): %s" % dst)
            skipped.append(dst)
            continue
        data = read_text(src_path)
        with open(dst_path, "w", encoding="utf-8") as f:
            f.write(data)
        changed.append(dst)
    # 新建 iron_wall（含新 .meta）
    for dst, src in NEW_TARGETS.items():
        src_path = os.path.join(SRC_ROOT, src)
        dst_path = os.path.join(DST_ROOT, dst)
        meta_path = dst_path + ".meta"
        if not os.path.exists(src_path):
            print("  [跳过] 源缺失: %s" % src)
            skipped.append(dst)
            continue
        if os.path.exists(dst_path):
            print("  [跳过] 已存在: %s" % dst)
            skipped.append(dst)
            continue
        data = read_text(src_path)
        with open(dst_path, "w", encoding="utf-8") as f:
            f.write(data)
        # 新造合法 hex32 guid 的 .meta（零引用场景，安全）
        guid = uuid.uuid4().hex  # 32 位小写 hex，正则 ^[0-9a-f]{32}$
        with open(meta_path, "w", encoding="utf-8") as f:
            f.write(META_TEMPLATE.format(guid=guid))
        created.append(dst)
    print("---")
    print("覆盖 %d / 新建 %d / 跳过 %d" % (len(changed), len(created), len(skipped)))
    print("回滚命令: git checkout -- Assets/Resources/VFX/Skills/")
    if created:
        print("新建项(需额外 git add): " + ", ".join(created))


def main():
    if not os.path.isdir(SRC_ROOT):
        print("✗ 源根目录不存在: %s" % SRC_ROOT)
        sys.exit(1)
    if not os.path.isdir(DST_ROOT):
        print("✗ 目标根目录不存在: %s" % DST_ROOT)
        sys.exit(1)
    if "--check" in sys.argv:
        check_mode()
    else:
        apply_mode()


if __name__ == "__main__":
    main()
