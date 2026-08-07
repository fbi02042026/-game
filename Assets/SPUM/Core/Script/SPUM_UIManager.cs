using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if UNITY_6000_0_OR_NEWER
using UnityEngine.InputSystem;
#endif

public class SPUM_UIManager : MonoBehaviour
{
    [Header("▼ Version")] [Space(5)]
    public Text _spumVersion;
    public string SpumUnitPrefix;
    public string UniqueID;
    public Text _unitCode;
    public Text _unitNumber;
    public Text _panelTitle;

    [Header("▼ Toast")] [Space(5)]
    [SerializeField] private CanvasGroup _toastObj;
    [SerializeField] private Text _toastMSG;
    public GameObject _loadObjCanvas;
    public Transform _loadPool;
    public Button CloseLoadPrefabPanelButton;
    private const float FADE_START_TIME = 2.0f;
    private const float TOTAL_DURATION = 3.0f;

    private float toastTimer;
    private bool isToastActive;

    [Header("▼ Package")] [Space(5)]
    public Transform _packageButtonPool;        // 위치 
    public ScrollRect _packageButtonScroll;     // 스크롤뷰
    public GameObject _packageButtonObj;        // 패키지 버튼 프리펩
    public GameObject _childItem;   // 스펌 이미지 아이템 프리펩
    public Transform _childPool;    // 이미지 아이템 보여질 위치
    public Dictionary<string, bool> SpritePackagesFilterList = new Dictionary<string, bool>(); //보여질 패키지 상태 관리
    [Header("▼ Button")] [Space(5)]
    public List<GameObject> _buttonList = new List<GameObject>();
    public Button NewMakeButton;
    public Button DataLoadButton;
    public Button EditButton;
    public Button SaveButton;
    public GameObject _noticeObj;
    public Text _noticeText;
    public List<Button> _buttonSet = new List<Button>();
    public int callbackNum = 0;

    [Header("▼ Charactor View Zoom")] [Space(5)]
    public Transform _characterPivot;
    public Button PlusButton;
    public Button MiunusButton;

    [Header("▼ Color Picker")] [Space(5)]
    public Image _nowColorShow;
    public InputField _hexColorText;
    public List<GameObject> _colorPanelType = new List<GameObject>();
    public List<ColorSelect> _colorSaveList = new List<ColorSelect>();
    public int _nowSelectColorNum;
    public GameObject _nowSelectColor;
    public GameObject _colorPicker;

    public Color _basicColor;
    private Color nowColor;

    public GameObject SpritePanel;
    public Button SpritePanelCloseButton;
    private Texture2D tex;
    public SPUM_SpriteButtonST NowSelectedButton;

    [Header("▼ Convert")] [Space(5)]
    public SPUM_ConvertView ConvertView;

