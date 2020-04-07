using System;
using System.CodeDom;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.CSharp;
using UnityEngine;
using UnityEditor;
using UnityEditor.Graphs;
using VRC.Udon.Common;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.Common.Utils;
using VRC.Udon.Compiler;
using VRC.Udon.EditorBindings;
using VRC.Udon.EditorBindings.Interfaces;
using VRC.Udon.Graph;
using VRC.Udon.Graph.Interfaces;
using VRC.Udon.Serialization;
using VRC.Udon.UAssembly.Assembler;

namespace VRC.Udon.Editor.ProgramSources
{
    public class UdonGraph : UnityEditor.Graphs.Graph
    {
        public IUdonGraphDataProvider graphProgramAsset;
        public UdonGraphData data;

        private readonly string[] _specialFlows =
        {
            "Block",
            "Branch",
            "For",
            "Foreach",
            "While",
        };

        public bool Reloading { get; set; } = false;

        internal static string FriendlyTypeName(Type t)
        {
            if (t == null)
            {
                return "null";
            }

            if (!t.IsPrimitive)
            {
                if(t == typeof(UnityEngine.Object))
                {
                    return "Unity Object";
                }
                return t.Name;
            }
            using (CSharpCodeProvider provider = new CSharpCodeProvider())
            {
                CodeTypeReference typeRef = new CodeTypeReference(t);
                return provider.GetTypeOutput(typeRef);
            }
        }

        public UdonNode CreateNode(UdonNodeData nodeData)
        {
            UdonNodeDefinition udonNodeDefinition;
            try
            {
                udonNodeDefinition = UdonEditorManager.Instance.GetNodeDefinition(nodeData.fullName);
            }
            catch
            {
                Debug.LogError($"Skipping missing node: {nodeData.fullName}");
                return null;
            }
            
            if (!TrySetupNode(udonNodeDefinition, nodeData.position, out UdonNode node, ref nodeData)) return null;
            int connectedFlowCount = nodeData.flowUIDs.Count(f => !string.IsNullOrEmpty(f));
            ValidateNodeData();
            LayoutSlots(udonNodeDefinition, node, connectedFlowCount);
            AddNode(node);
            return node;
            
            void ValidateNodeData()
            {
                bool modifiedData = false;
                for (int i = 0; i < nodeData.nodeValues.Length; i++)
                {
                    if (udonNodeDefinition.Inputs.Count <= i)
                    {
                        continue;
                    }

                    Type expectedType = udonNodeDefinition.Inputs[i].type;

                    if (nodeData.nodeValues[i] == null)
                    {
                        continue;
                    }

                    object value = nodeData.nodeValues[i].Deserialize();
                    if (value == null)
                    {
                        continue;
                    }

                    if (!expectedType.IsInstanceOfType(value))
                    {
                        nodeData.nodeValues[i] = SerializableObjectContainer.Serialize(null, expectedType);
                        modifiedData = true;
                    }
                }

                if (modifiedData)
                {
                    ReSerializeData();
                }
            }
        }
        
        public void CreateNode(UdonNodeDefinition udonNodeDefinition, Vector2? position = null)
        {
            UdonNodeData nodeData = null; 
            if (!TrySetupNode(udonNodeDefinition, position, out UdonNode node, ref nodeData)) return;
            PopulateDefaultValues();
            LayoutSlots(udonNodeDefinition, node, 0);
            ReSerializeData();
            AddNode(node);
            
            void PopulateDefaultValues()
            {
                if (udonNodeDefinition.defaultValues == null) return;
                nodeData.nodeValues = new SerializableObjectContainer[udonNodeDefinition.defaultValues.Count];
                nodeData.nodeUIDs = new string[udonNodeDefinition.defaultValues.Count];
                for (int i = 0; i < udonNodeDefinition.defaultValues.Count; i++)
                {
                    object defaultValue = udonNodeDefinition.defaultValues[i];
                    if (defaultValue != null)
                    {
                        nodeData.nodeValues[i] = SerializableObjectContainer.Serialize(defaultValue);
                    }
                }
            }
        }

