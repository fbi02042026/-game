# -*- coding: utf-8 -*-
import os
import shutil
import pathlib
from datetime import datetime

root = pathlib.Path(r"Y:/PixelAdventureTown")
docs = root / "Docs"
target = docs / "软著源码鉴别"
ascii_target = docs / "SoftCopyrightSource"

# 1) report dirs
report = []
for p in sorted(docs.iterdir(), key=lambda x: x.name):
    if p.is_dir():
        cps = " ".join(f"U+{ord(c):04X}" for c in p.name)
        files = list(p.iterdir())[:8]
        report.append(f"DIR {p.name!r}\n  codepoints: {cps}\n  files: {[f.name for f in files]}")

# 2) find any folder that already has 源程序 txt
found_src = None
for p in docs.iterdir():
    if not p.is_dir():
        continue
    names = {f.name for f in p.iterdir() if f.is_file()}
    if any("前30" in n or "前30页" in n or n.endswith("前30页.txt") for n in names) or \
       any("源程序" in n for n in names):
        found_src = p
        report.append(f"FOUND_EXPORT_IN: {p.name!r} -> {list(names)}")
        break
    # also match by size pattern of known export files
    if "导出说明.txt" in names or any(n.endswith(".txt") and f.stat().st_size > 10000 for n in names for f in [p / n] if (p / n).exists()):
        # check content
        for f in p.iterdir():
            if f.suffix.lower() == ".txt" and f.stat().st_size > 1000:
                head = f.read_text(encoding="utf-8", errors="ignore")[:80]
                if "源程序鉴别" in head or "FILE:" in head:
                    found_src = p
                    report.append(f"FOUND_EXPORT_IN(content): {p.name!r}")
                    break

# 3) ensure correct UTF-8 folder exists and copy files there + ascii alias
target.mkdir(parents=True, exist_ok=True)
ascii_target.mkdir(parents=True, exist_ok=True)

copied = []
if found_src is not None:
    for f in found_src.iterdir():
        if f.is_file() and f.suffix.lower() == ".txt":
            dst1 = target / f.name
            dst2 = ascii_target / f.name
            # normalize names if mojibake
            name = f.name
            if "前" in name or "30" in name and "前" in name:
                pass
            shutil.copy2(f, dst1)
            # ascii-friendly copies with english names too
            shutil.copy2(f, dst2)
            copied.append(f.name)

# 4) if still empty, run inline export (same as ps1)
cs_files_prefer = [
    "Assets/Scripts/Core/SaveData.cs",
    "Assets/Scripts/Systems/MercenaryOfferGenerator.cs",
    "Assets/Scripts/UI/TavernRosterPanel.cs",
    "Assets/Scripts/UI/TownHeroCostumePreview.cs",
    "Assets/Scripts/Unit/HeroCostumeManager.cs",
    "Assets/Scripts/Systems/MercenaryManager.cs",
    "Assets/Scripts/Config/SkillRegistry.cs",
    "Assets/Scripts/Systems/StageRoller.cs",
    "Assets/Scripts/UI/RestStagePopupUI.cs",
    "Assets/Scripts/Core/GameConfig.cs",
    "Assets/Scripts/Core/GameAudio.cs",
    "Assets/Scripts/Core/GameBgm.cs",
    "Assets/Scripts/Story/TutorialDirector.cs",
    "Assets/Scripts/UI/BattleHeadTalkUI.cs",
    "Assets/Scripts/Systems/SaveSystem.cs",
    "Assets/Scripts/Systems/ResourceWallet.cs",
    "Assets/Scripts/Systems/StaminaSystem.cs",
    "Assets/Scripts/Systems/StageClearRewardDirector.cs",
    "Assets/Scripts/Systems/PreLevelSystem.cs",
    "Assets/Scripts/Systems/OfflineGoldCalc.cs",
    "Assets/Scripts/Systems/TownSaveAlign.cs",
    "Assets/Scripts/Managers/BattleManager.cs",
    "Assets/Scripts/Managers/ChapterManager.cs",
    "Assets/Scripts/UI/TownHubController.cs",
    "Assets/Scripts/UI/AdventureUI.cs",
    "Assets/Scripts/UI/GuildHallUI.cs",
    "Assets/Scripts/UI/AdventureLogUI.cs",
    "Assets/Scripts/UI/CharacterUI.cs",
    "Assets/Scripts/UI/BattleStageMapUI.cs",
    "Assets/Scripts/UI/OfflineRewardPopup.cs",
    "Assets/Scripts/Combat/DamageFormula.cs",
    "Assets/Scripts/Platform/CloudSaveBridge.cs",
    "Assets/Scripts/Platform/RewardedAdBridge.cs",
]

