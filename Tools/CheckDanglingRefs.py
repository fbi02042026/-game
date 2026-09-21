# -*- coding: utf-8 -*-
"""
团结/Unity 工程断链（悬空引用）扫描器

背景（2026-09-16 事故）：
    本工程曾出现大量 prefab / scene 引用了工程里根本不存在的 guid，
    表现为 Inspector 里 "Missing Script"、UI 上图片空掉。
    guid 的唯一真值是 Assets 下的 .meta 文件；Library/ 只是导入缓存，
    删 Library 重新导入**修不了**断链，只会让问题暴露得更彻底。

用法：
    python Tools/CheckDanglingRefs.py                 # 扫游戏本体（默认范围）
    python Tools/CheckDanglingRefs.py --all           # 扫整个 Assets
    python Tools/CheckDanglingRefs.py --strict        # 有脚本类断链就以非 0 退出（CI/钩子用）

判定：
    1) 悬空引用：被引用的 guid 不在表里（含内置资源跳过）
    2) 形态非法：.meta 的 guid 不是「32 位十六进制」也不是「56 字符 base64（解码 41 字节）」
       —— 只有这两种形态团结引擎接受；44 字符 base64（解码 32 字节）会被拒绝并忽略该资源。
       详见 guid_kind() 的注释，别再把 581 个合法 base64 当成非法去改。

2026-09-21 精确化改造（主人反馈「探针能不能精确一点」）：
    以前只按 guid 汇总，导致「真缺失」和「查不到但无害」混在一起，每次都要肉眼复核。
    现在：
      A. **按字段分危害等级**，输出时直接标出来：
           m_Script                → 🔴 必修（Missing Script，组件丢功能）
           m_Sprite / m_Texture    → 🟡 玩家可见（UI 上会空图/粉块）
           m_Material / m_Shader   → 🟢 低危（大量内置材质 guid 本就不在 Assets 里，UI 上看不出来）
           其它（m_CorrespondingSourceObject / m_SourcePrefab …）→ ⚪ 场景实例引用，多为重命名残留
      B. **打印对象名 + 行号**（就近取 m_Name），点开文件就能定位，不用全文搜 guid。
      C. **豁免清单** Tools/dangling_allowlist.txt：已人工确认无害的 guid 写一行，
         输出时单独归到「已确认无害」，并且**不参与 --strict 退出码**。
         这样每次体检真正需要看的，只剩"新冒出来的"那几条。
"""
import argparse
import base64
import pathlib
import re
import sys
from collections import defaultdict

# 换台电脑也能跑：脚本输出带 emoji，中文 Windows 默认控制台是 GBK，
# 不强制 UTF-8 会直接 UnicodeEncodeError 崩掉（2026-09-21 加）。
for _s in (sys.stdout, sys.stderr):
    try:
        _s.reconfigure(encoding='utf-8', errors='replace')
    except Exception:
        pass

ROOT = pathlib.Path(__file__).resolve().parent.parent

RG = re.compile(rb'guid:\s*([0-9A-Za-z+/\-_=]+)')
BUILTIN = re.compile(r'^0{16}[0-9a-f]0{15}$')
HEX32 = re.compile(r'^[0-9a-fA-F]{32}$')

GUID_DIRS = ['Assets', 'Packages', 'Library/PackageCache']
DEFAULT_SCAN = ['Assets/Resources/Prefabs', 'Assets/Scenes', 'Assets/Resources/UI']
EXTS = {'.prefab', '.unity', '.asset', '.mat', '.anim', '.controller',
        '.overridecontroller', '.playable', '.spriteatlas', '.physicsmaterial2d'}

# 第三方插件的 Demo/示例场景，断链属正常，不算问题
NOISE = ('/Demo/', '/DemoResources/', 'PreviewScene', 'Sample',
         'Sprite Shaders Ultimate', 'Epic Toon FX', 'SPUM',
         'Hyperbit', 'Pixel Craft VFX URP', 'UIShaderEffects',
         '2D Pixel RPG Monster Pack', 'RPG Props and Items')

# 已人工确认无害的 guid 清单（每行「guid  # 说明」，# 后为注释）
ALLOWLIST_PATH = ROOT / 'Tools' / 'dangling_allowlist.txt'

