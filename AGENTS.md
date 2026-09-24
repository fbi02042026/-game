# 项目协作约定（AI 开工必读 · 跨机一致）

> 本文件随仓库走，**任何一台机器 `git pull` 之后都生效**，用来保证不同机器上执行同一套规范。
> 本地会话记忆在 `.workbuddy/memory/`（被 `.gitignore` 排除，**不跨机同步**），所以跨机规矩一律以本文件为准。
> **本地记忆若与本文件冲突，以本文件为准。**

## 1. GUID / 资源编码规范

**唯一权威文档：`Docs/团结引擎资源GUID编码规范_v2.md`**。写任何涉及 GUID 的代码前，先通读该文档第 5、8 节。

- 资源引用链路唯一合法格式：`^[0-9a-f]{32}$`（hex32，小写、无横杠）。
- Editor 取 GUID 只用 `UnityEngine.GUID.ToString()`；业务 ID 用 `System.Guid.ToString("N")`。两套 ID 体系禁止互转、禁止混用同一字段。
- 禁止把 `System.Guid.ToByteArray()` / `new Guid(byte[])` 送入引擎资源链路。
- **56 字符 Base64 不是合法 GUID Base64**（解码约 41 字节 ≠ 16 字节，§6.1 第一条就不满足）：
  - **不转换、不静默跳过** → 打 Warning，记录文件路径 + 字段名 + 原串，交人工确认；
  - 禁止为凑长度修改 `BusinessBase64ToEngineHex32`；
  - 确认为业务数据则放独立字段（如 `biz_uid_base64`）并注明【业务专用，不可用于引擎加载】；
  - 扫描用 `Tools/ScanGuid56.py`（**默认只出报告、不写盘**）。
- 团结会持续生成这类 56 字符标识（09-16 清过 582 个，09-24 又长出 626 个）→ 每批资源入库后跑一次扫描看报告即可。

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
