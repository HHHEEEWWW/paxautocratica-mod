using System;
using Multiverse.EFAS.NpcSimulator;
using UnityEngine;

namespace PaxAutocraticaHelper;

/// <summary>
/// 游戏内便利面板（IMGUI）。布局/数值全部配置化，作弊按钮默认隐藏。
/// </summary>
internal static class GuiHook
{
    private static Vector2 _scrollPos;
    private static Vector2 _soldierListScroll;

    internal static void DrawPanel()
    {
        if (!ModConfig.PanelEnabled.Value) return;
        if (!PaxPlugin.ShowWindow || Time.time < ModConfig.PanelShowDelay.Value) return;

        var backgroundColor = GUI.backgroundColor;
        var contentColor = GUI.contentColor;
        var matrix = GUI.matrix;
        try
        {
            var scale = ModConfig.PanelScale.Value;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            GUI.backgroundColor = new Color(0.15f, 0.15f, 0.2f, 0.9f);
            GUI.contentColor = Color.white;

            var width = ModConfig.PanelWidth.Value;
            var height = ModConfig.PanelHeight.Value;
            GUILayout.BeginArea(new Rect(ModConfig.PanelX.Value, ModConfig.PanelY.Value, width, height), GUIContent.none, GUI.skin.box);
            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Width(width), GUILayout.Height(height));

            GUILayout.Label("<b>暗星铁律 便利面板</b>");
            GUILayout.Space(4f);

            // ===== 士兵管理 =====
            GUILayout.Label("<b>══ 士兵管理 ══</b>");
            GUILayout.Label($"士兵列表（{SoldierManager.SoldierEntries.Count}）");
            SoldierManager.RefreshSoldierList();
            _soldierListScroll = GUILayout.BeginScrollView(_soldierListScroll, GUILayout.Height(110f));
            foreach (var entry in SoldierManager.SoldierEntries)
            {
                if (GUILayout.Button(entry.Label, GUILayout.Height(22f)))
                {
                    SoldierManager.SelectFromList(entry.Id);
                }
            }
            GUILayout.EndScrollView();

            var detail = SoldierManager.CurrentDetailNpc;
            var detailText = detail == null ? "（未选择士兵）" : $"{detail.Name}  Lv.{detail.Level}  id={detail.Id}";
            GUILayout.Label("当前: " + detailText);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("复制当前士兵 (F2)", GUILayout.Height(24f))) SoldierManager.CopyCurrentSoldier();
            if (GUILayout.Button("同步到面板", GUILayout.Height(24f))) SoldierManager.SyncPanelFromCurrent();
            GUILayout.EndHorizontal();

            GUILayout.Space(2f);
            GUILayout.Label("等级 / 经验");
            GUILayout.BeginHorizontal();
            SoldierManager.LevelText = GUILayout.TextField(SoldierManager.LevelText, GUILayout.Width(80f));
            SoldierManager.ExpText = GUILayout.TextField(SoldierManager.ExpText, GUILayout.Width(80f));
            GUILayout.EndHorizontal();

            GUILayout.Label("体力 / 饱食 / 心情");
            GUILayout.BeginHorizontal();
            SoldierManager.StaminaText = GUILayout.TextField(SoldierManager.StaminaText, GUILayout.Width(60f));
            SoldierManager.FullnessText = GUILayout.TextField(SoldierManager.FullnessText, GUILayout.Width(60f));
            SoldierManager.MoodText = GUILayout.TextField(SoldierManager.MoodText, GUILayout.Width(60f));
            GUILayout.EndHorizontal();

            GUILayout.Label("支持 / 恐惧 / 金币");
            GUILayout.BeginHorizontal();
            SoldierManager.SupportText = GUILayout.TextField(SoldierManager.SupportText, GUILayout.Width(60f));
            SoldierManager.FearText = GUILayout.TextField(SoldierManager.FearText, GUILayout.Width(60f));
            SoldierManager.GoldText = GUILayout.TextField(SoldierManager.GoldText, GUILayout.Width(60f));
            GUILayout.EndHorizontal();

            GUILayout.Label("工资 / 制造 / 研究 / 生产 / 采集(采矿)速度");
            GUILayout.BeginHorizontal();
            SoldierManager.WageText = GUILayout.TextField(SoldierManager.WageText, GUILayout.Width(54f));
            SoldierManager.CraftSpeedText = GUILayout.TextField(SoldierManager.CraftSpeedText, GUILayout.Width(54f));
            SoldierManager.ResearchSpeedText = GUILayout.TextField(SoldierManager.ResearchSpeedText, GUILayout.Width(54f));
            SoldierManager.ProduceSpeedText = GUILayout.TextField(SoldierManager.ProduceSpeedText, GUILayout.Width(54f));
            SoldierManager.CollectSpeedText = GUILayout.TextField(SoldierManager.CollectSpeedText, GUILayout.Width(54f));
            GUILayout.EndHorizontal();

