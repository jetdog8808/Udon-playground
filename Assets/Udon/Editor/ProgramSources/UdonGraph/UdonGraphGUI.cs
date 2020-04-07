using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEditor;
using UnityEditor.Graphs;
using VRC.Udon.EditorBindings;
using VRC.Udon.EditorBindings.Interfaces;
using VRC.Udon.Graph;
using VRC.Udon.Graph.Interfaces;
using VRC.Udon.Serialization;
using CompressionLevel = System.IO.Compression.CompressionLevel;
using Object = UnityEngine.Object;

namespace VRC.Udon.Editor.ProgramSources
{
    // TODO: Use https://docs.unity3d.com/2018.4/Documentation/ScriptReference/Experimental.UIElements.GraphView.GraphView.html instead.
    [Serializable]
    public class UdonGraphGUI : GraphGUI
    {
        private Texture2D nodeBackground;
        private Texture2D activeNodeBackground;
        private Texture2D nodeAccent;
        private Texture2D nodeInlay;
        private Texture2D slotTexture;
        private Texture2D flowSlotTexture;

        public EditorWindow Host => m_Host;

        private List<string> _variablePopupOptions = new List<string>();
        private List<UdonNodeData> _variableNodes = new List<UdonNodeData>();
        private string _lastActiveControl = "";
        
        private static GUIStyle _udonLogo;
        private const float CONTENT_LOGO_SCALE = .75f;
        bool m_mouseDown = false;
        
        public override void OnGraphGUI()
        {
            //Show node sub-windows
            m_Host.BeginWindows();

            InitializeStyles();

            _variableNodes = ((UdonGraph)graph).data.nodes
                .Where(n => n.fullName.StartsWithCached("Variable_")).Where(n => n.nodeValues.Length > 1 && n.nodeValues[1] != null).ToList();
            _variablePopupOptions =
                _variableNodes.Select(s => (string) s.nodeValues[1].Deserialize()).ToList();


            if (_udonLogo == null)
            {
                Texture2D logoTexture = Resources.Load<Texture2D>("UdonLogoAlpha");
                _udonLogo = new GUIStyle
                {
                    normal =
                    {
                        background = logoTexture,
                        textColor = Color.white,
                    },
                    fixedHeight = (int) (logoTexture.height * CONTENT_LOGO_SCALE),
                    fixedWidth = (int) (logoTexture.width * CONTENT_LOGO_SCALE),
                };
            }

            Rect graphExtents = (Rect)GetInstanceField(typeof(UdonGraph), (UdonGraph)graph, "graphExtents");
            graphExtents = new Rect(new Vector2(scrollPosition.x + (graphExtents.x + (Host.position.width / 2)) - 125, scrollPosition.y + (graphExtents.y + (Host.position.height / 2)) - 24), new Vector2(10, 10));
            
            int guiDepth = GUI.depth;
            Color guiColor = GUI.color;
            GUI.depth = 100;
            GUI.color = new Color(1, 1, 1, .75f);
            GUILayout.Window(
                123456789,
                new Rect(graphExtents.position.x-(_udonLogo.fixedWidth/3), graphExtents.position.y-(_udonLogo.fixedHeight/2), 300, 300),
                delegate { DrawLogo(); },
                "", _udonLogo 
                );
            GUI.depth = guiDepth;
            GUI.color = guiColor;
            
            foreach (Node node in graph.nodes)
            {
                // Recapture the variable for the delegate.
                Node node2 = node;

                if (m_EdgeGUI == null || m_EdgeGUI.GetType() != typeof(UdonEdgeGUI))
                {
                    m_EdgeGUI = new UdonEdgeGUI
                    {
                        host = this
                    };
                }


                bool isActive = selection.Contains(node);
                GUIStyle style = Styles.GetNodeStyle(node.style, node.color, isActive);
                style.normal.background = isActive ? activeNodeBackground : nodeBackground;
                style.border = new RectOffset(10, 30, 40, 10);

                node.position = GUILayout.Window(
                    node.GetInstanceID(),
                    node.position,
                    delegate { NodeGUI(node2); },
                    node.title,
                    style,
                    GUILayout.Height(10)
                );
            }
            
            if (_doReSerialize)
            {
                //Leaving this mess here incase the selection issue regresses and I have to deal with it like this
                //EditorGUI.FocusTextInControl(_lastActiveControl);
                //int currentKeyboardControl = GUIUtility.keyboardControl;
                //TextEditor te = (TextEditor) GUIUtility.GetStateObject(typeof(TextEditor), currentKeyboardControl);
                //Debug.Log($"Before {te.cursorIndex} : {te.text} : Control = {_lastActiveControl}");
                ((UdonGraph)graph).ReSerializeData();
                _doReSerialize = false;
                //EditorGUILayout.TextField("");
                //if (!string.IsNullOrEmpty(_lastActiveControl))
                //{
                //    GUI.FocusControl(_lastActiveControl);
                //}
                //GUIUtility.keyboardControl = currentKeyboardControl;
                //GUI.FocusControl(_lastActiveControl);
                //EditorGUI.FocusTextInControl(_lastActiveControl);
                //te = (TextEditor) GUIUtility.GetStateObject(typeof(TextEditor), currentKeyboardControl);
                //if (te != null)
                //{
                //    te.SelectNone();
                //    te.MoveTextEnd();
                //    Debug.Log($"After {te.cursorIndex}");
                //}
            }

            // Workaround: If there is no node in the graph, put an empty
            // window to avoid corruption due to a bug.
            if (graph.nodes.Count == 0)
            {
                GUILayout.Window(0, new Rect(0, 0, 1, 1), delegate { }, "", "MiniLabel");
            }

            m_Host.EndWindows();

            //Graph edges
            edgeGUI.DoEdges();
            edgeGUI.DoDraggedEdge();
            
            // Deselect nodes when clicking outside the graph //
            if (Event.current.type == EventType.MouseUp && m_mouseDown)
            {
                m_mouseDown = false;
                ClearSelection();
                selection.Clear();
            }
            if (Event.current.type == EventType.MouseDown)
                m_mouseDown = true;
            if (Event.current.type == EventType.MouseDrag)
                m_mouseDown = false;
            ///////////////////////////////////////////////////

            DragSelection();

            ShowContextMenu();

            HandleMenuEvents();
        }

        
        
        private static void DrawLogo()
        {
            //Do nothing, just drawing a logo for now
        }

        //TODO: Move all this color stuff to a utility file?
        private static class NodeColors
        {
            public static readonly Color Base = new Color(77f / 255f, 157f / 255f, 1);
            public static readonly Color Const = new Color(0.4f, 0.14f, 1f);
            public static readonly Color Variable = new Color(1f, 0.86f, 0.3f);
            public static readonly Color Function = new Color(1f, 0.42f, 0.14f);
            public static readonly Color Event = new Color(0.53f, 1f, 0.3f);
            public static readonly Color Return = new Color(0.89f, 0.2f, 0.2f);
        }

        private static readonly MD5 _md5Hasher = MD5.Create();
        private static readonly Dictionary<Type, Color> _typeColors = new Dictionary<Type, Color>();

        public static Color MapTypeToColor(Type type)
        {
            if (type == null)
            {
                return Color.white;
            }

            if (type.IsPrimitive)
            {
                return new Color(0.12f, 0.53f, 0.9f);
            }

            if (typeof(Object).IsAssignableFrom(type))
            {
                return new Color(0.9f, 0.23f, 0.39f);
            }

            if (type.IsValueType)
            {
                return NodeColors.Variable;
            }

            if (_typeColors.ContainsKey(type))
            {
                return _typeColors[type];
            }

            byte[] hashed = _md5Hasher.ComputeHash(type.ToString() == "T"
                ? System.Text.Encoding.UTF8.GetBytes("T")
                : System.Text.Encoding.UTF8.GetBytes(type.Name));
            int iValue = BitConverter.ToInt32(hashed, 0);

            //TODO: Make this provide more varied colors
            Color color = Color.HSVToRGB((iValue & 0xff) / 255f, .69f, 1f);


            _typeColors.Add(type, color);
            return color;
        }

