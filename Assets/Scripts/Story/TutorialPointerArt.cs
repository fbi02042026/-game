using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>硬引导手指指针图（Art/UI/引导/引导.png）。</summary>
public static class TutorialPointerArt
{
    public const string AssetPath = "Assets/Art/UI/引导/引导.png";

    static Sprite _cached;

    public static Sprite Get()
    {
        if (_cached != null) return _cached;
#if UNITY_EDITOR
        // 2026-09-28 主人拍板：编辑器不再走美术源目录，与真机口径一致。
        if (DeviceParity.EditorFallbackEnabled)
        {
            _cached = AssetDatabase.LoadAssetAtPath<Sprite>(AssetPath);
            if (_cached == null)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetPath);
                if (tex != null)
                    _cached = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 1f), 100f);
            }
        }
#endif
        if (_cached == null)
        {
            _cached = Resources.Load<Sprite>("UI/Tutorial/pointer_hand");
            if (_cached == null)
            {
                var tex = Resources.Load<Texture2D>("UI/Tutorial/pointer_hand");
                if (tex != null)
                    _cached = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 1f), 100f);
            }
        }
        return _cached;
    }
}
