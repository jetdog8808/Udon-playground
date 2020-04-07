
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class test : UdonSharpBehaviour
{
    public float myvar;
    private void Start()
    {
        checkthis();
    }

    public void checkthis()
    {
        Debug.Log("this is a test if the custom method is being called");
    }
}
