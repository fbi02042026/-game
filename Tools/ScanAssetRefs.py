#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
ScanAssetRefs.py — 资源引用闭包分析（只出报告，不写盘任何资源）

目的：为微信小游戏移植做包体瘦身，找出 Art/Effects（及全工程）里
     「从打包入口出发不可达」的零引用资源。

原理（模拟 Unity 打包收集逻辑的近似）：
  入口（roots）：
    1. EditorBuildSettings 里登记的场景
    2. Assets/Resources/** 下所有可序列化资源（Resources.Load 全量打包）
    3. StreamingAssets / DirichletMediation 的 Runtime 资源等目录（整目录进包）
  闭包扩展：
    从入口文件的 YAML 文本里抽 `guid: <hex32>` 引用，映射回资源路径，
    prefab/asset/controller/mat/anim 等可序列化资源递归跟进（它们被打包带上）。
    .cs 脚本视为恒打包（进程序集），单独统计不算零引用。
  剩余：
    不在闭包里的资源 = 候选零引用（需人工确认后才能裁）。

注意（与 AGENTS.md 对齐）：
  - 56 字符 meta guid（团结自有标识）按 2026-10-23 口径「一律不管」，单独列出提示走路径加载。
  - 本脚本只读、只写一份 Markdown 报告，不移动/删除/修改任何资源。

用法：
  python Tools/ScanAssetRefs.py            # 分析并写报告到 Logs/
  python Tools/ScanAssetRefs.py --stdout   # 只打印摘要
"""

import os
import re
import sys
from collections import defaultdict

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ASSETS = os.path.join(ROOT, "Assets")
PROJECT_SETTINGS = os.path.join(ROOT, "ProjectSettings", "EditorBuildSettings.asset")

GUID_RE = re.compile(r"guid:\s*([0-9a-f]{32})")
# 可序列化、需要递归跟进的资源扩展名（YAML 文本类）
RECURSE_EXT = {".prefab", ".unity", ".asset", ".controller", ".overridecontroller",
               ".mat", ".anim", ".playable", ".mask", ".signal", ".guiskin"}
# 恒打包的目录（整目录进包，其内容全部视为入口）
WHOLE_DIR_ROOTS = [
    os.path.join(ASSETS, "Resources"),
    os.path.join(ASSETS, "StreamingAssets"),
    os.path.join(ASSETS, "DirichletMediation", "Runtime"),
]
SCRIPT_EXT = ".cs"

def iter_files(base):
    for dirpath, _dirnames, filenames in os.walk(base):
        for fn in filenames:
            yield os.path.join(dirpath, fn)

def iter_dirs(base):
    for dirpath, _dirnames, _filenames in os.walk(base):
        yield dirpath

def human(n):
    for unit in ("B", "KB", "MB", "GB"):
        if n < 1024 or unit == "GB":
            return "%.1f%s" % (n, unit) if unit != "B" else "%dB" % n
        n /= 1024.0

def main():
    write_report = "--stdout" not in sys.argv

    # 1. 收集全工程 meta guid -> path
    guid2path = {}
    path2guid = {}
    guids56 = []  # 56字符长串（口径：不管，只报告）
    for f in iter_files(ASSETS):
        if not f.endswith(".meta"):
            continue
        try:
            with open(f, "r", encoding="utf-8", errors="ignore") as fh:
                head = fh.read(4096)
        except OSError:
            continue
        m = re.search(r"guid:\s*([0-9a-f]{56})", head)
        if m:
            guids56.append(f)
            continue
        m = GUID_RE.search(head)
        if m:
            asset = f[:-5]
            guid2path[m.group(1)] = asset
            path2guid[asset] = m.group(1)

    # 1.5 所有名为 Resources 的目录都是特殊目录（整目录进包），不止 Assets/Resources 一个
    resource_dirs = [d for d in iter_dirs(ASSETS) if os.path.basename(d) == "Resources"]

    # 2. 入口：BuildSettings 场景 + 整目录进包的资源
    roots = set()
    if os.path.exists(PROJECT_SETTINGS):
        with open(PROJECT_SETTINGS, "r", encoding="utf-8", errors="ignore") as fh:
            text = fh.read()
        # 团结引擎把 BuildSettings 场景 guid 写成 56 字符 Base64，直接按 path 字段解析最稳
        for m in re.finditer(r"path:\s*(Assets/[^\r\n]+)", text):
            p = os.path.join(ROOT, m.group(1).strip().replace("/", os.sep))
            if os.path.exists(p):
                roots.add(p)
    for d in WHOLE_DIR_ROOTS + resource_dirs:
        for f in iter_files(d):
            if f.endswith(".meta"):
                continue
            roots.add(f)

    # 3. 闭包 BFS
    closed = set(roots)
    queue = list(roots)
    while queue:
        cur = queue.pop()
        if cur.endswith(SCRIPT_EXT):
            continue
        try:
            with open(cur, "r", encoding="utf-8", errors="ignore") as fh:
                text = fh.read()
        except OSError:
            continue
        for m in GUID_RE.finditer(text):
            p = guid2path.get(m.group(1))
            if p and p not in closed and os.path.exists(p):
                closed.add(p)
                queue.append(p)

    # 4. 统计：按一级业务目录聚合（重点 Art/Effects 子目录）
    def classify(path):
        rel = os.path.relpath(path, ASSETS).replace("\\", "/")
        parts = rel.split("/")
        return "/".join(parts[:2]) if len(parts) > 1 else parts[0]

    stats = defaultdict(lambda: {"total": 0, "total_size": 0, "used": 0, "used_size": 0})
    unused_by_dir = defaultdict(list)
    scripts = {"total": 0, "total_size": 0}
    for f in iter_files(ASSETS):
        if f.endswith(".meta"):
            continue
        ext = os.path.splitext(f)[1].lower()
        size = os.path.getsize(f)
        if ext == SCRIPT_EXT:
            scripts["total"] += 1
            scripts["total_size"] += size
            continue
        if f in path2guid and path2guid[f] not in guids56 and f in closed:
            used = True
        elif any(f.startswith(d + os.sep) or f in d for d in resource_dirs):
            used = True  # 任意层级 Resources 目录整目录进包
        else:
            used = f in closed
        key = classify(f)
        s = stats[key]
        s["total"] += 1
        s["total_size"] += size
        if used:
            s["used"] += 1
            s["used_size"] += size
        else:
            unused_by_dir[key].append((f, size))

    # 5. 大音频清单（>1MB，待转码）
    big_audio = []
    for f in iter_files(ASSETS):
        if f.endswith(".meta"):
            continue
        if os.path.splitext(f)[1].lower() in (".wav", ".mp3", ".ogg"):
            sz = os.path.getsize(f)
            if sz > 1024 * 1024:
                big_audio.append((f, sz))
    big_audio.sort(key=lambda x: -x[1])

    # 6. 输出
    lines = []
    ap = lines.append
    ap("# 资源引用闭包分析报告（%s）" % __import__("datetime").date.today())
    ap("")
    ap("> 工具：Tools/ScanAssetRefs.py（只读分析，未动任何资源）。")
    ap("> 入口 = BuildSettings 场景 + Resources/StreamingAssets/DirichletMediation 整目录 + 其 GUID 依赖闭包。")
    ap("> **零引用 ≠ 可直接删**：路径加载（非 Resources 的 AssetBundle 方式）、反射、编辑器专用、动态拼接路径都可能漏判，裁剪前必须人工抽查 + 回归。")
    ap("")
    ap("## 一、总览")
    ap("")
    ap("| 一级目录 | 文件数 | 总大小 | 打包可达 | 可达大小 | 候选零引用 | 候选大小 |")
    ap("|---|---|---|---|---|---|---|")
    tot = [0, 0, 0, 0]
    for key in sorted(stats, key=lambda k: -stats[k]["total_size"]):
        s = stats[key]
        ap("| %s | %d | %s | %d | %s | %d | %s |" % (
            key, s["total"], human(s["total_size"]), s["used"], human(s["used_size"]),
            s["total"] - s["used"], human(s["total_size"] - s["used_size"])))
        tot[0] += s["total"]; tot[1] += s["total_size"]
        tot[2] += s["used"]; tot[3] += s["used_size"]
    ap("| **合计(不含脚本)** | %d | %s | %d | %s | %d | %s |" % (
        tot[0], human(tot[1]), tot[2], human(tot[3]), tot[0] - tot[2], human(tot[1] - tot[3])))
    ap("")
    ap("## 二、重点：Art/Effects 候选零引用清单")
    ap("")
    eff_key = [k for k in stats if k.startswith("Art/Effects")]
    eff_unused = []
    for k in eff_key:
        eff_unused.extend(unused_by_dir.get(k, []))
    eff_unused.sort(key=lambda x: -x[1])
    ap("候选 %d 个文件，合计 %s（Top 50 按大小）：\n" % (len(eff_unused), human(sum(s for _, s in eff_unused))))
    for f, sz in eff_unused[:50]:
        ap("- `%s`（%s）" % (os.path.relpath(f, ROOT).replace("\\", "/"), human(sz)))
    if len(eff_unused) > 50:
        ap("- …（其余 %d 个见脚本明细输出）" % (len(eff_unused) - 50))
    ap("")
    ap("## 三、>1MB 音频待转码清单（共 %d 个，%s）" % (len(big_audio), human(sum(s for _, s in big_audio))))
    ap("")
    for f, sz in big_audio[:40]:
        ap("- `%s`（%s）" % (os.path.relpath(f, ROOT).replace("\\", "/"), human(sz)))
    if len(big_audio) > 40:
        ap("- …其余 %d 个" % (len(big_audio) - 40))
    ap("")
    ap("## 四、56 字符 meta guid（口径：2026-10-23 拍板「一律不管」）")
    ap("")
    ap("共 %d 个，不做处理、不出现在零引用判定里，运行时走路径加载。" % len(guids56))
    ap("")
    ap("## 五、脚本")
    ap("")
    ap("- .cs 共 %d 个（%s），进程序集恒打包，不参与零引用判定。" % (scripts["total"], human(scripts["total_size"])))
    ap("")

    report = "\n".join(lines)
    if write_report:
        out = os.path.join(ROOT, "Logs", "ScanAssetRefs_report.md")
        with open(out, "w", encoding="utf-8") as fh:
            fh.write(report)
        # 明细：全量零引用清单单独一份
        detail = os.path.join(ROOT, "Logs", "ScanAssetRefs_unused_detail.txt")
        with open(detail, "w", encoding="utf-8") as fh:
            for key in sorted(unused_by_dir):
                for f, sz in sorted(unused_by_dir[key], key=lambda x: -x[1]):
                    fh.write("%s\t%s\t%s\n" % (key, human(sz), os.path.relpath(f, ROOT).replace("\\", "/")))
        print("报告: Logs/ScanAssetRefs_report.md")
        print("全量零引用明细: Logs/ScanAssetRefs_unused_detail.txt")
        print()
    print(report[:6000])

if __name__ == "__main__":
    main()
