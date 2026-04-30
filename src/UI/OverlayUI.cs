using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CheatMenuV2.UI;

public class OverlayUI : MonoBehaviour
{
    public OverlayUI(IntPtr ptr) : base(ptr) { }

    // Public state — set by MenuCoordinator in later tasks.
    public string ModeText = "Probing";

    // Flash messages: short-lived overlay text.
    private string _flashText;
    private float  _flashUntilTime;

    // Per-frame record of drawn rects, used for click-through prevention (Task 12).
    // Exposed as Rect[] (not IReadOnlyList) because Il2CppInterop can't marshal
    // generic interfaces across the IL2CPP boundary.
    private readonly List<Rect> _rectsThisFrame = new List<Rect>(8);
    public Rect[] ConsumedRectsThisFrame => _rectsThisFrame.ToArray();

    public void Flash(string message, float seconds = 2f)
    {
        _flashText      = message;
        _flashUntilTime = Time.unscaledTime + seconds;
    }

    private void OnGUI()
    {
        _rectsThisFrame.Clear();

        var label = $"CheatMenu-v2 | Mode: {ModeText} | F10: toggle";
        if (_flashText != null && Time.unscaledTime < _flashUntilTime)
            label += $"  |  {_flashText}";
        else
            _flashText = null;

        const float width  = 360f;
        const float height = 24f;
        const float margin = 8f;
        var rect = new Rect(Screen.width - width - margin, margin, width, height);
        GUI.Box(rect, label);
        _rectsThisFrame.Add(rect);
    }
}
