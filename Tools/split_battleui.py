#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
把 BattleUI.cs 按成员（而不是按行）拆成多个 partial 文件。
纯搬移：类名、字段名、方法签名一律不改，Inspector 拖拽引用不变。

用法：
    python Tools/split_battleui.py            # 预演，只打印计划
    python Tools/split_battleui.py --write    # 真正落盘

校验：
  1. 每个成员的首尾都落在类体的第 1 层括号深度上（不会出现半截方法）
  2. 所有成员的行数总和 + 头文件 == 原文件行数（无遗漏、无重复）
  3. 每个生成文件大括号配平
"""
import io
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, "Assets", "Scripts", "UI", "BattleUI.cs")
OUT_DIR = os.path.join(ROOT, "Assets", "Scripts", "UI")

HEADER_LINES = None  # 原文件 using 区，运行时填充

# 成员名 -> 目标文件（不含扩展名）。未列出的成员留在 BattleUI.cs 核心文件
MEMBER_TO_FILE = {
    # 自动绑定 / 重绑 / 自检
    "AutoBindMissingRefs": "BattleUI.Binding",
    "ReportBindingStatus": "BattleUI.Binding",
    "RebindAfterSystemsReady": "BattleUI.Binding",
    "BindProgressBar": "BattleUI.Binding",
    "ApplyFillBars": "BattleUI.Binding",
    "BindCharacterSlot": "BattleUI.Binding",
    "ConfigureFillBar": "BattleUI.Binding",
    "ApplySoloBattleHud": "BattleUI.Binding",
    "ApplySoloBattleHudPublic": "BattleUI.Binding",
    "SetSlotRootActive": "BattleUI.Binding",
    "SetAvatarRootActive": "BattleUI.Binding",
    "WireSlotSkillClicks": "BattleUI.Binding",
    "WireSlotClick": "BattleUI.Binding",
    "BindSkillAvatar": "BattleUI.Binding",

    # 槽位工厂 / 运行时建控件 / 查找工具
    "BindBottomQuickSlots": "BattleUI.WidgetFactory",
    "ResolveEquipSlotContainer": "BattleUI.WidgetFactory",
    "CountSlotLikeChildren": "BattleUI.WidgetFactory",
    "IsEquipSlotNode": "BattleUI.WidgetFactory",
    "EquipSlotTypeOf": "BattleUI.WidgetFactory",
    "BindRunSkillSlots": "BattleUI.WidgetFactory",
    "BindEquipQuickSlots": "BattleUI.WidgetFactory",
    "EnsureChildIcon": "BattleUI.WidgetFactory",
    "EnsureChildText": "BattleUI.WidgetFactory",
    "EnsureChildBar": "BattleUI.WidgetFactory",
    "FindUIText": "BattleUI.WidgetFactory",
    "FindImageNamed": "BattleUI.WidgetFactory",
    "FindImageNamedNoFallback": "BattleUI.WidgetFactory",
    "FindTextNamed": "BattleUI.WidgetFactory",
    "FindDeepChildIgnoreCase": "BattleUI.WidgetFactory",
    "FindChildIgnoreCase": "BattleUI.WidgetFactory",

    # 顶部资源 / 任务 / 金币 / 进度
    "RefreshQuestReward": "BattleUI.Hud",
    "LoadQuestGoldSprite": "BattleUI.Hud",
    "StageTypeToDifficulty": "BattleUI.Hud",
    "UpdateStageInfo": "BattleUI.Hud",
    "SetLabelWithFrameVisible": "BattleUI.Hud",
    "UpdateTopBarResources": "BattleUI.Hud",
    "UpdateGold": "BattleUI.Hud",
    "UpdateProgress": "BattleUI.Hud",
    "UpdateStageProgress": "BattleUI.Hud",
    "AttachMarkerUnder": "BattleUI.Hud",
    "UpdateQuest": "BattleUI.Hud",
    "_questGoldSprite": "BattleUI.Hud",
    "_questRewardGold": "BattleUI.Hud",

    # 技能槽 / 装备快捷槽 / 点击回调
    "UpdateRunSkillSlots": "BattleUI.Skills",
    "UpdateEquipQuickSlots": "BattleUI.Skills",
    "TickRunSkillSlots": "BattleUI.Skills",
    "UpdateSkillAvatars": "BattleUI.Skills",
    "GetMercSkillIcon": "BattleUI.Skills",
    "GetMercSkillIconAt": "BattleUI.Skills",
    "GetMercPortraitAt": "BattleUI.Skills",
    "UpdateSkillEnergy": "BattleUI.Skills",
    "BindAutoBattleUnavailable": "BattleUI.Skills",
    "OnAutoBattleUnavailableClicked": "BattleUI.Skills",
    "OnPause": "BattleUI.Skills",
    "OnPlayerSkillClick": "BattleUI.Skills",
    "OnMercSkillClick": "BattleUI.Skills",
    "_lastSkillKey": "BattleUI.Skills",

    # 网格背包
    "EnsureGridCellsBound": "BattleUI.Backpack",
    "UpdateBackpackGrid": "BattleUI.Backpack",
    "TryScreenToBackpackCell": "BattleUI.Backpack",
    "RefreshLootModeChrome": "BattleUI.Backpack",
    "EnsureBattleControls": "BattleUI.Backpack",
    "ApplyBackpackCellOccupiedColors": "BattleUI.Backpack",
    "FindGridCell": "BattleUI.Backpack",
    "FindGridCellRect": "BattleUI.Backpack",
    "OnOrganizeBackpack": "BattleUI.Backpack",
    "_backpackRowLock": "BattleUI.Backpack",

    # 角色栏 / 佣兵槽
    "UpdateCharacterSlots": "BattleUI.CharacterBar",
    "RefreshTutorialMercSlot": "BattleUI.CharacterBar",
    "RefreshTutorialMercLiveBar": "BattleUI.CharacterBar",
    "ResolveMercSlotCount": "BattleUI.CharacterBar",
    "SetupMercSlot": "BattleUI.CharacterBar",
    "FixCharacterBarLayout": "BattleUI.CharacterBar",
    "ClampCharacterBarInsideParent": "BattleUI.CharacterBar",
    "SoftFixLayoutElement": "BattleUI.CharacterBar",
    "MercLockedHint": "BattleUI.CharacterBar",

    # 击杀镜头
    "ApplyKillCamHudCompensation": "BattleUI.KillCam",
    "ResetKillCamHudCompensation": "BattleUI.KillCam",
    "_killCamHudBaseScales": "BattleUI.KillCam",
    "KillCamHudNodeNames": "BattleUI.KillCam",
}

FILE_DESC = {
    "BattleUI.Binding": "节点绑定：Awake 里按名字自动补引用、绑定自检、系统就绪后的重绑。",
    "BattleUI.WidgetFactory": "槽位工厂与查找工具：底部技能槽 / 装备槽的运行时构建，以及按名字找节点的通用方法。",
    "BattleUI.Hud": "顶部状态栏、任务面板、金币与关卡进度条。",
    "BattleUI.Skills": "底部 4 个主动技槽与 6 个装备快捷槽的刷新，以及技能/自动战斗相关的点击回调。",
    "BattleUI.Backpack": "底部网格背包：建格、占位着色、屏幕坐标拾取、拾取态外框。",
    "BattleUI.CharacterBar": "角色栏：玩家与两名佣兵的头像、血蓝条、布局修正。",
    "BattleUI.KillCam": "击杀镜头期间对 HUD 的缩放补偿（静态方法）。",
}

CLASS_TO_FILE = {
    "SkillAvatarUI": "SkillAvatarUI",
    "CharacterSlotUI": "CharacterSlotUI",
    "GridCellUI": "GridCellUI",
    "EquipQuickSlotUI": "EquipQuickSlotUI",
}

MODIFIERS = {
    "public", "private", "protected", "internal", "static", "readonly", "const",
    "virtual", "override", "sealed", "abstract", "new", "unsafe", "extern",
}


def strip_code(line):
    """去掉字符串字面量与 // 注释，用于可靠地统计大括号。"""
    out = []
    i = 0
    n = len(line)
    while i < n:
        c = line[i]
        if c == '/' and i + 1 < n and line[i + 1] == '/':
            break
        if c == '"':
            i += 1
            while i < n:
                if line[i] == '\\':
                    i += 2
                    continue
                if line[i] == '"':
                    i += 1
                    break
                i += 1
            continue
        if c == "'":
            i += 1
            while i < n:
                if line[i] == '\\':
                    i += 2
                    continue
                if line[i] == "'":
                    i += 1
                    break
                i += 1
            continue
        out.append(c)
        i += 1
    return ''.join(out)


