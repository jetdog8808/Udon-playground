#if VRC_SDK_VRCSDK3

/*
using UnityEngine;
using UnityEditor;
using VRC.SDKBase;

[CustomEditor(typeof(VRC.SDK3.Components.VRCDestructibleStandard))]
[CanEditMultipleObjects]
public class VRC_DestructibleStandardEditor3 : Editor
{
    VRC.SDK3.Components.VRCDestructibleStandard ds;

	SerializedProperty maxHealth;
	SerializedProperty currentHealth;
	SerializedProperty healable;
	SerializedProperty onDamagedTrigger;
	SerializedProperty onDestroyedTrigger;
	SerializedProperty onHealedTrigger;
	SerializedProperty onFullHealedTrigger;

	void OnEnable()
	{
		maxHealth = serializedObject.FindProperty("maxHealth");	
		currentHealth = serializedObject.FindProperty("currentHealth");
		healable = serializedObject.FindProperty("healable");
		onDamagedTrigger = serializedObject.FindProperty("onDamagedTrigger");
		onDestroyedTrigger = serializedObject.FindProperty("onDestructedTrigger");
		onHealedTrigger = serializedObject.FindProperty("onHealedTrigger");
		onFullHealedTrigger = serializedObject.FindProperty("onFullHealedTrigger");
	}

	public override void OnInspectorGUI()
	{
		ds = (VRC.SDK3.Components.VRCDestructibleStandard) target;

		// Update the serializedProperty - always do this in the beginning of OnInspectorGUI.
		serializedObject.Update ();

		EditorGUILayout.PropertyField(maxHealth, new GUIContent("Max Health"));
		EditorGUILayout.PropertyField(currentHealth, new GUIContent("Current Health"));
		EditorGUILayout.PropertyField(healable, new GUIContent("Is Healable"));

		EditorGUILayout.PropertyField(onDamagedTrigger, new GUIContent("On Damaged Trigger"));
        VRCSDK2.VRC_EditorTools.DrawTriggerActionCallback("On Damaged Action", ds.onDamagedTrigger, ds.onDamagedEvent);

		EditorGUILayout.PropertyField(onDestroyedTrigger, new GUIContent("On Destructed Trigger"));
		VRCSDK2.VRC_EditorTools.DrawTriggerActionCallback("On Destructed Action", ds.onDestructedTrigger, ds.onDestructedEvent);

		EditorGUILayout.PropertyField(onHealedTrigger, new GUIContent("On Healed Trigger"));
		VRCSDK2.VRC_EditorTools.DrawTriggerActionCallback("On Healed Action", ds.onHealedTrigger, ds.onHealedEvent);

		EditorGUILayout.PropertyField(onFullHealedTrigger, new GUIContent("On Full Healed Trigger"));
		VRCSDK2.VRC_EditorTools.DrawTriggerActionCallback("On Full Healed Action", ds.onFullHealedTrigger, ds.onFullHealedEvent);

		// Apply changes to the serializedProperty - always do this in the end of OnInspectorGUI.
		serializedObject.ApplyModifiedProperties ();
	}
}
*/

#endif