# -*- coding: utf-8 -*-
"""
把 BattleUI.prefab 里 BackpackPanel/DraftRoot（抽奖按钮容器）的 y 调到展开后可见的位置。

背景：面板锚屏幕底、pivot 在底边，所以「子节点相对屏幕底的高度 = 面板高/2 + pos.y」。
面板收起 600 / 展开 750 → 父中心分别在 300 / 375。
原来 pos.y = -539，展开后 375-539 = -164，还在屏幕外，所以按钮看不见。

展开时内容节点整体上移 150（父中心自动 +75、代码补 +75），底部空出 0~150。
按钮 100 高，中心放 125（占 75~175）：离屏幕底 75 躲安全区，顶边 175 与背包按钮底边 180 留 5px。
→ pos.y = 125 - 375 = -250

用法：python Tools/SetDraftRootPos.py [--y -250]   默认只预览，加 --apply 才写盘（先备份）
"""
import re, os, sys, shutil, time

sys.stdout.reconfigure(encoding='utf-8')

PF = r"Y:\PixelAdventureTown\Assets\Resources\Prefabs\Battle\BattleUI.prefab"
BACKUP_DIR = r"Y:\PixelAdventureTown\.workbuddy\backup\20260930-draft-root-pos"
TARGET_PATH = "BackpackPanel/DraftRoot"
DEFAULT_Y = -250.0


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


def build(lines):
    blocks = parse(lines)
    GO, TR = {}, {}
    for b in blocks:
        if b['kind'] == 1:
            GO[b['fid']] = dict(name=dec(field(lines, b, 'm_Name') or ''), block=b)
        elif b['kind'] in (4, 224):
            go = field(lines, b, 'm_GameObject')
            goid = int(re.search(r'(\d+)', go).group(1)) if go else -1
            TR[b['fid']] = dict(go=goid, ch=children(lines, b), block=b)
    return GO, TR


def rect_of(lines, GO, TR, gofid):
    b = GO[gofid]['block']
    i = b['start']
    while i < b['end']:
        if lines[i].strip() == 'm_Component:':
            j = i + 1
            while j < b['end']:
                m = re.search(r'component:\s*\{fileID:\s*(\d+)\}', lines[j])
                if not m:
                    break
                if int(m.group(1)) in TR:
                    return int(m.group(1))
                j += 1
            break
        i += 1
    return None


def locate(lines):
    """按路径 BackpackPanel/DraftRoot 找到它的 (Rect)Transform 块。"""
    GO, TR = build(lines)
    root = None
    for fid, g in GO.items():
        if g['name'] == TARGET_PATH.split('/')[0]:
            root = fid
            break
    if root is None:
        raise SystemExit('找不到 ' + TARGET_PATH.split('/')[0])
    rr = rect_of(lines, GO, TR, root)
    want = TARGET_PATH.split('/')[1]
    for c in TR[rr]['ch']:
        gofid = TR[c]['go']
        if gofid in GO and GO[gofid]['name'] == want:
            return TR[c]['block']
    raise SystemExit('找不到 ' + TARGET_PATH)


def main():
    newy = DEFAULT_Y
    if '--y' in sys.argv:
        newy = float(sys.argv[sys.argv.index('--y') + 1])
    apply = '--apply' in sys.argv

    raw = open(PF, 'r', encoding='utf-8', newline='').read()
    lines = raw.split('\n')
    b = locate(lines)

    cur = None
    idx = -1
    for i in range(b['start'], b['end']):
        s = lines[i].strip()
        if s.startswith('m_AnchoredPosition:'):
            cur = s
            idx = i
            break
    m = re.search(r'x:\s*(-?[\d.eE+-]+),\s*y:\s*(-?[\d.eE+-]+)', cur or '')
    curx, cury = float(m.group(1)), float(m.group(2))

    print('目标: %s' % TARGET_PATH)
    print('  当前 pos = (%.1f, %.1f)' % (curx, cury))
    print('  收起(面板600) 屏幕y = %.1f' % (300 + cury))
    print('  展开(面板750) 屏幕y = %.1f' % (375 + cury))
    print('  ---- 改成 ----')
    print('  新的 pos = (%.1f, %.1f)' % (curx, newy))
    print('  收起(面板600) 屏幕y = %.1f' % (300 + newy))
    print('  展开(面板750) 屏幕y = %.1f  <- 按钮中心，占 %.1f ~ %.1f'
          % (375 + newy, 375 + newy - 50, 375 + newy + 50))

    if not apply:
        print('\n[只读] 没写盘。确认后加 --apply')
        return

    os.makedirs(BACKUP_DIR, exist_ok=True)
    dst = os.path.join(BACKUP_DIR, 'BattleUI.prefab_%s' % time.strftime('%H%M%S'))
    shutil.copy2(PF, dst)
    print('\n[备份] %s' % dst)

    indent = lines[idx][:len(lines[idx]) - len(lines[idx].lstrip())]
    lines[idx] = '%sm_AnchoredPosition: {x: %s, y: %s}' % (indent, _num(curx), _num(newy))
    open(PF, 'w', encoding='utf-8', newline='').write('\n'.join(lines))
    print('[已写入] %s' % lines[idx].strip())


def _num(v):
    return str(int(v)) if float(v).is_integer() else ('%g' % v)


if __name__ == '__main__':
    main()
