using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.Graphs;
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable InvertIf
// ReSharper disable EnforceIfStatementBraces
// ReSharper disable UnusedMember.Local
// ReSharper disable Unity.InefficientMultiplicationOrder
// ReSharper disable PossibleNullReferenceException
// ReSharper disable RedundantExplicitArrayCreation
// ReSharper disable RedundantExplicitParamsArrayCreation
// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable ConvertToConstant.Global
// ReSharper disable RedundantNameQualifier
// ReSharper disable ArrangeThisQualifier


namespace VRC.Udon.Editor.ProgramSources
{
    public class UdonEdgeGUI : IEdgeGUI
    {
        public EdgeGUI.EdgeStyle edgeStyle = EdgeGUI.EdgeStyle.Curvy;

        private static Slot _sDragSourceSlot;
        private static Slot _sDropTarget;

        public List<int> edgeSelection { get; set; }

        public GraphGUI host { get; set; }

        private Edge DontDrawEdge { get; set; }

        private Edge MoveEdge { get; set; }

        public UdonEdgeGUI()
        {
            edgeSelection = new List<int>();
        }

        public void BeginSlotDragging(Slot slot, bool allowStartDrag, bool allowEndDrag)
        {
            if(allowStartDrag)
            {
                _sDragSourceSlot = slot;
                Event.current.Use();
            }

            if(allowEndDrag && slot.edges.Count > 0)
            {
                MoveEdge = slot.edges[slot.edges.Count - 1];
                _sDragSourceSlot = MoveEdge.fromSlot;
                _sDropTarget = slot;
                Event.current.Use();
            }
        }

