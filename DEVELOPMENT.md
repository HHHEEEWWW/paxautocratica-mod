# PaxAutocraticaHelper 开发手册

> 《Pax Autocratica》（暗星铁律）BepInEx 6（IL2CPP）辅助插件 —— 士兵管理面板 + 快捷命令。
> 本文档是**唯一的开发入口**：架构、模块、工具链、游戏内部 API、坑与约定都在这。改代码前先读这里。
> 本手册会随开发**持续更新**（见文末「维护本手册」）。

- 版本：v0.5.6（git HEAD；**已部署到本地档案**于 2026-08-19，旧 0.5.5 已迁入 `<BepInEx>\plugins-disabled\PaxAutocraticaHelper-retired-0.5.5\`）
- 插件 GUID：`com.hhewww.paxautocraticahelper`
- 目标框架：net6.0，C# latest，nullable enabled
- 仓库根：`E:\deepseekharness\BeplnEx-mod-workplace\paxautocratica-mod`

---

## 1. 这是什么

一个运行在游戏内的 IMGUI 便利面板 + 快捷键集合：

- **F1 面板只做士兵管理**（列表 / 属性编辑 / 复制士兵 / 应用属性 / 复制时补选词条）
- 其余功能全部走快捷键（时间倍速 / 完成研究 / 自动保存 / 作弊等），减少误触
- 所有数值与开关走 BepInEx 配置（管理器可表单化编辑）

设计原则（历史迭代沉淀，**不要再打破**）：

1. 面板“小而专”，能不放的就不放；
2. 复制/应用等有副作用操作**不给快捷键**，只在面板按钮触发（v0.5.3 起）；
3. 作弊按钮与快捷键统一受「显示作弊功能」开关（`CheatSectionVisible`）控制；
4. 输入一律用 `InputLegacyModule`（旧 Input），不依赖 `Unity.InputSystem` interop（v0.3.0 起）；
5. 每帧驱动由自注册的 Il2Cpp MonoBehaviour（`PanelBehaviour.Update`）完成，**不挂 Harmony 帧钩子**。

---

## 2. 环境与路径（重要）

| 项 | 路径 |
|---|---|
| 游戏目录 | `E:\steam\steamapps\common\Pax Autocratica` |
| 游戏引擎 | Unity 6000.0.37（IL2CPP），`GameAssembly.dll` 在游戏根 |
| 活动 BepInEx 树 | `E:\trainer\BepInExManager\data\plugin-library\pax-autocratica-1f70\pmsu8pzzmf4bg\BepInEx` |
| BepInEx 模式 | **BepInEx-Manager 隔离模式**：游戏目录里**没有** BepInEx 文件夹；`doorstop_config.ini` 的 `target_assembly=` 指向活动档案的 preloader |
| 插件部署目录 | `<BepInEx>\plugins\PaxAutocraticaHelper\`（build.ps1 自动处理） |
| 插件配置 | `<BepInEx>\config\com.hhewww.paxautocraticahelper.cfg` |
| 运行日志 | `<BepInEx>\LogOutput.log` / `ErrorLog.log` |
| dotnet SDK | 9.0.317（`dotnet --list-sdks` 确认） |

> 隔离模式是 v0.5.2 引入的能力：build.ps1 读 `doorstop_config.ini` → `target_assembly` → 反推 `plugin-library\<gameRoot>\<profileId>\BepInEx` 为 `BepDir`。若只在自己的电脑跑，这条链不用改；**换机器/换档案后路径不同**，优先改游戏目录、让它自动解析。

---

## 3. 快速上手（最常见操作）

```powershell
# 构建 + 部署到活动档案（自动备份旧 dll 为 .bak，重启游戏生效）
.\build.ps1
```

- 只编译不部署（改 BepDir 手动指，验证编译用）：
  ```powershell
  dotnet build .\src\PaxAutocraticaHelper\PaxAutocraticaHelper.csproj -c Release `
    -p:BepDir='E:\trainer\BepInExManager\data\plugin-library\pax-autocratica-1f70\pmsu8pzzmf4bg\BepInEx'
  ```