        private bool TrySetupNode(UdonNodeDefinition udonNodeDefinition, Vector2? position, out UdonNode node,
            ref UdonNodeData nodeData)
        {
            DoPropHack();
            if (!TryCreateNodeInstance(out node)) return false;
            if (nodeData == null)
            {
                nodeData = data.AddNode(udonNodeDefinition.fullName);
            }
            node.uid = nodeData.uid;
            return true;
            
            void DoPropHack()
            {
                //Awful hack to fix regression in unity graph property type conversion
                {
                    FieldInfo prop = typeof(TypeConverter).GetField(
                        "useCompatibleTypeConversion",
                        BindingFlags.NonPublic | BindingFlags.Static
                    );
                    if (prop != null) prop.SetValue(this, true);
                }
            }
            
            bool TryCreateNodeInstance(out UdonNode outNode)
            {
                outNode = CreateInstance<UdonNode>();

                outNode.name = udonNodeDefinition.fullName;
                outNode.title = PrettyString(udonNodeDefinition.name).FriendlyNameify();
                outNode.position = position == null ? new Rect(Vector2.zero, Vector2.zero) : new Rect(position.Value, Vector2.zero);
                string nodeName = outNode.name;
            
                if (nodeName.StartsWith("Event_") &&
                    (nodeName != "Event_Custom" || graphProgramAsset.GetType() == typeof(UdonSubGraphAsset)))
                {
                    if (nodes.Any(n => n.name == nodeName))
                    {
                        Debug.LogWarning(
                            $"Can't create more than one {nodeName} node, try managing your flow with a Block node instead!");
                        return false;
                    }
                }

                if (nodeName.StartsWith("Event_") &&
                    (nodeName != "Event_Custom" && graphProgramAsset.GetType() == typeof(UdonSubGraphAsset)))
                {
                    Debug.LogWarning($"SubGraphs can't use built-in events, pipe in your event from the parent graph instead!");
                    return false;
                }

                if (outNode.title == "Const_VRCUdonCommonInterfacesIUdonEventReceiver")
                {
                    outNode.title = "UdonBehaviour";
                }

                return true;
            }
        }

        

        private void LayoutSlots(UdonNodeDefinition udonNodeDefinition, UdonNode node, int connectedFlowCount)
        {
            //Layout Flow Slots
            if (udonNodeDefinition.flow)
            {
                if (!udonNodeDefinition.fullName.StartsWith("Event_"))
                {
                    node.AddInputSlot("");
                }

                node.AddOutputSlot("");
                if (_specialFlows.Contains(udonNodeDefinition.fullName))
                {
                    node.AddOutputSlot("");
                }

                if (udonNodeDefinition.fullName == "Block")
                {
                    int connectedFlows = connectedFlowCount;
                    if (connectedFlows > 1)
                    {
                        for (int i = 0; i < connectedFlows - 1; i++)
                        {
                            node.AddOutputSlot("");
                        }
                    }
                }
            }

            //Layout InOut Slots
            for (int index = 0; index < udonNodeDefinition.Inputs.Count; index++)
            {
                UdonNodeParameter input = udonNodeDefinition.Inputs[index];
                string label = "";
                if (udonNodeDefinition.Inputs.Count > index && index >= 0)
                {
                    label = udonNodeDefinition.Inputs[index].name;
                }

                if (label == "IUdonEventReceiver")
                {
                    label = "UdonBehaviour";
                }

                label = label.FriendlyNameify();

                Slot slot = node.AddInputSlot(FriendlyTypeName(input.type),
                    SlotTypeConverter(input.type, udonNodeDefinition.fullName));
                slot.title = label;
            }

            foreach (UdonNodeParameter output in udonNodeDefinition.Outputs)
            {
                node.AddOutputSlot(FriendlyTypeName(output.type), SlotTypeConverter(output.type, udonNodeDefinition.fullName));
            }
        }

        private static Type SlotTypeConverter(Type type, string fullName)
        {
            if(type == null)
            {
                return typeof(object);
            }

            if (fullName.Contains("IUdonEventReceiver") && type == typeof(UnityEngine.Object))
            {
                return typeof(UdonBehaviour);
            }

            return type;
        }