lines = []
used = set()
for rel in cs_files_prefer:
    full = root / rel.replace("/", os.sep)
    if not full.exists():
        continue
    lines.append(f"// ===== FILE: {rel} =====")
    try:
        lines.extend(full.read_text(encoding="utf-8", errors="replace").splitlines())
    except Exception as e:
        lines.append(f"// READ ERROR: {e}")
    lines.append("")
    used.add(rel)

need = 30 * 2 * 50
scripts = root / "Assets" / "Scripts"
if scripts.exists():
    for full in sorted(scripts.rglob("*.cs")):
        if len(lines) >= need:
            break
        rel = str(full.relative_to(root)).replace("\\", "/")
        if rel in used:
            continue
        lines.append(f"// ===== FILE: {rel} =====")
        lines.extend(full.read_text(encoding="utf-8", errors="replace").splitlines())
        lines.append("")
        used.add(rel)


def write_paged(path: pathlib.Path, start: int, count: int, tag: str):
    out = []
    out.append(f"======== 源程序鉴别材料 · {tag}30页 · 每页50行 ========")
    out.append("")
    end = min(len(lines), start + count)
    page = 1
    line_in_page = 0
    for i in range(start, end):
        if line_in_page == 0:
            out.append(f"---------- 第 {page} 页 ----------")
        out.append(lines[i])
        line_in_page += 1
        if line_in_page >= 50:
            line_in_page = 0
            page += 1
            out.append("")
    path.write_text("\n".join(out) + "\n", encoding="utf-8")


need_lines = 30 * 50
for dest in (target, ascii_target):
    dest.mkdir(parents=True, exist_ok=True)
    write_paged(dest / "源程序_前30页.txt", 0, need_lines, "前")
    back = max(0, len(lines) - need_lines)
    write_paged(dest / "源程序_后30页.txt", back, need_lines, "后")
    note = (
        f"导出时间: {datetime.now():%Y-%m-%d %H:%M:%S}\n"
        f"登记版本: V0.3.5（最新）\n"
        f"每页行数: 50\n"
        f"前后页数: 30\n"
        f"总拼接行数: {len(lines)}\n"
        f"文件数: {len(used)}\n"
        f"输出目录: Docs/软著源码鉴别/ 与 Docs/SoftCopyrightSource/\n"
        f"用法: 将 txt 按页排版为 PDF（≥50 行/页）提交鉴别材料。\n"
    )
    (dest / "导出说明.txt").write_text(note, encoding="utf-8")
    # english aliases for Explorer search
    shutil.copy2(dest / "源程序_前30页.txt", dest / "source_front_30pages.txt")
    shutil.copy2(dest / "源程序_后30页.txt", dest / "source_back_30pages.txt")
    shutil.copy2(dest / "导出说明.txt", dest / "README_export.txt")

report.append(f"WROTE target={target} files={list(f.name for f in target.iterdir())}")
report.append(f"WROTE ascii={ascii_target} files={list(f.name for f in ascii_target.iterdir())}")
report.append(f"lines={len(lines)} files={len(used)}")

(docs / "_softcopyright_export_report.txt").write_text("\n".join(report), encoding="utf-8")
print("OK", len(lines), len(used))
print("target", target)
print("ascii", ascii_target)