def member_name(content_line):
    """从成员的声明首行取名字：找最靠前的 '(' '=' ';' '{'，取它前面最后一个标识符。"""
    s = content_line.split('//')[0].strip()
    cand = [p for p in (s.find('('), s.find('='), s.find(';'), s.find('{')) if p >= 0]
    head = s[:min(cand)] if cand else s
    toks = head.replace('[', ' ').replace(']', ' ').split()
    for t in reversed(toks):
        if t not in MODIFIERS:
            return t
    return s


def analyze(lines):
    """把文件切成一个个成员块。返回 [(start, end, name, kind)]，下标为 0-based，end 不含。"""
    depth = 0
    depth_at_start = []
    for idx, raw in enumerate(lines):
        depth_at_start.append(depth)
        code = strip_code(raw)
        depth += code.count('{') - code.count('}')
    if depth != 0:
        raise SystemExit("源文件大括号不配平，净深度=%d" % depth)

    # 顶层类型块
    blocks = []
    i = 0
    while i < len(lines):
        if depth_at_start[i] == 0 and re.match(r'\s*(public|internal)?\s*(sealed\s+|abstract\s+)?class\s+\w+', lines[i]):
            start = i
            # 找到本类型的 { 与本类型的 }
            j = i
            while '{' not in strip_code(lines[j]):
                j += 1
            body_depth = depth_at_start[j] + 1
            k = j + 1
            while True:
                # 类的闭合 '}' 自身开始时的深度等于类体深度（此时还没减回去）
                if depth_at_start[k] == body_depth and lines[k].strip() == '}':
                    break
                k += 1
                if k >= len(lines):
                    raise SystemExit("找不到 %s 的闭合括号" % cname)
            blocks.append((start, k + 1, re.search(r'class\s+(\w+)', lines[i]).group(1)))
            i = k + 1
        else:
            i += 1
    return blocks, depth_at_start


