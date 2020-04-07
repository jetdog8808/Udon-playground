using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Assertions;
using VRC.Udon.Common;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.ProgramSources;

namespace VRC.Udon.Editor.ProgramSources
{
    public class UdonProgramAsset : AbstractUdonProgramSource, ISerializationCallbackReceiver
    {
        protected IUdonProgram program;

        [SerializeField]
        protected AbstractSerializedUdonProgramAsset serializedUdonProgramAsset;

        public override AbstractSerializedUdonProgramAsset SerializedProgramAsset
        {
            get
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(this, out string guid, out long _);
                if(serializedUdonProgramAsset != null)
                {
                    if(serializedUdonProgramAsset.name == guid)
                    {
                        return serializedUdonProgramAsset;
                    }

                    string oldSerializedUdonProgramAssetPath = Path.Combine("Assets", "SerializedUdonPrograms", $"{serializedUdonProgramAsset.name}.asset");
                    AssetDatabase.DeleteAsset(oldSerializedUdonProgramAssetPath);
                }

                string serializedUdonProgramAssetPath = Path.Combine("Assets", "SerializedUdonPrograms", $"{guid}.asset");

                serializedUdonProgramAsset = (SerializedUdonProgramAsset)AssetDatabase.LoadAssetAtPath(
                    Path.Combine("Assets", "SerializedUdonPrograms", $"{guid}.asset"),
                    typeof(SerializedUdonProgramAsset)
                );

                if(serializedUdonProgramAsset != null)
                {
                    return serializedUdonProgramAsset;
                }

                serializedUdonProgramAsset = CreateInstance<SerializedUdonProgramAsset>();
                if(!AssetDatabase.IsValidFolder(Path.Combine("Assets", "SerializedUdonPrograms")))
                {
                    AssetDatabase.CreateFolder("Assets", "SerializedUdonPrograms");
                }

                AssetDatabase.CreateAsset(serializedUdonProgramAsset, serializedUdonProgramAssetPath);
                AssetDatabase.SaveAssets();

                RefreshProgram();
                AssetDatabase.SaveAssets();

                AssetDatabase.Refresh();

                return serializedUdonProgramAsset;
            }
        }

        public sealed override void RunEditorUpdate(UdonBehaviour udonBehaviour, ref bool dirty)
        {
            if(program == null && serializedUdonProgramAsset != null)
            {
                program = serializedUdonProgramAsset.RetrieveProgram();
            }

            if(program == null)
            {
                RefreshProgram();
            }

            DrawProgramSourceGUI(udonBehaviour, ref dirty);

            if(dirty)
            {
                EditorUtility.SetDirty(this);
            }
        }

        protected virtual void DrawProgramSourceGUI(UdonBehaviour udonBehaviour, ref bool dirty)
        {
            DrawPublicVariables(udonBehaviour, ref dirty);
            DrawProgramDisassembly();
        }

        public sealed override void RefreshProgram()
        {
            if(Application.isPlaying)
            {
                return;
            }

            RefreshProgramImpl();

            SerializedProgramAsset.StoreProgram(program);
            EditorUtility.SetDirty(this);
        }

        protected virtual void RefreshProgramImpl()
        {
        }

        [PublicAPI]
        protected void DrawPublicVariables(UdonBehaviour udonBehaviour, ref bool dirty)
        {
            IUdonVariableTable publicVariables = null;
            if(udonBehaviour != null)
            {
                publicVariables = udonBehaviour.publicVariables;
            }

            EditorGUILayout.LabelField("Public Variables", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            if(program?.SymbolTable == null)
            {
                EditorGUILayout.LabelField("No public variables.");
                EditorGUI.indentLevel--;
                return;
            }

            IUdonSymbolTable symbolTable = program.SymbolTable;
            string[] exportedSymbolNames = symbolTable.GetExportedSymbols();

            if(publicVariables != null)
            {
                foreach(string publicVariableSymbol in publicVariables.VariableSymbols.ToArray())
                {
                    if(!exportedSymbolNames.Contains(publicVariableSymbol))
                    {
                        publicVariables.RemoveVariable(publicVariableSymbol);
                    }
                }
            }

            if(exportedSymbolNames.Length <= 0)
            {
                EditorGUILayout.LabelField("No public variables.");
                EditorGUI.indentLevel--;
                return;
            }

            foreach(string exportedSymbol in exportedSymbolNames)
            {
                Type symbolType = symbolTable.GetSymbolType(exportedSymbol);
                if(publicVariables == null)
                {
                    DrawPublicVariableField(exportedSymbol, GetPublicVariableDefaultValue(exportedSymbol, symbolType), symbolType, ref dirty, false);
                    continue;
                }

                if(!publicVariables.TryGetVariableType(exportedSymbol, out Type declaredType) || declaredType != symbolType)
                {
                    publicVariables.RemoveVariable(exportedSymbol);
                    if(!publicVariables.TryAddVariable(CreateUdonVariable(exportedSymbol, GetPublicVariableDefaultValue(exportedSymbol, declaredType), symbolType)))
                    {
                        EditorGUILayout.LabelField($"Error drawing field for symbol '{exportedSymbol}'.");
                        continue;
                    }
                }

                if(!publicVariables.TryGetVariableValue(exportedSymbol, out object variableValue))
                {
                    variableValue = GetPublicVariableDefaultValue(exportedSymbol, declaredType);
                }

                variableValue = DrawPublicVariableField(exportedSymbol, variableValue, symbolType, ref dirty, true);
                if(!dirty)
                {
                    continue;
                }

                Undo.RecordObject(udonBehaviour, "Modify Public Variable");

                if(!publicVariables.TrySetVariableValue(exportedSymbol, variableValue))
                {
                    if(!publicVariables.TryAddVariable(CreateUdonVariable(exportedSymbol, variableValue, symbolType)))
                    {
                        Debug.LogError($"Failed to set public variable '{exportedSymbol}' value.");
                    }
                }

                EditorSceneManager.MarkSceneDirty(udonBehaviour.gameObject.scene);

                if(PrefabUtility.IsPartOfPrefabInstance(udonBehaviour))
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(udonBehaviour);
                }
            }

            EditorGUI.indentLevel--;
        }

