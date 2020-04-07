
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class trigger_teleport : UdonSharpBehaviour
{
    public Transform teleport_entry;
    public Transform teleport_receiver;
    [Tooltip("distance from entry")]
    public float teleport_radius = 0.3f;
    [Tooltip("optional")]
    public UdonBehaviour twoway_ref;
    private bool state = true;
    private VRCPlayerApi PlayerApiref;

    private void Start()
    {
        PlayerApiref = Networking.LocalPlayer;
    }

    private void Update()
    {
        if (PlayerApiref != null)
        {
            if (state)
            {
                Vector3 player = PlayerApiref.GetPosition();

                if (Vector3.Distance(player, teleport_entry.position) < teleport_radius)
                {
                    if (twoway_ref != null)
                    {
                        twoway_ref.SetProgramVariable("state", false);
                    }
                    PlayerApiref.TeleportTo(teleport_receiver.position, teleport_receiver.rotation);
                }
            }
            else
            {
                Vector3 player = PlayerApiref.GetPosition();

                if (Vector3.Distance(player, teleport_entry.position) > teleport_radius)
                {
                    state = true;
                }
            }
        }
        
        

    }
}
