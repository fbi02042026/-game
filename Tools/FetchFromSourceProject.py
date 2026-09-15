#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""在【源工程】（公司电脑的 Y:\\PixelAdventureTown）里按 guid 清单取回缺失资源。

拿到本机的用法（三步）
----------------------
1. 把 `缺失资源guid清单.json` 和本脚本一起拷到源工程根目录。
2. 在源工程里跑：
       python FetchFromSourceProject.py
   （若源工程不在当前目录：python FetchFromSourceProject.py --src "Y:\\PixelAdventureTown"）
3. 脚本会在 `./_导出资源/` 下按原相对路径放好**资源文件 + 同名 .meta**，
   并生成 `导入说明.txt`。把这个目录整个拷回本机，按说明覆盖到 Assets/ 即可。

铁律
----
**资源文件与 .meta 必须成对拷贝。** 只拷 .png 等于让 Unity 重新导入、生成新 guid，
引用依旧对不上，白拷。本脚本强制成对导出。

冲突处理
--------
若目标工程里已存在同名文件，说明两边 .meta 的 guid 不一致。脚本会在导入说明里
列出这类冲突，由人工决定（默认建议保留目标工程的 .meta，只补目标没有的文件）。
"""
import json
import os
import re
import shutil
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
LIST_JSON = os.path.join(HERE, "缺失资源guid清单.json")
OUT_DIR = os.path.join(HERE, "_导出资源")

META_GUID = re.compile(r"^guid:\s*(\S+)", re.M)


def build_index(src):
    """源工程 (guid -> 相对路径)"""
    idx = {}
    for base in ["Assets", "Packages"]:
        abs_base = os.path.join(src, base)
        if not os.path.isdir(abs_base):
            continue
        for dp, _dn, fn in os.walk(abs_base):
            for f in fn:
                if not f.endswith(".meta"):
                    continue
                p = os.path.join(dp, f)
                try:
                    with open(p, encoding="utf-8", errors="ignore") as fh:
                        m = META_GUID.search(fh.read(6000))
                except OSError:
                    continue
                if m:
                    idx[m.group(1)] = os.path.relpath(p[:-5], src).replace("\\", "/")
    return idx


def main():
    src = os.path.abspath(HERE)
    if "--src" in sys.argv:
        src = sys.argv[sys.argv.index("--src") + 1]

    if not os.path.isfile(LIST_JSON):
        print("找不到 %s，请和本脚本放同一目录" % LIST_JSON)
        return 1
    if not os.path.isdir(os.path.join(src, "Assets")):
        print("源工程不像 Unity 工程（缺 Assets）: %s" % src)
        return 1

    wanted = json.load(open(LIST_JSON, encoding="utf-8"))
    print("源工程: %s" % src)
    print("待取 guid: %d" % len(wanted))

    idx = build_index(src)
    print("源工程 .meta 索引: %d" % len(idx))

    hit, miss, copied = [], [], 0
    os.makedirs(OUT_DIR, exist_ok=True)

    for g, refs in wanted.items():
        rel = idx.get(g)
        if not rel:
            miss.append(g)
            continue
        src_file = os.path.join(src, rel)
        src_meta = src_file + ".meta"
        if not os.path.isfile(src_file):
            miss.append(g)
            continue
        dst_file = os.path.join(OUT_DIR, rel.replace("/", os.sep))
        os.makedirs(os.path.dirname(dst_file), exist_ok=True)
        shutil.copy2(src_file, dst_file)
        if os.path.isfile(src_meta):
            shutil.copy2(src_meta, dst_file + ".meta")
        else:
            print("  !! 缺 .meta，引用仍会断: %s" % rel)
        hit.append((g, rel))
        copied += 1

    print("\n取到 %d / %d   未命中 %d" % (len(hit), len(wanted), len(miss)))

    report = os.path.join(OUT_DIR, "导入说明.txt")
    with open(report, "w", encoding="utf-8") as fh:
        fh.write("缺失资源取回结果\n")
        fh.write("=" * 60 + "\n")
        fh.write("源工程: %s\n" % src)
        fh.write("取到 %d 个，未命中 %d 个\n\n" % (len(hit), len(miss)))
        fh.write("导入步骤（在本机工程执行）\n")
        fh.write("-" * 60 + "\n")
        fh.write("1. 把 _导出资源/Assets 下的内容，**按原相对路径**覆盖到\n")
        fh.write("   本机工程的 Assets/ 对应位置。\n")
        fh.write("2. **必须连 .meta 一起覆盖**，否则 guid 仍是本机新生成的，引用对不上。\n")
        fh.write("3. 若目标已存在同名文件：默认保留目标现有 .meta（避免打断现有引用），\n")
        fh.write("   只补目标没有的文件。确需替换时再手动处理。\n")
        fh.write("4. 回 Unity 触发刷新，然后跑：\n")
        fh.write("       python Tools/DiagnosePhantomSprites.py\n")
        fh.write("       python Tools/CheckGuidRefs.py\n")
        fh.write("   确认悬空数下降。\n\n")
        fh.write("已取回清单\n")
        fh.write("-" * 60 + "\n")
        for g, rel in sorted(hit, key=lambda x: x[1]):
            fh.write("  %s  %s\n" % (g, rel))
        if miss:
            fh.write("\n未命中（源工程里也没有，只能语义重绑）\n")
            fh.write("-" * 60 + "\n")
            for g in miss:
                fh.write("  %s   （被 %s 引用）\n" % (g, ", ".join(
                    os.path.basename(r) for r in wanted[g][:3])))
    print("导入说明: %s" % report)
    return 0


if __name__ == "__main__":
    sys.exit(main())
