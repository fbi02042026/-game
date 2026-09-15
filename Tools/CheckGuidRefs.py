#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
guid 完整性校验器（本工程专用）—— 基线制，只拦「新增」悬空

背景（务必先读，否则会重犯历史错误）：
- 本工程用【团结引擎】。团结给「新导入资源」分配 **base64 形态 guid**，是引擎正常行为，
  **不是损坏**。因此 32 位 hex 与 base64 **都合法，绝不能互转**；互转只会让引用悬空
  （越修越坏，见已废弃的 Tools/FixAllCorruptedGuids.ps1）。
- 真正要防的是「**引用悬空**」：prefab/scene/asset 里的 `guid: X` 在本工程
  （Assets + Packages + Library/PackageCache）找不到对应 .meta。

基线制：
- `Tools/guid-baseline.txt` 记录「已知的历史悬空」(file|guid)。
- 默认只报「不在基线里的新增悬空」→ 历史包袱不阻塞提交，新引入的破坏立刻拦截。
- `--update-baseline` 把当前全部悬空写进基线（处理完一批后刷新用）。

用法：
    python Tools/CheckGuidRefs.py                  # 报告 + 写 Tools/guid-audit-report.txt
    python Tools/CheckGuidRefs.py --quiet          # 只返回码（供 git hook）
    python Tools/CheckGuidRefs.py --update-baseline
    python Tools/CheckGuidRefs.py --strict          # 第三方 Demo 目录也算
返回码：0 = 无新增问题；1 = 有真正非法 .meta 或**新增**悬空引用
"""
from __future__ import annotations

import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
META_DIRS = [os.path.join(ROOT, "Assets"),
             os.path.join(ROOT, "Packages"),
             os.path.join(ROOT, "Library", "PackageCache")]
BASELINE = os.path.join(ROOT, "Tools", "guid-baseline.txt")

REF_EXT = {
    ".prefab", ".unity", ".asset", ".mat", ".controller", ".anim", ".overrideController",
    ".physicMaterial", ".physicsMaterial2D", ".guiskin", ".mask", ".playable", ".mixer",
    ".spriteatlas", ".spriteatlasv2", ".shader", ".shadergraph", ".shadersubgraph", ".compute",
    ".cginc", ".hlsl", ".uss", ".uxml", ".inputactions", ".asmdef", ".asmref", ".preset",
}

OWN_GUID = re.compile(rb"^guid:[ \t]*([^\r\n]+)", re.M)
REF_GUID = re.compile(rb"guid:[ \t]*([^\r\n,}\s]+)")
HEX32 = re.compile(r"^[0-9a-f]{32}$")
B64 = re.compile(r"^[A-Za-z0-9+/]{16,}={0,2}$")
BUILTIN = re.compile(r"^0{8}[0-9a-f]{24}$")

THIRD_PARTY_HINTS = ("\\Art\\Effects\\", "\\SPUM\\")


def scan_meta():
    guids, bad = set(), []
    for base in META_DIRS:
        if not os.path.isdir(base):
            continue
        for dp, _dn, fn in os.walk(base):
            for f in fn:
                if not f.endswith(".meta"):
                    continue
                p = os.path.join(dp, f)
                try:
                    with open(p, "rb") as fh:
                        head = fh.read(400)
                except OSError:
                    continue
                m = OWN_GUID.search(head)
                if not m:
                    continue
                g = m.group(1).strip().decode("ascii", "replace")
                if HEX32.match(g) or B64.match(g):
                    guids.add(g)
                else:
                    bad.append((p, g))
    return guids, bad


def scan_refs(guids):
    dangling = {}
    total = 0
    for dp, _dn, fn in os.walk(os.path.join(ROOT, "Assets")):
        for f in fn:
            if os.path.splitext(f)[1].lower() not in REF_EXT:
                continue
            p = os.path.join(dp, f)
            try:
                if os.path.getsize(p) > 8_000_000:
                    continue
                with open(p, "rb") as fh:
                    data = fh.read()
            except OSError:
                continue
            if b"guid:" not in data:
                continue
            rel = os.path.relpath(p, ROOT)
            for m in REF_GUID.finditer(data):
                g = m.group(1).decode("ascii", "replace")
                if not (HEX32.match(g) or B64.match(g)) or BUILTIN.match(g):
                    continue
                total += 1
                if g not in guids:
                    dangling["%s|%s" % (rel, g)] = rel
    return dangling, total


def load_baseline() -> set:
    if not os.path.isfile(BASELINE):
        return set()
    with open(BASELINE, encoding="utf-8") as fh:
        return {ln.strip() for ln in fh if ln.strip()}


def save_baseline(keys) -> None:
    with open(BASELINE, "w", encoding="utf-8", newline="\n") as fh:
        for k in sorted(keys):
            fh.write(k + "\n")


def main() -> int:
    quiet = "--quiet" in sys.argv
    strict = "--strict" in sys.argv
    guids, bad = scan_meta()
    dangling, total = scan_refs(guids)

    if "--update-baseline" in sys.argv:
        save_baseline(dangling.keys())
        print("基线已更新：%d 条 -> Tools/guid-baseline.txt" % len(dangling))
        return 0

    base = load_baseline()
    new_keys = [k for k in dangling if k not in base]
    game_new = [k for k in new_keys
                if not any(h in dangling[k] for h in THIRD_PARTY_HINTS)]
    third_all = [k for k in dangling if any(h in dangling[k] for h in THIRD_PARTY_HINTS)]

    lines = ["meta 合法 guid=%d  非法 .meta=%d" % (len(guids), len(bad))]
    for p, g in bad:
        lines.append("  BAD_META %s  guid=%s" % (os.path.relpath(p, ROOT), g[:40]))
    lines.append("引用总数=%d  悬空总计=%d（基线 %d，新增 %d）  第三方=%d"
                 % (total, len(dangling), len(base), len(new_keys), len(third_all)))
    if new_keys:
        lines.append("— 新增悬空（会被拦截）—")
    for k in sorted(new_keys):
        lines.append("  NEW_DANGLING %s" % k)

    report = "\n".join(lines)
    if not quiet:
        print(report)
        with open(os.path.join(ROOT, "Tools", "guid-audit-report.txt"),
                  "w", encoding="utf-8", newline="\n") as fh:
            fh.write(report + "\n")
        print("\n报告已写入 Tools/guid-audit-report.txt")

    fail = bool(bad) or bool(game_new) or (strict and bool(third_all))
    return 1 if fail else 0


if __name__ == "__main__":
    sys.exit(main())
