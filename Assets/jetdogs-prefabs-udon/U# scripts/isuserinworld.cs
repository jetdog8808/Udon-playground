
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class isuserinworld : UdonSharpBehaviour
{
    public string user;
    public GameObject vis;

    private void Start()
    {
        vis.SetActive(false);
    }

    public void OnPlayerJoined(VRC.SDKBase.VRCPlayerApi player)
    {
        if (player.displayName == user)
        {
            vis.SetActive(true);
        }
    }

    public virtual void OnPlayerLeft(VRC.SDKBase.VRCPlayerApi player)
    {
        if (player.displayName == user)
        {
            vis.SetActive(false);
        }
    }
}
