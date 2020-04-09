
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using System;

public class bonepositions : UdonSharpBehaviour
{
    public VRCPlayerApi PlayerApiref;
    public Transform[] bonevisuals = new Transform[55];
    [UdonSynced]
    public bool track = false;
    public bool localwait = false;

    private void Update()
    {
        VRCPlayerApi PlayerApiref = Networking.GetOwner(gameObject);

        if (PlayerApiref != null && track)
        {
            transform.position = PlayerApiref.GetPosition();
            transform.rotation = PlayerApiref.GetRotation();
            bonesset(HumanBodyBones.Hips, 0, PlayerApiref);
            bonesset(HumanBodyBones.LeftUpperLeg, 1, PlayerApiref);
            bonesset(HumanBodyBones.RightUpperLeg, 2, PlayerApiref);
            bonesset(HumanBodyBones.LeftLowerLeg, 3, PlayerApiref);
            bonesset(HumanBodyBones.RightLowerLeg, 4, PlayerApiref);
            bonesset(HumanBodyBones.LeftFoot, 5, PlayerApiref);
            bonesset(HumanBodyBones.RightFoot, 6, PlayerApiref);
            bonesset(HumanBodyBones.Spine, 7, PlayerApiref);
            bonesset(HumanBodyBones.Chest, 8, PlayerApiref);
            bonesset(HumanBodyBones.Neck, 9, PlayerApiref);
            bonesset(HumanBodyBones.Head, 10, PlayerApiref);
            bonesset(HumanBodyBones.LeftShoulder, 11, PlayerApiref);
            bonesset(HumanBodyBones.RightShoulder, 12, PlayerApiref);
            bonesset(HumanBodyBones.LeftUpperArm, 13, PlayerApiref);
            bonesset(HumanBodyBones.RightUpperArm, 14, PlayerApiref);
            bonesset(HumanBodyBones.LeftLowerArm, 15, PlayerApiref);
            bonesset(HumanBodyBones.RightLowerArm, 16, PlayerApiref);
            bonesset(HumanBodyBones.LeftHand, 17, PlayerApiref);
            bonesset(HumanBodyBones.RightHand, 18, PlayerApiref);
            bonesset(HumanBodyBones.LeftToes, 19, PlayerApiref);
            bonesset(HumanBodyBones.RightToes, 20, PlayerApiref);
            bonesset(HumanBodyBones.LeftEye, 21, PlayerApiref);
            bonesset(HumanBodyBones.RightEye, 22, PlayerApiref);
            bonesset(HumanBodyBones.Jaw, 23, PlayerApiref);
            bonesset(HumanBodyBones.LeftThumbProximal, 24, PlayerApiref);
            bonesset(HumanBodyBones.LeftThumbIntermediate, 25, PlayerApiref);
            bonesset(HumanBodyBones.LeftThumbDistal, 26, PlayerApiref);
            bonesset(HumanBodyBones.LeftIndexProximal, 27, PlayerApiref);
            bonesset(HumanBodyBones.LeftIndexIntermediate, 28, PlayerApiref);
            bonesset(HumanBodyBones.LeftIndexDistal, 29, PlayerApiref);
            bonesset(HumanBodyBones.LeftMiddleProximal, 30, PlayerApiref);
            bonesset(HumanBodyBones.LeftMiddleIntermediate, 31, PlayerApiref);
            bonesset(HumanBodyBones.LeftMiddleDistal, 32, PlayerApiref);
            bonesset(HumanBodyBones.LeftRingProximal, 33, PlayerApiref);
            bonesset(HumanBodyBones.LeftRingIntermediate, 34, PlayerApiref);
            bonesset(HumanBodyBones.LeftRingDistal, 35, PlayerApiref);
            bonesset(HumanBodyBones.LeftLittleProximal, 36, PlayerApiref);
            bonesset(HumanBodyBones.LeftLittleIntermediate, 37, PlayerApiref);
            bonesset(HumanBodyBones.LeftLittleDistal, 38, PlayerApiref);
            bonesset(HumanBodyBones.RightThumbProximal, 39, PlayerApiref);
            bonesset(HumanBodyBones.RightThumbIntermediate, 40, PlayerApiref);
            bonesset(HumanBodyBones.RightThumbDistal, 41, PlayerApiref);
            bonesset(HumanBodyBones.RightIndexProximal, 42, PlayerApiref);
            bonesset(HumanBodyBones.RightIndexIntermediate, 43, PlayerApiref);
            bonesset(HumanBodyBones.RightIndexDistal, 44, PlayerApiref);
            bonesset(HumanBodyBones.RightMiddleProximal, 45, PlayerApiref);
            bonesset(HumanBodyBones.RightMiddleIntermediate, 46, PlayerApiref);
            bonesset(HumanBodyBones.RightMiddleDistal, 47, PlayerApiref);
            bonesset(HumanBodyBones.RightRingProximal, 48, PlayerApiref);
            bonesset(HumanBodyBones.RightRingIntermediate, 49, PlayerApiref);
            bonesset(HumanBodyBones.RightRingDistal, 50, PlayerApiref);
            bonesset(HumanBodyBones.RightLittleProximal, 51, PlayerApiref);
            bonesset(HumanBodyBones.RightLittleIntermediate, 52, PlayerApiref);
            bonesset(HumanBodyBones.RightLittleDistal, 53, PlayerApiref);
            bonesset(HumanBodyBones.UpperChest, 54, PlayerApiref);
        }

        if (localwait && track)
        {
            localwait = false;
        }
        else if (localwait && Networking.IsOwner(gameObject))
        {
            track = true;
        }
    }

    void bonesset(HumanBodyBones bone, int index, VRCPlayerApi PlayerApiref)
    {
        bonevisuals[index].position = PlayerApiref.GetBonePosition(bone);
        bonevisuals[index].rotation = PlayerApiref.GetBoneRotation(bone);
    }
}
