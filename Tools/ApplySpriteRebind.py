#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""按「节点路径 → 图片」映射表回填被清空的 sprite。

设计要点
--------
Unity 会把悬空引用清成 `m_Sprite: {fileID: 0}`，原始 guid 永久丢失。
本工具**按节点层级路径定位**（路径稳定，Unity 不会改），从映射表
`Tools/sprite-rebind.json` 读取目标图片，取其**当前** .meta guid 回填。
映射表是**可重放资产**：以后再被清空，跑一次 --write 即可全部恢复。

用法
----
    python Tools/ApplySpriteRebind.py --plan            # 按内置规则生成映射表（不写盘）
    python Tools/ApplySpriteRebind.py --plan --write    # 生成映射表并落盘
    python Tools/ApplySpriteRebind.py                   # 按映射表回填（不写盘）
    python Tools/ApplySpriteRebind.py --write           # 按映射表回填并写盘（自动 .bak）
"""
import json
import os
import re
import sys

ROOT = os.path.dirname(os.path.abspath(__file__))
PROJ = os.path.dirname(ROOT)
MAP_FILE = os.path.join(ROOT, "sprite-rebind.json")
EMPTY_SPRITE = "  m_Sprite: {fileID: 0}"

DOC_RE = re.compile(r"^--- !u!(\d+) &(-?\d+)")


def unescape(s):
    """Unity YAML 把非 ASCII 写成 \\uXXXX，需解码后再比对中文节点名"""
    return re.sub(r"\\u([0-9a-fA-F]{4})",
                  lambda m: chr(int(m.group(1), 16)), s)

# ---------------------------------------------------------------- 内置规则
# 按「节点路径末段」匹配；返回图片相对路径，None 表示跳过（如运行时代码赋值）
def pick_image(node_path):
    """【已废弃】按节点名猜图的规则——实践证明必错，全部停用。

    2026-09-15 曾用它给 PlayerJobSelect 配了 27 处，用户反馈"配的全是错的"。
    原因：同名节点的实际用图各不相同（例如 3 张职业卡的 Icon 是 3 张不同的立绘，
    却都被猜成同一张），尺寸与风格也对不上。

    正确做法：在 Unity 里人工拖图 → 用 DumpSpriteMapping.py 导出真实映射
    → 本脚本只负责按映射表重放。**不要重新启用猜测逻辑。**
    """
    return None


TARGETS = ["Assets/Resources/Prefabs/UI/PlayerJobSelect.prefab"]


# ---------------------------------------------------------------- YAML 解析
def parse_structure(lines):
    """返回 (doc_spans, go_name, tr_of_go, go_of_tr, father)
    doc_spans: [(start_line, end_line, fileID)]"""
    starts = []
    for i, ln in enumerate(lines):
        m = DOC_RE.match(ln)
        if m:
            starts.append((i, int(m.group(2))))
    spans = []
    for k, (s, fid) in enumerate(starts):
        e = starts[k + 1][0] if k + 1 < len(starts) else len(lines)
        spans.append((s, e, fid))

    go_name, tr_of_go, go_of_tr, father = {}, {}, {}, {}
    for s, e, fid in spans:
        body = "\n".join(lines[s:e])
        cm = DOC_RE.match(lines[s])
        cid = int(cm.group(1))
        if cid == 1:
            m = re.search(r"^  m_Name:\s*(.*)$", body, re.M)
            go_name[fid] = (unescape(m.group(1).strip().strip('"')) if m else "?")
        elif cid in (4, 224):
            m = re.search(r"^  m_GameObject:\s*\{fileID:\s*(-?\d+)", body, re.M)
            if m:
                tr_of_go[int(m.group(1))] = fid
                go_of_tr[fid] = int(m.group(1))
            f = re.search(r"^  m_Father:\s*\{fileID:\s*(-?\d+)", body, re.M)
            father[fid] = int(f.group(1)) if f else 0
    return spans, go_name, tr_of_go, go_of_tr, father


def collect_empty(lines):
    """返回 [(行号, 节点路径)]：prefab 里所有 m_Sprite:{fileID:0} 的位置"""
    spans, go_name, tr_of_go, go_of_tr, father = parse_structure(lines)

    def path_of(go_fid):
        tr = tr_of_go.get(go_fid)
        chain, cur, seen = [], tr, set()
        while cur and cur not in seen:
            seen.add(cur)
            g = go_of_tr.get(cur)
            if g is None:
                break
            chain.append(go_name.get(g, "?"))
            cur = father.get(cur, 0)
        return "/".join(reversed(chain)) if chain else go_name.get(go_fid, "?")

    res = []
    for s, e, fid in spans:
        for i in range(s, e):
            if lines[i] != EMPTY_SPRITE:
                continue
            # 该 doc 的 m_GameObject
            gm = None
            for j in range(s, e):
                m = re.match(r"^  m_GameObject:\s*\{fileID:\s*(-?\d+)", lines[j])
                if m:
                    gm = int(m.group(1))
                    break
            if gm is None:
                continue
            res.append((i, path_of(gm)))
    return res


def meta_info(rel_path):
    """返回 (guid, spriteMode)"""
    p = os.path.join(PROJ, rel_path + ".meta")
    if not os.path.isfile(p):
        return None, None
    with open(p, encoding="utf-8", errors="ignore") as fh:
        t = fh.read()
    g = re.search(r"^guid:\s*(\S+)", t, re.M)
    sm = re.search(r"^spriteMode:\s*(\d+)", t, re.M)
    tt = re.search(r"^textureType:\s*(\d+)", t, re.M)
    return (g.group(1) if g else None), {
        "spriteMode": sm.group(1) if sm else "?",
        "textureType": tt.group(1) if tt else "?",
    }


# ---------------------------------------------------------------- 主流程
def build_plan():
    plan = {}
    for rel in TARGETS:
        full = os.path.join(PROJ, rel)
        if not os.path.isfile(full):
            print("缺文件: %s" % rel)
            continue
        with open(full, encoding="utf-8", newline="") as fh:
            lines = fh.read().split("\n")
        empties = collect_empty(lines)
        m = {}
        for _ln, npath in empties:
            img = pick_image(npath)
            m[npath] = img if img else ""    # "" = 跳过
        plan[rel] = m
    return plan


def apply(plan, do_write):
    changed = 0
    filled = 0
    for rel, mapping in plan.items():
        full = os.path.join(PROJ, rel)
        with open(full, encoding="utf-8", newline="") as fh:
            text = fh.read()
        lines = text.split("\n")
        empties = collect_empty(lines)
        if not empties:
            continue

        guid_cache = {}
        hit = 0
        for ln, npath in empties:
            img = mapping.get(npath, "")
            if not img:
                continue
            if img not in guid_cache:
                g, info = meta_info(img)
                if not g:
                    print("  !! 图片缺失: %s" % img)
                    guid_cache[img] = None
                else:
                    if info["spriteMode"] not in ("1", "?"):
                        print("  ~ 注意 %s 为多图模式(spriteMode=%s)，fileID 可能需要调整"
                              % (img, info["spriteMode"]))
                    guid_cache[img] = g
            g = guid_cache.get(img)
            if not g:
                continue
            lines[ln] = "  m_Sprite: {fileID: 21300000, guid: %s, type: 3}" % g
            hit += 1

        if hit == 0:
            continue
        new_text = "\n".join(lines)
        print("%s: 回填 %d 处" % (rel, hit))
        filled += hit
        changed += 1
        if do_write:
            with open(full + ".bak", "w", encoding="utf-8", newline="") as fh:
                fh.write(text)
            with open(full, "w", encoding="utf-8", newline="") as fh:
                fh.write(new_text)
    print("\n合计回填 %d 处 / %d 个文件%s"
          % (filled, changed, "" if do_write else "（预览，未写盘）"))
    return 0


def main():
    do_write = "--write" in sys.argv
    do_plan = "--plan" in sys.argv

    if do_plan:
        plan = build_plan()
        with open(MAP_FILE, "w", encoding="utf-8") as fh:
            json.dump(plan, fh, ensure_ascii=False, indent=2)
        print("映射表已生成: %s" % MAP_FILE)
        for rel, m in plan.items():
            yes = sum(1 for v in m.values() if v)
            print("  %s: %d 处，其中 %d 处有目标图，%d 处跳过"
                  % (rel, len(m), yes, len(m) - yes))
        if not do_write:
            return 0

    if not os.path.isfile(MAP_FILE):
        print("映射表不存在，请先跑 --plan")
        return 1
    with open(MAP_FILE, encoding="utf-8") as fh:
        plan = json.load(fh)
    return apply(plan, do_write)


if __name__ == "__main__":
    sys.exit(main())
