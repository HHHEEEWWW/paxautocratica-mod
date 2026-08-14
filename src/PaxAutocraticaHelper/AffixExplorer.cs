using System;
using System.Text;
using EFAS.EFAS_DATA;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppSystem.Reflection;
using Multiverse.EFAS.NpcSimulator;
using UnityEngine;

namespace PaxAutocraticaHelper;

/// <summary>
/// 运行时探索 NPC 特质（Affix）配置与士兵状态判断（调试用，Ctrl+F10 触发）。
/// v3：Il2CppSystem 值类型用 m_value unbox；打印 NPC 有效性判断（流放过滤依据）。
/// </summary>
internal static class AffixExplorer
{
    /// <summary>自动探测状态：是否已执行过（只自动跑一次）</summary>
    private static bool _autoDumped;
    private static float _autoStartTime = -1f;

    /// <summary>
    /// 每帧调用：数据就绪后延迟 20 秒自动 dump 一次（用户无需任何操作）。
    /// </summary>
    internal static void AutoCheck()
    {
        try
        {
            if (_autoDumped) return;
            var dic = Multiverse.EFAS.NpcSimulator.NpcSimulatorManager.NpcAttributeDic;
            if (dic == null)
            {
                _autoStartTime = -1f;
                return;
            }
            if (_autoStartTime < 0f) _autoStartTime = UnityEngine.Time.realtimeSinceStartup;
            // 数据就绪后再等 20 秒（等游戏加载完成、特质数据初始化），只跑一次
            if (UnityEngine.Time.realtimeSinceStartup - _autoStartTime > 20f)
            {
                _autoDumped = true;
                PaxPlugin.Log.LogInfo("[AffixDump] 自动探测触发（数据就绪 +20s）");
                DumpAll();
            }
        }
        catch
        {
            /* 静默：下次再试 */
        }
    }

    internal static void DumpAll()
    {
        try
        {
            DumpNpcAffixConfig();
            DumpNpcValidity();
        }
        catch (Exception ex)
        {
            PaxPlugin.Log.LogError($"[AffixDump] DumpAll: {ex}");
        }
    }

    /// <summary>遍历 DataObjNpcAffix 配置表，打印前 15 个 NpcAffix 的字段值（m_value unbox）</summary>
    private static void DumpNpcAffixConfig()
    {
        var objs = Resources.FindObjectsOfTypeAll<DataObjNpcAffix>();
        PaxPlugin.Log.LogInfo($"[AffixDump] DataObjNpcAffix 实例数: {objs?.Length ?? 0}");
        if (objs == null || objs.Length == 0) return;

        foreach (var dataObj in objs)
        {
            var list = dataObj.GetNpcAffixList();
            PaxPlugin.Log.LogInfo($"[AffixDump] NpcAffix 总数: {list?.Count ?? 0}（打印前 15 条）");
            if (list == null) continue;
            var max = Math.Min(list.Count, 15);
            for (var i = 0; i < max; i++)
            {
                var affix = list[i];
                if (affix == null) continue;
                var sb = new StringBuilder($"[AffixDump] #{i} | ");
                try
                {
                    var type = affix.GetIl2CppType();
                    var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                    var fields = type.GetFields(flags);
                    if (fields != null)
                    {
                        foreach (var f in fields)
                        {
                            try
                            {
                                sb.Append($"{f.Name}={DescribeValue(f.GetValue(affix))}; ");
                            }
                            catch (Exception fe)
                            {
                                sb.Append($"{f.Name}=<err:{fe.GetType().Name}>; ");
                            }
                        }
                    }
                }
                catch (Exception te)
                {
                    sb.Append($"reflect-err:{te.GetType().Name}");
                }
                PaxPlugin.Log.LogInfo(sb.ToString());
            }
        }
    }

