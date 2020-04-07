using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using VRC.Udon.Graph;
using VRC.Udon.Graph.Interfaces;

namespace VRC.Udon.Editor.ProgramSources
{
    public class UdonGraphWindow : EditorWindow
    {
        private const int TOOLBAR_HEIGHT = 17;
        private const float CONTENT_LOGO_SCALE = .75f;

        [SerializeField]
        private UdonGraph graph;

        [SerializeField]
        private UdonGraphGUI graphGUI;

        private static GUIStyle _udonLogo;

        [MenuItem("VRChat SDK/Udon Graph")]
        private static void Init()
        {
            GetWindow(typeof(UdonGraphWindow));
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Udon Graph");

            graph = CreateInstance<UdonGraph>();
            graphGUI = CreateInstance<UdonGraphGUI>();
            graphGUI.graph = graph;
            
            Texture2D logoTexture = Resources.Load<Texture2D>(EditorGUIUtility.isProSkin ? "UdonLogoAlphaWhite" : "UdonLogoAlpha");
            _udonLogo = new GUIStyle
            {
                normal =
                {
                    background = logoTexture,
                    textColor = Color.white
                },
                fixedHeight = (int)(logoTexture.height * CONTENT_LOGO_SCALE),
                fixedWidth = (int)(logoTexture.width * CONTENT_LOGO_SCALE)
            };

            // ReSharper disable once DelegateSubtraction
            Undo.undoRedoPerformed -= OnUndoRedo; //Remove old handler if present to prevent duplicates, doesn't cause errors if not present
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnUndoRedo()
        {
            Repaint();
        }

        private bool _drawGraph;
        private string _displayText = "";

        private void OnGUILogic()
        {
            _drawGraph = true;

            if(Selection.gameObjects.Count(g => g.GetComponent<UdonBehaviour>()) > 1)
            {
                _displayText = "Multi-object editing not supported";
                _drawGraph = false;
            }
            else if(Selection.objects.Count(o => o != null && o is IUdonGraphDataProvider) > 1) // == typeof(UdonGraphProgramAsset)
            {
                _displayText = "Multi-object editing not supported";
                _drawGraph = false;
            }

            IUdonGraphDataProvider udonGraphProgramAsset = (IUdonGraphDataProvider)Selection.objects.FirstOrDefault(g => g != null && g is IUdonGraphDataProvider);
            if(udonGraphProgramAsset == null)
            {
                GameObject behaviourObject = Selection.gameObjects.FirstOrDefault(g => g.GetComponent<UdonBehaviour>());
                if(behaviourObject != null)
                {
                    UdonBehaviour udonBehaviour = behaviourObject.GetComponent<UdonBehaviour>();
                    AbstractUdonProgramSource programSource = udonBehaviour.programSource;
                    if(programSource is IUdonGraphDataProvider asUdonGraphProgramAsset)
                    {
                        udonGraphProgramAsset = asUdonGraphProgramAsset;
                    }
                }
            }
            
            if (graph == null)
            {
                graph = CreateInstance<UdonGraph>();
            }

            if(udonGraphProgramAsset != null)
            {
                if (graphGUI == null)
                {
                    graphGUI = CreateInstance<UdonGraphGUI>();
                    graphGUI.graph = graph;
                }
                if (graph == null)
                {
                    graph = CreateInstance<UdonGraph>();
                    graphGUI.graph = graph;
                }
                
                if(graph.graphProgramAsset == udonGraphProgramAsset)
                {
                    if (graph.data != null)
                    {
                        return;
                    }
                }

                titleContent = new GUIContent($"Udon - {udonGraphProgramAsset}");
                
                graph.data = new UdonGraphData(udonGraphProgramAsset.GetGraphData());
                graph.graphProgramAsset = udonGraphProgramAsset;
                graph.Reload();
                graphGUI.CenterGraph();
            }
            else
            {
                if(graph.graphProgramAsset != null)
                {
                    return;
                }

                _displayText = "Create an Udon Graph Asset to begin.";
                _drawGraph = false;
            }
        }

        public void OnGUI()
        {
            OnGUILogic();
            DrawToolbar();
            
            if (!_drawGraph)
            {
                DrawCenteredText(_displayText);
            }
            else
            {
                DrawGraph();
            }
        }

        private void DrawNodeSearchBox()
        {
            UdonNodeSearchMenu.DrawWindow(graph, graphGUI);
        }

        private static void DrawCenteredText(string text)
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Box("", _udonLogo);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(100);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label(text);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            
            GUILayout.FlexibleSpace();
        }

        private void DrawGraph()
        {
            GUI.SetNextControlName("Default");
            graphGUI.BeginGraphGUI(this, new Rect(0, TOOLBAR_HEIGHT, position.width, position.height));
            graphGUI.OnGraphGUI();
            graphGUI.EndGraphGUI();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            GUILayout.FlexibleSpace();
            if (_drawGraph)
            {
                using(new EditorGUI.DisabledScope(Application.isPlaying))
                {
                    if (graph.graphProgramAsset is AbstractUdonProgramSource udonProgramSource)
                    {
                        bool triggerRefresh;
                        if (UdonEditorManager.Instance.IsProgramSourceRefreshQueued(udonProgramSource))
                        {
                            triggerRefresh =
                                GUILayout.Button(
                                    $"Auto Compile in {UdonEditorManager.Instance.ProgramRefreshDelayRemaining:F0}s.",
                                    EditorStyles.toolbarButton);
                            Repaint();
                        }
                        else
                        {
                            triggerRefresh = GUILayout.Button("Manual Compile", EditorStyles.toolbarButton);
                        }

                        if (triggerRefresh)
                        {
                            UdonEditorManager.Instance.RefreshQueuedProgramSources();
                        }
                    }

                    DrawNodeSearchBox();
                }

                if (GUILayout.Button(" Recenter ", EditorStyles.toolbarButton))
                {
                    graphGUI.CenterGraph();
                }

                using(new EditorGUI.DisabledScope(Application.isPlaying))
                {
                    if(GUILayout.Button(" Reload Graph ", EditorStyles.toolbarButton))
                    {
                        graph.data = new UdonGraphData(graph.graphProgramAsset.GetGraphData()); //just do this in reload always
                        graph.Reload();
                        graphGUI.CenterGraph();
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
