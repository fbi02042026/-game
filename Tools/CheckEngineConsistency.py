#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""双机协作体检：检测「两台电脑引擎不一致」留下的痕迹。

团结引擎(有山迪)与 Unity 国际版的序列化差异：
  - YAML tag 头：团结写 `%TAG !u! tag:yousandi.cn,2023:`
                 Unity 写 `%TAG !u! tag:unity3d.com,2011:`
  - 新导入资源的 .meta guid：团结可能给 base64 形态，Unity 判为非法并忽略该资源

若一个工程里两种 tag 混用，说明有不同引擎各自保存过同一批文件 —— 这就是
「图片/预制体反复丢失」的高发根源。

用法：
    python Tools/CheckEngineConsistency.py            # 体检报告
    python Tools/CheckEngineConsistency.py --list     # 列出所有非主流 tag 的文件
"""
import os
import re
import sys
from collections import Counter

ROOT = os.path.dirname(os.path.abspath(__file__))
PROJ = os.path.dirname(ROOT)

UNITY_TAG = "%TAG !u! tag:unity3d.com,2011:"
TUANJIE_TAG = "%TAG !u! tag:yousandi.cn,2023:"
HEX32 = re.compile(r"^[0-9a-f]{32}$")


def main():
    show_list = "--list" in sys.argv

    # 1) 引擎版本
    pv = os.path.join(PROJ, "ProjectSettings", "ProjectVersion.txt")
    print("=" * 66)
    print("引擎版本")
    print("=" * 66)
    if os.path.isfile(pv):
        with open(pv, encoding="utf-8", errors="ignore") as fh:
            for line in fh:
                if line.strip():
                    print("  %s" % line.strip())
    else:
        print("  (无 ProjectVersion.txt)")
    ei = os.path.join(PROJ, "Library", "EditorInstance.json")
    if os.path.isfile(ei):
        with open(ei, encoding="utf-8", errors="ignore") as fh:
            txt = fh.read()
        m = re.search(r'"app_path"\s*:\s*"([^"]+)"', txt)
        print("  最后打开: %s" % (m.group(1) if m else "?"))

    # 2) YAML tag 统计
    tags = Counter()
    files_by_tag = {}
    for dp, _dn, fn in os.walk(os.path.join(PROJ, "Assets")):
        for f in fn:
            if not f.endswith((".prefab", ".unity", ".asset", ".mat",
                               ".controller", ".anim", ".meta")):
                continue
            p = os.path.join(dp, f)
            try:
                with open(p, encoding="utf-8", errors="ignore") as fh:
                    head = fh.read(400)
            except OSError:
                continue
            for line in head.splitlines()[:4]:
                if line.startswith("%TAG"):
                    t = line.strip()
                    tags[t] += 1
                    files_by_tag.setdefault(t, []).append(
                        os.path.relpath(p, PROJ).replace("\\", "/"))
                    break

    print()
    print("=" * 66)
    print("YAML tag 头分布")
    print("=" * 66)
    for t, n in tags.most_common():
        kind = "团结" if "yousandi" in t else ("Unity" if "unity3d" in t else "?")
        print("  %5d  [%s]  %s" % (n, kind, t))

    if len(tags) > 1:
        print()
        print("  ·  混用多种 tag。注意：团结基于 Unity 2022.3 分支，读旧文件时保留原 tag，")
        print("     只有重新保存时才写成 yousandi.cn —— 所以混用**多半只是痕迹**，")
        print("     表示'哪些文件被团结重新保存过'，**并不等于两台电脑引擎不一致**。")
        print("     先确认两边引擎版本再下结论，勿据此重装引擎。")

    # 3) .meta guid 形态
    print()
    print("=" * 66)
    print(".meta guid 形态")
    print("=" * 66)
    shape = Counter()
    for base in ["Assets", "Packages"]:
        abs_base = os.path.join(PROJ, base)
        if not os.path.isdir(abs_base):
            continue
        for dp, _dn, fn in os.walk(abs_base):
            for f in fn:
                if not f.endswith(".meta"):
                    continue
                with open(os.path.join(dp, f), encoding="utf-8", errors="ignore") as fh:
                    m = re.search(r"^guid:\s*(\S+)", fh.read(4000), re.M)
                if not m:
                    continue
                g = m.group(1)
                if HEX32.match(g):
                    shape["hex32"] += 1
                elif re.match(r"^0{8}[0-9a-f]{24}$", g):
                    shape["内置"] += 1
                else:
                    shape["base64/其它"] += 1
    for k, v in shape.most_common():
        print("  %8d  %s" % (v, k))
    if shape.get("base64/其它", 0) > 0:
        print()
        print("  !! 存在 base64 形态 guid。团结下这是合法的；")
        print("     若另一台用 Unity 国际版打开，会被判为非法并忽略资源。")
        print("     **绝不要批量把 base64 改成 hex**（7e526d69 就是这么踩坑的）。")

    if show_list:
        print()
        print("=" * 66)
        print("非主流 tag 的文件")
        print("=" * 66)
        main_tag = tags.most_common(1)[0][0] if tags else ""
        for t, fl in files_by_tag.items():
            if t == main_tag:
                continue
            print("[%s]  %d 个" % (t, len(fl)))
            for p in sorted(fl)[:40]:
                print("   %s" % p)
            if len(fl) > 40:
                print("   ...另 %d 个" % (len(fl) - 40))
    return 0


if __name__ == "__main__":
    sys.exit(main())
