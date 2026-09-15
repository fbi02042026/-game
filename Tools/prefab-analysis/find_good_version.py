# -*- coding: utf-8 -*-
"""在 git 历史里找「引用还有效」的 prefab 版本。

思路：某个 prefab 的旧版本可能引用的是**当时工程里存在、且现在也还在**的图片。
只要找到这样的版本，直接 checkout 那一个文件即可，不用删工程也不用手工配图。
"""
import os
import re
import subprocess
import sys

PROJ = r"E:\xiangsumaoxian"
SPRITE = re.compile(r"m_Sprite: \{fileID: (-?\d+), guid: ([0-9a-zA-Z+/=]{16,}), type: 3\}")
BUILTIN = re.compile(r"^0{8}[0-9a-f]{24}$")


def build_index():
    idx = {}
    for base in ["Assets", "Packages", os.path.join("Library", "PackageCache")]:
        abs_base = os.path.join(PROJ, base)
        if not os.path.isdir(abs_base):
            continue
        for dp, _dn, fn in os.walk(abs_base):
            for f in fn:
                if not f.endswith(".meta"):
                    continue
                p = os.path.join(dp, f)
                with open(p, encoding="utf-8", errors="ignore") as fh:
                    m = re.search(r"^guid:\s*(\S+)", fh.read(6000), re.M)
                if m:
                    idx[m.group(1)] = os.path.relpath(p[:-5], PROJ).replace("\\", "/")
    return idx


def show(commit, path):
    p = subprocess.run(["git", "show", "%s:%s" % (commit, path)], cwd=PROJ,
                       stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    return p.stdout.decode("utf-8", "ignore")


def main():
    rel = sys.argv[1] if len(sys.argv) > 1 else \
        "Assets/Resources/Prefabs/UI/PlayerJobSelect.prefab"
    idx = build_index()
    print("图片索引: %d" % len(idx))
    print("目标文件: %s\n" % rel)

    out = subprocess.run(["git", "log", "--format=%H|%ad|%s", "--date=short", "--", rel],
                         cwd=PROJ, stdout=subprocess.PIPE)
    commits = [l for l in out.stdout.decode("utf-8", "ignore").splitlines() if "|" in l]
    print("历史版本数: %d\n" % len(commits))
    print("%-12s %-10s %6s %6s  %s" % ("提交", "日期", "有效", "悬空", "说明"))
    print("-" * 96)

    best = []
    seen_blob = set()
    for line in commits:
        h, date, subj = line.split("|", 2)
        text = show(h, rel)
        if not text.strip():
            continue
        # 同一 blob 不重复统计
        bp = subprocess.run(["git", "rev-parse", "%s:%s" % (h, rel)], cwd=PROJ,
                            stdout=subprocess.PIPE)
        blob = bp.stdout.decode().strip()
        if blob in seen_blob:
            continue
        seen_blob.add(blob)

        gs = set(g for _f, g in SPRITE.findall(text))
        ok = sum(1 for g in gs if g in idx or BUILTIN.match(g))
        bad = len(gs) - ok
        flag = ""
        if bad == 0 and gs:
            flag = "   <<< 全部有效"
            best.append((h, date, len(gs), subj))
        print("%-12s %-10s %6d %6d  %s%s" % (
            h[:10], date, ok, bad, subj[:44], flag))

    print()
    if best:
        print("=" * 96)
        print("找到 %d 个「所有 sprite 引用都有效」的版本：" % len(best))
        for h, date, n, subj in best:
            print("  %s  %s  %d 个引用  %s" % (h[:10], date, n, subj[:50]))
        newest = best[0]
        print()
        print("推荐还原（最新且全有效）：")
        print('  git checkout %s -- "%s"' % (newest[0], rel))
    else:
        print("没有找到全部有效的版本 —— 说明这个文件从来就没完整过。")
    return 0


if __name__ == "__main__":
    sys.exit(main())
