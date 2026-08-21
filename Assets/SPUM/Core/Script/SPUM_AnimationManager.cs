using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
//using System.IO;
using UnityEngine;
using UnityEngine.UI;

public class SPUM_AnimationManager : MonoBehaviour
{
    [Header("Animation Play Controller")]
    [SerializeField] Slider timeLineSlider;
    [SerializeField] Slider playSpeedSlider;
    [SerializeField] Text slidertimeLineInfo;
    [SerializeField] Text timeLineText;
    [SerializeField] Text playSpeedText;
    
    public SPUM_Prefabs unit => SPUM_Manager != null ? SPUM_Manager.PreviewPrefab : null;
    public Transform StatePanel;
    public Button StateButtonPrefab;

    public string SelectedType;
    //private AnimatorOverrideController animatorOverrideController;
    public SPUM_AnimationControllerPanel AnimationControllerPanel;
    public SPUM_AnimationStatePanel spumAnimationStatePanel;
    public SPUM_AnimationPackagePanel spumAnimationPackagePanel;
    public string CurrentPlayClip;

    public RectTransform rectTransform;

    [Header("Animation Preset")]
    public SPUM_AnimationPreset PresetPrefab;
    public Transform PresetContent;
    public Button AddPresetButton;
    public SPUM_PresetData SPUM_PresetData;
    public Toggle PresetTogle;
    public Dropdown presetDropdown;
    [Header("Manager")]
    public SPUM_Manager SPUM_Manager;
    public void ScrollContentReset(){
        if (rectTransform != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
    }
    public void PlayAnimation(SpumAnimationClip currentPlayClip){
        if (unit == null || unit._anim == null)
        {
            Debug.LogWarning("[SPUM] AnimationManager: unit 或 unit._anim 为 null，跳过播放动画。");
            return;
        }
        Animator animator = unit._anim;
        AnimatorOverrideController animatorOverrideController = animator.runtimeAnimatorController as AnimatorOverrideController;

        var PlayState = $"{currentPlayClip.StateType}";
        animator.Rebind();
        animator.Update(0f);
        
        animatorOverrideController[PlayState] = LoadAnimationClip(currentPlayClip.ClipPath);

        animator.SetBool("1_Move", PlayState.Contains("MOVE"));
        animator.SetBool("5_Debuff", PlayState.Contains("DEBUFF"));
        animator.SetBool("isDeath", PlayState.Contains("DEATH"));
        animator.Play(PlayState, 0, 0);
        if (AnimationControllerPanel != null)
            AnimationControllerPanel.RefreshSlier(currentPlayClip.ClipPath);
    }

    AnimationClip LoadAnimationClip(string clipPath)
    {
        AnimationClip clip = Resources.Load<AnimationClip>(clipPath.Replace(".anim", ""));
        
        if (clip == null)
        {
            Debug.LogWarning($"Failed to load animation clip '{clipPath}'.");
        }
        
        return clip;
    }
    public void CloseAnimationPanels(){
        if (spumAnimationStatePanel != null)
            spumAnimationStatePanel.gameObject.SetActive(false);
        if (spumAnimationPackagePanel != null)
            spumAnimationPackagePanel.gameObject.SetActive(false);
    }
    
    void InitAnimator()
    {
        if (unit == null)
        {
            Debug.LogWarning("[SPUM] AnimationManager: unit (PreviewPrefab) 为 null，跳过动画初始化。");
            return;
        }

        foreach (var anim in unit.GetComponentsInChildren<Animator>(true))
        {
            if (anim == null) continue;
            Animator animator = anim;
            if (animator.runtimeAnimatorController == null) continue;

            var animatorOverrideController = new AnimatorOverrideController();
            animatorOverrideController.runtimeAnimatorController = animator.runtimeAnimatorController;

            AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;

            if (clips != null)
            {
                foreach (AnimationClip clip in clips)
                {
                    if (clip != null)
                        animatorOverrideController[clip.name] = clip;
                }
            }

            animator.runtimeAnimatorController = animatorOverrideController;
        }
    }

    public void InitializeDropdown()
    {
        if (presetDropdown == null) return;
        if (SPUM_PresetData == null || SPUM_PresetData.Presets == null) return;

        presetDropdown.ClearOptions();

        List<string> options = new List<string>();
        foreach (SPUM_Preset preset in SPUM_PresetData.Presets)
        {
            if (preset != null)
                options.Add($"{preset.UnitType} - {preset.PresetName}");
        }

        presetDropdown.AddOptions(options);
    }

    void Start()
    {
        try
        {
            InitAnimator();

            if (unit != null)
            {
                CreateSpumAnimationTypeButton();
                if (AnimationControllerPanel != null)
                    AnimationControllerPanel.Init(unit);
            }
            else
            {
                Debug.LogWarning("[SPUM] AnimationManager: unit 为 null，跳过动画相关初始化。");
            }

            if (spumAnimationStatePanel != null && spumAnimationStatePanel.CloseButton != null)
                spumAnimationStatePanel.CloseButton.onClick.AddListener(()=>{
                    spumAnimationStatePanel.gameObject.SetActive(false);
                });

            if (spumAnimationPackagePanel != null && spumAnimationPackagePanel.CloseButton != null)
                spumAnimationPackagePanel.CloseButton.onClick.AddListener(()=>{
                    if (spumAnimationStatePanel != null)
                        spumAnimationStatePanel.CreateStateButton(this);
                    spumAnimationPackagePanel.gameObject.SetActive(false);
                });

            if (spumAnimationStatePanel != null && spumAnimationStatePanel.ResetButton != null)
                spumAnimationStatePanel.ResetButton.onClick.AddListener(()=>{
                    ResetSelectedStateTypeIndex();
                    RefreshStatePanel();
                });

            if (unit != null && SPUM_Manager != null)
                InitPreviewUnitPackage();

            LoadPresetLst();

            if (AddPresetButton != null)
                AddPresetButton.onClick.AddListener(()=> {
                    Debug.Log("PresetAdd");
                    AddPreset();
                });

            if (PresetTogle != null)
                PresetTogle.onValueChanged.AddListener((On)=> {
                    if(On) LoadPresetLst();
                });

            if (presetDropdown != null)
                InitializeDropdown();
        }
        catch (System.Exception e)
        {
            Debug.LogError("[SPUM] AnimationManager Start() 失败: " + e.Message);
        }
    }
    [ContextMenu("TESE")]
    public void LoadPresetLst()
    {
        if (PresetContent == null)
        {
            Debug.LogWarning("[SPUM] AnimationManager: PresetContent 为 null，跳过加载预设列表。");
            return;
        }

        foreach (Transform element in PresetContent)
        {
            Destroy(element.gameObject);
        }
        if(SPUM_PresetData == null) return;
        if (unit == null)
        {
            Debug.LogWarning("[SPUM] AnimationManager: unit 为 null，跳过过滤预设。");
            return;
        }
        var filteredPresets = FilterPresetsByUnitType(unit.UnitType);

        foreach (var presetData in filteredPresets)
        {
            var Preset = Instantiate(PresetPrefab, PresetContent);
            Preset.Init(presetData, this);
        }
    }
    public List<SPUM_Preset> FilterPresetsByName(string presetName)
    {
        if (SPUM_PresetData == null || SPUM_PresetData.Presets == null) return new List<SPUM_Preset>();
        var filteredPresets = SPUM_PresetData.Presets.Where(p => p != null && p.PresetName == presetName).ToList();
        return filteredPresets;
    }

    public List<SPUM_Preset> FilterPresetsByUnitType(string unitType)
    {
        if (SPUM_PresetData == null || SPUM_PresetData.Presets == null) return new List<SPUM_Preset>();
        var filteredPresets = SPUM_PresetData.Presets.Where(p => p != null && p.UnitType == unitType).ToList();
        return filteredPresets;
    }
    public void AddPreset(){
        if (unit == null)
        {
            Debug.LogWarning("[SPUM] AnimationManager: unit 为 null，跳过添加预设。");
            return;
        }
        if (PresetPrefab == null || PresetContent == null) return;

        var Preset = Instantiate(PresetPrefab, PresetContent);
        SPUM_Preset presetData = new SPUM_Preset
        {
            UnitType = unit.UnitType,
            PresetName = System.DateTime.Now.ToString("yyyyMMddHHmmssfff"),
            Packages = unit.spumPackages?.Select(p => (SpumPackage)p.Clone()).ToList()
        };

        SavePresetData(presetData);
    }
    public void EditPresetData(string PreviousName, string ChangedName)
    {
        if (SPUM_PresetData == null || SPUM_PresetData.Presets == null)
        {
            Debug.LogWarning("[SPUM] SPUM_PresetData 或 Presets 为 null，无法编辑预设。");
            return;
        }
        SPUM_Preset presetToRemove = SPUM_PresetData.Presets.Find(p => p != null && p.PresetName == PreviousName);
        if (presetToRemove != null)
        {
            presetToRemove.PresetName = ChangedName;
            LoadPresetLst();
            Debug.Log("Changed: " + ChangedName);
        }
        else
        {
            Debug.LogWarning("Preset not found: " + PreviousName);
        }
    }
    public void SavePresetData(SPUM_Preset presetData)
    {
        if (SPUM_PresetData == null || SPUM_PresetData.Presets == null)
        {
            Debug.LogWarning("[SPUM] SPUM_PresetData 或 Presets 为 null，无法保存预设。");
            return;
        }
        SPUM_PresetData.Presets.Add(presetData);
        // EditorUtility.SetDirty(SPUM_PresetData);
        // AssetDatabase.SaveAssets();
        LoadPresetLst();
        Debug.Log("Preset saved: " + presetData.PresetName);
    }
    public void DeletePresetData(string name)
    {
        if (SPUM_PresetData == null || SPUM_PresetData.Presets == null)
        {
            Debug.LogWarning("[SPUM] SPUM_PresetData 或 Presets 为 null，无法删除预设。");
            return;
        }
        SPUM_Preset presetToRemove = SPUM_PresetData.Presets.Find(p => p != null && p.PresetName == name);
        if (presetToRemove != null)
        {
            SPUM_PresetData.Presets.Remove(presetToRemove);
            Debug.Log("Preset deleted: " + presetToRemove.PresetName);
            LoadPresetLst();
        }
        else
        {
            string code = unit != null ? unit._code : "unknown";
            Debug.LogWarning("Preset not found: " + code);
        }
    }
    public void ApplyPreset(SPUM_Preset preset)
    {
        if (unit == null)
        {
            Debug.LogWarning("[SPUM] AnimationManager: unit 为 null，跳过应用预设。");
            return;
        }
        unit.spumPackages = preset.Packages;
        if (PresetTogle != null)
            PresetTogle.isOn = false;
        PlayFirstAnimation() ;
    }
    public void PlayFirstAnimation() 
    {
        if (unit == null || unit.spumPackages == null)
        {
            Debug.LogWarning("[SPUM] AnimationManager: unit 或 unit.spumPackages 为 null，跳过 PlayFirstAnimation。");
            return;
        }
        var clip = unit.spumPackages
            .SelectMany(package => package.SpumAnimationData)
            .FirstOrDefault(data => 
                data.StateType.Equals("IDLE", System.StringComparison.OrdinalIgnoreCase) && 
                data.HasData && 
                data.index == 0 && 
                data.UnitType.Equals(unit.UnitType));
        if (clip == null) {
            Debug.LogWarning("package data error");
            var legacyData = SPUM_Manager.GetSpumLegacyData();
            if (legacyData != null)
                unit.spumPackages = legacyData;
            return;
        }
        PlayAnimation(clip);
    } 


    void CreateSpumAnimationTypeButton()
    {
        if (unit == null || unit.spumPackages == null)
        {
            Debug.LogWarning("[SPUM] AnimationManager: unit 或 unit.spumPackages 为 null，跳过创建动画类型按钮。");
            return;
        }

        if (spumAnimationStatePanel == null || spumAnimationStatePanel.AddClipButton == null)
        {
            Debug.LogWarning("[SPUM] AnimationManager: spumAnimationStatePanel 或 AddClipButton 为 null，跳过。");
            return;
        }

        if (StateButtonPrefab == null || StatePanel == null)
        {
            Debug.LogWarning("[SPUM] AnimationManager: StateButtonPrefab 或 StatePanel 为 null，跳过。");
            return;
        }

        if (SPUM_Manager == null || SPUM_Manager.StateList == null)
        {
            Debug.LogWarning("[SPUM] AnimationManager: SPUM_Manager 或 StateList 为 null，跳过。");
            return;
        }

        spumAnimationStatePanel.AddClipButton.onClick.RemoveAllListeners();
       
        foreach (string state in SPUM_Manager.StateList)
        {
            var StateButton = Instantiate(StateButtonPrefab, StatePanel);
            var buttonText = StateButton.GetComponentInChildren<Text>();
            if (buttonText != null)
                buttonText.text = state;

            var stateType = state;
            StateButton.onClick.AddListener( () => 
            {
                SelectedType = stateType;
                if (spumAnimationPackagePanel != null)
                    spumAnimationPackagePanel.gameObject.SetActive(false);
                if (spumAnimationStatePanel != null)
                {
                    spumAnimationStatePanel.gameObject.SetActive(true);
                    spumAnimationStatePanel.CreateStateButton(this);
                }
            } );
        }

        // 클립 추가 버튼 데이터 할당
        spumAnimationStatePanel.AddClipButton.onClick.AddListener( () =>
        {
            if (spumAnimationPackagePanel != null)
            {
                spumAnimationPackagePanel.gameObject.SetActive(true);
                spumAnimationPackagePanel.CreateSpumAnimationPackagePanel(this);
            }
        });
    }

    public void RefreshStatePanel(){
        if (spumAnimationStatePanel != null)
            spumAnimationStatePanel.CreateStateButton(this);
    }
    public void InitPreviewUnitPackage(){
        var legacyData = SPUM_Manager.GetSpumLegacyData();
        if (legacyData != null)
            unit.spumPackages = legacyData;
        else
        {
            Debug.LogWarning("[SPUM] AnimationManager: GetSpumLegacyData 返回 null。");
            return;
        }
        ResetSelectedStateTypeIndex();
        PlayFirstAnimation();
    }
    public void ResetSelectedStateTypeIndex()
    {
        if (SPUM_Manager == null || SPUM_Manager.spumPackages == null)
        {
            Debug.LogWarning("[SPUM] AnimationManager: SPUM_Manager 或 spumPackages 为 null，跳过 ResetSelectedStateTypeIndex。");
            return;
        }
        unit.spumPackages = SPUM_Manager.spumPackages;
        var UnitPackagesData = unit.spumPackages;
        if (UnitPackagesData == null)
        {
            Debug.LogWarning("[SPUM] AnimationManager: unit.spumPackages 为 null，跳过。");
            return;
        }
        if (unit == null)
        {
            Debug.LogWarning("[SPUM] AnimationManager: unit 为 null，跳过 ResetSelectedStateTypeIndex。");
            return;
        }

        string SelectState = SelectedType;
        string UnitType = unit.UnitType;

        foreach (var package in UnitPackagesData)
        {
            if (package == null || package.SpumAnimationData == null) continue;
            foreach (var clipData in package.SpumAnimationData)
            {
                if (clipData == null) continue;
                if (clipData.StateType.Equals(SelectedType) && clipData.UnitType.Equals(unit.UnitType))
                {
                    clipData.index = -1;
                    clipData.HasData = false;
                }
            }
        }
        SpumPackage legacyPackage = null;

        foreach (var package in UnitPackagesData)
        {
            if (package == null || string.IsNullOrEmpty(package.Name)) continue;
            if (package.Name.Trim().Equals("Legacy", StringComparison.OrdinalIgnoreCase))
            {
                legacyPackage = package;
                break;
            }
        }
        //var legacyPackage = UnitPackagesData.Where(p => p.Name.Trim().Equals("Legacy", StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
        if (legacyPackage != null && legacyPackage.SpumAnimationData != null)
        {
            var relevantClips = legacyPackage.SpumAnimationData
                .Where(clipData => 
                    clipData != null &&
                    clipData.StateType.Equals(SelectedType) &&
                    clipData.UnitType.Equals(unit.UnitType))
                .ToList();
            for (int i = 0; i < relevantClips.Count; i++)
            {
                //Debug.Log(relevantClips[i].Name);
                relevantClips[i].index = i;
                relevantClips[i].HasData = true;
            }
        }
    }
    public void IndexSawp(int clipindex, int dir){
        if(clipindex == 0  && dir < 0) return;
        if (unit == null || unit.spumPackages == null) return;

        var UnitPackagesData = unit.spumPackages;
        var filteredClips = UnitPackagesData
        .SelectMany(package => package.SpumAnimationData)
        .Where(clip => clip.StateType.Equals(SelectedType) && clip.HasData && clip.UnitType.Equals(unit.UnitType)).OrderBy(clip => clip.index).ToList();
        

        int currentIndex = clipindex;
        int swapTargetIndex = clipindex + dir;
        if(swapTargetIndex == filteredClips.Count) return;
        //Debug.Log($"{currentIndex}==>{swapTargetIndex} {filteredClips.Count}");
        filteredClips[currentIndex].index = swapTargetIndex;
        filteredClips[swapTargetIndex].index = currentIndex;
        RefreshStatePanel();
    }
}