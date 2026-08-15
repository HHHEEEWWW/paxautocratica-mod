using System;
using System.Collections.Generic;
using EFAS.EFAS_DATA;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppSystem.Reflection;
using UnityEngine;

namespace PaxAutocraticaHelper;

/// <summary>
/// 特质过滤：复制士兵时只保留正面战斗类特质（攻击速度/精准/移动速度/伤害强化等），
/// 剔除负面与非战斗特质（煽动者/躁动/体弱/种族限定/忠诚波动等）。
/// 判定依据：DataObjNpcAffix 配置表的 m_localKey（特质名，CORPSAFFIX_* 枚举名）。
/// 配置表未加载（name==null）时保留原特质（降级安全，不破坏复制）。
/// </summary>
internal static class AffixFilter
{
    /// <summary>正面战斗类特质名模式（取自存档实测的 123 个 CORPSAFFIX_* 名称全集）</summary>
    private static readonly string[] PositiveCombatPatterns =
    {
        "ATTACKSPEED",          // 攻击速度
        "MOVESPEEDINCREASED",   // 移动速度
        "SHOOTDAMAGEINCREASED", // 射击伤害
        "ALLDAMAGEMODIFIER",    // 全伤害
        "SOLDIERALLDAMAGEMODIFIER", // 士兵全伤害
        "CRITSTRIKE",           // 暴击率/暴击
        "CRITICALDAMAGE",       // 暴击伤害
        "MAXHEALTH",            // 生命上限
        "MAXSHIELD",            // 护盾上限
        "SHIELDINCREASED",      // 护盾增加
        "SHIELDRECHARGE",       // 护盾充能
        "RELOADSPEED",          // 换弹速度
        "SWITCHWEAPONSPEED",    // 切枪速度
        "CLIPCAPACITY",         // 弹匣容量
        "HPINCREASED",          // 生命增加
        "ATTACKRAISE",          // 攻击提升
        "ENERGETIC",            // 精力充沛
        "HARDY",                // 强壮
        "DISEASEIMMUNITY",      // 疾病免疫
        "IRONSURVIVOR",         // 钢铁幸存者
        "MORNING_STRIKE",       // 晨间突袭
        "JETPACK",              // 喷气背包
        "ARMED_TRANSFORMATION", // 武装变形
        "IMMORTALITY",          // 不死
    };

    /// <summary>缓存：特质 id → 名称（配置表首次可查时构建）</summary>
    private static readonly Dictionary<int, string> AffixNameCache = new();

    private static bool _cacheBuilt;
    private static float _lastCacheAttempt;

    /// <summary>判定特质名是否为正面战斗类</summary>
    internal static bool IsPositiveCombatAffix(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        foreach (var p in PositiveCombatPatterns)
        {
            if (name.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }
        return false;
    }

    /// <summary>按特质 id 查名称（DataObjNpcAffix 配置表）；查不到返回 null（调用方降级保留）</summary>
    internal static string? GetAffixName(int affixId)
    {
        if (_cacheBuilt)
        {
            return AffixNameCache.TryGetValue(affixId, out var n) ? n : null;
        }

        // 配置表可能延迟加载（进主城后 Addressables 加载）：每 30 秒最多重试一次构建缓存
        if (Time.realtimeSinceStartup - _lastCacheAttempt < 30f) return null;
        _lastCacheAttempt = Time.realtimeSinceStartup;

        try
        {
            var objs = Resources.FindObjectsOfTypeAll<DataObjNpcAffix>();
            if (objs == null || objs.Length == 0) return null;

            foreach (var dataObj in objs)
            {
                var list = dataObj.GetNpcAffixList();
                if (list == null) continue;
                foreach (var affix in list)
                {
                    if (affix == null) continue;
                    var id = ReadIntField(affix, "m_affixId");
                    var key = ReadStringField(affix, "m_localKey");
                    if (id >= 0 && !string.IsNullOrEmpty(key) && !AffixNameCache.ContainsKey(id))
                    {
                        AffixNameCache[id] = key;
                    }
                }
            }
            _cacheBuilt = AffixNameCache.Count > 0;
            if (_cacheBuilt)
            {
                PaxPlugin.Log.LogInfo($"[AffixFilter] 特质名缓存构建完成: {AffixNameCache.Count} 条");
            }
            return AffixNameCache.TryGetValue(affixId, out var n2) ? n2 : null;
        }
        catch (Exception ex)
        {
            PaxPlugin.Log.LogError($"[AffixFilter] GetAffixName: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    private static int ReadIntField(NpcAffix affix, string fieldName)
    {
        try
        {
            var type = affix.GetIl2CppType();
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var f = type.GetField(fieldName, flags);
            if (f != null)
            {
                object v = f.GetValue(affix);
                if (v is Il2CppSystem.Int32 i32) return i32.m_value;
                if (v is int i) return i;
            }
            var p = type.GetProperty(fieldName, flags);
            if (p != null)
            {
                object v = p.GetValue(affix);
                if (v is Il2CppSystem.Int32 i32) return i32.m_value;
                if (v is int i) return i;
            }
        }
        catch { }
        return -1;
    }

    private static string? ReadStringField(NpcAffix affix, string fieldName)
    {
        try
        {
            var type = affix.GetIl2CppType();
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var f = type.GetField(fieldName, flags);
            if (f != null)
            {
                object v = f.GetValue(affix);
                if (v is string s) return s;
            }
            var p = type.GetProperty(fieldName, flags);
            if (p != null)
            {
                object v = p.GetValue(affix);
                if (v is string s2) return s2;
            }
        }
        catch { }
        return null;
    }
}