        public void DeleteNode(string nodeID)
        {
            UdonNodeData node = data.FindNode(nodeID);
            if (node == null)
            {
                return;
            }
            data.RemoveNode(node);
            ReSerializeData();
        }

        public override Edge Connect(Slot fromSlot, Slot toSlot)
        {
            int index = 0;
            int indexOther = 0;
            if (fromSlot.isFlowSlot)
            {
                foreach(Slot outputSlot in fromSlot.node.outputFlowSlots)
                {
                    if(outputSlot == fromSlot)
                    {
                        break;
                    }

                    index++;
                }
            }
            else
            {
                foreach(Slot inputSlot in toSlot.node.inputDataSlots)
                {
                    if(inputSlot == toSlot)
                    {
                        break;
                    }

                    index++;
                }    
                foreach(Slot outputSlot in fromSlot.node.outputDataSlots)
                {
                    if(outputSlot == fromSlot)
                    {
                        break;
                    }

                    indexOther++;
                }    
            }
            

            UdonNodeData fromNode = data.FindNode(((UdonNode)fromSlot.node).uid);
            UdonNodeData toNode = data.FindNode(((UdonNode)toSlot.node).uid);
            if(fromSlot.isFlowSlot)
            {
                fromNode.AddFlowNode(toNode, index);
            }
            else
            {
                toNode.AddNode(fromNode, index, indexOther);
            }
            
            if (fromNode.fullName == "Block")
            {
                int connectedFlows = fromNode.flowUIDs.Count(f => !string.IsNullOrEmpty(f));
                if (connectedFlows >= fromSlot.node.outputFlowSlots.Count())
                {
                    fromSlot.node.AddOutputSlot("");
                }
            }

            ReSerializeData();
            return base.Connect(fromSlot, toSlot);
        }

        public override void RemoveEdge(Edge e)
        {
            int index = 0;
            if (e.fromSlot.isFlowSlot)
            {
                foreach(Slot outputSlot in e.fromSlot.node.outputFlowSlots)
                {
                    if(outputSlot == e.fromSlot)
                    {
                        break;
                    }

                    index++;
                }
            }
            else
            {
                foreach(Slot inputSlot in e.toSlot.node.inputDataSlots)
                {
                    if(inputSlot == e.toSlot)
                    {
                        break;
                    }

                    index++;
                }    
            }

            UdonNodeData toNode = data.FindNode(((UdonNode)e.toSlot.node).uid);
            UdonNodeData fromNode = data.FindNode(((UdonNode)e.fromSlot.node).uid);
            if(e.fromSlot.isFlowSlot)
            {
                fromNode.RemoveFlowNode(index);
            }
            else
            {
                toNode.RemoveNode(index);
            }

            ReSerializeData();

            base.RemoveEdge(e);
        }

        public override bool CanConnect(Slot fromSlot, Slot toSlot)
        {
            if(fromSlot.node == toSlot.node)
            {
                return false;
            }

            if(fromSlot.isFlowSlot && toSlot.isFlowSlot)
            {
                return FindRecursiveFlow(fromSlot, toSlot);
                //return fromSlot.edges.Count <= 0 && toSlot.edges.Count <= 0 && FindRecursiveFlow(fromSlot, toSlot);
            }

            if(fromSlot.isFlowSlot && !toSlot.isFlowSlot)
            {
                return false;
            }

            if(!fromSlot.isFlowSlot && toSlot.isFlowSlot)
            {
                return false;
            }

            if (toSlot.dataType.IsAssignableFrom(fromSlot.dataType) ||
                fromSlot.dataType.IsAssignableFrom(toSlot.dataType))
            {
                return true;
            }
            if (fromSlot.node.name.Contains("__T") || fromSlot.node.name.Contains("__TArray"))
            {
                return true;
            }
            
            return false;
        }

        private static bool FindRecursiveFlow(Slot fromSlot, Slot toSlot)
        {
            foreach(Edge edge in toSlot.node.outputFlowEdges)
            {
                if(edge.toSlot.node == fromSlot.node)
                {
                    return false;
                }

                if(!FindRecursiveFlow(fromSlot, edge.toSlot))
                {
                    return false;
                }
            }

            return true;
        }

