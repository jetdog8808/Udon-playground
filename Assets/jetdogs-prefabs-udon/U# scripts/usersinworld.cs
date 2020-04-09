
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

public class usersinworld : UdonSharpBehaviour
{
    private VRCPlayerApi[] playerlist = new VRCPlayerApi[100]; 
    //private int[] playerlist = new int[100];
    [Tooltip("the value you set on upload")]
    public int setcapacity;  
    public Text usersdisplay;  
    private string names = ""; 

    private void Start()
    {
        setcapacity *= 2;
    }


    public void OnPlayerJoined(VRC.SDKBase.VRCPlayerApi player) 
    {
        for (int i = 0; i < setcapacity; i++) 
        {
            if (playerlist[i] == null) 
            {
                playerlist[i] = player; 
                break; 
            }
            else if(playerlist[i] == player)  
            {
                break; 
            }
        }

        names = ""; 

        for (int i = 0; i < setcapacity; i++)
        {
            
            if (playerlist[i] != null)
            {
                names += (playerlist[i].displayName + "\n");
            }
        }

        usersdisplay.text = names;

    }

    public virtual void OnPlayerLeft(VRC.SDKBase.VRCPlayerApi player)
    {
        for (int i = 0; i < setcapacity; i++)
        {
            if (playerlist[i] == player)
            {
                playerlist[i] = null;
                break;
            }
        }

        names = "";

        for (int i = 0; i < setcapacity; i++)
        {

            if (playerlist[i] != null)
            {
                names += (playerlist[i].displayName + "\n");
            }
        }

        usersdisplay.text = names;
    }

}
