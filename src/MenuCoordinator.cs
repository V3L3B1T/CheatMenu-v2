namespace CheatMenuV2;

public enum CheatMode { Probing, Native, Fallback, Failed }

public class MenuCoordinator
{
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
        // Task 9 will insert: NativePanelDriver.Open() first; Fallback only if Native fails.
        if (Plugin.Dispatcher.EnsureLocated())
        {
            Mode = CheatMode.Fallback;
            Plugin.Log.LogInfo($"Toggle ON — Mode={Mode} (dispatcher only, no native panel yet)");
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
        Plugin.Overlay.Flash("Menu closed");
        Plugin.Log.LogInfo("Toggle OFF");
    }
}
