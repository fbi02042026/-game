#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
城镇背包格子对齐到 4 列 × 3 行（12 格）。

背景：美术把 UI 改成了 4×3，GameConfig.BACKPACK_WIDTH/HEIGHT 也跟着改成 4/3，
但 Assets/Resources/Prefabs/UI/CharacterUI.prefab 里还手摆着 40 个格子
（Cell_0_0 ~ Cell_7_4，GridLayoutGroup.m_ConstraintCount = 8）。
不改的话动态建格 systems 按 4 列算，手摆的格子按 8 列排，城镇界面会溢出。

做法：把 x>=4 或 y>=3 的 Cell_x_y 整块删掉（GameObject + Transform + 它的所有
组件文档 + 父节点 RectTransform.m_Children 里的引用），并把 GridLayoutGroup 的
m_ConstraintCount 改成 4。

用法：
    python Tools/town_backpack_to_4x3.py            # 预演
    python Tools/town_backpack_to_4x3.py --write    # 落盘（自动备份同目录 .bak）
"""
import io
import os
import re
import sys
import shutil

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
# 城镇角色界面的预制体；也可以直接把路径作为第一个参数传进来
PREFAB_CANDIDATES = [
    os.path.join(ROOT, "Assets", "Resources", "Prefabs", "Town", "CharacterUI.prefab"),
    os.path.join(ROOT, "Assets", "Resources", "Prefabs", "UI", "CharacterUI.prefab"),
]
KEEP_W, KEEP_H = 4, 3


def resolve_prefab():
    for arg in sys.argv[1:]:
        if not arg.startswith('--'):
            return arg
    for p in PREFAB_CANDIDATES:
        if os.path.exists(p):
            return p
    return PREFAB_CANDIDATES[0]


def split_documents(text):
    """按 Unity YAML 的 `--- !u!xx &id` 切成文档。返回 [(header_line, body_str, start, end)]"""
    lines = text.split('\n')
    docs = []
    cur_header = None
    cur_start = 0
    cur = []
    for i, l in enumerate(lines):
        if l.startswith('--- !u!'):
            if cur_header is not None:
                docs.append((cur_header, '\n'.join(cur), cur_start, i))
            cur_header = l
            cur_start = i
            cur = []
        elif cur_header is not None:
            cur.append(l)
    if cur_header is not None:
        docs.append((cur_header, '\n'.join(cur), cur_start, len(lines)))
    return docs


def doc_id(header):
    m = re.search(r'&(-?\d+)', header)
    return m.group(1) if m else None


def doc_name(body):
    m = re.search(r'^\s*m_Name:\s*(.+)$', body, re.M)
    if not m:
        return None
    return m.group(1).strip()


def main():
    write = '--write' in sys.argv
    global PREFAB
    PREFAB = resolve_prefab()
    print("目标预制体：%s" % PREFAB)
    with io.open(PREFAB, 'r', encoding='utf-8-sig') as f:
        text = f.read()

    docs = split_documents(text)
    print("预制体文档总数：%d" % len(docs))

    # 1) 找出所有 Cell_x_y，判定保留还是删除
    cell_docs = {}   # name -> (index, docId)
    for idx, (header, body, s, e) in enumerate(docs):
        n = doc_name(body)
        if n and re.match(r'^Cell_\d+_\d+$', n):
            cell_docs[n] = (idx, doc_id(header))

    print("找到格子 %d 个" % len(cell_docs))
    drop_names, keep_names = [], []
    for n, (idx, did) in cell_docs.items():
        parts = n.split('_')
        x, y = int(parts[1]), int(parts[2])
        (keep_names if (x < KEEP_W and y < KEEP_H) else drop_names).append(n)
    print("保留 %d 个（4×3），待删 %d 个" % (len(keep_names), len(drop_names)))
    if not drop_names:
        print("无需改动")
        return

    drop_ids = set(cell_docs[n][1] for n in drop_names)

    # 2) 连带删除，并一路向下级联：
    #    - 组件的 m_GameObject 指向被删 GameObject  → 一起删
    #    - Transform 的 m_Father 指向被删 Transform → 一起删（格子里的图标/边框等子节点）
    #    - Transform 被删后，它挂的那个 GameObject 也要删
    all_drop = set(drop_ids)
    while True:
        added = 0
        for idx, (header, body, s, e) in enumerate(docs):
            did = doc_id(header)
            if did in all_drop:
                # 反向：被删文档所挂的 GameObject 也要删（Transform/组件都归它）
                m3 = re.search(r'm_GameObject:\s*\{fileID:\s*(-?\d+)\}', body)
                if m3 and m3.group(1) not in all_drop:
                    all_drop.add(m3.group(1))
                    added += 1
                continue
            m = re.search(r'm_GameObject:\s*\{fileID:\s*(-?\d+)\}', body)
            if m and m.group(1) in all_drop:
                all_drop.add(did)
                added += 1
                continue
            m2 = re.search(r'm_Father:\s*\{fileID:\s*(-?\d+)\}', body)
            if m2 and m2.group(1) in all_drop:
                all_drop.add(did)
                added += 1
        if added == 0:
            break
        print("  级联新增待删文档 %d 个（累计 %d）" % (added, len(all_drop)))
    print("连带删除的文档 %d 个（含格子节点下的图标/边框等）" % (len(all_drop) - len(drop_ids)))

    # 3) 从父文档的 m_Children 列表里摘掉（注意：Transform.m_Children 里记的是
    #    子节点的 Transform fileID，不是 GameObject fileID，所以要用 all_drop 判）
    removed_refs = 0
    new_docs = []
    for idx, (header, body, s, e) in enumerate(docs):
        did = doc_id(header)
        if did in all_drop:
            continue
        if 'm_Children:' in body:
            def repl(mm, _drop=all_drop):
                return '' if mm.group(1) in _drop else mm.group(0)
            body2 = re.sub(r'^\s*- \{fileID: (-?\d+)\}\n', repl, body, flags=re.M)
            if body2 != body:
                removed_refs += body.count('- {fileID:') - body2.count('- {fileID:')
                new_docs.append((header, body2, s, e))
                continue
        new_docs.append((header, body, s, e))

    # 4) GridLayoutGroup 约束列数
    touched_layout = 0
    final_docs = []
    for (header, body, s, e) in new_docs:
        if re.search(r'm_ConstraintCount:\s*\d+', body):
            old = re.search(r'm_ConstraintCount:\s*(\d+)', body).group(1)
            if old != str(KEEP_W):
                body = re.sub(r'm_ConstraintCount:\s*\d+', 'm_ConstraintCount: %d' % KEEP_W, body)
                touched_layout += 1
        final_docs.append((header, body, s, e))

    out = '\n'.join(header + '\n' + body.rstrip('\n') for (header, body, s, e) in final_docs)
    if not out.endswith('\n'):
        out += '\n'

    print("已从父节点摘除子节点引用 %d 条，改 GridLayoutGroup %d 处" % (removed_refs, touched_layout))
    print("文档数 %d -> %d" % (len(docs), len(final_docs)))

    # 5) 校验：剩下的格子正好 12 个且坐标连续
    rem_docs = split_documents(out)
    rem_cells = []
    for (h, b, s, e) in rem_docs:
        n = doc_name(b)
        if n and re.match(r'^Cell_\d+_\d+$', n):
            rem_cells.append(n)
    coords = sorted((int(n.split('_')[1]), int(n.split('_')[2])) for n in rem_cells)
    expect = sorted((x, y) for y in range(KEEP_H) for x in range(KEEP_W))
    ok_cells = coords == expect
    print("剩余格子 %d 个，坐标集合正确=%s" % (len(rem_cells), ok_cells))
    leftovers = [d for d in all_drop if re.search(r'fileID: %s[,}]' % d, out)]
    print("删后是否还有悬空引用指向已删节点：%s%s" % ("有！" if leftovers else "无",
                                                    (" -> " + str(leftovers[:3])) if leftovers else ""))
    if leftovers:
        out_lines = out.split('\n')
        for d in leftovers[:3]:
            print("  残留 ID %s 的引用位置：" % d)
            shown = 0
            for i, l in enumerate(out_lines):
                if re.search(r'fileID: %s[,}]' % d, l):
                    head = ''
                    for j in range(i, -1, -1):
                        if out_lines[j].startswith('--- !u!'):
                            head = out_lines[j][:40]
                            break
                    print("     %-52s  所在文档: %s" % (l.strip()[:52], head))
                    shown += 1
                    if shown >= 4:
                        break

    if not ok_cells or leftovers:
        print("校验未通过，不落盘")
        sys.exit(1)

    if not write:
        print("\n预演结束，加 --write 落盘")
        return

    shutil.copyfile(PREFAB, PREFAB + '.bak')

    # Unity 文本序列化必须有这两行头指令（%TAG 用来解析 !u! 标签）。
    # 丢掉它们会让 Unity 报 "File may be corrupted or was serialized with a newer version of Unity"。
    prologue = []
    for l in text.split('\n'):
        if l.startswith('%'):
            prologue.append(l)
        elif l.strip() == '':
            continue
        else:
            break
    if prologue:
        out = '\n'.join(prologue) + '\n' + out

    # 保持原文件的换行风格（Windows 下 Unity 写的是 CRLF）
    nl = '\r\n' if '\r\n' in text else '\n'
    with io.open(PREFAB, 'w', encoding='utf-8', newline='') as f:
        f.write(out.replace('\n', nl))
    print("\n已写入 %s（备份 %s.bak，头部指令 %d 行，换行=%s）"
          % (PREFAB, os.path.basename(PREFAB), len(prologue), 'CRLF' if nl == '\r\n' else 'LF'))


if __name__ == '__main__':
    main()
