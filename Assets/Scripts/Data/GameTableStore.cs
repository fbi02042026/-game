using UnityEngine;

/// <summary>表加载：优先 Resources/Data/Tables；支持明文或旧 PAT1。编辑器可回退源 CSV。</summary>
public static class GameTableStore
{
    public static string LoadText(string resourcesPathWithoutExt)
    {
        TextAsset ta = null;
        try
        {
            ta = Resources.Load<TextAsset>(resourcesPathWithoutExt);
        }
        catch (System.Exception e)
        {
            // 保险丝：构造期 / 反序列化期（MonoBehaviour 构造函数或字段初始化器）调用 Resources.Load
            // 会抛 UnityException。这里吞掉并回退，避免整个 MonoBehaviour 实例化失败导致场景打不开。
            Debug.LogWarning("[GameTableStore] Resources.Load 被拒绝（极可能是在构造函数或字段初始化器里调用）: "
                + resourcesPathWithoutExt + " -> " + e.Message);
            ta = null;
        }
        if (ta != null && ta.bytes != null && ta.bytes.Length > 0)
        {
            // 旧加密包仍可读
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
