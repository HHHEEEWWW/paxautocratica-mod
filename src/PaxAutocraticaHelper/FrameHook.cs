using System;
using UnityEngine;

namespace PaxAutocraticaHelper;

/// <summary>
/// 每帧驱动：按键检测 + 定时任务（士兵轮询 / 自动分配）。
/// 相比 v0.3.0：不再依赖 InputSystem（interop 缺失），改用 InputLegacyModule；
/// 不再挂载未知的 Harmony 帧钩子，由 PanelBehaviour.Update 驱动，更稳。
/// </summary>
internal static class FrameHook
{
    private static float autoAssignTimer;
    private static float soldierPollTimer;
    private static bool _kbReady = true;
    private static float _lastCheatDenyLog;

    internal static void Update()
    {
        try
        {
            var now = Time.realtimeSinceStartup;

            // 士兵列表后台定时刷新（独立于面板显示状态：
            // 不依赖 DrawPanel 帧循环，流放/新增士兵在面板外也持续同步）
            SoldierManager.RefreshSoldierList();

            // 士兵轮询（同步面板）
            var poll = ModConfig.SoldierPollInterval.Value;
            if (poll > 0f && now - soldierPollTimer > poll)
            {
                soldierPollTimer = now;
                SoldierManager.PollCurrentSoldier();
            }

            // 全局自动分配
            var assign = ModConfig.AutoAssignInterval.Value;
            if (assign > 0f && now - autoAssignTimer > assign)
            {
                autoAssignTimer = now;
                NpcAutoAssign.AutoAssignAll();
            }

            if (Time.time < ModConfig.PanelShowDelay.Value) return;

            // ===== 按键 =====
            if (Input.GetKeyDown(KeyCode.F1))
            {
                PaxPlugin.ShowWindow = !PaxPlugin.ShowWindow;
                // 打开面板时立即刷新士兵列表（不等冷却）
                if (PaxPlugin.ShowWindow) SoldierManager.ForceListRefresh();
                PaxPlugin.Log.LogInfo(PaxPlugin.ShowWindow ? "Panel shown" : "Panel hidden");
            }
            // 注意：复制士兵/应用属性不再绑定快捷键（v0.5.3），
            // 只能通过面板按钮操作，防止误触。

            var ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (!ctrl) return;

            if (Input.GetKeyDown(KeyCode.Alpha1)) PaxPlugin.Exec("SetTimeScale 2");
            else if (Input.GetKeyDown(KeyCode.Alpha2)) PaxPlugin.Exec("SetTimeScale 5");
            else if (Input.GetKeyDown(KeyCode.Alpha3)) PaxPlugin.Exec("SetTimeScale 10");
            else if (Input.GetKeyDown(KeyCode.Alpha4)) PaxPlugin.Exec("SetTimeScale 1");
            else if (Input.GetKeyDown(KeyCode.Alpha5)) PaxPlugin.Exec("CompleteAllResearching");
            else if (Input.GetKeyDown(KeyCode.Alpha6)) PaxPlugin.Exec("AutoSave");
            else if (Input.GetKeyDown(KeyCode.Alpha7)) MaybeCheat("TestGod");
            else if (Input.GetKeyDown(KeyCode.Alpha8)) MaybeCheat("TestDaddy");
            else if (Input.GetKeyDown(KeyCode.Alpha9)) MaybeCheat("CraftNoConsume 1");
            else if (Input.GetKeyDown(KeyCode.Alpha0))
            {
                PaxPlugin.Log.LogInfo("Ctrl+0: 智能自动分配");
                NpcAutoAssign.AutoAssignAll();
            }
        }
        catch (Exception ex)
        {
            if (_kbReady)
            {
                _kbReady = false;
                PaxPlugin.Log.LogError($"FrameHook exception: {ex}");
            }
        }
    }

    /// <summary>执行作弊命令前检查「显示作弊功能」开关（与 README 约定一致：Ctrl+7/8/9 受该开关控制）</summary>
    private static void MaybeCheat(string command)
    {
        if (ModConfig.CheatSectionVisible.Value)
        {
            PaxPlugin.Exec(command);
            return;
        }
        // 限频提示：10 秒内最多记录一条
        if (Time.realtimeSinceStartup - _lastCheatDenyLog > 10f)
        {
            _lastCheatDenyLog = Time.realtimeSinceStartup;
            PaxPlugin.Log.LogWarning($"[FrameHook] 作弊快捷键被忽略（配置「显示作弊功能」=false）: {command}");
        }
    }
}
