using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 简易对象池，所有怪物/特效/子弹统一走池子，避免GC卡顿
/// </summary>
public class PoolManager : Singleton<PoolManager>
{
    [HideInInspector] public GameObject _monsterPrefab;
    private Dictionary<string, Queue<GameObject>> _pool = new Dictionary<string, Queue<GameObject>>();
    private Dictionary<string, GameObject> _prefabDict = new Dictionary<string, GameObject>();
    /// <summary>GameObject → 池键映射，解决 Monster.Init() 改名导致 Release 时池键不匹配的问题</summary>
    private Dictionary<GameObject, string> _goToPoolKey = new Dictionary<GameObject, string>();

    /// <summary>
    /// 初始化预加载
    /// </summary>
    public void Preload(string poolKey, GameObject prefab, int count)
    {
        _prefabDict[poolKey] = prefab;
        if (!_pool.ContainsKey(poolKey)) _pool[poolKey] = new Queue<GameObject>();
        for (int i = 0; i < count; i++)
        {
            GameObject go = Instantiate(prefab, transform);
            go.SetActive(false);
            _pool[poolKey].Enqueue(go);
            _goToPoolKey[go] = poolKey;
        }
    }

    /// <summary>
    /// 在已有预制体登记后补足池内空闲数量，减轻首波 Instantiate 卡顿。
    /// </summary>
    public void Warm(string poolKey, int targetIdleCount)
    {
        if (!_prefabDict.ContainsKey(poolKey) || _prefabDict[poolKey] == null) return;
        if (!_pool.ContainsKey(poolKey)) _pool[poolKey] = new Queue<GameObject>();
        int need = targetIdleCount - _pool[poolKey].Count;
        for (int i = 0; i < need; i++)
        {
            GameObject go = Instantiate(_prefabDict[poolKey], transform);
            go.SetActive(false);
            _pool[poolKey].Enqueue(go);
            _goToPoolKey[go] = poolKey;
        }
    }

    /// <summary>
    /// 池外 Instantiate 的对象登记到指定池键，保证 Release 时不按改名误入错池。
    /// </summary>
    public void RegisterExternal(GameObject go, string poolKey)
    {
        if (go == null || string.IsNullOrEmpty(poolKey)) return;
        _goToPoolKey[go] = poolKey;
        if (!_pool.ContainsKey(poolKey))
            _pool[poolKey] = new Queue<GameObject>();
        if (!_prefabDict.ContainsKey(poolKey) && _monsterPrefab != null && poolKey == "Monster")
            _prefabDict[poolKey] = _monsterPrefab;
    }

    /// <summary>
    /// 从池子取对象
    /// </summary>
    public GameObject Get(string poolKey, Vector3 pos = default, Quaternion rot = default)
    {
        if (!_pool.ContainsKey(poolKey) || _pool[poolKey].Count == 0)
        {
            if (!_prefabDict.ContainsKey(poolKey))
            {
                Debug.LogError("池子不存在预制体：" + poolKey);
                return null;
            }
            GameObject newGo = Instantiate(_prefabDict[poolKey], transform);
            newGo.SetActive(false);
            _pool[poolKey].Enqueue(newGo);
            _goToPoolKey[newGo] = poolKey;
        }

        GameObject go = _pool[poolKey].Dequeue();
        GameConfig.SetWorldPosition(go, pos);
        go.transform.rotation = rot;
        go.SetActive(true);
        // 确保映射存在（Get 时再次确认，防止 Preload 时遗漏）
        if (!_goToPoolKey.ContainsKey(go))
            _goToPoolKey[go] = poolKey;
        return go;
    }

    /// <summary>
    /// 放回池子
    /// </summary>
    public void Release(GameObject go)
    {
        if (go == null) return;

        // 回收前尽量清掉战斗委托，避免下次 Get 叠订阅
        var unit = go.GetComponent<UnitBase>();
        if (unit != null)
            unit.ResetForReuse();

        go.SetActive(false);
        go.transform.SetParent(transform);

        string poolKey = null;
        if (!_goToPoolKey.TryGetValue(go, out poolKey) || string.IsNullOrEmpty(poolKey))
        {
            // 兜底：怪物统一回 Monster 池，禁止用改名后的 go.name 开新池
            if (go.GetComponent<Monster>() != null)
                poolKey = "Monster";
            else
                poolKey = go.name.Replace("(Clone)", "").Trim();
            Debug.LogWarning($"[PoolManager] 对象映射未命中，回退池键: go.name={go.name} → poolKey={poolKey}");
            _goToPoolKey[go] = poolKey;
        }

        if (!_pool.ContainsKey(poolKey)) _pool[poolKey] = new Queue<GameObject>();
        _pool[poolKey].Enqueue(go);
    }
}
