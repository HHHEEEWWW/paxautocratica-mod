using UnityEngine;

namespace PaxAutocraticaHelper;

/// <summary>
/// IMGUI 面板宿主 + 每帧驱动（按键/定时任务）。
/// </summary>
public class PanelBehaviour : MonoBehaviour
{
    private void Update()
    {
        FrameHook.Update();
    }

    private void OnGUI()
    {
        GuiHook.DrawPanel();
    }
}
