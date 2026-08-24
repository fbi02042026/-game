using UnityEngine;

/// <summary>剧情道具 ID。资源在 Resources/Story/Props。</summary>
public static class StoryProps
{
    public const string QuestPaper = "quest_paper";
    public const string SpeechBubble = "speech_bubble";

    public static Sprite Get(string id)
    {
        return StoryAssetLoader.Load(StoryAssetLoader.Props, id);
    }
}
