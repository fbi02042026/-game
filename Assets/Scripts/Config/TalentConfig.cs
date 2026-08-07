using UnityEngine;

/// <summary>
/// 天赋配置
/// </summary>
[CreateAssetMenu(fileName = "TalentConfig", menuName = "Config/Talent")]
public class TalentConfig : ScriptableObject
{
    public string id;
    public string talentName;
    public string desc;
    public int maxLevel;
    public int costPerLevel;
    public AttrType attrType;
    public float valuePerLevel;
}
