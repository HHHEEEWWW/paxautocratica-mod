using System;
using System.Collections.Generic;
using EFAS.UIFramework;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Multiverse.EFAS.NpcSimulator;
using Multiverse.EFAS.UI.Meta;
using UnityEngine;

namespace PaxAutocraticaHelper;

/// <summary>
/// 士兵管理：列表 / 选择 / 复制 / 属性编辑。
/// 逻辑保持与 v0.3.0 一致，列表刷新间隔改为配置项。
/// </summary>
internal static class SoldierManager
{
    internal sealed class SoldierEntry
    {
        public long Id;
        public string Label = "";
    }

    internal static long CurrentDetailNpcId;
    internal static NpcAttribute? CurrentDetailNpc;

    private static bool _pendingCopy;
    private static NpcAttribute? _copySource;
    private static float _copyStartTime;
    private static long _maxIdBeforeCopy;

    internal static readonly List<SoldierEntry> SoldierEntries = new();

    private static float _listRefreshTimer;

    // 面板文本框状态
    internal static string LevelText = "";
    internal static string ExpText = "";
    internal static string StaminaText = "";
    internal static string FullnessText = "";
    internal static string MoodText = "";
    internal static string SupportText = "";
    internal static string FearText = "";
    internal static string GoldText = "";
    internal static string WageText = "";
    internal static string CraftSpeedText = "";
    internal static string ResearchSpeedText = "";
    internal static string ProduceSpeedText = "";
    internal static string CollectSpeedText = "";
    internal static string CarrySpeedText = "";
    internal static string PlantingSpeedText = "";
    internal static string LoggingSpeedText = "";
    internal static string CookingSpeedText = "";
    internal static string BreedingSpeedText = "";
    internal static string GatherFoodSpeedText = "";
    internal static string StatusText = "";

    // ================= 列表 =================

    /// <summary>重置列表刷新冷却，使下一次绘制立即刷新（复制完成/打开面板时调用）</summary>
    internal static void ForceListRefresh()
    {
        _listRefreshTimer = 0f;
    }

    internal static void RefreshSoldierList()
    {
        try
        {
            var interval = ModConfig.SoldierListRefreshInterval.Value;
            if (interval <= 0f || Time.realtimeSinceStartup - _listRefreshTimer < interval) return;
            _listRefreshTimer = Time.realtimeSinceStartup;
            SoldierEntries.Clear();
            var dic = NpcSimulatorManager.NpcAttributeDic;
            if (dic == null) return;

            var il2cppDic = ((Il2CppObjectBase)dic).Cast<Il2CppSystem.Collections.Generic.Dictionary<long, NpcAttribute>>();
            if (il2cppDic == null) return;

            var currentStillExists = false;
            var enumerator = il2cppDic.GetEnumerator();
            while (enumerator.MoveNext())
            {
                var value = enumerator.Current.Value;
                if (value != null)
                {
                    if (enumerator.Current.Key == CurrentDetailNpcId) currentStillExists = true;
                    SoldierEntries.Add(new SoldierEntry
                    {
                        Id = enumerator.Current.Key,
                        Label = $"{value.Name}  Lv.{value.Level}  职业:{value.EfasItem}"
                    });
                }
            }
            SoldierEntries.Sort((a, b) => string.Compare(a.Label, b.Label, StringComparison.Ordinal));

            // 当前选中的士兵已从字典消失（流放/死亡等）→ 清空详情，防止操作已不存在的对象
            if (!currentStillExists && CurrentDetailNpc != null)
            {
                CurrentDetailNpc = null;
                CurrentDetailNpcId = 0;
                StatusText = "当前士兵已不在（可能已流放/死亡）";
                PaxPlugin.Log.LogInfo("[Soldier] 当前选中士兵已从字典消失，已清空详情");
            }
        }
        catch (Exception ex)
        {
            PaxPlugin.Log.LogError($"[Soldier] RefreshSoldierList: {ex}");
        }
    }

    // ================= 选择 =================

    internal static void SelectFromList(long id)
    {
        try
        {
            CurrentDetailNpcId = id;
            CurrentDetailNpc = NpcSimulatorManager.TryGetNpcData(id, out var npc, true) ? npc : null;
            if (CurrentDetailNpc != null)
            {
                PaxPlugin.Log.LogInfo($"[Soldier] 列表选中: id={id} name={CurrentDetailNpc.Name} lv={CurrentDetailNpc.Level}");
                SyncPanelFromCurrent();
            }
            else
            {
                StatusText = $"未找到士兵 id={id}";
            }
        }
        catch (Exception ex)
        {
            PaxPlugin.Log.LogError($"[Soldier] SelectFromList: {ex}");
        }
    }

