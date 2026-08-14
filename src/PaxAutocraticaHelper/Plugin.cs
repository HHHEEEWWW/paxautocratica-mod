using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;

namespace PaxAutocraticaHelper;

[BepInPlugin("com.hhewww.paxautocraticahelper", "PaxAutocraticaHelper", "0.5.1")]
[BepInProcess("Pax Autocratica.exe")]
public class PaxPlugin : BasePlugin
{
    internal new static ManualLogSource Log = null!;
    internal static bool ShowWindow = true;
    internal static float LastCmdTime;

    /// <summary>转发到 CheatConsoleExecutor（面板/快捷键入口） */
    internal static void Exec(string command) => CheatConsoleExecutor.Exec(command);

    public override void Load()
    {
        Log = base.Log;
        Log.LogInfo("PaxAutocraticaHelper 0.5.1 loading (soldier-panel + hotkeys).");

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
