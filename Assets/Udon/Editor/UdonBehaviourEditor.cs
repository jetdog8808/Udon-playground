using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.Udon.Editor.ProgramSources.Attributes;

namespace VRC.Udon.Editor
{
    [CustomEditor(typeof(UdonBehaviour))]
    public class UdonBehaviourEditor : UnityEditor.Editor
    {
        private SerializedProperty _programSourceProperty;
        private SerializedProperty _serializedProgramAssetProperty;
        private int _newProgramType = 1;

        private void OnEnable()
        {
            _programSourceProperty = serializedObject.FindProperty("programSource");
            _serializedProgramAssetProperty = serializedObject.FindProperty("serializedProgramAsset");

            UdonEditorManager.Instance.WantRepaint += Repaint;
        }

        private void OnDisable()
        {
            UdonEditorManager.Instance.WantRepaint -= Repaint;
        }

        public override void OnInspectorGUI()
        {
            UdonBehaviour udonTarget = (UdonBehaviour)target;

            using(new EditorGUI.DisabledScope(Application.isPlaying))
            {
                EditorGUILayout.LabelField("Specialized Synchronization Behaviour");

                //if(udonTarget.GetComponent<Animator>() != null || udonTarget.GetComponent<Animation>() != null)
                //    udonTarget.SynchronizeAnimation = EditorGUILayout.Toggle("Synchronize Animation", udonTarget.SynchronizeAnimation);
                //else
                //    udonTarget.SynchronizeAnimation = EditorGUILayout.Toggle("Synchronize Animation", false);
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.Toggle("Synchronize Animation", false);
                    EditorGUILayout.LabelField("Coming Soon!");
                    EditorGUILayout.EndHorizontal();
                }

                udonTarget.SynchronizePosition = EditorGUILayout.Toggle("Synchronize Position", udonTarget.SynchronizePosition);
                if(udonTarget.GetComponent<Collider>() != null)
                    udonTarget.AllowCollisionOwnershipTransfer = EditorGUILayout.Toggle("Allow Ownership Transfer on Collision", udonTarget.AllowCollisionOwnershipTransfer);
                else
                    udonTarget.AllowCollisionOwnershipTransfer = EditorGUILayout.Toggle("Allow Ownership Transfer on Collision", false);

                EditorGUILayout.Space();

                EditorGUILayout.LabelField("Udon");

                bool dirty = false;
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                _programSourceProperty.objectReferenceValue = EditorGUILayout.ObjectField(
                    "Program Source",
                    _programSourceProperty.objectReferenceValue,
                    typeof(AbstractUdonProgramSource),
                    false
                );

                if(EditorGUI.EndChangeCheck())
                {
                    dirty = true;
                    serializedObject.ApplyModifiedProperties();
                }

                if(_programSourceProperty.objectReferenceValue == null)
                {
                    if(_serializedProgramAssetProperty.objectReferenceValue != null)
                    {
                        _serializedProgramAssetProperty.objectReferenceValue = null;
                        serializedObject.ApplyModifiedPropertiesWithoutUndo();
                    }

                    List<(string displayName, Type newProgramType)> programSourceTypesForNewMenu = GetProgramSourceTypesForNewMenu();
                    if(GUILayout.Button("New Program"))
                    {
                        (string displayName, Type newProgramType) = programSourceTypesForNewMenu.ElementAt(_newProgramType);

                        string udonBehaviourName = udonTarget.name;
                        Scene scene = udonTarget.gameObject.scene;
                        if (string.IsNullOrEmpty(scene.path))
                        {
                            Debug.LogError("You need to save the scene before you can create new Udon program assets!");
                        }
                        else
                        {
                            AbstractUdonProgramSource newProgramSource = CreateUdonProgramSourceAsset(newProgramType, displayName, scene, udonBehaviourName);
                            _programSourceProperty.objectReferenceValue = newProgramSource;
                            _serializedProgramAssetProperty.objectReferenceValue = newProgramSource.SerializedProgramAsset;
                            serializedObject.ApplyModifiedProperties();    
                        }
                    }

                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    _newProgramType = EditorGUILayout.Popup(
                        "",
                        _newProgramType,
                        programSourceTypesForNewMenu.Select(t => t.displayName).ToArray(),
                        GUILayout.ExpandWidth(false)
                    );
                }
                else
                {
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                    using(new EditorGUI.DisabledScope(true))
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.ObjectField(
                            "Serialized Udon Program Asset ID: ",
                            _serializedProgramAssetProperty.objectReferenceValue,
                            typeof(AbstractSerializedUdonProgramAsset),
                            false
                        );

                        EditorGUI.indentLevel--;
                    }

                    AbstractUdonProgramSource programSource = (AbstractUdonProgramSource)_programSourceProperty.objectReferenceValue;
                    AbstractSerializedUdonProgramAsset serializedUdonProgramAsset = programSource.SerializedProgramAsset;
                    if(_serializedProgramAssetProperty.objectReferenceValue != serializedUdonProgramAsset)
                    {
                        _serializedProgramAssetProperty.objectReferenceValue = serializedUdonProgramAsset;
                        serializedObject.ApplyModifiedPropertiesWithoutUndo();
                    }
                }

                EditorGUILayout.EndHorizontal();

                udonTarget.RunEditorUpdate(ref dirty);
                if(dirty && !Application.isPlaying)
                {
                    EditorSceneManager.MarkSceneDirty(udonTarget.gameObject.scene);
                }
            }
        }

        private static AbstractUdonProgramSource CreateUdonProgramSourceAsset(Type newProgramType, string displayName, Scene scene, string udonBehaviourName)
        {
            string scenePath = Path.GetDirectoryName(scene.path) ?? "Assets";

            string folderName = $"{scene.name}_UdonProgramSources";
            string folderPath = Path.Combine(scenePath, folderName);

            if(!AssetDatabase.IsValidFolder(folderPath))
            {
                AssetDatabase.CreateFolder(scenePath, folderName);
            }

            string assetPath = Path.Combine(folderPath, $"{udonBehaviourName} {displayName}.asset");
            assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);

            AbstractUdonProgramSource asset = (AbstractUdonProgramSource)CreateInstance(newProgramType);
            AssetDatabase.CreateAsset(asset, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return asset;
        }

        private static List<(string displayName, Type newProgramType)> GetProgramSourceTypesForNewMenu()
        {
            Type abstractProgramSourceType = typeof(AbstractUdonProgramSource);
            Type attributeNewMenuAttributeType = typeof(UdonProgramSourceNewMenuAttribute);

            List<(string displayName, Type newProgramType)> programSourceTypesForNewMenu = new List<(string displayName, Type newProgramType)>();
            foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                object[] attributesUncast;
                try
                {
                    attributesUncast = assembly.GetCustomAttributes(attributeNewMenuAttributeType, false);
                }
                catch
                {
                    attributesUncast = new object[0];
                }

                foreach(object attributeUncast in attributesUncast)
                {
                    if(!(attributeUncast is UdonProgramSourceNewMenuAttribute udonProgramSourceNewMenuAttribute))
                    {
                        continue;
                    }

                    if(!abstractProgramSourceType.IsAssignableFrom(udonProgramSourceNewMenuAttribute.Type))
                    {
                        continue;
                    }

                    programSourceTypesForNewMenu.Add((udonProgramSourceNewMenuAttribute.DisplayName, udonProgramSourceNewMenuAttribute.Type));
                }
            }

            return programSourceTypesForNewMenu;
        }
    }
}
