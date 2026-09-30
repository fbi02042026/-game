# -*- coding: utf-8 -*-
"""
把指定 GameObject 上 Image 组件的 m_RaycastTarget 关掉（1 -> 0）。

用途：纯背景 / 遮罩 / 提示条这类不需要接点击的图，开着的 raycast 会吃掉落在它下面的按钮点击。
本例：BackpackPanel/zhezhao（58.8% 黑遮罩）和它下面的 Image (1)（"战斗中无法调整"提示条）
     都盖在抽奖按钮上面且 raycastTarget=1 → 按钮看得见但点不动。

按 GameObject 名字定位（只在 BackpackPanel 子树里找），不硬编码 fileID，改路径也不怕。
默认只预览，加 --apply 才写盘（先备份，文件名带时间戳）。

用法：
    python Tools/SetRaycastTarget.py                 # 预览当前状态
    python Tools/SetRaycastTarget.py --apply         # 关掉
    python Tools/SetRaycastTarget.py --apply --on    # 改回 1（反悔用）
"""
import re, os, sys, shutil, time

sys.stdout.reconfigure(encoding='utf-8')

PF = r"Y:\PixelAdventureTown\Assets\Resources\Prefabs\Battle\BattleUI.prefab"
BACKUP_DIR = r"Y:\PixelAdventureTown\.workbuddy\backup\20260930-raycast-off"
ROOT = "BackpackPanel"
# 要处理的 GameObject 名（在 BackpackPanel 子树里按名字找）
TARGETS = ["zhezhao", "Image (1)"]
IMAGE_SCRIPT_GUID = "fe87c0e1cc204ed48ad3b37840f39efc"     # UnityEngine.UI.Image


def dec(s):
    if '\\u' in s:
        try:
            s = s.encode('latin-1', 'ignore').decode('unicode_escape')
        except Exception:
            pass
    if len(s) >= 2 and s[0] == '"' and s[-1] == '"':
        s = s[1:-1]
    return s


def parse(lines):
    blocks, cur = [], None
    for i, ln in enumerate(lines):
        m = re.match(r'^--- !u!(\d+) &(\d+)', ln)
        if m:
            if cur:
                blocks.append(cur)
            cur = dict(kind=int(m.group(1)), fid=int(m.group(2)), start=i, end=len(lines))
    if cur:
        blocks.append(cur)
    for k in range(len(blocks) - 1):
        blocks[k]['end'] = blocks[k + 1]['start']
    blocks[-1]['end'] = len(lines)
    return blocks


def field(lines, b, key):
    for i in range(b['start'], b['end']):
        s = lines[i].strip()
        if s.startswith(key + ':'):
            return s[len(key) + 1:].strip()
    return None


def children(lines, b):
    out = []
    i = b['start']
    while i < b['end']:
        if lines[i].strip() == 'm_Children:':
            j = i + 1
            while j < b['end']:
                m = re.match(r'^-\s*\{fileID:\s*(\d+)\}', lines[j].strip())
                if not m:
                    break
                out.append(int(m.group(1)))
                j += 1
            return out
        i += 1
    return out


def main():
    apply = '--apply' in sys.argv
    val = '1' if '--on' in sys.argv else '0'

    raw = open(PF, 'r', encoding='utf-8', newline='').read()
    lines = raw.split('\n')
    blocks = parse(lines)

    GO, TR, comp_blocks = {}, {}, {}
    for b in blocks:
        if b['kind'] == 1:
            GO[b['fid']] = dict(name=dec(field(lines, b, 'm_Name') or ''), block=b)
        elif b['kind'] in (4, 224):
            go = field(lines, b, 'm_GameObject')
            TR[b['fid']] = dict(go=int(re.search(r'(\d+)', go).group(1)) if go else -1,
                                ch=children(lines, b))
        elif b['kind'] == 114:      # MonoBehaviour
            go = field(lines, b, 'm_GameObject')
            if go and IMAGE_SCRIPT_GUID in (field(lines, b, 'm_Script') or ''):
                comp_blocks[int(re.search(r'(\d+)', go).group(1))] = b

    # 从 BackpackPanel 往下走，收集目标 GO
    root = None
    for fid, g in GO.items():
        if g['name'] == ROOT:
            root = fid
            break
    if root is None:
        raise SystemExit('找不到 ' + ROOT)

    found = []

    def walk(transfid):
        gofid = TR[transfid]['go']
        if gofid in GO and GO[gofid]['name'] in TARGETS:
            found.append(gofid)
        for c in TR[transfid]['ch']:
            walk(c)

    for fid in TR:
        if TR[fid]['go'] == root:
            for c in TR[fid]['ch']:
                walk(c)
            break

    print('目标值 m_RaycastTarget: %s' % val)
    edits = []
    for gofid in found:
        b = comp_blocks.get(gofid)
        nm = GO[gofid]['name']
        if b is None:
            print('  [跳过] %-12s 没有 Image 组件' % nm)
            continue
        idx = -1
        cur = None
        for i in range(b['start'], b['end']):
            if lines[i].strip().startswith('m_RaycastTarget:'):
                idx, cur = i, lines[i].strip()
                break
        sprite = field(lines, b, 'm_Sprite') or '-'
        print('  %-12s 当前 %s   精灵=%s' % (nm, cur, sprite[:60]))
        if cur and idx >= 0:
            edits.append((idx, nm))

    if not apply:
        print('\n[只读] 没写盘。确认后加 --apply')
        return

    os.makedirs(BACKUP_DIR, exist_ok=True)
    dst = os.path.join(BACKUP_DIR, 'BattleUI.prefab_%s' % time.strftime('%H%M%S'))
    shutil.copy2(PF, dst)
    print('\n[备份] %s' % dst)

    for idx, nm in edits:
        indent = lines[idx][:len(lines[idx]) - len(lines[idx].lstrip())]
        lines[idx] = '%sm_RaycastTarget: %s' % (indent, val)
        print('  [已改] %-12s -> %s' % (nm, lines[idx].strip()))

    open(PF, 'w', encoding='utf-8', newline='').write('\n'.join(lines))
    print('[完成] 共 %d 处' % len(edits))


if __name__ == '__main__':
    main()
