using UnityEngine;

namespace CheatMenuV2.UI;

/// Marker component on the plugin's host GameObject. Lets us find the host across scene loads.
public class MenuHostBehaviour : MonoBehaviour
{
    public MenuHostBehaviour(System.IntPtr ptr) : base(ptr) { }
}
