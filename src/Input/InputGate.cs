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
        if (UnityEngine.Input.GetKeyDown(Toggle) && !IsModifierHeld() && !IsTextInputFocused())
        {
            if (OnTogglePressed != null) OnTogglePressed();
            else Plugin.Overlay.Flash($"{Toggle} pressed (no handler yet)");
        }
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
