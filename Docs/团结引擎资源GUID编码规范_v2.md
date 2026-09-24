# 团结引擎资源 GUID 编码规范 v2

> 适用范围：所有涉及 团结引擎 AssetDatabase 寻址、meta 文件读写、场景/预制体资源引用、AssetBundle 依赖清单、资源加载 API 的 GUID 字符串处理。
> 执行方：全体开发 + WorkBuddy 等 AI 编码助手。AI 生成相关代码前必须先通读本规范第 5、8 节。

## 0. 总则（一句话）

**引擎资源链路只认 hex32；Editor 取 GUID 只用 `UnityEngine.GUID.ToString()`；任何字节级转换必须走 `GuidHelper`；改完必须跑第 7 节自测。**

## 1. 格式定义

| 名称 | 定义 | 示例 |
|---|---|---|
| hex32 | 32 个 `0-9a-f`，无横杠、全小写。资源引用链路**唯一**合法格式 | `4b4f6b7e9a2c48d1b3f5a6e78c9d0e1f` |
| raw16 | 引擎 GUID 的 16 字节原始布局（见 1.1） | — |
| 业务 Base64 | raw16 的标准 Base64（16 字节 → 24 字符，带 `==`） | `S09rXpssSNGz9abnjJ0OHw==` |

### 1.1 引擎字节序（必读）

- `UnityEngine.GUID.ToByteArray()` 返回的 16 字节 = **4 个 uint32 小端**布局。
- hex32 与 raw16 的对应关系：**每 8 个 hex 字符为一组**，按 uint32 解析后**小端**写入 4 字节。
  - 例：hex32 片段 `...f0000000` ↔ 字节 `00 00 00 f0`。
- ⚠️ 因此 raw16 **不能**直接逐字节 hex 编码当 hex32 用（两者每 4 字节组相差反序）。raw16 ↔ hex32 的一切转换必须走 `GuidHelper`。

### 1.2 合法性校验正则

`^[0-9a-f]{32}$`

## 2. 三条铁律

1. 凡进入资源引用链路（AssetDatabase 寻址、meta 读写、场景/预制体引用、AssetBundle 依赖清单、资源加载 API）的 GUID 字符串，必须是 hex32；禁止 Base64、Base32、带横杠格式。
2. Base64 仅允许出现在业务层（网络请求 / 数据库存储）；进入引擎加载链路前**必须**解码转回 hex32。Base64 串永远不得传入资源加载 API。
3. 【引擎资源 GUID】与【业务自定义 ID】是两套独立体系：禁止互转、禁止复用同一字段、禁止混用命名。

## 3. 类型区分（执行时必须严格遵守）

### 3.1 UnityEngine.GUID（团结引擎原生，推荐）

- ✅ `guid.ToString()` → 直接输出 meta 标准 hex32。**Editor 取 GUID 的唯一推荐方式，不要做手动 byte 转换**。
- ⚠️ `guid.ToByteArray()` → raw16（4×uint32 小端，见 1.1）。**禁止**直接 `BitConverter.ToString()` 后当 hex32 用，结果每 4 字节组是反的。

### 3.2 System.Guid（.NET，仅限业务层）

- ✅ `guid.ToString("N")` → hex32。**字符串路径是安全的**，可用于业务层 hex32 表示。
- ❌ `guid.ToByteArray()`：前 8 字节按 **4/2/2 分组反序**、后 8 字节不变，与 raw16 布局不同。**禁止**把其结果送入 `GuidHelper` 或任何引擎 GUID 字节转换函数，否则 hex 错乱、资源无法寻址。
- 备注：.NET 8+ 提供 `guid.ToByteArray(bigEndian: true)`，但团结引擎项目默认不启用 .NET 8，一律按上面的禁用规则执行。

### 3.3 混用禁令

- 引擎资源链路中禁止出现 `System.Guid`。
- 禁止 `new Guid(byte[])` / `new Guid(raw16)` 出现在任何资源导出 / 加载代码中。