            GUILayout.Label("搬运 / 种植 / 伐木 / 烹饪 / 繁殖速度");
            GUILayout.BeginHorizontal();
            SoldierManager.CarrySpeedText = GUILayout.TextField(SoldierManager.CarrySpeedText, GUILayout.Width(54f));
            SoldierManager.PlantingSpeedText = GUILayout.TextField(SoldierManager.PlantingSpeedText, GUILayout.Width(54f));
            SoldierManager.LoggingSpeedText = GUILayout.TextField(SoldierManager.LoggingSpeedText, GUILayout.Width(54f));
            SoldierManager.CookingSpeedText = GUILayout.TextField(SoldierManager.CookingSpeedText, GUILayout.Width(54f));
            SoldierManager.BreedingSpeedText = GUILayout.TextField(SoldierManager.BreedingSpeedText, GUILayout.Width(54f));
            GUILayout.EndHorizontal();

            if (GUILayout.Button("应用属性 (F3)", GUILayout.Height(24f))) SoldierManager.ApplyAttributes();
            if (!string.IsNullOrEmpty(SoldierManager.StatusText))
            {
                GUILayout.Label($"<color=yellow>{SoldierManager.StatusText}</color>");
            }

            GUILayout.Space(6f);
            GUILayout.Label("— — — — 其他功能 — — — —");
            GUILayout.Space(2f);

            // 时间加速
            GUILayout.Label("时间加速 (SetTimeScale)");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("1x")) PaxPlugin.Exec("SetTimeScale 1");
            if (GUILayout.Button("2x")) PaxPlugin.Exec("SetTimeScale 2");
            if (GUILayout.Button("5x")) PaxPlugin.Exec("SetTimeScale 5");
            if (GUILayout.Button("10x")) PaxPlugin.Exec("SetTimeScale 10");
            GUILayout.EndHorizontal();

            // 建筑效率
            GUILayout.Label("建筑效率倍率 (BuildingWorkDebug)");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("x1")) PaxPlugin.Exec("BuildingWorkDebug 1");
            if (GUILayout.Button("x5")) PaxPlugin.Exec("BuildingWorkDebug 5");
            if (GUILayout.Button("x10")) PaxPlugin.Exec("BuildingWorkDebug 10");
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("完成所有研究")) PaxPlugin.Exec("CompleteAllResearching");
            if (GUILayout.Button("自动保存")) PaxPlugin.Exec("AutoSave");
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("生成机器人 (Ctrl+`)")) NpcAutoAssign.SpawnRobots(ModConfig.RobotSpawnCount.Value);
            if (GUILayout.Button("智能自动分配 (Ctrl+0)")) NpcAutoAssign.AutoAssignAll();
            GUILayout.EndHorizontal();

            // ===== 作弊区（默认隐藏，配置开启后显示） =====
            if (ModConfig.CheatSectionVisible.Value)
            {
                GUILayout.Space(6f);
                GUILayout.Label("<color=orange>══ 作弊功能 ══</color>");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("免费制造 开")) PaxPlugin.Exec("CraftNoConsume 1");
                if (GUILayout.Button("免费制造 关")) PaxPlugin.Exec("CraftNoConsume 0");
                GUILayout.EndHorizontal();
                if (ModConfig.CheatAllDestruction.Value)
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("可拆所有建筑 开")) PaxPlugin.Exec("CraftDebugAllDestruction 1");
                    if (GUILayout.Button("可拆所有建筑 关")) PaxPlugin.Exec("CraftDebugAllDestruction 0");
                    GUILayout.EndHorizontal();
                }
                if (ModConfig.CheatGodMode.Value)
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("GOD MODE")) PaxPlugin.Exec("TestGod");
                    if (GUILayout.Button("Daddy mode")) PaxPlugin.Exec("TestDaddy");
                    GUILayout.EndHorizontal();
                }
                if (ModConfig.CheatStopAi.Value)
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("停止敌军AI")) PaxPlugin.Exec("SetStopAiThink 1");
                    if (GUILayout.Button("恢复AI")) PaxPlugin.Exec("SetStopAiThink -1");
                    GUILayout.EndHorizontal();
                }
                if (ModConfig.CheatUnlockCivilian.Value)
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("解锁平民")) PaxPlugin.Exec("UnlockCivilian");
                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.Space(8f);
            GUILayout.Label("<color=grey>F1 开关面板 · Ctrl+1~9 快捷键 · 配置见 BepInEx/config</color>");
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
        catch (Exception ex)
        {
            PaxPlugin.Log.LogError($"OnGui exception: {ex}");
        }
        finally
        {
            GUI.backgroundColor = backgroundColor;
            GUI.contentColor = contentColor;
            GUI.matrix = matrix;
        }
    }
}