- 部署输出：`<BepInEx>\plugins\PaxAutocraticaHelper\PaxAutocraticaHelper.dll`（产物在 `src\...\bin\Release\net6.0\`）
- **验证**：重启游戏 → 看 `LogOutput.log`：
  - `PaxAutocraticaHelper loading.` → 加载成功
  - `Harmony patches applied.` → 补丁生效
  - `PanelBehaviour registered in Il2Cpp.` → 面板宿主就绪
  - 游戏内 F1 开面板。
- 升级后若配置面板残留旧条目（如老的机器人相关键）：删 cfg 重启即重建。

**常见故障**：

| 现象 | 处理 |
|---|---|
| 面板不显示 | 「启用便利面板」=true；等「显示延迟」后按 F1；看 OnGui 相关报错（已限频 10s 一条） |
| 作弊按钮/快捷键无效 | 「显示作弊功能」=true |
| AddPeople/命令失败 | `ConsoleLineHelper.Self` 为空 = 控制台未就绪，进主城后重试 |
| 部署报错 / dll 被锁 | 游戏还在运行，先关游戏 |
| 两个版本都加载 | 删旧 dll 与 .bak |

---

## 4. 架构总览

```
BasePlugin (PaxPlugin)
 ├─ ModConfig.Init(Config)            # 全部 BepInEx 配置项
 ├─ Harmony.PatchAll                  # 见 §6 Harmony 补丁清单
 └─ ClassInjector.RegisterTypeInIl2Cpp<PanelBehaviour> + AddComponent
       └─ PanelBehaviour (MonoBehaviour)
             ├─ Update() → FrameHook.Update()      # 每帧：延迟词条 + 列表刷新 + 轮询 + 自动分配 + 按键
             └─ OnGUI() → GuiHook.DrawPanel()      # IMGUI 面板绘制（读 SoldierManager 状态）
核心静态服务（游戏内所有模块都是 static class，无实例）：
  SoldierManager      # 士兵列表/选择/复制/应用属性/词条应用（核心，见 §5.5）
  GuiHook             # 面板 UI（读 SoldierManager 的文本框状态写回）
  FrameHook           # 定时与快捷键
  NpcAutoAssign       # 自动分配
  CheatConsoleExecutor# 执行游戏 CheatConsole 命令（反射 + MethodInfo 缓存）
  AffixFilter         # 特质名过滤（复制时只留正面战斗类）
  AffixCatalog        # 词条库（WIP，复制时补选词条用）
