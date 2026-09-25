# -*- coding: utf-8 -*-
"""菜单清理：把待删的 Editor 脚本先备份到 Tools/_menu_archive_2026-09-25/，再从 Assets 下删除。"""
import io, os, shutil

ROOT = r"E:\xiangsumaoxian"
ARCH = os.path.join(ROOT, "Tools", "_menu_archive_2026-09-25")

DELETE = [
    r"Assets\Editor\AdventureLogUIPrefabGenerator.cs",
    r"Assets\Editor\AdventureUIPrefabGenerator.cs",
    r"Assets\Editor\BattleRuntimePrefabTool.cs",
    r"Assets\Editor\BattleSettlementPrefabBuilder.cs",
    r"Assets\Editor\BoxPrefabRebuilder.cs",
    r"Assets\Editor\CharacterRegistryBuilder.cs",
    r"Assets\Editor\CharacterUIPrefabGenerator.cs",
    r"Assets\Editor\CodexInfoPopupPrefabBuilder.cs",
    r"Assets\Editor\DailyLoginFullBuilder.cs",
    r"Assets\Editor\DialogueUIPrefabGenerator.cs",
    r"Assets\Editor\EquipDropPopupPrefabBuilder.cs",
    r"Assets\Editor\EvacuateConfirmPopupPrefabBuilder.cs",
    r"Assets\Editor\GameSceneBuilder.cs",
    r"Assets\Editor\GuildHallPrefabGenerator.cs",
    r"Assets\Editor\HealthNoticePrefabGenerator.cs",
    r"Assets\Editor\LoadingUIPrefabGenerator.cs",
    r"Assets\Editor\MainBottomNavTools.cs",
    r"Assets\Editor\MercenaryRecruitPopupPrefabBuilder.cs",
    r"Assets\Editor\NextStageRoulettePrefabBuilder.cs",
    r"Assets\Editor\PlayerJobSelectPrefabGenerator.cs",
    r"Assets\Editor\PlayerNamingUIPrefabGenerator.cs",
    r"Assets\Editor\RestStagePopupPrefabBuilder.cs",
    r"Assets\Editor\RewardPopupBuilder.cs",
    r"Assets\Editor\SettingsPopupPrefabBuilder.cs",
    r"Assets\Editor\TalentUIPrefabGenerator.cs",
    r"Assets\Editor\TavernPrefabGenerator.cs",
    r"Assets\Editor\ToolsMenuGuide.cs",
    r"Assets\Editor\TutorialHintPrefabGenerator.cs",
    r"Assets\Editor\WorldMapPopupPrefabGenerator.cs",
    r"Assets\Scripts\Editor\BattleBackgroundRegistrySetup.cs",
    r"Assets\Scripts\Editor\MonsterConfigGenerator.cs",
    r"Assets\Scripts\Editor\MonsterHealthBarGenerator.cs",
    r"Assets\Scripts\Editor\MonsterPrefabGenerator.cs",
    r"Assets\Scripts\Editor\MonsterSpriteSetup.cs",
]

# 顺手清掉之前子代理留在 Assets/Editor 下的临时垃圾（未跟踪）
JUNK = [
    r"Assets\Editor\_crlf_check_out.txt",
    r"Assets\Editor\_crlf_check_out.txt.meta",
    r"Assets\Editor\_crlf_check_tmp.py",
    r"Assets\Editor\_crlf_check_tmp.py.meta",
]


def main():
    log = []
    os.makedirs(ARCH, exist_ok=True)

    for rel in DELETE:
        src = os.path.join(ROOT, rel)
        if not os.path.exists(src):
            log.append("MISSING  " + rel)
            continue
        dst = os.path.join(ARCH, rel)
        os.makedirs(os.path.dirname(dst), exist_ok=True)
        shutil.copy2(src, dst)
        log.append("BACKUP   " + rel)
        os.unlink(src)
        meta = src + ".meta"
        if os.path.exists(meta):
            shutil.copy2(meta, dst + ".meta")
            os.unlink(meta)

    for rel in JUNK:
        p = os.path.join(ROOT, rel)
        if os.path.exists(p):
            os.unlink(p)
            log.append("JUNK     " + rel)

    out = "\r\n".join(log)
    io.open(os.path.join(ARCH, "_backup_log.txt"), "w", encoding="utf-8", newline="").write(out)
    print(out)
    print("\ndeleted/backed up: %d" % len(DELETE))


if __name__ == "__main__":
    main()
