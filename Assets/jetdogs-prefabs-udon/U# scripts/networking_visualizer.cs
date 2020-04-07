
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

public class networking_visualizer : UdonSharpBehaviour
{
    public VRCPlayerApi PlayerApiref;

    public Text networksettled;
    public Text ismaster;
    public Text netdatatime;
    public double prevtime = 0.0;
    public Text servertimesec;
    public Text serverdeltatime;

    public Text ownername;
    public Text isowner;
    public Text objready;
    public Text uniquename;

    private void Start()
    {
        PlayerApiref = Networking.LocalPlayer;
    }

    void Update()
    {
        double secs = Networking.GetServerTimeInSeconds();
        networksettled.text = "isnetworksettled: " + Networking.IsNetworkSettled.ToString();
        ismaster.text = "ismaster: " + Networking.IsMaster.ToString();
        netdatatime.text = "networkdatatime: " + Networking.GetNetworkDateTime().ToString();
        servertimesec.text = "servertimeinseconds: " + secs.ToString();
        serverdeltatime.text = "serverdeltatime: " + Networking.CalculateServerDeltaTime(secs, prevtime).ToString();
        prevtime = secs;


    }

    private void OnTriggerEnter(Collider other)
    {
        if (other != null)
        {
            GameObject objselected = other.gameObject;

            ownername.text = "ownername: " + Networking.GetOwner(objselected).displayName;
            isowner.text = "isowner: " + Networking.IsOwner(PlayerApiref, objselected).ToString();
            objready.text = "isobjectready: " + Networking.IsObjectReady(objselected).ToString();
            uniquename.text = "uniquename: " + Networking.GetUniqueName(objselected);
        }
        
    }
}

/*VRCSDKBaseNetworking.__get_SceneEventHandler__VRCSDKBaseVRC_EventHandler
VRCSDKBaseNetworking.__get_IsNetworkSettled__SystemBoolean
VRCSDKBaseNetworking.__get_IsMaster__SystemBoolean
VRCSDKBaseNetworking.__get_LocalPlayer__VRCSDKBaseVRCPlayerApi
VRCSDKBaseNetworking.__IsOwner__VRCSDKBaseVRCPlayerApi_UnityEngineGameObject__SystemBoolean
VRCSDKBaseNetworking.__IsOwner__UnityEngineGameObject__SystemBoolean
VRCSDKBaseNetworking.__GetOwner__UnityEngineGameObject__VRCSDKBaseVRCPlayerApi
VRCSDKBaseNetworking.__SetOwner__VRCSDKBaseVRCPlayerApi_UnityEngineGameObject__SystemVoid
VRCSDKBaseNetworking.__IsObjectReady__UnityEngineGameObject__SystemBoolean
VRCSDKBaseNetworking.__Destroy__UnityEngineGameObject__SystemVoid
VRCSDKBaseNetworking.__GetUniqueName__UnityEngineGameObject__SystemString
VRCSDKBaseNetworking.__GetNetworkDateTime__SystemDateTime
VRCSDKBaseNetworking.__GetServerTimeInSeconds__SystemDouble
VRCSDKBaseNetworking.__GetServerTimeInMilliseconds__SystemInt32
VRCSDKBaseNetworking.__CalculateServerDeltaTime__SystemDouble_SystemDouble__SystemDouble
VRCSDKBaseNetworking.__GetEventDispatcher__VRCSDKBaseVRC_EventDispatcher
*/