    [Header("▼ Manager")] [Space(5)]
    public SPUM_AnimationManager animationManager;
    public SPUM_Manager spumManager;
    public Color NowColor 
    { 
        get { return nowColor; }
        set
        {
            nowColor = value;
            OnColorChanged(value);
        }
    }
    void OnColorChanged(Color color)
    {
        if (_nowColorShow != null)
            _nowColorShow.color = color;
        if (_hexColorText != null)
            _hexColorText.text = ColorUtility.ToHtmlStringRGB(color);
        if (NowSelectedButton != null)
            NowSelectedButton.PartSpriteColor = color;
    }   
    void Start()
    {
        #if UNITY_6000_0_OR_NEWER
        CheckInputSystemUIModule();
        #endif

        if (NewMakeButton != null)
            NewMakeButton.onClick.AddListener(()=>{ if (spumManager != null) spumManager.NewMake(); });
        if (DataLoadButton != null)
            DataLoadButton.onClick.AddListener(()=>{ if (spumManager != null) spumManager.OpenLoadData(); });
        if (EditButton != null)
            EditButton.onClick.AddListener(()=>{ if (spumManager != null) spumManager.EditPrefabs(); });
        if (SaveButton != null)
            SaveButton.onClick.AddListener(()=>{ if (spumManager != null) spumManager.SavePrefabs(); });
        if (CloseLoadPrefabPanelButton != null)
            CloseLoadPrefabPanelButton.onClick.AddListener(() => { SetActiveLoadPanel(false); });
        
        if (_buttonSet != null && _buttonSet.Count > 0 && _buttonSet[0] != null)
            _buttonSet[0].onClick.AddListener(()=> {});
        if (_buttonSet != null && _buttonSet.Count > 1 && _buttonSet[1] != null)
            _buttonSet[1].onClick.AddListener(()=> {});

        if (PlusButton != null)
            PlusButton.onClick.AddListener(()=> {SetCharPivotSize(0.1f);});
        if (MiunusButton != null)
            MiunusButton.onClick.AddListener(()=> {SetCharPivotSize(-0.1f);});

        if (SpritePanelCloseButton != null)
            SpritePanelCloseButton.onClick.AddListener(()=>{ DrawItemOff(); });

        if (SoonsoonData.Instance._spumManager != null)
        {
            if (_spumVersion != null)
                _spumVersion.text = "VER " + SoonsoonData.Instance._spumManager._version;
            if (ConvertView != null && ConvertView.SPUM_Version != null)
                ConvertView.SPUM_Version.text = $"latest version\nSPUM VERSION {SoonsoonData.Instance._spumManager._version}";
        }
        SpumUnitPrefix = string.IsNullOrWhiteSpace(SpumUnitPrefix) ? "SPUM" : SpumUnitPrefix;
        SetPackageActiveStateList();
        ResetUniqueID();
        ShowNowUnitNumber();
        if (spumManager != null && spumManager.PreviewPrefab != null)
        {
            spumManager.PreviewPrefab._code =  _unitCode.text;
        }
    }
    public void ResetUniqueID() 
    {
        if (_unitCode == null) return;
        UniqueID = System.DateTime.Now.ToString("yyyyMMddHHmmssfff");
        _unitCode.text = SpumUnitPrefix + "_" + UniqueID;
    }
    public void ShowNowUnitNumber()
    {
        if (_unitNumber == null) return;
        var SavedPrefabs = Resources.LoadAll<SPUM_Prefabs>("");
        _unitNumber.text = "All " + SavedPrefabs.Length.ToString("D3") + " Unit";
    }
    void Update()
    {
        if (isToastActive && _toastObj != null)
        {
            toastTimer += Time.deltaTime;
            
            if (toastTimer > FADE_START_TIME)
            {
                _toastObj.alpha = 1.0f - (toastTimer - FADE_START_TIME);
            }
            
            if (toastTimer > TOTAL_DURATION)
            {
                CloseToast();
            }
        }
    }
    public void SetPackageActiveStateList(){{
        if (spumManager == null || spumManager.SpritePackageNameList == null)
        {
            Debug.LogWarning("[SPUM] UIManager: spumManager 或 SpritePackageNameList 为 null，跳过设置包状态列表。");
            return;
        }
        SpritePackagesFilterList = spumManager.SpritePackageNameList.ToDictionary(name => name, name => true);
    }}
    public void SetPackageButtons(SPUM_SpriteButtonST ButtonData)
    {
        var packageList = SpritePackagesFilterList;
        if (_packageButtonPool != null)
        {
            foreach (Transform obj in _packageButtonPool)
            {
                if (obj != null)
                    Destroy(obj.gameObject);
            }
        }

        if (packageList == null || packageList.Count == 0) return;
        if (spumManager == null || spumManager.spumPackages == null) return;
        if (_packageButtonObj == null || _packageButtonPool == null) return;
        
        foreach (var item in packageList)
        {
            bool hasPackageWithPart = spumManager.spumPackages
            .Any(package => 
                package.SpumTextureData.Count > 0 && 
                package.Name == item.Key &&
                package.SpumTextureData.Any(textureData => textureData.PartType == ButtonData.PartType)
            );
            if(!hasPackageWithPart) continue;
            GameObject PackageButtonObject = Instantiate(_packageButtonObj, _packageButtonPool);
            PackageButtonObject.transform.localScale = Vector3.one;
            SPUM_PackageButton PackageButton = PackageButtonObject.GetComponent<SPUM_PackageButton>();
            if (PackageButton != null && PackageButton.PackageToggleButton != null)
                PackageButton.PackageToggleButton.isOn = item.Value;
            if (PackageButton != null)
                PackageButton.SetInit(0, item.Key, this, ButtonData);
        }
    }
    public void ShowItem() 
    {
        if (_packageButtonScroll != null)
            _packageButtonScroll.verticalNormalizedPosition = 1f; 
        ShowItemPanel();
        if (animationManager != null)
            animationManager.CloseAnimationPanels();
    }

    public void ClearPreviewItems(){
        if (_childPool == null) return;
        if(_childPool.childCount > 0)
        {
            for(var i=0; i < _childPool.childCount;i++)
            {
                Destroy(_childPool.GetChild(i).gameObject);
            }
        }
    } 

