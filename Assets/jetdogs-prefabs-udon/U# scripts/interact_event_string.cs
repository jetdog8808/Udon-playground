
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class interact_event_string : UdonSharpBehaviour
{
    //public UdonBehaviour[] receivers;
    public UdonBehaviour receiver;
    public string event_name;

    public void Interact()
    {
        receiver.SendCustomEvent(event_name);
        Debug.Log("clicked object");


        /*
        for (int i = 0; i < receivers.Length; i++)
        { 
            if (receivers[i] != null)
            {
                receivers[i].SendCustomEvent(event_name);
            }
        }
        */
    }
}