        private void DrawDocumentationLink(UdonNodeDefinition nodeDefinition)
        {
            List<string> specialNames = new List<string>
            {
                "Block",
                "Branch",
                "For",
                "While",
                "Foreach",
                "Get_Variable",
                "Set_Variable",
                "Event_Custom",
                "Event_OnDataStorageAdded",
                "Event_OnDataStorageChanged",
                "Event_OnDataStorageRemoved",
                "Event_OnDrop",
                "Event_Interact",
                "Event_OnNetworkReady",
                "Event_OnOwnershipTransferred",
                "Event_OnPickup",
                "Event_OnPickupUseDown",
                "Event_OnPickupUseUp",
                "Event_OnPlayerJoined",
                "Event_OnPlayerLeft",
                "Event_OnSpawn",
                "Event_OnStationEntered",
                "Event_OnStationExited",
                "Event_OnVideoEnd",
                "Event_OnVideoPause",
                "Event_OnVideoPlay",
                "Event_OnVideoStart",
                "VRCUdonCommonInterfacesIUdonEventReceiver.__SendCustomEvent__SystemString__SystemVoid",
                "VRCUdonCommonInterfacesIUdonEventReceiver.__SetHeapVariable__SystemString_SystemObject__SystemVoid",
                "VRCUdonCommonInterfacesIUdonEventReceiver.__GetHeapVariable__SystemString__SystemObject",
                "Const_VRCUdonCommonInterfacesIUdonEventReceiver",
            };

            if (nodeDefinition.type == null || nodeDefinition.type.Namespace == null)
            {
                return;
            }

            if (specialNames.Contains(nodeDefinition.fullName))
            {
                return;
            }

            if (!nodeDefinition.type.Namespace.Contains("UnityEngine") &&
                !nodeDefinition.type.Namespace.Contains("System"))
            {
                return;
            }

            if (!GUILayout.Button(EditorGUIUtility.IconContent("_Help"), GUIStyle.none, GUILayout.ExpandWidth(false)))
            {
                return;
            }

            if (nodeDefinition.fullName.StartsWithCached("Event_"))
            {
                string url = "https://docs.unity3d.com/2018.4/Documentation/ScriptReference/MonoBehaviour.";
                url += nodeDefinition.name;
                url += ".html";
                Help.BrowseURL(url);
                return;
            }

            if (nodeDefinition.fullName.Contains("Array.__ctor"))
            {
                //I couldn't find the array constructor documentation
                const string URL = "https://docs.microsoft.com/en-us/dotnet/api/system.array?view=netframework-4.8";
                Help.BrowseURL(URL);
                return;
            }

            if (nodeDefinition.fullName.Contains("Array.__Get"))
            {
                const string URL = "https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/operators/member-access-operators#indexer-operator-";
                Help.BrowseURL(URL);
                return;
            }

            if (nodeDefinition.fullName.Contains(".__Equals__SystemObject"))
            {
                const string URL = "https://docs.microsoft.com/en-us/dotnet/api/system.object.equals?view=netframework-4.8";
                Help.BrowseURL(URL);
                return;
            }

            if (nodeDefinition.name.Contains("[]"))
            {
                string url = "https://docs.microsoft.com/en-us/dotnet/api/system.array.";
                url += nodeDefinition.name.Split(' ')[1];
                url += "?view=netframework-4.8";
                Help.BrowseURL(url);
                return;
            }

            if (nodeDefinition.type.Namespace.Contains("UnityEngine"))
            {
                string url = "https://docs.unity3d.com/2018.4/Documentation/ScriptReference/";
                Debug.Log(nodeDefinition.type.Namespace);
                if (nodeDefinition.type.Namespace != "UnityEngine")
                {
                    url += nodeDefinition.type.Namespace.Replace("UnityEngine.", "");
                    url += ".";
                }
                url += nodeDefinition.type.Name;

                if (nodeDefinition.fullName.Contains("__get_") || nodeDefinition.fullName.Contains("__set_"))
                {
                    if (nodeDefinition.fullName.Contains("__get_"))
                    {
                        url += "-" + nodeDefinition.name.Split(new[] {"get_"}, StringSplitOptions.None)[1];
                    }
                    else
                    {
                        url += "-" + nodeDefinition.name.Split(new[] {"set_"}, StringSplitOptions.None)[1];
                    }

                    url += ".html";
                    Help.BrowseURL(url);
                    return;
                }

                if (nodeDefinition.fullName.Contains("Const_") || nodeDefinition.fullName.Contains("Type_") || nodeDefinition.fullName.Contains("Variable_"))
                {
                    url += ".html";
                    Help.BrowseURL(url);
                    return;
                }

                {
                    // Methods
                    url += "." + nodeDefinition.name.Split(' ')[1];
                    url += ".html";
                    Help.BrowseURL(url);
                    return;
                }
            }

            if (nodeDefinition.type.Namespace.Contains("System"))
            {
                string url = "https://docs.microsoft.com/en-us/dotnet/api/system.";
                url += nodeDefinition.type.Name;
                if (nodeDefinition.fullName.Contains("__get_") || nodeDefinition.fullName.Contains("__set_"))
                {
                    url += "." + nodeDefinition.name.Split(' ')[1].Replace("get_", "").Replace("set_", "");
                    url += "?view=netframework-4.8";
                    Help.BrowseURL(url);
                    return;
                }

                if (nodeDefinition.name == "ctor")
                {
                    url += ".-ctor";
                    url += "?view=netframework-4.8#System_";
                    url += nodeDefinition.type.Name + "__ctor_";
                    foreach (var pType in nodeDefinition.Inputs)
                    {
                        url += "System_" + pType.type.Name.Replace('[', '_').Replace(']', '_') + "_";
                    }

                    Help.BrowseURL(url);
                    return;
                }

                if (nodeDefinition.fullName.Contains("Const_") || nodeDefinition.fullName.Contains("Type_"))
                {
                    url += "?view=netframework-4.8";
                    Help.BrowseURL(url);
                    return;
                }

                {
                    // Methods
                    url += "." + nodeDefinition.name.Split(' ')[1];
                    url += "?view=netframework-4.8";
                    Help.BrowseURL(url);
                    return;
                }
            }
        }

        public override void NodeGUI(Node node)
        {
            if (node == null)
            {
                return;
            }

            //if (node.name.StartsWithCached("Event"))
            //{
            //    EditorGUILayout.GetControlRect(false, 0, GUILayout.MinWidth(GUI.skin.label.CalcSize(new GUIContent(node.title)).x + 40));
            //}
            //else
            {
                EditorGUILayout.GetControlRect(false, 0,
                    GUILayout.MinWidth(GUI.skin.label.CalcSize(new GUIContent(node.title)).x + 10));
            }

            Color nodeColor = NodeColors.Base;
            if (node.name.StartsWithCached("Event_"))
            {
                nodeColor = NodeColors.Event;
            }
            //else if (node.name.StartsWithCached("Const_"))
            //{
            //    nodeColor = NodeColors.Const;
            //}
            //else if (node.name.StartsWithCached("Func_"))
            //{
            //    nodeColor = NodeColors.Function;
            //}
            //else if (node.name.StartsWithCached("Var_"))
            //{
            //    nodeColor = NodeColors.Variable;
            //}
            //else if (node.name.StartsWithCached("Ret_"))
            //{
            //    nodeColor = NodeColors.Return;
            //}

            GUI.DrawTexture(
                new Rect(
                    0,
                    4,
                    EditorGUILayout.GetControlRect(
                        false,
                        0f
                    ).width + 8,
                    45
                ),
                nodeAccent,
                ScaleMode.StretchToFill,
                true,
                0,
                nodeColor,
                0,
                0);


            Rect controlRect = EditorGUILayout.GetControlRect(false, 0f);
            float outputSlotSize = 0;
            if (node.outputSlots.Any())
            {
                outputSlotSize = OutputsWidth((UdonNode) node) + 30;
                GUI.DrawTexture(new Rect(controlRect.width - outputSlotSize + 10, 25, outputSlotSize, 500), nodeInlay,
                    ScaleMode.StretchToFill);
            }

            UdonNodeData nodeData = ((UdonGraph) graph).data.FindNode(((UdonNode) node).uid);
            UdonNodeDefinition udonNodeDefinition;
            if (_nodeDefinitionCache.ContainsKey(nodeData.fullName))
            {
                udonNodeDefinition = _nodeDefinitionCache[nodeData.fullName];
            }
            else
            {
                udonNodeDefinition = UdonEditorManager.Instance.GetNodeDefinition(nodeData.fullName);
                _nodeDefinitionCache.Add(nodeData.fullName, udonNodeDefinition);
            }

            EditorGUILayout.BeginHorizontal();

            DrawOverloads(node, nodeData);

            GUILayout.FlexibleSpace();
            DrawDocumentationLink(udonNodeDefinition);

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();

            //      Slot Layout Code      //

            SelectNode(node);

            List<Slot> outputSlots = new List<Slot>(node.outputSlots);

            string nodeName = ((UdonNode) node).name;
            
            for (int i = 0; i < node.inputSlots.Count(); i++)
            {
                Slot slot = node.inputSlots.ElementAt(i);

                if (nodeName == "SubGraph")
                {
                    EditorGUILayout.LabelField("Coming soon!");
                    break;
                }
                
                if (!_slotDataTypeCache.TryGetValue(slot, out Type slotDataType))
                {
                    slotDataType = slot.dataType;
                    _slotDataTypeCache.Add(slot, slotDataType);
                }

                //bool drawConstNameField = nodeName.StartsWithCached("Const_") && nodeData.nodeValues != null && nodeData.nodeValues.Length > 2 &&
                //                          nodeData.nodeValues[2]?.Deserialize() != null &&
                //                          (bool) nodeData.nodeValues[2].Deserialize();
                //if (nodeName.StartsWithCached("Const_") && !drawConstNameField && i == 1) //name field
                //{
                //    continue;
                //}
                
                if (i == 3 && nodeName.StartsWithCached("Variable_"))
                {
                    if (udonNodeDefinition.type.IsValueType || udonNodeDefinition.type == typeof(string) || (
                            // ReSharper disable once PossibleNullReferenceException
                            udonNodeDefinition.type.IsArray && udonNodeDefinition.type.GetElementType().IsValueType)
                    ) 
                    {
                        //sync field
                    }
                    else
                    {
                        continue;    
                    }
                }
                
                bool drawSyncField = nodeName.StartsWithCached("Variable_") && nodeData.nodeValues != null && nodeData.nodeValues.Length > 3 &&
                                     nodeData.nodeValues[3]?.Deserialize() != null &&
                                     (bool) nodeData.nodeValues[3].Deserialize();
                if (nodeName.StartsWithCached("Variable_") && !drawSyncField && i == 4) //syncMode field
                {
                    continue;
                }

                EditorGUILayout.BeginHorizontal();
                if (!nodeName.StartsWithCached("Const_") && nodeName != "Event_Custom")
                {
                    GUILayout.Space(5);
                }

                GUI.backgroundColor = new Color(.85f, .85f, .85f);

                if (slot.edges.Count == 0 && !slot.isFlowSlot)
                {
                    if (!nodeName.StartsWithCached("Const_") &&
                        !nodeName.StartsWithCached("Variable_") &&
                        nodeName != "Comment" && (
                            nodeName != "Event_Custom" &&
                            nodeName != "Set_Variable" &&
                            nodeName != "Get_Variable"|| i > 1)
                        )
                    {
                        if (nodeName != "SubGraph" || !slot.isDataSlot || slot.title != "asset")
                        {
                            LayoutSlot(
                                slot,
                                "",
                                false,
                                true,
                                false,
                                slot.name == "" ? Styles.triggerPinIn : Styles.varPinIn);
                        }
                    }

                    Rect offsetRect = EditorGUILayout.GetControlRect(GUILayout.ExpandWidth(false),
                        GUILayout.MinWidth(160), GUILayout.MaxWidth(160));
                    EditorGUI.BeginChangeCheck();
                    if (nodeName != "Const_This" && nodeName != "Const_Null")
                    {
                        bool showField = ! (typeof(UnityEngine.Object).IsAssignableFrom(slotDataType) && slotDataType != typeof(ScriptableObject) && slotDataType != typeof(Texture)); //nodeData.fullName.StartsWithCached("Const_") &&  && i == 0 
                        DrawSlotProperty(slot, offsetRect, showField);
                    }
                }
                else
                {
                    GUI.backgroundColor = new Color(.85f, .85f, .85f);
                    LayoutSlot(slot, "", false, true, false, slot.name == "" || slot.dataTypeString == "" ? Styles.triggerPinIn : Styles.varPinIn);
                    Rect offsetRect = EditorGUILayout.GetControlRect(GUILayout.ExpandWidth(false),
                        GUILayout.MinWidth(160), GUILayout.MaxWidth(160));
                    DrawSlotProperty(slot, offsetRect, false); //slot.name
                }

                if (slot.edges.Count != 0)
                {
                    EditorGUILayout.GetControlRect(false, 0,
                        GUILayout.MinWidth(GUI.skin.label.CalcSize(new GUIContent(slot.name)).x + 5));
                }
                else
                {
                    EditorGUILayout.GetControlRect(false, 0, GUILayout.MinWidth(5));
                }

                if (outputSlots.Count > 0)
                {
                    DrawOutputSlot(outputSlots[0]);
                    outputSlots.RemoveAt(0);
                }
                else
                {
                    GUILayout.Space(outputSlotSize);
                }

                EditorGUILayout.EndHorizontal();
                GUILayout.Space(4);
                GUI.backgroundColor = Color.white;
            }

            for (int i = 0; i < outputSlots.Count; i++)
            {
                if (nodeName == "SubGraph")
                {
                    break;
                }
                
                EditorGUILayout.BeginHorizontal();
                Slot slot = outputSlots[i];
                GUILayout.FlexibleSpace();
                DrawOutputSlot(slot);
                if (i > 0)
                {
                    GUILayout.Space(4);
                }

                EditorGUILayout.EndHorizontal();
                GUILayout.Space(4);
            }

            node.NodeUI(this);
            DragNodes();
        }