def carve_members(lines, blocks, depth_at_start):
    """在每个顶层类块内部再按成员切分。返回 {class: [(start,end,name)]}"""
    result = {}
    for (bstart, bend, cname) in blocks:
        # 类体从第几个 '}' 开始
        j = bstart
        while '{' not in strip_code(lines[j]):
            j += 1
        class_base = depth_at_start[j] + 1   # 类体内部的相对深度基准
        members = []
        i = j + 1
        pre_start = j + 1
        while i < bend - 1:
            code = strip_code(lines[i]).strip()
            cur_depth = depth_at_start[i] - class_base
            if cur_depth == 0 and code and not code.startswith('//'):
                # 成员首行
                start = pre_start
                # 成员延续：括号要配平，且要么以 ';' 收尾、要么以 '}' 收尾（方法体/数组初始化）
                k = i
                cur = 0
                while True:
                    code_k = strip_code(lines[k])
                    cur += (code_k.count('{') + code_k.count('(') -
                            code_k.count('}') - code_k.count(')'))
                    last = lines[k].split('//')[0].rstrip()
                    if cur == 0 and (last.endswith(';') or last.endswith('}')):
                        break
                    if cur == 0 and k == i and last.endswith(']'):
                        break  # 单独成行的 Attribute，如 [Header("...")]
                    k += 1
                    if k >= bend - 1:
                        raise SystemExit("%s 的成员在 %d 行处没有正常收尾" % (cname, i + 1))
                members.append((start, k + 1, member_name(lines[i]), lines[i].strip()[:70]))
                i = k + 1
                pre_start = k + 1
                continue
            i += 1
        result[cname] = members
    return result


