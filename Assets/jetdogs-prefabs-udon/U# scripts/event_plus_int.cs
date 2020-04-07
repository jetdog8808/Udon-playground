
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class event_plus_int : UdonSharpBehaviour
{
    public UdonBehaviour receiver;
    public string event_name;
    public string variable_name;
    public int variable_state = 0;

    public void Interact()
    { 
        Debug.Log("clicked object");
        activate();        
    }

    public void activate()
    {
        receiver.SetProgramVariable(variable_name, variable_state);
        receiver.SendCustomEvent(event_name);
    }
}