        public void Reload()
        {

            if (this == null)
            {
                DestroyImmediate(this);
                return;
            }
            Reloading = true;
            // ReSharper disable once DelegateSubtraction
            Undo.undoRedoPerformed -= OnUndoRedo; //Remove old handler if present to prevent duplicates, doesn't cause errors if not present
            Undo.undoRedoPerformed += OnUndoRedo;

            nodes.Clear();
            edges.Clear();

            IEnumerable<UdonNodeDefinition> definitions = UdonEditorManager.Instance.GetNodeDefinitions();
            if (definitions == null || definitions.Count() < 100)
            {
                throw new NullReferenceException("Udon NodeDefinitions have failed to load, aborting graph load.");
            }
            
            for (int i = data.nodes.Count - 1; i >= 0; i--)
            {
                UdonNodeData node = data.nodes[i];
                UdonNode udonNode = CreateNode(node);
                if (udonNode != null) continue;
                Debug.Log($"Removing null node '{node.fullName}'");
                data.nodes.RemoveAt(i);
            }

            foreach(Node node in nodes)
            {
                UdonNode udonNode = (UdonNode)node;
                udonNode.PopulateEdges();
            }

            Reloading = false;
            ReSerializeData();
        }

        private void OnUndoRedo()
        {
            data = new UdonGraphData(graphProgramAsset.GetGraphData());
            Reload();
        }

        public override void RemoveNode(Node node, bool destroyNode = false)
        {
            if (node == null)
            {
                return;
            }
            base.RemoveNode(node, destroyNode);
        }

        public void ReSerializeData()
        {
            if (Reloading)
            {
                return;
            }

            SerializedObject serializedGraphProgramAsset;
            if (graphProgramAsset.GetType() == typeof(UdonGraphProgramAsset))
            {
                serializedGraphProgramAsset = new SerializedObject((UdonGraphProgramAsset)graphProgramAsset); 
            }
            else
            {
                serializedGraphProgramAsset = new SerializedObject((UdonSubGraphAsset)graphProgramAsset);
            }
            
            SerializedProperty graphDataProperty = serializedGraphProgramAsset.FindProperty("graphData");
            SerializedProperty nodesProperty = graphDataProperty.FindPropertyRelative("nodes");

            if(nodesProperty.arraySize > data.nodes.Count)
            {
                nodesProperty.ClearArray();
            }

            for(int i = 0; i < data.nodes.Count; i++)
            {
                if(nodesProperty.arraySize < data.nodes.Count)
                {
                    nodesProperty.InsertArrayElementAtIndex(i);
                }

                SerializedProperty nodeProperty = nodesProperty.GetArrayElementAtIndex(i);

                SerializedProperty fullNameProperty = nodeProperty.FindPropertyRelative("fullName");
                fullNameProperty.stringValue = data.nodes[i].fullName;

                SerializedProperty uidProperty = nodeProperty.FindPropertyRelative("uid");
                uidProperty.stringValue = data.nodes[i].uid;

                SerializedProperty positionProperty = nodeProperty.FindPropertyRelative("position");
                positionProperty.vector2Value = data.nodes[i].position;

                SerializedProperty nodeUIDsProperty = nodeProperty.FindPropertyRelative("nodeUIDs");
                while(nodeUIDsProperty.arraySize > data.nodes[i].nodeUIDs.Length)
                {
                    nodeUIDsProperty.DeleteArrayElementAtIndex(nodeUIDsProperty.arraySize - 1);
                }

                for(int j = 0; j < data.nodes[i].nodeUIDs.Length; j++)
                {
                    if(nodeUIDsProperty.arraySize < data.nodes[i].nodeUIDs.Length)
                    {
                        nodeUIDsProperty.InsertArrayElementAtIndex(j);
                        nodeUIDsProperty.GetArrayElementAtIndex(j).stringValue = "";
                    }

                    SerializedProperty nodeUIDProperty = nodeUIDsProperty.GetArrayElementAtIndex(j);
                    nodeUIDProperty.stringValue = data.nodes[i].nodeUIDs[j];
                }

                SerializedProperty flowUIDsProperty = nodeProperty.FindPropertyRelative("flowUIDs");
                while(flowUIDsProperty.arraySize > data.nodes[i].flowUIDs.Length)
                {
                    flowUIDsProperty.DeleteArrayElementAtIndex(flowUIDsProperty.arraySize - 1);
                }

                for(int j = 0; j < data.nodes[i].flowUIDs.Length; j++)
                {
                    if(flowUIDsProperty.arraySize < data.nodes[i].flowUIDs.Length)
                    {
                        flowUIDsProperty.InsertArrayElementAtIndex(j);
                        flowUIDsProperty.GetArrayElementAtIndex(j).stringValue = "";
                    }

                    SerializedProperty flowUIDProperty = flowUIDsProperty.GetArrayElementAtIndex(j);
                    flowUIDProperty.stringValue = data.nodes[i].flowUIDs[j];
                }

                SerializedProperty nodeValuesProperty = nodeProperty.FindPropertyRelative("nodeValues");
                while(nodeValuesProperty.arraySize > data.nodes[i].nodeValues.Length)
                {
                    nodeValuesProperty.DeleteArrayElementAtIndex(nodeValuesProperty.arraySize - 1);
                }

                for(int j = 0; j < data.nodes[i].nodeValues.Length; j++)
                {
                    if(nodeValuesProperty.arraySize < data.nodes[i].nodeValues.Length)
                    {
                        nodeValuesProperty.InsertArrayElementAtIndex(j);
                        nodeValuesProperty.GetArrayElementAtIndex(j).FindPropertyRelative("unityObjectValue").objectReferenceValue = null;
                        nodeValuesProperty.GetArrayElementAtIndex(j).FindPropertyRelative("stringValue").stringValue = "";
                    }

                    SerializedProperty nodeValueProperty = nodeValuesProperty.GetArrayElementAtIndex(j);

                    if (data.nodes[i].nodeValues[j] == null)
                    {
                        continue;
                    }
                    object nodeValue = data.nodes[i].nodeValues[j].Deserialize();
                    if (nodeValue != null)
                    {
                        if (nodeValue is UnityEngine.Object value)
                        {
                            if (value != null)
                            {
                                nodeValueProperty.FindPropertyRelative("unityObjectValue").objectReferenceValue =
                                    data.nodes[i].nodeValues[j].unityObjectValue;
                            }
                        }
                    }
                    nodeValueProperty.FindPropertyRelative("stringValue").stringValue =
                        data.nodes[i].nodeValues[j].stringValue;
                }
            }

            serializedGraphProgramAsset.ApplyModifiedProperties();

            if (graphProgramAsset is AbstractUdonProgramSource udonProgramSource)
            {
                UdonEditorManager.Instance.QueueProgramSourceRefresh(udonProgramSource);
            }
        }

