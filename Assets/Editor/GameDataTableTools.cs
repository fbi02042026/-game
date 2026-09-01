using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>关卡怪物五表 Editor 工具：导出、补全、种子生成（UTF-8 无 BOM）。</summary>
public static class GameDataTableTools
{
    const string TablesDir = ContentPaths.Source.Tables;
    const string MonsterStatsCsv = TablesDir + "/monster_stats.csv";

    static readonly string[] ThemePrefixes =
    {
        "", "undead", "jungle", "sea", "forest", "field", "cave", "devil", "ice"
    };

    [MenuItem("Tools/Data/从 MonsterConfig 导出 monster_stats.csv")]
    public static void ExportMonsterStatsFromAssets()
    {
        Directory.CreateDirectory(TablesDir);
        var monsters = Resources.LoadAll<MonsterConfig>(ContentPaths.Config.Monsters);
        var sb = new StringBuilder();
        sb.AppendLine("# monster_stats：从 MonsterConfig SO 导出");
        sb.AppendLine("id,monsterChapter,spriteIndex,name,minWave,isBoss,unlockClearCount,baseHp,baseAtk,baseAtkSpeed,attackRange,moveSpeed,baseGold,exp,spriteScale,note");

        int count = 0;
        for (int i = 0; i < monsters.Length; i++)
        {
            var m = monsters[i];
            if (m == null || string.IsNullOrEmpty(m.id)) continue;
            int ch = ExtractChapterFromId(m.id);
            if (ch <= 0) ch = 1;
            int sprite = m.spriteIndex > 0 ? m.spriteIndex : ExtractSpriteFromId(m.id);
            sb.AppendLine(string.Join(",",
                Csv(m.id), ch, sprite, Csv(m.monsterName ?? m.id),
                m.minWave, m.isBoss ? 1 : 0, m.unlockClearCount,
                m.baseHp, m.baseAttack, m.baseAttackSpeed, m.attackRange, m.baseMoveSpeed,
                m.baseGoldDrop, m.expDrop, m.spriteScale > 0.01f ? m.spriteScale : 1f, ""));
            count++;
        }

        File.WriteAllText(MonsterStatsCsv, sb.ToString(), new UTF8Encoding(false));
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Data", $"已导出 {count} 条到 monster_stats.csv", "OK");
    }

    [MenuItem("Tools/Data/补全 monster_stats 至 96 行")]
    public static void CompleteMonsterStatsTo96()
    {
        Directory.CreateDirectory(TablesDir);
        var existing = LoadMonsterStatsRows();
        var attackNotes = LoadAttackStyleNotes();

        var sb = new StringBuilder();
        sb.AppendLine("# monster_stats：怪物数值与出场（monsterChapter=素材章 1~8）");
        sb.AppendLine("id,monsterChapter,spriteIndex,name,minWave,isBoss,unlockClearCount,baseHp,baseAtk,baseAtkSpeed,attackRange,moveSpeed,baseGold,exp,spriteScale,note");

        int added = 0;
        for (int ch = 1; ch <= 8; ch++)
        {
            for (int idx = 1; idx <= 12; idx++)
            {
                string id = BuildDefaultId(ch, idx);
                bool isBoss = idx >= 11;
                if (existing.TryGetValue(Key(ch, idx), out var row))
                {
                    sb.AppendLine(FormatMonsterRow(row));
                    continue;
                }

                attackNotes.TryGetValue(Key(ch, idx), out string note);
                string name = !string.IsNullOrEmpty(note) ? note.Replace("-", " ") : id;
                var stats = DefaultStats(ch, idx, isBoss);
                sb.AppendLine(string.Join(",",
                    Csv(id), ch, idx, Csv(name),
                    DefaultMinWave(idx, isBoss), isBoss ? 1 : 0, DefaultUnlock(idx, isBoss),
                    stats.hp, stats.atk, stats.asp, stats.ar, stats.ms, stats.gold, stats.exp, 1f, Csv(note ?? "")));
                added++;
            }
        }

        File.WriteAllText(MonsterStatsCsv, sb.ToString(), new UTF8Encoding(false));
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Data", $"monster_stats 已补全至 96 行（新增骨架 {added} 行）", "OK");
    }

    [MenuItem("Tools/Data/生成 chapter_theme_map / unlock_tier / stage_spawn / tutorial_battle 种子")]
    public static void GenerateSeedTables()
    {
        Directory.CreateDirectory(TablesDir);
        WriteChapterThemeMap();
        WriteUnlockTier();
        WriteStageSpawn();
        WriteTutorialBattle();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Data", "已写入 4 张种子 CSV（chapter_theme_map、unlock_tier、stage_spawn、tutorial_battle）", "OK");
    }

