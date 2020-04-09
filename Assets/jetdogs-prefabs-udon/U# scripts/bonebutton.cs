
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class bonebutton : UdonSharpBehaviour
{
    public UdonBehaviour boneposition;
    public VRCPlayerApi PlayerApiref;

    private void Start()
    {
        PlayerApiref = Networking.LocalPlayer;
    }

    void Interact()
    {
        if (Networking.IsOwner(boneposition.gameObject))
        {

            bool track = (bool)boneposition.GetProgramVariable("track");
            boneposition.SetProgramVariable("track", !track);
        }
        else
        {
            Networking.SetOwner(PlayerApiref, boneposition.gameObject);
            boneposition.SetProgramVariable("localwait", true);
        }
    }
}