        private readonly Dictionary<Node, IList<UdonNodeDefinition>> _overloadCache = new Dictionary<Node, IList<UdonNodeDefinition>>();
        private readonly Dictionary<UdonNodeDefinition, string> _optionNameCache = new Dictionary<UdonNodeDefinition, string>();
        private readonly Dictionary<UdonNodeDefinition, string> _cleanerOptionNameCache = new Dictionary<UdonNodeDefinition, string>();
        private void DrawOverloads(Node node, UdonNodeData nodeData)
        {
            if (node == null)
            {
                return;
            }

            if (!_overloadCache.TryGetValue(node, out IList<UdonNodeDefinition> udonNodeDefinitions))
            {
                string baseIdentifier = node.name;
                string[] splitBaseIdentifier = baseIdentifier.Split(new[] {"__"}, StringSplitOptions.None);
                if (splitBaseIdentifier.Length >= 2)
                {
                    baseIdentifier = $"{splitBaseIdentifier[0]}__{splitBaseIdentifier[1]}__";
                }

                if (baseIdentifier.StartsWithCached("Const_"))
                {
                    return;
                }

                if (baseIdentifier.StartsWithCached("Type_"))
                {
                    baseIdentifier = "Type_";
                }

                if (baseIdentifier.StartsWithCached("Variable_"))
                {
                    baseIdentifier = "Variable_";
                }

                IEnumerable<UdonNodeDefinition> matchingNodeDefinitions =
                    UdonEditorManager.Instance.GetNodeDefinitions(baseIdentifier);
                udonNodeDefinitions = matchingNodeDefinitions.ToList();
                _overloadCache.Add(node, udonNodeDefinitions);
            }

            if (udonNodeDefinitions.Count <= 1)
            {
                return;
            }

            int currentIndex = 0;
            for (int i = 0; i < udonNodeDefinitions.Count; i++)
            {
                if (udonNodeDefinitions.ElementAt(i).fullName != node.name)
                {
                    continue;
                }

                currentIndex = i;
                break;
            }

            GUIContent[] options = new GUIContent[udonNodeDefinitions.Count];
            for (int i = 0; i < udonNodeDefinitions.Count; i++)
            {
                UdonNodeDefinition nodeDefinition = udonNodeDefinitions.ElementAt(i);
                if (!_optionNameCache.TryGetValue(nodeDefinition, out string optionName))
                {
                    optionName = nodeDefinition.fullName;
                    string[] splitOptionName = optionName.Split(new[] {"__"}, StringSplitOptions.None);
                    if (splitOptionName.Length >= 3)
                    {
                        optionName = $"({splitOptionName[2].Replace("_", ", ")})";
                    }
                    optionName = optionName.FriendlyNameify();
                    _optionNameCache.Add(nodeDefinition, optionName);
                }

                if (!_cleanerOptionNameCache.TryGetValue(nodeDefinition, out string cleanerOptionName))
                {
                    cleanerOptionName =
                        optionName.Replace("UnityEngine", "").Replace("System", "").Replace("Variable_", "");
                    _cleanerOptionNameCache.Add(nodeDefinition, cleanerOptionName);
                }

                options[i] = new GUIContent(cleanerOptionName,
                    optionName);
            }

            float popupSize = GUI.skin.label.CalcSize(options[currentIndex]).x + 5;
            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUILayout.Popup(new GUIContent(""), currentIndex, options,
                GUILayout.Width(popupSize));
            if (EditorGUI.EndChangeCheck())
            {
                if (newIndex != currentIndex)
                {
                    nodeData.fullName = udonNodeDefinitions.ElementAt(newIndex).fullName;
                    ((UdonGraph) graph).ReSerializeData();
                    //((UdonGraph) graph).Reload();
                    ((UdonGraph) graph).data = new UdonGraphData(((UdonGraph) graph).graphProgramAsset.GetGraphData());
                    ((UdonGraph) graph).Reload();
                    EditorWindow view = EditorWindow.GetWindow<UdonGraphWindow>();
                    view.Repaint();
                    ((UdonGraph) graph).data = new UdonGraphData(((UdonGraph) graph).graphProgramAsset.GetGraphData());
                    ((UdonGraph) graph).Reload();
                }
            }
        }

        private float OutputsWidth(UdonNode node)
        {
            UdonNodeData nodeData = ((UdonGraph) graph).data.FindNode(node.uid);

            UdonNodeDefinition udonNodeDefinition;
            if (_nodeDefinitionCache.ContainsKey(nodeData.fullName))
            {
                udonNodeDefinition = _nodeDefinitionCache[nodeData.fullName];
            }
            else
            {
                udonNodeDefinition = UdonEditorManager.Instance.GetNodeDefinition(nodeData.fullName);
                _nodeDefinitionCache.Add(nodeData.fullName, udonNodeDefinition);
            }

            string longestString = "";
            foreach (Slot slot in node.outputSlots)
            {
                string label = ""; //slot.name;

                int index = slot.node.outputDataSlots.ToList().IndexOf(slot);
                if (udonNodeDefinition.Outputs.Count > index &&
                    index >= 0)
                {
                    label = $"{udonNodeDefinition.Outputs[index].name}"; //{label}
                }

                if (slot.isFlowSlot)
                {
                    int flowIndex = slot.node.outputFlowSlots.ToList().IndexOf(slot);
                    if (udonNodeDefinition.outputFlowNames != null &&
                        udonNodeDefinition.outputFlowNames.Count > flowIndex && flowIndex >= 0)
                    {
                        label = $"{udonNodeDefinition.outputFlowNames[flowIndex]}"; //{label}
                    }
                }

                if (UdonNodeSearchMenu.GetCachedTypeThumbnail(slot.dataType) == null)
                {
                    label = $"{slot.name}{label}";
                }

                if (label.Length > longestString.Length)
                {
                    longestString = label;
                }
            }

            if (longestString == "")
            {
                longestString = " ";
            }

            return GUI.skin.label.CalcSize(new GUIContent(longestString)).x;
        }

        private void DrawOutputSlot(Slot slot)
        {
            //Color backgroundColor = GUI.backgroundColor;
            //if(slot.edges.Count == 0)
            //{
            //    GUI.backgroundColor = MapTypeToColor(slot.dataType);
            //}

            UdonNodeData nodeData = ((UdonGraph) graph).data.FindNode(((UdonNode) slot.node).uid);
            int index = slot.node.outputDataSlots.ToList().IndexOf(slot);
            UdonNodeDefinition udonNodeDefinition;
            if (_nodeDefinitionCache.ContainsKey(nodeData.fullName))
            {
                udonNodeDefinition = _nodeDefinitionCache[nodeData.fullName];
            }
            else
            {
                udonNodeDefinition = UdonEditorManager.Instance.GetNodeDefinition(nodeData.fullName);
                _nodeDefinitionCache.Add(nodeData.fullName, udonNodeDefinition);
            }

            string label = ""; // slot.name;
            if (udonNodeDefinition.Outputs.Count > index && index >= 0)
            {
                if (udonNodeDefinition.Outputs[index].name == "T" || udonNodeDefinition.Outputs[index].name == "T[]")
                {
                    label = udonNodeDefinition.Outputs[index].name;
                }
                else
                {
                    label = $"{udonNodeDefinition.Outputs[index].name}"; //{label} 
                }
            }

            if (slot.isFlowSlot)
            {
                int flowIndex = slot.node.outputFlowSlots.ToList().IndexOf(slot);
                if (udonNodeDefinition.outputFlowNames != null &&
                    udonNodeDefinition.outputFlowNames.Count > flowIndex && flowIndex >= 0)
                {
                    label = $"{udonNodeDefinition.outputFlowNames[flowIndex]}"; //{label} 
                }
            }

            GUIContent iconContent =
                new GUIContent(slot.name, slot.name);
            float iconWidth = EditorStyles.boldLabel.CalcSize(iconContent).x;
            
            float labelWidth = EditorStyles.boldLabel.CalcSize(new GUIContent(label)).x;
            
            if (UdonNodeSearchMenu.GetCachedTypeThumbnail(slot.dataType) != null)
            {
                iconContent =
                    new GUIContent("", UdonNodeSearchMenu.GetCachedTypeThumbnail(slot.dataType), slot.name);
                iconWidth = 20;
            }

            //GUILayout.Space(GUI.skin.label.CalcSize(new GUIContent(label)).x + + iconWidth + 15);
            //Rect iconRect = EditorGUILayout.GetControlRect(GUILayout.Width(iconWidth), GUILayout.ExpandWidth(false)); //, GUILayout.MinWidth(20), GUILayout.MaxWidth(20)
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(iconWidth + 5);

            //Rect iconRect = GUILayoutUtility.GetRect(iconContent, EditorStyles.boldLabel);

            LayoutSlot(slot, label, true, false, true, slot.name == "" || slot.dataTypeString == "" ? Styles.triggerPinOut : Styles.varPinOut);
            Rect iconRect = GUILayoutUtility.GetLastRect();

            if (UdonNodeSearchMenu.GetCachedTypeThumbnail(slot.dataType) != null)
            {
                iconRect.x -= 15;
            }
            else
            {
                iconRect.x -= iconWidth + labelWidth; // -= 35;    
            }

            iconRect.width = iconWidth;
            //iconRect.y -= 2;
            //Rect iconRect = EditorGUILayout.GetControlRect(false, 20, EditorStyles.label, GUILayout.Width(iconWidth));
            GUI.Label(iconRect, iconContent, EditorStyles.boldLabel);

            EditorGUILayout.EndHorizontal();
            //GUI.backgroundColor = backgroundColor;
        }

        private readonly Dictionary<string, UdonNodeDefinition> _nodeDefinitionCache =
            new Dictionary<string, UdonNodeDefinition>();

        private bool _doReSerialize;