        private static IUdonVariable CreateUdonVariable(string symbolName, object value, Type declaredType)
        {
            Type udonVariableType = typeof(UdonVariable<>).MakeGenericType(declaredType);
            return (IUdonVariable)Activator.CreateInstance(udonVariableType, symbolName, value);
        }

        [PublicAPI]
        protected virtual object GetPublicVariableDefaultValue(string symbol, Type type)
        {
            return null;
        }

        [PublicAPI]
        protected void DrawProgramDisassembly()
        {
            try
            {
                EditorGUILayout.LabelField("Disassembled Program", EditorStyles.boldLabel);
                using(new EditorGUI.DisabledScope(true))
                {
                    string[] disassembledProgram = UdonEditorManager.Instance.DisassembleProgram(program);
                    EditorGUILayout.TextArea(string.Join("\n", disassembledProgram));
                }
            }
            catch(Exception e)
            {
                Debug.LogException(e);
            }
        }

        [NonSerialized]
        private readonly Dictionary<string, bool> _arrayStates = new Dictionary<string, bool>();

        protected virtual object DrawPublicVariableField(string symbol, object variableValue, Type variableType, ref bool dirty, bool enabled)
        {
            using(new EditorGUI.DisabledScope(!enabled))
            {
                // ReSharper disable RedundantNameQualifier
                EditorGUILayout.BeginHorizontal();
                if(!variableType.IsInstanceOfType(variableValue))
                {
                    if(variableType.IsValueType)
                    {
                        variableValue = Activator.CreateInstance(variableType);
                    }
                    else
                    {
                        variableValue = null;
                    }
                }

                if(typeof(UnityEngine.Object).IsAssignableFrom(variableType))
                {
                    UnityEngine.Object unityEngineObjectValue = (UnityEngine.Object)variableValue;
                    EditorGUI.BeginChangeCheck();
                    Rect fieldRect = EditorGUILayout.GetControlRect();
                    variableValue = EditorGUI.ObjectField(fieldRect, symbol, unityEngineObjectValue, variableType, true);

                    if(variableValue == null && (variableType == typeof(GameObject) || variableType == typeof(Transform) ||
                                                 variableType == typeof(UdonBehaviour)))
                    {
                        EditorGUI.LabelField(
                            fieldRect,
                            new GUIContent(symbol),
                            new GUIContent("Self (" + variableType.Name + ")", AssetPreview.GetMiniTypeThumbnail(variableType)),
                            EditorStyles.objectField);
                    }

                    if(EditorGUI.EndChangeCheck())
                    {
                        dirty = true;
                    }
                }
                else if(variableType == typeof(string))
                {
                    string stringValue = (string)variableValue;
                    EditorGUI.BeginChangeCheck();
                    variableValue = EditorGUILayout.TextField(symbol, stringValue);
                    if(EditorGUI.EndChangeCheck())
                    {
                        dirty = true;
                    }
                }
                else if(variableType == typeof(string[]))
                {
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.LabelField(symbol);
                    EditorGUILayout.BeginVertical();
                    string[] valueArray = (string[])variableValue;
                    GUI.SetNextControlName("NodeField");
                    bool showArray = false;
                    if(_arrayStates.ContainsKey(symbol))
                    {
                        showArray = _arrayStates[symbol];
                    }
                    else
                    {
                        _arrayStates.Add(symbol, false);
                    }

                    EditorGUILayout.BeginHorizontal();
                    showArray = EditorGUILayout.Foldout(showArray, GUIContent.none);
                    _arrayStates[symbol] = showArray;

                    int newSize = EditorGUILayout.IntField(
                        "size:",
                        valueArray != null && valueArray.Length > 0 ? valueArray.Length : 1);

                    newSize = newSize >= 0 ? newSize : 0;
                    Array.Resize(ref valueArray, newSize);
                    EditorGUILayout.EndHorizontal();

                    if(showArray)
                    {
                        if(valueArray != null && valueArray.Length > 0)
                        {
                            for(int i = 0; i < valueArray.Length; i++)
                            {
                                GUI.SetNextControlName("NodeField");
                                valueArray[i] = EditorGUILayout.TextField(
                                    $"{i}:",
                                    valueArray.Length > i ? valueArray[i] : "");
                            }
                        }
                    }

                    EditorGUILayout.EndVertical();
                    if(EditorGUI.EndChangeCheck())
                    {
                        variableValue = valueArray;
                        dirty = true;
                    }
                }
                else if(variableType == typeof(float))
                {
                    float floatValue = (float?)variableValue ?? default;
                    EditorGUI.BeginChangeCheck();
                    variableValue = EditorGUILayout.FloatField(symbol, floatValue);
                    if(EditorGUI.EndChangeCheck())
                    {
                        dirty = true;
                    }
                }
                else if(variableType == typeof(float[]))
                {
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.LabelField(symbol);
                    EditorGUILayout.BeginVertical();
                    float[] valueArray = (float[])variableValue;
                    GUI.SetNextControlName("NodeField");
                    bool showArray = false;
                    if(_arrayStates.ContainsKey(symbol))
                    {
                        showArray = _arrayStates[symbol];
                    }
                    else
                    {
                        _arrayStates.Add(symbol, false);
                    }

                    EditorGUILayout.BeginHorizontal();
                    showArray = EditorGUILayout.Foldout(showArray, GUIContent.none);
                    _arrayStates[symbol] = showArray;

                    int newSize = EditorGUILayout.IntField(
                        "size:",
                        valueArray != null && valueArray.Length > 0 ? valueArray.Length : 1);

                    newSize = newSize >= 0 ? newSize : 0;
                    Array.Resize(ref valueArray, newSize);
                    EditorGUILayout.EndHorizontal();

                    if(showArray)
                    {
                        if(valueArray != null && valueArray.Length > 0)
                        {
                            for(int i = 0; i < valueArray.Length; i++)
                            {
                                GUI.SetNextControlName("NodeField");
                                valueArray[i] = EditorGUILayout.FloatField(
                                    $"{i}:",
                                    valueArray.Length > i ? valueArray[i] : 0);
                            }
                        }
                    }

                    EditorGUILayout.EndVertical();
                    if(EditorGUI.EndChangeCheck())
                    {
                        variableValue = valueArray;
                        dirty = true;
                    }
                }
                else if(variableType == typeof(int))
                {
                    int intValue = (int?)variableValue ?? default;
                    EditorGUI.BeginChangeCheck();
                    variableValue = EditorGUILayout.IntField(symbol, intValue);
                    if(EditorGUI.EndChangeCheck())
                    {
                        dirty = true;
                    }
                }
                else if(variableType == typeof(int[]))
                {
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.LabelField(symbol);
                    EditorGUILayout.BeginVertical();
                    int[] valueArray = (int[])variableValue;
                    GUI.SetNextControlName("NodeField");
                    bool showArray = false;
                    if(_arrayStates.ContainsKey(symbol))
                    {
                        showArray = _arrayStates[symbol];
                    }
                    else
                    {
                        _arrayStates.Add(symbol, false);
                    }

                    EditorGUILayout.BeginHorizontal();
                    showArray = EditorGUILayout.Foldout(showArray, GUIContent.none);
                    _arrayStates[symbol] = showArray;

                    int newSize = EditorGUILayout.IntField(
                        "size:",
                        valueArray != null && valueArray.Length > 0 ? valueArray.Length : 1);

                    newSize = newSize >= 0 ? newSize : 0;
                    Array.Resize(ref valueArray, newSize);
                    EditorGUILayout.EndHorizontal();

                    if(showArray)
                    {
                        if(valueArray != null && valueArray.Length > 0)
                        {
                            for(int i = 0; i < valueArray.Length; i++)
                            {
                                GUI.SetNextControlName("NodeField");
                                valueArray[i] = EditorGUILayout.IntField(
                                    $"{i}:",
                                    valueArray.Length > i ? valueArray[i] : 0);
                            }
                        }
                    }

                    EditorGUILayout.EndVertical();
                    if(EditorGUI.EndChangeCheck())
                    {
                        variableValue = valueArray;
                        dirty = true;
                    }
                }
                else if(variableType == typeof(bool))
                {
                    bool boolValue = (bool?)variableValue ?? default;
                    EditorGUI.BeginChangeCheck();
                    variableValue = EditorGUILayout.Toggle(symbol, boolValue);
                    if(EditorGUI.EndChangeCheck())
                    {
                        dirty = true;
                    }
                }
                else if(variableType == typeof(bool[]))
                {
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.LabelField(symbol);
                    EditorGUILayout.BeginVertical();
                    bool[] valueArray = (bool[])variableValue;
                    GUI.SetNextControlName("NodeField");
                    bool showArray = false;
                    if(_arrayStates.ContainsKey(symbol))
                    {
                        showArray = _arrayStates[symbol];
                    }
                    else
                    {
                        _arrayStates.Add(symbol, false);
                    }

                    EditorGUILayout.BeginHorizontal();
                    showArray = EditorGUILayout.Foldout(showArray, GUIContent.none);
                    _arrayStates[symbol] = showArray;

                    int newSize = EditorGUILayout.IntField(
                        "size:",
                        valueArray != null && valueArray.Length > 0 ? valueArray.Length : 1);

                    newSize = newSize >= 0 ? newSize : 0;
                    Array.Resize(ref valueArray, newSize);
                    EditorGUILayout.EndHorizontal();

                    if(showArray)
                    {
                        if(valueArray != null && valueArray.Length > 0)
                        {
                            for(int i = 0; i < valueArray.Length; i++)
                            {
                                GUI.SetNextControlName("NodeField");
                                valueArray[i] = EditorGUILayout.Toggle(
                                    $"{i}:",
                                    valueArray.Length > i && valueArray[i]);
                            }
                        }
                    }

                    EditorGUILayout.EndVertical();
                    if(EditorGUI.EndChangeCheck())
                    {
                        variableValue = valueArray;
                        dirty = true;
                    }
                }
                else if(variableType == typeof(UnityEngine.Vector2))
                {
                    Vector2 vector2Value = (Vector2?)variableValue ?? default;
                    EditorGUI.BeginChangeCheck();
                    variableValue = EditorGUILayout.Vector2Field(symbol, vector2Value);
                    if(EditorGUI.EndChangeCheck())
                    {
                        dirty = true;
                    }
                }
                else if(variableType == typeof(Vector2[]))
                {
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.LabelField(symbol);
                    EditorGUILayout.BeginVertical();
                    Vector2[] valueArray = (Vector2[])variableValue;
                    GUI.SetNextControlName("NodeField");
                    bool showArray = false;
                    if(_arrayStates.ContainsKey(symbol))
                    {
                        showArray = _arrayStates[symbol];
                    }
                    else
                    {
                        _arrayStates.Add(symbol, false);
                    }

                    EditorGUILayout.BeginHorizontal();
                    showArray = EditorGUILayout.Foldout(showArray, GUIContent.none);
                    _arrayStates[symbol] = showArray;

                    int newSize = EditorGUILayout.IntField(
                        "size:",
                        valueArray != null && valueArray.Length > 0 ? valueArray.Length : 1);

                    newSize = newSize >= 0 ? newSize : 0;
                    Array.Resize(ref valueArray, newSize);
                    EditorGUILayout.EndHorizontal();

                    if(showArray)
                    {
                        if(valueArray != null && valueArray.Length > 0)
                        {
                            for(int i = 0; i < valueArray.Length; i++)
                            {
                                GUI.SetNextControlName("NodeField");
                                valueArray[i] = EditorGUILayout.Vector2Field(
                                    $"{i}:",
                                    valueArray.Length > i ? valueArray[i] : Vector2.zero);
                            }
                        }
                    }

                    EditorGUILayout.EndVertical();
                    if(EditorGUI.EndChangeCheck())
                    {
                        variableValue = valueArray;
                        dirty = true;
                    }
                }
                else if(variableType == typeof(UnityEngine.Vector3))
                {
                    Vector3 vector3Value = (Vector3?)variableValue ?? default;
                    EditorGUI.BeginChangeCheck();
                    variableValue = EditorGUILayout.Vector3Field(symbol, vector3Value);
                    if(EditorGUI.EndChangeCheck())
                    {
                        dirty = true;
                    }
                }
                else if(variableType == typeof(Vector3[]))
                {
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.LabelField(symbol);
                    EditorGUILayout.BeginVertical();
                    Vector3[] valueArray = (Vector3[])variableValue;
                    GUI.SetNextControlName("NodeField");
                    bool showArray = false;
                    if(_arrayStates.ContainsKey(symbol))
                    {
                        showArray = _arrayStates[symbol];
                    }
                    else
                    {
                        _arrayStates.Add(symbol, false);
                    }

                    EditorGUILayout.BeginHorizontal();
                    showArray = EditorGUILayout.Foldout(showArray, GUIContent.none);
                    _arrayStates[symbol] = showArray;

                    int newSize = EditorGUILayout.IntField(
                        "size:",
                        valueArray != null && valueArray.Length > 0 ? valueArray.Length : 1);

                    newSize = newSize >= 0 ? newSize : 0;
                    Array.Resize(ref valueArray, newSize);
                    EditorGUILayout.EndHorizontal();

                    if(showArray)
                    {
                        if(valueArray != null && valueArray.Length > 0)
                        {
                            for(int i = 0; i < valueArray.Length; i++)
                            {
                                GUI.SetNextControlName("NodeField");
                                valueArray[i] = EditorGUILayout.Vector3Field(
                                    $"{i}:",
                                    valueArray.Length > i ? valueArray[i] : Vector3.zero);
                            }
                        }
                    }

                    EditorGUILayout.EndVertical();
                    if(EditorGUI.EndChangeCheck())
                    {
                        variableValue = valueArray;
                        dirty = true;
                    }
                }
                else if(variableType == typeof(UnityEngine.Vector4))
                {
                    Vector4 vector4Value = (Vector4?)variableValue ?? default;
                    EditorGUI.BeginChangeCheck();
                    variableValue = EditorGUILayout.Vector4Field(symbol, vector4Value);
                    if(EditorGUI.EndChangeCheck())
                    {
                        dirty = true;
                    }
                }
                else if(variableType == typeof(Vector4[]))
                {
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.LabelField(symbol);
                    EditorGUILayout.BeginVertical();
                    Vector4[] valueArray = (Vector4[])variableValue;
                    GUI.SetNextControlName("NodeField");
                    bool showArray = false;
                    if(_arrayStates.ContainsKey(symbol))
                    {
                        showArray = _arrayStates[symbol];
                    }
                    else
                    {
                        _arrayStates.Add(symbol, false);
                    }

                    EditorGUILayout.BeginHorizontal();
                    showArray = EditorGUILayout.Foldout(showArray, GUIContent.none);
                    _arrayStates[symbol] = showArray;

                    int newSize = EditorGUILayout.IntField(
                        "size:",
                        valueArray != null && valueArray.Length > 0 ? valueArray.Length : 1);

                    newSize = newSize >= 0 ? newSize : 0;
                    Array.Resize(ref valueArray, newSize);
                    EditorGUILayout.EndHorizontal();

                    if(showArray)
                    {
                        if(valueArray != null && valueArray.Length > 0)
                        {
                            for(int i = 0; i < valueArray.Length; i++)
                            {
                                GUI.SetNextControlName("NodeField");
                                valueArray[i] = EditorGUILayout.Vector4Field(
                                    $"{i}:",
                                    valueArray.Length > i ? valueArray[i] : Vector4.zero);
                            }
                        }
                    }

                    EditorGUILayout.EndVertical();
                    if(EditorGUI.EndChangeCheck())
                    {
                        variableValue = valueArray;
                        dirty = true;
                    }
                }
                else if(variableType == typeof(UnityEngine.Quaternion))
                {
                    Quaternion quaternionValue = (Quaternion?)variableValue ?? default;
                    EditorGUI.BeginChangeCheck();
                    Vector4 quaternionVector4 = EditorGUILayout.Vector4Field(symbol, new Vector4(quaternionValue.x, quaternionValue.y, quaternionValue.z, quaternionValue.w));
                    variableValue = new Quaternion(quaternionVector4.x, quaternionVector4.y, quaternionVector4.z, quaternionVector4.w);
                    if(EditorGUI.EndChangeCheck())
                    {
                        dirty = true;
                    }
                }
                else if(variableType == typeof(Quaternion[]))
                {
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.LabelField(symbol);
                    EditorGUILayout.BeginVertical();
                    Quaternion[] valueArray = (Quaternion[])variableValue;
                    GUI.SetNextControlName("NodeField");
                    bool showArray = false;
                    if(_arrayStates.ContainsKey(symbol))
                    {
                        showArray = _arrayStates[symbol];
                    }
                    else
                    {
                        _arrayStates.Add(symbol, false);
                    }

                    EditorGUILayout.BeginHorizontal();
                    showArray = EditorGUILayout.Foldout(showArray, GUIContent.none);
                    _arrayStates[symbol] = showArray;

                    int newSize = EditorGUILayout.IntField(
                        "size:",
                        valueArray != null && valueArray.Length > 0 ? valueArray.Length : 1);

                    newSize = newSize >= 0 ? newSize : 0;
                    Array.Resize(ref valueArray, newSize);
                    EditorGUILayout.EndHorizontal();

                    if(showArray)
                    {
                        if(valueArray != null && valueArray.Length > 0)
                        {
                            for(int i = 0; i < valueArray.Length; i++)
                            {
                                GUI.SetNextControlName("NodeField");
                                Vector4 vector4 = EditorGUILayout.Vector4Field(
                                    $"{i}:",
                                    valueArray.Length > i ? new Vector4(valueArray[i].x, valueArray[i].y, valueArray[i].z, valueArray[i].w) : Vector4.zero);

                                valueArray[i] = new Quaternion(vector4.x, vector4.y, vector4.z, vector4.w);
                            }
                        }
                    }

                    EditorGUILayout.EndVertical();
                    if(EditorGUI.EndChangeCheck())
                    {
                        variableValue = valueArray;
                        dirty = true;
                    }
                }
                else if(variableType == typeof(UnityEngine.Color))
                {
                    Color color2Value = (Color?)variableValue ?? default;
                    EditorGUI.BeginChangeCheck();
                    variableValue = EditorGUILayout.ColorField(symbol, color2Value);
                    if(EditorGUI.EndChangeCheck())
                    {
                        dirty = true;
                    }
                }
                else if(variableType == typeof(Color[]))
                {
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.LabelField(symbol);
                    EditorGUILayout.BeginVertical();
                    Color[] valueArray = (Color[])variableValue;
                    GUI.SetNextControlName("NodeField");
                    bool showArray = false;
                    if(_arrayStates.ContainsKey(symbol))
                    {
                        showArray = _arrayStates[symbol];
                    }
                    else
                    {
                        _arrayStates.Add(symbol, false);
                    }

                    EditorGUILayout.BeginHorizontal();
                    showArray = EditorGUILayout.Foldout(showArray, GUIContent.none);
                    _arrayStates[symbol] = showArray;

                    int newSize = EditorGUILayout.IntField(
                        "size:",
                        valueArray != null && valueArray.Length > 0 ? valueArray.Length : 1);

                    newSize = newSize >= 0 ? newSize : 0;
                    Array.Resize(ref valueArray, newSize);
                    EditorGUILayout.EndHorizontal();

                    if(showArray)
                    {
                        if(valueArray != null && valueArray.Length > 0)
                        {
                            for(int i = 0; i < valueArray.Length; i++)
                            {
                                GUI.SetNextControlName("NodeField");
                                valueArray[i] = EditorGUILayout.ColorField(
                                    $"{i}:",
                                    valueArray.Length > i ? valueArray[i] : Color.white);
                            }
                        }
                    }

                    EditorGUILayout.EndVertical();
                    if(EditorGUI.EndChangeCheck())
                    {
                        variableValue = valueArray;
                        dirty = true;
                    }
                }
                else if(variableType == typeof(UnityEngine.Color32))
                {
                    Color32 colorValue = (Color32?)variableValue ?? default;
                    EditorGUI.BeginChangeCheck();
                    variableValue = (Color32)EditorGUILayout.ColorField(symbol, colorValue);
                    if(EditorGUI.EndChangeCheck())
                    {
                        dirty = true;
                    }
                }
                else if(variableType == typeof(Color32[]))
                {
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.LabelField(symbol);
                    EditorGUILayout.BeginVertical();
                    Color32[] valueArray = (Color32[])variableValue;
                    GUI.SetNextControlName("NodeField");
                    bool showArray = false;
                    if(_arrayStates.ContainsKey(symbol))
                    {
                        showArray = _arrayStates[symbol];
                    }
                    else
                    {
                        _arrayStates.Add(symbol, false);
                    }

                    EditorGUILayout.BeginHorizontal();
                    showArray = EditorGUILayout.Foldout(showArray, GUIContent.none);
                    _arrayStates[symbol] = showArray;

                    int newSize = EditorGUILayout.IntField(
                        "size:",
                        valueArray != null && valueArray.Length > 0 ? valueArray.Length : 1);

                    newSize = newSize >= 0 ? newSize : 0;
                    Array.Resize(ref valueArray, newSize);
                    EditorGUILayout.EndHorizontal();

                    if(showArray)
                    {
                        if(valueArray != null && valueArray.Length > 0)
                        {
                            for(int i = 0; i < valueArray.Length; i++)
                            {
                                GUI.SetNextControlName("NodeField");
                                valueArray[i] = (Color32)EditorGUILayout.ColorField(
                                    $"{i}:",
                                    valueArray.Length > i ? valueArray[i] : (Color32)Color.white);
                            }
                        }
                    }

                    EditorGUILayout.EndVertical();
                    if(EditorGUI.EndChangeCheck())
                    {
                        variableValue = valueArray;
                        dirty = true;
                    }
                }
                else if(variableType == typeof(ParticleSystem.MinMaxCurve))
                {
                    ParticleSystem.MinMaxCurve minMaxCurve = (ParticleSystem.MinMaxCurve?)variableValue ?? default;
                    EditorGUI.BeginChangeCheck();
                    float multiplier = minMaxCurve.curveMultiplier;
                    AnimationCurve minCurve = minMaxCurve.curveMin;
                    AnimationCurve maxCurve = minMaxCurve.curveMax;
                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.LabelField(symbol);
                    EditorGUI.indentLevel++;
                    multiplier = EditorGUILayout.FloatField("Multiplier", multiplier);
                    minCurve = EditorGUILayout.CurveField("Min Curve", minCurve);
                    maxCurve = EditorGUILayout.CurveField("Max Curve", maxCurve);
                    EditorGUI.indentLevel--;
                    EditorGUILayout.EndVertical();
                    variableValue = new ParticleSystem.MinMaxCurve(multiplier, minCurve, maxCurve);
                    if(EditorGUI.EndChangeCheck())
                    {
                        dirty = true;
                    }
                }
                else if(variableType == typeof(ParticleSystem.MinMaxCurve[]))
                {
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.LabelField(symbol);
                    EditorGUILayout.BeginVertical();
                    ParticleSystem.MinMaxCurve[] valueArray = (ParticleSystem.MinMaxCurve[])variableValue;
                    GUI.SetNextControlName("NodeField");
                    bool showArray = false;
                    if(_arrayStates.ContainsKey(symbol))
                    {
                        showArray = _arrayStates[symbol];
                    }
                    else
                    {
                        _arrayStates.Add(symbol, false);
                    }

                    EditorGUILayout.BeginHorizontal();
                    showArray = EditorGUILayout.Foldout(showArray, GUIContent.none);
                    _arrayStates[symbol] = showArray;

                    int newSize = EditorGUILayout.IntField(
                        "size:",
                        valueArray != null && valueArray.Length > 0 ? valueArray.Length : 1);

                    newSize = newSize >= 0 ? newSize : 0;
                    Array.Resize(ref valueArray, newSize);
                    EditorGUILayout.EndHorizontal();

                    if(showArray)
                    {
                        if(valueArray != null && valueArray.Length > 0)
                        {
                            for(int i = 0; i < valueArray.Length; i++)
                            {
                                GUI.SetNextControlName("NodeField");
                                ParticleSystem.MinMaxCurve minMaxCurve = (ParticleSystem.MinMaxCurve)valueArray[i];
                                float multiplier = minMaxCurve.curveMultiplier;
                                AnimationCurve minCurve = minMaxCurve.curveMin;
                                AnimationCurve maxCurve = minMaxCurve.curveMax;
                                EditorGUILayout.BeginVertical();
                                EditorGUI.indentLevel++;
                                multiplier = EditorGUILayout.FloatField("Multiplier", multiplier);
                                minCurve = EditorGUILayout.CurveField("Min Curve", minCurve);
                                maxCurve = EditorGUILayout.CurveField("Max Curve", maxCurve);
                                EditorGUI.indentLevel--;
                                EditorGUILayout.EndVertical();
                                valueArray[i] = new ParticleSystem.MinMaxCurve(multiplier, minCurve, maxCurve);
                            }
                        }
                    }

                    EditorGUILayout.EndVertical();
                    if(EditorGUI.EndChangeCheck())
                    {
                        variableValue = valueArray;
                        dirty = true;
                    }
                }
                else if(variableType.IsEnum)
                {
                    Enum enumValue = (Enum)variableValue;
                    GUI.SetNextControlName("NodeField");
                    EditorGUI.BeginChangeCheck();
                    variableValue = EditorGUILayout.EnumPopup(symbol, enumValue);
                    if(EditorGUI.EndChangeCheck())
                    {
                        dirty = true;
                    }
                }
                // ReSharper disable once PossibleNullReferenceException
                else if(variableType.IsArray && variableType.GetElementType().IsEnum)
                {
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.LabelField(symbol);
                    EditorGUILayout.BeginVertical();
                    Enum[] valueArray = (Enum[])variableValue;
                    GUI.SetNextControlName("NodeField");
                    bool showArray = false;
                    if(_arrayStates.ContainsKey(symbol))
                    {
                        showArray = _arrayStates[symbol];
                    }
                    else
                    {
                        _arrayStates.Add(symbol, false);
                    }

                    EditorGUILayout.BeginHorizontal();
                    showArray = EditorGUILayout.Foldout(showArray, GUIContent.none);
                    _arrayStates[symbol] = showArray;

                    int newSize = EditorGUILayout.IntField(
                        "size:",
                        valueArray != null && valueArray.Length > 0 ? valueArray.Length : 1);

                    newSize = newSize >= 0 ? newSize : 0;
                    Array.Resize(ref valueArray, newSize);
                    EditorGUILayout.EndHorizontal();

                    if(showArray)
                    {
                        if(valueArray != null && valueArray.Length > 0)
                        {
                            for(int i = 0; i < valueArray.Length; i++)
                            {
                                GUI.SetNextControlName("NodeField");
                                valueArray[i] = EditorGUILayout.EnumPopup(
                                    $"{i}:",
                                    valueArray[i]);
                            }
                        }
                    }

                    EditorGUILayout.EndVertical();
                    if(EditorGUI.EndChangeCheck())
                    {
                        variableValue = valueArray;
                        dirty = true;
                    }
                }
                else if(variableType == typeof(Type))
                {
                    Type typeValue = (Type)variableValue;
                    EditorGUILayout.LabelField(symbol, typeValue == null ? $"Type = null" : $"Type = {typeValue.Name}");
                }

                else if(variableType.IsArray && typeof(UnityEngine.Object).IsAssignableFrom(variableType.GetElementType()))
                {
                    Type elementType = variableType.GetElementType();
                    Assert.IsNotNull(elementType);

                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.LabelField(symbol);
                    EditorGUILayout.BeginVertical();

                    if(variableValue == null)
                    {
                        variableValue = Array.CreateInstance(elementType, 0);
                    }

                    UnityEngine.Object[] valueArray = (UnityEngine.Object[])variableValue;
                    GUI.SetNextControlName("NodeField");
                    int newSize = EditorGUILayout.IntField(
                        "size:",
                        valueArray.Length > 0 ? valueArray.Length : 1);

                    Array.Resize(ref valueArray, newSize);
                    Assert.IsNotNull(valueArray);

                    if(valueArray.Length > 0)
                    {
                        for(int i = 0; i < valueArray.Length; i++)
                        {
                            GUI.SetNextControlName("NodeField");
                            valueArray[i] = EditorGUILayout.ObjectField($"{i}:", valueArray.Length > i ? valueArray[i] : null, variableType.GetElementType(), true);
                        }
                    }

                    EditorGUILayout.EndVertical();
                    if(EditorGUI.EndChangeCheck())
                    {
                        Array destinationArray = Array.CreateInstance(elementType, valueArray.Length);
                        Array.Copy(valueArray, destinationArray, valueArray.Length);

                        variableValue = destinationArray;

                        dirty = true;
                    }
                }
                else if (variableType == typeof(VRC.SDKBase.VRCUrl))
                {
                    if(variableValue == null)
                        variableValue = new VRC.SDKBase.VRCUrl();
                    VRC.SDKBase.VRCUrl url = (VRC.SDKBase.VRCUrl)variableValue;
                    EditorGUI.BeginChangeCheck();
                    url.Set( EditorGUILayout.TextField(symbol, url.Get() ) );
                    if (EditorGUI.EndChangeCheck())
                    {
                        dirty = true;
                    }
                }
                else if (variableType == typeof(VRC.SDKBase.VRCUrl[]))
                {
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.BeginVertical();

                    GUI.SetNextControlName("NodeField");
                    bool showArray = false;
                    if (_arrayStates.ContainsKey(symbol))
                        showArray = _arrayStates[symbol];
                    else
                        _arrayStates.Add(symbol, false);
                    showArray = EditorGUILayout.Foldout( showArray, symbol );
                    _arrayStates[symbol] = showArray;

                    VRC.SDKBase.VRCUrl[] valueArray = (VRC.SDKBase.VRCUrl[])variableValue;

                    if (showArray)
                    {
                        EditorGUI.indentLevel++;
                        int newSize = EditorGUILayout.IntField(
                            "size:",
                            valueArray != null && valueArray.Length > 0 ? valueArray.Length : 1);
                        newSize = newSize >= 0 ? newSize : 0;
                        Array.Resize(ref valueArray, newSize);

                        if (valueArray != null && valueArray.Length > 0)
                        {
                            for (int i = 0; i < valueArray.Length; i++)
                            {
                                GUI.SetNextControlName("NodeField");
                                if (valueArray[i] == null)
                                    valueArray[i] = new VRC.SDKBase.VRCUrl();

                                valueArray[i].Set( EditorGUILayout.TextField(
                                    $"{i}:",
                                    valueArray.Length > i ? valueArray[i].Get() : "") );
                            }
                        }
                        EditorGUI.indentLevel--;
                    }

                    EditorGUILayout.EndVertical();
                    if (EditorGUI.EndChangeCheck())
                    {
                        variableValue = valueArray;
                        dirty = true;
                    }
                }
                else
                {
                    EditorGUILayout.LabelField(symbol + " no defined editor for type of " + variableType);
                }
                // ReSharper restore RedundantNameQualifier

                IUdonSyncMetadata sync = program.SyncMetadataTable.GetSyncMetadataFromSymbol(symbol);
                if(sync != null)
                {
                    GUILayout.Label($"sync{sync.Properties[0].InterpolationAlgorithm.ToString()}", GUILayout.Width(80));
                }
            }

            EditorGUILayout.EndHorizontal();

            return variableValue;
        }

        #region Serialization Methods

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            OnAfterDeserialize();
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            OnBeforeSerialize();
        }

        [PublicAPI]
        protected virtual void OnAfterDeserialize()
        {
        }

        [PublicAPI]
        protected virtual void OnBeforeSerialize()
        {
        }

        #endregion
    }
}