```

数据流要点：

- **士兵列表**：`SoldierManager.SoldierEntries`（只读列表）← `FrameHook` 按「士兵列表刷新间隔」后台调用 `RefreshSoldierList()`；枚举 `NpcSimulatorManager.NpcAttributeDic`（两条路径见 §5.5）。
- **当前选中**：`CurrentDetailNpcId` + `CurrentDetailNpc`；由 4 个 Harmony 补丁 + 轮询 + 面板点击共同驱动；选中后 `SyncPanelFromCurrent()` 把属性灌进面板文本框。
- **编辑**：面板文本框（`*Text` 静态字段）→「应用属性」→ `ApplyAttributes()` → 写 `Attribute<int>` 值 → `OnNpcBehaviourChanged?.Invoke(id)` 通知游戏。
- **复制**：`CopyCurrentSoldier()` → `AddPeople` 命令 → 同步 `TryCaptureNewNpc()`（id > 复制前 max 即新兵）→ 兜底 `AddNpcAttribute` Postfix → `CopyAttributes`（过滤特质）→（WIP）`ApplySelectedAffix`。

---

## 5. 模块详解

### 5.1 Plugin.cs（入口）

- `[BepInPlugin("com.hhewww.paxautocraticahelper", ..., "0.5.6")]`，**版本号改这里，README/记忆要同步**。
- `internal static bool ShowWindow`：面板可见性（F1 切换）。
- 加载顺序固定：配置 → Harmony → 注册 PanelBehaviour。

### 5.2 ModConfig.cs（配置）

- 全部 `ConfigEntry`，节：通用 / 面板 / 自动行为 / 作弊。见 §7 配置表。
- `CheatSectionVisible` 是作弊总开关（面板按钮 + Ctrl+7/8/9 都受它控制）。

### 5.3 FrameHook.cs（每帧驱动）

`PanelBehaviour.Update` 每帧调用 `FrameHook.Update()`，顺序固定：

1. `SoldierManager.ProcessPendingAffix()` —— 延迟词条应用（WIP，见 §5.5）
2. `SoldierManager.RefreshSoldierList()` —— 列表后台刷新（与面板显示状态无关，独立冷却）
3. 士兵轮询 `PollCurrentSoldier()`（默认 0.5s）
4. 全局自动分配 `NpcAutoAssign.AutoAssignAll()`（默认 60s）
5. 显示延迟过后处理按键：F1 面板开关（打开即 `ForceListRefresh`）；Ctrl+1~0 快捷键

坑：

- 快捷键只认 Ctrl 组合（`Input.GetKey(LeftControl/RightControl)`），F1 单独。
- 复制/应用属性**没有**快捷键（v0.5.3 删的，防误触），只能面板按钮。
- 整体 try/catch，出错日志只打第一条（`_kbReady` 闸，防刷屏）。

### 5.4 GuiHook.cs（IMGUI 面板）

- `DrawPanel()` 由 `PanelBehaviour.OnGUI` 每帧调用；`PanelEnabled`/`ShowWindow`/显示延迟三重闸。
- **缩放**：`GUI.matrix = Matrix4x4.Scale(scale)`；注意**拖动标题栏时鼠标坐标换算**（§8 坑）。
- 不透明背景垫底（`PanelBgTexture`，1×1 纹理拉伸），不再透游戏画面。
- 布局：快捷键说明 → 士兵列表（120px 滚动）→ 当前详情 → 属性输入框（20 个：等级/经验 + 体力/饱食/心情 + 支持/恐惧/金币 + 工资 + 10 种工作速度，含「采摘速度」）→ 复制/应用按钮 →（WIP）词条选择区 → 状态行。
- OnGui 异常日志已限频（10s 一条），防止每帧刷屏。

### 5.5 SoldierManager.cs（核心）

**列表** `RefreshSoldierList()`
- 冷却 = `SoldierListRefreshInterval`（默认 2s；`ForceListRefresh()` 置零 → 复制完成/开面板时立即刷新）。
- 过滤 `value.IsDead`；计数在诊断日志（30s 限频）`[Soldier] 列表刷新: 新增/已过滤死亡/总数`。
- 枚举 `NpcAttributeDic` 两条路径：
  - **路径 1**：`((Il2CppObjectBase)dic).Cast<Il2CppSystem.Collections.Generic.Dictionary<long, NpcAttribute>>()`，走 Il2Cpp 枚举器（常规，快）。
  - **路径 2（兜底）**：属性被游戏更新成 `IReadOnlyDictionary` 壳时，反射 `.Values` 枚举（`EnumerateNpcsReflect`，支持 KeyValuePair 包装与 `NpcAttribute` 直取两种形态）。
- 当前选中从字典消失（死亡/流放）→ 清空详情并提示。

**选择与跟随**
- `SelectFromList(id)` / `SelectById(id)` → `TryGetNpcData(id, out npc, true)` → `SyncPanelFromCurrent()`。
- 4 个 Harmony 补丁保持“游戏里看谁面板就同步谁”（UIManagerSoldier.OnConfirmSoldier、UIPopupSoldierDetail.SetContent、UIManagerSoldier.UpdateContent、UIPopupSoldierDetail.OnShow——后两个用 `Traverse` 读私有字段 `m_npcAttribute` / `m_managerData`）。
- 轮询 `PollCurrentSoldier()` 兜底（FindObjectOfType 详情弹窗 / 管理页）。

**复制士兵**`CopyCurrentSoldier()`
- 前提：已在面板打开一个未死亡士兵详情。
- 记录 `_maxIdBeforeCopy = GetMaxNpcId()` → `RunCommand("AddPeople", { EfasItem, GenderType, Age, 1 })`。
- 同步命中：`TryCaptureNewNpc()` 扫字典找 `id > max && id != src.Id`（多半命中）；命中即 `CopyAttributes` +（WIP）`ApplySelectedAffix` + `OnNpcBehaviourChanged` + `ForceListRefresh`。
- 兜底：`AddNpcAttribute` Postfix（`OnNpcAttributeAdded`），3s 超时取消；只认 id>max 的目标，避免误吞自然生成 NPC（v0.5.2 修复的竞态）。
- **特质过滤**：`CopyAttributes` 逐个 `AffixList` id → `AffixFilter.GetAffixName(id)`：
  - 查不到名（配置表未加载）→ 保留原特质（降级安全，不破坏复制）；
  - 查到且是正面战斗类 → 保留；否则剔除并打日志。
- **复制只影响属性副本，不改源**；新兵由游戏 `AddPeople` 生成、再被我们“属性覆盖”。

**应用属性** `ApplyAttributes()`
- 20 个文本框逐项 `ApplyIfParsedUInt/Int` 解析后写 `Attribute<int>.Value`（空/解析失败跳过）。
- 写完全部后 `OnNpcBehaviourChanged?.Invoke(id)` + 状态提示。

**词条应用（WIP，未完成）** —— 见 §9。涉及：
- `SelectedAffix`（面板选择）、`ApplySelectedAffix`（排队延迟 3s，等新兵初始化）→ 执行官方命令 `AddAffixToAllSoldier {affix.Id,1}`（注：**对全体士兵生效**，目前注释即“含新兵”）+ **同步实验2**：`DestroyNpcRuntimeData(npc)` + `CreateNpcAffixData(npc)` 强制重建运行时数据。
- 还有一批**观察 Hook**（`OnNpcAffixDataRefresh`/`OnCreateNpcAffixData`/`OnRandomSoldierAffixInvoke`/`OnAffixItemCreated`）和 `[AffixDump]` 诊断 dump（`DumpAffixState`/`DumpFirstNpcs`），是排查 8/15 更新后“特质运行时生成、存档 afx=null”的探针 —— **这些是调试期代码，发布前要清理**。

### 5.6 NpcAutoAssign.cs

- 触发游戏全局分配：`NpcEnvironmentManager.OnAutoAssignPeople?.Invoke()`；null（未进主城）→ 报错返回。日志 30s 限频。

### 5.7 CheatConsoleExecutor.cs（命令执行）

- `RunCommand(methodName, args)`：
  1. `ConsoleLineHelper.Self` 为空 → 失败。
  2. 在 `Self.Dics` 里找 `Name` 匹配 `"CheatConsole."+methodName`（兜底精确名）的 `MethodTarget.obj`。
  3. `ResolveMethod`（按方法名+参数个数反射，MethodInfo 缓存到 `_cache`）。
  4. **必须 `rawObj.Cast<CheatConsole>()` 再 `method.Invoke(console, ...)`**，否则 `TargetException: Object does not match target type`（v0.4.0 关键修复）。
  5. `ConvertArgs` 自动类型强转（bool/int/float/string 互通，`Coerce`）。
- `Exec(command)` 字符串入口：`SetTimeScale <n>` 特殊处理（同时设 `GameTimeManager.TimeScale`/`SettingsTimeScale`/`Time.timeScale`）；其余空格拆分后 `RunCommand(方法名, 参数串)`。限频：**相同命令 0.3s 内重复忽略**（原版行为），不同命令不限（v0.5.2 修过连按被吞）。

### 5.8 AffixFilter.cs（特质过滤）

- 正面战斗类白名单模式 `PositiveCombatPatterns`（24 个 `CORPSAFFIX_*` 名称片段，取自存档实测 123 个名称全集）。
- `IsPositiveCombatAffix(name)`：不区分大小写包含匹配。
- `GetAffixName(id)`：从 `DataObjNpcAffix` 配置表（`Resources.FindObjectsOfTypeAll`）读 `NpcAffix` 的 `m_affixId`/`m_localKey` 建缓存（id→枚举名）；配置表延迟加载（进主城后 Addressables），每 30s 重试；缓存建好前返回 null → 调用方降级保留原特质。

### 5.9 AffixCatalog.cs（词条库，WIP）

- `AffixEntry { Id, Name(枚举名), DisplayName(中文), IsCombat }`。
- `Entries` 收录精选正面词条：战斗类（射击伤害/攻击提升/攻击速度/移速/各类伤害/护盾/生命/换弹切枪/喷气背包/不死等）与生活类（农业专家/医疗专家/建造大师/工作狂/小胃口）。
- `ByCategory(bool? combat)` 过滤。
- id 与 `EFAS_ITEM` 枚举一致（111001…119001），新增词条前用 `ItemNameLookup`/`TypeExplorerPax` 核对枚举值。

---

## 6. Harmony 补丁清单（PatchAll 自动装配）

| 目标 | 补丁 | 作用 |
|---|---|---|
| `UIManagerSoldier.OnConfirmSoldier(int, GridItemInfo)` | Postfix `OnSoldierConfirm` | 士兵列表确认选中 → 同步面板 |
| `UIPopupSoldierDetail.SetContent(UIPopupSoldierDetailData)` | Postfix `OnDetailSetContent` | 详情弹窗数据 → 同步面板 |
| `UIManagerSoldier.UpdateContent(UISoldierManagerData)` | Postfix `OnSoldierManagerUpdate` | 管理页更新（`ViewDetailNpcId`）→ 同步 |
| `UIPopupSoldierDetail.OnShow()` | Postfix `OnDetailShow` | 弹窗显示，Traverse 读私有 `m_npcAttribute` → 同步 |
| `NpcSimulatorManager.AddNpcAttribute(NpcAttribute)` | Postfix `OnNpcAttributeAdded` | 复制士兵兜底捕获新兵并写属性 |
| `NpcAffixData.RefreshAffix(NpcAttribute)` | Postfix `OnNpcAffixDataRefresh` | ⚠️ WIP 观察探针（[AffixDump]） |
| `NpcSimulatorManager.CreateNpcAffixData(NpcAttribute)` | Postfix `OnCreateNpcAffixData` | ⚠️ WIP 观察探针（[AffixDump]） |
| `DelegateWarp.FuncWarp<long,uint,EFAS_ITEM>.Invoke` | Postfix `OnRandomSoldierAffixInvoke` | ⚠️ WIP 观察探针（随机词条 id dump） |
| `NpcAffixItem.OnCreate(NpcAffixData,NpcAttribute,UUID)` | Postfix `OnAffixItemCreated` | ⚠️ WIP 观察探针（[AffixDump]） |

> `PatchAll(typeof(PaxPlugin).Assembly)` 自动扫全部 `[HarmonyPatch]`。**WIP 探针补丁在发布前务必移除/收敛**（每条 Postfix 都会在对应游戏调用点触发日志）。

---

## 7. 配置项（cfg 键与默认值）

| 节 | 键 | 默认 | 说明 |
|---|---|---|---|
| 通用 | 启用便利面板 | true | F1 面板总开关 |
| 通用 | 显示作弊功能 | false | 开则面板与 Ctrl+7/8/9 生效 |
| 面板 | 界面缩放 | 2 | GUI.matrix 缩放 |
| 面板 | 位置 X / 位置 Y | 20 / 60 | 左上角（GUI 空间坐标，拖动后自动反写） |
| 面板 | 宽度 / 高度 | 330 / 470 | 缩放前逻辑像素 |
| 面板 | 显示延迟(秒) | 8 | 进游戏后多久可显示面板 |
| 自动行为 | 自动分配间隔(秒) | 60 | 0=关闭 |
| 自动行为 | 士兵轮询间隔(秒) | 0.5 | 面板同步频率 |
| 自动行为 | 士兵列表刷新间隔(秒) | 2 | 列表冷却 |
| 作弊 | God Mode / Daddy Mode / 停止敌军AI / 免费制造 / 解锁平民 / 可拆所有建筑 | 全 false | 面板显示对应作弊按钮（受「显示作弊功能」门控） |

> 作弊节里的键目前仅作面板按钮/快捷命令的门控配置；面板里对应按钮在「显示作弊功能」开启后由 GuiHook 按 `Cheat` 前缀开关显示（见 README）。改键名要同步 `ModConfig.cs` 与清理旧 cfg。

---

## 8. 快捷键

| 快捷键 | 功能 | 说明 |
|---|---|---|
| F1 | 开关面板 | 打开即强制刷新列表 |
| Ctrl+1/2/3/4 | 时间 2x/5x/10x/1x | 走 SetTimeScale 特殊路径（GameTime+Unity 双时间） |
| Ctrl+5 | 完成所有研究 | `CompleteAllResearching` |
| Ctrl+6 | 自动保存 | `AutoSave` |
| Ctrl+7/8/9 | God/Daddy/免费制造 | **受「显示作弊功能」开关门控**（`MaybeCheat`，10s 限频拒绝日志） |
| Ctrl+0 | 智能自动分配 | 直接 `AutoAssignAll` |

复制士兵 / 应用属性：**无快捷键**（只走面板按钮）。

---

## 9. 当前进行中的工作（重要）

工作区有一批**未提交**改动，是「复制时补选/应用词条」功能的探路版（截至接手时状态）：

| 文件 | 状态 | 内容 |
|---|---|---|
| `AffixCatalog.cs` | 未跟踪（新） | 精选词条库（战斗/生活） |
| `GuiHook.cs` | 已改 | 面板新增词条选择区（不添加/战斗/生活 + 滚动列表 + 高亮当前选择） |
| `SoldierManager.cs` | 已改 | `SelectedAffix`/`ApplySelectedAffix`/`ProcessPendingAffix` + 词条 dump/观察 Hook + 复制链挂上词条应用 |
| `FrameHook.cs` | 已改 | 每帧调 `ProcessPendingAffix` |
| `PaxAutocraticaHelper.csproj` | 已改 | 新增 EFAS.Config 引用 |

**风险提示**：

- `ProcessPendingAffix` 里的「同步实验2」`DestroyNpcRuntimeData + CreateNpcAffixData` 会**强制销毁并重建 NPC 运行时词条数据**，属于实验性强力手段，可能影响存档/预算/NPC 状态 —— 结论未定，**不要把当前工作区当发布版直接 build.ps1 部署**。
- `AddAffixToAllSoldier {id,1}` 作用对象是**全体士兵**（命令语义如此），与“给单个新兵加词条”的目标存在偏差；需要改造成单兵接口（游戏侧若没有，就要 Harmony 注入或构造单兵词条数据）。
- 一票 `[AffixDump]` / 观察 Hook 是排查 8/15 更新后“特质运行时生成、存档 `afx=null`”用的探针，发布前清理。

**下一步候选（待用户确认优先级）**：见交付说明末尾的建议。原则上与用户对齐后再动这份 WIP。

---

## 10. 构建与部署管线（build.ps1）

1. 解析 BepDir：`doorstop_config.ini` 存在且 `target_assembly` 指向 `plugin-library` → 反推档案 BepInEx 目录；否则退化为游戏目录 `BepInEx`。找不到核心 dll → 报错退出。
2. `dotnet build -c Release -p:BepDir=...`（csproj 默认 BepDir 是游戏目录 fallback）。
3. 备份旧插件为 `.dll.bak` → 复制新 dll → **SHA256 校验**（不一致即失败退出）。
4. 部署被锁（游戏运行中）→ 明确 HINT 提示关游戏。

> 产物名恒为 `PaxAutocraticaHelper.dll`（AssemblyName）。历史上有过带版本后缀的手动部署（`PaxAutocraticaHelper-0.5.5.dll`），**不要混用**：同一目录里同名/异名多版本会让 BepInEx 重复加载或老版本残留不生效。清理原则 = 目录里只留一个当前版 dll（+ 可选 .bak）；旧版 dll 移入 `plugins-disabled\`（BepInEx 不会加载该目录）留档即可，如 `plugins-disabled\PaxAutocraticaHelper-retired-0.5.5\`。

---

## 11. 工具链与游戏更新应对（tools/，gitignore 不入库）

用于**游戏更新后重生成 interop**，以及日常查游戏内部结构。整条链：

```
GameAssembly.dll + global-metadata.dat
      │ Cpp2IL / Cpp2ILScan (dump)
      ▼
