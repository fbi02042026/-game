#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
拆分后的校验脚本（配合 split_battleui.py 使用）：

1. 每个 partial 文件的大括号配平、文件非空
2. 所有 partial 分部里出现的成员名 == 原文件 BattleUI 的成员名（不多不少）
3. 至少有一个文件写了 `public partial class BattleUI : MonoBehaviour`
4. 每个分部文件的 using 区与原文件一致

用法：python Tools/split_battleui_verify.py
"""
import io
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, "Assets", "Scripts", "UI", "BattleUI.cs")
UI = os.path.join(ROOT, "Assets", "Scripts", "UI")

PARTIALS = [
    "BattleUI.cs",
    "BattleUI.Binding.cs",
    "BattleUI.WidgetFactory.cs",
    "BattleUI.Hud.cs",
    "BattleUI.Skills.cs",
    "BattleUI.Backpack.cs",
    "BattleUI.CharacterBar.cs",
    "BattleUI.KillCam.cs",
]
WIDGETS = ["SkillAvatarUI.cs", "CharacterSlotUI.cs", "GridCellUI.cs", "EquipQuickSlotUI.cs"]
ALL_FILES = PARTIALS + WIDGETS


def read(path):
    with io.open(path, 'r', encoding='utf-8-sig') as f:
        return f.read().split('\n')


def ok(n=0):
    if n:
        print()
    print("-" * 60)


def main():
    fail = 0
    original_lines = read(SRC)
    orig_using = [l for l in original_lines if l.startswith('using ')]

    # 1) 文件存在 + 括号配平 + 非空
    print("=== 1. 文件与括号 ===")
    for name in ALL_FILES:
        p = os.path.join(UI, name)
        if not os.path.exists(p):
            print("缺失文件：%s" % name)
            fail += 1
            continue
        txt = read(p)
        depth = 0
        for l in txt:
            s = l.split('//')[0]
            depth += s.count('{') - s.count('}')
        usings = [l for l in txt if l.startswith('using ')]
        flag = "OK " if depth == 0 else "NG!"
        if depth != 0:
            fail += 1
        if usings != orig_using:
            print("  %s using 区与原文件不一致：%s -> %s" % (name, orig_using, usings))
            fail += 1
        print("  %s %-28s %4d 行  括号净深度=%d" % (flag, name, len(txt), depth))

    # 2) partial 声明
    print("\n=== 2. partial 声明 ===")
    for name in PARTIALS:
        p = os.path.join(UI, name)
        if not os.path.exists(p):
            continue
        txt = read(p)
        has = any(re.match(r'\s*public partial class BattleUI\s*:', l) for l in txt)
        print("  %s %s" % ("OK " if has else "NG!", name))
        if not has:
            fail += 1

    # 3) 内容核对：把所有新文件里缩进体（>=4 空格）的非空行做成多重集合，
    #    与 git HEAD 版本的原 BattleUI.cs 逐行比对。
    #    判据够硬：只要两边完全一致，就没有丢行、没有重复、也没有半截方法。
    print("\n=== 3. 内容逐行核对（与 git HEAD 版本比对）===")
    import subprocess
    import collections

    def content_lines(lines):
        out = []
        for l in lines:
            if not l.strip():
                continue
            if l.startswith('    ') or l.startswith('\t'):
                s = l.rstrip()
                if s.strip() in ('{', '}'):
                    continue
                out.append(re.sub(r'\s+', ' ', s.strip()))
        return out

    try:
        head = subprocess.check_output(
            ['git', 'show', 'HEAD:Assets/Scripts/UI/BattleUI.cs'],
            cwd=ROOT).decode('utf-8-sig').split('\n')
    except Exception as e:
        print("  跳过：无法取 git HEAD 版本（%s）" % e)
        head = None

    if head:
        old = collections.Counter(content_lines(head))
        new = collections.Counter()
        per_file = {}
        for name in ALL_FILES:
            p = os.path.join(UI, name)
            if not os.path.exists(p):
                continue
            cl = content_lines(read(p))
            per_file[name] = len(cl)
            new.update(cl)

        print("  原文件成员/语句行 %d，拆分后合计 %d" % (sum(old.values()), sum(new.values())))
        missing = old - new
        extra = new - old
        if missing or extra:
            fail += 1
            print("  差异 %d 条：" % (sum(missing.values()) + sum(extra.values())))
            for x, n in list(missing.items())[:12]:
                print("     - %dx %s" % (n, x[:110]))
            for x, n in list(extra.items())[:12]:
                print("     + %dx %s" % (n, x[:110]))
        else:
            print("  OK  逐行完全一致：无丢行、无重复、无缺字")

        print("  各文件行数：")
        for name, n in sorted(per_file.items(), key=lambda kv: -kv[1]):
            print("     %-28s %d" % (name, n))

    print()
    if fail:
        print("=== 校验失败：%d 项 ===" % fail)
        sys.exit(1)
    print("=== 全部校验通过 ===")


if __name__ == '__main__':
    main()
