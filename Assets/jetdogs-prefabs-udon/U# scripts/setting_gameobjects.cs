
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class setting_gameobjects : UdonSharpBehaviour
{
    public GameObject[] receivers;
    [Tooltip("1=True | 2=False | 3=Toggle")]
    public int state;

    void Interact()
    {
        setobj();
        Debug.Log("clicked object");
    }

    public void setobj()
    {
        switch (state)
        {
            case 1: //will set all gameobjects to true
                for (int i = 0; i < receivers.Length; i++)
                {
                    if (receivers[i] != null)
                    {
                        receivers[i].SetActive(true);
                    }
                }
                break;
            case 2: //will set all gameobjects to false
                for (int i = 0; i < receivers.Length; i++)
                {
                    if (receivers[i] != null)
                    {
                        receivers[i].SetActive(false);
                    }
                }
                break;
            case 3: //will invert all gamobjects
                for (int i = 0; i < receivers.Length; i++)
                {
                    if (receivers[i] != null)
                    {
                        receivers[i].SetActive(!receivers[i].activeSelf);
                    }
                }
                break;
            default: //if given invalid value
                Debug.LogWarning("select 1-3 | 1=True | 2=False | 3=Toggle");
                break;

        }
        
    }

}
