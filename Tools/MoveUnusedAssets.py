#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
MoveUnusedAssets.py — 把 ScanAssetRefs.py 判定的候选零引用资源搬到工程外备份区
（只挪不删；带 manifest 可整体回滚；56 字符 guid 文件跳过只报告，遵守 AGENTS.md §2.3）

用法：
  python Tools/MoveUnusedAssets.py                # dry-run，只统计
  python Tools/MoveUnusedAssets.py --go Art/Effects   # 真正执行指定组
  python Tools/MoveUnusedAssets.py --undo Logs/MoveUnusedAssets_manifest_*.txt  # 按 manifest 回滚

数据源：Logs/ScanAssetRefs_unused_detail.txt（组名\\t大小\\t相对路径）
备份区：E:/xiangsumaoxian_backup_assets/（工程外，保持 Assets/ 相对结构）
"""

import os
import re
import sys
import glob
import shutil

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DETAIL = os.path.join(ROOT, "Logs", "ScanAssetRefs_unused_detail.txt")
BACKUP_ROOT = "E:/xiangsumaoxian_backup_assets"
# 【2026-11-01 修复】团结对重复内容会把 meta guid 重签成 55字符+'=' 的 56 字符长串，
# 原来按"整56字符"匹配全部漏过（892 个被误搬）。改为：guid 非 hex32 一律跳过不搬。
HEX32 = re.compile(r"^[0-9a-f]{32}$")


def load_groups():
    groups = {}
    with open(DETAIL, "r", encoding="utf-8") as fh:
        for line in fh:
            parts = line.rstrip("\n").split("\t")
            if len(parts) != 3:
                continue
            grp, _size, rel = parts
            groups.setdefault(grp, []).append(rel.replace("/", os.sep))
    return groups


def plan_group(rels):
    move, skip56, missing = [], [], []
    for rel in rels:
        src = os.path.join(ROOT, rel)
        meta = src + ".meta"
        if not os.path.exists(src):
            missing.append(rel)
            continue
        if os.path.exists(meta):
            try:
                with open(meta, "r", encoding="utf-8", errors="ignore") as fh:
                    m = re.search(r"guid:\s*(\S+)", fh.read(4096))
                    if m and not HEX32.match(m.group(1)):
                        skip56.append(rel)
                        continue
            except OSError:
                pass
        move.append(rel)
    return move, skip56, missing


def do_move(rels):
    manifest = []
    for rel in rels:
        src = os.path.join(ROOT, rel)
        dst = os.path.join(BACKUP_ROOT, rel)
        os.makedirs(os.path.dirname(dst), exist_ok=True)
        shutil.move(src, dst)
        manifest.append(rel)
        msrc = src + ".meta"
        if os.path.exists(msrc):
            shutil.move(msrc, dst + ".meta")
            manifest.append(rel + ".meta")
    return manifest


def prune_empty(base_rel):
    """组目录（相对 Assets/）下搬空的目录连同 .meta 一起收进备份区（仍属'挪'）"""
    base = os.path.join(ROOT, "Assets", base_rel)
    pruned = []
    if not os.path.isdir(base):
        return pruned
    for dp, _dns, _fns in os.walk(base, topdown=False):
        if dp == base:
            continue
        try:
            if os.listdir(dp):
                continue  # 目录非空，跳过
        except OSError:
            continue
        rel = os.path.relpath(dp, ROOT)
        m = dp + ".meta"
        if os.path.exists(m):
            dstm = os.path.join(BACKUP_ROOT, os.path.relpath(m, ROOT))
            os.makedirs(os.path.dirname(dstm), exist_ok=True)
            shutil.move(m, dstm)
            pruned.append(rel + ".meta")
        os.rmdir(dp)
        pruned.append(rel + os.sep)
    return pruned


def main():
    args = sys.argv[1:]
    if "--prune-empty" in args:
        i = args.index("--prune-empty")
        for grp in args[i + 1:]:
            pruned = prune_empty(grp)
            print("[%s] 收走空目录/meta %d 项" % (grp, len(pruned)))
        return

    if "--undo" in args:
        i = args.index("--undo")
        mf = glob.glob(args[i + 1])[-1] if glob.glob(args[i + 1]) else args[i + 1]
        with open(mf, "r", encoding="utf-8") as fh:
            for rel in fh.read().splitlines():
                bsrc = os.path.join(BACKUP_ROOT, rel)
                dst = os.path.join(ROOT, rel)
                if os.path.exists(bsrc):
                    os.makedirs(os.path.dirname(dst), exist_ok=True)
                    shutil.move(bsrc, dst)
        print("已按 manifest 回滚:", mf)
        return

    groups = load_groups()
    go = "--go" in args
    targets = [a for a in args[args.index("--go") + 1:]] if go else list(groups)

    all_manifest = []
    for grp in targets:
        rels = groups.get(grp, [])
        move, skip56, missing = plan_group(rels)
        print("[%s] 计划搬 %d 个 | 56字符跳过 %d | 已缺失 %d" % (grp, len(move), len(skip56), len(missing)))
        for r in skip56[:10]:
            print("  [56字符不搬]", r)
        if not go:
            total = sum(os.path.getsize(os.path.join(ROOT, r)) for r in move if os.path.exists(os.path.join(ROOT, r)))
            print("  dry-run 预计搬出 %.1f MB" % (total / 1048576.0))
            continue
        all_manifest.extend(do_move(move))

    if go and all_manifest:
        os.makedirs(os.path.dirname(os.path.join(BACKUP_ROOT, "x")), exist_ok=True)
        out = os.path.join(ROOT, "Logs", "MoveUnusedAssets_manifest_20261101.txt")
        with open(out, "w", encoding="utf-8") as fh:
            fh.write("\n".join(all_manifest))
        print("已搬 %d 个文件(含meta)，manifest: %s" % (len(all_manifest), out))
        for grp in targets:
            pruned = prune_empty(grp)
            print("[%s] 收走空目录/meta %d 项（undo 恢复文件时会自动重建目录，无需记 manifest）" % (grp, len(pruned)))


if __name__ == "__main__":
    main()
