# -*- coding: utf-8 -*-
"""对比若干历史提交的"悬空 guid 数量"，定位破坏发生的时点。

不检出文件，全部通过 git cat-file 在内存里比对。
"""
import re
import subprocess
import sys

PROJ = r"E:\xiangsumaoxian"
GUID_RE = re.compile(r"guid:\s*([0-9a-zA-Z+/=]{16,})")
HEX32 = re.compile(r"^[0-9a-f]{32}$")
B64 = re.compile(r"^[A-Za-z0-9+/]{16,}={0,2}$")
BUILTIN = re.compile(r"^0{8}[0-9a-f]{24}$")
META_GUID = re.compile(rb"^guid:\s*(\S+)", re.M)

REF_EXT = (".prefab", ".unity", ".asset", ".mat", ".controller", ".anim")


def g(args, binary=False):
    p = subprocess.run(["git"] + args, cwd=PROJ, stdout=subprocess.PIPE,
                       stderr=subprocess.PIPE)
    return p.stdout if binary else p.stdout.decode("utf-8", "ignore")


def cat_batch(shas):
    """返回 {sha: bytes}"""
    out = {}
    CHUNK = 1500
    for i in range(0, len(shas), CHUNK):
        chunk = shas[i:i + CHUNK]
        inp = b"".join(s.encode() + b"\n" for s in chunk)
        p = subprocess.run(["git", "cat-file", "--batch"], cwd=PROJ,
                           input=inp, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        data = p.stdout
        pos = 0
        for sha in chunk:
            nl = data.find(b"\n", pos)
            if nl < 0:
                break
            header = data[pos:nl].decode("utf-8", "ignore").split()
            if len(header) < 3:
                pos = nl + 1
                continue
            size = int(header[2])
            out[sha] = data[nl + 1: nl + 1 + size]
            pos = nl + 1 + size + 1
    return out


def analyze(commit):
    tree = g(["ls-tree", "-r", "-z", commit])
    metas = []
    refs = []
    for entry in tree.split("\0"):
        if not entry:
            continue
        # "<mode> <type> <sha>\t<path>"
        try:
            meta_part, path = entry.split("\t", 1)
        except ValueError:
            continue
        sha = meta_part.split()[2]
        if path.endswith(".meta"):
            metas.append((sha, path))
        elif path.endswith(REF_EXT):
            refs.append((sha, path))
    blobs = cat_batch([s for s, _ in metas] + [s for s, _ in refs])
    idx = set()
    for sha, path in metas:
        body = blobs.get(sha)
        if not body:
            continue
        m = META_GUID.search(body)
        if m:
            idx.add(m.group(1).decode("utf-8", "ignore"))
    bad = set()
    for sha, path in refs:
        body = blobs.get(sha)
        if not body:
            continue
        for gg in set(GUID_RE.findall(body.decode("utf-8", "ignore"))):
            if gg in idx:
                continue
            if BUILTIN.match(gg):
                continue
            if not (HEX32.match(gg) or B64.match(gg)):
                continue
            bad.add(gg)
    return len(idx), len(bad)


def main():
    commits = sys.argv[1:]
    if not commits:
        commits = ["HEAD", "7e526d69", "7e526d69^", "7e526d69~2", "167e9937"]
    print("%-14s %10s %10s" % ("提交", ".meta数", "悬空guid"))
    for c in commits:
        try:
            n, bad = analyze(c)
            print("%-14s %10d %10d" % (c, n, bad))
        except Exception as e:
            print("%-14s   错误 %s" % (c, e))


if __name__ == "__main__":
    main()