    private static void SelectById(long id)
    {
        CurrentDetailNpc = NpcSimulatorManager.TryGetNpcData(id, out var npc, true) ? npc : null;
        if (CurrentDetailNpc != null)
        {
            PaxPlugin.Log.LogInfo($"[Soldier] 切换: id={id} name={CurrentDetailNpc.Name} lv={CurrentDetailNpc.Level}");
            SyncPanelFromCurrent();
        }
    }

    // ================= Harmony：跟随游戏内选择 =================

    [HarmonyPatch(typeof(UIManagerSoldier), "OnConfirmSoldier")]
    [HarmonyPostfix]
    private static void OnSoldierConfirm(int _index, GridItemInfo _itemInfo)
    {
        try
        {
            if (_itemInfo.CharacterId <= 0) return;
            if (_itemInfo.CharacterId == CurrentDetailNpcId && CurrentDetailNpc != null) return;
            CurrentDetailNpcId = _itemInfo.CharacterId;
            SelectById(CurrentDetailNpcId);
        }
        catch (Exception ex)
        {
            PaxPlugin.Log.LogError($"[Soldier] OnSoldierConfirm: {ex}");
        }
    }

    [HarmonyPatch(typeof(UIPopupSoldierDetail), "SetContent")]
    [HarmonyPostfix]
    private static void OnDetailSetContent(UIPopupSoldierDetailData _data)
    {
        try
        {
            if (_data?.ItemInfo == null || _data.ItemInfo.CharacterId <= 0) return;
            CurrentDetailNpcId = _data.ItemInfo.CharacterId;
            SelectById(CurrentDetailNpcId);
        }
        catch (Exception ex)
        {
            PaxPlugin.Log.LogError($"[Soldier] OnDetailSetContent: {ex}");
        }
    }

    [HarmonyPatch(typeof(UIManagerSoldier), "UpdateContent")]
    [HarmonyPostfix]
    private static void OnSoldierManagerUpdate(UIManagerSoldier __instance, UISoldierManagerData _managerData)
    {
        try
        {
            if (_managerData == null) return;
            var id = _managerData.ViewDetailNpcId;
            if (id <= 0 || id == CurrentDetailNpcId) return;
            CurrentDetailNpcId = id;
            SelectById(id);
        }
        catch (Exception ex)
        {
            PaxPlugin.Log.LogError($"[Soldier] OnSoldierManagerUpdate: {ex}");
        }
    }

    [HarmonyPatch(typeof(UIPopupSoldierDetail), "OnShow")]
    [HarmonyPostfix]
    private static void OnDetailShow(UIPopupSoldierDetail __instance)
    {
        try
        {
            var npc = Traverse.Create(__instance).Field("m_npcAttribute").GetValue<NpcAttribute>();
            if (npc != null && npc.Id > 0 && npc.Id != CurrentDetailNpcId)
            {
                CurrentDetailNpcId = npc.Id;
                CurrentDetailNpc = npc;
                PaxPlugin.Log.LogInfo($"[Soldier] 详情弹窗显示(OnShow): id={npc.Id} name={npc.Name}");
                SyncPanelFromCurrent();
            }
        }
        catch (Exception ex)
        {
            PaxPlugin.Log.LogError($"[Soldier] OnDetailShow: {ex}");
        }
    }

    // ================= 轮询 =================

    internal static void PollCurrentSoldier()
    {
        try
        {
            var popup = UnityEngine.Object.FindObjectOfType<UIPopupSoldierDetail>();
            if (popup != null)
            {
                var npc = Traverse.Create(popup).Field("m_npcAttribute").GetValue<NpcAttribute>();
                if (npc != null && npc.Id > 0)
                {
                    if (npc.Id != CurrentDetailNpcId)
                    {
                        CurrentDetailNpcId = npc.Id;
                        CurrentDetailNpc = npc;
                        PaxPlugin.Log.LogInfo($"[Soldier] 轮询: 详情弹窗 id={npc.Id} name={npc.Name}");
                        SyncPanelFromCurrent();
                    }
                    return;
                }
            }

            var manager = UnityEngine.Object.FindObjectOfType<UIManagerSoldier>();
            if (manager != null)
            {
                var data = Traverse.Create(manager).Field("m_managerData").GetValue<UISoldierManagerData>();
                var id = data?.ViewDetailNpcId ?? 0;
                if (id <= 0 || id == CurrentDetailNpcId) return;
                CurrentDetailNpcId = id;
                SelectById(id);
            }
        }
        catch (Exception ex)
        {
            PaxPlugin.Log.LogError($"[Soldier] PollCurrentSoldier: {ex}");
        }
    }

