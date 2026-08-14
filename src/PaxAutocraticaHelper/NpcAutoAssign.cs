using System;
using DelegateWarp;
using Multiverse.EFAS.NpcSimulator;

namespace PaxAutocraticaHelper;

/// <summary>
/// NPC 自动分配与机器人生成（间隔/数量配置化）。
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

    /// <summary>生成机器人（EfasItem 由配置指定） */
    internal static void SpawnRobots(int count)
    {
        try
        {
            var item = ModConfig.RobotEfasItem.Value;
            var ok = CheatConsoleExecutor.RunCommand("AddPeople", new object[] { item, 0, 20, count });
            PaxPlugin.Log.LogInfo($"[Robots] AddPeople({item},0,20,{count}) = {ok}");
        }
        catch (Exception ex)
        {
            PaxPlugin.Log.LogError($"[Robots] exception: {ex}");
        }
    }
}
