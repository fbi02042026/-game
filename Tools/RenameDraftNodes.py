# -*- coding: utf-8 -*-
"""
把 BattleUI.prefab 里 BackpackPanel 下这一套抽奖节点改成英文。

默认只做只读 dump（不改盘）；加 --apply 才真正写盘，写盘前自动备份。

定位方式：按「BackpackPanel 下的层级路径 + 旧中文名」找 GameObject，
只改它自己块里的 m_Name 行 —— 不碰图、不碰 guid、不碰父子关系，所以不会断链
（prefab 内部引用一律走 fileID，跟名字无关）。

用法：
    python Tools/RenameDraftNodes.py            # 只读，导出节点树到 Tools/_dump_bp.txt
    python Tools/RenameDraftNodes.py --apply    # 备份 + 改名 + 重新导出验证
"""
import re, os, sys, shutil

sys.stdout.reconfigure(encoding='utf-8')

PF = r"Y:\PixelAdventureTown\Assets\Resources\Prefabs\Battle\BattleUI.prefab"
DUMP = r"Y:\PixelAdventureTown\Tools\_dump_bp.txt"
BACKUP_DIR = r"Y:\PixelAdventureTown\.workbuddy\backup\20260930-draft-node-rename"

# 「相对 BackpackPanel 的路径」 -> 新英文名
# 路径用 / 分隔，写的是改之前的中文名
RENAME = {
    "抽奖":       "DraftRoot",
    "继续":       "BtnContinue",
    "背包":       "BtnBackpack",
    "抽奖/普通":   "BtnNormal",
    "抽奖/佣兵":   "BtnMerc",
    "抽奖/装备":   "BtnEquip",
    "抽奖/技能":   "BtnSkill",
    "抽奖/普通/金额": "Cost",
    "抽奖/佣兵/金额": "Cost",
    "抽奖/装备/金额": "Cost",
    "抽奖/技能/金额": "Cost",
}


def dec(s):
    """
    prefab 里中文被写成带引号的 "\\u62BD\\u5956"，还原成真中文便于匹配。
    注意 Unity 给含转义的名字加了双引号，必须剥掉，否则匹配不上。
    """
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
    """返回 GO(goFid -> {name, block}) 与 TR(transFid -> {go, ch})。"""
    blocks = parse(lines)
    GO, TR = {}, {}
    for b in blocks:
        if b['kind'] == 1:      # GameObject：名字在这里
            GO[b['fid']] = dict(name=dec(field(lines, b, 'm_Name') or ''), block=b)
        elif b['kind'] in (4, 224):     # (Rect)Transform：父子关系在这里
            go = field(lines, b, 'm_GameObject')
            goid = int(re.search(r'(\d+)', go).group(1)) if go else -1
            TR[b['fid']] = dict(go=goid, ch=children(lines, b))
    return GO, TR


def rect_of(lines, GO, TR, gofid):
    """GameObject -> 它的 (Rect)Transform fileID（走 m_Component 列表）。"""
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


def find_backpack(GO):
    for fid, g in GO.items():
        if g['name'] == 'BackpackPanel':
            return fid
    return None


def collect(lines):
    """从 BackpackPanel 往下走，把要改名的 GO 收集成 [(goFid, 新名, 旧路径)]。"""
    GO, TR = build(lines)
    root = find_backpack(GO)
    if root is None:
        raise SystemExit('找不到 BackpackPanel')
    rect = rect_of(lines, GO, TR, root)
    if rect is None:
        raise SystemExit('BackpackPanel 没有 Transform')

    hits = []

    def walk(transfid, path):
        gofid = TR[transfid]['go']
        if gofid not in GO:
            return
        nm = GO[gofid]['name']
        here = path + '/' + nm if path else nm
        if here in RENAME:
            hits.append((gofid, RENAME[here], here))
        for c in TR[transfid]['ch']:
            walk(c, here)

    for c in TR[rect]['ch']:
        walk(c, '')
    return GO, TR, hits


def dump_tree(lines):
    GO, TR = build(lines)
    root = find_backpack(GO)
    out = ['BackpackPanel GO=%s' % root]
    if root is None:
        return '\n'.join(out)
    rect = rect_of(lines, GO, TR, root)
    out.append('BackpackPanel Rect=%s' % rect)

    def walk(transfid, indent):
        gofid = TR[transfid]['go']
        if gofid not in GO:
            return
        out.append('  ' * indent + '[go %s] %s' % (gofid, GO[gofid]['name']))
        for c in TR[transfid]['ch']:
            walk(c, indent + 1)

    for c in TR[rect]['ch']:
        walk(c, 1)
    return '\n'.join(out)


def main():
    apply = '--apply' in sys.argv
    raw = open(PF, 'r', encoding='utf-8', newline='').read()
    lines = raw.split('\n')

    if not apply:
        open(DUMP, 'w', encoding='utf-8', newline='\n').write(dump_tree(lines))
        print('[只读] 节点树已导出 -> %s' % DUMP)
        GO, TR, hits = collect(lines)
        print('[预览] 待改名 %d 处：' % len(hits))
        for gofid, new, old in hits:
            print('  %-24s -> %s' % (old, new))
        return

    GO, TR, hits = collect(lines)
    if not hits:
        print('[中止] 一个都没匹配到，不动盘')
        return

    os.makedirs(BACKUP_DIR, exist_ok=True)
    dst = os.path.join(BACKUP_DIR, 'BattleUI.prefab')
    shutil.copy2(PF, dst)
    print('[备份] %s' % dst)

    blocks = parse(lines)
    byFid = {b['fid']: b for b in blocks if b['kind'] == 1}
    n = 0
    for gofid, new, old in hits:
        b = byFid.get(gofid)
        if b is None:
            print('  [警告] go %d 找不到块' % gofid)
            continue
        for i in range(b['start'], b['end']):
            s = lines[i].strip()
            if s.startswith('m_Name:'):
                indent = lines[i][:len(lines[i]) - len(lines[i].lstrip())]
                lines[i] = '%sm_Name: %s' % (indent, new)
                print('  %-24s -> %s' % (old, new))
                n += 1
                break

    open(PF, 'w', encoding='utf-8', newline='').write('\n'.join(lines))
    print('[改名] 完成 %d 处' % n)

    lines2 = open(PF, 'r', encoding='utf-8', newline='').read().split('\n')
    open(DUMP, 'w', encoding='utf-8', newline='\n').write(dump_tree(lines2))
    print('[验证] 新的节点树已导出 -> %s' % DUMP)


if __name__ == '__main__':
    main()
