using System;
using System.Linq;
using System.Text;
using Hex.Cheat;
using Hex.Cheat.UI;
using Hex.UI.City;
using UnityEngine;
using UnityEngine.SceneManagement;
using Il2CppInterop.Runtime;

namespace CheatMenuV2.Native;

public enum CheatContext { None, World, City, Battle }

public class NativePanelDriver
{
    public enum Result { Rendered, Failed }

    public bool         IsOpen         { get; private set; }
    public CheatContext CurrentContext { get; private set; } = CheatContext.None;

    // Two distinct targets — show goes through the controller, hide through the panel.
    private BhControllerCheat  _controller;
    private BhWindowCheatPanel _panel;

    private string _showGlyph;            // resolved 3-letter glyph on the controller (e.g. "hrl")
    private string _hideName = "CloseCheatPanel"; // semantic, observed on the panel; stays put unless rebinding fires

    private CheatContext _cachedContext = CheatContext.None;
    private bool _diagnosticDumped;

    // Glyphs we have NEVER seen do the right thing — refuse to bind to them.
    // Add to this list whenever a "succeeded but invisible" regression is diagnosed.
    private static readonly System.Collections.Generic.HashSet<string> ShowGlyphBlocklist =
        new() { "gio" };

    public Result Open()
    {
        var ctx = DetectContext();
        if (ctx == CheatContext.None)
        {
            Plugin.Log.LogInfo("NativePanelDriver: no game session — skipping");
            return Result.Failed;
        }

        // Re-resolve when context changes — different controller and panel per context.
        if (ctx != _cachedContext)
        {
            _controller = null; _panel = null; _showGlyph = null;
            _cachedContext = ctx;
        }
        CurrentContext = ctx;

        if (_controller == null || _panel == null) ResolveTargetsForContext(ctx);
        if (_controller == null) return Result.Failed; // hide-only without show is useless

        try
        {
            // Cached fast path
            if (_showGlyph != null)
            {
                if (TryInvokeNamed(_controller, _showGlyph) && PanelLooksOpen())
                {
                    IsOpen = true;
                    Plugin.Log.LogInfo($"NativePanelDriver: rendered ({CurrentContext}) via {_controller.GetIl2CppType().FullName}.{_showGlyph}() [cached]");
                    return Result.Rendered;
                }
                Plugin.Log.LogWarning($"NativePanelDriver: cached show '{_showGlyph}()' looked broken — re-resolving");
                _showGlyph = null;
            }

            // Re-scan — 3-letter glyphs only, on the controller.
            var resolved = ResolveShowGlyph(_controller);
            if (resolved == null)
            {
                DumpDiagnosticsOnce();
                return Result.Failed;
            }

            _showGlyph = resolved;
            IsOpen = true;
            Plugin.Log.LogInfo($"NativePanelDriver: rendered ({CurrentContext}) via {_controller.GetIl2CppType().FullName}.{_showGlyph}()");
            return Result.Rendered;
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"NativePanelDriver.Open failed: {e}");
            return Result.Failed;
        }
    }

    public void Close()
    {
        if (!IsOpen) { return; }
        try
        {
            // Hide via panel — never the controller.
            if (_panel != null && TryInvokeNamed(_panel, _hideName))
            {
                Plugin.Log.LogInfo($"NativePanelDriver: hidden via {_panel.GetIl2CppType().FullName}.{_hideName}() [cached]");
            }
            else if (_panel != null)
            {
                // Fallback: 3-letter scan on the panel (NOT the controller).
                var resolved = ResolveHideOnPanel(_panel);
                if (resolved != null)
                {
                    _hideName = resolved;
                    Plugin.Log.LogWarning($"NativePanelDriver: hidden via panel escape hatch — {_hideName}()");
                }
                else
                {
                    Plugin.Log.LogWarning("NativePanelDriver: no hide method on panel — leaving panel as-is (do NOT call Close on controller)");
                }
            }
        }
        catch (Exception e) { Plugin.Log.LogWarning($"NativePanelDriver.Close failed: {e}"); }
        IsOpen = false;
    }

    // ---- discovery -----------------------------------------------------

    private void ResolveTargetsForContext(CheatContext ctx)
    {
        // Controller (show target)
        var controllers = Resources.FindObjectsOfTypeAll<BhControllerCheat>();
        if (controllers != null && controllers.Length > 0)
        {
            string wantName = ctx switch
            {
                CheatContext.World  => "CheatWorld",
                CheatContext.City   => "CheatCity",
                CheatContext.Battle => "CheatBattle",
                _                    => null
            };
            BhControllerCheat pickC = null;
            for (int i = 0; i < controllers.Length; i++)
            {
                if (controllers[i] == null) continue;
                if (wantName != null && controllers[i].gameObject.name == wantName) { pickC = controllers[i]; break; }
            }
            _controller = pickC ?? controllers[0];
            Plugin.Log.LogInfo($"NativePanelDriver: controller = {_controller.GetIl2CppType().FullName} '{_controller.gameObject.name}' (count={controllers.Length})");
        }
        else
        {
            Plugin.Log.LogWarning("NativePanelDriver: no BhControllerCheat in scene");
        }

        // Panel (hide target)
        var panels = Resources.FindObjectsOfTypeAll<BhWindowCheatPanel>();
        if (panels != null && panels.Length > 0)
        {
            string wantTypeName = ctx switch
            {
                CheatContext.World  => "BhWindowWorld",
                CheatContext.City   => "BhWindowCity",
                CheatContext.Battle => "BhWindowBattle",
                _                    => null
            };
            BhWindowCheatPanel pickP = null;
            for (int i = 0; i < panels.Length; i++)
            {
                if (panels[i] == null) continue;
                if (wantTypeName != null && panels[i].GetIl2CppType().Name == wantTypeName) { pickP = panels[i]; break; }
            }
            _panel = pickP ?? panels[0];
            Plugin.Log.LogInfo($"NativePanelDriver: panel = {_panel.GetIl2CppType().FullName} '{_panel.gameObject.name}' (count={panels.Length})");
        }
        else
        {
            Plugin.Log.LogWarning("NativePanelDriver: no BhWindowCheatPanel in scene");
        }
    }

    // ---- ladder helpers -----------------------------------------------

    private string ResolveShowGlyph(BhControllerCheat controller)
    {
        var t = controller.GetIl2CppType();
        var glyphs = VoidNoArgPublicInstance(t)
            .Where(m => m.Name != null && m.Name.Length == 3 && char.IsLower(m.Name[0]))
            .Where(m => !ShowGlyphBlocklist.Contains(m.Name))
            .ToArray();

        foreach (var m in glyphs)
        {
            if (TryInvokeNamed(controller, m.Name) && PanelLooksOpen())
            {
                Plugin.Log.LogWarning($"NativePanelDriver: show bound — {m.Name}()");
                return m.Name;
            }
        }
        return null;
    }

    private string ResolveHideOnPanel(BhWindowCheatPanel panel)
    {
        var t = panel.GetIl2CppType();
        var candidates = VoidNoArgPublicInstance(t).ToArray();

        // Prefer the semantic name we observed before.
        if (candidates.Any(x => x.Name == "CloseCheatPanel") && TryInvokeNamed(panel, "CloseCheatPanel"))
            return "CloseCheatPanel";

        // Else any 3-letter glyph on the panel.
        foreach (var m in candidates.Where(x => x.Name != null && x.Name.Length == 3 && char.IsLower(x.Name[0])))
            if (TryInvokeNamed(panel, m.Name)) return m.Name;

        return null;
    }

    private bool PanelLooksOpen()
    {
        if (_panel == null) return true; // can't validate; trust the call
        try { return _panel.gameObject != null && _panel.gameObject.activeInHierarchy; }
        catch { return true; }
    }

    private static Il2CppSystem.Reflection.MethodInfo[] VoidNoArgPublicInstance(Il2CppSystem.Type t)
        => t.GetMethods()
            .Where(m => m != null && m.IsPublic && !m.IsStatic
                     && m.GetParameters().Length == 0
                     && m.ReturnType != null && m.ReturnType.Name == "Void")
            .ToArray();

    private static bool TryInvokeNamed(Il2CppSystem.Object target, string name)
    {
        try
        {
            var t = target.GetIl2CppType();
            var m = t.GetMethod(name);
            if (m == null) return false;
            m.Invoke(target, null);
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogDebug($"  {name}() threw: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    // ---- diagnostics ---------------------------------------------------

    private void DumpDiagnosticsOnce()
    {
        if (_diagnosticDumped) return;
        _diagnosticDumped = true;
        if (_controller != null) DumpType(_controller, "controller");
        if (_panel != null) DumpType(_panel, "panel");
    }

    private static void DumpType(Il2CppSystem.Object target, string label)
    {
        try
        {
            var t = target.GetIl2CppType();
            var sb = new StringBuilder();
            sb.Append("NativePanelDriver:DIAG ").Append(label).Append(' ').Append(t.FullName).Append('\n');
            sb.Append("  void()-methods: ");
            foreach (var m in t.GetMethods()
                .Where(x => x != null && x.IsPublic && !x.IsStatic
                         && x.GetParameters().Length == 0
                         && x.ReturnType != null && x.ReturnType.Name == "Void")
                .OrderBy(x => x.Name)) sb.Append(m.Name).Append(' ');
            Plugin.Log.LogError(sb.ToString());
        }
        catch (Exception e) { Plugin.Log.LogWarning($"NativePanelDriver:DIAG dump on {label} threw: {e.Message}"); }
    }

    private static CheatContext DetectContext()
    {
        var sceneName = (SceneManager.GetActiveScene().name ?? "").ToLowerInvariant();
        if (sceneName.Contains("battle") || sceneName.Contains("arena"))
            return CheatContext.Battle;

        var cityViews = Resources.FindObjectsOfTypeAll<BhCityHeroesGroupView>();
        if (cityViews == null || cityViews.Length == 0)
        {
            if (sceneName.Contains("menu") || sceneName.Contains("title") || sceneName.Contains("loading"))
                return CheatContext.None;
            return CheatContext.World;
        }

        for (int i = 0; i < cityViews.Length; i++)
            if (cityViews[i] != null && cityViews[i].gameObject.activeInHierarchy)
                return CheatContext.City;

        return CheatContext.World;
    }
}
