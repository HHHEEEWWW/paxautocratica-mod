using System;
using DelegateWarp;
using Multiverse.EFAS.NpcSimulator;

namespace PaxAutocraticaHelper;

/// <summary>
/// NPC 自动分配（间隔配置化）。
/// 注：机器人生成功能已按需求移除（v0.5.0）。
/// </summary>
internal static class NpcAutoAssign
{
    private static float lastLogTime;

    /// <summary>触发游戏全局人员自动分配（每 30 秒最多记录一次日志） */
    internal static void AutoAssignAll()
    {
        try
        {
            var onAutoAssignPeople = NpcEnvironmentManager.OnAutoAssignPeople;
            if (onAutoAssignPeople == null)
            {
                PaxPlugin.Log.LogError("[AutoAssign] OnAutoAssignPeople 为 null（未进主城？）");
                return;
            }
            onAutoAssignPeople.Invoke();
            if (UnityEngine.Time.time - lastLogTime > 30f)
            {
                lastLogTime = UnityEngine.Time.time;
                PaxPlugin.Log.LogInfo("[AutoAssign] 已触发游戏全局自动分配");
            }
        }
        catch (Exception ex)
        {
            PaxPlugin.Log.LogError($"[AutoAssign] exception: {ex}");
        }
    }
}
