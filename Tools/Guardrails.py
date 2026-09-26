# -*- coding: utf-8 -*-
"""
Guardrails.py —— 防复发闸门（对应 Docs/模块化与防复发方案_2026-09-26.md 规则 R4「修复即断言」）。

背景：项目里有些 bug 修好过几天又回来，根因是「修过的东西被改回去时没人报错」。
最硬的证据是提交 e3f176f7：
    fix(core): Singleton 构造期访问 isPlaying 抛 UnityException —— 重新应用 9-15 的修复
同一个修复被应用了两次，说明中间有提交把它冲掉了，而当时没有任何东西拦住。

本脚本把「已经复发过的点」写成断言：谁再改回去，提交前就会当场叫。

用法：
    python Tools/Guardrails.py          # 体检，有问题列出来
    python Tools/Guardrails.py --strict # 有 ERROR 项时退出码 1（可接进提交流程）

退出码：0 = 通过；1 = 存在 ERROR（或 --strict 下存在 WARN）；2 = 脚本自身跑不起来。

新增断言的约定：
    每修一个「复发过」的 bug，就在 CHECKS 里加一条，注释写清：哪天修的、被冲掉过几次、
    正确的样子是什么。别写模糊断言，要写能一眼看出「对/错」的那种。
"""

import os
import re
import sys
import subprocess

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8")

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
SCRIPTS = os.path.join(ROOT, "Assets", "Scripts")

ERRORS = []
WARNS = []


def err(check, msg):
    ERRORS.append("[%s] %s" % (check, msg))


def warn(check, msg):
    WARNS.append("[%s] %s" % (check, msg))


def read(rel):
    p = os.path.join(ROOT, rel)
    if not os.path.exists(p):
        return None
    with open(p, encoding="utf-8-sig") as f:
        return f.read()


def iter_cs():
    for dirpath, _, filenames in os.walk(SCRIPTS):
        for fn in filenames:
            if fn.endswith(".cs"):
                yield os.path.join(dirpath, fn)


# ---------------------------------------------------------------------------
# G1 职业图标单一入口（2026-09-26 收口）
#     同一个「职业图标」曾有三套资源 + 四个加载函数，主人两次反馈「总和玩家职业icon搞混」。
#     现在唯一入口是 Data/JobIconResolver.cs，其余一律 [Obsolete] 转调。
#     谁再直接调旧名、或新拼一条路径，旧图就会回来 —— 这里拦住。
# ---------------------------------------------------------------------------
def check_g1_job_icon():
    name = "G1 职业图标单一入口"
    resolver = os.path.join(SCRIPTS, "Data", "JobIconResolver.cs")
    if not os.path.exists(resolver):
        err(name, "找不到 Assets/Scripts/Data/JobIconResolver.cs —— 唯一入口没了，旧入口会复活")
        return

    obsolete_call = re.compile(r'(?:MercHireSession\s*\.\s*(?:LoadJobIcon|JobIconFile)\s*\()')
    for path in iter_cs():
        with open(path, encoding="utf-8-sig") as f:
            for i, line in enumerate(f, 1):
                if obsolete_call.search(line):
                    err(name, "已废弃的旧入口被调用：%s:%d  %s"
                        % (os.path.relpath(path, ROOT), i, line.strip()[:100]))

    # 新拼第四套路径：只允许 JobIconResolver.cs 里出现
    hardcode = re.compile(r'"Icons/(?:职业icon|Job)"')
    for path in iter_cs():
        if os.path.abspath(path) == os.path.abspath(resolver):
            continue
        with open(path, encoding="utf-8-sig") as f:
            for i, line in enumerate(f, 1):
                if hardcode.search(line):
                    warn(name, "出现硬编码图标目录（应统一走 JobIconResolver.CombatBadge）：%s:%d  %s"
                         % (os.path.relpath(path, ROOT), i, line.strip()[:100]))


# ---------------------------------------------------------------------------
# G2 Singleton 构造期保护（9-15 首次修复 → 被冲掉 → 9-26 重新应用，防第三次）
#     Application.isPlaying 在 MonoBehaviour 构造期访问会抛 UnityException。
#     正确样子：每一处读 Application.isPlaying 的前面都必须有 try 包住。
# ---------------------------------------------------------------------------
def check_g2_singleton():
    name = "G2 Singleton 构造期保护"
    txt = read(os.path.join("Assets", "Scripts", "Core", "Singleton.cs"))
    if txt is None:
        err(name, "找不到 Core/Singleton.cs")
        return

    hits = list(re.finditer(r"Application\.isPlaying", txt))
    if not hits:
        err(name, "Singleton.cs 里读不到 Application.isPlaying —— 9-15/9-26 的修复被改没了")
        return

    for m in hits:
        # 注释里提到 Application.isPlaying 不算真实调用，跳过
        line_start = txt.rfind("\n", 0, m.start()) + 1
        line_end = txt.find("\n", m.start())
        line_txt = txt[line_start:line_end if line_end != -1 else len(txt)].strip()
        if line_txt.startswith("//") or line_txt.startswith("*") or line_txt.startswith("/*"):
            continue

        head = txt[max(0, m.start() - 400):m.start()]
        line_no = txt.count("\n", 0, m.start()) + 1
        if "try" not in head:
            err(name, "第 %d 行读 Application.isPlaying 没有 try 包住 —— 构造期会抛 UnityException"
                "（这个修复在 9-15 做过、被冲掉、9-26 重新应用过一次，别让它第三次发生）" % line_no)


