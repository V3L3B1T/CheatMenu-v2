using CheatMenuV2.Native;

namespace CheatMenuV2;

public enum CheatMode { Probing, Native, Fallback, Failed }

public class MenuCoordinator
{
    public readonly NativePanelDriver Native = new NativePanelDriver();

    public CheatMode Mode { get; private set; } = CheatMode.Probing;
    public bool      IsOpen { get; private set; }

    public void Toggle()
    {
        if (IsOpen) { Close(); return; }
        Open();
    }

    private void Open()
    {
        IsOpen = true;

        // Try Native first.
        var nativeResult = Native.Open();
        if (nativeResult == NativePanelDriver.Result.Rendered)
        {
            Mode = CheatMode.Native;
            Plugin.Log.LogInfo($"Toggle ON — Mode={Mode}");
            Plugin.Overlay.Flash($"Mode={Mode}");
            return;
        }

        // Native unavailable — try Fallback.
        if (Plugin.Dispatcher.EnsureLocated())
        {
            Mode = CheatMode.Fallback;
            Plugin.Log.LogInfo($"Toggle ON — Mode={Mode} (native unavailable)");
        }
        else
        {
            Mode = CheatMode.Failed;
            Plugin.Log.LogError("Toggle ON — both backends unavailable");
        }
        Plugin.Overlay.Flash($"Mode={Mode}");
    }

    private void Close()
    {
        IsOpen = false;
        if (Mode == CheatMode.Native) Native.Close();
        Plugin.Overlay.Flash("Menu closed");
        Plugin.Log.LogInfo("Toggle OFF");
    }
}
