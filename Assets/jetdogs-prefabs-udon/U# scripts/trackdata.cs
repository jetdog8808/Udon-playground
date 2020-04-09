
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class trackdata : UdonSharpBehaviour
{
    public Transform head;
    public Transform lefthand;
    public Transform righthand;
    [UdonSynced]
    private bool track = false;
    private bool localwait = false;

    void Interact()
    {
        if (Networking.IsOwner(gameObject))
        {
            track = !track;
        }
        else
        {
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
            localwait = true;
        }
    }

    private void Update()
    {
        VRCPlayerApi PlayerApiref = Networking.GetOwner(gameObject);

        if (PlayerApiref != null && track)
        {
            gettracking(head, VRCPlayerApi.TrackingDataType.Head, PlayerApiref);
            gettracking(lefthand, VRCPlayerApi.TrackingDataType.LeftHand, PlayerApiref);
            gettracking(righthand, VRCPlayerApi.TrackingDataType.RightHand, PlayerApiref);
        }

        if(localwait && track)
        {
            localwait = false;
        }
        else if (localwait && Networking.IsOwner(gameObject))
        {
            track = true;
        }

    }

    public void gettracking(Transform visobj, VRCPlayerApi.TrackingDataType trackpoint, VRCPlayerApi PlayerApiref)
    {
        visobj.position = PlayerApiref.GetTrackingData(trackpoint).position;
        visobj.rotation = PlayerApiref.GetTrackingData(trackpoint).rotation;
    }
}
