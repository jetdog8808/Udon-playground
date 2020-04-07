using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using UnityEditor;
using UnityEngine;
using VRC.Udon.EditorBindings;
using VRC.Udon.EditorBindings.Interfaces;
using VRC.Udon.Graph;

namespace VRC.Udon.Editor.ProgramSources
{
    public class UdonNodeSearchMenu : EditorWindow
    {
        private static UdonNodeSearchMenu _instance;

        private string _searchString = string.Empty;
        private UdonGraph _graph;
        private UdonGraphGUI _graphGUI;
        private Styles _styles;
        private Vector2 _scrollPosition = Vector2.zero;
        private IEnumerable<UdonNodeDefinition> _filteredNodeDefinitions;
        private IEnumerable<UdonNodeDefinition> _unfilteredNodeDefinitions;
        private Dictionary<string, UdonNodeDefinition> _searchDefinitions;

        private long _lastTime;
        private float _animValue;
        private float _animGoal = 1;
        private bool _isAnimating;

        private string _currentActivePath = "";
        private string _currentActiveNode = "";
        private int _currentActiveIndex;

        private string _nextActivePath = "";
        private string _nextActiveNode = "";
        private int _nextActiveIndex;

        private const int LIST_BUTTON_HEIGHT = 20;
        private const float DROPDOWN_HEIGHT = 320f;
        private float _dropDownWidth;
        private Rect _dropDownRect;

        private static readonly NodeMenuLayer NodeMenu = new NodeMenuLayer();

        private void Awake()
        {
            //_unfilteredNodeDefinitions = _udonEditorInterface.GetNodeDefinitions();
            Dictionary<string, UdonNodeDefinition> baseNodeDefinitions = new Dictionary<string, UdonNodeDefinition>();
            foreach (UdonNodeDefinition nodeDefinition in UdonEditorManager.Instance.GetNodeDefinitions().OrderBy(s => PrettyFullName(s)))
            {
                string baseIdentifier = nodeDefinition.fullName;
                string[] splitBaseIdentifier = baseIdentifier.Split(new[] {"__"}, StringSplitOptions.None);
                if (splitBaseIdentifier.Length >= 2)
                {
                    baseIdentifier = $"{splitBaseIdentifier[0]}__{splitBaseIdentifier[1]}";
                }
                if (baseNodeDefinitions.ContainsKey(baseIdentifier))
                {
                    continue;
                }
                baseNodeDefinitions.Add(baseIdentifier, nodeDefinition);
            }

            _unfilteredNodeDefinitions = baseNodeDefinitions.Values;
            _searchDefinitions = _unfilteredNodeDefinitions.ToDictionary(nodeDefinition => SanitizedSearchString(nodeDefinition.fullName));
            _filteredNodeDefinitions =  _unfilteredNodeDefinitions;

            string SanitizedSearchString(string s)
            {
                s = s.FriendlyNameify()
                    .ToLowerInvariant()
                    .Replace(".__", ".");
                if (s.StartsWith("variable_"))
                {
                    s = s.Replace("variable_", "");
                }
                s = s.Replace("_", " ")
                    .ReplaceFirst("unityengine", "");
                    //.ReplaceFirst("system", "");
                return s;
            }
        }

        public static void DrawWindow(UdonGraph graph, UdonGraphGUI graphGUI)
        {
            if(_instance == null)
            {
                _instance = CreateInstance<UdonNodeSearchMenu>();
            }

            Rect rect = GUILayoutUtility.GetLastRect();
            bool goodState = graphGUI.selection.Count == 0;
            if(goodState)
            {
                goodState = GUI.GetNameOfFocusedControl() != "NodeField";
            }

            if(goodState && KeyUpEvent(KeyCode.Space) && !Event.current.shift)
            {
                GUI.UnfocusWindow();
            }

            if(!GUILayout.Button("Add Node", EditorStyles.toolbarButton, GUILayout.Width(120)) && !(KeyUpEvent(KeyCode.Space) && goodState))
            {
                return;
            }
            
            rect = RemapRectForPopup(rect);
            _instance.InitWindow(graph, graphGUI, rect);
            _instance.Repaint();
        }

