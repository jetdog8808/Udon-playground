
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

public class udon_pedastal : UdonSharpBehaviour
{
    public VRC_AvatarPedestal pedestal;
    public InputField input;
    private VRCPlayerApi PlayerApiref;


    void Start()
    {
        if (pedestal == null)
        {
            pedestal = gameObject.GetComponent<VRC_AvatarPedestal>();
        }

        PlayerApiref = Networking.LocalPlayer;
    }

    void Interact()
    {
        use();
    }

    public void use()
    {
        pedestal.SetAvatarUse(PlayerApiref);
    }
}
