using System;
using Hex.Cheat.UI;
using UnityEngine;

namespace CheatMenuV2.Commands;

// Branch C — drive the existing BhResourcePanel.AddAllResource() entry point.
// See docs/recon-notes.md §"Selected dispatcher" for the rationale.
public class CommandDispatcher
{
    private bool             _located;
    private BhResourcePanel  _panel;
    private string           _failureReason;

    public bool EnsureLocated()
    {
        if (_located) return true;
        if (_failureReason != null) return false;

        try
        {
            // FindObjectsOfTypeAll includes inactive objects, so we'll see the panel
            // even if the dev tools were left disabled in the shipped scene hierarchy.
            var found = Resources.FindObjectsOfTypeAll<BhResourcePanel>();
            if (found == null || found.Length == 0)
            {
                _failureReason = "no BhResourcePanel instance in any loaded scene";
                Plugin.Log.LogWarning("CommandDispatcher: " + _failureReason);
                return false;
            }
            _panel = found[0];
            _located = true;
            Plugin.Log.LogInfo($"CommandDispatcher located BhResourcePanel ({found.Length} instance(s); using first: {_panel.gameObject.name})");
            return true;
        }
        catch (Exception e)
        {
            _failureReason = e.Message;
            Plugin.Log.LogError($"CommandDispatcher failed to locate dispatch site: {e}");
            return false;
        }
    }

    public void AddAllResources(int amount)
    {
        if (!EnsureLocated()) { Plugin.Overlay.Flash("dispatcher not located"); return; }
        WarnIfMultiplayer();
        try
        {
            // Populate the panel's seven input fields with the desired amount,
            // then trigger its existing buy-all logic.
            var fields = _panel.inputFields;
            if (fields != null)
            {
                var s = amount.ToString();
                for (int i = 0; i < fields.Count; i++)
                {
                    var tf = fields[i];
                    if (tf != null) tf.text = s;
                }
            }
            _panel.AddAllResource();
            Plugin.Log.LogInfo($"Dispatched AddAllResource amount={amount}");
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"AddAllResources failed: {e}");
            Plugin.Overlay.Flash("dispatch error — see log");
        }
    }

    private static void WarnIfMultiplayer()
    {
        // Spec §5.5 — log every cheat command. EOS / analytics may report state
        // changes; offline-only is the safe testing constraint.
        Plugin.Log.LogWarning("Issuing cheat command — confirm you are NOT in multiplayer/ranked/leaderboard mode (spec §5.5)");
    }
}
