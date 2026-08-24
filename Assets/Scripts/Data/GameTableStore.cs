using UnityEngine;

/// <summary>加密表加载。优先 Resources/Data/Tables 的 .bytes；编辑器可回退源 CSV。</summary>
public static class GameTableStore
{
    public static string LoadText(string resourcesPathWithoutExt)
    {
        var ta = Resources.Load<TextAsset>(resourcesPathWithoutExt);
        if (ta != null && ta.bytes != null && ta.bytes.Length > 0)
        {
            if (SecureCodec.TryDecryptUtf8(ta.bytes, out string text) && !string.IsNullOrEmpty(text))
                return text;
            if (!string.IsNullOrEmpty(ta.text) && !ta.text.StartsWith("PAT1"))
                return ta.text;
        }

#if UNITY_EDITOR
        string id = resourcesPathWithoutExt;
        int slash = id.LastIndexOf('/');
        if (slash >= 0) id = id.Substring(slash + 1);
        string src = ContentPaths.Source.Tables + "/" + id + ".csv";
        if (System.IO.File.Exists(src))
            return System.IO.File.ReadAllText(src);
#endif
        return null;
    }
}