    public SPUM_PreviewItem CreatePreviewItem(){ 
        if (_childItem == null || _childPool == null) return null;
        GameObject ttObj = Instantiate(_childItem, _childPool);
        ttObj.transform.localScale = new Vector3(1,1,1);
        SPUM_PreviewItem ttObjST = ttObj.GetComponent<SPUM_PreviewItem>();
        return ttObjST;
    }
    public void OnNotice(string text,int type = 0, int callback = -1)
    {
        if (_noticeObj != null)
            _noticeObj.SetActive(true);
        if (_noticeText != null)
            _noticeText.text = text;
        callbackNum = callback;

        if (_buttonSet == null || _buttonSet.Count < 2) return;
        
        if(type == 0 ) //버튼 사용 선택
        {
            if (_buttonSet[0] != null && _buttonSet[0].transform.parent != null)
                _buttonSet[0].transform.parent.gameObject.SetActive(true);
            if (_buttonSet[1] != null && _buttonSet[1].transform.parent != null)
                _buttonSet[1].transform.parent.gameObject.SetActive(false);
        }
        else
        {
            if (_buttonSet[0] != null && _buttonSet[0].transform.parent != null)
                _buttonSet[0].transform.parent.gameObject.SetActive(false);
            if (_buttonSet[1] != null && _buttonSet[1].transform.parent != null)
                _buttonSet[1].transform.parent.gameObject.SetActive(true);
        }
    }

    public void CloseNotice()
    {
        if(callbackNum!=1)CloseOnlyNotice();
        switch(callbackNum)
        {
            case 0:
            break;

            case 1:
            Debug.Log("Please Check Error Message");
            break;
        }
    }

    public void CloseOnlyNotice()
    {
        if (_noticeObj != null)
            _noticeObj.SetActive(false);
    }
    public void LoadButtonSet(bool value)
    {
        if (_buttonList == null || _buttonList.Count < 2) return;
        if (_buttonList[0] != null)
            _buttonList[0].SetActive(!value);
        if (_buttonList[1] != null)
            _buttonList[1].SetActive(value);
    }
    public void ClearChildTransform(){
        if (_loadPool == null) return;
        if(_loadPool.childCount > 0)
        {
            for(var i=0; i < _loadPool.childCount;i++)
            {
                Destroy(_loadPool.GetChild(i).gameObject);
            }
        }
    }
    public void SetActiveLoadPanel(bool isActive)
    {
        if (_loadObjCanvas != null)
            _loadObjCanvas.SetActive(isActive);
    }
    
    // public string GetFileName()
    // {
    //     string tName ="Unit";
    //     int tNameNum = 0;
    //     var _prefabUnitList = SoonsoonData.Instance._spumManager._prefabUnitList;
    //     List<string> _prefabNameList = new List<string>();
    //     for(var i = 0 ; i < _prefabUnitList.Count;i++)
    //     {
    //         _prefabNameList.Add(_prefabUnitList[i].name);
    //     }

    //     for(var i = 0; i < 10000; i++)
    //     {
    //         if(_prefabNameList.Contains(tName+i.ToString("D3")) == false)
    //         {
    //             tNameNum = i;
    //             break;
    //         }
    //     }
        
    //     tName = tName + tNameNum.ToString("D3");
    //     return tName;
    // }
    void CloseToast()
    {
        isToastActive = false;
        toastTimer = 0;
        if (_toastObj != null)
            _toastObj.gameObject.SetActive(false);
    }
    public void ToastOn(string text)
    {
        if (_toastObj == null || _toastMSG == null) return;
        
        // 이전 토스트가 활성화되어 있다면 즉시 종료
        if (isToastActive)
        {
            CloseToast();
        }

        // 새로운 토스트 표시
        _toastObj.gameObject.SetActive(true);
        _toastObj.alpha = 1.0f;
        _toastMSG.text = text;
        toastTimer = 0;
        isToastActive = true;
    }
    public void DrawItemOff()
    {
        if (SpritePanel != null)
            SpritePanel.SetActive(false);
    }
    public void ShowItemPanel()
    {
        if (SpritePanel != null)
            SpritePanel.SetActive(true);
    }
    public void SetCharPivotSize( float num )
    {
        if (_characterPivot == null) return;
        _characterPivot.localScale += new Vector3(num,num,num);

        if( _characterPivot.localScale.x < 0.5f) 
        {
            _characterPivot.localScale = new Vector3(0.5f,0.5f,0.5f);
            ToastOn("Reached Minimum size");
        }
        if(_characterPivot.localScale.x > 1.1f)
        {
            _characterPivot.localScale = new Vector3(1.1f,1.1f,1.1f);
            ToastOn("Reached Maximum size");
        }
    }
    #region ColorPicker Function
    public void DeleteSelectColor()
    {
        if (_nowSelectColor == null || !_nowSelectColor.activeInHierarchy) return;
        if (_colorSaveList != null && _nowSelectColorNum < _colorSaveList.Count 
            && _colorSaveList[_nowSelectColorNum] != null && _colorSaveList[_nowSelectColorNum]._savedColor != null)
        {
            _colorSaveList[_nowSelectColorNum]._savedColor.gameObject.SetActive(false);
        }
        if (SoonsoonData.Instance._soonData2 != null && SoonsoonData.Instance._soonData2._savedColorList != null 
            && _nowSelectColorNum < SoonsoonData.Instance._soonData2._savedColorList.Count)
        {
            SoonsoonData.Instance._soonData2._savedColorList[_nowSelectColorNum] = "";
        }
        _nowSelectColor.SetActive(false);
        SoonsoonData.Instance.SaveData();
    }
    
