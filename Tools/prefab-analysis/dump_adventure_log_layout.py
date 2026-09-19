# -*- coding: utf-8 -*-
"""导出 AdventureLogUI.prefab 的层级树：节点名/激活/Rect/关键组件/raycastTarget。
只读分析，不改 prefab。"""
import re, sys, io, os

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

ROOT = r"E:\xiangsumaoxian"
PREFAB = sys.argv[1] if len(sys.argv) > 1 else os.path.join(
    ROOT, r"Assets\Resources\Prefabs\Town\AdventureLogUI.prefab")

text = open(PREFAB, 'r', encoding='utf-8').read()

# --- 按 --- !u! 分块 ---
blocks = re.split(r'--- !u!', text)
objs = {}   # fileId -> (classid, body)
for b in blocks:
    if not b.strip():
        continue
    header, _, body = b.partition('\n')
    m = re.match(r'(\d+) &(\d+)', header)
    if not m:
        continue
    classid, fid = m.group(1), m.group(2)
    objs[fid] = (classid, body)

def get(fid):
    return objs.get(fid, (None, None))

def field(body, name):
    m = re.search(r'^\s*%s: (.*)$' % re.escape(name), body, re.M)
    return m.group(1).strip() if m else None

def parse_vec(s):
    if not s:
        return None
    m = re.match(r'\{([^}]*)\}', s)
    if not m:
        return None
    parts = []
    for p in m.group(1).split(','):
        if ':' in p:
            parts.append(p.split(':')[1])
        elif '=' in p:
            parts.append(p.split('=')[1])
    try:
        return [float(p) for p in parts]
    except ValueError:
        return None

# GameObject: name, components, active
go_name, go_comps, go_active = {}, {}, {}
for fid, (cid, body) in objs.items():
    if cid == '1':  # GameObject
        nm = field(body, 'm_Name')
        # unescape \uXXXX
        if nm:
            try:
                nm = nm.encode('utf-8').decode('unicode_escape').encode('latin-1', 'ignore').decode('utf-8', 'ignore')
            except Exception:
                pass
        go_name[fid] = nm
        comps = re.findall(r'component: \{fileID: (\d+)\}', body)
        go_comps[fid] = comps
        act = field(body, 'm_IsActive')
        go_active[fid] = act == '1'

# component -> gameobject
comp_owner = {}
comp_data = {}
for fid, (cid, body) in objs.items():
    if cid in ('4', '114', '223', '225', '1140'):
        owner = re.search(r'm_GameObject: \{fileID: (\d+)\}', body)
        if owner:
            comp_owner[fid] = owner.group(1)
            comp_data[fid] = body

# transform tree（m_Children 顺序 = sibling 顺序，决定遮挡/点击优先级）
tr_parent, tr_children, tr_go, tr_rect = {}, {}, {}, {}
for fid, (cid, body) in objs.items():
    if cid in ('4', '224'):  # Transform / RectTransform
        fgo = re.search(r'm_GameObject: \{fileID: (\d+)\}', body)
        fath = re.search(r'm_Father: \{fileID: (\d+)\}', body)
        tr_go[fid] = fgo.group(1) if fgo else None
        tr_parent[fid] = fath.group(1) if fath and fath.group(1) != '0' else None
        sa = parse_vec(field(body, 'm_AnchorMin'))
        sb = parse_vec(field(body, 'm_AnchorMax'))
        sd = parse_vec(field(body, 'm_SizeDelta'))
        ap = parse_vec(field(body, 'm_AnchoredPosition'))
        sp = parse_vec(field(body, 'm_LocalScale'))
        tr_rect[fid] = (sa, sb, sd, ap, sp)
for fid, (cid, body) in objs.items():
    if cid in ('4', '224'):
        kids = re.findall(r'- \{fileID: (\d+)\}', body)
        if kids:
            tr_children[fid] = kids
for fid in tr_go:
    if fid not in tr_children:
        tr_children.setdefault(tr_parent.get(fid), []).append(fid) if False else None
    tr_children.setdefault(fid, tr_children.get(fid, []))

# gather interesting comp types per GO
mono_names = {}
for fid, (cid, body) in objs.items():
    if cid == '114':  # MonoBehaviour
        m = re.search(r'm_Script: \{fileID: \d+, guid: ([0-9a-f]+)', body)
        mono_names[fid] = m.group(1)[:8] if m else '??'

def fmt_rect(fid):
    sa, sb, sd, ap, sp = tr_rect.get(fid, (None,) * 5)
    def f(v):
        return '(' + ','.join(('%.0f' % x) for x in v) + ')' if v else '?'
    return 'aMin%s aMax%s size%s pos%s scale%s' % (f(sa), f(sb), f(sd), f(ap), f(sp))

def comp_summary(gofid):
    out = []
    for c in go_comps.get(gofid, []):
        cid, body = get(c)
        if cid == '114':
            out.append('Mono:' + mono_names.get(c, '??'))
        elif cid == '20':
            out.append('Camera')
        elif cid == '223':
            out.append('Canvas')
        elif cid == '225':
            out.append('CanvasGroup')
    return out

# UI comps: find Image/Button/Text by scanning class 114? No, they are engine types w/ fileID hash.
# Instead detect via m_Component list of GO? Engine UI components have own class ids in YAML? They appear as 114 too? No:
# In Unity YAML, built-in UI components (Image/Button/Text) are serialized as MonoBehaviour (class 114) with script guid referencing engine.
# So detect by m_Script guid known hashes or by presence of fields.
def detect_ui(gofid):
    tags = []
    for c in go_comps.get(gofid, []):
        cid, body = get(c)
        if cid != '114':
            continue
        if 'm_Font:' in body or 'm_Text:' in body:
            tags.append('Text')
        if 'm_RaycastTarget:' in body and 'm_Font:' not in body:
            rt = field(body, 'm_RaycastTarget')
            if 'm_OnClick:' in body:
                tags.append('Button(ray=%s)' % rt)
            elif 'm_Sprite:' in body or 'm_Color:' in body:
                tags.append('Image(ray=%s)' % rt)
    return tags

lines = []
def walk(tfid, depth):
    gofid = tr_go.get(tfid)
    name = go_name.get(gofid, '?')
    act = 'A' if go_active.get(gofid) else '-'
    info = fmt_rect(tfid)
    tags = detect_ui(gofid) + comp_summary(gofid)
    lines.append('%s%s %s [%s] %s' % ('  ' * depth, act, name, info, ','.join(tags) if tags else ''))
    for ch in tr_children.get(tfid, []):
        walk(ch, depth + 1)

roots = [fid for fid in tr_go if tr_parent.get(fid) is None]
for r in roots:
    walk(r, 0)

print('\n'.join(lines))
