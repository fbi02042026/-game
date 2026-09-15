# -*- coding: utf-8 -*-
"""针对 PlayerJobSelect 被清空的 13 个 guid，在 git 全历史 .meta 里溯源"""
import os
import re
import subprocess

PROJ = r"E:\xiangsumaoxian"
G13 = """20e0157fc1c7561439853e342ae6003b
6350c1c1d19343243aafc10a3a47780b
6a5739df367d64146a264e9d1e727b91
6dfb85e55ccc7494283dab763e1ae787
7b9a3c75b1b331e4d9011dce3a594f5e
81073cf40c6d23f4aa131d031dc59a00
9c54efdf5c3e03d489265d31be6000f8
a33daf5bb6b93714083fe44405b948d2
bf9b25478854ea94a9a065a30cb14da3
d04831f79b0535446a671ebc442dd1db
fa2518063812ce642a8f861fff6f4c52
fdc2db57c3db7f748a6ec68f6fefbaf6
fe989e41377721d46b9a0dbc67e63cc6""".split()


def main():
    wanted = set(G13)
    out = subprocess.run(["git", "rev-list", "--objects", "--all"], cwd=PROJ,
                         stdout=subprocess.PIPE).stdout
    shas = []
    for line in out.split(b"\n"):
        if not line:
            continue
        parts = line.split(b" ", 1)
        if len(parts) == 2 and parts[1].endswith(b".meta"):
            shas.append((parts[0].decode(), parts[1].decode("utf-8", "ignore")))
    print("历史 .meta 对象: %d" % len(shas))

    hist = {}
    CHUNK = 2000
    for i in range(0, len(shas), CHUNK):
        chunk = shas[i:i + CHUNK]
        inp = b"".join(s.encode() + b"\n" for s, _p in chunk)
        p = subprocess.run(["git", "cat-file", "--batch"], cwd=PROJ, input=inp,
                           stdout=subprocess.PIPE)
        data = p.stdout
        pos = 0
        for sha, path in chunk:
            nl = data.find(b"\n", pos)
            if nl < 0:
                break
            h = data[pos:nl].decode("utf-8", "ignore").split()
            if len(h) < 3:
                pos = nl + 1
                continue
            size = int(h[2])
            body = data[nl + 1: nl + 1 + size]
            pos = nl + 1 + size + 1
            m = re.search(rb"^guid:\s*(\S+)", body, re.M)
            if m:
                g = m.group(1).decode("utf-8", "ignore")
                if g in wanted:
                    hist.setdefault(g, set()).add(path)

    print("命中 %d / %d" % (len(hist), len(wanted)))
    for g in G13:
        paths = hist.get(g)
        if paths:
            print("  %s -> %s" % (g, sorted(paths)))
        else:
            print("  %s -> 【历史中也不存在】" % g)

    # 这些路径现在是否存在？
    print("\n--- 命中路径当前是否存在 ---")
    for g, paths in hist.items():
        for pth in sorted(paths):
            full = os.path.join(PROJ, pth)
            cur = None
            if os.path.isfile(full + ".meta"):
                with open(full + ".meta", encoding="utf-8", errors="ignore") as fh:
                    m = re.search(r"^guid:\s*(\S+)", fh.read(6000), re.M)
                cur = m.group(1) if m else None
            print("  %s  存在=%s  当前guid=%s" % (pth, os.path.isfile(full), cur))


if __name__ == "__main__":
    main()