def main():
    write = '--write' in sys.argv
    with io.open(SRC, 'r', encoding='utf-8-sig') as f:
        text = f.read()
    lines = text.split('\n')
    if lines and lines[-1] == '':
        lines = lines[:-1]
        trailing_newline = True
    else:
        trailing_newline = False

    blocks, depth_at_start = analyze(lines)
    carve = carve_members(lines, blocks, depth_at_start)

    # 头文件（第一个顶层类型之前的所有行）
    header_end = blocks[0][0]
    header = lines[:header_end]
    while header and header[-1].strip() == '':
        header = header[:-1]

    moved_idx = set()
    plan = {}
    report = []

    for (bstart, bend, cname) in blocks:
        if cname in CLASS_TO_FILE:
            preview = bstart
            while preview > header_end:
                s = lines[preview - 1].strip()
                if s.startswith('///') or s.startswith('//') or s.startswith('['):
                    preview -= 1
                else:
                    break
            for x in range(preview, bend):
                moved_idx.add(x)
            plan.setdefault(CLASS_TO_FILE[cname], []).append(('RANGE', preview, bend, 'class ' + cname))
            continue

        members = carve[cname]
        for (mstart, mend, mname, preview) in members:
            if mname in MEMBER_TO_FILE:
                tgt = MEMBER_TO_FILE[mname]
                for x in range(mstart, mend):
                    moved_idx.add(x)
                plan.setdefault(tgt, []).append(('RANGE', mstart, mend, mname))
                report.append((tgt, mname, mend - mstart))

        # 类头（到 '{' 那一行）与类尾 '}' 永远留在核心文件；其余未搬走的行整段保留
        j = bstart
        while '{' not in strip_code(lines[j]):
            j += 1
        kept = list(range(bstart, j + 1))
        kept += [x for x in range(j + 1, bend - 1) if x not in moved_idx]
        kept.append(bend - 1)
        plan['BattleUI.cs'] = [('LINES', kept, '<BattleUI 核心>')]

    # ---------------- 校验 ----------------
    used = set()
    for fname, chunks in plan.items():
        for ch in chunks:
            if ch[0] == 'RANGE':
                for x in range(ch[1], ch[2]):
                    if x in used:
                        raise SystemExit("行 %d 被两个文件重复占用：%s" % (x + 1, fname))
                    used.add(x)
            else:
                for x in ch[1]:
                    if x in used:
                        raise SystemExit("行 %d 被两个文件重复占用：%s" % (x + 1, fname))
                    used.add(x)
    total = len(lines)
    uncovered = [x + 1 for x in range(total) if x not in used and x >= header_end]
    uncovered = [x for x in uncovered if lines[x - 1].strip()]
    if uncovered:
        print("警告：以下行未被搬走到任何文件（可能只是注释）：", uncovered[:20])

    # 成员名映射有没有写错的
    all_member_names = set()
    for cname, members in carve.items():
        for m in members:
            all_member_names.add(m[2])
    bogus = [k for k in MEMBER_TO_FILE if k not in all_member_names]
    if bogus:
        raise SystemExit("映射表里这些成员在源文件里不存在：%s" % bogus)

    # ---------------- 输出 ----------------
    print("=== 拆分计划 ===")
    for fname in sorted(plan):
        chunks = plan[fname]
        n = 0
        for ch in chunks:
            if ch[0] == 'RANGE':
                n += ch[2] - ch[1]
            else:
                n += len(ch[1])
        names = [c[3] for c in chunks if c[0] == 'RANGE']
        if fname == 'BattleUI.cs':
            names = [m[2] for m in carve['BattleUI'] if m[2] not in MEMBER_TO_FILE]
        print("%-28s %5d 行  成员 %d 个" % (fname, n, len(names)))
        if names:
            import textwrap
            print("      " + "\n      ".join(textwrap.wrap(", ".join(names), 96)))
    print("原文件 %d 行 -> 覆盖 %d 行" % (total, len(used)))

    if not write:
        print("\n预演结束，加 --write 落盘")
        return

    import collections
    bodies = collections.defaultdict(list)  # file -> line numbers in order
    usings_only = [l for l in header if l.startswith('using ')]
    for fname, chunks in plan.items():
        for ch in chunks:
            if ch[0] == 'RANGE':
                bodies[fname].extend(range(ch[1], ch[2]))
            else:
                bodies[fname].extend(ch[1])

    for fname, idxs in bodies.items():
        idxs = sorted(set(idxs))
        body = [lines[x] for x in idxs]
        body_text = [l for l in body]
        while body_text and body_text[0].strip() == '':
            body_text = body_text[1:]

        if fname == 'BattleUI.cs':
            # 核心文件：保留原始的 using + 类注释，其余原样
            out = list(header) + ['']
            out.extend(body_text)
            # 把 'public class BattleUI : MonoBehaviour' 改成 partial
            for i2, l in enumerate(out):
                if re.match(r'public class BattleUI : MonoBehaviour\s*$', l):
                    out[i2] = 'public partial class BattleUI : MonoBehaviour'
            text_out = '\n'.join(out) + '\n'
        elif fname in CLASS_TO_FILE.values():
            out = list(usings_only) + ['']
            out.extend(body_text)
            text_out = '\n'.join(out) + '\n'
        else:
            desc = FILE_DESC.get(fname, '')
            out = list(usings_only)
            out.append('')
            out.append('/// <summary>')
            out.append('/// %s' % desc)
            out.append('/// BattleUI 的 partial 分部，与 BattleUI.cs 同属一个类，成员签名保持原名。')
            out.append('/// </summary>')
            out.append('public partial class BattleUI : MonoBehaviour')
            out.append('{')
            out.extend(body_text)
            out.append('}')
            text_out = '\n'.join(out) + '\n'

        # 校验大括号配平
        d = 0
        for l in text_out.split('\n'):
            d += strip_code(l).count('{') - strip_code(l).count('}')
        if d != 0:
            raise SystemExit("%s 大括号不配平：%d" % (fname, d))

        path = os.path.join(OUT_DIR, fname if fname.endswith('.cs') else fname + '.cs')
        with io.open(path, 'w', encoding='utf-8', newline='\n') as f:
            f.write(text_out)
        print("已写入 %s" % path)

    print("\n完成。共生成 %d 个文件。" % len(bodies))


if __name__ == '__main__':
    main()
