//using System.IO;
using UnityEngine;
using UnityEngine.UI;

public class SPUM_AnimationControllerPanel : MonoBehaviour
{
    [Header("Animation Play Controller")]
    [SerializeField] Slider timeLineSlider;
    [SerializeField] Slider playSpeedSlider;
    [SerializeField] Text slidertimeLineInfo;
    [SerializeField] Text timeLineText;
    [SerializeField] Text playSpeedText;
    [SerializeField] Text ClipName;
    private SPUM_Prefabs previewUnit;

    public void Init(SPUM_Prefabs unit){
        previewUnit = unit;
        if (timeLineSlider != null)
        {
            timeLineSlider.minValue = 0f;
            timeLineSlider.maxValue = 1f;
            timeLineText = timeLineSlider.transform.GetComponentInChildren<Text>();
        }

        if (playSpeedSlider != null)
        {
            playSpeedSlider.minValue = 1;
            playSpeedSlider.maxValue = 200;
            
            playSpeedSlider.wholeNumbers = true;
            playSpeedText = playSpeedSlider.transform.GetComponentInChildren<Text>();
            
            playSpeedSlider.onValueChanged.AddListener( x => {
                if (previewUnit == null || previewUnit._anim == null) return;
                var AnimationSpeed = x * .01f;
                previewUnit._anim.speed = AnimationSpeed;
                if (playSpeedText != null)
                    playSpeedText.text = string.Format("Speed x{0:0.00}", AnimationSpeed);
                var clipInfo = previewUnit._anim.GetCurrentAnimatorClipInfo(0);
                if (clipInfo != null && clipInfo.Length > 0 && clipInfo[0].clip != null)
                {
                    var ClipText = clipInfo[0].clip.name;
                    if (ClipName != null)
                        ClipName.text = $"{ClipText} [{string.Format("{0:F2}", SetClipTime(1))}]";
                }
            });
            playSpeedSlider.value = 100f;
        }
    }
    public void RefreshSlier(string clipPath){
        if (string.IsNullOrEmpty(clipPath)) return;
        var Name = clipPath.Split('/');
        var clip = Name[Name.Length-1].Replace(".anim","");
        if (timeLineSlider != null)
            timeLineSlider.SetValueWithoutNotify(0f);
        if (playSpeedSlider != null)
            playSpeedSlider.onValueChanged.Invoke(playSpeedSlider.value);
        if (ClipName != null)
            ClipName.text = $"{clip} [{string.Format("{0:F2}", SetClipTime(1))}]";
    }
    private float SetClipTime(float progress){
        if (previewUnit == null || previewUnit._anim == null) return 0f;
        var clipInfo = previewUnit._anim.GetCurrentAnimatorClipInfo(0);
        if (clipInfo == null || clipInfo.Length == 0 || clipInfo[0].clip == null) return 0f;
        float clipLength = clipInfo[0].clip.length;
        if (playSpeedSlider == null || playSpeedSlider.value == 0) return 0f;
        var attackAnimationClipPlayTime = (clipLength / (playSpeedSlider.value * 0.01f)) * progress;
        return attackAnimationClipPlayTime;
       
    }
    private void SetAnimationNormailzedTime(float progress)
    {
        if (previewUnit == null || previewUnit._anim == null) return;
        var state = previewUnit._anim.GetCurrentAnimatorStateInfo(0);
        //Debug.Log(attackAnimationClipPlayTime);
        previewUnit._anim.speed = 0;
        previewUnit._anim.Play(state.shortNameHash, 0, progress);
        previewUnit._anim.Update(0f);
        if (timeLineText != null)
            timeLineText.text = string.Format("SEC :{0:F2}", SetClipTime(progress));
    }
}