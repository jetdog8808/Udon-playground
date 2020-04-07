using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor.Graphs;
using UnityEngine.Serialization;
using VRC.Udon.Graph;

namespace VRC.Udon.Editor.ProgramSources
{
    public class UdonNode : Node
    {
        public string uid;

        public override void EndDrag()
        {
            base.EndDrag();

            ((UdonGraph)graph).UpdateNodePosition(this);
        }

        public override void RemovingFromGraph()
        {
            ((UdonGraph)graph).DeleteNode(uid);
            foreach(Edge edge in inputEdges)
            {
                graph.RemoveEdge(edge);
            }

            foreach(Edge edge in outputEdges)
            {
                graph.RemoveEdge(edge);
            }

            base.RemovingFromGraph();
        }
        private static readonly Dictionary<string, UdonNodeDefinition> NodeDefinitionCache =
            new Dictionary<string, UdonNodeDefinition>();

        internal void PopulateEdges()
        {
            UdonNodeData data = ((UdonGraph)graph).data.FindNode(uid);
            //UdonNodeDefinition nodeDefinition = ((UdonGraph)graph).data.
                
            UdonNodeDefinition udonNodeDefinition;
            if (NodeDefinitionCache.ContainsKey(data.fullName))
            {
                udonNodeDefinition = NodeDefinitionCache[data.fullName];
            }
            else
            {
                udonNodeDefinition = UdonEditorManager.Instance.GetNodeDefinition(data.fullName);
                NodeDefinitionCache.Add(data.fullName, udonNodeDefinition);
            }

            for (int i = 0; i < inputDataSlots.Count(); i++) // udonNodeDefinition.Inputs.Count
            {
                if (data.nodeUIDs.Length <= i)
                {
                    continue;
                }
                if (string.IsNullOrEmpty(data.nodeUIDs[i]))
                {
                    continue;
                }

                string[] splitUID = data.nodeUIDs[i].Split('|');
                string nodeUID = splitUID[0];
                int otherIndex = 0;
                if (splitUID.Length > 1)
                {
                    otherIndex = int.Parse(splitUID[1]);
                }

                if (string.IsNullOrEmpty(nodeUID))
                {
                    continue;
                }

                Node connectedNode = graph.nodes.FirstOrDefault(n => ((UdonNode) n).uid == nodeUID);
                if (connectedNode == null)
                {
                    Debug.LogError("Failed to connect node " + nodeUID);
                    data.nodeUIDs[i] = "";
                    ((UdonGraph) graph).ReSerializeData();
                    continue;
                }

                List<Slot> slots = inputDataSlots.ToList();
                if (slots.Count <= i)
                {
                    Debug.LogError($"Failed to find input data slot (index {i}) for node {uid} {data.fullName}");
                    continue;
                }

                Slot destSlot = slots[i];

                if (destSlot == null)
                {
                    Debug.LogError("Failed to find input data slot for node " + uid);
                    continue;
                }

                if (otherIndex < 0)
                {
                    otherIndex = 0;
                }

                if (connectedNode.outputDataSlots.Count() <= otherIndex)
                {
                    otherIndex = 0;
                }
                Slot sourceSlot = connectedNode.outputDataSlots.ToList()[otherIndex]; //.FirstOrDefault();
//                
//                    catch
//                {
//                    Debug.LogError($"failed to connect node {uid} {data.fullName} to node {((UdonNode)connectedNode).uid} | otherindex is {otherIndex}");
//                }


                if(sourceSlot == null)
                {
                    Debug.LogError("Failed to find output data slot for node " + nodeUID);
                    continue;
                }

                graph.Connect(sourceSlot, destSlot);
            }

            for (int i = 0; i < data.flowUIDs.Length; i++)
            {
                string nodeUID = data.flowUIDs[i];
                if (string.IsNullOrEmpty(nodeUID))
                {
                    continue;
                }

                Node connectedNode = graph.nodes.FirstOrDefault(n => ((UdonNode) n).uid == nodeUID);
                if (connectedNode == null)
                {
                    Debug.LogError("Failed to connect flow node " + nodeUID);
                    continue;
                }

                if (uid == "4e2c7cdc-8134-4616-bc83-783dc495759f")
                {
                    int count = 0;
                    foreach (Slot slot in outputFlowSlots) count++;
                    Debug.Log(count);
                }

                Slot sourceSlot = outputFlowSlots.Count() > 1 ? outputFlowSlots.ToArray()[i] : outputFlowSlots.FirstOrDefault();
                if (sourceSlot == null)
                {
                    Debug.LogError("Failed to find output flow slot for node " + uid);
                    continue;
                }

                Slot destSlot = connectedNode.inputFlowSlots.FirstOrDefault();
                if (destSlot == null)
                {
                    Debug.LogError("Failed to find input flow slot for node " + nodeUID);
                    continue;
                }

                graph.Connect(sourceSlot, destSlot);
            }
        }
    }
}