        private void InitWindow(UdonGraph graph, UdonGraphGUI graphGUI, Rect rect)
        {
            _dropDownRect = rect;
            _graph = graph;
            _graphGUI = graphGUI;
            _styles = new Styles();
            wantsMouseMove = true;

            if (NodeMenu.MenuName == null)
            {
                new Thread(() => 
                {
                    Thread.CurrentThread.IsBackground = true; 
                    NodeMenu.PopulateNodeMenu(); 
                }).Start();
            }

            _dropDownWidth = rect.width;
            ShowAsDropDown(rect, new Vector2(rect.width, DROPDOWN_HEIGHT));
            Focus();
        }

        private void OnGUI()
        {
            GUI.Label(new Rect(0.0f, 0.0f, position.width, position.height), GUIContent.none, _styles.Background);
            
            if(_filteredNodeDefinitions == null)
            {
                _filteredNodeDefinitions = _unfilteredNodeDefinitions;
            }
            
            DrawSearchBox();
            
            if(_isAnimating && Event.current.type == EventType.Repaint)
            {
                long ms = DateTime.Now.Millisecond;
                float maxDelta = -(ms - _lastTime) * .01f;
                _lastTime = ms;
                _animValue = Mathf.MoveTowards(_animValue, _animGoal, maxDelta);
                Repaint();
            }
            
            if ((_animValue >= 1 || _animValue <= -1) && Event.current.type != EventType.Repaint)
            {
                _isAnimating = false;
                _animValue = 0;
                
                _currentActivePath = _nextActivePath;
                _currentActiveNode = _nextActiveNode;
                _currentActiveIndex = _nextActiveIndex;
                _nextActivePath = "";
                _nextActiveNode = "";
                _nextActiveIndex = 0;
                
                while (_currentActiveIndex * LIST_BUTTON_HEIGHT >=
                       _scrollPosition.y + DROPDOWN_HEIGHT - (LIST_BUTTON_HEIGHT * 3) - 5)
                {
                    _scrollPosition.y += LIST_BUTTON_HEIGHT;
                }

                while (_currentActiveIndex * LIST_BUTTON_HEIGHT < _scrollPosition.y)
                {
                    _scrollPosition.y -= LIST_BUTTON_HEIGHT;
                }
            }
            
            DrawListGUI(false, _animValue);
            if (_isAnimating)
            {
                //if(animGoal == 1)
                DrawListGUI( true, _animValue - 1);
                //else
                DrawListGUI( true, _animValue + 1);
            }
            

            if(KeyUpEvent(KeyCode.Escape))
            {
                Close();
            }
            
        }
        
