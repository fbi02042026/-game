#!/usr/bin/env python3
"""软著源程序鉴别材料导出（与 SoftCopyrightSourceExport.cs 同逻辑）。"""
from __future__ import annotations

from datetime import datetime
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
LINES_PER_PAGE = 50
PAGES_EACH = 30

PREFERRED_ORDER = [
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


def append_file(all_lines: list[str], rel: str, full: Path) -> None:
    all_lines.append(f"// ===== FILE: {rel} =====")
    try:
        all_lines.extend(full.read_text(encoding="utf-8").splitlines())
    except OSError as e:
        all_lines.append(f"// READ ERROR: {e}")
    all_lines.append("")


def write_paged(path: Path, lines: list[str], start: int, count: int, tag: str) -> None:
    out: list[str] = [
        f"======== 源程序鉴别材料 · {tag}30页 · 每页{LINES_PER_PAGE}行 ========",
        "",
    ]
    end = min(len(lines), start + count)
    page = 1
    line_in_page = 0
    for i in range(start, end):
        if line_in_page == 0:
            out.append(f"---------- 第 {page} 页 ----------")
        out.append(lines[i])
        line_in_page += 1
        if line_in_page >= LINES_PER_PAGE:
            line_in_page = 0
            page += 1
            out.append("")
    if line_in_page > 0:
        out.append("")
    path.write_text("\n".join(out), encoding="utf-8")


def main() -> None:
    all_lines: list[str] = []
    used: set[str] = set()

    for rel in PREFERRED_ORDER:
        full = ROOT / rel.replace("/", "\\")
        if not full.is_file():
            continue
        append_file(all_lines, rel, full)
        used.add(rel)

    need = PAGES_EACH * 2 * LINES_PER_PAGE
    scripts_dir = ROOT / "Assets" / "Scripts"
    if scripts_dir.is_dir():
        for full in sorted(scripts_dir.rglob("*.cs")):
            if len(all_lines) >= need:
                break
            rel = full.relative_to(ROOT).as_posix()
            if rel in used:
                continue
            append_file(all_lines, rel, full)
            used.add(rel)

    out_dir = ROOT / "Docs" / "软著源码鉴别"
    out_dir.mkdir(parents=True, exist_ok=True)

    need_lines = PAGES_EACH * LINES_PER_PAGE
    write_paged(out_dir / "源程序_前30页.txt", all_lines, 0, need_lines, "前")
    back_start = max(0, len(all_lines) - need_lines)
    write_paged(out_dir / "源程序_后30页.txt", all_lines, back_start, need_lines, "后")

    note = (
        f"导出时间: {datetime.now():%Y-%m-%d %H:%M:%S}\n"
        "登记版本: V0.3.5（最新）\n"
        f"每页行数: {LINES_PER_PAGE}\n"
        f"前后页数: {PAGES_EACH}\n"
        f"总拼接行数: {len(all_lines)}\n"
        f"文件数: {len(used)}\n"
        "优先收录: SaveData / MercenaryOfferGenerator / GameAudio / GameBgm / "
        "TutorialDirector / BattleHeadTalkUI 等\n"
        "用法: 将 txt 按页排版为 PDF（≥50 行/页）提交鉴别材料。\n"
        "也可在团结编辑器执行 Tools/软著/导出源程序鉴别材料 覆盖本目录。\n"
        "提交记录: git commit 含「软著 V0.3.5」时与本版代码一致。\n"
    )
    (out_dir / "导出说明.txt").write_text(note, encoding="utf-8")
    print(f"Exported to {out_dir} ({len(all_lines)} lines, {len(used)} files)")


if __name__ == "__main__":
    main()
