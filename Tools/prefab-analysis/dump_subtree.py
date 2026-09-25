# -*- coding: utf-8 -*-
"""打印 prefab 中某个节点(按名字)的整棵子树：节点名 / 组件 / sprite guid / material guid / activeSelf。
坑：m_Father 挂在 RectTransform(class 224 / Transform class 4) 上，**不在 GameObject(class 1) 上**。
用法: python dump_subtree.py <prefab路径> <节点名>
"""
import io, os, re, sys

IMG_SCRIPT = 'fe87c0e1cc204ed48ad3b37840f39efc'
TXT_SCRIPT = 'f5f67c52d1564df4a8936ccd202a3bd8'


def load(path):
    with io.open(path, 'r', encoding='utf-8', errors='replace') as f:
        return f.read().split('\n')


def parse(lines):
    docs = {}
    cur = None
    for ln in lines:
        m = re.match(r'^--- !u!(-?\d+) &(-?\d+)', ln)
        if m:
            cur = {'cls': int(m.group(1)), 'body': []}
            docs[int(m.group(2))] = cur
            continue
        if cur is not None:
            cur['body'].append(ln)
    return docs


def find1(body, key):
    k = key + ': '
    for ln in body:
        s = ln.strip()
        if s.startswith(k):
            return s[len(k):].strip()
    return None


def guid_of(v):
    if not v:
        return None
    m = re.search(r'guid:\s*([0-9a-fA-F]{32})', v)
    return m.group(1) if m else None


def fid_of(v):
    if not v:
        return None
    m = re.search(r'fileID:\s*(-?\d+)', v)
    return int(m.group(1)) if m else None


def main():
    path = sys.argv[1]
    target = sys.argv[2]
    lines = load(path)
    docs = parse(lines)

    name, comps, active = {}, {}, {}
    for fid, d in docs.items():
        if d['cls'] != 1:
            continue
        b = d['body']
        name[fid] = find1(b, 'm_Name') or ''
        active[fid] = find1(b, 'm_IsActive')
        cs, incomp = [], False
        for ln in b:
            s = ln.strip()
            if s.startswith('m_Component:'):
                incomp = True
                continue
            if incomp:
                if s.startswith('- component:'):
                    v = fid_of(s)
                    if v is not None:
                        cs.append(v)
                    continue
                if s.startswith('m_'):
                    incomp = False
        comps[fid] = cs

    # transform/recttransform -> owning GameObject
    trans2go = {}
    father_t = {}
    for fid, d in docs.items():
        if d['cls'] not in (4, 224):
            continue
        b = d['body']
        g = fid_of(find1(b, 'm_GameObject'))
        if g is not None:
            trans2go[fid] = g
            ft = fid_of(find1(b, 'm_Father'))
            if ft is not None:
                father_t[g] = ft

    father = {}
    for g, ft in father_t.items():
        pg = trans2go.get(ft)
        if pg is not None and pg != 0:
            father[g] = pg

    root = None
    for fid, nm in name.items():
        if nm == target:
            root = fid
            break
    if root is None:
        print('NOT FOUND: ' + target)
        print('可用节点名:')
        print(', '.join(sorted(set(v for v in name.values() if v))[:120]))
        return

    kids = {}
    for g, f in father.items():
        kids.setdefault(f, []).append(g)

    owner = {}
    for g, cs in comps.items():
        for c in cs:
            owner[c] = g

    out = []

    def walk(g, depth):
        nm = name.get(g, '?') or '(unnamed)'
        pad = '  ' * depth
        ia = active.get(g)
        line = pad + '- ' + nm + '  [go ' + str(g) + ']'
        if ia is not None:
            line += '  active=' + ia
        out.append(line)
        for c in comps.get(g, []):
            d = docs.get(c)
            if d is None:
                continue
            cls = d['cls']
            if cls in (4, 224):
                out.append(pad + '    RectTransform')
            elif cls == 222:
                out.append(pad + '    CanvasRenderer')
            elif cls == 114:
                b = d['body']
                sg = guid_of(find1(b, 'm_Script'))
                sp = guid_of(find1(b, 'm_Sprite'))
                mt = guid_of(find1(b, 'm_Material'))
                col = find1(b, 'm_Color')
                en = find1(b, 'm_Enabled')
                tag = 'Mono'
                if sg == IMG_SCRIPT:
                    tag = 'Image'
                elif sg == TXT_SCRIPT:
                    tag = 'Text'
                s = pad + '    ' + tag
                if sp:
                    s += ' sprite=' + sp
                if mt:
                    s += ' material=' + mt
                if col:
                    s += ' color=' + col
                if en is not None:
                    s += ' enabled=' + en
                if sg and tag == 'Mono':
                    s += ' script=' + sg
                out.append(s)
            elif cls == 198:
                out.append(pad + '    ParticleSystem')
            elif cls == 212:
                out.append(pad + '    SpriteRenderer')
            elif cls == 23:
                out.append(pad + '    MeshRenderer')
            elif cls == 33:
                out.append(pad + '    MeshFilter')
            else:
                out.append(pad + '    cls' + str(cls))
        for k in sorted(kids.get(g, []), key=lambda x: (name.get(x, ''), x)):
            walk(k, depth + 1)

    walk(root, 0)
    print('\n'.join(out))


if __name__ == '__main__':
    main()
