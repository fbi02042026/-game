# 项目协作约定（AI 开工必读 · 跨机一致）

> 本文件随仓库走，**任何一台机器 `git pull` 之后都生效**，用来保证不同机器上执行同一套规范。
> 本地会话记忆在 `.workbuddy/memory/`（被 `.gitignore` 排除，**不跨机同步**），所以跨机规矩一律以本文件为准。
> **本地记忆若与本文件冲突，以本文件为准。**

---

## 1. GUID / 资源编码规范（最高优先）

**权威文档：`Docs/团结引擎资源GUID编码规范_v2.md`（全文必读，尤其第 5、8 节）**
**补充口径：`Docs/56字符Base64处置口径_2026-09-24.md`**（规范未覆盖 56 字符长串，单独立口径）

### 1.1 总则（一句话）

**引擎资源链路只认 hex32；Editor 取 GUID 只用 `UnityEngine.GUID.ToString()`；任何字节级转换必须走 `GuidHelper`；改完必须跑第 7 节自测。**

### 1.2 格式定义

| 名称 | 定义 | 示例 |
|---|---|---|
| hex32 | 32 个 `0-9a-f`，无横杠、全小写。资源引用链路**唯一**合法格式 | `4b4f6b7e9a2c48d1b3f5a6e78c9d0e1f` |
| raw16 | 引擎 GUID 的 16 字节原始布局 | — |
| 业务 Base64 | raw16 的标准 Base64（16 字节 → 24 字符，带 `==`） | `S09rXpssSNGz9abnjJ0OHw==` |

**字节序（必读）**：`UnityEngine.GUID.ToByteArray()` 返回的 16 字节 = **4 个 uint32 小端**布局；hex32 与 raw16 每 8 个 hex 字符为一组，按 uint32 解析后小端写入。
⚠️ 因此 raw16 **不能**直接逐字节 hex 编码当 hex32 用（两者每 4 字节组相差反序），raw16 ↔ hex32 的一切转换必须走 `GuidHelper`。

**合法性校验正则**：`^[0-9a-f]{32}$`

### 1.3 三条铁律

1. 凡进入资源引用链路（AssetDatabase 寻址、meta 读写、场景/预制体引用、AssetBundle 依赖清单、资源加载 API）的 GUID 字符串，必须是 hex32；禁止 Base64、Base32、带横杠格式。
2. Base64 仅允许出现在业务层（网络请求 / 数据库存储）；进入引擎加载链路前**必须**解码转回 hex32。Base64 串永远不得传入资源加载 API。
3. 【引擎资源 GUID】与【业务自定义 ID】是两套独立体系：禁止互转、禁止复用同一字段、禁止混用命名。

### 1.4 类型区分

**`UnityEngine.GUID`（团结引擎原生，推荐）**
- ✅ `guid.ToString()` → 直接输出 meta 标准 hex32。**Editor 取 GUID 的唯一推荐方式，不要做手动 byte 转换**。
- ⚠️ `guid.ToByteArray()` → raw16（4×uint32 小端）。**禁止**直接 `BitConverter.ToString()` 后当 hex32 用（结果每 4 字节组是反的）。

**`System.Guid`（.NET，仅限业务层）**
- ✅ `guid.ToString("N")` → hex32，字符串路径安全，可用于业务层。
- ❌ `guid.ToByteArray()`：前 8 字节按 **4/2/2 分组反序**、后 8 字节不变，与 raw16 布局不同。**禁止**把其结果送入 `GuidHelper` 或任何引擎 GUID 字节转换函数，否则 hex 错乱、资源无法寻址。
- .NET 8+ 的 `guid.ToByteArray(bigEndian: true)` 在团结项目默认不启用，一律按上面的禁用规则执行。

**混用禁令**
- 引擎资源链路中禁止出现 `System.Guid`。
- 禁止 `new Guid(byte[])` / `new Guid(raw16)` 出现在任何资源导出 / 加载代码中。

### 1.5 写代码硬性约束（规范第 5 节）

1. Editor 扫描资源、导出资源清单：必须 `AssetDatabase.GUIDFromAssetPath` → `.ToString()`；禁止手动 byte 转换。
2. 仅解析外部二进制 / Base64 数据时才允许调用 `GuidHelper`。
3. 禁止在资源导出逻辑内出现 `Convert.ToBase64String(...)` 处理引擎 GUID。
4. 输出 JSON 资源清单：资源引用字段名统一为 `guid`（hex32）；Base64 只允许出现在单独的业务字段（如 `biz_uid_base64`），且注释标明 `【业务专用，不可用于引擎加载】`。同一字段禁止既存过 hex32 又存过 Base64 / 业务 ID。
5. 禁止新增自定义 ID 并混入 GUID 字段；两套 ID 体系命名必须可区分（如 `guid` vs `biz_uid`）。
6. 禁止 `System.Guid` 与 `UnityEngine.GUID` 混用；禁止 `System.Guid.ToByteArray()` / `new Guid(byte[])` 送入 `GuidHelper`。
7. 所有进入资源链路的 GUID 字符串必须先经 `GuidHelper.NormalizeEngineHex32` 规范化 + 正则校验，不通过则报错中断，**不允许静默跳过**。
8. 生成 / 修改相关代码时，必须附带第 7 节自测中的对应断言。

