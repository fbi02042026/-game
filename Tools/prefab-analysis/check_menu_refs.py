# -*- coding: utf-8 -*-
"""删除前安全检查：对待删的 Editor 脚本，提取类名，grep 全工程其它 .cs 是否引用。
用法: python check_menu_refs.py
"""
import io, os, re

ROOT = r"E:\xiangsumaoxian"

TARGETS = [
    r"Assets\Editor\AdventureLogUIPrefabGenerator.cs",
    r"Assets\Editor\AdventureUIPrefabGenerator.cs",
    r"Assets\Editor\AppIconSetup.cs",
    r"Assets\Editor\BattlePrefabGenerator.cs",
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


def all_cs():
    for dp, dn, fn in os.walk(os.path.join(ROOT, "Assets")):
        for f in fn:
            if f.endswith(".cs"):
                yield os.path.join(dp, f)


def main():
    targets = set(os.path.normpath(os.path.join(ROOT, t)) for t in TARGETS)
    missing = [t for t in targets if not os.path.exists(t)]
    print("待删文件数: %d，其中不存在: %d" % (len(targets), len(missing)))
    for m in missing:
        print("   MISSING " + os.path.relpath(m, ROOT))
    print("")

    # 收集待删文件里的类名
    class_of = {}
    for t in sorted(targets):
        if not os.path.exists(t):
            continue
        s = io.open(t, "r", encoding="utf-8", errors="replace").read()
        for c in re.findall(r'\b(?:public|internal)\s+(?:static\s+)?(?:partial\s+)?(?:class|struct)\s+(\w+)', s):
            class_of.setdefault(c, []).append(os.path.relpath(t, ROOT))
        for c in re.findall(r'\bclass\s+(\w+)', s):
            class_of.setdefault(c, []).append(os.path.relpath(t, ROOT))

    print("== 类名 -> 定义处 ==")
    for c in sorted(class_of):
        print("  %-45s %s" % (c, ", ".join(sorted(set(class_of[c])))))
    print("")

    # 全工程 grep（排除待删文件自身，也排除同名 .cs 本身）
    hits = []
    for p in all_cs():
        if os.path.normpath(p) in targets:
            continue
        rel = os.path.relpath(p, ROOT)
        s = io.open(p, "r", encoding="utf-8", errors="replace").read()
        for i, line in enumerate(s.split("\n"), 1):
            for c in class_of:
                if re.search(r'\b' + re.escape(c) + r'\b', line):
                    hits.append((c, rel, i, line.strip()[:100]))
                    break

    if not hits:
        print("== 没有任何外部引用，可以安全删除 ==")
    else:
        print("== !! 发现外部引用，需人工确认 ==（共 %d 处）" % len(hits))
        seen = set()
        for c, rel, i, line in hits:
            k = (c, rel)
            if k in seen:
                continue
            seen.add(k)
            print("  [%s] %s:%d  %s" % (c, rel, i, line))


if __name__ == "__main__":
    main()
