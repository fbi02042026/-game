# -*- coding: utf-8 -*-
"""盘点 Assets 下所有 [MenuItem]，并把每个脚本里提到的 .prefab 路径检查是否存在。
用途：判断「预制体生成器」类菜单哪些的产物已经存在（可以删）。
用法: python audit_menus.py [--exists-only]
"""
import io, os, re, sys

ROOT = r"E:\xiangsumaoxian"
ASSETS = os.path.join(ROOT, "Assets")
SKIP_DIRS = ("SPUM",)          # 第三方插件，不动


def scan():
    rows = []
    for dp, dn, fn in os.walk(ASSETS):
        rel_dir = os.path.relpath(dp, ROOT)
        if any(rel_dir.startswith("Assets\\" + s) or rel_dir.startswith("Assets/" + s) for s in SKIP_DIRS):
            continue
        for f in fn:
            if not f.endswith(".cs"):
                continue
            p = os.path.join(dp, f)
            try:
                s = io.open(p, "r", encoding="utf-8", errors="replace").read()
            except Exception:
                continue
            menus = re.findall(r'MenuItem\(\s*"([^"]+)"', s)
            if not menus:
                continue
            # 找 .prefab 路径字样
            paths = set(re.findall(r'"([^"\n]*?\.prefab)"', s))
            exists = []
            for q in paths:
                q2 = q.replace("\\\\", "\\")
                if q2.startswith("Assets/") or q2.startswith("Assets\\"):
                    fp = os.path.join(ROOT, q2.replace("/", os.sep))
                    exists.append((q2, os.path.exists(fp)))
            rows.append((os.path.relpath(p, ROOT), menus, exists))
    return rows


def main():
    exists_only = "--exists-only" in sys.argv
    rows = scan()
    print("== 含菜单的脚本 %d 个 ==\n" % len(rows))
    for rel, menus, exists in sorted(rows):
        if exists_only and not exists:
            continue
        print("### " + rel)
        for m in menus:
            print("    menu: " + m)
        for q, ok in sorted(exists):
            print("    prefab: [%s] %s" % ("存在" if ok else "缺失", q))
        if not exists:
            print("    prefab: （脚本里没有 .prefab 路径）")
        print("")


if __name__ == "__main__":
    main()
