# -*- coding: utf-8 -*-
"""只读 dump BattleUI.prefab 前 3 层结构，用于定位场景/玩家头像/顶部 UI 在哪里。"""
import re, sys
sys.stdout.reconfigure(encoding='utf-8')

PF = r"Y:\PixelAdventureTown\Assets\Resources\Prefabs\Battle\BattleUI.prefab"
OUT = r"Y:\PixelAdventureTown\Tools\_tree.txt"


def dec(s):
    if '\\u' in s:
        try:
            s = s.encode('latin-1', 'ignore').decode('unicode_escape')
        except Exception:
            pass
    if len(s) >= 2 and s[0] == '"' and s[-1] == '"':
        s = s[1:-1]
    return s


lines = open(PF, 'r', encoding='utf-8', newline='').read().split('\n')

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


def field(b, key):
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


GO, TR = {}, {}
for b in blocks:
    if b['kind'] == 1:
        GO[b['fid']] = dict(name=dec(field(b, 'm_Name') or ''), block=b)
    elif b['kind'] in (4, 224):
        go = field(b, 'm_GameObject')
        goid = int(re.search(r'(\d+)', go).group(1)) if go else -1
        TR[b['fid']] = dict(go=goid, ch=children(lines, b), block=b)


def rect_of(gofid):
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


def vec2(b, key):
    v = field(b, key)
    if not v:
        return None
    m = re.search(r'x:\s*(-?[\d.eE+-]+),\s*y:\s*(-?[\d.eE+-]+)', v)
    return (float(m.group(1)), float(m.group(2))) if m else None


out = []

def row(transfid, indent, maxDepth):
    if indent >= maxDepth:
        return
    gofid = TR[transfid]['go']
    if gofid not in GO:
        return
    b = TR[transfid]['block']
    nm = GO[gofid]['name']
    pos = vec2(b, 'm_AnchoredPosition')
    size = vec2(b, 'm_SizeDelta')
    out.append('%s%s go=%d pos=%s size=%s' % (
        '  ' * indent, nm, gofid,
        '(%.0f,%.0f)' % pos if pos else '-',
        '(%.0f,%.0f)' % size if size else '-'))
    for c in TR[transfid]['ch']:
        row(c, indent + 1, maxDepth)

# 找 BattleUI 根（取第一个名为 "BattleUI" 的 GO 或者最深的根）
root = None
for fid, g in GO.items():
    if g['name'] == 'BattleUI':
        root = fid
        break

if root:
    rr = rect_of(root)
    out.append('BattleUI go=%d root' % root)
    for c in TR[rr]['ch']:
        row(c, 1, 4)

open(OUT, 'w', encoding='utf-8', newline='\n').write('\n'.join(out))
print('OK -> %s (%d 行)' % (OUT, len(out)))