## 4. GuidHelper（固定工具，全部代码以此为准）

```csharp
using System;
using System.Linq;
using System.Text;

/// <summary>
/// 团结引擎 GUID 编解码唯一入口。
/// 术语：
///   hex32  —— 32 位小写 hex，无横杠（meta 文件 / 资源引用链路唯一合法格式）
///   raw16  —— UnityEngine.GUID.ToByteArray() 的 16 字节（4×uint32 小端布局）
///   base64 —— raw16 的标准 Base64（业务层网络/数据库专用，禁止进入引擎加载链路）
/// 使用原则：
///   Editor 读取引擎资源 GUID 优先用 UnityEngine.GUID.ToString()，不要强行使用本类；
///   仅在需要解析外部二进制 / Base64 数据时才调用本类。
/// </summary>
public static class GuidHelper
{
    static void ValidateRaw16(byte[] guidBytes)
    {
        if (guidBytes == null || guidBytes.Length != 16)
            throw new ArgumentException("GUID 二进制数据必须为 16 字节", nameof(guidBytes));
    }

    /// <summary>
    /// raw16 → hex32。
    /// 布局规则：每 4 字节一组，按 uint32 小端解析，组内按 %08x 顺序拼接。
    /// 输入必须是 UnityEngine.GUID.ToByteArray() 的结果；
    /// ❌ 禁止传入 System.Guid.ToByteArray() 的结果（其前 8 字节按 4/2/2 分组反序，布局不同）。
    /// </summary>
    public static string GuidBytesToEngineHex32(byte[] guidBytes)
    {
        ValidateRaw16(guidBytes);
        var sb = new StringBuilder(32);
        for (int g = 0; g < 4; g++)
        {
            int o = g * 4;
            // 显式按小端拼 uint32，避免对 BitConverter 机器字节序的隐式依赖
            uint word = (uint)guidBytes[o]
                      | ((uint)guidBytes[o + 1] << 8)
                      | ((uint)guidBytes[o + 2] << 16)
                      | ((uint)guidBytes[o + 3] << 24);
            sb.Append(word.ToString("x8"));
        }
        return sb.ToString();
    }

    /// <summary>
    /// hex32 → raw16（GuidBytesToEngineHex32 的逆运算）。
    /// 输入允许带横杠 / 大写，内部自动规范化；输出始终是引擎 raw16 布局。
    /// </summary>
    public static byte[] Hex32ToGuidBytes(string hex32)
    {
        string normalized = NormalizeEngineHex32(hex32);
        var buffer = new byte[16];
        for (int g = 0; g < 4; g++)
        {
            uint word = Convert.ToUInt32(normalized.Substring(g * 8, 8), 16);
            buffer[g * 4 + 0] = (byte)(word & 0xFF);
            buffer[g * 4 + 1] = (byte)((word >> 8) & 0xFF);
            buffer[g * 4 + 2] = (byte)((word >> 16) & 0xFF);
            buffer[g * 4 + 3] = (byte)((word >> 24) & 0xFF);
        }
        return buffer;
    }

    /// <summary>
    /// 外部输入规范化：去横杠、转小写、逐字符校验。不合法直接抛异常。
    /// 凡进入引擎资源链路的 GUID 字符串，必须先过本方法。
    /// </summary>
    public static string NormalizeEngineHex32(string guid)
    {
        if (string.IsNullOrWhiteSpace(guid))
            throw new ArgumentNullException(nameof(guid));
        string normalized = guid.Trim().Replace("-", "").ToLowerInvariant();
        if (normalized.Length != 32 ||
            !normalized.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
            throw new ArgumentException($"非法引擎 GUID 格式（必须为 hex32）：{guid}");
        return normalized;
    }

    /// <summary>
    /// 【业务层专用】raw16 → Base64（网络请求 / 数据库存储）。
    /// ⚠️ 输出的 Base64 串禁止用于任何引擎资源加载 / 寻址。
    /// </summary>
    public static string GuidBytesToBusinessBase64(byte[] guidBytes)
    {
        ValidateRaw16(guidBytes);
        return Convert.ToBase64String(guidBytes);
    }

    /// <summary>
    /// 【业务层专用】hex32 → Base64 便捷封装（内部先转 raw16）。
    /// </summary>
    public static string EngineHex32ToBusinessBase64(string hex32)
    {
        return GuidBytesToBusinessBase64(Hex32ToGuidBytes(hex32));
    }

    /// <summary>
    /// 【业务层专用】Base64 → hex32。
    /// 解码结果必须是 16 字节，否则视为非法 GUID。
    /// 标准格式为 24 字符且以 "==" 结尾；无 padding 的 22 字符变体也兼容。
    /// </summary>
    public static string BusinessBase64ToEngineHex32(string base64Str)
    {
        if (string.IsNullOrWhiteSpace(base64Str))
            throw new ArgumentNullException(nameof(base64Str));
        string token = base64Str.Trim().Replace('-', '+').Replace('_', '/');
        if (token.Length == 22) token += "==";
        byte[] bytes = Convert.FromBase64String(token);
        if (bytes.Length != 16)
            throw new ArgumentException("Base64 解码结果必须是 16 字节，不是合法 GUID");
        return GuidBytesToEngineHex32(bytes);
    }
}
```

