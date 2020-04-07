#if VRC_SDK_VRCSDK3

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using System;

[CustomEditor(typeof(VRC.SDK3.Components.VRCStation))]
public class VRCPlayerStationEditor3 : Editor 
{
    VRC.SDK3.Components.VRCStation myTarget;

	void OnEnable()
	{
		if(myTarget == null)
			myTarget = (VRC.SDK3.Components.VRCStation)target;
	}

	string[] UdonMethods = null;

	public override void OnInspectorGUI()
	{
		myTarget.PlayerMobility = (VRC.SDKBase.VRCStation.Mobility)EditorGUILayout.EnumPopup("Player Mobility", myTarget.PlayerMobility);
		myTarget.canUseStationFromStation = EditorGUILayout.Toggle("Can Use Station From Station", myTarget.canUseStationFromStation);
		myTarget.animatorController = (RuntimeAnimatorController) EditorGUILayout.ObjectField("Animator Controller", myTarget.animatorController, typeof(RuntimeAnimatorController), false );
		myTarget.disableStationExit = EditorGUILayout.Toggle("Disable Station Exit", myTarget.disableStationExit );
		myTarget.seated = EditorGUILayout.Toggle("Seated", myTarget.seated);
		myTarget.stationEnterPlayerLocation = (Transform)EditorGUILayout.ObjectField("Player Enter Location", myTarget.stationEnterPlayerLocation, typeof(Transform), true);
		myTarget.stationExitPlayerLocation = (Transform)EditorGUILayout.ObjectField("Player Exit Location", myTarget.stationExitPlayerLocation, typeof(Transform), true);
		myTarget.controlsObject = (VRC.SDKBase.VRC_ObjectApi)EditorGUILayout.ObjectField("API Object", myTarget.controlsObject, typeof(VRC.SDKBase.VRC_ObjectApi), false);

		var udon = myTarget.GetComponent<VRC.Udon.UdonBehaviour>();
		if (udon != null)
		{
			#if VRC_CLIENT
				myTarget.OnLocalPlayerEnterStation = EditorGUILayout.TextField("On Local Player Enter", myTarget.OnLocalPlayerEnterStation);
				myTarget.OnLocalPlayerExitStation = EditorGUILayout.TextField("On Local Player Exit", myTarget.OnLocalPlayerExitStation);
				myTarget.OnRemotePlayerEnterStation = EditorGUILayout.TextField("On Remote Player Enter", myTarget.OnRemotePlayerEnterStation);
				myTarget.OnRemotePlayerExitStation = EditorGUILayout.TextField("On Remote Player Exit", myTarget.OnRemotePlayerExitStation);
			#else
				List<string> choices = new List<string>(udon.GetPrograms());
				choices.Insert(0, "-none-");
				myTarget.OnLocalPlayerEnterStation = DrawUdonProgramPicker("On Local Player Enter", myTarget.OnLocalPlayerEnterStation, choices);
				myTarget.OnLocalPlayerExitStation = DrawUdonProgramPicker("On Local Player Exit", myTarget.OnLocalPlayerExitStation, choices);
				myTarget.OnRemotePlayerEnterStation = DrawUdonProgramPicker("On Remote Player Enter", myTarget.OnRemotePlayerEnterStation, choices);
				myTarget.OnRemotePlayerExitStation = DrawUdonProgramPicker("On Remote Player Exit", myTarget.OnRemotePlayerExitStation, choices);
			#endif
		}
	}

	string DrawUdonProgramPicker( string title, string current, List<string> choices )
	{
		int index = choices.IndexOf(current);
		if (index == -1)
			index = 0;
		int value = EditorGUILayout.Popup(title , index, choices.ToArray());
		if (value != 0)
			return choices[value];
		return current;
	}
}
#endif