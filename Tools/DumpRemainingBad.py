# -*- coding: utf-8 -*-
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from AuditPrefabSprites import parse_prefab

ROOT = Path(r"Y:\PixelAdventureTown\Assets")
guid_to_path = {}
for p in ROOT.joinpath("Art").rglob("*.png"):
    meta = Path(str(p) + ".meta")
    if not meta.exists():
        continue
    m = re.search(r"^guid:\s*([0-9a-fA-F]{32})", meta.read_text(encoding="utf-8-sig"), re.M)
    if m:
        guid_to_path[m.group(1)] = str(p)

out = []
for name in [
    "CharacterUI.prefab",
    "TalentUI.prefab",
    "TavernUI.prefab",
    "AdventureLogUI.prefab",
    "BattleStageMap.prefab",
    "NextStageRoulette.prefab",
    "RestStagePopup.prefab",
    "LoadingUI.prefab",
]:
    prefs = list(ROOT.joinpath("Resources/Prefabs").rglob(name))
    if not prefs:
        continue
    rows, _ = parse_prefab(str(prefs[0]))
    out.append(f"==== {name}")
    seen = set()
    for r in rows:
        if r["guid"] and r["guid"] not in guid_to_path:
            key = (r["path"], r["guid"])
            if key in seen:
                continue
            seen.add(key)
            out.append(f"  {r['path']}\t{r['guid']}")

Path(r"Y:\PixelAdventureTown\Tools\_remaining_audit.txt").write_text("\n".join(out), encoding="utf-8")
print("wrote", len(out), "lines")
