# -*- coding: utf-8 -*-
"""评估回滚 7e526d69（581 个 .meta 的 base64 -> 随机 hex）会不会造成二次破坏。

回滚后这 581 个资源的 guid 会变回 base64。若现在已有 prefab/场景引用了
那批"新 hex guid"，回滚就会把它们打断。本脚本统计引用次数，给出结论。
"""
import os
import re
import subprocess
from collections import Counter

PROJ = r"E:\xiangsumaoxian"
COMMIT = "7e526d69"
REF_EXT = (".prefab", ".unity", ".asset", ".mat", ".controller", ".anim")
GUID_RE = re.compile(r"guid:\s*([0-9a-zA-Z+/=]{16,})")
SKIP = ("/SPUM/", "Epic Toon FX", "Pixel Craft VFX", "Hyperbit", "/Demo/")


def diff_pairs():
    """返回 [(file, old_guid, new_guid)]"""
    out = subprocess.run(
        ["git", "show", "--unified=0", "--format=", COMMIT],
        cwd=PROJ, stdout=subprocess.PIPE)
    text = out.stdout.decode("utf-8", "ignore")
    pairs = []
    cur = None
    old = new = None
    for line in text.splitlines():
        if line.startswith("diff --git"):
            if cur and old and new:
                pairs.append((cur, old, new))
            cur = line.split(" b/")[-1]
            old = new = None
        elif line.startswith("-guid:"):
            old = line[6:].strip()
        elif line.startswith("+guid:"):
            new = line[6:].strip()
    if cur and old and new:
        pairs.append((cur, old, new))
    return pairs


def count_refs(guids):
    """统计这批 guid 在游戏本体里被引用的次数"""
    cnt = Counter()
    for dp, _dn, fn in os.walk(os.path.join(PROJ, "Assets")):
        for f in fn:
            if not f.endswith(REF_EXT):
                continue
            p = os.path.join(dp, f).replace("\\", "/")
            rel = os.path.relpath(p, PROJ).replace("\\", "/")
            if any(s in rel for s in SKIP):
                continue
            with open(p, encoding="utf-8", errors="ignore") as fh:
                text = fh.read()
            for g in set(GUID_RE.findall(text)):
                if g in guids:
                    cnt[g] += 1
    return cnt


def main():
    pairs = diff_pairs()
    print("%s 改动文件: %d" % (COMMIT, len(pairs)))
    if not pairs:
        return 1

    new_guids = {new for _f, _o, new in pairs}
    old_guids = {old for _f, old, _n in pairs}
    print("新 hex guid: %d   旧 base64 guid: %d" % (len(new_guids), len(old_guids)))

    cnt = count_refs(new_guids)
    used = {g: n for g, n in cnt.items() if n > 0}
    print()
    print("=" * 66)
    print("回滚影响评估")
    print("=" * 66)
    print("  这批新 hex guid 中，当前被游戏本体引用的: %d / %d" % (len(used), len(new_guids)))
    total_ref = sum(used.values())
    print("  引用总次数: %d" % total_ref)

    if used:
        print()
        print("  !! 回滚会把下面这些引用打断（Top 15）：")
        for g, n in sorted(used.items(), key=lambda x: -x[1])[:15]:
            f = next((f for f, _o, nw in pairs if nw == g), "?")
            print("     %2d 次  %s  (%s)" % (n, g, os.path.basename(f)))
        print()
        print("  → 结论：回滚会造成二次破坏，建议【保留】7e526d69。")
        print("    若一定要回滚，需同步把这 %d 处引用改回旧 base64 值。" % total_ref)
    else:
        print()
        print("  → 结论：无人引用这批新 guid，回滚【安全】，不会二次破坏。")
        print("    回滚命令：git revert --no-commit %s" % COMMIT)
    return 0


if __name__ == "__main__":
    sys.exit(main()) if False else main()
