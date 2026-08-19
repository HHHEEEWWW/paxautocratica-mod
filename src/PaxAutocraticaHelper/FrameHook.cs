using System;
using UnityEngine;

namespace PaxAutocraticaHelper;

/// <summary>
/// 每帧驱动：按键检测 + 定时任务（士兵轮询）。
/// 相比 v0.3.0：不再依赖 InputSystem（interop 缺失），改用 InputLegacyModule；
/// 不再挂载未知的 Harmony 帧钩子，由 PanelBehaviour.Update 驱动，更稳。
/// v0.5.7：移除智能自动分配（系统自带）；时间倍速改为 Ctrl+1 循环切换；Ctrl+2 完成研究；Ctrl+3 恐惧归零。
/// </summary>
internal static class FrameHook
{
    private static float soldierPollTimer;
    private static bool _kbReady = true;
    private static float _lastCheatDenyLog;

    /// <summary>时间倍速档位（循环）</summary>
    private static readonly float[] TimeScaleCycle = { 1f, 2f, 5f, 10f };
    private static int _timeScaleIndex = -1;

    internal static void Update()
    {
        try
        {
            var now = Time.realtimeSinceStartup;

            // 士兵列表后台定时刷新（独立于面板显示状态：
            // 不依赖 DrawPanel 帧循环，流放/新增士兵在面板外也持续同步）
            SoldierManager.RefreshSoldierList();

            // 士兵轮询（同步面板：游戏点谁 MOD 面板跟谁）
            var poll = ModConfig.SoldierPollInterval.Value;
            if (poll > 0f && now - soldierPollTimer > poll)
            {
                soldierPollTimer = now;
                SoldierManager.PollCurrentSoldier();
            }

            if (Time.time < ModConfig.PanelShowDelay.Value) return;

            // ===== 按键 =====
            if (Input.GetKeyDown(KeyCode.F1))
            {
                PaxPlugin.ShowWindow = !PaxPlugin.ShowWindow;
                if (PaxPlugin.ShowWindow)
                {
                    // 打开面板立即刷新列表 + 从游戏当前选中拉取属性（不用去 MOD UI 翻名字）
                    SoldierManager.ForceListRefresh();
                    SoldierManager.SyncFromGameNow();
                }
                PaxPlugin.Log.LogInfo(PaxPlugin.ShowWindow ? "Panel shown" : "Panel hidden");
            }
            // 注意：复制士兵/应用属性不再绑定快捷键（v0.5.3），
            // 只能通过面板按钮操作，防止误触。

            var ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (!ctrl) return;

            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                PaxPlugin.Log.LogInfo("Ctrl+1: 时间倍速切换");
                CycleTimeScale();
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                PaxPlugin.Log.LogInfo("Ctrl+2: 完成所有研究");
                PaxPlugin.Exec("CompleteAllResearching");
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                PaxPlugin.Log.LogInfo("Ctrl+3: 所有单位恐惧归零");
                SoldierManager.SetAllFearZero();
            }
            else if (Input.GetKeyDown(KeyCode.Alpha7)) MaybeCheat("TestGod");
            else if (Input.GetKeyDown(KeyCode.Alpha8)) MaybeCheat("TestDaddy");
            // 注：Ctrl+9「免费制造」v0.5.7 移除——游戏在 CraftNoConsume=true 时对 DROP_VFX_GREEN
            // 常量特效每帧重播且缺可见性守卫，触发 VFX 超容量报错（游戏 bug，见 DEVELOPMENT.md §9-需求6）。
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

    /// <summary>时间倍速循环：1x→2x→5x→10x→1x…；首次按下取当前倍速就近档作起点</summary>
    private static void CycleTimeScale()
    {
        try
        {
            if (_timeScaleIndex < 0)
            {
                var cur = Time.timeScale;
                var best = 0;
                var bestDist = float.MaxValue;
                for (var i = 0; i < TimeScaleCycle.Length; i++)
                {
                    var d = Mathf.Abs(TimeScaleCycle[i] - cur);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        best = i;
                    }
                }
                _timeScaleIndex = best;
            }
            _timeScaleIndex = (_timeScaleIndex + 1) % TimeScaleCycle.Length;
            CheatConsoleExecutor.SetTimeScale(TimeScaleCycle[_timeScaleIndex]);
        }
        catch (Exception ex)
        {
            PaxPlugin.Log.LogError($"CycleTimeScale: {ex}");
        }
    }

    /// <summary>执行作弊命令前检查「显示作弊功能」开关（与 README 约定一致：Ctrl+7/8 受该开关控制）</summary>
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
