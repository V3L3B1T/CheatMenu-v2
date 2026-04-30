using System;
using BepInEx;
using BepInEx.Unity.IL2CPP;
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
    internal static GameObject Host = null;

    public override void Load()
    {
        Log = base.Log;

        var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "unknown";
        var exeBuild = System.IO.File.Exists(exePath)
            ? System.IO.File.GetLastWriteTimeUtc(exePath).ToString("yyyy-MM-dd")
            : "unknown";
        Log.LogInfo($"Loaded — BepInEx 6 IL2CPP, target HeroesOldenEra build {exeBuild}");
        Log.LogInfo($"GUID={Guid} Version={Version}");

        // §4.5 load-bearing rule: register every MonoBehaviour subclass before AddComponent.
        ClassInjector.RegisterTypeInIl2Cpp<MenuHostBehaviour>();

        Host = new GameObject($"{Name}-Host");
        Host.AddComponent<MenuHostBehaviour>();
        UnityEngine.Object.DontDestroyOnLoad(Host);
        Log.LogInfo("Host GameObject created and marked DontDestroyOnLoad");
    }
}
