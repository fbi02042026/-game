using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>正式第一章：大厅开场 + Boss 后石碑选择。</summary>
public static class Chapter1Story
{
    public static void PlayHallIntro(Action onDone)
    {
        var beats = new List<StoryBeat>
        {
            StoryDirector.Solo("咨询台小姐",
                "森林层是新人练手的好地方。最近有一支正式小队路过这里，你应该不会遇到太强的敌人。",
                StoryPortraits.Receptionist)
                .Bg(StoryBackgrounds.GuildHall),
            StoryDirector.Narration("她说得轻松，但你注意到她低头整理文件时，手指停了一下。")
        };
        StoryDirector.Ensure().Play(beats, () =>
        {
            StoryProgress.MarkChapter1IntroDone();
            onDone?.Invoke();
        });
    }

    public static void PlayEnding(Action onDone)
    {
        var beats = new List<StoryBeat>
        {
            StoryDirector.Narration("击败 Boss 后，你在地上发现一道新鲜的剑痕。"),
            StoryDirector.Solo("你",
                "有人比我早到一步。这剑痕……有点眼熟。",
                StoryPortraits.Player),
            StoryDirector.Narration("剑痕指向一块被藤蔓覆盖的石碑。你蹲下身，看着那道痕迹，心里突然闪过一个名字。"),
            new StoryBeat
            {
                leftName = "你",
                leftPortraitId = StoryPortraits.Player,
                text = "这剑痕……",
                speaker = -1,
                choices = new[]
                {
                    "她还活着，一定来过这里。",
                    "这剑痕……不能确定是谁。",
                    "不管是谁，先活下去再说。"
                }
            }
        };

        StoryDirector.Ensure().Play(beats, null, choice =>
        {
            ApplyChoice(choice);
            PlayAfterChoice(onDone);
        });
    }

    static void ApplyChoice(int index)
    {
        string id = index == 0 ? "A" : index == 1 ? "B" : "C";
        StoryProgress.MarkChapter1Choice(id);
        var bm = BattleManager.Instance;
        var hero = bm != null ? bm.hero : Hero.Instance;

        if (index == 0)
        {
            StoryProgress.AddBond(StoryProgress.NpcXiaomei, 10);
            BattleManager.Instance?.ApplyChapter1ChoiceModifiers("A");
        }
        else if (index == 1)
        {
            BattleManager.Instance?.ApplyChapter1ChoiceModifiers("B");
            if (bm != null)
                bm.currentGold = (long)(bm.currentGold * 1.1f);
        }
        else
        {
            StoryProgress.AddBond(StoryProgress.NpcMaster, 5);
            StoryProgress.AddBond(StoryProgress.NpcXiaomei, -5);
            if (hero != null && hero.attr != null)
            {
                float maxHp = hero.attr.GetAttr(AttrType.MaxHp);
                hero.attr.AddAttr(AttrType.MaxHp, maxHp * 0.05f, false);
                hero.currentHp = Mathf.Min(hero.currentHp + maxHp * 0.05f, hero.attr.GetAttr(AttrType.MaxHp));
            }
        }
    }

    static void PlayAfterChoice(Action onDone)
    {
        var beats = new List<StoryBeat>
        {
            StoryDirector.Narration("你站起身，看向石碑。上面刻着古老的防御架势。你照着比划了几下，体内涌起一股守护之力。"),
            StoryDirector.Narration("获得技能：圣盾壁垒。")
        };
        StoryDirector.Ensure().Play(beats, () =>
        {
            ResourceWallet.Add(ResourceWallet.ResourceType.Gold, 200, save: false, notify: true);
            ResourceWallet.Add(ResourceWallet.ResourceType.DecomposeMat, 3, save: true, notify: true);
            UIManager.Instance?.ShowToast("获得 圣盾壁垒、金币200、分解材料×3");
            StoryProgress.QueueChapter1TownReturn();
            onDone?.Invoke();
        });
    }

    public static void PlayReturnTown(Action onDone)
    {
        if (StoryProgress.GetChoice(1) == null)
        {
            onDone?.Invoke();
            return;
        }
        var beats = new List<StoryBeat>
        {
            StoryDirector.Solo("咨询台小姐",
                "裂隙内偶尔会出现其他小队的痕迹，不要过度调查。那是其他部门的工作。",
                StoryPortraits.Receptionist)
                .Bg(StoryBackgrounds.GuildHall),
            StoryDirector.Narration("你没有接话。那个名字在喉咙里滚了一圈，又被你咽了回去。")
        };
        StoryDirector.Ensure().Play(beats, onDone);
    }
}