### 1.6 56 字符长串（补充口径，详见 `Docs/56字符Base64处置口径_2026-09-24.md`）

- **算术**：56 字符 = 14 组 × 3 字节 → 解码 **40~42 字节**（视 padding），**≠ 16 字节**。
  24（带 `==`）/ 22（无 padding）才解码出 16 字节 → 才是合法 GUID Base64。
- **规范依据**：§6.1 三条检测条件（长度 24/22、字符集、解码恰好 16 字节）必须全部命中才告警转换；**56 字符第一条就失败**。
- **代码行为（防线，不要绕过）**：`TryParseSuspectedGuidBase64` 返回 `false`；强行调 `BusinessBase64ToEngineHex32` 会抛 `ArgumentException("Base64 解码结果必须是 16 字节")`。**不要为了让长度凑数去改 `BusinessBase64ToEngineHex32`。**
- **处置四条**：
  1. 先查来源——大概率根本不是 GUID（可能是 hash、token、拼接串或脏数据），按铁律 3 不得塞进 `guid` 字段；
  2. 扫描脚本中：不转换、不静默跳过，打 Warning/Error 并记录**路径 + 字段名 + 原串**，交人工确认；
  3. 若确认是业务 ID → 放独立业务字段（如 `biz_uid_base64`），注释标明业务专用；
  4. 若怀疑是拼接/截断（56 不能被 24/22 整除，拆不干净）→ 回数据源重新导出，**不要手工切割猜测**。
- **扫描工具**：`Tools/ScanGuid56.py`（**默认只出报告、不写盘**）。
- **本项目实际**：团结会在 `.meta` 的 `guid:` 写入 56 字符资产标识（解码 41 字节，属引擎自有标识而非业务数据）。这类资产实测**拖不进预制体**（Inspector 显示正常、Ctrl+S 也存了，但 `m_Sprite` 不落盘）。2026-09-24 已对 626 个**换发新的合法 hex32 标识**（零引用前提下、主人确认），实测拖图可正常落盘。**以后遇到新的 56 字符：只报告，不自动处理。**

### 1.7 自测与回归（规范第 7 节，强制）

每次修改 `GuidHelper` 或资源导出 / 加载相关逻辑后，必须在 Editor 执行 **`Tools → GUID → 编码自测`**，全部断言通过方可提交。

断言 ①（raw16 ↔ hex32 往返，验证 4×uint32 小端字节序假设）若失败 → **立即上报并冻结相关导出逻辑**，禁止带病上线。

### 1.8 输出前自检清单（规范第 8 节）

- [ ] 代码中所有资源 GUID 均来自 `UnityEngine.GUID.ToString()`（而非 byte 转换）？
- [ ] 是否出现 `System.Guid.ToByteArray()` / `new Guid(byte[])`？→ 出现即必须删除
- [ ] 资源清单的 GUID 字段是否全部通过 `^[0-9a-f]{32}$` 校验？
- [ ] Base64 是否只存在于业务字段，并带 `【业务专用，不可用于引擎加载】` 注释？
- [ ] 两套 ID 体系字段命名是否可区分、无混用？
- [ ] 是否附带了第 7 节对应的断言 / 自测代码？

---

## 2. 开工 / 提交前（每台机器都要做）

1. 提交前必跑 `Tools/CheckDanglingRefs.py --strict` + `Tools/EnsureMeta.py`。
2. **新拖的图必须连同 `.meta` 一起提交**——图不入库，guid 引用就是空中楼阁，换机或重启必空。

## 3. 换机 / 拉取之后

1. `git pull`（本地有改动先提交或 stash）。
2. **必须重启团结引擎**——Library 缓存还按旧 guid 索引，不重启会看到一堆 Missing / 空图。
3. 跑 `Tools/ScanGuid56.py` 看有无新长出的 56 字符 guid（只报告，不自动处理）。

## 4. 工作纪律

- 出错**只报位置不私自改**（路径 + 行号 + 原文 + 判断），等确认后再动手。
- **禁止删掉重建**，只做最小增量；不顺手优化当前没问题的地方。
- 不动美术排版，改前先问；没明确指令不动文件。
- 资源 / 代码 / .meta 全部入库。
- 🚫 不主动提交推送；用户说「提交」= 提交 + 推送，一次做完不再回头问。
- 汇报讲人话，不堆术语。

## 5. 常用工具

| 工具 | 用途 |
|---|---|
| `Tools/ScanGuid56.py` | 扫 56 字符 guid（默认只报告，`--apply` 需人工确认） |
| `Tools/CheckDanglingRefs.py --strict` | 断链检查（prefab 引用了不存在的资源） |
| `Tools/EnsureMeta.py` | 检查资源是否都带 `.meta` |
| `Tools/RewireDailyLoginFull.py` | 每日登录弹窗槽位还原（一次性，已执行） |