        private readonly Dictionary<Slot, int> _slotIndexCache = new Dictionary<Slot, int>();
        private readonly Dictionary<Slot, Type> _slotDataTypeCache = new Dictionary<Slot, Type>();
        private void DrawSlotProperty(Slot slot, Rect rect, bool showField = true)
        {
            if (!_slotDataTypeCache.TryGetValue(slot, out Type slotDataType))
            {
                slotDataType = slot.dataType;
                _slotDataTypeCache.Add(slot, slotDataType);
            }
            
            rect.x += 3;
            rect.y -= 3;
            
            UdonGraph udonGraph = (UdonGraph) graph;
            UdonNodeData nodeData = udonGraph.data.FindNode(((UdonNode) slot.node).uid);
            if (nodeData == null)
            {
                EditorGUI.LabelField(rect, "MISSING NODE DATA");
                return;
            }

            if (!_slotIndexCache.TryGetValue(slot, out int index))
            {
                index = slot.node.inputDataSlots.ToList().IndexOf(slot);
                _slotIndexCache.Add(slot, index);
            }
            
            nodeData.Resize(index + 1);
            if (showField && index >= 0)
            {
                if (nodeData.nodeValues[index] == null)
                {
                    nodeData.nodeValues[index] = new SerializableObjectContainer();
                }
            }

            string label = slot.title;
            float textWidth = 4 + GUI.skin.label.CalcSize(new GUIContent(label)).x;

            EditorGUIUtility.labelWidth = textWidth;

            if (!showField)
            {
                if (slotDataType != null)
                {
                    string tooltip = slot.name;
                    if (slotDataType == typeof(GameObject) || slotDataType == typeof(Transform) ||
                        slotDataType == typeof(UdonBehaviour))
                    {
                        tooltip = $"{slot.name} (Defaults to this object if null)";
                    }
                    if (UdonNodeSearchMenu.GetCachedTypeThumbnail(slotDataType) != null)
                    {
                        EditorGUI.LabelField(rect,
                            new GUIContent(label, UdonNodeSearchMenu.GetCachedTypeThumbnail(slotDataType), tooltip),
                            EditorStyles.boldLabel);
                    }
                    else
                    {
                        EditorGUI.LabelField(rect, new GUIContent(label, tooltip), new GUIContent(UdonGraph.FriendlyTypeName(slotDataType).FriendlyNameify()),
                            EditorStyles.boldLabel);
                    }
                }
                else
                {
                    GUI.Label(rect, $"{label} {slotDataType}");
                }

                return;
            }

            if (slotDataType != null &&
                (slotDataType.IsSubclassOf(typeof(Object)) ||
                 slotDataType == typeof(Object)))
            {
                Type fieldType = slotDataType == typeof(ScriptableObject) ? typeof(UdonSubGraphAsset) : slotDataType ; 
                EditorGUI.BeginChangeCheck();
                Object value = (Object) nodeData.nodeValues[index].Deserialize();
                GUI.SetNextControlName("NodeField");
                nodeData.nodeValues[index] =
                    SerializableObjectContainer.Serialize(
                        EditorGUI.ObjectField(rect, label, value, fieldType, true));
                if (EditorGUI.EndChangeCheck())
                {
                    _doReSerialize = true;    
                }
            }
            else
            {
                object value = nodeData.nodeValues[index].Deserialize();
                EditorGUI.BeginChangeCheck();
                object slotValue;
                if ((slot.node.name == "Set_Variable" && slot.node.inputDataSlots.ElementAt(0) == slot) ||
                    (slot.node.name == "Get_Variable" && slot.node.inputDataSlots.ElementAt(0) == slot))
                {
                    
                    int popupIndex = _variableNodes
                        .IndexOf(_variableNodes.FirstOrDefault(v => v.uid == (string) value));
                    if (popupIndex < 0)
                    {
                        popupIndex = 0;
                    }

                    List<string> popupOptions = _variablePopupOptions;
                    popupOptions.Add("Create New Variable");
                    EditorGUI.BeginChangeCheck();
                    if (_variableNodes.Count == 0)
                    {
                        popupIndex = 0;
                        GUI.Button(rect, "Create New Variable");
                    }
                    else
                    {
                        popupIndex = EditorGUI.Popup(rect, "Variable", popupIndex,
                            popupOptions.ToArray());
                    }
                    if (popupIndex < 0)
                    {
                        popupIndex = 0;
                    }

                    if (popupIndex >= _variableNodes.Count)
                    {
                        if (EditorGUI.EndChangeCheck() || _variableNodes.Count > 0)
                        {
                            string newVariableName = "newVariable";
                            int defaultCount = _variableNodes.Count(n =>
                                (string) n.nodeValues[1].Deserialize() == newVariableName);
                            if (defaultCount > 0)
                            {
                                newVariableName = $"{newVariableName}{defaultCount + 1}";
                            }
                            
                            string newVarUID = Guid.NewGuid().ToString();
                            
                            Rect graphExtents = (Rect)GetInstanceField(typeof(UdonGraph), udonGraph, "graphExtents");
                            graphExtents = new Rect(new Vector2(scrollPosition.x + (graphExtents.x + (Host.position.width / 2)) - 125, scrollPosition.y + (graphExtents.y + (Host.position.height / 2)) - 24), new Vector2(10, 10));
                            
                            UdonNodeData newNodeData = new UdonNodeData(udonGraph.data, "Variable_SystemString")
                            {
                                uid = newVarUID,
                                nodeUIDs = new string[5],
                                nodeValues = new[]
                                {
                                    SerializableObjectContainer.Serialize("", typeof(string)),
                                    SerializableObjectContainer.Serialize(newVariableName, typeof(string)),
                                    SerializableObjectContainer.Serialize(false, typeof(bool)),
                                    SerializableObjectContainer.Serialize(false, typeof(bool)),
                                    SerializableObjectContainer.Serialize("none", typeof(string))
                                },
                                position = graphExtents.position
                            };
                            
                            udonGraph.data.nodes.Add(newNodeData);
                            udonGraph.ReSerializeData();
                            udonGraph.Reload();
                            
                            slotValue = newVarUID;
                        }
                        else
                        {
                            slotValue = "";
                        }
                    }
                    else if (_variableNodes.Count == 0)
                    {
                        slotValue = "";
                    }
                    else
                    {
                        slotValue = _variableNodes[popupIndex].uid;
                        if ((string) nodeData.nodeValues[index].Deserialize() != (string)slotValue)
                        {
                            nodeData.nodeValues[index] = SerializableObjectContainer.Serialize(slotValue, slotDataType);
                            _doReSerialize = true;
                        }
                    }
                }
                else
                {
                    slotValue = DrawSlotLabel(value, slotDataType, rect, label, nodeData.uid);
                }

                if (EditorGUI.EndChangeCheck())
                {
                    if (slot.node.name.StartsWithCached("Variable_") && slot.node.inputSlots.ElementAt(1) == slot) //variable name field 
                    {
                        slotValue = ((string) slotValue).Replace(" ", "");
                    }
                    nodeData.nodeValues[index] = SerializableObjectContainer.Serialize(slotValue, slotDataType);
                    _doReSerialize = true;
                    string typeName = slotDataType == null ? "null" : slotDataType.Name;
                    _lastActiveControl = $"{label}{typeName}NodeField";
                }
            }
        }
        
        //TODO: oof ouch my reflection
        private static object GetInstanceField(Type type, object instance, string fieldName)
        {
            BindingFlags bindFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                                     | BindingFlags.Static;
            FieldInfo field = type.GetField(fieldName, bindFlags);
            if (field != null) return field.GetValue(instance);
            throw new NullReferenceException($"Failed to get private field named: {fieldName} on object: {instance} of type: {type}");
        }
        
        
        private static readonly Dictionary<string, bool> ArrayStates = new Dictionary<string, bool>();

        //TODO: merge with similar code in UdonBehaviourEditor
        //TODO: make array drawing code a more generic function, lots of repeated code atm
        private static object DrawSlotLabel(object value, Type type, Rect rect, string title, string uid)
        {
            if (type != null)
            {
                if (type.IsValueType)
                {
                    if (value == null)
                    {
                        value = Activator.CreateInstance(type);
                    }
                }

                if (value != null && !type.IsInstanceOfType(value))
                {
                    value = type.IsValueType ? Activator.CreateInstance(type) : null;
                }
            }

