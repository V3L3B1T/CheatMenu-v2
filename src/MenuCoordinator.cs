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
        // Task 9 will replace this with: try NativePanelDriver.Open(); falls back to CommandDispatcher.
        // For now: flip to Failed so the overlay reflects we don't have backends wired.
        Mode = CheatMode.Failed;
        IsOpen = true;
        Plugin.Overlay.Flash("Menu open (no backend yet)");
        Plugin.Log.LogInfo($"Toggle ON — Mode={Mode}");
    }

    private void Close()
    {
        IsOpen = false;
        Plugin.Overlay.Flash("Menu closed");
        Plugin.Log.LogInfo("Toggle OFF");
    }
}
