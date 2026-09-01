using System.Collections.Generic;
using UnityEngine;

public static class ChapterThemeMapTable
{
    public struct Row
    {
        public int gameChapter;
        public int monsterChapter;
        public string folderName;
        public string mapName;
        public string bgFolder;
    }

    static readonly List<Row> _rows = new List<Row>();
    static bool _loaded;

    public static bool HasData => _loaded && _rows.Count > 0;

    public static void Reload()
    {
        _loaded = false;
        _rows.Clear();
        EnsureLoaded();
    }

    public static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;

        string raw = GameTableStore.LoadText(ContentPaths.Data.ChapterThemeMap);
        if (string.IsNullOrEmpty(raw)) return;

        var lines = GameTableCsv.ParseRows(raw);
        for (int i = 1; i < lines.Count; i++)
        {
            var c = lines[i];
            if (c.Length < 4) continue;
            if (!GameTableCsv.TryInt(c[0], out int gameChapter)) continue;
            if (!GameTableCsv.TryInt(c[1], out int monsterChapter)) continue;

            _rows.Add(new Row
            {
                gameChapter = gameChapter,
                monsterChapter = monsterChapter,
                folderName = c[2],
                mapName = c.Length > 3 ? c[3] : "",
                bgFolder = c.Length > 4 && !string.IsNullOrEmpty(c[4]) ? c[4] : c[2]
            });
        }
        Debug.Log($"[ChapterThemeMap] 已加载 {_rows.Count} 条");
    }

    static Row? Find(int gameChapter)
    {
        EnsureLoaded();
        for (int i = 0; i < _rows.Count; i++)
        {
            if (_rows[i].gameChapter == gameChapter)
                return _rows[i];
        }
        return null;
    }

    public static bool TryGet(int gameChapter, out Row row)
    {
        var found = Find(gameChapter);
        if (found.HasValue)
        {
            row = found.Value;
            return true;
        }
        row = default;
        return false;
    }

    public static int GetMonsterChapter(int gameChapter)
    {
        if (TryGet(gameChapter, out var row))
            return row.monsterChapter;
        return gameChapter;
    }

    public static string GetMapName(int gameChapter)
    {
        if (TryGet(gameChapter, out var row) && !string.IsNullOrEmpty(row.mapName))
            return row.mapName;
        int idx = Mathf.Clamp(gameChapter - 1, 0, GameConfig.ChapterMapNames.Length - 1);
        return GameConfig.ChapterMapNames[idx];
    }

    public static string GetFolderName(int gameChapter)
    {
        if (TryGet(gameChapter, out var row) && !string.IsNullOrEmpty(row.folderName))
            return row.folderName;
        int idx = Mathf.Clamp(gameChapter - 1, 0, GameConfig.ChapterMonsterFolders.Length - 1);
        return GameConfig.ChapterMonsterFolders[idx];
    }
}
