using System;
using HarmonyLib;
using Multiverse.EFAS.NpcSimulator;

namespace PaxAutocraticaHelper;

/// <summary>
/// 机器人强化：读取到机器人数据时自动拉满属性（数值全部配置化）。
/// </summary>
[HarmonyPatch]
internal static class NpcDataHook
{
    private static long lastBoostedId;

    [HarmonyPatch(typeof(NpcSimulatorManager), "TryGetNpcData")]
    [HarmonyPostfix]
    private static void OnNpcDataFetched(long _npcId, bool __result, NpcAttribute _npcAttribute)
    {
        try
        {
            if (!ModConfig.RobotBoostEnabled.Value) return;
            if (!__result || _npcAttribute == null) return;
            if (_npcAttribute.EfasItem != ModConfig.RobotEfasItem.Value) return;
            if (lastBoostedId == _npcId) return;
            lastBoostedId = _npcId;

            if (_npcAttribute.Stamina != null) _npcAttribute.Stamina.Value = ModConfig.RobotBoostStamina.Value;
            if (_npcAttribute.Fullness != null) _npcAttribute.Fullness.Value = ModConfig.RobotBoostFullness.Value;
            if (_npcAttribute.Mood != null) _npcAttribute.Mood.Value = ModConfig.RobotBoostMood.Value;
            if (_npcAttribute.Support != null) _npcAttribute.Support.Value = ModConfig.RobotBoostSupport.Value;
            if (_npcAttribute.Fear != null) _npcAttribute.Fear.Value = ModConfig.RobotBoostFear.Value;
            if (_npcAttribute.Level < ModConfig.RobotBoostMinLevel.Value)
            {
                _npcAttribute.Level = ModConfig.RobotBoostMinLevel.Value;
            }

            PaxPlugin.Log.LogInfo(
                $"[Robots] 强化员工 id={_npcId} EfasItem={_npcAttribute.EfasItem} 种族={_npcAttribute.Race}");
        }
        catch (Exception ex)
        {
            PaxPlugin.Log.LogError($"[Robots] Hook failed: {ex}");
        }
    }
}
