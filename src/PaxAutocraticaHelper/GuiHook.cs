using System;
using UnityEngine;

namespace PaxAutocraticaHelper;

/// <summary>
/// 游戏内士兵管理面板（IMGUI）。
/// 按需求精简：面板只做士兵管理；其他功能（时间加速/建筑效率/研究/作弊等）
/// 全部通过快捷键使用（见 README 快捷键表）。
/// </summary>
internal static class GuiHook
{
    private static Vector2 _scrollPos;
    private static Vector2 _soldierListScroll;

    // ===== 面板拖动状态 =====
    private static bool _dragging;
    private static Vector2 _dragOffset;
    /// <summary>标题栏高度（缩放前）</summary>
    private const float TitleBarHeight = 26f;

    /// <summary>面板不透明背景色（避免与游戏画面重叠混淆）</summary>
    private static readonly Color PanelBgColor = new(0.09f, 0.09f, 0.13f, 0.97f);
    private static Texture2D? _panelBgTex;

    private static Texture2D PanelBgTexture()
    {
        if (_panelBgTex == null)
        {
            _panelBgTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _panelBgTex.SetPixel(0, 0, PanelBgColor);
            _panelBgTex.Apply();
        }
        return _panelBgTex;
    }

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

            // ===== 标题栏拖动 =====
            // 注意：Event.mousePosition 在 GUI.matrix 缩放下是 GUI 空间坐标（屏幕坐标/scale），
            // 面板 Rect 也是 GUI 空间坐标，两者直接比较。
            var titleRect = new Rect(ModConfig.PanelX.Value, ModConfig.PanelY.Value, width, TitleBarHeight);
            var ev = Event.current;
            if (ev.type == EventType.MouseDown && ev.button == 0)
            {
                var hit = titleRect.Contains(ev.mousePosition);
                if (hit)
                {
                    _dragging = true;
                    _dragOffset = ev.mousePosition - new Vector2(ModConfig.PanelX.Value, ModConfig.PanelY.Value);
                    ev.Use();
                }
            }
            else if (ev.type == EventType.MouseUp && _dragging)
            {
                _dragging = false;
                ev.Use();
            }
            // 拖动中：Input.mousePosition 是屏幕坐标（左下原点），转 GUI 空间（左上原点 / scale）
            if (_dragging)
            {
                var im = Input.mousePosition;
                var gx = im.x / scale;
                var gy = (Screen.height - im.y) / scale;
                var newX = Mathf.Max(0f, gx - _dragOffset.x);
                var newY = Mathf.Max(0f, gy - _dragOffset.y);
                newY = Mathf.Min(newY, Screen.height / scale - TitleBarHeight);
                ModConfig.PanelX.Value = newX;
                ModConfig.PanelY.Value = newY;
            }

            GUILayout.BeginArea(new Rect(ModConfig.PanelX.Value, ModConfig.PanelY.Value, width, height), GUIContent.none, GUI.skin.box);
            // 不透明背景垫底：整个面板区域（含滚动区）不再透出游戏画面
            GUI.DrawTexture(new Rect(0f, 0f, width, height), PanelBgTexture(), ScaleMode.StretchToFill);
            // 标题栏（可拖动区域）
            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>士兵管理面板</b>（按住此处拖动）");
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(2f);
            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Width(width), GUILayout.Height(height - TitleBarHeight - 6f));

            GUILayout.Label("<b>══ 快捷键说明 ══</b>");
            GUILayout.Label("F1：开关士兵管理面板");
            GUILayout.Label("Ctrl+1：时间倍速切换（1/2/5/10x）");
            GUILayout.Label("Ctrl+2：完成所有研究");
            GUILayout.Label("Ctrl+3：所有单位恐惧归零");
            if (ModConfig.CheatSectionVisible.Value)
            {
                GUILayout.Label("Ctrl+7：God Mode（无敌）");
                GUILayout.Label("Ctrl+8：Daddy Mode");
            }
            GUILayout.Label("游戏士兵页点选任意单位后，属性自动同步到面板");
            GUILayout.Space(4f);

            // ===== 士兵列表 =====
            GUILayout.Label("<b>══ 士兵列表 ══</b>");
            GUILayout.Label($"共 {SoldierManager.SoldierEntries.Count} 名（点击选中并载入属性）");
            _soldierListScroll = GUILayout.BeginScrollView(_soldierListScroll, GUILayout.Height(120f));
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

            GUILayout.Label("采摘速度");
            GUILayout.BeginHorizontal();
            SoldierManager.GatherFoodSpeedText = GUILayout.TextField(SoldierManager.GatherFoodSpeedText, GUILayout.Width(60f));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("复制当前士兵 (F2)", GUILayout.Height(24f))) SoldierManager.CopyCurrentSoldier();
            if (GUILayout.Button("应用属性 (F3)", GUILayout.Height(24f))) SoldierManager.ApplyAttributes();
            GUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(SoldierManager.StatusText))
            {
                GUILayout.Label($"<color=yellow>{SoldierManager.StatusText}</color>");
            }

            GUILayout.Space(6f);
            GUILayout.Label("<color=grey>选中士兵后属性自动同步到上方输入框，改完点「应用属性」生效</color>");
            GUILayout.Space(8f);
            GUILayout.Label("<color=grey>F1 开关面板 · 其他功能见快捷键（Ctrl+1~0）</color>");
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