        private void DrawSearchBox()
        {
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();

            GUI.SetNextControlName("nodeSearch");
            EditorGUI.BeginChangeCheck();
            _searchString = EditorGUILayout.TextField(_searchString, _styles.SearchTextField);
            if(EditorGUI.EndChangeCheck())
            {
                ApplySearchFilter();
            }

            EditorGUI.FocusTextInControl("nodeSearch");
            GUIStyle searchButtonStyle =
                _searchString == string.Empty ? _styles.SearchCancelButtonEmpty : _styles.SearchCancelButton;

            if(GUILayout.Button(string.Empty, searchButtonStyle))
            {
                _searchString = string.Empty;
                GUI.FocusControl(null);
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
        }

        private void ApplySearchFilter()
        {
            string lowerSearchString = _searchString.ToLowerInvariant();
            string unSpacedSearchString = lowerSearchString.Replace(" ", string.Empty);
            string[] lowerSearchStringArray = lowerSearchString.Split(' ');
            
            if(unSpacedSearchString.Length < 3)
            {
                _filteredNodeDefinitions = new List<UdonNodeDefinition>();
                return;
            }

            _filteredNodeDefinitions = _searchDefinitions
                .Where(s => s.Key.Contains(lowerSearchString))
                .OrderBy(s => s.Key, new SearchComparer(lowerSearchString))
                .Concat(_searchDefinitions
                        .Where(s => s.Key.Contains(unSpacedSearchString))
                        .OrderBy(s => s.Key, new SearchComparer(unSpacedSearchString))
                        .Concat(_searchDefinitions
                            .Where(s => lowerSearchStringArray.All(w => s.Key.Contains(w.ToLowerInvariant())))
                            .OrderBy(s => s.Key, new SearchComparer(lowerSearchStringArray.First().Replace(" ", string.Empty)))
                            .Where(s => s.Key.IndexOf(lowerSearchStringArray.First().Replace(" ", string.Empty), StringComparison.InvariantCultureIgnoreCase) != -1)
                        )
                ) 
                .Select(d => d.Value).Distinct().Take(200).ToArray();

            //if(_filteredNodeDefinitions.Count() > 1000)
            //{
            //    _filteredNodeDefinitions = _filteredNodeDefinitions.Take(1000).ToArray();
            //}
        }

        public static string PrettyFullName(UdonNodeDefinition nodeDefinition, bool keepLong = false)
        {
            string fullName = nodeDefinition.fullName;
            string result;
            if(keepLong)
            {
                result = fullName.Replace("UnityEngine", "UnityEngine.").Replace("System", "System.");
            }
            else
            {
                result = fullName.Replace("UnityEngine", "").Replace("System", "");
            }

            string[] resultSplit = result.Split(new[] {"__"}, StringSplitOptions.None);
            if(resultSplit.Length >= 3)
            {
                string outName = "";
                if (nodeDefinition.type != typeof(void))
                {
                    if (nodeDefinition.Outputs.Count > 0)
                    {
                        outName = string.Join(", ", nodeDefinition.Outputs.Select(o => o.name));
                    }
                }
                
                result = nodeDefinition.Inputs.Count > 0
                    ? $"{resultSplit[0]}{resultSplit[1]}({string.Join(", ", nodeDefinition.Inputs.Select(s => s.name))}{outName})"
                    : $"{resultSplit[0]}{resultSplit[1]}({resultSplit[2].Replace("_", ", ")}{outName})";
            }
            else if(resultSplit.Length >= 2)
            {
                result = $"{resultSplit[0]}{resultSplit[1]}()";
            }

            if(!keepLong)
            {
                result = result.FriendlyNameify();
                result = result.Replace("op_", "");
                result = result.Replace("_", " ");
            }

            return result;
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

        private Rect _headerRect; 
        private void DrawListGUI( bool isNext = false, float anim = 0)
        {
            Rect rect = position;
            rect.x = +1f;
            rect.x -= 230*anim;
            rect.y = 30f;
            rect.height -= 30f;
            rect.width -= 2f;
            GUILayout.BeginArea(rect);
            
            rect = GUILayoutUtility.GetRect(10f, 25f);

            if (string.IsNullOrEmpty(_currentActivePath) || _currentActivePath == "/")
                GUI.Label(rect, _searchString == string.Empty ? "Nodes" : "Search", _styles.Header);
            else
            {
                string nestedTitle = _currentActivePath.Substring(0, _currentActivePath.Length - 1); 
                if (GUI.Button(rect, _searchString == string.Empty ? nestedTitle : "Search", _styles.Header))
                {
                    if (!isNext && !string.IsNullOrEmpty(_currentActivePath))
                    {
                        List<string> tmp = _currentActivePath.Split('/').ToList();
                        if (tmp.Count - 2 >= 0)
                        {
                            _nextActiveNode = tmp[tmp.Count - 2];
                            _nextActiveIndex = 0;
                            tmp.RemoveAt(_currentActivePath.Split('/').Length - 2);
                        }
                        _nextActivePath = string.Join("/", tmp.ToArray());
                        _isAnimating = true;
                        _animGoal = 1;
                        _lastTime = DateTime.Now.Millisecond;
                    }
                    
                }
                if (string.IsNullOrEmpty(_searchString))
                    GUI.Label(new Rect((float)(rect.x + 6.0), rect.y + 6f, 13f, 13f), "", _styles.LeftArrow);
            }

            _headerRect = rect;
            //TODO: don't be lazy, figure out the math for this so you don't have to loop :P
            bool repaint = false;
            while (_currentActiveIndex * LIST_BUTTON_HEIGHT >=
                   _scrollPosition.y + DROPDOWN_HEIGHT - (LIST_BUTTON_HEIGHT * 3) - 5)
            {
                _scrollPosition.y += LIST_BUTTON_HEIGHT;
                repaint = true;
            }

            while (_currentActiveIndex * LIST_BUTTON_HEIGHT < _scrollPosition.y)
            {
                _scrollPosition.y -= LIST_BUTTON_HEIGHT;
                repaint = true;
            }

            if (repaint)
            {
                Repaint();
            }

            float oldScrollPosition = _scrollPosition.y;
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            if (string.IsNullOrEmpty(_searchString))
            {
                DrawNodeEntries(NodeMenu, isNext);
            }
            else
            {
                for(int i = 0; i < (_filteredNodeDefinitions.Count() > 100 ? 100 : _filteredNodeDefinitions.Count()); i++)
                {
                    UdonNodeDefinition udonNodeDefinition = _filteredNodeDefinitions.ElementAt(i);
                    
                    string aB = _currentActiveNode;
                    if(_filteredNodeDefinitions.All(f => f.fullName != aB))
                    {
                        _currentActiveNode = udonNodeDefinition.fullName;
                    }

                    GUIStyle buttonStyle = _currentActiveNode == udonNodeDefinition.fullName
                        ? _styles.ActiveComponentButton
                        : _styles.ComponentButton;

                    rect = GUILayoutUtility.GetRect(10f, LIST_BUTTON_HEIGHT);
                    bool pressedButton = GUI.Button(rect, NodeContentWithIcon(udonNodeDefinition, PrettyFullName(udonNodeDefinition), PrettyFullName(udonNodeDefinition, true)), buttonStyle); 
                    if(pressedButton ||
                       (_currentActiveNode == udonNodeDefinition.fullName && (KeyUpEvent(KeyCode.Return) || KeyUpEvent(KeyCode.RightArrow))))
                    {
                        Rect graphExtents = (Rect)GetInstanceField(typeof(UdonGraph), _graph, "graphExtents");
                        graphExtents = new Rect(new Vector2(_graphGUI.scrollPosition.x + (graphExtents.x + (_graphGUI.Host.position.width / 2)) - 125, _graphGUI.scrollPosition.y + (graphExtents.y + (_graphGUI.Host.position.height / 2)) - 24), new Vector2(10, 10));
                        _graph.CreateNode(udonNodeDefinition, graphExtents.position);
                        Close();
                    }
                    
                    if (rect.Contains(Event.current.mousePosition) &&
                        Event.current.mousePosition.y <= _dropDownRect.yMin + DROPDOWN_HEIGHT + _scrollPosition.y &&
                        Event.current.mousePosition.y > _headerRect.yMax + _scrollPosition.y - LIST_BUTTON_HEIGHT)
                    {
                        _currentActiveNode = udonNodeDefinition.fullName;
                        _currentActiveIndex = i;
                    }
                    

                    if(Event.current.type != EventType.MouseMove)
                    {
                            continue;
                    }
                    
                    if(mouseOverWindow != null)
                    {
                        mouseOverWindow.Repaint();
                    }
                }
            }

            GUILayout.EndScrollView();
            
            if ((int)_scrollPosition.y != (int)oldScrollPosition)
            {
                {
                    Repaint();
                }
            }
            
            GUILayout.EndArea();

            if (!string.IsNullOrEmpty(_searchString))
            {
                if (KeyUsedEvent(KeyCode.DownArrow))
                {
                    OffsetActiveButton(1);
                }
                else if (KeyUsedEvent(KeyCode.UpArrow))
                {
                    OffsetActiveButton(-1);
                }
            }
        }

        private string _loadingText = "Loading Nodes";
        private void DrawNodeEntries(NodeMenuLayer layer, bool isNext)
        {
            if (NodeMenu.MenuName == null)
            {
                Rect buttonRect = GUILayoutUtility.GetRect(10f, LIST_BUTTON_HEIGHT, GUILayout.ExpandWidth(true));
                GUI.Label(buttonRect, _loadingText, _styles.ComponentButton);
                _loadingText = $"{_loadingText}.";
                if (_loadingText.Contains("......"))
                {
                    _loadingText = "Loading Nodes";
                }
                Repaint();
                return;
            }
            
            NodeMenuLayer foundLayer = layer.FindLayer(_currentActivePath);
            string aN = _currentActiveNode;

            if (foundLayer.SubNodes.Count == 0)
            {
                return;
            }
            
            if (foundLayer.SubNodes.All(n => n.MenuName != aN))
            {
                if (!isNext)
                {
                    _currentActiveNode = foundLayer.SubNodes.First().MenuName;
                    _currentActiveIndex = 0;
                    _scrollPosition.y = 0;
                    aN = _currentActiveNode;
                }
            }   

            if (Event.current.type == EventType.Used && Event.current.keyCode == KeyCode.DownArrow)
            {
                int idx = foundLayer.SubNodes.Select((value, index) => new { value, index }).Where(pair => pair.value.MenuName == aN).Select(pair => pair.index).FirstOrDefault();
                if (idx + 1 < foundLayer.SubNodes.Count)
                {
                    if (!isNext)
                    {
                        _currentActiveNode = foundLayer.SubNodes[idx + 1].MenuName;
                        _currentActiveIndex = idx + 1;
                        if (_currentActiveIndex * LIST_BUTTON_HEIGHT >=
                            _scrollPosition.y + DROPDOWN_HEIGHT - (LIST_BUTTON_HEIGHT * 3) - 5)
                        {
                            _scrollPosition.y += LIST_BUTTON_HEIGHT;
                        }
                    }
                }
            }
            if (Event.current.type == EventType.Used && Event.current.keyCode == KeyCode.UpArrow)
            {
                int idx = foundLayer.SubNodes.Select((value, index) => new { value, index }).Where(pair => pair.value.MenuName == aN).Select(pair => pair.index).FirstOrDefault();
                if (idx - 1 >= 0)
                {
                    if (!isNext)
                    {
                        _currentActiveNode = foundLayer.SubNodes[idx - 1].MenuName;
                        _currentActiveIndex = idx - 1;
                        if (_currentActiveIndex * LIST_BUTTON_HEIGHT < _scrollPosition.y)
                        {
                            _scrollPosition.y -= LIST_BUTTON_HEIGHT;
                        }
                    }
                }
            }
            if (Event.current.type == EventType.Used && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.RightArrow))
            {
                if (!isNext)
                {
                    if (foundLayer.SubNodes.First(n => n.MenuName == aN).SubNodes.Count == 0)
                    {
                        Rect graphExtents = (Rect)GetInstanceField(typeof(UdonGraph), _graph, "graphExtents");
                        graphExtents = new Rect(new Vector2(_graphGUI.scrollPosition.x + (graphExtents.x + (_graphGUI.Host.position.width / 2)) - 125, _graphGUI.scrollPosition.y + (graphExtents.y + (_graphGUI.Host.position.height / 2)) - 24), new Vector2(10, 10));
                        _graph.CreateNode(foundLayer.SubNodes.First(n => n.MenuName == aN).NodeDefinition, graphExtents.position);
                        Close();
                    }
                    else
                    {
                        _nextActivePath = _currentActivePath + _currentActiveNode + "/";
                        _isAnimating = true;
                        _animGoal = -1;
                        _lastTime = DateTime.Now.Millisecond;
                    }
                }
            }
            if (Event.current.type == EventType.Used && (Event.current.keyCode == KeyCode.Backspace || Event.current.keyCode == KeyCode.LeftArrow))
            {
                if (!isNext && !string.IsNullOrEmpty(_currentActivePath))
                {
                    List<string> tmp = _currentActivePath.Split('/').ToList();
                    if (tmp.Count - 2 >= 0)
                    {
                        _nextActiveNode = tmp[tmp.Count - 2];
                        _nextActiveIndex = 0;
                        tmp.RemoveAt(_currentActivePath.Split('/').Length - 2);
                    }
                
                    _nextActivePath = string.Join("/", tmp.ToArray());
                    _isAnimating = true;
                    _animGoal = 1;
                    _lastTime = DateTime.Now.Millisecond;
                }
            }

            int nodeIndex = 0;
            foreach (NodeMenuLayer item in foundLayer.SubNodes)
            {
                Rect buttonRect = GUILayoutUtility.GetRect(10f, LIST_BUTTON_HEIGHT, GUILayout.ExpandWidth(true));
                if (!isNext)
                {
                    if (string.IsNullOrEmpty(_currentActiveNode))
                    {
                        _currentActiveNode = item.MenuName;
                        _currentActiveIndex = nodeIndex;
                        while (_currentActiveIndex * LIST_BUTTON_HEIGHT >=
                               _scrollPosition.y + DROPDOWN_HEIGHT - (LIST_BUTTON_HEIGHT * 3) - 5)
                        {
                            _scrollPosition.y += LIST_BUTTON_HEIGHT;
                        }

                        while (_currentActiveIndex * LIST_BUTTON_HEIGHT < _scrollPosition.y)
                        {
                            _scrollPosition.y -= LIST_BUTTON_HEIGHT;
                        }
                    }
                }

                GUIStyle buttonStyle = _styles.ComponentButton;
                if (_currentActiveNode == item.MenuName)
                {
                    buttonStyle = _styles.ActiveComponentButton;
                }

                if (GUI.Button(buttonRect,
                    NodeContentWithIcon(item.NodeDefinition, item.MenuName,
                        item.NodeDefinition != null ? PrettyFullName(item.NodeDefinition, true) : item.MenuName),
                    buttonStyle))
                {
                    if (item.SubNodes.Count == 0)
                    {
                        Rect graphExtents = (Rect)GetInstanceField(typeof(UdonGraph), _graph, "graphExtents");
                        graphExtents = new Rect(new Vector2(_graphGUI.scrollPosition.x + (graphExtents.x + (_graphGUI.Host.position.width / 2)) - 125, _graphGUI.scrollPosition.y + (graphExtents.y + (_graphGUI.Host.position.height / 2)) - 24), new Vector2(10, 10));
                        _graph.CreateNode(foundLayer.SubNodes.First(n => n.MenuName == aN).NodeDefinition, graphExtents.position);
                        Close();
                    }
                    
                    if (!isNext)
                    {
                        _nextActivePath = $"{_currentActivePath}{item.MenuName}/";
                        _isAnimating = true;
                        _animGoal = -1;
                        _lastTime = DateTime.Now.Millisecond;
                    }
                }

                if (buttonRect.Contains(Event.current.mousePosition) &&
                    Event.current.mousePosition.y <= _dropDownRect.yMin + DROPDOWN_HEIGHT + _scrollPosition.y &&
                    Event.current.mousePosition.y > _headerRect.yMax + _scrollPosition.y - LIST_BUTTON_HEIGHT)
                {
                    if (!isNext)
                    {
                        _currentActiveNode = item.MenuName;
                        _currentActiveIndex = nodeIndex;
                        while (_currentActiveIndex * LIST_BUTTON_HEIGHT >=
                               _scrollPosition.y + DROPDOWN_HEIGHT - (LIST_BUTTON_HEIGHT * 3) - 5)
                        {
                            _scrollPosition.y += LIST_BUTTON_HEIGHT;
                        }

                        while (_currentActiveIndex * LIST_BUTTON_HEIGHT < _scrollPosition.y)
                        {
                            _scrollPosition.y -= LIST_BUTTON_HEIGHT;
                        }
                    }
                }

                if (buttonRect.Contains(Event.current.mousePosition))
                {
                    if (!isNext)
                    {
                        if (Event.current.type == EventType.MouseMove)
                        {
                            if (mouseOverWindow != null)
                            {
                                mouseOverWindow.Repaint();
                            }
                        }
                    }
                    else
                    {
                        _nextActiveNode = item.MenuName;
                        _nextActiveIndex = nodeIndex;
                    }
                }

                if (item.SubNodes.Count > 0)
                {
                    GUI.Label(
                        new Rect((float) ((double) buttonRect.x + buttonRect.width - 13.0), buttonRect.y + 4f, 13f,
                            13f), "", _styles.RightArrow);
                }

                nodeIndex++;
            }

        }

        private void OffsetActiveButton(int offset)
        {
            _currentActiveIndex += offset;
            _currentActiveIndex = Mathf.Clamp(_currentActiveIndex, 0, _filteredNodeDefinitions.Count() - 1);
            _currentActiveNode = _filteredNodeDefinitions.ElementAt(_currentActiveIndex).fullName;
            while (_currentActiveIndex * LIST_BUTTON_HEIGHT >=
                   _scrollPosition.y + DROPDOWN_HEIGHT - (LIST_BUTTON_HEIGHT * 3) - 5)
            {
                _scrollPosition.y += LIST_BUTTON_HEIGHT;
            }

            while (_currentActiveIndex * LIST_BUTTON_HEIGHT < _scrollPosition.y)
            {
                _scrollPosition.y -= LIST_BUTTON_HEIGHT;
            }
        }

        private static Rect RemapRectForPopup(Rect rect)
        {
            rect.y += 26f;
            rect.x += rect.width;
            rect.width = 500;
            rect.x -= rect.width / 2;
            Vector2 v2 = GUIUtility.GUIToScreenPoint(new Vector2(rect.x, rect.y));
            rect.x = v2.x;
            rect.y = v2.y;
            return rect;
        }

        private static bool KeyUpEvent(KeyCode keyCode)
        {
            return Event.current.type == EventType.KeyUp && Event.current.keyCode == keyCode;
        }

        private static bool KeyUsedEvent(KeyCode keyCode)
        {
            return Event.current.type == EventType.Used && Event.current.keyCode == keyCode;
        }

        private static Dictionary<Type, Texture2D> typeThumbCache = new Dictionary<Type, Texture2D>(); 
        private GUIContent NodeContentWithIcon(UdonNodeDefinition nodeDefinition, string menuName, string tooltip)
        {
            GUIContent content;
            if (nodeDefinition != null)
            {
                Type thumbType = nodeDefinition.type;
                
                if (thumbType != null)
                {
                    if (thumbType.IsArray)
                    {
                        thumbType = thumbType.GetElementType();
                    }
                }
                
                Texture2D thumb = GetCachedTypeThumbnail(thumbType);

                //TODO: This is real gross and hacky, figure out how to just let the name clip naturally without clipping the icon in the process 
                int maxLength = (int)(_dropDownWidth / 7);
                menuName = menuName.Substring(0, menuName.Length <= maxLength ? menuName.Length : maxLength);
                while (GUI.skin.label.CalcSize(new GUIContent(menuName)).x > _dropDownWidth - 35)
                {
                    menuName = menuName.Substring(0, menuName.Length - 1);
                }
                
                content = thumb != null
                    ? new GUIContent($"{menuName}", thumb, tooltip)
                    : new GUIContent($"     {menuName}", tooltip);
            }
            else
            {
                content = new GUIContent($"     {menuName}", tooltip);
            }
            return content;
        }

        public static Texture2D GetCachedTypeThumbnail(Type thumbType)
        {
            Texture2D thumb;
            if (thumbType == null)
            {
                thumb = null;
            }
            else
            {
                if (typeThumbCache.ContainsKey(thumbType))
                {
                    thumb = typeThumbCache[thumbType];
                }
                else
                {
                    thumb = AssetPreview.GetMiniTypeThumbnail(thumbType);
                    typeThumbCache.Add(thumbType, thumb);
                }
            }

            return thumb;
        }

        private class Styles
        {
            public readonly GUIStyle ComponentButton = new GUIStyle("PR Label");
            public readonly GUIStyle ActiveComponentButton;
            public readonly GUIStyle Header = new GUIStyle("In BigTitle");
            public readonly GUIStyle SearchTextField;
            public readonly GUIStyle SearchCancelButton;
            public readonly GUIStyle SearchCancelButtonEmpty;
            public readonly GUIStyle Background = "grey_border";
            //public readonly GUIStyle PreviewBackground = "PopupCurveSwatchBackground";
            //public readonly GUIStyle PreviewHeader = new GUIStyle(EditorStyles.label);
            //public readonly GUIStyle PreviewText = new GUIStyle(EditorStyles.wordWrappedLabel);
            public readonly GUIStyle RightArrow = "AC RightArrow";
            public readonly GUIStyle LeftArrow = "AC LeftArrow";
            //public readonly GUIStyle GroupButton;

            public Styles()
            {
                ComponentButton.active = ComponentButton.onActive;
                ComponentButton.hover = ComponentButton.onHover;
                ComponentButton.padding.left -= 15;
                ComponentButton.fixedHeight = 20f;
                //ComponentButton.stretchWidth = true;
                ComponentButton.alignment = TextAnchor.MiddleLeft;//.MiddleLeft;
                
                ActiveComponentButton = new GUIStyle(ComponentButton);
                ActiveComponentButton.normal = ActiveComponentButton.onHover;

                Header.font = EditorStyles.boldLabel.font;

                SearchTextField = GUI.skin.FindStyle("SearchTextField");
                SearchCancelButton = GUI.skin.FindStyle("SearchCancelButton");
                SearchCancelButtonEmpty = GUI.skin.FindStyle("SearchCancelButtonEmpty");
                
                Header.font = EditorStyles.boldLabel.font;
                
                //GroupButton = new GUIStyle(ComponentButton);
                //GroupButton.padding.left += 17;
                //PreviewText.padding.left += 3;
                //PreviewText.padding.right += 3;
                //++PreviewHeader.padding.left;
                //PreviewHeader.padding.right += 3;
                //PreviewHeader.padding.top += 3;
                //PreviewHeader.padding.bottom += 2;

                
            }
        }
        
        public class NodeMenuLayer
        {
            public string MenuName;
            public UdonNodeDefinition NodeDefinition;
            public readonly List<NodeMenuLayer> SubNodes = new List<NodeMenuLayer>();
        }
    }
}
