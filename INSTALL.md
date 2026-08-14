# 安装说明（PaxAutocraticaHelper 0.4.0）

## 前置

- 游戏：《Pax Autocratica》（Steam）
- BepInEx 6（IL2CPP）已安装（本机已装：`BepInEx 6.0.0`）

## 安装 / 升级

1. 运行 `build.ps1`（自动构建并部署）
2. 或手动：把 `src/PaxAutocraticaHelper/bin/Release/net6.0/PaxAutocraticaHelper.dll`
   复制到 `E:\steam\steamapps\common\Pax Autocratica\BepInEx\plugins\PaxAutocraticaHelper\`
3. **升级前删除旧版 dll**（build.ps1 会自动备份为 .bak，同名 GUID 冲突会导致旧版不加载）

## 验证

1. 启动游戏，查看 `BepInEx/LogOutput.log`：
   - `PaxAutocraticaHelper 0.4.0 loading` → 插件加载成功
   - `Harmony patches applied.` → 补丁生效
   - `PanelBehaviour registered in Il2Cpp.` → 面板宿主就绪
2. 游戏内按 `F1` 呼出便利面板
3. 配置文件在 `BepInEx/config/com.hhewww.paxautocraticahelper.cfg`，
   可用 BepInEx 管理器「配置」按钮表单化编辑

## 常见问题

| 现象 | 处理 |
|---|---|
| 面板不显示 | 检查配置「启用便利面板」；等待显示延迟（默认 8 秒）后按 F1 |
| 作弊按钮不见 | 配置「显示作弊功能」设为 true |
| AddPeople 失败 | 游戏内控制台未就绪（进主城后重试） |
| 升级后两个版本都加载 | 删除旧 dll（或 .bak）后重启游戏 |
