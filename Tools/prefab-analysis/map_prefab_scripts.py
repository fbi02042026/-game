# -*- coding: utf-8 -*-
"""列出预制体里挂了哪些 MonoBehaviour 脚本（guid -> 脚本文件名），并打印所在 GameObject 名。
用法: python map_prefab_scripts.py <prefab路径>
"""
import io, os, re, sys

ROOT = r"E:\xiangsumaoxian\Assets"

def build_guid_map():
    m = {}
    for dp, dn, fn in os.walk(ROOT):
        for f in fn:
            if not f.endswith('.cs.meta'):
                continue
            p = os.path.join(dp, f)
            try:
                t = io.open(p, 'r', encoding='utf-8', errors='replace').read()
            except Exception:
                continue
            g = re.search(r'guid:\s*([0-9a-fA-F]{32})', t)
            if g:
                m[g.group(1).lower()] = f[:-5]
    return m

def main():
    prefab = sys.argv[1]
    txt = io.open(prefab, 'r', encoding='utf-8', errors='replace').read()
    gmap = build_guid_map()

    # 收集 fileID -> GameObject 名
    go_names = {}
    for m in re.finditer(r'--- !u!1 &(\d+)\r?\nGameObject:\r?\n(?:.*\r?\n)*?\s*m_Name:\s*(\S+)', txt):
        go_names[m.group(1)] = m.group(2)

    # 每个 MonoBehaviour 块：--- !u!114 &<fileID> ... m_GameObject: {fileID: X} ... m_Script: {guid: Y}
    blocks = re.split(r'\n--- !u!', txt)
    rows = []
    for b in blocks:
        if not b.startswith('114 &'):
            continue
        fid = re.match(r'114 &(\d+)', b).group(1)
        gog = re.search(r'm_GameObject:\s*\{fileID:\s*(\d+)\}', b)
        sg = re.search(r'm_Script:\s*\{[^}]*guid:\s*([0-9a-fA-F]{32})', b)
        if not sg:
            continue
        script = gmap.get(sg.group(1).lower(), '?' + sg.group(1)[:8])
        goname = go_names.get(gog.group(1), '?') if gog else '?'
        rows.append((script, goname, fid))

    seen = set()
    for s, g, f in rows:
        k = (s, g)
        if k in seen:
            continue
        seen.add(k)
        print("%-28s  <-  GameObject: %s" % (s, g))

if __name__ == '__main__':
    main()
