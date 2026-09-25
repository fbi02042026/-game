# -*- coding: utf-8 -*-
"""打印预制体里某个 GameObject（按 fileID 或名字）的所有组件 YAML 块。
用法: python dump_node_detail.py <prefab> <fileID 或 节点名>
重点输出：RectTransform(anchor/sizeDelta/anchoredPosition/pivot)、Image(sprite/type/color)、
GridLayoutGroup(cellSize/spacing/padding/constraint/startAxis/childAlignment)、Mono script guid。
"""
import io, os, re, sys

def load(prefab):
    return io.open(prefab, 'r', encoding='utf-8', errors='replace').read()

def go_blocks(txt):
    """返回 {fileID: (name, [component fileIDs])}"""
    out = {}
    for m in re.finditer(r'--- !u!1 &(\d+)\s*\n(.*?)(?=\n--- !u!|\Z)', txt, re.S):
        fid = m.group(1)
        body = m.group(2)
        nm = re.search(r'm_Name:\s*(.*)', body)
        name = nm.group(1).strip() if nm else ''
        comps = re.findall(r'- component: \{fileID: (\d+)\}', body)
        out[fid] = (name, comps)
    return out

def comp_block(txt, fid):
    m = re.search(r'--- !u!(\d+) &%s\s*\n(.*?)(?=\n--- !u!|\Z)' % fid, txt, re.S)
    if not m:
        return None, None
    return m.group(1), m.group(2)

def main():
    prefab = sys.argv[1]
    key = sys.argv[2]
    txt = load(prefab)
    gos = go_blocks(txt)

    targets = []
    if key.isdigit():
        if key in gos:
            targets.append(key)
    for fid, (name, comps) in gos.items():
        if name == key or name.strip('"') == key:
            targets.append(fid)
    if not targets:
        print("NOT FOUND: " + key)
        return

    for fid in targets:
        name, comps = gos[fid]
        print("=== GameObject %s  name=%s ===" % (fid, name))
        for cf in comps:
            cls, body = comp_block(txt, cf)
            if body is None:
                continue
            if cls == '224':
                def g(pat):
                    mm = re.search(pat, body)
                    return mm.group(1) if mm else '-'
                print("  [RectTransform]")
                for line in ['m_AnchorMin', 'm_AnchorMax', 'm_Pivot', 'm_SizeDelta',
                             'm_AnchoredPosition', 'm_LocalScale']:
                    mm = re.search(re.escape(line) + r': (.*)', body)
                    if mm:
                        print("    %-20s %s" % (line, mm.group(1).strip()))
                print("    localScale=%s" % g(r'localScale: \{x: ([-\d.e]+), y: ([-\d.e]+), z: ([-\d.e]+)\}'))
            elif cls == '114':
                sg = re.search(r'm_Script: \{fileID: \d+, guid: ([0-9a-f]{32})', body)
                script = sg.group(1)[:8] if sg else '?'
                print("  [Mono script=%s]" % script)
                for line in ['m_CellSize', 'm_Spacing', 'm_Padding', 'm_Constraint', 'm_ConstraintCount',
                             'm_StartAxis', 'm_StartCorner', 'm_ChildAlignment']:
                    mm = re.search(re.escape(line) + r': (.*)', body)
                    if mm:
                        print("    %s: %s" % (line, mm.group(1).strip()))
            elif cls == '222':
                mm = re.search(r'sprite: \{fileID: \d+, guid: ([0-9a-f]{32})', body)
                tt = re.search(r'm_Type: (\d+)', body)
                print("  [CanvasRenderer]")
            elif cls == '114' or True:
                pass
        # Image / other classes
        for cf in comps:
            cls, body = comp_block(txt, cf)
            if body is None:
                continue
            if cls == '222':
                continue
            if cls in ('1', '224', '114', '4', '223', '225'):
                pass
        print("")

if __name__ == '__main__':
    main()