    public void SetColorPickerPanel(int num)
    {
        if (_colorPanelType == null) return;
        foreach( var obj in _colorPanelType )
        {
            if (obj != null)
                obj.SetActive(false);
        }

        if (num >= 0 && num < _colorPanelType.Count && _colorPanelType[num] != null)
            _colorPanelType[num].SetActive(true);
    }
    public void SetColorButton(SPUM_SpriteButtonST button)
    {
        if (_colorPicker != null)
            _colorPicker.SetActive(true);
        NowSelectedButton = button;
        NowColor =  button.PartSpriteColor;
    }
    public void PickColor()
    {
        tex = new Texture2D(1, 1);
        StartCoroutine(CaptureTempArea());
    }

    IEnumerator CaptureTempArea() {
        yield return new WaitForEndOfFrame();
        
        Vector2 pos;
        #if UNITY_6000_0_OR_NEWER
        pos = Mouse.current.position.ReadValue();
        #else
        pos = Input.mousePosition;
        #endif

        tex.ReadPixels(new Rect(pos.x, pos.y, 1, 1), 0, 0);
        tex.Apply();
        NowColor = tex.GetPixel(0, 0);

        yield return new WaitForSecondsRealtime(0.1f);

        if (_nowColorShow != null)
            _nowColorShow.color = NowColor;
        if (_hexColorText != null)
            _hexColorText.text = ColorUtility.ToHtmlStringRGB(NowColor);
        if (NowSelectedButton != null)
            NowSelectedButton.PartSpriteColor = NowColor;
    }
    public void CloseColorPick()
    {
        if (_colorPicker != null)
            _colorPicker.SetActive(false);
    }
    //#if UNITY_EDITOR
    public void CopyToClipboard()
    {
        if (_hexColorText != null)
            GUIUtility.systemCopyBuffer = _hexColorText.text;
        ToastOn("Copied Color Code");
    }
    #endregion

    #if UNITY_6000_0_OR_NEWER
    void CheckInputSystemUIModule()
    {
        if (EventSystem.current == null)
        {
            Debug.LogWarning("[SPUM] EventSystem not found in scene");
            return;
        }

        var currentModule = EventSystem.current.currentInputModule;
        
        // StandaloneInputModule Remove and InputSystemUIInputModule Add
        if (currentModule == null || currentModule.GetType().Name != "InputSystemUIInputModule")
        {
            // StandaloneInputModule Remove
            var standaloneModule = EventSystem.current.GetComponent<StandaloneInputModule>();
            if (standaloneModule != null)
            {
                DestroyImmediate(standaloneModule);
                Debug.Log("[SPUM] StandaloneInputModule removed");
            }

            // Other InputModules Remove
            if (currentModule != null && currentModule != standaloneModule)
            {
                DestroyImmediate(currentModule);
            }

            // InputSystemUIInputModule Add
            try
            {
                var inputSystemModule = EventSystem.current.gameObject.AddComponent(System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem"));
                Debug.Log("[SPUM] InputSystemUIInputModule added to EventSystem for Unity 6+ compatibility");
                ToastOn("Input System UI Module added");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[SPUM] Failed to add InputSystemUIInputModule: {e.Message}");
                ToastOn("Input System package required for Unity 6+");
                
                // 실패 시 StandaloneInputModule 복원
                EventSystem.current.gameObject.AddComponent<StandaloneInputModule>();
                Debug.Log("[SPUM] StandaloneInputModule restored as fallback");
            }
        }
        else
        {
            Debug.Log("[SPUM] InputSystemUIInputModule already configured");
        }
    }
    #endif

}