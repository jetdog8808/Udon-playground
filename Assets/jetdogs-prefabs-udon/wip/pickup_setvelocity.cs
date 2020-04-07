
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class pickup_setvelocity : UdonSharpBehaviour
{
    public VRCPlayerApi PlayerApiref;
    private Transform transform;
    public Vector3 direction;
    public float speed = 1;
    private bool state = false;

    void Start()
    {
        transform = GetComponent<Transform>();
        PlayerApiref = Networking.LocalPlayer;
    }

    private void Update()
    {
        if (state)
        {
            PlayerApiref.SetVelocity(transform.rotation * direction.normalized * speed);
        }
    }

    public void OnPickupUseDown()
    {
        state = true;
    }

    public void OnPickupUseUp()
    {
        state = false;
    }

    public void OnDrop()
    {
        state = false;
    }
}
