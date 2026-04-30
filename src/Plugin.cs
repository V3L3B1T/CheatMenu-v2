using System;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using CheatMenuV2.Commands;
using CheatMenuV2.Input;
using CheatMenuV2.UI;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace CheatMenuV2;

[BepInPlugin(Plugin.Guid, Plugin.Name, Plugin.Version)]
public class Plugin : BasePlugin
{
    public const string Guid    = "com.velebit.cheatmenuv2";
    public const string Name    = "CheatMenu-v2";
    public const string Version = "0.1.0";

    internal static new BepInEx.Logging.ManualLogSource Log = null;
    internal static GameObject  Host    = null;
    internal static OverlayUI   Overlay = null;
    internal static InputGate   Input   = null;
    internal static MenuCoordinator   Coordinator = null;
    internal static CommandDispatcher Dispatcher  = null;
    internal static BepInEx.Configuration.ConfigEntry<KeyCode> ToggleKey = null;

    public override void Load()
    {
        Log = base.Log;

        var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "unknown";
        var exeBuild = System.IO.File.Exists(exePath)
            ? System.IO.File.GetLastWriteTimeUtc(exePath).ToString("yyyy-MM-dd")
            : "unknown";
        Log.LogInfo($"Loaded — BepInEx 6 IL2CPP, target HeroesOldenEra build {exeBuild}");
        Log.LogInfo($"GUID={Guid} Version={Version}");

        ToggleKey = Config.Bind(
            section:      "Hotkeys",
            key:          "Toggle",
            defaultValue: KeyCode.F10,
            description:  "Key that toggles the cheat menu. Modifier keys (Shift/Ctrl/Alt) are ignored. F1 is reserved by the game's bug reporter — avoid it.");
        Log.LogInfo($"Toggle hotkey: {ToggleKey.Value}");

        // §4.5 load-bearing rule: register every MonoBehaviour subclass before AddComponent.
        ClassInjector.RegisterTypeInIl2Cpp<MenuHostBehaviour>();
        ClassInjector.RegisterTypeInIl2Cpp<OverlayUI>();
        ClassInjector.RegisterTypeInIl2Cpp<InputGate>();

        Host    = new GameObject($"{Name}-Host");
        Host.AddComponent<MenuHostBehaviour>();
        Overlay = Host.AddComponent<OverlayUI>();
        Input   = Host.AddComponent<InputGate>();
        UnityEngine.Object.DontDestroyOnLoad(Host);

        Input.Toggle = ToggleKey.Value;
        ToggleKey.SettingChanged += (_, _) => { Input.Toggle = ToggleKey.Value; Log.LogInfo($"Toggle hotkey changed to {ToggleKey.Value}"); };

        Coordinator = new MenuCoordinator();
        Dispatcher  = new CommandDispatcher();
        Input.OnTogglePressed = () => Coordinator.Toggle();

        Log.LogInfo("Host GameObject created; OverlayUI + InputGate attached; Coordinator wired");
    }
}
