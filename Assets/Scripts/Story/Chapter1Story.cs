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
            StoryDirector.Solo("赛琳",
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
            // 【2026-10-18 主人报「剧情没有背景」】这段原先整段没挂 .Bg，露出底下战斗场景；
            // 复用 abyss_choice（裂隙石台）顶上，后续台词与选完选项后的剧情自动沿用同一张。
            StoryDirector.Narration("击败 Boss 后，你在地上发现一道新鲜的剑痕。").Bg(StoryBackgrounds.AbyssChoice),
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

        // 【2026-11-11 主人拍板】选项后果只留玩家可感知的战斗加成：
        // 剧情 NPC 好感（NpcXiaomei/NpcMaster）是隐藏数值，好感界面看不到、图鉴判定也用不上（教程完成即达标），全部删；
        // C 原「当场回 5% 血」打完 Boss 才结算属无感 → 改「我方受到伤害 ×0.95」持久生效（BattleManager.Query 同 A/B 一套模式）。
        if (index == 0)
        {
            BattleManager.Instance?.ApplyChapter1ChoiceModifiers("A");
        }
        else if (index == 1)
        {
            BattleManager.Instance?.ApplyChapter1ChoiceModifiers("B");
            var bm = BattleManager.Instance;
            if (bm != null)
                bm.currentGold = (long)(bm.currentGold * 1.1f);
        }
        else
        {
            BattleManager.Instance?.ApplyChapter1ChoiceModifiers("C");
        }
    }

    static void PlayAfterChoice(Action onDone)
    {
        // 【2026-11-05 主人拍板「删」】原两句旁白「石碑上的防御架势 / 获得技能：圣盾壁垒。」
        // 与 toast 里的技能名一并删除：holy_barrier 开局自带（player_skills.csv unlockChapter=0、
        // PlayerSkillDefs.Fallback unlockSource=None、剑盾初始技能），剧情里本就没有真实解锁动作，
        // 三选一播完又宣告一次会误导玩家。金币奖励 / 回城队列 / 对外回调全部照旧。
        // 选完选项时 StoryDirector 的 choice 分支已 ui.Hide() + ResumeGame()（StoryDirector.cs:345-352），
        // 这里直接发奖励即可，不必再开一次 Play（空 beats 会打 LogError、还会二次暂停游戏）。
        // 【2026-10-18 主人拍板：材料产出全部改发抽奖币】原「分解材料×3」按锚价（1材料=30金）折成金币×90
        // 【2026-11-11 主人拍板：剧情金币奖励全删】+20、+90 两笔都删，本段剧情不再发任何钱
        StoryProgress.QueueChapter1TownReturn();
        onDone?.Invoke();
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
            StoryDirector.Solo("赛琳",
                "裂隙内偶尔会出现其他小队的痕迹，不要过度调查。那是其他部门的工作。",
                StoryPortraits.Receptionist)
                .Bg(StoryBackgrounds.GuildHall),
            StoryDirector.Narration("你没有接话。那个名字在喉咙里滚了一圈，又被你咽了回去。")
        };
        StoryDirector.Ensure().Play(beats, onDone);
    }
}
