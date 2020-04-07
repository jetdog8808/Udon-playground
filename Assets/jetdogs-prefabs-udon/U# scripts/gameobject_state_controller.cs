
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class gameobject_state_controller : UdonSharpBehaviour
{
    public GameObject[] receivers;
    [Tooltip("1=True | 2=False | 3=Toggle")]
    public bool others_state = false;
    [Tooltip("sets all in receivers active state to others state")]
    public bool start_set = true;
    [HideInInspector]
    public int index = -1;

    private void Start()
    {
        for (int i = 0; i < receivers.Length; i++)
        {
            if (receivers[i] != null)
            {
                receivers[i].SetActive(others_state);
            }
        }
    }

    public void active_int()
    {
        switch (others_state)
        {
            case true: 
                for (int i = 0; i < receivers.Length; i++)
                {
                    if (receivers[i] != null && i != index)
                    {
                        receivers[i].SetActive(true);
                    }
                    else
                    {
                        receivers[i].SetActive(!receivers[i].activeSelf);
                    }
                }
                break;
            case false: 
                for (int i = 0; i < receivers.Length; i++)
                {
                    if (receivers[i] != null && i != index)
                    {
                        receivers[i].SetActive(false);
                    }
                    else
                    {
                        receivers[i].SetActive(!receivers[i].activeSelf);
                    }
                }
                break;
            default: 
                Debug.LogWarning("how did you break this...");
                break;

        }
    }

    
}