## 5. 写代码硬性约束（WorkBuddy 执行项）

1. Editor 扫描资源、导出资源清单：必须 `AssetDatabase.GUIDFromAssetPath` → `.ToString()`；禁止手动 byte 转换。
2. 仅解析外部二进制 / Base64 数据时才允许调用 `GuidHelper`。
3. 禁止在资源导出逻辑内出现 `Convert.ToBase64String(...)` 处理引擎 GUID。
4. 输出 JSON 资源清单：资源引用字段名统一为 `guid`（hex32）；Base64 只允许出现在单独的业务字段（如 `biz_uid_base64`），且注释标明 `【业务专用，不可用于引擎加载】`。同一字段禁止既存过 hex32 又存过 Base64 / 业务 ID。
5. 禁止新增自定义 ID 并混入 GUID 字段；两套 ID 体系命名必须可区分（如 `guid` vs `biz_uid`）。
6. 禁止 `System.Guid` 与 `UnityEngine.GUID` 混用；禁止 `System.Guid.ToByteArray()` / `new Guid(byte[])` 送入 `GuidHelper`。
7. 所有进入资源链路的 GUID 字符串必须先经 `GuidHelper.NormalizeEngineHex32` 规范化 + 正则校验，不通过则报错中断，不允许静默跳过。
8. 生成 / 修改相关代码时，必须附带第 7 节自测中的对应断言。

## 6. Editor 批量处理脚本要求

### 6.1 疑似 GUID Base64 检测规则

命中**全部**以下条件才告警（24 / 22 字符只是线索，不是判定依据）：

1. 标准格式：长度 24 且以 `==` 结尾；无 padding 变体：长度 22。
2. 字符集限定 `[A-Za-z0-9+/]`（URL-safe 变体 `-` / `_` 先归一化为 `+` / `/` 再判）。
3. 解码后长度**必须恰好为 16 字节**，否则不算。

命中后打 `Warning` 日志，内容包含：配置文件路径、字段名、原串、转换后 hex32。并提供一键批量转换（转换前备份原文件）。

### 6.2 检测参考实现

