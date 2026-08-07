using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class IPrefabFileHandler : MonoBehaviour, IFileHandler
{
    public void Delete(SPUM_Prefabs prefab)
    {
#if UNITY_EDITOR
        string pathToDelete = AssetDatabase.GetAssetPath(prefab);
        Debug.Log(pathToDelete); 
        AssetDatabase.DeleteAsset(pathToDelete);
#else
        Debug.LogWarning("[SPUM] Delete 仅在 Editor 模式下可用。");
#endif
    }

    public SPUM_Prefabs Edit(SPUM_Prefabs SpumPreviewUnit, SPUM_Manager manager)
    {
#if UNITY_EDITOR
        var SPUM_AnimatorDic = manager.SPUM_AnimatorDic;
        var _version = manager._version;
        var unitPath = manager.unitPath;
        var prefabName = manager.UIManager._unitCode.text;
        var EditPrefab = manager.EditPrefab;
        var isSaveSamePath = manager.isSaveSamePath;

        SPUM_Prefabs PreviewUnit = SpumPreviewUnit.GetComponent<SPUM_Prefabs>();

        SpumPreviewUnit._version = _version;

        GameObject prefabs = Instantiate(SpumPreviewUnit.gameObject);
        SPUM_Prefabs SpumUnitData = prefabs.GetComponent<SPUM_Prefabs>();
        SpumUnitData.ImageElement = SpumPreviewUnit.ImageElement;
        SpumUnitData.spumPackages = SpumPreviewUnit.spumPackages;

        var inactiveObjects = prefabs.transform.Cast<Transform>()
            .Where(child => !child.gameObject.activeInHierarchy)
            .Select(child => child.gameObject)
            .ToList();

        inactiveObjects.ForEach(DestroyImmediate);

        prefabs.transform.localScale = Vector3.one;
        SpumUnitData._anim = prefabs.GetComponentInChildren<Animator>();
        SpumUnitData._anim.runtimeAnimatorController = SPUM_AnimatorDic[SpumPreviewUnit.UnitType];

        var sourcePath = AssetDatabase.GetAssetPath(EditPrefab);
        Debug.Log(sourcePath);
        if(string.IsNullOrWhiteSpace(sourcePath)) 
        {
            sourcePath = Path.Combine(unitPath,SpumUnitData._code );
        }
        var FileName = sourcePath.Split("/");
        var path = isSaveSamePath ? sourcePath.Replace(FileName[FileName.Length-1], "") : unitPath;
        SpumUnitData.PopulateAnimationLists();
        GameObject SavePrefab = PrefabUtility.SaveAsPrefabAsset(prefabs,path+SpumUnitData._code+".prefab");
        DestroyImmediate(prefabs);
        var Prefab = SavePrefab.GetComponent<SPUM_Prefabs>();

        PreviewUnit._code = "";
        return Prefab;
#else
        Debug.LogWarning("[SPUM] Edit 仅在 Editor 模式下可用。");
        return null;
#endif
    }

    public SPUM_Prefabs[] Load()
    {
        return Resources.LoadAll<SPUM_Prefabs>("");
    }

    public SPUM_Prefabs Save(SPUM_Prefabs SpumPreviewUnit, SPUM_Manager manager)
    {
#if UNITY_EDITOR
        var SPUM_AnimatorDic = manager.SPUM_AnimatorDic;
        var _version = manager._version;
        var unitPath = manager.unitPath;
        var prefabName = manager.UIManager._unitCode.text;

        GameObject prefabs = Instantiate(SpumPreviewUnit.gameObject);
        SPUM_Prefabs SpumUnitData = prefabs.GetComponent<SPUM_Prefabs>();
        SpumUnitData.ImageElement = SpumPreviewUnit.ImageElement;
        SpumUnitData.spumPackages = SpumPreviewUnit.spumPackages;
        var inactiveObjects = prefabs.transform.Cast<Transform>()
            .Where(child => !child.gameObject.activeInHierarchy)
            .Select(child => child.gameObject)
            .ToList();

        inactiveObjects.ForEach(DestroyImmediate);
        
        prefabs.transform.localScale = Vector3.one;
        SpumUnitData._anim = prefabs.GetComponentInChildren<Animator>();
        SpumUnitData._anim.runtimeAnimatorController = SPUM_AnimatorDic[SpumPreviewUnit.UnitType];
        SpumUnitData._version = _version;
        SpumUnitData.PopulateAnimationLists();
        if (!Directory.Exists(unitPath))
        {
            Directory.CreateDirectory(unitPath);
            AssetDatabase.Refresh();
            Debug.Log("Folder created at: " + unitPath);
        }  
        GameObject SavePrefab = PrefabUtility.SaveAsPrefabAsset(prefabs,unitPath+prefabName+".prefab");
        DestroyImmediate(prefabs);
        var Prefab = SavePrefab.GetComponent<SPUM_Prefabs>();
        manager.paginationManager.AddNewPrefab(Prefab);
        return Prefab;
#else
        Debug.LogWarning("[SPUM] Save 仅在 Editor 模式下可用。");
        return null;
#endif
    }

    public SPUM_Prefabs SaveConvertPrefabs(SPUM_Prefabs asset, SPUM_Manager manager)
    {
#if UNITY_EDITOR
        var SpumPreviewUnit = manager.PreviewPrefab;
        string prefabName = manager.UIManager._unitCode.text;

        SpumPreviewUnit._code = prefabName;
        
        GameObject prefabs = Instantiate(manager.previewUnit.gameObject);
        SPUM_Prefabs SpumUnitData = prefabs.GetComponent<SPUM_Prefabs>();
        SpumUnitData.ImageElement = manager.DebugList;
        SpumUnitData.spumPackages = SpumPreviewUnit.spumPackages;
        
        prefabs.transform.localScale = Vector3.one;
        prefabs.transform.position = Vector3.zero;
        SpumUnitData._version = manager._version;
        var UniqueID = System.DateTime.Now.ToString("yyyyMMddHHmmssfff");
        SpumUnitData._code = "SPUM" + "_" + UniqueID;
        SpumUnitData._anim.Rebind();
        var sourcePath = AssetDatabase.GetAssetPath(asset);
        var FileName = sourcePath.Split("/");
        var path = manager.isSaveSamePath ? sourcePath.Replace(FileName[FileName.Length-1], "") : manager.unitPath;

        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
            AssetDatabase.Refresh();
        }

        Debug.Log(sourcePath.Replace(asset.name+".prefab", "").Replace(asset.name+".Prefab", ""));
        var prefabFullPath = path + SpumUnitData._code + ".prefab";
        var prefabDir = Path.GetDirectoryName(prefabFullPath);
        if (!Directory.Exists(prefabDir))
        {
            Directory.CreateDirectory(prefabDir);
            AssetDatabase.Refresh();
        }

        Debug.Log(sourcePath.Replace(asset.name+".prefab", "").Replace(asset.name+".Prefab", ""));
        GameObject SavePrefab = PrefabUtility.SaveAsPrefabAsset(prefabs, prefabFullPath);
        DestroyImmediate(prefabs);
        AssetDatabase.Refresh();
        manager.UIManager.ToastOn("Saved Unit Object " + prefabName);
        SpumPreviewUnit._code = "";
        manager.DebugList.Clear();
        MoveOldPrefabBackup(asset, manager);
        var Prefab = SavePrefab.GetComponent<SPUM_Prefabs>();
        Prefab.PopulateAnimationLists();
        return Prefab;
#else
        Debug.LogWarning("[SPUM] SaveConvertPrefabs 仅在 Editor 模式下可用。");
        return null;
#endif
    }

    public void MoveOldPrefabBackup(SPUM_Prefabs asset, SPUM_Manager manager)
    {
#if UNITY_EDITOR
        var sourcePath = AssetDatabase.GetAssetPath(asset);
        if (!Directory.Exists(manager.unitBackUpPath))
        {
            Directory.CreateDirectory(manager.unitBackUpPath);
            AssetDatabase.Refresh();
            Debug.Log("Folder created at: " + manager.unitBackUpPath);
        }  
        var destinationPath = manager.unitBackUpPath+asset.name+"_Backup.Prefab";
        AssetDatabase.MoveAsset(sourcePath, destinationPath);
        AssetDatabase.Refresh();
#else
        Debug.LogWarning("[SPUM] MoveOldPrefabBackup 仅在 Editor 模式下可用。");
#endif
    }

    public (int, List<PreviewMatchingElement>) ValidateSpumFile(SPUM_Prefabs PrefabObject, SPUM_Manager manager)
    {
        var SpumPrefab = PrefabObject;
        var version = SpumPrefab._version;
        var UnitType =  SpumPrefab.UnitType;
        var MatchingList = SpumPrefab.GetComponentsInChildren<SPUM_MatchingList>();
        bool isMatchingListExist = MatchingList != null || MatchingList.Length > 0;
        bool isVersionSame = SpumPrefab._version == version;
        var NewDataListElement = new List<PreviewMatchingElement>();
        var OldData = SpumPrefab.GetComponentInChildren<SPUM_SpriteList>();
        if(OldData == null) {
            return (2, PrefabObject.ImageElement);
        }
        var horseString = OldData._spHorseString;

#if UNITY_EDITOR
        var path = AssetDatabase.GetAssetPath(PrefabObject);
        Debug.Log(path);
#endif
        bool HorseExist = !string.IsNullOrWhiteSpace(horseString);
        if(HorseExist){
            var horseReset = manager.SetLegacyHorseData();
            NewDataListElement.AddRange(horseReset);
        }

        string Unitype = "Unit";

        var hairString = OldData._hairListString;
        var hairList = OldData._hairList;
        var TuppleHair = CreateTupleList(hairString, hairList);
        var MaskSet = new List<PreviewMatchingElement>();
        foreach (var tuple in TuppleHair)
        {
            MaskSet.AddRange(StringToSpumElementList(Unitype, tuple, manager));
        }
        List<string> requiredPartTypes = new List<string> { "Hair", "Helmet"};
        bool result = requiredPartTypes.All(partType => MaskSet.Any(element => element.PartType == partType));
        if(result) 
        {
            foreach (var item in MaskSet)
            {
                if(item.PartType.Equals("Hair")) item.MaskIndex = 1;
            }
        }

        NewDataListElement.AddRange(MaskSet);
        var clothString = OldData._clothListString;
        var clothList = OldData._clothList;
        var TuppleCloth = CreateTupleList(clothString, clothList);
        foreach (var tuple in TuppleCloth)
        {
            NewDataListElement.AddRange(StringToSpumElementList(Unitype, tuple, manager));
        }

        var armorString = OldData._armorListString;
        var armorList = OldData._armorList;
        var TuppleArmor = CreateTupleList(armorString, armorList);
        foreach (var tuple in TuppleArmor)
        {
            NewDataListElement.AddRange(StringToSpumElementList(Unitype, tuple, manager));
        }

        var pantString = OldData._pantListString;
        var pantList = OldData._pantList;
        var TupplePant = CreateTupleList(pantString, pantList);
        foreach (var tuple in TupplePant)
        {
            NewDataListElement.AddRange(StringToSpumElementList(Unitype, tuple, manager));
        }

        var weaponString = OldData._weaponListString;
        var weaponList = OldData._weaponList;
        var TuppleWeapon = CreateTupleList(weaponString, weaponList);
        foreach (var tuple in TuppleWeapon)
        {
            var WeaponsData = StringToSpumElementList(Unitype, tuple, manager);
            NewDataListElement.AddRange(WeaponsData);
        }

        var backString = OldData._backListString;
        var backList = OldData._backList;
        var TuppleBack = CreateTupleList(backString, backList);
        foreach (var tuple in TuppleBack)
        {
            NewDataListElement.AddRange(StringToSpumElementList(Unitype, tuple, manager));
        }

        var bodyString = OldData._bodyString;
        var bodyList = OldData._bodyList;
        var BodySet = new List<PreviewMatchingElement>();
        foreach (var renderer in bodyList)
        {
            BodySet.AddRange(StringToSpumElementList(Unitype, (bodyString, renderer), manager));
        }
        NewDataListElement.AddRange(BodySet);

        var eyeString = "";
        var eyeList = OldData._eyeList; 
        var EyeColorSet = new List<PreviewMatchingElement>();
        foreach (var renderer in eyeList)
        {
            EyeColorSet.AddRange(StringToSpumElementList(Unitype, (eyeString, renderer), manager));
        }
    
        var EyeDistict = EyeColorSet.Distinct().GroupBy(x => new { x.Structure }).Select(g => g.First()).ToList();
        foreach (var item in EyeDistict)
        {
            foreach (var sprite in eyeList)
            {
                if(sprite.name.Equals(item.Structure)) 
                { 
                    item.Color = sprite.color; 
                }
            }
        }
        NewDataListElement.AddRange(EyeDistict);
        
        var distinct = NewDataListElement.Distinct()
        .GroupBy(x => new { x.UnitType, x.PartType, x.Structure, x.Dir })
            .Select(g => g.First())
            .ToList();
        return (1, distinct);
    }

    public List<PreviewMatchingElement> StringToSpumElementList(string UnitType, (string, SpriteRenderer) Tuple, SPUM_Manager manager)
    {
        var PartPath = Tuple.Item1;
        string unitType = UnitType;
        string PackageName = "Legacy";
        string pattern = @"Packages\/([^\/]+)\/";
        bool isPackage = false;
        
        System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(PartPath, pattern);
        if (match.Success)
        {
            PackageName = match.Groups[1].Value;
            isPackage = true;
        }
        if(PackageName.Equals("Heroes")) PackageName = "RetroHeroes";
        bool missingPackage = isPackage && !manager.SpritePackageNameList.Contains(PackageName);
        if(missingPackage)
        {
            manager.MissingPackageNames.Add(PackageName);
        }

        if( ((PartPath == "") && (Tuple.Item2.sprite != null)) || missingPackage){
#if UNITY_EDITOR
            var path = AssetDatabase.GetAssetPath(Tuple.Item2.sprite);
            PartPath = path;
            string pattern2 = @"Addons\/(.*?)\/0_Unit";
            System.Text.RegularExpressions.Match match2 = System.Text.RegularExpressions.Regex.Match(PartPath, pattern2);
            if (match2.Success)
            {
                PackageName = match2.Groups[1].Value;
            }
#else
            PartPath = Tuple.Item2.sprite.name;
#endif
        }
        if(string.IsNullOrWhiteSpace(PartPath)) return new List<PreviewMatchingElement>();

        var PathArray =  PartPath.Split("/");
        string PartType = System.Text.RegularExpressions.Regex.Replace(PathArray[PathArray.Length-2],@"[^a-zA-Z가-힣\s]", "");
        string NoNamePackagePartType = System.Text.RegularExpressions.Regex.Replace(PathArray[PathArray.Length-3],@"[^a-zA-Z가-힣\s]", "");
        PartType = PartPath.Contains("BodySource") ? "Body" : NoNamePackagePartType.Equals("Weapons") ? "Weapons" : PartType;

        string PartName = System.Text.RegularExpressions.Regex.Replace(PathArray[PathArray.Length-1], @"\..*", "");
        if(NoNamePackagePartType.Equals("BasicResources")) 
        {
            PartType = PartType.Replace("Backup", "");
        }
        var dir = "";
        bool isHide = false;
        if(PartType.Equals("Helmet"))
        {
            if(Tuple.Item2.name == "12_Helmet2") { dir = "Front"; isHide = Tuple.Item1 == ""; }
            if(Tuple.Item2.name == "11_Helmet1") { dir = "Front"; isHide = Tuple.Item1 == ""; }
        }

        if(PartType.Equals("Weapons"))
        {
            if(Tuple.Item2.name == "R_Weapon") { dir = "Right"; isHide = Tuple.Item1 == ""; }
            if(Tuple.Item2.name == "R_Shield") { dir = "Right"; isHide = Tuple.Item1 == ""; }
            if(Tuple.Item2.name == "L_Weapon") { dir = "Left";  isHide = Tuple.Item1 == ""; }
            if(Tuple.Item2.name == "L_Shield") { dir = "Left";  isHide = Tuple.Item1 == ""; }
        }

        var ExtractList = ExtractTextureData(PackageName, unitType, PartType, PartName, manager);
        
        var ListElement = new List<PreviewMatchingElement>();
        foreach (var item in ExtractList)
        {
            var part = new PreviewMatchingElement();
            part.UnitType = UnitType;
            part.PartType = PartType;
            part.PartSubType = item.PartSubType;
            part.Dir = dir;
            part.ItemPath =  isHide ? "" : item.Path;
            part.Structure = item.SubType.Equals(item.Name) ? PartType : item.SubType;
            part.MaskIndex = 0;
            part.Color = Tuple.Item2.color;

            ListElement.Add(part);
        }
        return ListElement;
    }

    public List<SpumTextureData> ExtractTextureData(string packageName, string unitType, string partType, string textureName, SPUM_Manager manager)
    {
        var query = manager.spumPackages.AsEnumerable();

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

    List<(string, SpriteRenderer)> CreateTupleList(List<string> stringList, List<SpriteRenderer> spriteRendererList)
    {
        int minLength = Mathf.Min(stringList.Count, spriteRendererList.Count);

        return stringList.Take(minLength)
                         .Zip(spriteRendererList.Take(minLength), (s, sr) => (s, sr))
                         .ToList();
    }
}
