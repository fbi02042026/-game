# -*- coding: utf-8 -*-
"""佣兵攻防加成：Rarity1 x1.35 / Rarity2 x1.8，同步 csv + MercRosterDefs.cs 数组。"""
import re
from decimal import Decimal, ROUND_HALF_UP
from pathlib import Path

ROOT = Path(r"e:\xiangsumaoxian")
CSV = ROOT / "Assets/Data/Source/Tables/merc_roster.csv"
CS = ROOT / "Assets/Scripts/Config/MercRosterDefs.cs"

MUL = {1: Decimal("1.35"), 2: Decimal("1.8")}

def fmt(d: Decimal):
    """整数值输出整数，否则一位小数。"""
    if d == d.to_integral_value():
        return str(int(d))
    return str(d.quantize(Decimal("0.1"), rounding=ROUND_HALF_UP))

def calc(old: str, mul: Decimal):
    new = (Decimal(old) * mul).quantize(Decimal("0.1"), rounding=ROUND_HALF_UP)
    if new < Decimal(old):
        new = Decimal(old)
    return fmt(new)

# ---- 1) CSV ----
raw = CSV.read_bytes()
assert raw.startswith(b"\xef\xbb\xbf"), "csv 必须有 BOM"
text = raw.decode("utf-8-sig")
lines = text.split("\n")  # 保留 LF
header_idx = next(i for i, l in enumerate(lines) if l.startswith("HireId"))
header = lines[header_idx].split(",")
i_rar = header.index("Rarity")
i_atk = header.index("BaseAtk")
i_def = header.index("BaseDef")

changes = {}
for i in range(header_idx + 1, len(lines)):
    if not lines[i].strip() or lines[i].startswith("#"):
        continue
    c = lines[i].split(",")
    if len(c) < 10:
        continue
    rar = int(c[i_rar])
    if rar not in MUL:
        continue
    old_atk, old_def = c[i_atk], c[i_def]
    c[i_atk] = calc(old_atk, MUL[rar])
    c[i_def] = calc(old_def, MUL[rar])
    changes[c[0]] = (old_atk, c[i_atk], old_def, c[i_def], rar, c[1])
    lines[i] = ",".join(c)

# 加一行本次调参备注（沿用表头注释风格）
note = "# 【2026-10-09 主人拍板】佣兵攻防加成：在 10-07 版基础上，稀有 BaseAtk/BaseDef ×1.35、传说 ×1.8（普通不动），三处同值。\n"
lines.insert(4, note)
out = "".join(l if l.endswith("\n") else l + "\n" for l in lines)
CSV.write_bytes(b"\xef\xbb\xbf" + out.encode("utf-8").replace(b"\r\n", b"\n"))

# ---- 2) C# 数组 ----
cs = CS.read_bytes()
has_bom = cs.startswith(b"\xef\xbb\xbf")
cs_text = cs.decode("utf-8-sig")
cs_lines = cs_text.split("\n")

def patch(line):
    m = re.match(r'^(\s*)D\("([^"]+)"', line)
    if not m:
        return line
    hid = m.group(2)
    if hid not in changes:
        return line
    old_atk, new_atk, old_def, new_def, rar, _ = changes[hid]
    parts = line.split(",")
    # D(...) 字段：0 hireId 1 name 2 nick 3 asset 4 job 5 rarity 6 growth 7 hp 8 atk 9 def ...
    def csfmt(v):
        return v if "." in v else v
    assert parts[8].strip().rstrip("f") == old_atk, f"{hid} atk 不匹配: {parts[8]}"
    assert parts[9].strip().rstrip("f") == old_def, f"{hid} def 不匹配: {parts[9]}"
    parts[8] = csfmt(new_atk) + ("f" if "." in new_atk else "")
    parts[9] = csfmt(new_def) + ("f" if "." in new_def else "")
    return ",".join(parts)

cs_lines = [patch(l) for l in cs_lines]
cs_out = "".join(l if l.endswith("\n") else l + "\n" for l in cs_lines)
CS.write_bytes((b"\xef\xbb\xbf" if has_bom else b"") + cs_out.encode("utf-8").replace(b"\r\n", b"\n"))

rar_cn = {1: "稀有", 2: "传说"}
for hid, (oa, na, od, nd, rar, name) in changes.items():
    print(f"{hid} {name} {rar_cn[rar]}  ATK {oa} -> {na}   DEF {od} -> {nd}")