            // ReSharper disable RedundantToStringCall
            if (type == null || type == typeof(void))
            {
                EditorGUI.LabelField(rect, title);
            }
            else if (type == typeof(string))
            {
                GUI.SetNextControlName($"{title}{type.Name}NodeField");
                if (title == "syncMode")
                {
                    int popupIndex = 0;
                    switch ((string)value)
                    {
                        case "none":
                        {
                            popupIndex = 0;
                            break;
                        }
                        case "linear":
                        {
                            popupIndex = 1;
                            break;
                        }
                        case "smooth":
                        {
                            popupIndex = 2;
                            break;
                        }
                    }
                    popupIndex = EditorGUI.Popup(rect, title, popupIndex, new []{"none", "linear", "smooth"});
                    switch (popupIndex)
                    {
                        case 0:
                        {
                            return "none";
                        }
                        case 1:
                        {
                            return "linear";
                        }
                        case 2:
                        {
                            return "smooth";
                        }
                    }
                }
                else if(title == "comment")
                {
                    float baseWidth = rect.width + rect.x;
                    Vector2 textDimensions = GUI.skin.label.CalcSize(new GUIContent((string)value));
                    //EditorGUIUtility.labelWidth = 0;
                    //return EditorGUILayout.TextArea((string) value);
                    if (rect.width < 200)
                    {
                        rect.width += 200;
                    }
                    rect.width = textDimensions.x + 10;
                    rect.height = textDimensions.y + 10;
                    EditorGUILayout.GetControlRect(false, rect.height, GUILayout.Width(rect.width - baseWidth - 150));
                    return EditorGUI.TextArea(rect, (string) value);
                }
                return EditorGUI.TextField(rect, title, (string) value);
            }
            else if (type == typeof(string[]))
            {
                EditorGUI.LabelField(rect, title);
                EditorGUILayout.BeginVertical();
                string[] valueArray = (string[]) value;
                GUI.SetNextControlName("NodeField");
                
                bool showArray = false;
                if (ArrayStates.ContainsKey(uid))
                {
                    showArray = ArrayStates[uid];
                }
                else
                {
                    ArrayStates.Add(uid, false);
                }

                EditorGUILayout.BeginHorizontal();
                showArray = EditorGUILayout.Foldout(showArray, GUIContent.none);
                ArrayStates[uid] = showArray;
                
                int newSize = EditorGUILayout.IntField("size:",
                    valueArray != null && valueArray.Length > 0 ? valueArray.Length : 1);
                newSize = newSize >= 0 ? newSize : 0;
                Array.Resize(ref valueArray, newSize);
                EditorGUILayout.EndHorizontal();

                if (showArray)
                {
                    if (valueArray != null && valueArray.Length > 0)
                    {
                        for (int i = 0; i < valueArray.Length; i++)
                        {
                            GUI.SetNextControlName("NodeField");
                            valueArray[i] =
                                EditorGUILayout.TextField($"{i}:", valueArray.Length > i ? valueArray[i] : "");
                        }
                    }
                }

                EditorGUILayout.EndVertical();
                return valueArray;
            }
            else if (type == typeof(int))
            {
                GUI.SetNextControlName("NodeField");
                return EditorGUI.IntField(rect, title, (int) value);
            }
            else if (type == typeof(int[]))
            {
                EditorGUI.LabelField(rect, title);
                EditorGUILayout.BeginVertical();
                int[] valueArray = (int[]) value;
                GUI.SetNextControlName("NodeField");
                
                bool showArray = false;
                if (ArrayStates.ContainsKey(uid))
                {
                    showArray = ArrayStates[uid];
                }
                else
                {
                    ArrayStates.Add(uid, false);
                }

                EditorGUILayout.BeginHorizontal();
                showArray = EditorGUILayout.Foldout(showArray, GUIContent.none);
                ArrayStates[uid] = showArray;
                
                int newSize = EditorGUILayout.IntField("size:",
                    valueArray != null && valueArray.Length > 0 ? valueArray.Length : 1);
                newSize = newSize >= 0 ? newSize : 0;
                Array.Resize(ref valueArray, newSize);
                EditorGUILayout.EndHorizontal();

                if (showArray)
                {
                    if (valueArray != null && valueArray.Length > 0)
                    {
                        for (int i = 0; i < valueArray.Length; i++)
                        {
                            GUI.SetNextControlName("NodeField");
                            valueArray[i] =
                                EditorGUILayout.IntField($"{i}:", valueArray.Length > i ? valueArray[i] : 0);
                        }
                    }
                }

                EditorGUILayout.EndVertical();
                return valueArray;
            }
            else if (type == typeof(float))
            {
                GUI.SetNextControlName("NodeField");
                return EditorGUI.FloatField(rect, title, (float) value);
            }
            else if (type == typeof(float[]))
            {
                EditorGUI.LabelField(rect, title);
                EditorGUILayout.BeginVertical();
                float[] valueArray = (float[]) value;
                GUI.SetNextControlName("NodeField");
                
                bool showArray = false;
                if (ArrayStates.ContainsKey(uid))
                {
                    showArray = ArrayStates[uid];
                }
                else
                {
                    ArrayStates.Add(uid, false);
                }

                EditorGUILayout.BeginHorizontal();
                showArray = EditorGUILayout.Foldout(showArray, GUIContent.none);
                ArrayStates[uid] = showArray;
                
                int newSize = EditorGUILayout.IntField("size:",
                    valueArray != null && valueArray.Length > 0 ? valueArray.Length : 1);
                newSize = newSize >= 0 ? newSize : 0;
                Array.Resize(ref valueArray, newSize);
                EditorGUILayout.EndHorizontal();

                if (showArray)
                {
                    if (valueArray != null && valueArray.Length > 0)
                    {
                        for (int i = 0; i < valueArray.Length; i++)
                        {
                            GUI.SetNextControlName("NodeField");
                            valueArray[i] =
                                EditorGUILayout.FloatField($"{i}:", valueArray.Length > i ? valueArray[i] : 0);
                        }
                    }
                }

                EditorGUILayout.EndVertical();
                return valueArray;
            }
            else if (type == typeof(bool))
            {
                GUI.SetNextControlName("NodeField");
                return EditorGUI.Toggle(rect, title, (bool) value);
            }
            else if (type == typeof(bool[]))
            {
                EditorGUI.LabelField(rect, title);
                EditorGUILayout.BeginVertical();
                bool[] valueArray = (bool[]) value;
                GUI.SetNextControlName("NodeField");
                
                bool showArray = false;
                if (ArrayStates.ContainsKey(uid))
                {
                    showArray = ArrayStates[uid];
                }
                else
                {
                    ArrayStates.Add(uid, false);
                }

                EditorGUILayout.BeginHorizontal();
                showArray = EditorGUILayout.Foldout(showArray, GUIContent.none);
                ArrayStates[uid] = showArray;
                
                int newSize = EditorGUILayout.IntField("size:",
                    valueArray != null && valueArray.Length > 0 ? valueArray.Length : 1);
                newSize = newSize >= 0 ? newSize : 0;
                Array.Resize(ref valueArray, newSize);
                EditorGUILayout.EndHorizontal();

                if (showArray)
                {
                    if (valueArray != null && valueArray.Length > 0)
                    {
                        for (int i = 0; i < valueArray.Length; i++)
                        {
                            GUI.SetNextControlName("NodeField");
                            valueArray[i] = EditorGUILayout.Toggle($"{i}:", valueArray.Length > i && valueArray[i]);
                        }
                    }
                }

                EditorGUILayout.EndVertical();
                return valueArray;
            }
            else if (type == typeof(Vector2))
            {
                bool wideMode = EditorGUIUtility.wideMode;
                EditorGUIUtility.wideMode = true;
                GUI.SetNextControlName("NodeField");
                Vector2 result = EditorGUI.Vector2Field(rect, title, (Vector2) value);
                EditorGUIUtility.wideMode = wideMode;
                return result;
            }
            else if (type == typeof(Vector2[]))
            {
                EditorGUI.LabelField(rect, title);
                EditorGUILayout.BeginVertical();
                Vector2[] valueArray = (Vector2[]) value;
                GUI.SetNextControlName("NodeField");
                bool showArray = false;
                if (ArrayStates.ContainsKey(uid))
                {
                    showArray = ArrayStates[uid];
                }
                else
                {
                    ArrayStates.Add(uid, false);
                }

                EditorGUILayout.BeginHorizontal();
                showArray = EditorGUILayout.Foldout(showArray, GUIContent.none);
                ArrayStates[uid] = showArray;
                
                int newSize = EditorGUILayout.IntField("size:",
                    valueArray != null && valueArray.Length > 0 ? valueArray.Length : 1);
                newSize = newSize >= 0 ? newSize : 0;
                Array.Resize(ref valueArray, newSize);
                EditorGUILayout.EndHorizontal();

                if (showArray)
                {
                    if (valueArray != null && valueArray.Length > 0)
                    {
                        for (int i = 0; i < valueArray.Length; i++)
                        {
                            GUI.SetNextControlName("NodeField");
                            valueArray[i] = EditorGUILayout.Vector2Field($"{i}:",
                                valueArray.Length > i ? valueArray[i] : Vector2.zero);
                        }
                    }
                }

                EditorGUILayout.EndVertical();
                return valueArray;
            }
            else if (type == typeof(Vector3))
            {
                bool wideMode = EditorGUIUtility.wideMode;
                EditorGUIUtility.wideMode = true;
                GUI.SetNextControlName("NodeField");
                Vector3 result = EditorGUI.Vector3Field(rect, title, (Vector3) value);
                EditorGUIUtility.wideMode = wideMode;
                return result;
            }
            else if (type == typeof(Vector3[]))
            {
                EditorGUI.LabelField(rect, title);
                EditorGUILayout.BeginVertical();
                Vector3[] valueArray = (Vector3[]) value;
                GUI.SetNextControlName("NodeField");
                bool showArray = false;
                if (ArrayStates.ContainsKey(uid))
                {
                    showArray = ArrayStates[uid];
                }
                else
                {
                    ArrayStates.Add(uid, false);
                }

                EditorGUILayout.BeginHorizontal();
                showArray = EditorGUILayout.Foldout(showArray, GUIContent.none);
                ArrayStates[uid] = showArray;
                
                int newSize = EditorGUILayout.IntField("size:",
                    valueArray != null && valueArray.Length > 0 ? valueArray.Length : 1);
                newSize = newSize >= 0 ? newSize : 0;
                Array.Resize(ref valueArray, newSize);
                EditorGUILayout.EndHorizontal();

                if (showArray)
                {
                    if (valueArray != null && valueArray.Length > 0)
                    {
                        for (int i = 0; i < valueArray.Length; i++)
                        {
                            GUI.SetNextControlName("NodeField");
                            valueArray[i] = EditorGUILayout.Vector3Field($"{i}:",
                                valueArray.Length > i ? valueArray[i] : Vector3.zero);
                        }
                    }
                }

                EditorGUILayout.EndVertical();
                return valueArray;
            }
            else if (type == typeof(Vector4))
            {
                bool wideMode = EditorGUIUtility.wideMode;
                EditorGUIUtility.wideMode = true;
                GUI.SetNextControlName("NodeField");
                Vector4 result = EditorGUI.Vector4Field(rect, title, (Vector4) value);
                EditorGUIUtility.wideMode = wideMode;
                return result;
            }
            else if (type == typeof(Vector4[]))
            {
                EditorGUI.LabelField(rect, title);
                EditorGUILayout.BeginVertical();
                Vector4[] valueArray = (Vector4[]) value;
                GUI.SetNextControlName("NodeField");
                bool showArray = false;
                if (ArrayStates.ContainsKey(uid))
                {
                    showArray = ArrayStates[uid];
                }
                else
                {
                    ArrayStates.Add(uid, false);
                }

                EditorGUILayout.BeginHorizontal();
                showArray = EditorGUILayout.Foldout(showArray, GUIContent.none);
                ArrayStates[uid] = showArray;
                
                int newSize = EditorGUILayout.IntField("size:",
                    valueArray != null && valueArray.Length > 0 ? valueArray.Length : 1);
                newSize = newSize >= 0 ? newSize : 0;
                Array.Resize(ref valueArray, newSize);
                EditorGUILayout.EndHorizontal();

                if (showArray)
                {
                    if (valueArray != null && valueArray.Length > 0)
                    {
                        for (int i = 0; i < valueArray.Length; i++)
                        {
                            GUI.SetNextControlName("NodeField");
                            valueArray[i] = EditorGUILayout.Vector4Field($"{i}:",
                                valueArray.Length > i ? valueArray[i] : Vector4.zero);
                        }
                    }
                }

                EditorGUILayout.EndVertical();
                return valueArray;
            }
            else if (type == typeof(Quaternion))
            {
                bool wideMode = EditorGUIUtility.wideMode;
                EditorGUIUtility.wideMode = true;
                GUI.SetNextControlName("NodeField");
                Quaternion quaternionValue = (Quaternion) value;
                rect.width += 25;
                Vector4 vec4 = EditorGUI.Vector4Field(rect, title,
                    new Vector4(quaternionValue.x, quaternionValue.y, quaternionValue.z, quaternionValue.w));
                EditorGUIUtility.wideMode = wideMode;
                return new Quaternion(vec4.x, vec4.y, vec4.z, vec4.w);
            }
            else if (type == typeof(Quaternion[]))
            {
                EditorGUI.LabelField(rect, title);
                EditorGUILayout.BeginVertical();
                Quaternion[] valueArray = (Quaternion[]) value;
                GUI.SetNextControlName("NodeField");
                bool showArray = false;
                if (ArrayStates.ContainsKey(uid))
                {
                    showArray = ArrayStates[uid];
                }
                else
                {
                    ArrayStates.Add(uid, false);
                }

                EditorGUILayout.BeginHorizontal();
                showArray = EditorGUILayout.Foldout(showArray, GUIContent.none);
                ArrayStates[uid] = showArray;
                
                int newSize = EditorGUILayout.IntField("size:",
                    valueArray != null && valueArray.Length > 0 ? valueArray.Length : 1);
                newSize = newSize >= 0 ? newSize : 0;
                Array.Resize(ref valueArray, newSize);
                EditorGUILayout.EndHorizontal();

                if (showArray)
                {
                    if (valueArray != null && valueArray.Length > 0)
                    {
                        for (int i = 0; i < valueArray.Length; i++)
                        {
                            GUI.SetNextControlName("NodeField");
                            Vector4 vec4 = EditorGUILayout.Vector4Field($"{i}:",
                                valueArray.Length > i
                                    ? new Vector4(valueArray[i].x, valueArray[i].y, valueArray[i].z, valueArray[i].w)
                                    : Vector4.zero);
                            valueArray[i] = new Quaternion(vec4.x, vec4.y, vec4.z, vec4.w);
                        }
                    }
                }

                EditorGUILayout.EndVertical();
                return valueArray;
            }
            else if (type == typeof(Color))
            {
                bool wideMode = EditorGUIUtility.wideMode;
                EditorGUIUtility.wideMode = true;
                GUI.SetNextControlName("NodeField");
                Color result = EditorGUI.ColorField(rect, title, (Color) value);
                EditorGUIUtility.wideMode = wideMode;
                return result;
            }
            else if (type == typeof(Color[]))
            {
                EditorGUI.LabelField(rect, title);
                EditorGUILayout.BeginVertical();
                Color[] valueArray = (Color[]) value;
                GUI.SetNextControlName("NodeField");
                bool showArray = false;
                if (ArrayStates.ContainsKey(uid))
                {
                    showArray = ArrayStates[uid];
                }
                else
                {
                    ArrayStates.Add(uid, false);
                }

                EditorGUILayout.BeginHorizontal();
                showArray = EditorGUILayout.Foldout(showArray, GUIContent.none);
                ArrayStates[uid] = showArray;
                
                int newSize = EditorGUILayout.IntField("size:",
                    valueArray != null && valueArray.Length > 0 ? valueArray.Length : 1);
                newSize = newSize >= 0 ? newSize : 0;
                Array.Resize(ref valueArray, newSize);
                EditorGUILayout.EndHorizontal();

                if (showArray)
                {
                    if (valueArray != null && valueArray.Length > 0)
                    {
                        for (int i = 0; i < valueArray.Length; i++)
                        {
                            GUI.SetNextControlName("NodeField");
                            valueArray[i] = EditorGUILayout.ColorField($"{i}:",
                                valueArray.Length > i ? valueArray[i] : Color.white);
                        }
                    }
                }

                EditorGUILayout.EndVertical();
                return valueArray;
            }
            else if (type == typeof(Color32))
            {
                bool wideMode = EditorGUIUtility.wideMode;
                EditorGUIUtility.wideMode = true;
                GUI.SetNextControlName("NodeField");
                Color32 result = EditorGUI.ColorField(rect, title, (Color32) value);
                EditorGUIUtility.wideMode = wideMode;
                return result;
            }
            else if (type == typeof(Color32[]))
            {
                EditorGUI.LabelField(rect, title);
                EditorGUILayout.BeginVertical();
                Color32[] valueArray = (Color32[]) value;
                GUI.SetNextControlName("NodeField");
                bool showArray = false;
                if (ArrayStates.ContainsKey(uid))
                {
                    showArray = ArrayStates[uid];
                }
                else
                {
                    ArrayStates.Add(uid, false);
                }

                EditorGUILayout.BeginHorizontal();
                showArray = EditorGUILayout.Foldout(showArray, GUIContent.none);
                ArrayStates[uid] = showArray;
                
                int newSize = EditorGUILayout.IntField("size:",
                    valueArray != null && valueArray.Length > 0 ? valueArray.Length : 1);
                newSize = newSize >= 0 ? newSize : 0;
                Array.Resize(ref valueArray, newSize);
                EditorGUILayout.EndHorizontal();

                if (showArray)
                {
                    if (valueArray != null && valueArray.Length > 0)
                    {
                        for (int i = 0; i < valueArray.Length; i++)
                        {
                            GUI.SetNextControlName("NodeField");
                            valueArray[i] = (Color32)EditorGUILayout.ColorField($"{i}:",
                                valueArray.Length > i ? valueArray[i] : (Color32)Color.white);
                        }
                    }
                }

                EditorGUILayout.EndVertical();
                return valueArray;
            }
            else if (type == typeof(ParticleSystem.MinMaxCurve))
            {
                GUI.SetNextControlName("NodeField");
                ParticleSystem.MinMaxCurve minMaxCurve = (ParticleSystem.MinMaxCurve) value;
                float multiplier = minMaxCurve.curveMultiplier;
                AnimationCurve minCurve = minMaxCurve.curveMin;
                AnimationCurve maxCurve = minMaxCurve.curveMax;
                EditorGUILayout.BeginVertical();
                multiplier = EditorGUI.FloatField(rect, "Multiplier", multiplier);
                minCurve = EditorGUILayout.CurveField("Min Curve", minCurve);
                maxCurve = EditorGUILayout.CurveField("Max Curve", maxCurve);
                EditorGUILayout.EndVertical();
                ParticleSystem.MinMaxCurve result = new ParticleSystem.MinMaxCurve(multiplier, minCurve, maxCurve);
                return result;
            }
            else if (type == typeof(ParticleSystem.MinMaxCurve[]))
            {
                EditorGUI.LabelField(rect, title);
                EditorGUILayout.BeginVertical();
                ParticleSystem.MinMaxCurve[] valueArray = (ParticleSystem.MinMaxCurve[]) value;
                GUI.SetNextControlName("NodeField");
                bool showArray = false;
                if (ArrayStates.ContainsKey(uid))
                {
                    showArray = ArrayStates[uid];
                }
                else
                {
                    ArrayStates.Add(uid, false);
                }

                EditorGUILayout.BeginHorizontal();
                showArray = EditorGUILayout.Foldout(showArray, GUIContent.none);
                ArrayStates[uid] = showArray;
                
                int newSize = EditorGUILayout.IntField("size:",
                    valueArray != null && valueArray.Length > 0 ? valueArray.Length : 1);
                newSize = newSize >= 0 ? newSize : 0;
                Array.Resize(ref valueArray, newSize);
                EditorGUILayout.EndHorizontal();

                if (showArray)
                {
                    if (valueArray != null && valueArray.Length > 0)
                    {
                        for (int i = 0; i < valueArray.Length; i++)
                        {
                            GUI.SetNextControlName("NodeField");
                            ParticleSystem.MinMaxCurve minMaxCurve = valueArray[i];
                            float multiplier = minMaxCurve.curveMultiplier;
                            AnimationCurve minCurve = minMaxCurve.curveMin;
                            AnimationCurve maxCurve = minMaxCurve.curveMax;
                            EditorGUILayout.BeginVertical();
                            multiplier = EditorGUI.FloatField(rect, "Multiplier", multiplier);
                            minCurve = EditorGUILayout.CurveField("Min Curve", minCurve);
                            maxCurve = EditorGUILayout.CurveField("Max Curve", maxCurve);
                            EditorGUILayout.EndVertical();
                            ParticleSystem.MinMaxCurve result = new ParticleSystem.MinMaxCurve(multiplier, minCurve, maxCurve);
                            valueArray[i] = result;
                        }
                    }
                }

                EditorGUILayout.EndVertical();
                return valueArray;
            }
            else if (type.IsEnum)
            {
                GUI.SetNextControlName("NodeField");
                return EditorGUI.EnumPopup(rect, title, value as Enum);
            }
            // ReSharper disable once PossibleNullReferenceException
            else if (type.IsArray && type.GetElementType().IsEnum)
            {
                EditorGUI.LabelField(rect, title);
                EditorGUILayout.BeginVertical();
                Enum[] valueArray = (Enum[]) value;
                GUI.SetNextControlName("NodeField");
                bool showArray = false;
                if (ArrayStates.ContainsKey(uid))
                {
                    showArray = ArrayStates[uid];
                }
                else
                {
                    ArrayStates.Add(uid, false);
                }

                EditorGUILayout.BeginHorizontal();
                showArray = EditorGUILayout.Foldout(showArray, GUIContent.none);
                ArrayStates[uid] = showArray;
                
                int newSize = EditorGUILayout.IntField("size:",
                    valueArray != null && valueArray.Length > 0 ? valueArray.Length : 1);
                newSize = newSize >= 0 ? newSize : 0;
                Array.Resize(ref valueArray, newSize);
                EditorGUILayout.EndHorizontal();

                if (showArray)
                {
                    if (valueArray != null && valueArray.Length > 0)
                    {
                        for (int i = 0; i < valueArray.Length; i++)
                        {
                            GUI.SetNextControlName("NodeField");
                            valueArray[i] = EditorGUI.EnumPopup(rect, title, valueArray[i] as Enum);
                        }
                    }
                }

                EditorGUILayout.EndVertical();
                return valueArray;
            }
            else if (type == typeof(char))
            {
                GUI.SetNextControlName("NodeField");
                string valueString = UnescapeLikeALiteral((char)value);
                string result = EditorGUI.TextField(rect, title, valueString);
                if (string.IsNullOrEmpty(result))
                {
                    return ' ';
                }
                if (result[0] == '\\' && result.Length > 1)
                {
                    return EscapeLikeALiteral(result.Substring(0, 2));
                }
                return result[0];
            }
            else if (type == typeof(char[]))
            {
                EditorGUI.LabelField(rect, title);
                EditorGUILayout.BeginVertical();
                char[] valueArray = (char[]) value;
                GUI.SetNextControlName("NodeField");
                bool showArray = false;
                if (ArrayStates.ContainsKey(uid))
                {
                    showArray = ArrayStates[uid];
                }
                else
                {
                    ArrayStates.Add(uid, false);
                }

                EditorGUILayout.BeginHorizontal();
                showArray = EditorGUILayout.Foldout(showArray, GUIContent.none);
                ArrayStates[uid] = showArray;
                
                int newSize = EditorGUILayout.IntField("size:",
                    valueArray != null && valueArray.Length > 0 ? valueArray.Length : 1);
                newSize = newSize >= 0 ? newSize : 0;
                Array.Resize(ref valueArray, newSize);
                EditorGUILayout.EndHorizontal();

                if (showArray)
                {
                    if (valueArray != null && valueArray.Length > 0)
                    {
                        for (int i = 0; i < valueArray.Length; i++)
                        {
                            GUI.SetNextControlName("NodeField");
                            string valueString = UnescapeLikeALiteral(valueArray[i]);
                            string result = EditorGUILayout.TextField($"{i}:",
                                valueArray.Length > i ? valueString : " ");
                            //valueArray[i] = !string.IsNullOrEmpty(result) ? result[0] : ' ';
                            if (string.IsNullOrEmpty(result))
                            {
                                valueArray[i] = ' ';
                            }
                            else if (result[0] == '\\' && result.Length > 1)
                            {
                                valueArray[i] = EscapeLikeALiteral(result.Substring(0, 2));
                            }
                            else
                            {
                                valueArray[i] = result[0];    
                            }
                        }
                    }
                }

                EditorGUILayout.EndVertical();
                return valueArray;
            }
            else if (type == typeof(byte))
            {
                GUI.SetNextControlName("NodeField");
                return (byte) EditorGUI.IntField(rect, title, (byte) value);
            }
            else if (type == typeof(byte[]))
            {
                EditorGUI.LabelField(rect, title);
                EditorGUILayout.BeginVertical();
                byte[] valueArray = (byte[]) value;
                GUI.SetNextControlName("NodeField");
                bool showArray = false;
                if (ArrayStates.ContainsKey(uid))
                {
                    showArray = ArrayStates[uid];
                }
                else
                {
                    ArrayStates.Add(uid, false);
                }

                EditorGUILayout.BeginHorizontal();
                showArray = EditorGUILayout.Foldout(showArray, GUIContent.none);
                ArrayStates[uid] = showArray;
                
                int newSize = EditorGUILayout.IntField("size:",
                    valueArray != null && valueArray.Length > 0 ? valueArray.Length : 1);
                newSize = newSize >= 0 ? newSize : 0;
                Array.Resize(ref valueArray, newSize);
                EditorGUILayout.EndHorizontal();

                if (showArray)
                {
                    if (valueArray != null && valueArray.Length > 0)
                    {
                        for (int i = 0; i < valueArray.Length; i++)
                        {
                            GUI.SetNextControlName("NodeField");
                            valueArray[i] = (byte) EditorGUILayout.IntField($"{i}:",
                                valueArray.Length > i ? valueArray[i] : (byte) 0);
                        }
                    }
                }

                EditorGUILayout.EndVertical();
                return valueArray;
            }
            else if (type == typeof(double))
            {
                GUI.SetNextControlName("NodeField");
                return EditorGUI.DoubleField(rect, title, (double) value);
            }
            else if (type == typeof(double[]))
            {
                EditorGUI.LabelField(rect, title);
                EditorGUILayout.BeginVertical();
                double[] valueArray = (double[]) value;
                GUI.SetNextControlName("NodeField");
                bool showArray = false;
                if (ArrayStates.ContainsKey(uid))
                {
                    showArray = ArrayStates[uid];
                }
                else
                {
                    ArrayStates.Add(uid, false);
                }

                EditorGUILayout.BeginHorizontal();
                showArray = EditorGUILayout.Foldout(showArray, GUIContent.none);
                ArrayStates[uid] = showArray;
                
                int newSize = EditorGUILayout.IntField("size:",
                    valueArray != null && valueArray.Length > 0 ? valueArray.Length : 1);
                newSize = newSize >= 0 ? newSize : 0;
                Array.Resize(ref valueArray, newSize);
                EditorGUILayout.EndHorizontal();

                if (showArray)
                {
                    if (valueArray != null && valueArray.Length > 0)
                    {
                        for (int i = 0; i < valueArray.Length; i++)
                        {
                            GUI.SetNextControlName("NodeField");
                            valueArray[i] = EditorGUILayout.DoubleField($"{i}:",
                                valueArray.Length > i ? valueArray[i] : (double) 0);
                        }
                    }
                }

                EditorGUILayout.EndVertical();
                return valueArray;
            }
            else if (type == typeof(short))
            {
                GUI.SetNextControlName("NodeField");
                return (short) EditorGUI.IntField(rect, title, (short) value);
            }
            else if (type == typeof(short[]))
            {
                EditorGUI.LabelField(rect, title);
                EditorGUILayout.BeginVertical();
                short[] valueArray = (short[]) value;
                GUI.SetNextControlName("NodeField");
                bool showArray = false;
                if (ArrayStates.ContainsKey(uid))
                {
                    showArray = ArrayStates[uid];
                }
                else
                {
                    ArrayStates.Add(uid, false);
                }

                EditorGUILayout.BeginHorizontal();
                showArray = EditorGUILayout.Foldout(showArray, GUIContent.none);
                ArrayStates[uid] = showArray;
                
                int newSize = EditorGUILayout.IntField("size:",
                    valueArray != null && valueArray.Length > 0 ? valueArray.Length : 1);
                newSize = newSize >= 0 ? newSize : 0;
                Array.Resize(ref valueArray, newSize);
                EditorGUILayout.EndHorizontal();

                if (showArray)
                {
                    if (valueArray != null && valueArray.Length > 0)
                    {
                        for (int i = 0; i < valueArray.Length; i++)
                        {
                            GUI.SetNextControlName("NodeField");
                            valueArray[i] = (short) EditorGUILayout.IntField($"{i}:",
                                valueArray.Length > i ? valueArray[i] : (short) 0);
                        }
                    }
                }

                EditorGUILayout.EndVertical();
                return valueArray;
            }
            else if (type == typeof(long))
            {
                GUI.SetNextControlName("NodeField");
                return (long) EditorGUI.DoubleField(rect, title, (long) value);
            }
            else if (type == typeof(long[]))
            {
                EditorGUI.LabelField(rect, title);
                EditorGUILayout.BeginVertical();
                long[] valueArray = (long[]) value;
                GUI.SetNextControlName("NodeField");
                bool showArray = false;
                if (ArrayStates.ContainsKey(uid))
                {
                    showArray = ArrayStates[uid];
                }
                else
                {
                    ArrayStates.Add(uid, false);
                }

                EditorGUILayout.BeginHorizontal();
                showArray = EditorGUILayout.Foldout(showArray, GUIContent.none);
                ArrayStates[uid] = showArray;
                
                int newSize = EditorGUILayout.IntField("size:",
                    valueArray != null && valueArray.Length > 0 ? valueArray.Length : 1);
                newSize = newSize >= 0 ? newSize : 0;
                Array.Resize(ref valueArray, newSize);
                EditorGUILayout.EndHorizontal();

                if (showArray)
                {
                    if (valueArray != null && valueArray.Length > 0)
                    {
                        for (int i = 0; i < valueArray.Length; i++)
                        {
                            GUI.SetNextControlName("NodeField");
                            valueArray[i] = (long) EditorGUILayout.DoubleField($"{i}:",
                                valueArray.Length > i ? valueArray[i] : (long) 0);
                        }
                    }
                }

                EditorGUILayout.EndVertical();
                return valueArray;
            }
            else if (type == typeof(sbyte))
            {
                GUI.SetNextControlName("NodeField");
                return (sbyte) EditorGUI.IntField(rect, title, (sbyte) value);
            }
            else if (type == typeof(sbyte[]))
            {
                EditorGUI.LabelField(rect, title);
                EditorGUILayout.BeginVertical();
                sbyte[] valueArray = (sbyte[]) value;
                GUI.SetNextControlName("NodeField");
                bool showArray = false;
                if (ArrayStates.ContainsKey(uid))
                {
                    showArray = ArrayStates[uid];
                }
                else
                {
                    ArrayStates.Add(uid, false);
                }

                EditorGUILayout.BeginHorizontal();
                showArray = EditorGUILayout.Foldout(showArray, GUIContent.none);
                ArrayStates[uid] = showArray;
                
                int newSize = EditorGUILayout.IntField("size:",
                    valueArray != null && valueArray.Length > 0 ? valueArray.Length : 1);
                newSize = newSize >= 0 ? newSize : 0;
                Array.Resize(ref valueArray, newSize);
                EditorGUILayout.EndHorizontal();

                if (showArray)
                {
                    if (valueArray != null && valueArray.Length > 0)
                    {
                        for (int i = 0; i < valueArray.Length; i++)
                        {
                            GUI.SetNextControlName("NodeField");
                            valueArray[i] = (sbyte) EditorGUILayout.IntField($"{i}:",
                                valueArray.Length > i ? valueArray[i] : (sbyte) 0);
                        }
                    }
                }

                EditorGUILayout.EndVertical();
                return valueArray;
            }
            else if (type == typeof(ushort))
            {
                GUI.SetNextControlName("NodeField");
                return (ushort) EditorGUI.IntField(rect, title, (ushort) value);
            }
            else if (type == typeof(ushort[]))
            {
                EditorGUI.LabelField(rect, title);
                EditorGUILayout.BeginVertical();
                ushort[] valueArray = (ushort[]) value;
                GUI.SetNextControlName("NodeField");
                bool showArray = false;
                if (ArrayStates.ContainsKey(uid))
                {
                    showArray = ArrayStates[uid];
                }
                else
                {
                    ArrayStates.Add(uid, false);
                }

                EditorGUILayout.BeginHorizontal();
                showArray = EditorGUILayout.Foldout(showArray, GUIContent.none);
                ArrayStates[uid] = showArray;
                
                int newSize = EditorGUILayout.IntField("size:",
                    valueArray != null && valueArray.Length > 0 ? valueArray.Length : 1);
                newSize = newSize >= 0 ? newSize : 0;
                Array.Resize(ref valueArray, newSize);
                EditorGUILayout.EndHorizontal();

                if (showArray)
                {
                    if (valueArray != null && valueArray.Length > 0)
                    {
                        for (int i = 0; i < valueArray.Length; i++)
                        {
                            GUI.SetNextControlName("NodeField");
                            valueArray[i] = (ushort) EditorGUILayout.IntField($"{i}:",
                                valueArray.Length > i ? valueArray[i] : (ushort) 0);
                        }
                    }
                }

                EditorGUILayout.EndVertical();
                return valueArray;
            }
            else if (type == typeof(uint))
            {
                GUI.SetNextControlName("NodeField");
                return (uint) EditorGUI.DoubleField(rect, title, (uint) value);
            }
            else if (type == typeof(uint[]))
            {
                EditorGUI.LabelField(rect, title);
                EditorGUILayout.BeginVertical();
                uint[] valueArray = (uint[]) value;
                GUI.SetNextControlName("NodeField");
                bool showArray = false;
                if (ArrayStates.ContainsKey(uid))
                {
                    showArray = ArrayStates[uid];
                }
                else
                {
                    ArrayStates.Add(uid, false);
                }

                EditorGUILayout.BeginHorizontal();
                showArray = EditorGUILayout.Foldout(showArray, GUIContent.none);
                ArrayStates[uid] = showArray;
                
                int newSize = EditorGUILayout.IntField("size:",
                    valueArray != null && valueArray.Length > 0 ? valueArray.Length : 1);
                newSize = newSize >= 0 ? newSize : 0;
                Array.Resize(ref valueArray, newSize);
                EditorGUILayout.EndHorizontal();

                if (showArray)
                {
                    if (valueArray != null && valueArray.Length > 0)
                    {
                        for (int i = 0; i < valueArray.Length; i++)
                        {
                            GUI.SetNextControlName("NodeField");
                            valueArray[i] = (uint) EditorGUILayout.DoubleField($"{i}:",
                                valueArray.Length > i ? valueArray[i] : (uint) 0);
                        }
                    }
                }

                EditorGUILayout.EndVertical();
                return valueArray;
            }
            else if (type == typeof(ulong))
            {
                GUI.SetNextControlName("NodeField");
                return (ulong) EditorGUI.DoubleField(rect, title, (ulong) value);
            }
            else if (type == typeof(ulong[]))
            {
                EditorGUI.LabelField(rect, title);
                EditorGUILayout.BeginVertical();
                ulong[] valueArray = (ulong[]) value;
                GUI.SetNextControlName("NodeField");
                bool showArray = false;
                if (ArrayStates.ContainsKey(uid))
                {
                    showArray = ArrayStates[uid];
                }
                else
                {
                    ArrayStates.Add(uid, false);
                }

                EditorGUILayout.BeginHorizontal();
                showArray = EditorGUILayout.Foldout(showArray, GUIContent.none);
                ArrayStates[uid] = showArray;
                
                int newSize = EditorGUILayout.IntField("size:",
                    valueArray != null && valueArray.Length > 0 ? valueArray.Length : 1);
                newSize = newSize >= 0 ? newSize : 0;
                Array.Resize(ref valueArray, newSize);
                EditorGUILayout.EndHorizontal();

                if (showArray)
                {
                    if (valueArray != null && valueArray.Length > 0)
                    {
                        for (int i = 0; i < valueArray.Length; i++)
                        {
                            GUI.SetNextControlName("NodeField");
                            valueArray[i] = (ulong) EditorGUILayout.DoubleField($"{i}:",
                                valueArray.Length > i ? valueArray[i] : (ulong) 0);
                        }
                    }
                }

                EditorGUILayout.EndVertical();
                return valueArray;
            }
            else
            {
                EditorGUI.LabelField(rect, title);
            }
            // ReSharper restore RedundantToStringCall

