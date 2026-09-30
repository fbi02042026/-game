# -*- coding: utf-8 -*-
"""只读 dump BackpackPanel 下各节点的 RectTransform 布局，用于算抽奖容器的 y。"""
import re, sys
sys.stdout.reconfigure(encoding='utf-8')

PF = r"Y:\PixelAdventureTown\Assets\Resources\Prefabs\Battle\BattleUI.prefab"
OUT = r"Y:\PixelAdventureTown\Tools\_layout.txt"


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


def vec2(b, key):
    v = field(b, key)
    if not v:
        return None
    m = re.search(r'x:\s*(-?[\d.eE+-]+),\s*y:\s*(-?[\d.eE+-]+)', v)
    return (float(m.group(1)), float(m.group(2))) if m else None


def children(b):
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
        TR[b['fid']] = dict(go=goid, ch=children(b), block=b)


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


root = None
for fid, g in GO.items():
    if g['name'] == 'BackpackPanel':
        root = fid
        break

out = []


def row(transfid, indent, tag=''):
    b = TR[transfid]['block']
    gofid = TR[transfid]['go']
    nm = GO[gofid]['name'] if gofid in GO else '?'
    pos = vec2(b, 'm_AnchoredPosition')
    size = vec2(b, 'm_SizeDelta')
    amin = vec2(b, 'm_AnchorMin')
    amax = vec2(b, 'm_AnchorMax')
    piv = vec2(b, 'm_Pivot')
    out.append('%s%-16s pos=%-16s size=%-16s anchorMin=%-14s anchorMax=%-14s pivot=%s %s'
               % ('  ' * indent, nm,
                  '(%.1f, %.1f)' % pos if pos else '-',
                  '(%.1f, %.1f)' % size if size else '-',
                  '(%.2f, %.2f)' % amin if amin else '-',
                  '(%.2f, %.2f)' % amax if amax else '-',
                  '(%.2f, %.2f)' % piv if piv else '-', tag))


rr = rect_of(root)
row(rr, 0, '<- BackpackPanel')
for c in TR[rr]['ch']:
    row(c, 1)
    for g in TR[c]['ch']:
        row(g, 2)

open(OUT, 'w', encoding='utf-8', newline='\n').write('\n'.join(out))
print('OK -> %s (%d 行)' % (OUT, len(out)))