    // ================= 复制士兵 =================

    /// <summary>当前 NPC 字典中最大的 Id（复制前快照，用于识别 AddPeople 新兵）</summary>
    private static long GetMaxNpcId()
    {
        try
        {
            var dic = NpcSimulatorManager.NpcAttributeDic;
            if (dic == null) return 0;
            var il2cppDic = ((Il2CppObjectBase)dic).Cast<Il2CppSystem.Collections.Generic.Dictionary<long, NpcAttribute>>();
            if (il2cppDic == null) return 0;
            long max = 0;
            var enumerator = il2cppDic.GetEnumerator();
            while (enumerator.MoveNext())
            {
                if (enumerator.Current.Key > max) max = enumerator.Current.Key;
            }
            return max;
        }
        catch (Exception ex)
        {
            PaxPlugin.Log.LogError($"[Soldier] GetMaxNpcId: {ex}");
            return 0;
        }
    }

    internal static void CopyCurrentSoldier()
    {
        try
        {
            var src = CurrentDetailNpc;
            if (src == null || src.IsDead)
            {
                StatusText = "请先在士兵管理页打开一个士兵详情";
                PaxPlugin.Log.LogWarning($"[Soldier] 复制失败: CurrentDetailNpcId={CurrentDetailNpcId} Npc=null");
                return;
            }

            _copySource = src;
            _pendingCopy = true;
            _copyStartTime = Time.realtimeSinceStartup;
            // 记录复制前 NPC 最大 Id，用于区分 AddPeople 生成的新兵
            _maxIdBeforeCopy = GetMaxNpcId();

            if (!CheatConsoleExecutor.RunCommand("AddPeople", new object[] { src.EfasItem, src.GenderType, src.Age, 1 }))
            {
                _pendingCopy = false;
                StatusText = "AddPeople 调用失败（控制台未就绪？）";
                return;
            }

            // 同步路径：AddPeople 为同步命令，返回后立即查找新增 NPC（绝大多数情况在此命中）
            if (TryCaptureNewNpc())
            {
                return;
            }
            StatusText = $"正在复制 {src.Name} …";
            PaxPlugin.Log.LogInfo($"[Soldier] 发起复制: item={src.EfasItem} gender={src.GenderType} age={src.Age}");
        }
        catch (Exception ex)
        {
            _pendingCopy = false;
            PaxPlugin.Log.LogError($"[Soldier] CopyCurrentSoldier: {ex}");
        }
    }

    /// <summary>
    /// 在 NPC 字典中查找 Id 大于复制前最大值的 NPC 并执行复制；找到返回 true。
    /// 此时游戏内新增 NPC 几乎必然是本次 AddPeople 的产物，直接复制其全部属性。
    /// </summary>
    private static bool TryCaptureNewNpc()
    {
        try
        {
            var dic = NpcSimulatorManager.NpcAttributeDic;
            if (dic == null || _copySource == null) return false;
            var il2cppDic = ((Il2CppObjectBase)dic).Cast<Il2CppSystem.Collections.Generic.Dictionary<long, NpcAttribute>>();
            if (il2cppDic == null) return false;

            var enumerator = il2cppDic.GetEnumerator();
            while (enumerator.MoveNext())
            {
                var npc = enumerator.Current.Value;
                if (npc == null || npc.Id <= _maxIdBeforeCopy || npc.Id == _copySource.Id) continue;

                _pendingCopy = false;
                CopyAttributes(_copySource, npc);
                PaxPlugin.Log.LogInfo($"[Soldier] 复制完成(同步): 新士兵 id={npc.Id} name={npc.Name}（源 {_copySource.Name}）");
                NpcSimulatorManager.OnNpcBehaviourChanged?.Invoke(npc.Id);
                StatusText = $"已复制: {_copySource.Name} → {npc.Name} (id={npc.Id})";
                // 新兵立即出现在列表中（不等 2 秒刷新冷却）
                ForceListRefresh();
                return true;
            }
        }
        catch (Exception ex)
        {
            PaxPlugin.Log.LogError($"[Soldier] TryCaptureNewNpc: {ex}");
        }
        return false;
    }

