using BepInEx;
using BepInEx.Unity.IL2CPP;

namespace CheatMenuV2;

[BepInPlugin(Plugin.Guid, Plugin.Name, Plugin.Version)]
public class Plugin : BasePlugin
{
    public const string Guid    = "com.velebit.cheatmenuv2";
    public const string Name    = "CheatMenu-v2";
    public const string Version = "0.1.0";

    public override void Load()
    {
        Log.LogInfo($"{Name} v{Version} placeholder — Task 0 scaffold");
    }
}