        public void DoDraggedEdge()
        {
            if(_sDragSourceSlot != null)
            {
                EventType typeForControl = Event.current.GetTypeForControl(0);
                if(typeForControl != EventType.Repaint)
                {
                    if(typeForControl == EventType.MouseDrag)
                    {
                        _sDropTarget = null;
                        DontDrawEdge = null;
                        Event.current.Use();
                    }
                }
                else
                {
                    Assembly unityEngineAssembly = Assembly.GetAssembly(typeof(UnityEngine.GUI));
                    Type guiClipType = unityEngineAssembly.GetType("UnityEngine.GUIClip", true);

                    FieldInfo propInfo = typeof(Slot).GetField(
                        "m_Position",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                    if(propInfo == null) Debug.LogError("PropInfo m_Position is null!");
                    Rect position = (Rect)propInfo.GetValue(_sDragSourceSlot);

                    Vector2 end = Event.current.mousePosition;

                    if(_sDropTarget != null)
                    {
                        Rect position2 = (Rect)propInfo.GetValue(_sDropTarget);

                        object[] endArgs = {new Vector2(position2.x, position2.y + 9f)};
                        ParameterModifier endP = new ParameterModifier(1);
                        endP[0] = true;
                        ParameterModifier[] endMods = {endP};
                        MethodInfo endClipRect = guiClipType.GetMethod(
                            "Clip",
                            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic |
                            BindingFlags.FlattenHierarchy,
                            Type.DefaultBinder,
                            new Type[] {typeof(Vector2)},
                            endMods);

                        end = (Vector2)endClipRect.Invoke(null, endArgs);
                    }

                    object[] startArgs = {new Vector2(position.xMax, position.y + 9f)};
                    ParameterModifier startP = new ParameterModifier(1);
                    startP[0] = true;
                    ParameterModifier[] startMods = {startP};
                    MethodInfo startClipRect = guiClipType.GetMethod(
                        "Clip",
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic |
                        BindingFlags.FlattenHierarchy,
                        Type.DefaultBinder,
                        new Type[] {typeof(Vector2)},
                        startMods);

                    Vector2 start = (Vector2)startClipRect.Invoke(null, startArgs);

                    Color edgeColor = Color.white;
                    if (_sDragSourceSlot.dataType != null)
                        edgeColor = UdonGraphGUI.MapTypeToColor(_sDragSourceSlot.dataType);

                    DrawEdge(start, end, (Texture2D)Styles.selectedConnectionTexture.image, edgeColor, edgeStyle);
                }
            }
        }

        public void DoEdges()
        {
            int num = 0;
            int num2 = 0;
            if(Event.current.type == EventType.Repaint)
            {
                foreach(Edge current in host.graph.edges)
                {
                    if(current == DontDrawEdge || current == MoveEdge) continue;

                    Texture2D tex = (Texture2D)Styles.connectionTexture.image;
                    if(num < edgeSelection.Count && edgeSelection[num] == num2)
                    {
                        num++;
                        tex = (Texture2D)Styles.selectedConnectionTexture.image;
                    }

                    // TODO: CLEAN-UP
                    Color color;
                    if(!current.toSlot.isFlowSlot && current.toSlot.dataType != null &&
                       current.toSlot.dataType.IsSubclassOf(typeof(UnityEngine.Object)))
                    {
                        color = EdgeGUI.kObjectTypeEdgeColor;
                    }
                    
                    Color niceGrey = new Color(.85f, .85f, .85f);
                    color = current.fromSlot.dataType != null ? UdonGraphGUI.MapTypeToColor(current.fromSlot.dataType) : niceGrey;
                    
                    Color endColor = new Color(.85f, .85f, .85f);
                    if (current.toSlot.dataType != null)
                    {
                        endColor = UdonGraphGUI.MapTypeToColor(current.toSlot.dataType);
                    }

                    DrawEdge(current, tex, color != endColor ? Color.Lerp(color, endColor, .5f) : color, edgeStyle);
                    num2++;
                }
            }

            if(_sDragSourceSlot == null) return;
            if(Event.current.type != EventType.MouseUp) return;

            if(MoveEdge != null)
            {
                host.graph.RemoveEdge(MoveEdge);
                MoveEdge = null;
            }

            if(_sDropTarget != null) return;

            EndDragging();
            Event.current.Use();
        }

        private static void DrawEdge(Edge e, Texture2D tex, Color color, EdgeGUI.EdgeStyle style)
        {
            Vector2 start;
            Vector2 end;
            GetEdgeEndPoints(e, out start, out end);
            DrawEdge(start, end, tex, color, style);
        }

        private static void DrawEdge(Edge e, Texture2D tex, Color color, Color endColor, EdgeGUI.EdgeStyle style)
        {
            Vector2 start;
            Vector2 end;
            GetEdgeEndPoints(e, out start, out end);
            DrawEdge(start, end, tex, color, endColor, style);
        }

        private static void DrawEdge(Vector2 start, Vector2 end, Texture2D tex, Color color, EdgeGUI.EdgeStyle style)
        {
            if(style != EdgeGUI.EdgeStyle.Angular)
            {
                if(style != EdgeGUI.EdgeStyle.Curvy) return;

                Vector3[] array;
                Vector3[] array2;
                GetCurvyConnectorValues(start, end, out array, out array2);
                Handles.DrawBezier(array[0], array[1], array2[0], array2[1], color, tex, 8f);
            }
            else
            {
                Vector3[] array;
                Vector3[] array2;
                GetAngularConnectorValues(start, end, out array, out array2);
                DrawRoundedPolyLine(array, array2, tex, color);
            }
        }

        private static void DrawEdge(Vector2 start, Vector2 end, Texture2D tex, Color color, Color endColor,
            EdgeGUI.EdgeStyle style)
        {
            if(style != EdgeGUI.EdgeStyle.Angular)
            {
                if(style != EdgeGUI.EdgeStyle.Curvy) return;

                Vector3[] array;
                Vector3[] array2;

                Vector3[] array3;
                Vector3[] array4;

                Vector3[] array5;
                Vector3[] array6;
                GetCurvyConnectorValues(start, end, out array5, out array6);
                GetCurvyConnectorValues(start, (start + end) / 2f, out array, out array2);
                GetCurvyConnectorValues((start + end) / 2f, end, out array3, out array4);

                Handles.DrawBezier(array[0], array[1], array6[0], array2[1], color, tex, 8f);
                Handles.DrawBezier(array3[0], array3[1], array4[0], array6[1], endColor, tex, 8f);
            }
            else
            {
                Vector3[] array;
                Vector3[] array2;
                GetAngularConnectorValues(start, end, out array, out array2);
                DrawRoundedPolyLine(array, array2, tex, color);
            }
        }

        private static void GetCurvyConnectorValues(Vector2 start, Vector2 end, out Vector3[] points,
            out Vector3[] tangents)
        {
            points = new Vector3[]
            {
                start,
                end
            };

            tangents = new Vector3[2];

            float num = 0.5f;
            float num2 = 1f - num;
            float num3 = 0f;
            if(start.x > end.x)
            {
                num = (num2 = -0.25f);
                float f = (start.x - end.x) / (start.y - end.y);
                if(Mathf.Abs(f) > 0.5f)
                {
                    float num4 = (Mathf.Abs(f) - 0.5f) / 8f;
                    num4 = Mathf.Sqrt(num4);
                    num3 = Mathf.Min(num4 * 80f, 80f);
                    if(start.y > end.y)
                    {
                        num3 = -num3;
                    }
                }
            }

            float d = Mathf.Clamp01(((start - end).magnitude - 10f) / 50f);
            tangents[0] = start + new Vector2((end.x - start.x) * num + 30f, num3) * d;
            tangents[1] = end + new Vector2((end.x - start.x) * -num2 - 30f, -num3) * d;
        }

        private static void GetAngularConnectorValues(Vector2 start, Vector2 end, out Vector3[] points,
            out Vector3[] tangents)
        {
            Vector2 a = start - end;
            Vector2 vector = a / 2f + end;
            Vector2 vector2 = new Vector2(Mathf.Sign(a.x), Mathf.Sign(a.y));
            Vector2 vector3 = new Vector2(Mathf.Min(Mathf.Abs(a.x / 2f), 5f), Mathf.Min(Mathf.Abs(a.y / 2f), 5f));
            points = new Vector3[]
            {
                start,
                new Vector3(vector.x + vector3.x * vector2.x, start.y),
                new Vector3(vector.x, start.y - vector3.y * vector2.y),
                new Vector3(vector.x, end.y + vector3.y * vector2.y),
                new Vector3(vector.x - vector3.x * vector2.x, end.y),
                end
            };

            tangents = new Vector3[]
            {
                (points[1] - points[0]).normalized * vector3.x * 0.6f + points[1],
                (points[2] - points[3]).normalized * vector3.y * 0.6f + points[2],
                (points[3] - points[2]).normalized * vector3.y * 0.6f + points[3],
                (points[4] - points[5]).normalized * vector3.x * 0.6f + points[4]
            };
        }

        private static void DrawRoundedPolyLine(Vector3[] points, Vector3[] tangents, Texture2D tex, Color color)
        {
            Handles.color = color;
            for(int i = 0; i < points.Length; i += 2)
            {
                Handles.DrawAAPolyLine(
                    tex,
                    3f,
                    new Vector3[]
                    {
                        points[i],
                        points[i + 1]
                    });
            }

            for(int j = 0; j < tangents.Length; j += 2)
            {
                Handles.DrawBezier(points[j + 1], points[j + 2], tangents[j], tangents[j + 1], color, tex, 3f);
            }
        }

        private static void GetEdgeEndPoints(Edge e, out Vector2 start, out Vector2 end)
        {
            FieldInfo propInfo = typeof(Slot).GetField(
                "m_Position",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if(propInfo == null) Debug.LogError("PropInfo m_Position is null!");
            Rect fromSlotPosition = (Rect)propInfo.GetValue(e.fromSlot);
            Rect toSlotPosition = (Rect)propInfo.GetValue(e.toSlot);

            Assembly unityEngineAssembly = Assembly.GetAssembly(typeof(UnityEngine.GUI));
            Type guiClipType = unityEngineAssembly.GetType("UnityEngine.GUIClip", true);

            object[] startArgs = {new Vector2(fromSlotPosition.xMax, fromSlotPosition.y + 9f)};
            object[] endArgs = {new Vector2(toSlotPosition.x, toSlotPosition.y + 9f)};

            ParameterModifier startP = new ParameterModifier(1);
            startP[0] = true;
            ParameterModifier[] startMods = {startP};

            ParameterModifier endP = new ParameterModifier(1);
            endP[0] = true;
            ParameterModifier[] endMods = {endP};

            MethodInfo startClipRect = guiClipType.GetMethod(
                "Clip",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy,
                Type.DefaultBinder,
                new Type[] {typeof(Vector2)},
                startMods);

            MethodInfo endClipRect = guiClipType.GetMethod(
                "Clip",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy,
                Type.DefaultBinder,
                new Type[] {typeof(Vector2)},
                endMods);

            start = (Vector2)startClipRect.Invoke(null, startArgs);
            end = (Vector2)endClipRect.Invoke(null, endArgs);
        }

        public void EndDragging()
        {
            _sDragSourceSlot = (_sDropTarget = null);
            MoveEdge = null;
            DontDrawEdge = null;
        }

        public void EndSlotDragging(Slot slot, bool allowMultiple)
        {
            if(slot.isInputSlot && slot.isFlowSlot)
                allowMultiple = true;

            if(_sDropTarget != slot) return;

            if(MoveEdge != null)
            {
                slot.node.graph.RemoveEdge(MoveEdge);
            }

            while(_sDropTarget.edges.Count > 0)
            {
                if(allowMultiple)
                {
                    break;
                }

                slot.node.graph.RemoveEdge(_sDropTarget.edges[0]);
            }

            try
            {
                slot.node.graph.Connect(_sDragSourceSlot, slot);
            }
            finally
            {
                EndDragging();
                slot.node.graph.Dirty();
                Event.current.Use();
            }

            GUIUtility.ExitGUI();
        }


        public Edge FindClosestEdge()
        {
            Vector2 mousePosition = Event.current.mousePosition;
            float num = float.PositiveInfinity;
            Edge result = null;
            foreach(Edge current in host.graph.edges)
            {
                Vector2 start;
                Vector2 end;
                GetEdgeEndPoints(current, out start, out end);
                Vector3[] array;
                if(this.edgeStyle == EdgeGUI.EdgeStyle.Angular)
                {
                    Vector3[] array2;
                    GetAngularConnectorValues(start, end, out array, out array2);
                }
                else
                {
                    Vector3[] array2;
                    GetCurvyConnectorValues(start, end, out array, out array2);
                }

                for(int i = 0; i < array.Length; i += 2)
                {
                    float num2 = HandleUtility.DistancePointLine(mousePosition, array[i], array[i + 1]);
                    if(!(num2 < num)) continue;

                    num = num2;
                    result = current;
                }
            }

            if(num > 10f)
            {
                result = null;
            }

            return result;
        }

        public void SlotDragging(Slot slot, bool allowEndDrag, bool allowMultiple)
        {
            if(slot.isInputSlot && slot.isFlowSlot)
                allowMultiple = true;

            if(!allowEndDrag || _sDragSourceSlot == null || _sDragSourceSlot == slot) return;

            if(_sDropTarget != slot && slot.node.graph.CanConnect(_sDragSourceSlot, slot) &&
               !slot.node.graph.Connected(_sDragSourceSlot, slot))
            {
                if((slot.node).inputDataEdges.All(e => e != MoveEdge))
                {
                    _sDropTarget = slot;
                    if(slot.edges.Count > 0 && !allowMultiple)
                    {
                        DontDrawEdge = slot.edges[slot.edges.Count - 1];
                    }
                }
            }

            Event.current.Use();
        }
    }
}