    [HarmonyPatch(typeof(NpcSimulatorManager), "AddNpcAttribute")]
    [HarmonyPostfix]
    private static void OnNpcAttributeAdded(NpcAttribute _attribute)
    {
        try
        {
            if (!_pendingCopy) return;
            // 复制等待超时（3 秒；同步路径已处理绝大多数情况，这里仅兜底异步生成）
            if (Time.realtimeSinceStartup - _copyStartTime > 3f)
            {
                _pendingCopy = false;
                PaxPlugin.Log.LogWarning("[Soldier] 复制等待超时，已取消");
                StatusText = "复制超时（AddPeople 未生成新兵）";
                return;
            }
            if (_attribute == null || _copySource == null || _attribute.Id == _copySource.Id) return;

            // 兜底：只认 Id 大于复制前最大值的 NPC（新兵生成瞬间属性可能尚未初始化，
            // 不做职业/性别/年龄匹配——同步路径已保证绝大多数情况命中正确目标）
            if (_attribute.Id <= _maxIdBeforeCopy) return;

            _pendingCopy = false;
            CopyAttributes(_copySource, _attribute);
            PaxPlugin.Log.LogInfo($"[Soldier] 复制完成: 新士兵 id={_attribute.Id} name={_attribute.Name}（源 {_copySource.Name}）");
            NpcSimulatorManager.OnNpcBehaviourChanged?.Invoke(_attribute.Id);
            StatusText = $"已复制: {_copySource.Name} → {_attribute.Name} (id={_attribute.Id})";
            // 新兵立即出现在列表中
            ForceListRefresh();
        }
        catch (Exception ex)
        {
            PaxPlugin.Log.LogError($"[Soldier] OnNpcAttributeAdded: {ex}");
        }
    }

    private static void CopyAttributes(NpcAttribute src, NpcAttribute dst)
    {
        dst.Level = src.Level;
        dst.Exp = src.Exp;
        CopyInt(src.Stamina, dst.Stamina);
        CopyInt(src.Fullness, dst.Fullness);
        CopyInt(src.Mood, dst.Mood);
        CopyInt(src.Support, dst.Support);
        CopyInt(src.Fear, dst.Fear);
        CopyInt(src.Wages, dst.Wages);
        CopyInt(src.Gold, dst.Gold);
        CopyInt(src.CraftSpeed, dst.CraftSpeed);
        CopyInt(src.CollectSpeed, dst.CollectSpeed);
        CopyInt(src.ResearchSpeed, dst.ResearchSpeed);
        CopyInt(src.PlantingSpeed, dst.PlantingSpeed);
        CopyInt(src.ProduceSpeed, dst.ProduceSpeed);
        CopyInt(src.CarrySpeed, dst.CarrySpeed);
        CopyInt(src.GatherFoodSpeed, dst.GatherFoodSpeed);
        CopyInt(src.LoggingSpeed, dst.LoggingSpeed);
        CopyInt(src.CookingSpeed, dst.CookingSpeed);
        CopyInt(src.BreedingSpeed, dst.BreedingSpeed);

        if (src.AffixList != null)
        {
            if (dst.AffixList == null)
            {
                dst.AffixList = new Il2CppSystem.Collections.Generic.List<int>();
            }
            dst.AffixList.Clear();
            var enumerator = src.AffixList.GetEnumerator();
            while (enumerator.MoveNext())
            {
                var affixId = enumerator.Current;
                var name = AffixFilter.GetAffixName(affixId);
                if (name == null || AffixFilter.IsPositiveCombatAffix(name))
                {
                    // 配置表不可查（name==null）时保留原特质（降级安全）；可查时只保留正面战斗类
                    dst.AffixList.Add(affixId);
                }
                else
                {
                    PaxPlugin.Log.LogInfo($"[Soldier] 过滤特质: id={affixId} name={name}（非正面战斗类，剔除）");
                }
            }
        }
    }

    private static void CopyInt(Attribute<int>? src, Attribute<int>? dst)
    {
        if (src != null && dst != null) dst.Value = src.Value;
    }

    // ================= 应用属性 =================

