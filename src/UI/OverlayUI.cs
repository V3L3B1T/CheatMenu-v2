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

        var mode = Plugin.Coordinator?.Mode.ToString() ?? "Probing";
        var label = $"CheatMenu-v2 | Mode: {mode} | F10: toggle";
        if (_flashText != null && Time.unscaledTime < _flashUntilTime)
            label += $"  |  {_flashText}";
        else
            _flashText = null;

        const float width  = 360f;
        const float height = 24f;
        const float margin = 8f;
        var rect = new Rect(Screen.width - width - margin, margin, width, height);
        GUI.Box(rect, label);
        ConsumedRectsThisFrame.Add(rect);
    }
}
