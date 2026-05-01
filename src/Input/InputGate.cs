using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CheatMenuV2.Input;

public class InputGate : MonoBehaviour
{
    public InputGate(IntPtr ptr) : base(ptr) { }

    public KeyCode Toggle = KeyCode.F10;

    // Wired in Task 6 — for now, just flash via the overlay.
    public Action OnTogglePressed;

    private void Update()
    {
        // Hotkey toggle
        if (UnityEngine.Input.GetKeyDown(Toggle) && !IsModifierHeld() && !IsTextInputFocused())
        {
            if (OnTogglePressed != null) OnTogglePressed();
            else Plugin.Overlay.Flash($"{Toggle} pressed (no handler yet)");
        }

        // Click-through prevention: if any mouse button is down and the cursor sits
        // over our IMGUI, swallow the input so the game's raycasters don't see it.
        if (UnityEngine.Input.GetMouseButton(0) || UnityEngine.Input.GetMouseButton(1) || UnityEngine.Input.GetMouseButton(2))
        {
            if (CursorOverOurUI()) UnityEngine.Input.ResetInputAxes();
        }
    }

    private static bool CursorOverOurUI()
    {
        // EventSystem catches uGUI elements layered above us
        var es = EventSystem.current;
        if (es != null && es.IsPointerOverGameObject()) return true;

        // IMGUI rect check — IMGUI doesn't participate in EventSystem so this is the
        // actual mechanism. IMGUI uses top-left origin; Input.mousePosition uses
        // bottom-left, so flip y.
        var rects = Plugin.Overlay?.ConsumedRectsThisFrame;
        if (rects == null || rects.Count == 0) return false;

        var p = UnityEngine.Input.mousePosition;
        var imguiP = new Vector2(p.x, Screen.height - p.y);
        for (int i = 0; i < rects.Count; i++)
            if (rects[i].Contains(imguiP)) return true;
        return false;
    }

    private static bool IsModifierHeld()
    {
        return UnityEngine.Input.GetKey(KeyCode.LeftShift)   || UnityEngine.Input.GetKey(KeyCode.RightShift)
            || UnityEngine.Input.GetKey(KeyCode.LeftControl) || UnityEngine.Input.GetKey(KeyCode.RightControl)
            || UnityEngine.Input.GetKey(KeyCode.LeftAlt)     || UnityEngine.Input.GetKey(KeyCode.RightAlt);
    }

    private static bool IsTextInputFocused()
    {
        var es = EventSystem.current;
        if (es == null) return false;
        var sel = es.currentSelectedGameObject;
        if (sel == null) return false;
        if (sel.GetComponent<InputField>() != null) return true;
        if (sel.GetComponent<TMPro.TMP_InputField>() != null) return true;
        return false;
    }
}