    /// <summary>遍历 NPC 字典，打印每个 NPC 的名字与有效性判断（找流放区分依据）</summary>
    private static void DumpNpcValidity()
    {
        var dic = NpcSimulatorManager.NpcAttributeDic;
        if (dic == null) return;
        var il2cppDic = ((Il2CppObjectBase)dic).Cast<Il2CppSystem.Collections.Generic.Dictionary<long, NpcAttribute>>();
        if (il2cppDic == null) return;

        var shown = 0;
        var enumerator = il2cppDic.GetEnumerator();
        while (enumerator.MoveNext() && shown < 25)
        {
            var npc = enumerator.Current.Value;
            if (npc == null) continue;
            var sb = new StringBuilder();
            sb.Append($"[AffixDump] NPC id={npc.Id} name={npc.Name} | ");
            // 属性
            sb.Append($"AffixList=[");
            if (npc.AffixList != null)
            {
                var ae = npc.AffixList.GetEnumerator();
                while (ae.MoveNext()) sb.Append($"{ae.Current},");
            }
            sb.Append("] | ");
            // 状态判断（尝试各方法，找出流放可区分项）
            TryAppend(sb, "IsValidSoldier", () => NpcAttributeExtension.IsValidSoldier(npc));
            TryAppend(sb, "IsRegularSoldier", () => NpcAttributeExtension.IsRegularSoldier(npc));
            TryAppend(sb, "IsDissident", () => NpcAttributeExtension.IsDissident(npc));
            TryAppend(sb, "IsRecruit", () => npc.IsRecruit);
            TryAppend(sb, "IsBeCaptured", () => npc.IsBeCaptured);
            TryAppend(sb, "IsUnderSurveillance", () => npc.IsUnderSurveillance);
            TryAppend(sb, "IsDead", () => npc.IsDead);
            TryAppend(sb, "StatusList", () => DescribeStatusList(npc.StatusList));
            PaxPlugin.Log.LogInfo(sb.ToString());
            shown++;
        }
    }

    private static void TryAppend(StringBuilder sb, string name, Func<object?> get)
    {
        try
        {
            sb.Append($"{name}={get()} | ");
        }
        catch (Exception ex)
        {
            sb.Append($"{name}=<err:{ex.GetType().Name}> | ");
        }
    }

    private static string DescribeStatusList(object? list)
    {
        if (list == null) return "null";
        var sb = new StringBuilder();
        try
        {
            if (list is Il2CppSystem.Collections.Generic.List<Il2CppSystem.Int32> il)
            {
                sb.Append("List<int>(").Append(il.Count).Append(")={");
                for (var i = 0; i < il.Count && i < 10; i++) sb.Append(il[i].m_value).Append(",");
                sb.Append("}");
                return sb.ToString();
            }
            var t = list.GetType();
            if (t.Name.StartsWith("List`1") || t.Name.Contains("List"))
            {
                var count = t.GetProperty("Count")?.GetValue(list);
                return $"{t.Name}(count={count})";
            }
            return $"<{t.FullName}>";
        }
        catch (Exception ex)
        {
            return $"<err:{ex.GetType().Name}>";
        }
    }

    /// <summary>把字段值转换为可读字符串（Il2CppSystem 值类型用 m_value unbox）</summary>
    private static string DescribeValue(object? v)
    {
        if (v == null) return "null";
        try
        {
            if (v is int || v is long || v is float || v is double || v is bool) return v.ToString()!;
            if (v is string s) return "\"" + s + "\"";
            if (v is Il2CppSystem.Int32 i32) return i32.m_value.ToString();
            if (v is Il2CppSystem.Int64 i64) return i64.m_value.ToString();
            if (v is Il2CppSystem.Single f) return f.m_value.ToString("0.###");
            if (v is Il2CppSystem.Boolean b) return b.m_value.ToString();
            if (v is Il2CppSystem.Single[] fa) return $"float[{fa.Length}]";
            if (v is Il2CppSystem.Int32[] ia) return $"int[{ia.Length}]";
            return "<" + v.GetType().FullName + ">";
        }
        catch (Exception ex)
        {
            return $"<err:{ex.GetType().Name}>";
        }
    }
}
