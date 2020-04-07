
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class networkingtest : UdonSharpBehaviour
{
    [UdonSynced,]
    public bool testval = true;
    public GameObject state;
    public GameObject pres;
    public GameObject des;

    public GameObject netall;

    public GameObject prop;
    public Transform loc;

    public UdonBehaviour udonBehaviour;
    public VRCPlayerApi PlayerApiref;

    void Start()
    {
        PlayerApiref = Networking.LocalPlayer;
        udonBehaviour = (UdonBehaviour)GetComponent(typeof(UdonBehaviour));

    }

    void Interact()
    {
        udonBehaviour.SetProgramVariable("testval", !testval);
        udonBehaviour.SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "netobj");
        
    }

    private void Update()
    {
        state.SetActive(testval);
    }
    /*
    public void OnPreSerialization()
    {
        pres.SetActive(!pres.activeSelf);
    }

    public void OnDeserialization()
    {
        des.SetActive(!des.activeSelf);
    }
    */
    public void netobj()
    {
        netall.SetActive(!netall.activeSelf);
    }
}
