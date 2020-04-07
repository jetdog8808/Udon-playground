// C# example:
using UnityEngine;
using UnityEditor;

public class VRCPlayerModEditorWindow : EditorWindow {

	public delegate void AddModCallback();
	public static AddModCallback addModCallback;

	private static VRC.SDKBase.VRC_PlayerMods myTarget;

	private static VRC.SDKBase.VRCPlayerModFactory.PlayerModType type;

	public static void Init (VRC.SDKBase.VRC_PlayerMods target, AddModCallback callback) 
	{
		// Get existing open window or if none, make a new one:
		EditorWindow.GetWindow (typeof (VRCPlayerModEditorWindow));
		addModCallback = callback;
		myTarget = target;

		type = VRC.SDKBase.VRCPlayerModFactory.PlayerModType.Jump;
	}
	
	void OnGUI ()
	{
		type = (VRC.SDKBase.VRCPlayerModFactory.PlayerModType)EditorGUILayout.EnumPopup("Mods", type);
		if(GUILayout.Button("Add Mod"))
		{
            VRC.SDKBase.VRCPlayerMod mod = VRC.SDKBase.VRCPlayerModFactory.Create(type);
			myTarget.AddMod(mod);
			addModCallback();
		}
	}
}