using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 连杀加速时友军移动残影/幻影（运行时挂载，不改 prefab）。
/// </summary>
public class KillComboAfterimage : MonoBehaviour
{
    UnitBase _unit;
    float _spawnCd;
    int _aliveGhosts;
    static readonly List<SpriteRenderer> SrBuf = new List<SpriteRenderer>(32);

    public static void Ensure(UnitBase unit)
    {
        if (unit == null || !unit.isAlly) return;
        if (unit.GetComponent<KillComboAfterimage>() != null) return;
        var c = unit.gameObject.AddComponent<KillComboAfterimage>();
        c._unit = unit;
    }

    void Awake()
    {
        if (_unit == null)
            _unit = GetComponent<UnitBase>();
    }

    void LateUpdate()
    {
        if (_unit == null || _unit.isDead) return;
        var bm = BattleManager.Instance;
        if (bm == null || !bm.isInBattle) return;

        // 残影只与攻速技能绑定：攻速增益（gale_stance）生效期间才出现，连杀不再单独触发。
        // 门控与 UnitBase.GetAttackSpeed 用同一个标志，保证「有残影 = 正在吃攻速 buff」。
        if (!SkillCastService.IsTeamAttackSpeedBuffActive) return;
        if (_aliveGhosts >= GameConfig.COMBO_AFTERIMAGE_MAX_PER_UNIT) return;

        _spawnCd -= Time.deltaTime;
        if (_spawnCd > 0f) return;

        // 不再随连杀倍率加速：固定间隔，避免攻速 buff 期间刷得比原来更密
        _spawnCd = GameConfig.COMBO_AFTERIMAGE_INTERVAL_SLOW;
        SpawnGhost(GameConfig.COMBO_AFTERIMAGE_ALPHA);
    }

    void SpawnGhost(float alpha)
    {
        SrBuf.Clear();
        var srs = _unit.GetComponentsInChildren<SpriteRenderer>(false);
        for (int i = 0; i < srs.Length; i++)
        {
            var sr = srs[i];
            if (sr == null || !sr.enabled || !sr.gameObject.activeInHierarchy) continue;
            if (sr.sprite == null) continue;
            // 跳过几乎透明 / UI 类
            if (sr.color.a < 0.05f) continue;
            SrBuf.Add(sr);
        }
        if (SrBuf.Count == 0) return;

        var root = new GameObject("ComboAfterimage");
        root.transform.SetParent(null, true);
        if (BattleManager.Instance != null && BattleManager.Instance.unitRoot != null)
            root.transform.SetParent(BattleManager.Instance.unitRoot, true);

        Color tint = new Color(0.75f, 0.9f, 1f, alpha);
        for (int i = 0; i < SrBuf.Count; i++)
        {
            var src = SrBuf[i];
            var go = new GameObject(src.name + "_ghost");
            go.transform.SetParent(root.transform, false);
            go.transform.position = src.transform.position;
            go.transform.rotation = src.transform.rotation;
            go.transform.localScale = src.transform.lossyScale;

            var dst = go.AddComponent<SpriteRenderer>();
            dst.sprite = src.sprite;
            dst.flipX = src.flipX;
            dst.flipY = src.flipY;
            dst.sortingLayerID = src.sortingLayerID;
            dst.sortingOrder = src.sortingOrder - 1;
            dst.sharedMaterial = src.sharedMaterial;
            Color c = src.color;
            c.r = Mathf.Min(1f, c.r * tint.r + 0.1f);
            c.g = Mathf.Min(1f, c.g * tint.g + 0.1f);
            c.b = Mathf.Min(1f, c.b * tint.b + 0.15f);
            c.a = alpha;
            dst.color = c;
        }

        _aliveGhosts++;
        StartCoroutine(CoFadeDestroy(root, alpha));
    }

    IEnumerator CoFadeDestroy(GameObject root, float startAlpha)
    {
        float life = GameConfig.COMBO_AFTERIMAGE_LIFE;
        float t = 0f;
        var srs = root != null ? root.GetComponentsInChildren<SpriteRenderer>() : null;
        while (t < life && root != null)
        {
            t += Time.deltaTime;
            float a = startAlpha * (1f - Mathf.Clamp01(t / life));
            if (srs != null)
            {
                for (int i = 0; i < srs.Length; i++)
                {
                    if (srs[i] == null) continue;
                    Color c = srs[i].color;
                    c.a = a;
                    srs[i].color = c;
                }
            }
            yield return null;
        }
        _aliveGhosts = Mathf.Max(0, _aliveGhosts - 1);
        if (root != null)
            Destroy(root);
    }
}
