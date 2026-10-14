# UI 资源去 GUID 化（方案 A）任务清单

> 主人 2026-10-14 08:45 拍板「选 A，直接改」。目标：让 UI 引用走 Resources 路径，使 56 字符 guid 无害。

## 根因
- 56 字符 guid = 团结 t16 导入时写入 `.meta` 的引擎标识（非项目脚本作祟，已确认 `Assets/Scripts` 无 AssetPostprocessor）。
- **56 字符 guid 资源无法被 prefab 的 `m_Sprite` 引用（不落盘）** → 新图 56 → 界面空图 / 丢资源。
- 结论：问题不在入库，在「56 字符无法按 guid 引用」。

## 已落地文件（本机无编译器，需主人跑编辑器验证）
| 文件 | 作用 |
|---|---|
| `Assets/Scripts/ResSpriteLoader.cs` | 运行时 `Resources.Load<Sprite>(spritePath)` 按路径加载；挂 Image 上 |
| `Assets/Editor/MigrateUiToResources.cs` | 菜单 `Tools/UI/迁移 Sprite 引用到 Resources 路径`：扫描 UI prefab → 图 move 到 `Assets/Resources/UI/Migrated` → 挂 ResSpriteLoader 填路径 → 原 m_Sprite 置空 |

## 使用步骤（主人在编辑器里执行）
1. 完全退出团结编辑器（避免重导覆盖 meta）。
2. 打开工程，等编译通过（若有 CS 报错，把报错发我）。
3. 菜单 `Tools/UI/迁移 Sprite 引用到 Resources 路径` 一键执行。
4. 执行后复跑 `Tools/CheckDanglingRefs.py --strict` + `Tools/EnsureMeta.py` 自检。
5. 进游戏逐页看 UI 是否正常显示（重点：登录、抽卡、天赋、战斗、主城、冒险日志）。

## 验收标准
- [ ] 编译无错
- [ ] UI prefab 的 Image 不再持有 56 字符 guid 的 m_Sprite 引用（改走 ResSpriteLoader）
- [ ] 图已 move 到 `Assets/Resources/UI/Migrated`，原 Art/UI 处已无（无重复内容）
- [ ] 游戏内 UI 图全部正常显示
- [ ] 断链数 0

## 包体说明（主人关切）
- move 不改变资源总量，图还是那些图。
- ⚠️ **Resources 目录所有资源会被无条件打进包**，不能按场景剔除 → 只迁 UI 常驻图，别把关卡 / 怪物图也塞 Resources。
- 后续小游戏包体优化：微信分包（subpackage）、纹理压缩（Android ETC2/ASTC、iOS PVRTC/ASTC）、SpriteAtlas 合图、Max Size 限制、Addressables 按需下载。
- A 本身不增大包体；滥用 Resources 才会。

## 风险
- 🔴 某图被 UI 与非 UI prefab 共用时，move 到 Resources 后非 UI 处会断链。执行前先扫共享引用（`AssetDatabase.GetDependencies` 或全局 grep guid）。
- move 是破坏性操作（原图移走），但 git 可还原；执行前建议 `git status` 确认工作区状态。

## 红线遵守
- 用 **move（移走原图）不是 copy** → 不触发重复内容重签 56（2026-09-27 教训）。
- **不改写 56 字符 guid**（遵守 2026-10-06「56 只报告」铁律）→ A 是绕开 guid，不是改 guid。

## 状态
- 未提交、未推送，等主人在引擎验证。
