#if VRC_SDK_VRCSDK3

using UnityEditor;

[CustomEditor(typeof(VRC.SDK3.Components.VRCAvatarDescriptor))]
public class AvatarDescriptorEditor3 : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("Coming soon", MessageType.Warning);
    }
}

#endif