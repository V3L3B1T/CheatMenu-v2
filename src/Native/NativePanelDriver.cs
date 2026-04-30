using System;
using Hex.Cheat;
using Hex.UI.City;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CheatMenuV2.Native;

public enum CheatContext { None, World, City, Battle }

public class NativePanelDriver
{
    public enum Result { Rendered, Failed }

    public bool         IsOpen         { get; private set; }
    public CheatContext CurrentContext { get; private set; } = CheatContext.None;

    private BhControllerCheat _cachedInstance;

    public Result Open()
    {
        CurrentContext = DetectContext();
        if (CurrentContext == CheatContext.None)
        {
            Plugin.Log.LogInfo("NativePanelDriver: no game session — skipping");
            return Result.Failed;
        }

        if (_cachedInstance == null) _cachedInstance = TryFind();
        if (_cachedInstance == null) return Result.Failed;

        try
        {
            _cachedInstance.gameObject.SetActive(true);
            // hrf() is the native Show entry on BhScreen subclasses; gik() is Hide.
            _cachedInstance.hrf();
            IsOpen = true;
            Plugin.Log.LogInfo($"NativePanelDriver: rendered ({CurrentContext})");
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
        if (_cachedInstance == null || !IsOpen) return;
        try
        {
            _cachedInstance.gik();
            _cachedInstance.gameObject.SetActive(false);
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"NativePanelDriver.Close failed: {e}");
        }
        IsOpen = false;
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

    private static BhControllerCheat TryFind()
    {
        var existing = Resources.FindObjectsOfTypeAll<BhControllerCheat>();
        if (existing != null && existing.Length > 0)
        {
            Plugin.Log.LogInfo($"NativePanelDriver: found {existing.Length} BhControllerCheat instance(s) (using first: {existing[0].gameObject.name})");
            return existing[0];
        }
        Plugin.Log.LogInfo("NativePanelDriver: no BhControllerCheat in any loaded scene — falling back to dispatcher");
        return null;
    }
}