# 字段 → (危害等级, 说明)。等级只影响输出分组与措辞，不改变判定本身。
FIELD_RISK = [
    ('m_Script', 3, '🔴 必修', '组件脚本丢失（Missing Script）'),
    ('m_Sprite', 2, '🟡 可见', '精灵丢失，UI 上会空图/粉块'),
    ('m_Texture', 2, '🟡 可见', '贴图丢失，UI 上会空图/粉块'),
    ('m_Material', 1, '🟢 低危', '材质丢失，多为引擎内置材质 guid，界面通常看不出'),
    ('m_Shader', 1, '🟢 低危', '着色器丢失，多为引擎内置，界面通常看不出'),
]


def field_risk(fields):
    """按字段给出 (等级数字, 标记, 说明)。取命中的最高等级。"""
    best = (0, '⚪ 参考', '场景/预制体实例引用，多为改名后的残留')
    for name, lvl, tag, desc in FIELD_RISK:
        if name in fields and lvl > best[0]:
            best = (lvl, tag, desc)
    return best


def load_allowlist():
    """读豁免清单 → {guid: 说明}。文件不存在时返回空表（不报错）。"""
    out = {}
    if not ALLOWLIST_PATH.exists():
        return out
    for raw in ALLOWLIST_PATH.read_text(encoding='utf-8', errors='ignore').splitlines():
        line = raw.split('#', 1)[0].strip()
        if not line:
            continue
        parts = line.split()
        if len(parts) < 1:
            continue
        note = raw.split('#', 1)[1].strip() if '#' in raw else ''
        out[parts[0].lower()] = note
    return out


def guid_kind(g):
    """判断 .meta 里 guid 的形态。

    2026-09-16 实测全工程 7746 个 .meta：
      32 字符十六进制              x 7164  合法（标准形态）
      56 字符 base64（解码 41 字节）x  581  合法（团结引擎自有格式，千万别改）
      44 字符 base64（解码 32 字节）x    1  非法，引擎直接忽略该资源

    早期版本曾把 581 个合法的 56 字符 base64 误判为非法并全量改写，
    导致 582 个资源重导入、UI 大面积缺图。判定务必按字节数，不要只看"是不是 base64"。
    """
    if HEX32.match(g):
        return 'hex32'
    try:
        raw = g.encode('ascii')
        dec = base64.b64decode(raw + b'=' * (-len(raw) % 4))
    except Exception:
        return 'unknown'
    if len(dec) == 41:
        return 'b64-ok'      # 团结引擎合法格式
    if len(dec) == 32:
        return 'b64-bad'     # 唯一被引擎拒绝的形态
    return 'unknown'


def bad_form_metas():
    """扫出形态非法的 .meta（引擎会忽略这些资源）。"""
    out = []
    for p in (ROOT / 'Assets').rglob('*.meta'):
        try:
            m = RG.search(p.read_bytes())
        except Exception:
            continue
        if not m:
            continue
        g = m.group(1).decode()
        k = guid_kind(g)
        if k in ('b64-bad', 'unknown'):
            out.append((p.relative_to(ROOT).as_posix(), g, k))
    return out


def build_guid_table():
    table = {}
    for base in GUID_DIRS:
        d = ROOT / base
        if not d.exists():
            continue
        for p in d.rglob('*.meta'):
            try:
                m = RG.search(p.read_bytes())
            except Exception:
                continue
            if m:
                table.setdefault(m.group(1).decode(), p.relative_to(ROOT).as_posix())
    return table


def scan(dirs, table):
    """返回 (script_hits, asset_hits, ctx)。

    ctx[guid] = [(文件, 行号, 字段, 所属文档起始行, GameObject fileID)]，每条最多留 6 条。
    「所属文档起始行」= 最近一个 `--- !u!NNN` 的行号，滚到那儿就能看到这个对象的名字。
    """
    script_hits = defaultdict(set)   # guid -> files
    asset_hits = defaultdict(lambda: defaultdict(set))  # guid -> field -> files
    ctx = defaultdict(list)
    doc_re = re.compile(r'^--- !u!(\d+)')
    go_re = re.compile(r'^\s*m_GameObject:\s*\{fileID:\s*(-?\d+)\}')
    for d in dirs:
        dd = ROOT / d
        if not dd.exists():
            continue
        for p in dd.rglob('*'):
            if not p.is_file() or p.suffix.lower() not in EXTS:
                continue
            rel = p.relative_to(ROOT).as_posix()
            try:
                txt = p.read_bytes().decode('utf-8', 'replace')
            except Exception:
                continue
            doc_start = 0
            cur_go = ''
            for i, ln in enumerate(txt.splitlines()):
                if doc_re.match(ln):
                    doc_start = i + 1
                    cur_go = ''
                gmo = go_re.match(ln)
                if gmo:
                    cur_go = gmo.group(1)
                m = re.search(r'guid:\s*([0-9A-Za-z+/\-_=]+)', ln)
                if not m:
                    continue
                g = m.group(1)
                if BUILTIN.match(g) or g in table:
                    continue
                if 'm_Script' in ln:
                    script_hits[g].add(rel)
                    field = 'm_Script'
                else:
                    field = ln.strip().split(':')[0].strip()
                    asset_hits[g][field].add(rel)
                if len(ctx[g]) < 6:
                    ctx[g].append((rel, i + 1, field, doc_start, cur_go))
    return script_hits, asset_hits, ctx