            return "";
        }

        protected override void CopyNodesToPasteboard()
        {
            UdonNodeData[] selectedNodeDataArray = new UdonNodeData[selection.Count];
            for (int i = 0; i < selection.Count; i++)
            {
                selectedNodeDataArray[i] = ((UdonGraph) graph).data.FindNode(((UdonNode) selection[i]).uid);
            }

            EditorGUIUtility.systemCopyBuffer = ZipString(JsonUtility.ToJson(
                new SerializableObjectContainer.ArrayWrapper<UdonNodeData>(selectedNodeDataArray)));
        }

        protected override void PasteNodesFromPasteboard()
        {
            UdonNodeData[] copiedNodeDataArray;
            try
            {
                copiedNodeDataArray = JsonUtility
                    .FromJson<SerializableObjectContainer.ArrayWrapper<UdonNodeData>>(UnZipString(EditorGUIUtility.systemCopyBuffer))
                    .value;
            }
            catch
            {
                //oof ouch that's not valid data
                return;
            }
            
            UdonGraph udonGraph = ((UdonGraph) graph);
            udonGraph.Reloading = true;
            
            List<Node> pastedNodes = new List<Node>();
            Dictionary<string, string> uidMap = new Dictionary<string, string>();
            UdonNodeData[] newNodeDataArray = new UdonNodeData[copiedNodeDataArray.Length];

            for (int i = 0; i < copiedNodeDataArray.Length; i++)
            {
                UdonNodeData nodeData = copiedNodeDataArray[i];
                newNodeDataArray[i] = new UdonNodeData(udonGraph.data, nodeData.fullName)
                {
                    position = nodeData.position + new Vector2(20f, 20f),
                    uid = Guid.NewGuid().ToString(),
                    nodeUIDs = new string[nodeData.nodeUIDs.Length],
                    nodeValues = nodeData.nodeValues,
                    flowUIDs = new string[nodeData.flowUIDs.Length]
                };

                uidMap.Add(nodeData.uid, newNodeDataArray[i].uid);
            }

            for (int i = 0; i < copiedNodeDataArray.Length; i++)
            {
                UdonNodeData nodeData = copiedNodeDataArray[i];
                UdonNodeData newNodeData = newNodeDataArray[i];

                for (int j = 0; j < newNodeData.nodeUIDs.Length; j++)
                {
                    if (uidMap.ContainsKey(nodeData.nodeUIDs[j].Split('|')[0]))
                    {
                        newNodeData.nodeUIDs[j] = uidMap[nodeData.nodeUIDs[j].Split('|')[0]];
                    }
                }

                for (int j = 0; j < newNodeData.flowUIDs.Length; j++)
                {
                    if (uidMap.ContainsKey(nodeData.flowUIDs[j].Split('|')[0]))
                    {
                        newNodeData.flowUIDs[j] = uidMap[nodeData.flowUIDs[j].Split('|')[0]];
                    }
                }

                UdonNode udonNode = udonGraph.CreateNode(newNodeDataArray[i]);
                if (udonNode != null)
                {
                    udonGraph.data.nodes.Add(newNodeDataArray[i]);
                    pastedNodes.Add(udonNode);    
                }
            }

            foreach (UdonNode pastedNode in pastedNodes.Cast<UdonNode>())
            {
                pastedNode.PopulateEdges();
            }

            selection = pastedNodes;
            udonGraph.Reloading = false; 
            udonGraph.ReSerializeData();
            CopyNodesToPasteboard();
        }
        
