using BepInEx.Configuration;

namespace PaxAutocraticaHelper;

/// <summary>
/// 全部可配置项（BepInEx 6 ConfigEntry，管理器可表单化编辑）
/// </summary>
internal static class ModConfig
{
    // ===== 通用 =====
    internal static ConfigEntry<bool> PanelEnabled;
    internal static ConfigEntry<bool> CheatSectionVisible;

    // ===== 面板 =====
    internal static ConfigEntry<float> PanelScale;
    internal static ConfigEntry<float> PanelX;
    internal static ConfigEntry<float> PanelY;
    internal static ConfigEntry<int> PanelWidth;
    internal static ConfigEntry<int> PanelHeight;
    internal static ConfigEntry<float> PanelShowDelay;

    // ===== 自动行为 =====
    internal static ConfigEntry<float> AutoAssignInterval;
    internal static ConfigEntry<float> SoldierPollInterval;
    internal static ConfigEntry<float> SoldierListRefreshInterval;

    // ===== 机器人强化 =====
    internal static ConfigEntry<bool> RobotBoostEnabled;
    internal static ConfigEntry<int> RobotEfasItem;
    internal static ConfigEntry<int> RobotBoostStamina;
    internal static ConfigEntry<int> RobotBoostFullness;
    internal static ConfigEntry<int> RobotBoostMood;
    internal static ConfigEntry<int> RobotBoostSupport;
    internal static ConfigEntry<int> RobotBoostFear;
    internal static ConfigEntry<uint> RobotBoostMinLevel;

    // ===== 作弊（默认隐藏） =====
    internal static ConfigEntry<bool> CheatGodMode;
    internal static ConfigEntry<bool> CheatDaddyMode;
    internal static ConfigEntry<bool> CheatStopAi;
    internal static ConfigEntry<bool> CheatCraftNoConsume;
    internal static ConfigEntry<bool> CheatUnlockCivilian;
    internal static ConfigEntry<bool> CheatAllDestruction;

    public static void Init(ConfigFile cfg)
    {
        // ===== 通用 =====
        PanelEnabled = cfg.Bind("通用", "启用便利面板", true, "是否启用游戏内便利面板（F1 开关）");
        CheatSectionVisible = cfg.Bind("通用", "显示作弊功能", false, "是否在面板中显示作弊类按钮（God/Daddy/停止AI 等），默认隐藏");

        // ===== 面板 =====
        PanelScale = cfg.Bind("面板", "界面缩放", 2f, "面板整体缩放倍数");
        PanelX = cfg.Bind("面板", "位置 X", 20f, "面板左上角 X（屏幕坐标）");
        PanelY = cfg.Bind("面板", "位置 Y", 60f, "面板左上角 Y（屏幕坐标）");
        PanelWidth = cfg.Bind("面板", "宽度", 330, "面板宽度（缩放前逻辑像素）");
        PanelHeight = cfg.Bind("面板", "高度", 470, "面板高度（缩放前逻辑像素）");
        PanelShowDelay = cfg.Bind("面板", "显示延迟(秒)", 8f, "游戏启动后多少秒才允许显示面板");

        // ===== 自动行为 =====
        AutoAssignInterval = cfg.Bind("自动行为", "自动分配间隔(秒)", 60f, "每隔多久自动触发一次全局人员自动分配（0 = 关闭）");
        SoldierPollInterval = cfg.Bind("自动行为", "士兵轮询间隔(秒)", 0.5f, "每隔多久轮询一次当前查看的士兵并同步面板");
        SoldierListRefreshInterval = cfg.Bind("自动行为", "士兵列表刷新间隔(秒)", 2f, "士兵列表的刷新冷却时间");

        // ===== 机器人强化 =====
        RobotBoostEnabled = cfg.Bind("机器人强化", "启用", true, "新生成/读取的机器人是否自动强化属性");
        RobotEfasItem = cfg.Bind("机器人强化", "机器人种族ID", 459, "被视为机器人的 EfasItem 种族 ID");
        RobotBoostStamina = cfg.Bind("机器人强化", "体力", 100, "强化后体力值");
        RobotBoostFullness = cfg.Bind("机器人强化", "饱食", 100, "强化后饱食值");
        RobotBoostMood = cfg.Bind("机器人强化", "心情", 100, "强化后心情值");
        RobotBoostSupport = cfg.Bind("机器人强化", "支持", 100, "强化后支持值");
        RobotBoostFear = cfg.Bind("机器人强化", "恐惧", 0, "强化后恐惧值");
        RobotBoostMinLevel = cfg.Bind("机器人强化", "最低等级", 100u, "强化后至少达到的等级");

        // ===== 作弊（默认隐藏） =====
        CheatGodMode = cfg.Bind("作弊", "God Mode", false, "面板显示 God Mode 按钮");
        CheatDaddyMode = cfg.Bind("作弊", "Daddy Mode", false, "面板显示 Daddy Mode 按钮");
        CheatStopAi = cfg.Bind("作弊", "停止敌军AI", false, "面板显示停止/恢复敌军AI按钮");
        CheatCraftNoConsume = cfg.Bind("作弊", "免费制造", false, "面板显示免费制造开关按钮");
        CheatUnlockCivilian = cfg.Bind("作弊", "解锁平民", false, "面板显示解锁平民按钮");
        CheatAllDestruction = cfg.Bind("作弊", "可拆所有建筑", false, "面板显示可拆所有建筑开关按钮");
    }
}
