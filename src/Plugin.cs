using System;
using BepInEx;
using BepInEx.Unity.IL2CPP;

namespace CheatMenuV2;

[BepInPlugin(Plugin.Guid, Plugin.Name, Plugin.Version)]
public class Plugin : BasePlugin
{
    public const string Guid    = "com.velebit.cheatmenuv2";
    public const string Name    = "CheatMenu-v2";
    public const string Version = "0.1.0";

    internal static new BepInEx.Logging.ManualLogSource Log = null!;

    public override void Load()
    {
        Log = base.Log;
        var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "unknown";
        var exeBuild = System.IO.File.Exists(exePath)
            ? System.IO.File.GetLastWriteTimeUtc(exePath).ToString("yyyy-MM-dd")
            : "unknown";
        Log.LogInfo($"Loaded — BepInEx 6 IL2CPP, target HeroesOldenEra build {exeBuild}");
        Log.LogInfo($"GUID={Guid} Version={Version}");
    }
}