        private static char EscapeLikeALiteral (string src) {
            switch (src) {
                    //case "\\'": return '\'';
                    //case "\\"": return '\"';
                    case "\\0": return '\0';
                    case "\\a": return '\a';
                    case "\\b": return '\b';
                    case "\\f": return '\f';
                    case "\\n": return '\n';
                    case "\\r": return '\r';
                    case "\\t": return '\t';
                    case "\\v": return '\v';
                    case "\\": return '\\';
                    default:
                        throw new InvalidOperationException($"src was {src}");
                }
        }
        
        private static string UnescapeLikeALiteral (char src) {
            switch (src) {
                //case "\\'": return '\'';
                //case "\\"": return '\"';
                case '\0': return "\\0";
                case '\a': return "\\a";
                case '\b': return "\\b";
                case '\f': return "\\f";
                case '\n': return "\\n";
                case '\r': return "\\r";
                case '\t': return "\\t";
                case '\v': return "\\v";
                //case '\\': return "\\";
                default:
                    return src.ToString();
            }
        }


        protected override void DuplicateNodesThroughPasteboard()
        {
            CopyNodesToPasteboard();
            PasteNodesFromPasteboard();
        }

        private const byte ZIP_VERSION = 0;
        private static string ZipString(string str)
        {
            using (MemoryStream output = new MemoryStream())
            {
                using (DeflateStream gzip = 
                    new DeflateStream(output, CompressionLevel.Optimal)) //, CompressionMode.Compress
                {
                    using (StreamWriter writer = 
                        new StreamWriter(gzip, System.Text.Encoding.UTF8))
                    {
                        writer.Write(str);           
                    }
                }
                List<byte> outputList = output.ToArray().ToList();
                outputList.Insert(0, ZIP_VERSION); //Version Number
                return Convert.ToBase64String(outputList.ToArray());
            }
        }

