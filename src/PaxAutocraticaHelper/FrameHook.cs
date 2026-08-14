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

    internal static void Update()
    {
        try
        {
            var now = Time.realtimeSinceStartup;

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
                PaxPlugin.Log.LogInfo(PaxPlugin.ShowWindow ? "Panel shown" : "Panel hidden");
            }
            if (Input.GetKeyDown(KeyCode.F2))
            {
                PaxPlugin.Log.LogInfo($"[Soldier] F2 pressed, CurrentDetailNpcId={SoldierManager.CurrentDetailNpcId}");
                SoldierManager.CopyCurrentSoldier();
            }
            if (Input.GetKeyDown(KeyCode.F3))
            {
                PaxPlugin.Log.LogInfo($"[Soldier] F3 pressed, CurrentDetailNpcId={SoldierManager.CurrentDetailNpcId}");
                SoldierManager.ApplyAttributes();
            }

            var ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (!ctrl) return;

            if (Input.GetKeyDown(KeyCode.Alpha1)) PaxPlugin.Exec("SetTimeScale 2");
            else if (Input.GetKeyDown(KeyCode.Alpha2)) PaxPlugin.Exec("SetTimeScale 5");
            else if (Input.GetKeyDown(KeyCode.Alpha3)) PaxPlugin.Exec("SetTimeScale 10");
            else if (Input.GetKeyDown(KeyCode.Alpha4)) PaxPlugin.Exec("SetTimeScale 1");
            else if (Input.GetKeyDown(KeyCode.Alpha5)) PaxPlugin.Exec("CompleteAllResearching");
            else if (Input.GetKeyDown(KeyCode.Alpha6)) PaxPlugin.Exec("AutoSave");
            else if (Input.GetKeyDown(KeyCode.Alpha7)) PaxPlugin.Exec("TestGod");
            else if (Input.GetKeyDown(KeyCode.Alpha8)) PaxPlugin.Exec("TestDaddy");
            else if (Input.GetKeyDown(KeyCode.Alpha9)) PaxPlugin.Exec("CraftNoConsume 1");
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
}
