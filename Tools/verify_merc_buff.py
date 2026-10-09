# -*- coding: utf-8 -*-
from pathlib import Path
import re
from decimal import Decimal, ROUND_HALF_UP

ROOT = Path(r"e:\xiangsumaoxian")
csv = (ROOT / "Assets/Data/Source/Tables/merc_roster.csv").read_bytes()
by = (ROOT / "Assets/Resources/Data/Tables/merc_roster.bytes").read_bytes()
assert csv.startswith(b"\xef\xbb\xbf"), "csv 缺 BOM"
assert not by.startswith(b"\xef\xbb\xbf"), "bytes 不该有 BOM"
assert by == csv[3:], "bytes != csv 去 BOM"
assert b"\r\n" not in by and b"\r\n" not in csv, "发现 CRLF"

cs = (ROOT / "Assets/Scripts/Config/MercRosterDefs.cs").read_text(encoding="utf-8-sig")
rows = [l for l in csv.decode("utf-8-sig").splitlines() if l.startswith("H0")]
dlines = [l for l in cs.splitlines() if re.match(r'\s*D\("H0', l)]
assert len(rows) == len(dlines) == 22, (len(rows), len(dlines))

orig = {
    "H001": ("9", "15"), "H002": ("13", "20"), "H003": ("12", "23"), "H004": ("18", "28"),
    "H005": ("16", "6"), "H006": ("23", "8"), "H007": ("17.5", "9"), "H008": ("25", "12"),
    "H009": ("23", "14"), "H010": ("35", "17"), "H011": ("7.5", "5"), "H012": ("24", "11"),
    "H013": ("25", "13"), "H014": ("34", "16"), "H015": ("15", "5"), "H016": ("24", "12"),
    "H017": ("11", "7"), "H018": ("8.5", "14"), "H019": ("17", "4"), "H020": ("25", "6"),
    "H021": ("10", "13"), "H022": ("15", "18"),
}

Q = Decimal
for r, d in zip(rows, dlines):
    rc = r.split(",")
    dc = [p.strip() for p in d.split(",")]
    hid = dc[0].replace("D(", "").replace('"', "")
    assert rc[0] == hid, (rc[0], hid)
    for idx in (7, 8, 9):
        assert rc[idx] == dc[idx].rstrip("f"), (idx, hid, rc[idx], dc[idx])
    rar = int(rc[5])
    if rar == 0:
        assert (rc[8], rc[9]) == orig[hid], ("普通被动了", hid)
    else:
        mul = Q("1.35") if rar == 1 else Q("1.8")
        for idx, oi in ((8, 0), (9, 1)):
            exp = (Q(orig[hid][oi]) * mul).quantize(Q("0.1"), rounding=ROUND_HALF_UP)
            assert Q(rc[idx]) == exp >= Q(orig[hid][oi]), (hid, rc[idx], exp)

print("三处同值 OK；倍率复核 OK（普通未动、稀有x1.35、传说x1.8、无下调）")
print("bytes = csv 去 BOM 逐字一致；LF OK")
