using System;
using System.Collections.Generic;
using UnityEngine;

namespace CheatMenuV2.UI;

public class OverlayUI : MonoBehaviour
{
    public OverlayUI(IntPtr ptr) : base(ptr) { }


    // Flash messages: short-lived overlay text.
    private string _flashText;
    private float  _flashUntilTime;

    // Per-frame record of drawn rects, used for click-through prevention (Task 12).
    // Exposed as a plain public field, NOT a property — Il2CppInterop tries to
    // generate a getter proxy for any property on an injected MonoBehaviour and
    // warns when the return type isn't marshallable. A plain field skips that.
    public readonly List<Rect> ConsumedRectsThisFrame = new List<Rect>(8);

    public void Flash(string message, float seconds = 2f)
    {
        _flashText      = message;
        _flashUntilTime = Time.unscaledTime + seconds;
    }

    private void OnGUI()
    {
        ConsumedRectsThisFrame.Clear();

        // ── Status strip (always on) ──
        var mode = Plugin.Coordinator?.Mode.ToString() ?? "Probing";
        var label = $"CheatMenu-v2 | Mode: {mode} | F10: toggle";
        if (_flashText != null && Time.unscaledTime < _flashUntilTime)
            label += $"  |  {_flashText}";
        else
            _flashText = null;

        const float stripW = 360f, stripH = 24f, margin = 8f;
        var stripRect = new Rect(Screen.width - stripW - margin, margin, stripW, stripH);
        GUI.Box(stripRect, label);
        ConsumedRectsThisFrame.Add(stripRect);

        // ── Fallback button strip (only when Mode == Fallback && IsOpen) ──
        var coord = Plugin.Coordinator;
        if (coord == null || coord.Mode != CheatMode.Fallback || !coord.IsOpen) return;

        const float panelW = 260f, panelH = 76f;
        var panelRect = new Rect(margin, margin, panelW, panelH);
        GUI.Box(panelRect, "Cheats (Fallback)");
        ConsumedRectsThisFrame.Add(panelRect);

        var btnRect = new Rect(margin + 8, margin + 28, panelW - 16, 32);
        if (GUI.Button(btnRect, "+10000 Gold (all resources)"))
        {
            Plugin.Dispatcher.AddAllResources(10000);
            Flash("+10000 res", 1.5f);
        }
        ConsumedRectsThisFrame.Add(btnRect);
    }
}