    internal static void ApplyAttributes()
    {
        try
        {
            var npc = CurrentDetailNpc;
            if (npc == null)
            {
                StatusText = "请先打开士兵详情页";
                return;
            }

            ApplyIfParsedUInt(LevelText, v => npc.Level = v);
            ApplyIfParsedUInt(ExpText, v => npc.Exp = v);
            ApplyIfParsedInt(StaminaText, v => { if (npc.Stamina != null) npc.Stamina.Value = v; });
            ApplyIfParsedInt(FullnessText, v => { if (npc.Fullness != null) npc.Fullness.Value = v; });
            ApplyIfParsedInt(MoodText, v => { if (npc.Mood != null) npc.Mood.Value = v; });
            ApplyIfParsedInt(SupportText, v => { if (npc.Support != null) npc.Support.Value = v; });
            ApplyIfParsedInt(FearText, v => { if (npc.Fear != null) npc.Fear.Value = v; });
            ApplyIfParsedInt(GoldText, v => { if (npc.Gold != null) npc.Gold.Value = v; });
            ApplyIfParsedInt(WageText, v => { if (npc.Wages != null) npc.Wages.Value = v; });
            ApplyIfParsedInt(CraftSpeedText, v => { if (npc.CraftSpeed != null) npc.CraftSpeed.Value = v; });
            ApplyIfParsedInt(ResearchSpeedText, v => { if (npc.ResearchSpeed != null) npc.ResearchSpeed.Value = v; });
            ApplyIfParsedInt(ProduceSpeedText, v => { if (npc.ProduceSpeed != null) npc.ProduceSpeed.Value = v; });
            ApplyIfParsedInt(CollectSpeedText, v => { if (npc.CollectSpeed != null) npc.CollectSpeed.Value = v; });
            ApplyIfParsedInt(CarrySpeedText, v => { if (npc.CarrySpeed != null) npc.CarrySpeed.Value = v; });
            ApplyIfParsedInt(PlantingSpeedText, v => { if (npc.PlantingSpeed != null) npc.PlantingSpeed.Value = v; });
            ApplyIfParsedInt(LoggingSpeedText, v => { if (npc.LoggingSpeed != null) npc.LoggingSpeed.Value = v; });
            ApplyIfParsedInt(CookingSpeedText, v => { if (npc.CookingSpeed != null) npc.CookingSpeed.Value = v; });
            ApplyIfParsedInt(BreedingSpeedText, v => { if (npc.BreedingSpeed != null) npc.BreedingSpeed.Value = v; });
            ApplyIfParsedInt(GatherFoodSpeedText, v => { if (npc.GatherFoodSpeed != null) npc.GatherFoodSpeed.Value = v; });

            NpcSimulatorManager.OnNpcBehaviourChanged?.Invoke(npc.Id);
            StatusText = $"已应用属性 → {npc.Name} (id={npc.Id})";
            PaxPlugin.Log.LogInfo($"[Soldier] 属性应用: id={npc.Id} lv={npc.Level}");
        }
        catch (Exception ex)
        {
            PaxPlugin.Log.LogError($"[Soldier] ApplyAttributes: {ex}");
        }
    }

    internal static void SyncPanelFromCurrent()
    {
        var npc = CurrentDetailNpc;
        if (npc == null) return;
        LevelText = npc.Level.ToString();
        ExpText = npc.Exp.ToString();
        StaminaText = npc.Stamina?.Value.ToString() ?? "";
        FullnessText = npc.Fullness?.Value.ToString() ?? "";
        MoodText = npc.Mood?.Value.ToString() ?? "";
        SupportText = npc.Support?.Value.ToString() ?? "";
        FearText = npc.Fear?.Value.ToString() ?? "";
        GoldText = npc.Gold?.Value.ToString() ?? "";
        WageText = npc.Wages?.Value.ToString() ?? "";
        CraftSpeedText = npc.CraftSpeed?.Value.ToString() ?? "";
        ResearchSpeedText = npc.ResearchSpeed?.Value.ToString() ?? "";
        ProduceSpeedText = npc.ProduceSpeed?.Value.ToString() ?? "";
        CollectSpeedText = npc.CollectSpeed?.Value.ToString() ?? "";
        CarrySpeedText = npc.CarrySpeed?.Value.ToString() ?? "";
        PlantingSpeedText = npc.PlantingSpeed?.Value.ToString() ?? "";
        LoggingSpeedText = npc.LoggingSpeed?.Value.ToString() ?? "";
        CookingSpeedText = npc.CookingSpeed?.Value.ToString() ?? "";
        BreedingSpeedText = npc.BreedingSpeed?.Value.ToString() ?? "";
        GatherFoodSpeedText = npc.GatherFoodSpeed?.Value.ToString() ?? "";
    }

    private static void ApplyIfParsedUInt(string text, Action<uint> set)
    {
        if (!string.IsNullOrWhiteSpace(text) && uint.TryParse(text.Trim(), out var v)) set(v);
    }

    private static void ApplyIfParsedInt(string text, Action<int> set)
    {
        if (!string.IsNullOrWhiteSpace(text) && int.TryParse(text.Trim(), out var v)) set(v);
    }
}
