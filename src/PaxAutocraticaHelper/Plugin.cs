using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;

namespace PaxAutocraticaHelper;

[BepInPlugin("com.hhewww.paxautocraticahelper", "PaxAutocraticaHelper", "0.5.8")]
[BepInProcess("Pax Autocratica.exe")]
public class PaxPlugin : BasePlugin
{
    internal new static ManualLogSource Log = null!;
    // v0.5.8 性能修复：面板默认关闭。IMGUI 在 IL2CPP 下每次 GUILayout.* 都跨 interop
    // 边界，40+ 控件 × 每帧 2-3 次 OnGUI = 数百次互操作调用/帧，是掉帧主因。按 F1 打开。
    internal static bool ShowWindow = false;

    /// <summary>面板当前应显示（用户开关 + 总开关）。后台定时任务（列表刷新/轮询）以此为门卫。</summary>
    internal static bool PanelVisible => ShowWindow && ModConfig.PanelEnabled.Value;

    /// <summary>转发到 CheatConsoleExecutor（面板/快捷键入口） */
    internal static void Exec(string command) => CheatConsoleExecutor.Exec(command);

    public override void Load()
    {
        Log = base.Log;
        Log.LogInfo("PaxAutocraticaHelper loading.");

        // 1. 配置体系（管理器可表单化编辑）
        ModConfig.Init(Config);

        // 2. Harmony 补丁
        new Harmony("com.hhewww.paxautocraticahelper").PatchAll(typeof(PaxPlugin).Assembly);
        Log.LogInfo("Harmony patches applied.");

        // 3. 游戏内 IMGUI 面板宿主
        try
        {
            ClassInjector.RegisterTypeInIl2Cpp<PanelBehaviour>();
            AddComponent<PanelBehaviour>();
            Log.LogInfo("PanelBehaviour registered in Il2Cpp.");
        }
        catch (System.Exception ex)
        {
            Log.LogError($"PanelBehaviour setup failed: {ex}");
        }

        Log.LogInfo($"PaxAutocraticaHelper loaded. 作弊功能显示={ModConfig.CheatSectionVisible.Value}");
    }
}