        public void UpdateNodePosition(UdonNode node)
        {
            data.FindNode(node.uid).position = node.position.position;
            ReSerializeData();
        }

        private static string PrettyString(string s)
        {
            switch(s)
            {
                case "op_Equality":
                    s = "==";
                    break;

                case "op_Inequality":
                    s =  "!=";
                    break;

                case "op_Addition":
                    s =  "+";
                    break;
                case "VRCUdonCommonInterfacesIUdonEventReceiver":
                    s = "UdonBehaviour";
                    break;
                // ReSharper disable once RedundantEmptySwitchSection
                default:
                    break;
            }

            s = s.Replace("_", " ");
            s = ParseByCase(s);
            TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;
            return textInfo.ToTitleCase(s);
        }

        private static string ParseByCase(string strInput)
        {
            string strOutput = "";
            int intCurrentCharPos = 0;
            int intLastCharPos = strInput.Length - 1;
            for(intCurrentCharPos = 0; intCurrentCharPos <= intLastCharPos; intCurrentCharPos++)
            {
                char chrCurrentInputChar = strInput[intCurrentCharPos];
                char chrPreviousInputChar = chrCurrentInputChar;
                if(intCurrentCharPos > 0)
                {
                    chrPreviousInputChar = strInput[intCurrentCharPos - 1];
                }

                if(char.IsUpper(chrCurrentInputChar) && char.IsLower(chrPreviousInputChar))
                {
                    strOutput += " ";
                }

                strOutput += chrCurrentInputChar;
            }

            return strOutput;
        }
    }
}
