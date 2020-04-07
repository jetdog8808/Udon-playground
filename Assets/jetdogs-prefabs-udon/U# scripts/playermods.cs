
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class playermods : UdonSharpBehaviour
{
    public float run_speed = 4;
    public float walk_speed = 2;
    public float jump_impulse = 3;
    public float gravity_strength = 1;
    public bool legacy_locomotion = false;
    public VRCPlayerApi PlayerApiref;

    void Start()
    {
        PlayerApiref = Networking.LocalPlayer;
        if (PlayerApiref != null)
        {
            if (legacy_locomotion)
            {
                PlayerApiref.UseLegacyLocomotion();
                Debug.Log("Playermods, using legacy locomotion");
            }
            else
            {
                Debug.Log("Playermods, not using legacy locomotion");
            }

            Update_run_speed();
            Update_walk_speed();
            Update_jump_Impulse();
            Update_gravity_strength();

        }
        else
        {
            Debug.LogWarning("Playermods, playerapi is Null");
        }

    }

    public void update_all()
    {
        Update_run_speed();
        Update_walk_speed();
        Update_jump_Impulse();
        Update_gravity_strength();
    }

    public void Update_run_speed()
    {
        PlayerApiref.SetRunSpeed(run_speed);
        Debug.Log("player run speed set to: " + run_speed);
    }

    public void Update_walk_speed()
    {
        PlayerApiref.SetWalkSpeed(walk_speed);
        Debug.Log("player walk speed set to: " + walk_speed);
    }

    public void Update_jump_Impulse()
    {
        PlayerApiref.SetJumpImpulse(jump_impulse);
        Debug.Log("player walk speed set to: " + jump_impulse);
    }

    public void Update_gravity_strength()
    {
        PlayerApiref.SetGravityStrength(gravity_strength);
        Debug.Log("player walk speed set to: " + gravity_strength);
    }
}