# ---------------------------------------------------------------------------
# G3 铁律：不许私自改 prefab / meta / asset / unity
#     只提示不拦（主人自己改 prefab 是正常操作），但要让改动在提交前被看见。
# ---------------------------------------------------------------------------
def check_g3_prefab():
    name = "G3 预制体改动提醒"
    try:
        r = subprocess.run(["git", "status", "--porcelain"], cwd=ROOT,
                           capture_output=True, text=True, encoding="utf-8", errors="replace")
    except Exception:
        return
    if r.returncode != 0:
        return
    for line in r.stdout.splitlines():
        if len(line) < 4:
            continue
        # 只看「已跟踪文件被改动」(M/A/D/R/C)，跳过 ?? 未跟踪 —— 新增脚本自带的 .meta 是正常的
        if line[0] not in "MADRC":
            continue
        path = line[3:].strip().strip('"')
        if path.endswith((".prefab", ".meta", ".asset", ".unity")):
            warn(name, "工作区改动了引擎文件：%s（确认是本人改的再提交）" % path)


# ---------------------------------------------------------------------------
# G4 跨资源回退清单（2026-09-26 建，信息级）
#     模式：`A ?? B`，其中 B 是另一套取图 / 取数据的调用 —— 取不到 A 就悄悄换成 B。
#     这正是「职业 icon 搞混」的同款机制（取不到四分类就换成职业立绘头像）。
#     全项目目前 35 处，过半是 GetComponent??GetComponentInChildren 的组件查找容错（正常），
#     其余多数也是有意的兼容设计（如 SkillRegistry:76 一个技能两个 id，注释里写明了）。
#     所以这里**不报错**，只每次列出来：你改动相关文件时，知道旁边还埋着一条回退路径。
# ---------------------------------------------------------------------------
def check_g4_fallback_inventory():
    name = "G4 跨资源回退清单"
    # 排除组件查找容错（GetComponent* 系列）
    pat = re.compile(r"\?\?(?![:=])\s*((?:Resources\.Load\w*\s*[<(\w]"
                     r"|[\w\.]+\.(?:Load|TryLoad|Get|TryGet|GetHead|GetIcon|GetSprite|GetPortrait|Resolve)\w*\s*[(<]))")
    hits = []
    for path in iter_cs():
        with open(path, encoding="utf-8-sig") as f:
            for i, line in enumerate(f, 1):
                s = line.strip()
                if s.startswith("//"):
                    continue
                if not pat.search(line):
                    continue
                if "GetComponent" in line:
                    continue
                hits.append("%s:%d  %s" % (os.path.relpath(path, ROOT), i, s[:96]))
    if hits:
        print("    （信息）跨资源回退 %d 处，改这些文件时留意旁边还埋着回退路径：" % len(hits))
        for h in hits:
            print("      " + h)


# ---------------------------------------------------------------------------
# G5 堵增量：本次改动里不许新增 public static Xxx Instance（2026-09-26 建）
#     现有 59 个单例**不动**（它们正在工作），但新的全局状态不许再造 —— 那是不受控的
#     跨场景残留来源（59 单例 + 51 文件 DontDestroyOnLoad + 204 静态字段已经够多了）。
#     新系统请走 Core/ServiceRegistry.cs 登记。
#     判定范围是「本次改动新增的行」+「新增的未跟踪 .cs」，所以不会误报存量。
# ---------------------------------------------------------------------------
def check_g5_new_singleton():
    name = "G5 新增单例堵增量"
    pat = re.compile(r"public\s+static\s+[\w<>\[\]?\.]+\s+Instance\b")
    try:
        r = subprocess.run(["git", "diff", "HEAD", "-U0", "--", "*.cs"], cwd=ROOT,
                           capture_output=True, text=True, encoding="utf-8", errors="replace")
        added = [l[1:] for l in r.stdout.splitlines()
                 if l.startswith("+") and not l.startswith("+++")]
    except Exception:
        added = []

    new_files = []
    try:
        r2 = subprocess.run(["git", "ls-files", "--others", "--exclude-standard"], cwd=ROOT,
                            capture_output=True, text=True, encoding="utf-8", errors="replace")
        new_files = [f.strip() for f in r2.stdout.splitlines() if f.strip().endswith(".cs")]
    except Exception:
        pass

    for fn in new_files:
        p = os.path.join(ROOT, fn)
        if os.path.exists(p):
            with open(p, encoding="utf-8-sig") as f:
                added.extend(f.read().splitlines())

    for l in added:
        s = l.strip()
        # 注释里提到 "public static Xxx Instance" 不算真实声明（G2 也踩过同样的坑）
        if s.startswith("//") or s.startswith("*") or s.startswith("/*"):
            continue
        if pat.search(l):
            warn(name, "本次改动新增了单例：%s —— 新系统请改用 ServiceRegistry.Register/Get"
                 % s[:100])


CHECKS = [check_g1_job_icon, check_g2_singleton, check_g3_prefab,
          check_g4_fallback_inventory, check_g5_new_singleton]


def main():
    strict = "--strict" in sys.argv
    print("=== Guardrails 防复发闸门 ===")
    for fn in CHECKS:
        try:
            fn()
        except Exception as e:  # 单项脚本出错不该拖垮其它检查
            err(fn.__name__, "检查自身异常：%r" % (e,))

    if ERRORS:
        print("\n--- ERROR（必须修掉才能提交）---")
        for x in ERRORS:
            print("  " + x)
    if WARNS:
        print("\n--- WARN（确认过再提交）---")
        for x in WARNS:
            print("  " + x)
    if not ERRORS and not WARNS:
        print("\n全部通过：%d 项检查" % len(CHECKS))
    else:
        print("\n合计：ERROR %d / WARN %d" % (len(ERRORS), len(WARNS)))

    if ERRORS or (strict and WARNS):
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