```csharp
/// <summary>
/// 检测疑似 GUID 的 Base64 串（线索级，非绝对判定）。
/// 标准格式：24 字符 + "==" 结尾；无 padding 变体：22 字符。解码后必须恰好 16 字节。
/// </summary>
public static bool TryParseSuspectedGuidBase64(string token, out string hex32)
{
    hex32 = null;
    if (string.IsNullOrWhiteSpace(token)) return false;

    string s = token.Trim().Replace('-', '+').Replace('_', '/');
    if (s.Length == 22) s += "==";
    if (s.Length != 24 || !s.EndsWith("==", StringComparison.Ordinal)) return false;

    for (int i = 0; i < s.Length; i++)
    {
        char c = s[i];
        bool ok = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')
                  || c == '+' || c == '/' || c == '=';
        if (!ok) return false;
    }

    try
    {
        byte[] bytes = Convert.FromBase64String(s);
        if (bytes.Length != 16) return false;
        hex32 = GuidHelper.GuidBytesToEngineHex32(bytes);
        return true;
    }
    catch (FormatException)
    {
        return false;
    }
}
```

遍历 JSON 配置时对所有字符串字段值执行本检测；命中示例日志：

```csharp
Debug.LogWarning($"[GuidScan] 疑似 GUID Base64：{filePath} 字段 {fieldName}，原值 {token} → hex32 {hex32}");
```

## 7. 自测与回归（强制）

每次修改 `GuidHelper` 或资源导出 / 加载相关逻辑后，必须在 Editor 执行 `Tools → GUID → 编码自测`，全部断言通过方可提交：

```csharp
#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class GuidEncodingSelfTest
{
    [MenuItem("Tools/GUID/编码自测")]
    public static void Run()
    {
        // 用项目内任意真实资源验证；换成本项目存在的路径即可
        const string path = "ProjectSettings/ProjectVersion.txt";
        var guid = AssetDatabase.GUIDFromAssetPath(path);
        string hex = guid.ToString();
        byte[] raw = guid.ToByteArray();

        // ① 核心：raw16 ↔ hex32 往返（验证 4×uint32 小端字节序假设）
        Debug.Assert(hex == GuidHelper.GuidBytesToEngineHex32(raw),
            $"[GuidSelfTest] 字节序假设错误：{hex} != {GuidHelper.GuidBytesToEngineHex32(raw)}");
        Debug.Assert(raw.SequenceEqual(GuidHelper.Hex32ToGuidBytes(hex)),
            "[GuidSelfTest] hex32 -> raw16 往返失败");

        // ② 规范化容错：带横杠、大写输入
        Debug.Assert(GuidHelper.NormalizeEngineHex32(hex.ToUpperInvariant().Insert(8, "-")) == hex,
            "[GuidSelfTest] 规范化失败");

        // ③ 业务 Base64 往返（24 字符、带 ==、可还原 hex32）
        string b64 = GuidHelper.GuidBytesToBusinessBase64(raw);
        Debug.Assert(b64.Length == 24 && b64.EndsWith("=="), "[GuidSelfTest] Base64 长度/padding 异常");
        Debug.Assert(GuidHelper.BusinessBase64ToEngineHex32(b64) == hex, "[GuidSelfTest] Base64 往返失败");

        Debug.Log($"[GuidSelfTest] 全部通过。样本 hex32 = {hex}");
    }
}
#endif
```

若断言 ① 失败：说明当前引擎版本 GUID 字节序与 1.1 节假设不符，**立即上报并冻结相关导出逻辑**，禁止带病上线。

## 8. WorkBuddy 输出前自检清单

生成或修改相关代码后，逐项确认：

- [ ] 代码中所有资源 GUID 均来自 `UnityEngine.GUID.ToString()`（而非 byte 转换）？
- [ ] 是否出现 `System.Guid.ToByteArray()` / `new Guid(byte[])`？→ 出现即必须删除
- [ ] 资源清单的 GUID 字段是否全部通过 `^[0-9a-f]{32}$` 校验？
- [ ] Base64 是否只存在于业务字段，并带 `【业务专用，不可用于引擎加载】` 注释？
- [ ] 两套 ID 体系字段命名是否可区分、无混用？
- [ ] 是否附带了第 7 节对应的断言 / 自测代码？