        private static string UnZipString(string input)
        {
            List<byte> inputList = new List<byte>(Convert.FromBase64String(input));
            if (inputList[0] != ZIP_VERSION) //Version Number
            {
                Debug.LogError("Tried loading unsupported version of compressed data");
                return "";
            }
            inputList.RemoveAt(0);
            using (MemoryStream inputStream = new MemoryStream(inputList.ToArray()))
            {
                using (DeflateStream gzip = 
                    new DeflateStream(inputStream, CompressionMode.Decompress))
                {
                    using (StreamReader reader = 
                        new StreamReader(gzip, System.Text.Encoding.UTF8))
                    {
                        return reader.ReadToEnd();
                    }
                }
            }
        }

        private void InitializeStyles()
        {
            nodeBackground =
                Resources.Load<Texture2D>(EditorGUIUtility.isProSkin
                    ? "UdonNodeBackground"
                    : "UdonNodeBackgroundLight");
            activeNodeBackground = Resources.Load<Texture2D>(EditorGUIUtility.isProSkin
                ? "UdonNodeActiveBackground"
                : "UdonNodeActiveBackgroundLight");

            nodeAccent = Resources.Load<Texture2D>("UdonNodeAccent");
            nodeInlay = Resources.Load<Texture2D>(EditorGUIUtility.isProSkin ? "UdonNodeInlay" : "UdonNodeInlayLight");
            slotTexture = Resources.Load<Texture2D>(EditorGUIUtility.isProSkin ? "UdonSlot" : "UdonSlotLight");
            flowSlotTexture =
                Resources.Load<Texture2D>(EditorGUIUtility.isProSkin ? "UdonFlowSlot" : "UdonFlowSlotLight");
            Styles.varPinIn = new GUIStyle
            {
                normal = {background = slotTexture},
                fixedWidth = 15,
                fixedHeight = 15,
                stretchHeight = true,
                stretchWidth = false,
                contentOffset = new Vector2(20, 0),
                imagePosition = ImagePosition.ImageLeft,
                fontSize = 11,
                alignment = TextAnchor.UpperLeft,
                fontStyle = FontStyle.Normal
            };

            Styles.varPinIn.normal.textColor = EditorGUIUtility.isProSkin ? Color.white : Color.black;
            Styles.varPinIn.clipping = TextClipping.Overflow;

            Styles.triggerPinIn = new GUIStyle(Styles.varPinIn)
            {
                normal = {background = flowSlotTexture}
            };

            Styles.varPinOut = new GUIStyle(Styles.varPinIn)
            {
                contentOffset = new Vector2(-20, 0),
                alignment = TextAnchor.UpperRight
            };

            Styles.triggerPinOut = new GUIStyle(Styles.varPinOut)
            {
                normal = {background = flowSlotTexture}
            };
        }
    }
}
