
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

public class layermask_controller : UdonSharpBehaviour
{
    
    public Toggle[] toggles = new Toggle[32];
    private int maskint;


    void Start()
    {
       
        setint();
        

    }

    public void setint()
    {
        maskint = 0;
        
        for (int i = 0; i < toggles.Length; i++)
        {
            if (toggles[i] != null)
            {
                maskint |= (int)((System.Convert.ToInt32(toggles[i].isOn)) << i);
            }
            
        }
        

        Debug.Log(System.Convert.ToString(maskint, 2));

        LayerMask layerMask = maskint;
        //VRC_MirrorReflection mirror;
        //mirror = gameObject.GetComponent<VRC_MirrorReflection>();
        //mirror = gameObject.GetComponent("VRC_MirrorReflection") as VRC_MirrorReflection;

        //mirror.m_ReflectLayers = layerMask;

        
    }
}
