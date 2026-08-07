using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using System;
using System.Globalization;
using UnityEngine.SceneManagement;

[Serializable]
public class SPUM_Animator{
    public string Type;
    public RuntimeAnimatorController RuntimeAnimator;
}
public class SPUM_Manager : MonoBehaviour
{
    public float _version;
    public string unitPath = "Assets/SPUM/Resources/Units/"; // 프리펩 저장 장소
    public string unitBackUpPath = "Assets/SPUM/Backup/";
    public bool isSaveSamePath = false;
    public SPUM_Prefabs EditPrefab;
    public SPUM_Prefabs PreviewPrefab; // 프리뷰 프리펩
    public SPUM_Animator[] SPUM_Animator; 
    public Dictionary<string, RuntimeAnimatorController> SPUM_AnimatorDic = new();
    public Toggle RandomColorButton;
    public List<SpumPackage> spumPackages;
    public List<string> StateList = new ();
    public List<string> UnitTypeList = new();
    public List<string> SpritePackageNameList = new();
    [Header("Manager")]
    [SerializeField] public SPUM_AnimationManager animationManager;
    [SerializeField] public SPUM_UIManager UIManager;
    [SerializeField] public SPUM_PaginationManager paginationManager;
    public IFileHandler fileHandler => GetComponent<IFileHandler>();

#region Unity Function
    void Awake()
    {
        SoonsoonData.Instance._spumManager= this;
        LoadPackages();
        
        // 修复: 如果 PreviewPrefab 为 null，尝试从 Resources 加载
        if (PreviewPrefab == null)
        {
            var previews = Resources.LoadAll<SPUM_Prefabs>("");
            if (previews.Length > 0)
            {
                PreviewPrefab = previews[0];
                Debug.Log("[SPUM] Auto-loaded PreviewPrefab from Resources: " + previews[0].name);
            }
            else
            {
                Debug.LogWarning("[SPUM] PreviewPrefab is null and no SPUM_Prefabs found in Resources. Some features may not work.");
            }
        }

        var unit = PreviewPrefab;
        if(unit != null && (unit.spumPackages == null || unit.spumPackages.Count.Equals(0))) {
            var InitLegacyData = GetSpumLegacyData();
            if(string.IsNullOrEmpty(unit.UnitType)) unit.UnitType = "Unit";
            unit.spumPackages = InitLegacyData;
        }
        var uniqueTypes = spumPackages
            .SelectMany(package => package.SpumAnimationData)
            .Select(clip => clip.StateType.ToUpper())
            .Distinct(System.StringComparer.OrdinalIgnoreCase)
            .ToList();
        StateList = uniqueTypes;

        var uniqueUnitTypes = spumPackages
            .SelectMany(package => package.SpumAnimationData)
            .Select(clip => clip.UnitType)
            .Distinct(System.StringComparer.OrdinalIgnoreCase)
            .ToList();
        UnitTypeList = uniqueUnitTypes;

        var uniquePackageNames = spumPackages
        .Where(package => package.SpumTextureData.Count > 0)
        .Select(package => package.Name)
        .Distinct(System.StringComparer.OrdinalIgnoreCase)
        .ToList();
        SpritePackageNameList = uniquePackageNames;
    }
    void Start()
    {
        // 修复: 确保 Canvas 有 GraphicRaycaster（World Space Canvas 必需）
        // 放在 try-catch 之前，确保即使后续初始化失败，按钮也能点击
        try
        {
            EnsureGraphicRaycaster();
        }
        catch (System.Exception e)
        {
            Debug.LogError("[SPUM] EnsureGraphicRaycaster 失败: " + e.Message);
        }

        // 修复: 确保 EventSystem 存在且有 InputModule
        try
        {
            EnsureEventSystem();
        }
        catch (System.Exception e)
        {
            Debug.LogError("[SPUM] EnsureEventSystem 失败: " + e.Message);
        }

        try
        {
            if (SPUM_Animator != null && SPUM_Animator.Length > 0)
            {
                SPUM_AnimatorDic = SPUM_Animator.ToDictionary(item => item.Type, item => item.RuntimeAnimator);
            }
            else
            {
                Debug.LogWarning("[SPUM] SPUM_Animator 数组为空或 null。");
                SPUM_AnimatorDic = new Dictionary<string, RuntimeAnimatorController>();
            }

            StartCoroutine(StartProcess());

            if (PreviewPrefab != null)
            {
                SetType("Unit");
                ItemResetAll();
                Setup();
            }
            else
            {
                Debug.LogWarning("[SPUM] PreviewPrefab 为 null，跳过角色初始化。请在 Inspector 中为 SPUM_Manager 的 PreviewPrefab 字段赋值。");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("[SPUM] SPUM_Manager Start() 初始化失败: " + e.Message);
            Debug.LogException(e);
        }
    }
    [ContextMenu("Setup")]
    public void Setup(){
        SetDefultSet("Unit", "Body", "Human_1", Color.white);
        SetDefultSet("Unit", "Eye", "Eye0", new Color32(71, 26,26, 255));
        SetDefultSet("Horse", "Body", "Horse1", Color.white);
    }
    
    private void EnsureGraphicRaycaster()
    {
        // 查找场景中所有 Canvas，确保每个都有 GraphicRaycaster
        var allCanvases = FindObjectsOfType<Canvas>();
        foreach (var canvas in allCanvases)
        {
            if (canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
            {
                canvas.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                Debug.Log("[SPUM] 已自动添加 GraphicRaycaster 到 Canvas: " + canvas.name);
            }
        }
    }

    private void EnsureEventSystem()
    {
        // 确保场景中有 EventSystem，否则 UI 按钮无法接收点击
        var eventSystem = FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
        if (eventSystem == null)
        {
            var esObj = new GameObject("EventSystem");
            esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            Debug.Log("[SPUM] 已自动创建 EventSystem");
        }
        else
        {
            // 确保 EventSystem 上有 StandaloneInputModule
            var inputModule = eventSystem.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            if (inputModule == null)
            {
                eventSystem.gameObject.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                Debug.Log("[SPUM] 已为 EventSystem 添加 StandaloneInputModule");
            }
        }
    }
#endregion

    public IEnumerator StartProcess()
    {
        Debug.Log("Data Load processing..");

        // 버전 체크 및 패키지 데이터 체크
        yield return StartCoroutine(SoonsoonData.Instance.LoadData());

        // 작업 색상 정보 로드
        if( SoonsoonData.Instance._soonData2._savedColorList == null ||  SoonsoonData.Instance._soonData2._savedColorList.Count.Equals(0))
        {
            SoonsoonData.Instance._soonData2._savedColorList = new List<string>();
            for(var i = 0 ; i < 17 ;i++)
            {
                SoonsoonData.Instance._soonData2._savedColorList.Add("");
            }
            SoonsoonData.Instance.SaveData();
        }
        else
        {
            //Debug.Log( SoonsoonData.Instance._soonData2._savedColorList.Count);
            for(var i = 0 ; i < SoonsoonData.Instance._soonData2._savedColorList.Count ;i++)
            {
                string tSTR = SoonsoonData.Instance._soonData2._savedColorList[i];
                //Debug.Log(tSTR);
                Color parsedColor;
                if(ColorUtility.TryParseHtmlString(tSTR, out parsedColor)){
                    if (UIManager != null && UIManager._colorSaveList != null && i < UIManager._colorSaveList.Count 
                        && UIManager._colorSaveList[i] != null && UIManager._colorSaveList[i]._savedColor != null)
                    {
                        UIManager._colorSaveList[i]._savedColor.gameObject.SetActive(true);
                        UIManager._colorSaveList[i]._savedColor.color = parsedColor;
                    }
                }else{
                    if(string.IsNullOrWhiteSpace(tSTR)) continue;
                    Debug.LogWarning(tSTR + " is Invalid color information");
                }
            }

        }
    }
    private void LoadPackages()
    {
        spumPackages.Clear();
        var jsonFileArray = Resources.LoadAll<TextAsset>("");
        //Debug.Log("Index" + jsonFileArray.Length);
        foreach (var asset in jsonFileArray)
        {
            if(asset == null) continue;
            if(!asset.name.Contains("Index")) continue;
            Debug.Log(asset);
            try
            {
                var Package = JsonUtility.FromJson<SpumPackage>(asset.ToString());
                if (Package != null)
                    spumPackages.Add(Package);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[SPUM] 无法解析 TextAsset '{asset.name}': {e.Message}");
            }
        }
        var dataSortList = spumPackages.OrderBy(p => DateTime.ParseExact(p.CreationDate, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)).ToList();
        spumPackages = dataSortList;
    }
    public List<SpumPackage> GetSpumPackageData(){
        return spumPackages;
    }
    public List<SpumPackage> GetSpumLegacyData(){
        string targetString = "Legacy";
        var LegacyData = spumPackages;
        foreach (var package in LegacyData)
        {
            if(package.Name.Equals(targetString)) {
                foreach (var data in package.SpumAnimationData)
                {
                    data.HasData = true;
                }
            }
        }
        return LegacyData; // Package;
    }
#region PreviewItemPanel
    public void DrawItemList(SPUM_SpriteButtonST ButtonData)
    {
        UIManager.SetPackageButtons(ButtonData);
        
        var enabledPackages =  UIManager.SpritePackagesFilterList
            .Where(kvp => kvp.Value)
            .Select(kvp => kvp.Key)
            .ToList();
        
        UIManager._panelTitle.text = ButtonData.PartType;

        UIManager.ClearPreviewItems();

        var SpumPackages =  spumPackages;

        string unitType = ButtonData.UnitType;
        string partType = ButtonData.PartType;

        //패키지 구룹화 - 패키지 필터 조건
        var groupedPackageData = SpumPackages
            .Where(p => enabledPackages.Contains(p.Name)) // 패키지 이름이 enabledPackages 리스트에 있는 것만 선택
            .SelectMany(p => p.SpumTextureData.Select(t => new { Package = p, Texture = t }))
            .Where(x => x.Texture.PartType.Equals(partType) && x.Texture.UnitType.Equals(unitType))
            .GroupBy(x => new { x.Texture.PartType, x.Texture.Name, x.Texture.UnitType }) // 구룹 키
            .Select(g => new 
            { 
                Key = g.Key, 
                Items = g.ToList() 
            })
            .ToList();
        
        // 필터된 패키지 아이템 순환
        foreach (var package in groupedPackageData) 
        {
            // 프리뷰 아이템 버튼 생성
            var PreviewItem = UIManager.CreatePreviewItem();
            var previewButton = PreviewItem.GetComponent<SPUM_PreviewItem>();
            previewButton.name = package.Key.Name;
            
            // 관련 패키지 데이터 주입
            var relatedPackages = package.Items.Select(item => item.Package).Distinct().ToList();
            previewButton.spumPackages = relatedPackages;
            
            // Hide Other Element
            foreach (Transform tr in previewButton.transform)
            {
                tr.gameObject.SetActive(tr.name.ToUpper() == ButtonData.ItemShowType.ToUpper());
            }
            previewButton.SetSpriteButton.onClick.AddListener(()=> { ButtonData.IsActive = true; } );

            //Preview Element Matching Map
            var UnitPartLists = previewButton.GetComponentsInChildren<SPUM_PreviewMatchingList>(); 

            // 보여질 이미지 추출
            var UnitPartList = UnitPartLists 
                .SelectMany(table => table.matchingTables)
                .Where(element => element.PartType == package.Key.PartType && element.UnitType == package.Key.UnitType)
                .ToList();
            //Debug.Log($"PartType: {group.Key.PartType}, Name: {group.Key.Name}, UnitType: {group.Key.UnitType}");
            foreach (var item in package.Items) // 아이템 개별 항목 순환
            {
                //Debug.Log($"  Package: {item.Package.Name}, SubType: {item.Texture.SubType}, Path: {item.Texture.Path}");
                foreach (var part in UnitPartList)
                {
                    if(part.Structure == item.Texture.SubType){ // 멀티플 타입 이미지 인 경우
                        var LoadSprite = LoadSpriteFromMultiple(item.Texture.Path, item.Texture.SubType);
                        part.ItemPath = item.Texture.Path;
                        part.image.sprite = LoadSprite;
                        part.PartSubType = item.Texture.PartSubType;
                        float pixelWidth = LoadSprite.rect.width;
                        float pixelHeight = LoadSprite.rect.height;

                        float unityWidth = pixelWidth / LoadSprite.pixelsPerUnit;
                        float unityHeight = pixelHeight / LoadSprite.pixelsPerUnit;

                        part.image.rectTransform.sizeDelta = new Vector2(unityWidth * 100, unityHeight * 100);
                        part.Dir = ButtonData.Direction;
                        Color PartColor = ButtonData.ignoreColorPart.Contains(item.Texture.SubType) ? Color.white : ButtonData.PartSpriteColor;
                        part.image.color = PartColor;
                        part.Color = PartColor;
                        part.MaskIndex = (int)ButtonData.SpriteMask;
                        previewButton.ImageElement.Add(part);
                    }
                    else if(UnitPartList.Count == 1) // 서브타입이 없는 멀티플이 아닌경우 
                    {
                        var LoadSprite = LoadSpriteFromMultiple(item.Texture.Path, item.Texture.SubType);
                        part.ItemPath = item.Texture.Path;
                        part.image.sprite = LoadSprite;
                        part.PartSubType = item.Texture.PartSubType;
                        float pixelWidth = LoadSprite.rect.width;
                        float pixelHeight = LoadSprite.rect.height;

                        float unityWidth = pixelWidth / LoadSprite.pixelsPerUnit;
                        float unityHeight = pixelHeight / LoadSprite.pixelsPerUnit;

                        part.image.rectTransform.sizeDelta = new Vector2(unityWidth * 100, unityHeight * 100);
                        part.Dir = ButtonData.Direction;
                        part.Structure = item.Texture.SubType.Equals(item.Texture.Name) ? package.Key.PartType : item.Texture.SubType;
                        Color PartColor = ButtonData.ignoreColorPart.Contains(item.Texture.PartSubType) ? Color.white : ButtonData.PartSpriteColor;
                        part.image.color = PartColor;
                        part.Color = PartColor;
                        part.MaskIndex = (int)ButtonData.SpriteMask;
                        previewButton.ImageElement.Add(part);
                    }
                }
            }
            previewButton.ImageElement = previewButton.ImageElement.Distinct().ToList();
            // 필요없는 항목 비활성화
            UnitPartList.ForEach(item => 
            {
                item.image.gameObject.SetActive(item.image.sprite != null);
            });

            // 바뀔 프리뷰 항목 가져오기
        }
        // 프리뷰 아이템 데이터를 적용 필요
        UIManager.ShowItem();
    }
#endregion

#region PreviewUnit
    public void SetType(string Type){
        var PreviewUnit = PreviewPrefab;
        if (PreviewUnit == null)
        {
            Debug.LogWarning("[SPUM] SetType: PreviewPrefab 为 null。");
            return;
        }
        PreviewUnit.UnitType = Type;
        foreach (Transform child in PreviewUnit.transform)
        {
            if (child != null)
                child.gameObject.SetActive(child.name.Contains(Type));
        }
        var anim = PreviewUnit.GetComponentInChildren<Animator>();
        PreviewPrefab._anim = anim;
        if(PreviewUnit.UnitType.Equals(Type)) return;
        if(Type.Equals("Unit"))
        {
            var ElementList = PreviewUnit.ImageElement;
            if (ElementList != null)
                ElementList.RemoveAll(element => element.UnitType != Type);
        }else{
            SetDefultSet(Type, "Body", Type+"1", Color.white);
        }
    }
    public void SetDefultSet(string UnitType, string PartType, string TextureName, Color color)
    {
        string PackageName ="Legacy";
        //패키지 구룹화
        var groupedData = spumPackages
            .SelectMany(p => p.SpumTextureData.Select(t => new { Package = p, Texture = t }))
            .Where(x => x.Texture.PartType.Equals(PartType) && x.Texture.UnitType.Equals(UnitType) && x.Package.Name.Equals(PackageName) && x.Texture.Name.Equals(TextureName))
            .GroupBy(x => new { x.Texture.PartType, x.Texture.Name, x.Texture.UnitType }) // 그룹 키
            .Select(g => new 
            { 
                Key = g.Key, 
                Items = g.ToList() 
            })
            .ToList();

        if (groupedData.Count == 0)
        {
            Debug.LogWarning($"[SPUM] SetDefultSet: 未找到匹配的纹理数据 - UnitType:{UnitType}, PartType:{PartType}, TextureName:{TextureName}, PackageName:{PackageName}");
            return;
        }

        var Parts = groupedData[0];

        var ListElement = new List<PreviewMatchingElement>();
        foreach (var item in Parts.Items)
        {
            //Debug.Log($"Path: {PartType}, SubType: { item.Texture.SubType}");
            var part = new PreviewMatchingElement();
            part.UnitType = UnitType;
            part.PartType = PartType;
            part.PartSubType = item.Texture.PartSubType;
            part.Dir = "";
            part.ItemPath = item.Texture.Path;
            part.Structure = item.Texture.SubType.Equals(item.Texture.Name) ? PartType : item.Texture.SubType;
            part.MaskIndex = 0;
            part.Color = color;

            ListElement.Add(part);
        }
        SetSprite(ListElement);
    }
    public void SetSprite(List<PreviewMatchingElement> ImageElement)
    {
        var PreviewUnit = PreviewPrefab;
        SaveElementData(ImageElement);

        // 프리뷰 유닛의 매칭 리스트를 가지고 온다.
        var matchingTables = PreviewUnit.GetComponentsInChildren<SPUM_MatchingList>(true);

        var allMatchingElements = matchingTables.SelectMany(mt => mt.matchingTables);
        //Debug.Log(ImageElement.Count + "SetSpriteCount" + allMatchingElements.Count());
        foreach (var matchingElement in allMatchingElements)
        {
            var matchingTypeElement = ImageElement.FirstOrDefault(ie => 
            (ie.UnitType == matchingElement.UnitType)
            && ("Weapons" == matchingElement.PartType)
            //&& ie.Index == matchingElement.Index
            && (ie.Dir == matchingElement.Dir) 
            && (ie.Structure == matchingElement.Structure)
            );
            //Debug.Log(matchingTypeElement != null);
            if (matchingTypeElement != null)
            {
                matchingElement.renderer.sprite = null;
                matchingElement.renderer.maskInteraction = (SpriteMaskInteraction) matchingTypeElement.MaskIndex;
                matchingElement.renderer.color = matchingTypeElement.Color;
                matchingElement.ItemPath = "";
                matchingElement.Color = matchingTypeElement.Color;
            }
        }
        #if UNITY_2023_1_OR_NEWER
            var ItemButtons = FindObjectsByType<SPUM_SpriteButtonST>(FindObjectsSortMode.None);
        #else
            #pragma warning disable CS0618
            var ItemButtons = FindObjectsOfType<SPUM_SpriteButtonST>();
            #pragma warning restore CS0618
        #endif

        foreach (var matchingElement in allMatchingElements)
        {
            var matchingTypeElement = ImageElement.FirstOrDefault(ie => 
            (ie.UnitType == matchingElement.UnitType)
            && (ie.PartType == matchingElement.PartType)
            //&& ie.Index == matchingElement.Index
            && (ie.Dir == matchingElement.Dir) 
            && (ie.Structure == matchingElement.Structure) 
            && (ie.PartSubType == matchingElement.PartSubType)
            //&& !string.IsNullOrEmpty( matchingElement.PartSubType )
            );
            //Debug.Log(matchingTypeElement != null);
            if (matchingTypeElement != null)
            {
                var existingElement = ItemButtons.FirstOrDefault(e => e.PartType == matchingTypeElement.PartType);
                var LoadSprite = LoadSpriteFromMultiple(matchingTypeElement.ItemPath , matchingTypeElement.Structure);
                matchingElement.renderer.sprite = LoadSprite;
                matchingElement.renderer.maskInteraction = (SpriteMaskInteraction) matchingTypeElement.MaskIndex;
                Color PartColor = existingElement.ignoreColorPart.Contains(matchingTypeElement.PartType) ? Color.white : matchingTypeElement.Color;
                matchingElement.renderer.color = PartColor;
                matchingElement.ItemPath = matchingTypeElement.ItemPath;
                matchingElement.Color =  PartColor;
            }
        }
        UIManager.DrawItemOff();
    }

    public void SetPartRandom(SPUM_SpriteButtonST ButtonData)
    {
        var isSpriteFixed = ButtonData.IsSpriteFixed;
        var UnitType = ButtonData.UnitType;
        var PartType = ButtonData.PartType;

        if(isSpriteFixed) return;
        string unitType = UnitType;
        string partType = PartType;

        //패키지 구룹화
        var groupedData = spumPackages
            .SelectMany(p => p.SpumTextureData.Select(t => new { Package = p, Texture = t }))
            .Where(x => x.Texture.PartType.Equals(partType) && x.Texture.UnitType.Equals(unitType))
            .GroupBy(x => new { x.Texture.PartType, x.Texture.Name, x.Texture.UnitType }) // 구룹 키
            .Select(g => new 
            { 
                Key = g.Key, 
                Items = g.ToList() 
            })
            .ToList();
        int randomValue = UnityEngine.Random.Range(0, groupedData.Count+1);

        if(randomValue.Equals(groupedData.Count)) 
        {
            ButtonData.RemoveSprite();
            return;
        }
        var randomGroup = groupedData[randomValue];

        var ListElement = new List<PreviewMatchingElement>();
        foreach (var item in randomGroup.Items)
        {
            //Debug.Log($"Path: {PartType}, SubType: { item.Texture.SubType}");
            var part = new PreviewMatchingElement();
            part.UnitType = UnitType;
            part.PartType = PartType;
            part.PartSubType = item.Texture.PartSubType;
            part.Dir = ButtonData.Direction;
            part.ItemPath = item.Texture.Path;
            part.Structure = item.Texture.SubType.Equals(item.Texture.Name) ? PartType : item.Texture.SubType;
            part.MaskIndex = (int)ButtonData.SpriteMask;
            Color PartColor = ButtonData.ignoreColorPart.Contains(item.Texture.SubType) ? Color.white : ButtonData.InitColor;
            part.Color = PartColor;

            ListElement.Add(part);
        }
        SetSprite(ListElement);
    }
    public void SetDefaultPart(SPUM_SpriteButtonST ButtonData)
    {
        var isSpriteFixed = ButtonData.IsSpriteFixed;
        var IsActive = ButtonData.IsActive;
        var UnitType = ButtonData.UnitType;
        var PartType = ButtonData.PartType;
        var PackageName = ButtonData.DefaultPackageName;
        var TextureName = ButtonData.DefaultTextureName;
        if(isSpriteFixed) return;
        IsActive = true;
        ButtonData.PartSpriteColor = ButtonData.InitColor;
        //Debug.Log(ButtonData.PartType + " " +  ButtonData.InitColor);
        //패키지 구룹화
        var groupedData = spumPackages
            .SelectMany(p => p.SpumTextureData.Select(t => new { Package = p, Texture = t }))
            .Where(x => x.Texture.PartType.Equals(PartType) && x.Texture.UnitType.Equals(UnitType) && x.Package.Name.Equals(PackageName) && x.Texture.Name.Equals(TextureName))
            .GroupBy(x => new { x.Texture.PartType, x.Texture.Name, x.Texture.UnitType }) // 그룹 키
            .Select(g => new 
            { 
                Key = g.Key, 
                Items = g.ToList() 
            })
            .ToList();
        //Debug.Log($"UnitType: {UnitType}, PartType: { PartType}, PackageName: { PackageName}, TextureName: { TextureName} / {groupedData.Count}");
        if (groupedData.Count == 0)
        {
            Debug.LogWarning($"[SPUM] SetDefaultPart: 未找到匹配的纹理数据 - UnitType:{UnitType}, PartType:{PartType}, TextureName:{TextureName}, PackageName:{PackageName}");
            return;
        }
        var randomGroup = groupedData[0];
        var ListElement = new List<PreviewMatchingElement>();
        foreach (var item in randomGroup.Items)
        {
            //Debug.Log($"Path: {PartType}, SubType: { item.Texture.Name}");
            var part = new PreviewMatchingElement();
            part.UnitType = UnitType;
            part.PartType = PartType;
            part.PartSubType = item.Texture.PartSubType;
            part.Dir = ButtonData.Direction;
            part.ItemPath = item.Texture.Path;
            part.Structure = item.Texture.SubType.Equals(item.Texture.Name) ? PartType : item.Texture.SubType;  
            part.MaskIndex = 0;
            //part.Color = ButtonData.InitColor;
            
            Color PartColor = ButtonData.ignoreColorPart.Contains(item.Texture.SubType) ? Color.white : ButtonData.InitColor;
            part.Color = PartColor;

            ListElement.Add(part);
        }
        SetSprite(ListElement);
    }
    public void RemoveSprite(SPUM_SpriteButtonST ButtonData)
    {
        var PreviewUnit = PreviewPrefab;
        var isSpriteFixed = ButtonData.IsSpriteFixed;
        var IsActive = ButtonData.IsActive;
        var UnitType = ButtonData.UnitType;
        var PartType = ButtonData.PartType;

        if(isSpriteFixed) return;
        ButtonData.IsActive = false;
        ButtonData.PartSpriteColor = ButtonData.InitColor;
        var matchingTables = PreviewUnit.GetComponentsInChildren<SPUM_MatchingList>(true);

        var allMatchingElements = matchingTables.SelectMany(mt => mt.matchingTables)
            .Where(element => 
                element.UnitType == UnitType &&
                element.PartType == PartType &&
                element.Dir == ButtonData.Direction
            );
        var ListElement = new List<PreviewMatchingElement>();
        foreach (var matchingElement in allMatchingElements)
        {
            //Debug.Log($"{matchingElement.UnitType} {matchingElement.Structure} {matchingElement.Dir}");
            var part = new PreviewMatchingElement();
            part.UnitType = UnitType;
            part.PartType = PartType;
            part.PartSubType = matchingElement.PartSubType;
            part.Dir = matchingElement.Dir;
            part.Structure = matchingElement.Structure;

            matchingElement.renderer.sprite = null;
            matchingElement.renderer.color = ButtonData.InitColor;
            matchingElement.ItemPath = "";
            matchingElement.Color = ButtonData.InitColor;
            ListElement.Add(part);
        }
        RemoveElementData(ListElement);
    }

    public void SetSpriteColor(SPUM_SpriteButtonST ButtonData)
    {
        var PreviewUnit = PreviewPrefab;
        var isSpriteFixed = ButtonData.IsSpriteFixed;
        var IsActive = ButtonData.IsActive;
        var UnitType = ButtonData.UnitType;
        var PartType = ButtonData.PartType;

        if(isSpriteFixed) return;
        var matchingTables = PreviewUnit.GetComponentsInChildren<SPUM_MatchingList>(true);

        var allMatchingElements = matchingTables.SelectMany(mt => mt.matchingTables)
            .Where(element => 
                element.UnitType == UnitType &&
                element.PartType == PartType &&
                element.Dir == ButtonData.Direction
            );
        var ListElement = new List<PreviewMatchingElement>();
        foreach (var matchingElement in allMatchingElements)
        {
            //Debug.Log($"{matchingElement.UnitType} {matchingElement.Structure} {matchingElement.Dir}");
            var part = new PreviewMatchingElement();
            part.UnitType = UnitType;
            part.PartType = PartType;
            part.Dir = matchingElement.Dir;
            part.Structure = matchingElement.Structure;  
            Color PartColor = ButtonData.ignoreColorPart.Contains(matchingElement.Structure) ? Color.white : ButtonData.PartSpriteColor;
            part.Color = PartColor;
            matchingElement.Color = PartColor;
            matchingElement.renderer.color = PartColor;
            ListElement.Add(part);
        }
        SetElementColorData(ListElement);
    }
    public void SetSpriteVisualMaskIndex(SPUM_SpriteButtonST ButtonData)
    {
        var PreviewUnit = PreviewPrefab;
        var isSpriteFixed = ButtonData.IsSpriteFixed;
        var IsActive = ButtonData.IsActive;
        var UnitType = ButtonData.UnitType;
        var PartType = ButtonData.PartType;

        //if(isSpriteFixed) return;
        var matchingTables = PreviewUnit.GetComponentsInChildren<SPUM_MatchingList>(true);

        var allMatchingElements = matchingTables.SelectMany(mt => mt.matchingTables)
            .Where(element => 
                element.UnitType == UnitType &&
                element.PartType == PartType &&
                element.Dir == ButtonData.Direction
            );
        var ListElement = new List<PreviewMatchingElement>();
        foreach (var matchingElement in allMatchingElements)
        {
            matchingElement.renderer.maskInteraction = ButtonData.SpriteMask;
            var part = new PreviewMatchingElement();
            part.UnitType = UnitType;
            part.PartType = PartType;
            part.Dir = matchingElement.Dir;
            part.Structure = matchingElement.Structure;  
            part.MaskIndex = (int)ButtonData.SpriteMask;
            ListElement.Add(part);
        }
        SetElementMaskData(ListElement);
    }
    public void ItemRandomAll()
    {
        #if UNITY_2023_1_OR_NEWER
            var ItemButtons = FindObjectsByType<SPUM_SpriteButtonST>(FindObjectsSortMode.None);
        #else
            #pragma warning disable CS0618
            var ItemButtons = FindObjectsOfType<SPUM_SpriteButtonST>();
            #pragma warning restore CS0618
        #endif
        List<string> conditionTypes = new List<string> {"Body", "Horse" };

        var filteredButtons = ItemButtons.Where(button => !conditionTypes.Contains(button.ItemShowType)).ToList();
        foreach (var button in filteredButtons)
        {
            button.SetPartRandom();
        }
    }
    public void ItemResetAll()
    {
        //Debug.Log("ItemResetAll");
        #if UNITY_2023_1_OR_NEWER
            var ItemButtons = FindObjectsByType<SPUM_SpriteButtonST>(FindObjectsSortMode.None);
        #else
            #pragma warning disable CS0618
            var ItemButtons = FindObjectsOfType<SPUM_SpriteButtonST>();
            #pragma warning restore CS0618
        #endif
        foreach (var button in ItemButtons)
        {
            button.RemoveSprite();
        }
        
        ResetBody();
    }
    public void ResetBody()
    {
        SetType(PreviewPrefab.UnitType);

        #if UNITY_2023_1_OR_NEWER
            var ItemButtons = FindObjectsByType<SPUM_SpriteButtonST>(FindObjectsSortMode.None);
        #else
            #pragma warning disable CS0618
            var ItemButtons = FindObjectsOfType<SPUM_SpriteButtonST>();
            #pragma warning restore CS0618
        #endif
        List<string> conditionTypes = new List<string> { "Eye" , "Body" };
        if(PreviewPrefab.UnitType.Equals("Horse")){
            conditionTypes.Add("Horse");
        }
        var filteredButtons = ItemButtons.Where(button => conditionTypes.Contains(button.ItemShowType)).ToList();
        foreach (var button in filteredButtons)
        {
            //Debug.Log(button.ItemShowType);
            button.SetInitPart();
        }
    }

    public void ItemLoadButtonActive(List<PreviewMatchingElement> ImageElement)
    {
        string[] uniquePartTypes = ImageElement.Select(m => m.PartType).Distinct().ToArray();
        var partTypeUnitTypeColorDict = ImageElement
                                            .GroupBy(m => new { m.PartType, m.UnitType, m.Dir })
                                            .ToDictionary(g => g.Key, g => g.First().Color);
        #if UNITY_2023_1_OR_NEWER
            var ItemButtons = FindObjectsByType<SPUM_SpriteButtonST>(FindObjectsSortMode.None);
        #else
            #pragma warning disable CS0618
            var ItemButtons = FindObjectsOfType<SPUM_SpriteButtonST>();
            #pragma warning restore CS0618
        #endif

        foreach (var button in ItemButtons)
        {
            var key = new { PartType = button.ItemShowType, UnitType = button.UnitType, Dir = button.Direction };
            
            if (partTypeUnitTypeColorDict.ContainsKey(key))
            {
                button.IsActive = true;
                button.PartSpriteColor = partTypeUnitTypeColorDict[key];
            }
            else
            {
                button.IsActive = false;
            }
        }
    }
    
    public void SetPrefabToPreviewPackageData(List<SpumPackage> packages){
        if(packages == null || packages.Count == 0){
            PreviewPrefab.spumPackages = GetSpumLegacyData();
            Debug.Log($"Using Legacy Data - Loaded { PreviewPrefab.spumPackages.Count } packages / Total Available { spumPackages.Count }");
        }
        //PreviewPrefab.spumPackages = GetSpumLegacyData();
        // else
        // {
        //     PreviewPrefab.spumPackages = packages;
        //     Debug.Log($"Using Provided Data - Loaded {packages.Count} packages / Total Available {spumPackages.Count}");
        // }
        animationManager.PlayFirstAnimation();
    }
    
#endregion
    
#region ElementData
    private bool AreElementsEqual(PreviewMatchingElement element1, PreviewMatchingElement element2)
    {
        return element1.UnitType == element2.UnitType &&
            element1.PartType == element2.PartType &&
            element1.Dir == element2.Dir &&
            element1.Structure == element2.Structure
            ;
        // 필요한 만큼 조건을 추가할 수 있습니다.
    }
    public void SaveElementData(List<PreviewMatchingElement> ElementList)
    {
        var PreviewUnit = PreviewPrefab;
        foreach (var newElement in ElementList)
        {
            var existingElement = PreviewUnit.ImageElement.FirstOrDefault(e => AreElementsEqual(e, newElement));

            if (existingElement != null)
            {
                int index = PreviewUnit.ImageElement.IndexOf(existingElement);
                PreviewUnit.ImageElement[index] = newElement;
            }
            else
            {
                PreviewUnit.ImageElement.Add(newElement);
            }
        }
    }
    public void RemoveElementData(List<PreviewMatchingElement> ElementList)
    {
        var PreviewUnit = PreviewPrefab;
        foreach (var newElement in ElementList)
        {
            var existingElement = PreviewUnit.ImageElement.FirstOrDefault(e => AreElementsEqual(e, newElement));

            if (existingElement != null)
            {
                int index = PreviewUnit.ImageElement.IndexOf(existingElement);
                PreviewUnit.ImageElement.RemoveAt(index);
            }
        }
    }
    public void SetElementColorData(List<PreviewMatchingElement> ElementList)
    {
         var PreviewUnit = PreviewPrefab;
        foreach (var newElement in ElementList)
        {
            var existingElement = PreviewUnit.ImageElement.FirstOrDefault(e => AreElementsEqual(e, newElement));

            if (existingElement != null)
            {
                int index = PreviewUnit.ImageElement.IndexOf(existingElement);
                var ElementData = PreviewUnit.ImageElement[index];
                ElementData.Color = newElement.Color;
            }
        }
    }
    public void SetElementMaskData(List<PreviewMatchingElement> ElementList)
    {
         var PreviewUnit = PreviewPrefab;
        foreach (var newElement in ElementList)
        {
            var existingElement = PreviewUnit.ImageElement.FirstOrDefault(e => AreElementsEqual(e, newElement));

            if (existingElement != null)
            {
                int index = PreviewUnit.ImageElement.IndexOf(existingElement);
                var ElementData = PreviewUnit.ImageElement[index];
                ElementData.MaskIndex = newElement.MaskIndex;
            }
        }
    }

#endregion

#region Prefab


    //프리팹 저장 부분
    public void SavePrefabs()
    {
        animationManager.CloseAnimationPanels();
        var SpumPreviewUnit = PreviewPrefab;
        string prefabName = UIManager._unitCode.text;

        SpumPreviewUnit._code = prefabName;
        //SpumPreviewUnit.EditChk = false;
        var Prefab = fileHandler.Save(SpumPreviewUnit, this);
        
        UIManager.ToastOn("Saved Unit Object " + prefabName);
        
        //paginationManager.AddNewPrefab(Prefab);
        SpumPreviewUnit._code = "";
        UIManager.ResetUniqueID();
        //SpumPreviewUnit.EditChk = true;

        UIManager.ShowNowUnitNumber();
        NewMake();
    }

    //프리펩 수정 부분
    public void EditPrefabs()
    {
        var SpumPreviewUnit = PreviewPrefab;

        string prefabCode = SpumPreviewUnit._code;

        var Prefab = fileHandler.Edit(SpumPreviewUnit, this);

        SpumPreviewUnit._code = "";
        UIManager.ResetUniqueID();

        UIManager.ToastOn("Edited Unit Object Unit" + prefabCode);

        NewMake();
    }

    public void NewMake()
    {
        ItemResetAll();
        EditPrefab = null;
        //UIManager._unitCode.text = UIManager.GetFileName();
        UIManager.LoadButtonSet(false);
        animationManager.CloseAnimationPanels();
        UIManager.CloseColorPick();
        animationManager.InitPreviewUnitPackage();
        ResetBody();
    }
    //프리팹 프리펩 리스트 불러오기
    public void OpenLoadData()
    {
        // 애니메이션 패널 닫기
        animationManager.CloseAnimationPanels();
        animationManager.InitializeDropdown();
        // 로드 오브젝트 캔버스 활성화

        UIManager.SetActiveLoadPanel(true);
        paginationManager.LoadPrefabs();
    }
    public List<PreviewMatchingElement> DebugList = new List<PreviewMatchingElement>();
    public List<string> MissingPackageNames = new List<string>();
    public SPUM_Prefabs previewUnit;
    public void SetUnitConverter(string Type)
    {
        int UnitBodyCount = DebugList.Count(element => element.PartType == "Body");
        if(UnitBodyCount < 6)
        {
            DebugList.AddRange(DefaultData("Unit", "Body", "Human_1", Color.white));
        }
        UIManager.ConvertView.WarningText.SetActive(UnitBodyCount < 6);
        //Debug.Log("UnitBodyCount " + UnitBodyCount);
        int UnitEyeCount = DebugList.Count(element => element.PartType == "Eye");
        UIManager.ConvertView.WarningEyeText.SetActive(UnitEyeCount < 2);
        if(UnitEyeCount < 2){
            DebugList.AddRange(DefaultData("Unit", "Eye", "Eye0", new Color32(71, 26,26, 255)));
        }
        var DistinctPackageList = MissingPackageNames.Distinct().ToList();
        MissingPackageNames = DistinctPackageList;

        UIManager.ConvertView.MissingPackageNames.transform.parent.gameObject.SetActive(MissingPackageNames.Count > 0);
        if(MissingPackageNames.Count > 0){
            string Text = "";
            foreach (var item in MissingPackageNames)
            {
                Text += "\n-" + item ;
            }
           
            string format = $"Missing\nPackages\n--------------{ Text }";
            UIManager.ConvertView.MissingPackageNames.text = format;
        }
        var containUnitTypes = DebugList
        .Select(e => e.UnitType)
        .Distinct()
        .ToList();
        bool shouldActivate = containUnitTypes.Any(unitType => unitType.Contains("Horse"));
        if(shouldActivate) Type = "Horse";
        previewUnit.UnitType = Type;
        foreach (Transform child in previewUnit.transform)
        {
            child.gameObject.SetActive(child.name.Contains(Type));
        }
        var anim = previewUnit.GetComponentInChildren<Animator>();
        previewUnit._anim = anim;

        var matchingTables = previewUnit.GetComponentsInChildren<SPUM_MatchingList>(true);
        var allMatchingElements = matchingTables.SelectMany(mt => mt.matchingTables).ToList();
        foreach (var matchingElement in allMatchingElements)
        {
            if (matchingElement.renderer != null)
            {
                matchingElement.renderer.sprite = null;
                matchingElement.renderer.maskInteraction = SpriteMaskInteraction.None;
                matchingElement.renderer.color = Color.white;
                matchingElement.ItemPath = "";
                matchingElement.Color = Color.white;
            }
        }
        foreach (var matchingElement in allMatchingElements)
        {
            var matchingTypeElement = DebugList.FirstOrDefault(ie => 
            (ie.UnitType == matchingElement.UnitType)
            && (ie.PartType == matchingElement.PartType)
            //&& ie.Index == matchingElement.Index
            && (ie.Dir == matchingElement.Dir)
            && (ie.Structure == matchingElement.Structure) 
            && ie.PartSubType == matchingElement.PartSubType
            );
            //Debug.Log(matchingTypeElement != null);
            if (matchingTypeElement != null)
            {
                var LoadSprite = LoadSpriteFromMultiple(matchingTypeElement.ItemPath , matchingTypeElement.Structure);
                matchingElement.renderer.sprite = LoadSprite;
                matchingElement.renderer.maskInteraction = (SpriteMaskInteraction)matchingTypeElement.MaskIndex;
                matchingElement.renderer.color = matchingTypeElement.Color; 
                matchingElement.ItemPath = matchingTypeElement.ItemPath;
                matchingElement.MaskIndex = matchingTypeElement.MaskIndex;
                matchingElement.Color = matchingTypeElement.Color;
                //Debug.Log( matchingTypeElement.PartType + "/" + matchingTypeElement.Color);
            }
        }


        previewUnit.ImageElement = DebugList;

        
        //애니메이션 경로 체크
        if(previewUnit.spumPackages.Count > 0){
            bool clipPathExists = true;
            var ClipList = previewUnit.spumPackages.SelectMany(package => package.SpumAnimationData).ToList();
            
            foreach (var clip in ClipList)
            {
                clipPathExists = ValidateAnimationClips(clip);
                if(!clipPathExists){
                    // 패키지 네임 // 애니메이션 타입 // 애니메이션 이름
                    var dataArray = clip.ClipPath.Split("/");

                    var PackageDataName = dataArray.Length > 0 ? dataArray[0] : "";
                    if(PackageDataName.Equals("Addons") && dataArray.Length > 1){
                        PackageDataName = dataArray[1];
                    }
                    var PackageNameExist = SpritePackageNameList.Contains(PackageDataName);
                    if(!PackageNameExist)
                    {
                        //Debug.Log("MissingPackage");
                        MissingPackageNames.Add(PackageDataName);
                    }
                    var PackageName = PackageNameExist ? PackageDataName : "";
                    var ClipName = dataArray[dataArray.Length-1];
                    var ExtractList = ExtractTextureData(PackageName, clip.UnitType, clip.StateType, ClipName);
                    var data = ExtractList.FirstOrDefault();
                    if(data != null){
                    Debug.Log($" Package {PackageNameExist} {PackageName} {ClipName} {data.Name} {data.Path}");
                        
                        clip.ClipPath = data.Path;
                    }
                }
            }

        }else{
            //애니메이션 데이터 초기화
            previewUnit.spumPackages = GetSpumLegacyData();
        }
    }
    public bool ValidateAnimationClips(SpumAnimationClip clipData)
    {
        bool clipPathExists = true;
        
        AnimationClip LoadClip = Resources.Load<AnimationClip>(clipData.ClipPath.Replace(".anim", ""));
        
        if (LoadClip == null)
        {
            Debug.LogWarning($"Failed to load animation clip '{clipData.ClipPath}'.");
            clipPathExists = false;
        }
        return clipPathExists;
    }
    
    public SPUM_Prefabs SaveConvertPrefabs(SPUM_Prefabs asset)
    {
        return fileHandler.SaveConvertPrefabs(asset, this);
        // 코어의 수정 작업
    }
    public List<PreviewMatchingElement> SetLegacyHorseData(){
        string PackageName = "Legacy";
        string UnitType = "Horse";
        string PartType = "Body";
        string TextureName = "Horse1";
        //패키지 구룹화
        var groupedData = spumPackages
            .SelectMany(p => p.SpumTextureData.Select(t => new { Package = p, Texture = t }))
            .Where(x => x.Texture.PartType.Equals(PartType) && x.Texture.UnitType.Equals(UnitType) && x.Package.Name.Equals(PackageName) && x.Texture.Name.Equals(TextureName))
            .GroupBy(x => new { x.Texture.PartType, x.Texture.Name, x.Texture.UnitType }) // 그룹 키
            .Select(g => new 
            { 
                Key = g.Key, 
                Items = g.ToList() 
            })
            .ToList();

        if (groupedData.Count == 0)
        {
            Debug.LogWarning($"[SPUM] SetLegacyHorseData: 未找到匹配的纹理数据 - UnitType:Horse, PartType:Body, TextureName:Horse1, PackageName:Legacy");
            return new List<PreviewMatchingElement>();
        }
        var randomGroup = groupedData[0];

        var ListElement = new List<PreviewMatchingElement>();
        foreach (var item in randomGroup.Items)
        {
            //Debug.Log($"Path: {PartType}, SubType: { item.Texture.SubType}");
            var part = new PreviewMatchingElement();
            part.UnitType = UnitType;
            part.PartType = PartType;
            part.PartSubType = item.Texture.PartSubType;
            part.Dir = "";
            part.ItemPath = item.Texture.Path;
            part.Structure = item.Texture.SubType.Equals(item.Texture.Name) ? PartType : item.Texture.SubType;
            part.MaskIndex = 0;
            part.Color = Color.white;

            ListElement.Add(part);
        }
        return ListElement;
    }

    public List<PreviewMatchingElement> DefaultData(string UnitType, string PartType, string TextureName, Color color)
    {
        string PackageName ="Legacy";
        //패키지 구룹화
        var groupedData = spumPackages
            .SelectMany(p => p.SpumTextureData.Select(t => new { Package = p, Texture = t }))
            .Where(x => x.Texture.PartType.Equals(PartType) && x.Texture.UnitType.Equals(UnitType) && x.Package.Name.Equals(PackageName) && x.Texture.Name.Equals(TextureName))
            .GroupBy(x => new { x.Texture.PartType, x.Texture.Name, x.Texture.UnitType }) // 그룹 키
            .Select(g => new 
            { 
                Key = g.Key, 
                Items = g.ToList() 
            })
            .ToList();

        if (groupedData.Count == 0)
        {
            Debug.LogWarning($"[SPUM] DefaultData: 未找到匹配的纹理数据 - UnitType:{UnitType}, PartType:{PartType}, TextureName:{TextureName}, PackageName:Legacy");
            return new List<PreviewMatchingElement>();
        }
        var randomGroup = groupedData[0];

        var ListElement = new List<PreviewMatchingElement>();
        foreach (var item in randomGroup.Items)
        {
            //Debug.Log($"Path: {PartType}, SubType: { item.Texture.SubType}");
            var part = new PreviewMatchingElement();
            part.UnitType = UnitType;
            part.PartType = PartType;
            part.PartSubType = item.Texture.PartSubType;
            part.Dir = "";
            part.ItemPath = item.Texture.Path;
            part.Structure = item.Texture.SubType.Equals(item.Texture.Name) ? PartType : item.Texture.SubType; // item.Texture.SubType.Equals(item.Texture.Name) ? PartType : item.Texture.SubType;
            part.MaskIndex = 0;
            part.Color = color;

            ListElement.Add(part);
        }
        return ListElement;
    }
    public List<PreviewMatchingElement> ReSyncSpumElementDataList(List<PreviewMatchingElement> List)
    {
        // 패키지 이름 존재 여부 2버전
        // 패키지 이름이 없으면 불가능 오브젝트로 이동동
        var ModifiyList = new List<PreviewMatchingElement>();
        foreach (var oldData in List)
        {
            var dataArray = oldData.ItemPath.Split("/");

            var PackageDataName = dataArray.Length > 0 ? dataArray[0] : "";
            if(PackageDataName.Equals("Addons") && dataArray.Length > 1){
                PackageDataName = dataArray[1];
            }
            var PackageNameExist = SpritePackageNameList.Contains(PackageDataName);
            if(!PackageNameExist)
            {
                //Debug.Log("MissingPackage");
                MissingPackageNames.Add(PackageDataName);
            }
            var PackageName = PackageNameExist ? PackageDataName : "";
            var PartName = dataArray[dataArray.Length-1];
            var ExtractList = ExtractTextureData(PackageName, oldData.UnitType, oldData.PartType, PartName);
            var data = ExtractList.FirstOrDefault();
            //Debug.Log($" Package" + oldData.PartType + "/" + PartName);
            if(data != null){
            //Debug.Log($" Package {PackageNameExist} {PackageName} {PartName} {data.Name} {data.Path}");
                if(oldData.PartType.Equals("Weapons"))
                {
                    var PathArray = data.Path.Split("/");
                    string PartType = System.Text.RegularExpressions.Regex.Replace(PathArray[PathArray.Length-2],@"[^a-zA-Z가-힣\s]", "");
                    oldData.PartSubType = PartType;
                }
                
                oldData.ItemPath = data.Path;
                oldData.Color = oldData.Color.Equals(Color.clear) ? Color.white : oldData.Color;
                ModifiyList.Add(oldData);
            }
        }
        return ModifiyList;
    }

    public List<SpumTextureData> ExtractTextureData(string packageName, string unitType, string partType, string textureName)
    {
        var query = spumPackages.AsEnumerable();

        if (!string.IsNullOrEmpty(packageName))
        {
            query = query.Where(package => package.Name == packageName);
        }

        return query
            .SelectMany(package => package.SpumTextureData)
            .Where(texture => 
                texture.UnitType == unitType &&
                texture.PartType == partType &&
                texture.Name == textureName)
            .ToList();
    }
    public List<SpumAnimationClip> ExtractAnimationData(string packageName, string unitType, string partType, string clipeName)
    {
        var query = spumPackages.AsEnumerable();

        if (!string.IsNullOrEmpty(packageName))
        {
            query = query.Where(package => package.Name == packageName);
        }

        return query
            .SelectMany(package => package.SpumAnimationData)
            .Where(clip => 
                clip.UnitType == unitType &&
                clip.StateType == partType &&
                clip.Name == clipeName)
            .ToList();
    }

    //Unit Delete
    public void DeleteUnit(SPUM_Prefabs prefab)
    {
        fileHandler.Delete(prefab);

        UIManager.ShowNowUnitNumber();
        UIManager.SetActiveLoadPanel(false);
        OpenLoadData();
    }
#endregion

    public Sprite LoadSpriteFromMultiple(string path, string spriteName)
    {
        Sprite[] sprites = Resources.LoadAll<Sprite>(path);

        if (sprites == null || sprites.Length == 0)
        {
            Debug.LogWarning($"No sprites found at path: {path}");
            return null;
        }

        Sprite foundSprite = System.Array.Find(sprites, sprite => sprite.name == spriteName);

        // 일치하는 spriteName이 없으면 첫 번째 항목 반환
        return foundSprite != null ? foundSprite : sprites[0];
    }


#region Scene Move

    #if UNITY_EDITOR
    public UnityEditor.SceneAsset ExportScene;
    #endif
    public string sceneName;

    public void LoadExportScene()
    {
        #if UNITY_EDITOR
        if (ExportScene != null)
        {
            string scenePath = UnityEditor.AssetDatabase.GetAssetPath(ExportScene);
            sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
        }
        else
        {
            Debug.LogWarning("ExportScene is not assigned.");
        }
        #endif

        if (!string.IsNullOrEmpty(sceneName))
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }
        else
        {
            Debug.LogWarning("Scene name is not assigned.");
        }
    }

#endregion
}
