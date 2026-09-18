# -*- coding: utf-8 -*-
"""
EnsureMeta.py —— 检查（并可选补齐）Assets 下缺失的 .meta 文件。

背景：团结/Unity 只有在编辑器运行时才会为新资源生成 .meta。
如果资源是脚本批量产出、或引擎没开着的时候放进来的，就会缺 .meta，
导致「资源/代码/.meta 全部入库」这条纪律破功（换机器拉下来 guid 会重新生成）。

用法：
    python Tools/EnsureMeta.py            # 只体检，列出缺 .meta 的资源
    python Tools/EnsureMeta.py --fix      # 自动补齐（复制同类 .meta 作模板 + 换新 guid）

补齐策略（保守）：
    - 绝不覆盖已存在的 .meta；
    - 模板取自工程内**同扩展名**的现有 .meta（保证格式与当前引擎版本一致）；
    - 只替换模板里的 guid（32 位 hex），其余字段原样保留；
    - 新 guid 全工程查重，保证唯一。

建议跑完 --fix 后，在 Unity 里确认一次导入设置（尤其是图片的 Sprite/纹理类型）。
"""

import os
import re
import sys
import uuid

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
ASSETS = os.path.join(ROOT, "Assets")
EXCLUDE_DIRS = {"2022.3.62t12", "Library", "Temp", "Logs"}

# Unity 会为其生成 .meta 的扩展名
TRACKED_EXT = {
    ".png", ".jpg", ".jpeg", ".tga", ".psd", ".exr", ".hdr", ".gif", ".bmp",
    ".cs", ".prefab", ".unity", ".mat", ".asset", ".anim", ".controller",
    ".physicMaterial2D", ".overrideController", ".shader", ".shadergraph",
    ".cginc", ".hlsl", ".compute", ".ttf", ".otf", ".fontsettings",
    ".wav", ".mp3", ".ogg", ".aif", ".json", ".txt", ".bytes", ".csv", ".md",
    ".xml", ".asmdef", ".asmref", ".uxml", ".uss", ".spriteatlas", ".preset",
}

GUID_RE = re.compile(r"^guid:\s*([0-9a-fA-F]{32})", re.M)


def collect_existing_guids():
    guids = set()
    for dp, dn, fn in os.walk(ASSETS):
        dn[:] = [d for d in dn if d not in EXCLUDE_DIRS]
        for f in fn:
            if not f.endswith(".meta"):
                continue
            try:
                with open(os.path.join(dp, f), encoding="utf-8", errors="ignore") as fh:
                    head = fh.read(400)
            except Exception:
                continue
            m = GUID_RE.search(head)
            if m:
                guids.add(m.group(1).lower())
    return guids


def find_template(ext, exclude_dir):
    """找一个同扩展名的现有 .meta 当模板（优先排除目录之外的同类型文件）。"""
    for dp, dn, fn in os.walk(ASSETS):
        dn[:] = [d for d in dn if d not in EXCLUDE_DIRS]
        if os.path.abspath(dp) == os.path.abspath(exclude_dir):
            continue
        for f in sorted(fn):
            if f.endswith(ext + ".meta"):
                return os.path.join(dp, f)
    return None


def scan():
    missing = []
    for dp, dn, fn in os.walk(ASSETS):
        dn[:] = [d for d in dn if d not in EXCLUDE_DIRS]
        for f in sorted(fn):
            ext = os.path.splitext(f)[1].lower()
            if ext not in TRACKED_EXT:
                continue
            if os.path.exists(os.path.join(dp, f + ".meta")):
                continue
            missing.append(os.path.join(dp, f))
    return missing


def main():
    do_fix = "--fix" in sys.argv
    missing = scan()

    if not missing:
        print("[EnsureMeta] 没有缺少 .meta 的资源，全部入库 ✓")
        return

    print(f"[EnsureMeta] 缺少 .meta 的资源：{len(missing)} 个")
    for p in missing[:30]:
        print("   ", os.path.relpath(p, ROOT))
    if len(missing) > 30:
        print(f"    ... 另有 {len(missing) - 30} 个")

    if not do_fix:
        print()
        print("只体检未修改。要自动补齐请加 --fix：")
        print("    python Tools/EnsureMeta.py --fix")
        return

    guids = collect_existing_guids()
    made, skipped = 0, []
    for p in missing:
        ext = os.path.splitext(p)[1].lower()
        tpl = find_template(ext, os.path.dirname(p))
        if not tpl:
            skipped.append(p)
            continue
        try:
            body = open(tpl, encoding="utf-8", errors="ignore").read()
        except Exception:
            skipped.append(p)
            continue
        guid = uuid.uuid4().hex
        while guid in guids:
            guid = uuid.uuid4().hex
        guids.add(guid)
        new = re.sub(r"^guid:\s*[0-9a-fA-F]{32}", "guid: " + guid, body, count=1, flags=re.M)
        with open(p + ".meta", "w", encoding="utf-8", newline="\n") as fh:
            fh.write(new)
        made += 1

    print()
    print(f"[EnsureMeta] 已补齐 {made} 个 .meta")
    if skipped:
        print(f"[EnsureMeta] 找不到同类模板、需手动处理：{len(skipped)} 个")
        for p in skipped[:10]:
            print("   ", os.path.relpath(p, ROOT))
    print("[EnsureMeta] 提醒：补齐后在 Unity 里确认一次导入设置（图片的 Sprite/纹理类型）")


if __name__ == "__main__":
    main()