    static void WriteChapterThemeMap()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# gameChapter=游戏章；monsterChapter=素材章");
        sb.AppendLine("gameChapter,monsterChapter,folderName,mapName,bgFolder");
        for (int i = 0; i < GameConfig.ChapterMonsterFolders.Length; i++)
        {
            int gameChapter = i + 1;
            string folder = GameConfig.ChapterMonsterFolders[i];
            int monsterChapter = MonsterChapterFromFolder(folder);
            string mapName = GameConfig.ChapterMapNames[i];
            sb.AppendLine($"{gameChapter},{monsterChapter},{Csv(folder)},{Csv(mapName)},{Csv(folder)}");
        }
        File.WriteAllText(TablesDir + "/chapter_theme_map.csv", sb.ToString(), new UTF8Encoding(false));
    }

    static void WriteUnlockTier()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# clearCountMin 达到后可用精灵上限");
        sb.AppendLine("clearCountMin,spriteIndexMax,stageIndexBonus,note");
        sb.AppendLine($"0,{GameConfig.TIER0_MAX_SPRITE},2,首次通关");
        sb.AppendLine($"{GameConfig.TIER1_UNLOCK_CLEARS},{GameConfig.TIER1_MAX_SPRITE},2,");
        sb.AppendLine($"{GameConfig.TIER2_UNLOCK_CLEARS},{GameConfig.TIER2_MAX_SPRITE},2,");
        File.WriteAllText(TablesDir + "/monster_unlock_tier.csv", sb.ToString(), new UTF8Encoding(false));
    }

    static void WriteStageSpawn()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# monsterTotal=0 表示走 GameConfig 公式；* 为通配");
        sb.AppendLine("gameChapter,stageIndex,stageType,monsterTotal,waveCountMin,waveCountMax,eliteScaleMul,note");
        sb.AppendLine("1,0,Normal,18,4,6,1.0,第一章首关");
        sb.AppendLine("*,*,Normal,0,3,6,1.0,默认普通关公式");
        sb.AppendLine("*,*,Elite,0,3,6,1.5,精英关公式");
        sb.AppendLine("*,*,Boss,0,3,7,1.0,Boss小怪公式");
        File.WriteAllText(TablesDir + "/stage_spawn.csv", sb.ToString(), new UTF8Encoding(false));
    }

    static void WriteTutorialBattle()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# order=执行顺序；action=normal|flank|around");
        sb.AppendLine("order,action,count,spriteMelee,spriteRanged,ambush,mercId,mercHpRatio,aheadDist,stunned,note");
        sb.AppendLine("1,normal,2,2,1,0,,,,,首波");
        sb.AppendLine("2,normal,2,2,1,0,,,,,第二小波");
        sb.AppendLine("3,flank,5,2,1,1,,,,,宝箱埋伏");
        sb.AppendLine("4,around,3,2,1,0,dunbing101,0.35,5.5,1,围殴老盾");
        sb.AppendLine("5,normal,4,2,1,0,,,,,组队后");
        sb.AppendLine("6,flank,3,2,1,1,,,,,清场后侧翼");
        File.WriteAllText(TablesDir + "/tutorial_battle.csv", sb.ToString(), new UTF8Encoding(false));
    }

    static Dictionary<int, MonsterStatsRow> LoadMonsterStatsRows()
    {
        var dict = new Dictionary<int, MonsterStatsRow>();
        if (!File.Exists(MonsterStatsCsv)) return dict;

        var lines = GameTableCsv.ParseRows(File.ReadAllText(MonsterStatsCsv, Encoding.UTF8));
        for (int i = 1; i < lines.Count; i++)
        {
            var c = lines[i];
            if (c.Length < 15) continue;
            if (!GameTableCsv.TryInt(c[1], out int ch)) continue;
            if (!GameTableCsv.TryInt(c[2], out int idx)) continue;
            dict[Key(ch, idx)] = new MonsterStatsRow(c);
        }
        return dict;
    }

    static Dictionary<int, string> LoadAttackStyleNotes()
    {
        var dict = new Dictionary<int, string>();
        string path = TablesDir + "/monster_attack_style.csv";
        if (!File.Exists(path)) return dict;

        var lines = GameTableCsv.ParseRows(File.ReadAllText(path, Encoding.UTF8));
        for (int i = 1; i < lines.Count; i++)
        {
            var c = lines[i];
            if (c.Length < 4) continue;
            if (!GameTableCsv.TryInt(c[0], out int ch)) continue;
            if (!GameTableCsv.TryInt(c[1], out int idx)) continue;
            dict[Key(ch, idx)] = c[3];
        }
        return dict;
    }

    static int MonsterChapterFromFolder(string folder)
    {
        if (string.IsNullOrEmpty(folder)) return 1;
        int spaceIdx = folder.IndexOf(' ');
        if (spaceIdx > 0 && int.TryParse(folder.Substring(0, spaceIdx), out int ch))
            return ch;
        return 1;
    }

    static string FormatMonsterRow(MonsterStatsRow row) => string.Join(",", row.cells);

    static int Key(int ch, int idx) => ch * 100 + idx;

    static string BuildDefaultId(int ch, int idx)
    {
        string theme = ch >= 0 && ch < ThemePrefixes.Length ? ThemePrefixes[ch] : "mob";
        return $"{theme}_{ch}{idx:00}";
    }

    static int ExtractChapterFromId(string id)
    {
        if (string.IsNullOrEmpty(id)) return 0;
        int us = id.IndexOf('_');
        if (us < 0 || us + 1 >= id.Length) return 0;
        string tail = id.Substring(us + 1);
        if (tail.Length >= 3 && int.TryParse(tail.Substring(0, tail.Length - 2), out int ch))
            return ch;
        return 0;
    }

    static int ExtractSpriteFromId(string id)
    {
        if (string.IsNullOrEmpty(id)) return 1;
        int us = id.IndexOf('_');
        if (us < 0 || us + 1 >= id.Length) return 1;
        string tail = id.Substring(us + 1);
        if (tail.Length >= 2 && int.TryParse(tail.Substring(tail.Length - 2), out int idx))
            return idx;
        return 1;
    }

    static int DefaultMinWave(int spriteIndex, bool isBoss)
    {
        if (isBoss) return 9;
        if (spriteIndex <= 2) return 0;
        if (spriteIndex <= 4) return 1;
        return 2;
    }

    static int DefaultUnlock(int spriteIndex, bool isBoss)
    {
        if (isBoss || spriteIndex >= 11) return 0;
        if (spriteIndex <= 5) return 0;
        if (spriteIndex <= 8) return 2;
        return 4;
    }

    static (float hp, float atk, float asp, float ar, float ms, int gold, int exp) DefaultStats(int ch, int idx, bool isBoss)
    {
        if (isBoss)
            return (3000f, 45f, 1f, 4f, 1.8f, 200, 100);
        float scale = 1f + (ch - 1) * 0.12f;
        return (
            Mathf.Round(50f * scale + idx * 2f * 10f) / 10f,
            Mathf.Round((5f * scale + idx * 0.5f) * 10f) / 10f,
            1.5f, 1.5f, 2.2f, 10 + ch, 5 + ch);
    }

    static string Csv(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        if (s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0)
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }

    struct MonsterStatsRow
    {
        public readonly string[] cells;
        public MonsterStatsRow(string[] c) { cells = c; }
    }

    [MenuItem("Tools/Data/从 BattleQuestConfig 导出 battle_quest.csv")]
    public static void ExportBattleQuestCsv()
    {
        Directory.CreateDirectory(TablesDir);
        var sb = new StringBuilder();
        sb.AppendLine("# gameChapter/stageType/isGoldDungeon: * \u4e3a\u901a\u914d");
        sb.AppendLine("gameChapter,stageType,isGoldDungeon,objective,clearGold,normalBase,normalChapterAdd,eliteGoldMul,note");
        string[] objectives =
        {
            "\u51fb\u8d25 Boss \u68ee\u4e4b\u5b88\u62a4\u8005", "\u51fb\u8d25 Boss \u5893\u56ed\u5b88\u536b",
            "\u51fb\u8d25 Boss \u96e8\u6797\u5de8\u86db", "\u51fb\u8d25 Boss \u6d77\u5996\u87f9",
            "\u51fb\u8d25 Boss \u65f6\u4e4b\u98ce\u8f66\u7cbe\u7075", "\u51fb\u8d25 Boss \u6676\u77f3\u5de8\u50cf",
            "\u51fb\u8d25 Boss \u88c2\u9699\u5316\u8eab \u00b7 \u5c0f\u7f8e", "\u51fb\u8d25 Boss \u88c2\u7f1d\u610f\u5fd7"
        };
        int[] gold = { 200, 300, 400, 500, 600, 700, 800, 2000 };
        for (int i = 0; i < 8; i++)
            sb.AppendLine(string.Join(",", (i + 1).ToString(), "Boss", "0", Csv(objectives[i]), gold[i], "", "", "", ""));
        sb.AppendLine("*,Normal,0,\u51fb\u8d25\u6240\u6709\u654c\u4eba,0,25,10,,");
        sb.AppendLine("*,Elite,0,\u51fb\u8d25\u6240\u6709\u654c\u4eba,0,25,10,1.5,");
        sb.AppendLine("*,*,1,\u6e05\u5272\u91d1\u5e01\u526f\u672c\u654c\u4eba,0,,,,");
        File.WriteAllText(TablesDir + "/battle_quest.csv", sb.ToString(), new UTF8Encoding(false));
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Data", "已导出 battle_quest.csv", "OK");
    }

    [MenuItem("Tools/Data/生成 stage_roller_weights 种子")]
    public static void GenerateStageRollerWeightsSeed()
    {
        Directory.CreateDirectory(TablesDir);
        var sb = new StringBuilder();
        sb.AppendLine("key,value,note");
        sb.AppendLine("bossWindow,3,");
        sb.AppendLine("maxRestPerChapter,2,");
        sb.AppendLine("bossWeightBase,0.22,");
        sb.AppendLine("bossWeightStep,0.24,");
        sb.AppendLine("restWeightBase,0.10,");
        sb.AppendLine("restWeightPerStageIndex,0.035,");
        sb.AppendLine("restFirstChapterMultiplier,1.6,");
        sb.AppendLine("eliteWeightBase,0.15,");
        sb.AppendLine("eliteWeightPerStageIndex,0.05,");
        sb.AppendLine("normalWeightFloor,0.2,");
        sb.AppendLine("normalWeightComplement,1.0,");
        File.WriteAllText(TablesDir + "/stage_roller_weights.csv", sb.ToString(), new UTF8Encoding(false));
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Data", "已写入 stage_roller_weights.csv", "OK");
    }

    [MenuItem("Tools/Data/生成 sprite_pick_weight / wave_slot 种子")]
    public static void GenerateSpritePickAndWaveSlotSeed()
    {
        Directory.CreateDirectory(TablesDir);
        var pick = new StringBuilder();
        pick.AppendLine("stageIndexMin,stageIndexMax,spriteIndex,weight,formula,minWeight,note");
        pick.AppendLine("0,0,1,5,,,");
        pick.AppendLine("0,0,0,1,,,");
        pick.AppendLine("1,2,1,3,,,");
        pick.AppendLine("1,2,2,2,,,");
        pick.AppendLine("1,2,0,1,,,");
        pick.AppendLine("3,999,0,0,spriteIndex*0.5,1.0,");
        File.WriteAllText(TablesDir + "/sprite_pick_weight.csv", pick.ToString(), new UTF8Encoding(false));

        var slot = new StringBuilder();
        slot.AppendLine("gameChapter,stageIndex,stageType,waveIndex,slotIndex,spriteIndex,styleFilter,allowDuplicate,note");
        File.WriteAllText(TablesDir + "/wave_slot.csv", slot.ToString(), new UTF8Encoding(false));

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Data", "已写入 sprite_pick_weight / wave_slot 种子", "OK");
    }

    [MenuItem("Tools/Data/生成 chapter_branch 种子")]
    public static void GenerateChapterBranchSeed()
    {
        Directory.CreateDirectory(TablesDir);
        var edges = new StringBuilder();
        edges.AppendLine("gameChapter,fromIndex,toIndex,edgeKind,priority,note");
        for (int i = 0; i < 9; i++)
            edges.AppendLine(string.Join(",", "*", i, i + 1, "main", "0", ""));
        File.WriteAllText(TablesDir + "/chapter_branch.csv", edges.ToString(), new UTF8Encoding(false));

        var rules = new StringBuilder();
        rules.AppendLine("gameChapter,branchCountMin,branchCountMax,branchPoolFrom,branchPoolTo,skipDistance,note");
        rules.AppendLine("*,1,2,1,5,2,");
        File.WriteAllText(TablesDir + "/chapter_branch_rules.csv", rules.ToString(), new UTF8Encoding(false));

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Data", "已写入 chapter_branch / chapter_branch_rules 种子", "OK");
    }
}
