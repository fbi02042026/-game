# -*- coding: utf-8 -*-
"""提交前扫描：区分真实改动 / CRLF 假改动 / 删除 / 未跟踪垃圾。"""
import subprocess, os, sys

ROOT = r"E:\xiangsumaoxian"

def git(*args):
    r = subprocess.run(["git"] + list(args), cwd=ROOT, capture_output=True)
    return r.stdout.decode("utf-8", "replace")

status = git("status", "--short").splitlines()
numstat = git("diff", "--numstat", "--diff-filter=d").splitlines()

real = {}
for l in numstat:
    if not l.strip() or l.startswith("warning"):
        continue
    parts = l.split("\t")
    if len(parts) >= 3:
        real[parts[2]] = (parts[0], parts[1])

mod = [l[3:] for l in status if l.startswith(" M")]
deleted = [l[3:] for l in status if l.startswith(" D")]
renamed = [l[3:] for l in status if l.startswith("R")]
untracked = [l[3:] for l in status if l.startswith("??")]

fake = [m for m in mod if m not in real]

print("status 总条目:", len(status))
print("真实改动(worktree vs index):", len(real))
print("删除:", len(deleted))
print("已暂存重命名:", len(renamed))
for r in renamed:
    print("   R", r)
print("未跟踪:", len(untracked))
print()
print("=== 假改动（status M 但 diff 为空，疑似仅换行符差异）:", len(fake))
for f in fake:
    print("   ", f)

# 对假改动做 hash-object 判别
print()
print("=== hash-object 判别（相等=干净，别 add） ===")
for f in fake:
    p = os.path.join(ROOT, f)
    if not os.path.exists(p):
        print("   [已不在磁盘]", f)
        continue
    h = git("hash-object", "--path=" + f, "--", f).strip()
    ls = git("ls-files", "-s", "--", f).strip()
    blob = ls.split()[1] if ls.split() else ""
    print("   %-70s %s" % (f, "CLEAN" if h == blob else "DIFFERENT  h=%s idx=%s" % (h[:8], blob[:8])))

print()
print("=== 未跟踪清单 ===")
for u in untracked:
    print("   ", u)
