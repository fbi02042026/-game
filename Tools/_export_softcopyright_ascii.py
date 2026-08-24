# -*- coding: utf-8 -*-
"""Export soft-copyright source materials to Docs/SoftCopyrightSource (ASCII path)."""
import os
from datetime import datetime
from pathlib import Path

root = Path(r"Y:/PixelAdventureTown")
out = root / "Docs" / "SoftCopyrightSource"
out.mkdir(parents=True, exist_ok=True)

prefer = [
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
for rel in prefer:
    full = root / rel.replace("/", os.sep)
    if not full.exists():
        continue
    lines.append(f"// ===== FILE: {rel} =====")
    lines.extend(full.read_text(encoding="utf-8", errors="replace").splitlines())
    lines.append("")
    used.add(rel)

need = 30 * 2 * 50
scripts = root / "Assets" / "Scripts"
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


def write_paged(path: Path, start: int, count: int, tag: str):
    out_lines = [
        f"======== 源程序鉴别材料 · {tag}30页 · 每页50行 ========",
        "",
    ]
    end = min(len(lines), start + count)
    page = 1
    lip = 0
    for i in range(start, end):
        if lip == 0:
            out_lines.append(f"---------- 第 {page} 页 ----------")
        out_lines.append(lines[i])
        lip += 1
        if lip >= 50:
            lip = 0
            page += 1
            out_lines.append("")
    path.write_text("\n".join(out_lines) + "\n", encoding="utf-8")


need_lines = 30 * 50
write_paged(out / "source_front_30pages.txt", 0, need_lines, "前")
write_paged(out / "源程序_前30页.txt", 0, need_lines, "前")
back = max(0, len(lines) - need_lines)
write_paged(out / "source_back_30pages.txt", back, need_lines, "后")
write_paged(out / "源程序_后30页.txt", back, need_lines, "后")

note = (
    f"导出时间: {datetime.now():%Y-%m-%d %H:%M:%S}\n"
    f"登记版本: V0.3.5\n"
    f"每页行数: 50\n"
    f"前后页数: 30\n"
    f"总拼接行数: {len(lines)}\n"
    f"文件数: {len(used)}\n"
    f"本目录为英文路径 Docs/SoftCopyrightSource/（避免中文目录编码问题）\n"
    f"中文同名也可看同目录下 源程序_前30页.txt / 源程序_后30页.txt\n"
)
(out / "README_export.txt").write_text(note, encoding="utf-8")
(out / "导出说明.txt").write_text(note, encoding="utf-8")

print("OK", out)
print("files", [p.name for p in out.iterdir()])
print("lines", len(lines), "cs", len(used))
