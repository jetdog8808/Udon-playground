﻿#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace VRCSDK2
{
    [CustomEditor(typeof(VRC.SDKBase.VRC_YouTubeSync))]
	public class VRC_YouTubeSyncEditor : Editor
    {
		public override void OnInspectorGUI()
		{
            EditorGUILayout.HelpBox("This component is deprecated, please use the VRC_SyncVideoPlayer component instead.", MessageType.Error);
		}
	} 
}
#endif