dummy C# 程序集 (tools/gen/dummy) + decompiled 源码 (tools/gen/decomp-*)
      │ Il2CppInterop.Generator 1.5.3 (+ 自研 InteropGen 包装)
      ▼
interop 程序集 → 拷到 <活动档案 BepInEx>\interop\   （csproj HintPath 全指这里）
      │ HashGen（复刻 BepInEx6 ComputeHash，interop 缓存键）
      ▼
插件引用 interop 编译部署；运行时泛化委托由 Il2CppInterop 负责
```

| 工具 | 路径 | 用途 / 用法 |
|---|---|---|
| Cpp2IL | `tools\cpp2il-1706`（含 Cpp2IL-src 全源码） | IL2CPP → C# dummy/decomp 主程序 |
| Cpp2ILScan | `tools\Cpp2ILScan`（自研包装，`--dump`/`--members`/`--dumpdummy`） | 调 Cpp2IL 输出类型/member/dummy |
| Il2CppInterop.Generator | `tools\il2cppinterop.generator.1.5.3`（gen-decomp 为反编译源码） | dummy → tagged interop |
| InteropGen | `tools\InteropGen`（自研，`<dummyDir> <unityLibsDir> <outDir>`） | 包装 Generator.Runners 批量产出 interop |
| HashGen | `tools\HashGen`（`<GameAssembly.dll> <unityLibsDir>`） | 复刻 `Il2CppInteropManager.ComputeHash`，生成缓存键 |
| MetaRegScan | `tools\MetaRegScan`（`<GameAssembly.dll> <global-metadata.dat>`） | 扫 TypeDefinitionCount / PE 段 / 方法注册（构建 method↔token 映射用） |
| ItemNameLookup | `tools\ItemNameLookup`（`<dll>`） | 在 interop 里找 `EFAS_ITEM` 枚举（核对词条/物品 id） |
| SaveDump | `tools\SaveDump`（`<saveFile> [collection] [maxDocs]`） | LiteDB 存档浏览器（游戏存档是 LiteDB） |
| TypeExplorerPax | `tools\TypeExplorerPax`（`<k1,k2,...> [--methods] [--fields]`） | interop/cīr 全程序集断词搜索类型，**自动解析隔离档案 interop**（可用作每日查 API 的首选工具） |

> `tools/gen/interop` 存有全套 tagged interop；`tools/gen/decomp-npc`、`tools/gen/decomp-npc2`、`tools/gen/decomp-cheat` 是对应重点程序集的 decompiled 参考源码（npc2 是最细/最新的一份，npc 与 cheat 按需对照）。游戏更新 → 整条链重跑 → 用 `HashGen` 刷新缓存键，否则运行时会因 `global-metadata` 哈希变化而重建 interop。decomp 目录主要在 `tools/gen/*` 下，**不要用工具的临时输出直接当插件引用**。

---

## 12. 游戏内部 API 参考（interop 实测签名）

> 首次核对这些签名用的是 `TypeExplorerPax`，来源 = 活动档案 `BepInEx\interop`。

**NPC 核心**
- `NpcSimulatorManager`（`Efas.NpcSimulator.dll`，`Multiverse.EFAS.NpcSimulator`）
  - `static IReadOnlyDictionary<long, NpcAttribute> NpcAttributeDic` —— 插件 Cast 成 Il2Cpp 字典枚举（path1），游戏更新后可能退化为只读壳（path2 反射兜底）
  - `static bool TryGetNpcData(long _npcId, out NpcAttribute _npcAttribute, bool _logError = true)`
  - `static void AddNpcAttribute(NpcAttribute)`（可 Postfix 捕获新兵）
  - `static void CreateNpcAffixData(NpcAttribute)`（**private** 静态，Harmony 仍可打）
  - `static void DestroyNpcRuntimeData(NpcAttribute)`（public）
  - `static ActionWarp<long> OnNpcBehaviourChanged`
- `NpcEnvironmentManager.OnAutoAssignPeople`：`static ActionWarp`（null=未进主城）
- `NpcAttribute`：`Name/Level/Exp/Id/IsDead/EfasItem/GenderType/Age` + `Attribute<int>` 型 `Stamina/Fullness/Mood/Support/Fear/Wages/Gold/CraftSpeed/CollectSpeed/ResearchSpeed/PlantingSpeed/ProduceSpeed/CarrySpeed/GatherFoodSpeed/LoggingSpeed/CookingSpeed/BreedingSpeed` + `Il2CppSystem.Collections.Generic.List<int> AffixList` + `NpcAffixData NpcAffixData`
- `NpcAffixData`：`SpeedMul/WorkEfficiency/CombatFatality/Restless`、`List<NpcAffixItem> m_affixItems`、`RefreshAffix(NpcAttribute)`、`OnCreate(UUID, NpcAttribute)`、`OnAffixItemCreated`
- `NpcAffixItem`：`NpcAffixData m_affixData`、`EFAS_ITEM m_affixSourceEfasItem`、`NpcAttribute m_npcAttribute`、`OnCreate(NpcAffixData, NpcAttribute, UUID)`、`OnAffixLevelChanged(uint)`

**作弊控制台**
- `ConsoleLineHelper`（`GlobalStatics.dll`，`Multiverse.Console`）：`static Self`、`Dictionary Dics`（每条 `MethodTarget` 含 `Name`/`obj`）、`methodDic`、`Register/Commit`
- `CheatConsole`（MonoBehaviour）：目标命令方法都在它上面（`SetTimeScale`/`CompleteAllResearching`/`AutoSave`/`TestGod`/`TestDaddy`/`CraftNoConsume`/`AddPeople(NpcItem, Gender, Age, count)`/`AddAffixToAllSoldier(id, lv)`…）；执行必须 `Cast<CheatConsole>` 再 Invoke

**UI（士兵管理）**
- `UIManagerSoldier`（`EFAS.UIFramework.dll`）：`OnConfirmSoldier(int, GridItemInfo)`、`UpdateContent(UISoldierManagerData)`、`m_managerData`
- `UIPopupSoldierDetail`：`SetContent(UIPopupSoldierDetailData)`、`OnShow()`、私有字段 `m_npcAttribute`
- `UISoldierManagerData.ViewDetailNpcId`、`UIPopupSoldierDetailData.ItemInfo.CharacterId`、`GridItemInfo`

**特质配置表**
- `DataObjNpcAffix`（`EFAS.Utils.dll` / `EFAS.EFAS_DATA`）：`GetNpcAffixList() → List<NpcAffix>`，条目含 `m_affixId`(int)、`m_localKey`(字符串 = `CORPSAFFIX_*` 枚举名)；`DataObjNpcAffixLv` 提供等级数据。配置表 Addressables 延迟加载 → `AffixFilter` 30s 重试。

**游戏/版本**
- Unity 6000.0.37；`GameAssembly.dll` 要配 `global-metadata.dat`（就在 `<GameDir>\Pax Autocratica_Data\il2cpp_data\Metadata`，Cpp2IL 用）。

---

## 13. 已知坑与经验（踩过的都在这）

1. **CheatConsole 反射 TargetException**：找 `Dics` 的 target 后必须 `Cast<CheatConsole>()` 才 Invoke（v0.4.0）。
2. **命令限频**：相同命令 0.3s 限频是原版行为；**不同命令不能**合流限频，否则连按被吞（v0.5.2 修）。
3. **输入**：不要用 `Unity.InputSystem` interop（缺失/不稳定），用 `Input.GetKey*`（InputLegacyModule，csproj 已引用对应 module）。
4. **不要挂 Harmony 帧钩子**：每帧逻辑集中到 `PanelBehaviour.Update`（自注册 Il2Cpp MonoBehaviour），更稳（v0.3.0 定）。
5. **IMGUI 缩放下的坐标**：`GUI.matrix` 缩放后，`Event.mousePosition` 与面板 `Rect` 都是 **GUI 空间**（=屏幕/scale），直接比较；拖动时要 `Input.mousePosition`（屏幕左下原点）转 GUI 空间 `(x/scale, (Screen.height-y)/scale)` 再换算，且 `newY` 上界 `Screen.height/scale - TitleBarHeight`（v0.5.6 标题栏拖动）。位置写回 `ModConfig.PanelX/Y.Value`（自动持久化）。
6. **NpcAttributeDic 有两条枚举路径**：新架构下属性可能返回 `IReadOnlyDictionary` 壳，直接 Cast Il2Cpp 字典会 null → 反射枚举 Values 兜底（v0.5.6）。
7. **复制竞态**：新兵识别 = `id > 复制前 max`；不要用职业/性别匹配，也别过早捕获未初始化对象（v0.5.2/0.5.3）。`AddPeople` 同步路径几乎总能命中；`AddNpcAttribute` Postfix 只是兜底。
8. **特质过滤降级**：配置表未加载时保留原特质（不破坏复制）；加载后只留正面战斗类（`AffixFilter.PositiveCombatPatterns` 24 模式）。过滤逻辑基于 `NpcAffix.m_localKey`（枚举名）。
9. **8/15 游戏更新**：NPC 特质改运行时生成、存档 `afx=null`，导致直接用 `AffixList` 读写词条不再可靠 → 观察 Hook + `[AffixDump]` 探针排查中（WIP，见 §9）。
10. **面板异常日志限频**：`OnGui` 每帧调用，错误 10s 只记一条；FrameHook 同理（`_kbReady`）。诊断类日志也尽量限频（30s）。
11. **可空性警告**：ModConfig 与 static 字段大量 CS8618（null! 初始化），当前是警告不是错误，改配置时新字段记得处理。
12. **隔离档案部署**：同一档案目录里只留一个当前版 dll（+ .bak），文件名固定 `PaxAutocraticaHelper.dll`；别用手动带版本后缀的旧文件（会造成双加载/残留）。
13. **代码/LF 规范化**：git 在提交时会提示 LF→CRLF，正常现象（core.autocrlf）；提交时留意别把 `bin/obj`、`tools/` 带进去（`.gitignore` 已含）。

---

## 14. 代码约定与版本规范

- **命名空间/风格**：整个插件一个命名空间 `PaxAutocraticaHelper`；`internal static class` 服务类；公开给 GUI 的状态用 `internal static` 字段（`*Text`）；Harmony 补丁方法**必须 private static**，与业务方法分开标注。
- **注释语言**：中文注释（与 README/本项目一致）。
- **错误处理**：方法内 try/catch 全包，`PaxPlugin.Log.LogError`；限频规则见 §13.10。
- **配置**：新开关一律 `ModConfig` 加 ConfigEntry + README 配置表同步 + 旧 cfg 清理策略。
- **快捷键**：`FrameHook.Update` 集中登记；作弊类走 `MaybeCheat` 门控；复制/应用等副作用操作不放快捷键。
- **版本号**：`Plugin.cs [BepInPlugin]` 版本号是唯一真相，改完同步 README 变更历史 + 顶部版本、DEVELOPMENT.md §1；部署用 `build.ps1`（产物固定名）。
- **提交信息**：现有历史用 `vX.Y.Z: 描述` 风格；一次提交一个主题；WIP 探针代码与正式发布分开提交。
- **WIP 治理**：未定论/实验性改动（如 §9 词条）在工作区可留，但**发布前**：移除观察 Hook 与 `[AffixDump]`、收敛同步实验代码、核对该命令作用域、更新 README/手册。

---

## 15. 维护本手册（主动更新约定）

本手册与代码同仓（`DEVELOPMENT.md`），作为项目一手文档。约定：

- **每次功能变更**：改代码的同一批提交里，带上 DEVELOPMENT.md 对应章节的更新（§4~§9、§12 新增 API、§13 新坑）。
- **每次游戏更新**：记录 interop 重生成结果变化到 §2/§11/§13；新增/变更的关键 API 同步进 §12。
- **每次发布**：更新 §1 版本、README 变更历史、§9 WIP 状态；结算未决项。
- 配置项/快捷键的任何增删 → §7/§8 与 README 同步。
- 新增工具或职责变化 → §11。
- 接手/换人时：先通读本手册 + README + §9 当前状态，再动代码。

> 校验：改完 `.\build.ps1` 能过 → 对照 §3 验证日志 → 更新手册 → 再提交。
