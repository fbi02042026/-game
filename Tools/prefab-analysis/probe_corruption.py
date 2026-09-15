# -*- coding: utf-8 -*-
"""探查 guid 损坏来源：3 个当前非法 .meta 的 HEAD/工作区/历史 guid。"""
import subprocess, os

R = r"E:\xiangsumaoxian"

def git(*a):
    p = subprocess.run(["git", "-C", R] + list(a),
                       capture_output=True, text=True, encoding="utf-8", errors="replace")
    return p.stdout

def own_guid(text):
    for line in text.splitlines():
        if line.startswith("guid:"):
            return line.split(":", 1)[1].strip()
    return "<none>"

files = [
    "Assets/Art/Video/opening_intro.mp4.meta",
    "Assets/Resources/Prefabs/UI/PlayerJobSelect.prefab.meta",
    "Assets/Resources/UI/Boot/登录动画.mp4.meta",
]
for f in files:
    print("=" * 70)
    print(f)
    print("  HEAD :", own_guid(git("show", "HEAD:" + f)))
    p = os.path.join(R, f.replace("/", os.sep))
    try:
        cur = open(p, "rb").read().decode("utf-8", "replace")
        print("  WORK :", own_guid(cur))
    except Exception as e:
        print("  WORK : <err>", e)
    log = git("log", "--format=%H|%ad|%s", "--date=short", "--", f)
    lines = [l for l in log.splitlines() if l.strip()]
    print("  history (%d commits):" % len(lines))
    for ln in lines[:15]:
        h, rest = ln.split("|", 1)
        c = git("show", "%s:%s" % (h, f))
        print("    %s  %-12s  guid=%s" % (h[:8], rest.split("|")[0], own_guid(c)))
    if len(lines) > 15:
        print("    ... +%d more" % (len(lines) - 15))
