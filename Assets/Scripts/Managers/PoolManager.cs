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
        go.SetActive(false);
        go.transform.SetParent(transform);

        // 优先从映射中查找正确的池键（Monster.Init 会改名，导致 go.name 不再是池键）
        string poolKey = null;
        if (_goToPoolKey.TryGetValue(go, out poolKey))
        {
            // 映射命中
        }
        else
        {
            // 兜底：用旧逻辑从名称推断
            poolKey = go.name.Replace("(Clone)", "").Trim();
            Debug.LogWarning($"[PoolManager] 对象映射未命中，使用名称推断池键: go.name={go.name} → poolKey={poolKey}");
        }

        if (!_pool.ContainsKey(poolKey)) _pool[poolKey] = new Queue<GameObject>();
        _pool[poolKey].Enqueue(go);
    }
}