def is_noise(files):
    return all(any(n in f for n in NOISE) for f in files)


def _ctx_lines(g, ctx):
    """把某 guid 的引用位置渲染成几行可读文本（文件:行 + 字段 + 对象块位置）。"""
    out = []
    for rel, lineno, field, doc_start, go in ctx.get(g, [])[:3]:
        extra = ('  对象块起自第 %d 行' % doc_start) if doc_start else ''
        if go:
            extra += ' / GO fileID=%s' % go
        out.append('        %s:%d  [%s]%s' % (rel, lineno, field, extra))
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--all', action='store_true', help='扫整个 Assets（含第三方插件 Demo）')
    ap.add_argument('--strict', action='store_true', help='发现**未豁免**的脚本类断链时退出码为 1')
    args = ap.parse_args()

    print('[1/2] 建立 guid 表 ...')
    table = build_guid_table()
    print('      共 %d 个 guid' % len(table))

    dirs = ['Assets'] if args.all else DEFAULT_SCAN
    print('[2/2] 扫描 %s ...' % ('整个 Assets' if args.all else '游戏本体'))
    script_hits, asset_hits, ctx = scan(dirs, table)

    allow = load_allowlist()
    print('      豁免清单 %d 条（Tools/dangling_allowlist.txt）' % len(allow))

    # 汇总成 (风险等级, guid, 字段表, 文件数, 文件表)
    findings = []
    for g, files in script_hits.items():
        if is_noise(files):
            continue
        findings.append((3, g, ['m_Script'], len(files), sorted(files)))
    for g, fields in asset_hits.items():
        files = set()
        for k in fields:
            files |= fields[k]
        if is_noise(files):
            continue
        lvl, _tag, _desc = field_risk(set(fields.keys()))
        findings.append((lvl, g, sorted(fields.keys()), len(files), sorted(files)))

    fresh = [f for f in findings if f[1].lower() not in allow]
    known = [f for f in findings if f[1].lower() in allow]
    fresh.sort(key=lambda x: (-x[0], -x[3]))
    known.sort(key=lambda x: (-x[0], -x[3]))

    print()
    print('===== 需要处理的（未豁免）=====')
    if not fresh:
        print('  无 ✅')
    for lvl, g, fields, n, files in fresh:
        _l, tag, desc = field_risk(set(fields))
        print('  %s %s | %s | %d 个文件 — %s' % (tag, g, ','.join(fields[:4]), n, desc))
        if ctx.get(g):
            for line in _ctx_lines(g, ctx):
                print(line)
        else:
            for f in files[:3]:
                print('        ', f)

    print()
    print('===== 已确认无害（豁免清单里的）=====')
    if not known:
        print('  无')
    for lvl, g, fields, n, files in known:
        _l, tag, desc = field_risk(set(fields))
        note = allow.get(g.lower(), '')
        print('  %s %s | %s | %d 个文件%s' % (
            tag, g, ','.join(fields[:4]), n, ('   # ' + note) if note else ''))
        for line in _ctx_lines(g, ctx)[:2]:
            print(line)

    print()
    print('===== 形态非法的 .meta guid（引擎会忽略该资源）=====')
    bad = bad_form_metas()
    if not bad:
        print('  无 ✅')
    for rel, g, k in bad:
        print('  [%s] %s' % (k, rel))
        print('        guid=%s' % g)

    print()
    print('===== 汇总 =====')
    n_script = sum(1 for f in fresh if f[2] == ['m_Script'])
    print('  未豁免断链 guid 数   : %d（其中脚本类 %d）' % (len(fresh), n_script))
    print('  已豁免（确认无害）数 : %d' % len(known))
    print('  形态非法 .meta 数    : %d' % len(bad))
    if args.strict and (n_script > 0 or bad):
        sys.exit(1)


if __name__ == '__main__':
    main()
