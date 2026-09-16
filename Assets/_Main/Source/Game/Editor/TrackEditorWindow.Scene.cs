using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MatchRacers.Editor
{
    public sealed partial class TrackEditorWindow
    {
        private const float GizmoStep = 2f;
        private const float ToolbarWidth = 270f;

        private static readonly Color CenterlineColor = new Color(1f, 0.85f, 0.2f, 0.9f);
        private static readonly Color EdgeColor = new Color(1f, 1f, 1f, 0.35f);
        private static readonly Color SelectedColor = new Color(0.2f, 0.9f, 1f, 1f);
        private static readonly Color StartColor = new Color(0.3f, 0.9f, 0.4f, 1f);
        private static readonly Color FinishColor = new Color(1f, 0.3f, 0.3f, 1f);
        private static readonly Color PropColor = new Color(1f, 0.55f, 0.1f, 0.9f);
        private static readonly Color DimColor = new Color(1f, 1f, 1f, 0.25f);
        private static readonly Color RiseColor = new Color(0.35f, 1f, 0.45f, 1f);

        [SerializeField] private bool m_StartSelected;

        private readonly List<Vector3> m_Line = new List<Vector3>();
        private Rect m_ToolbarRect;

        private void OnSceneGUI(SceneView view)
        {
            if (m_Layout == null)
                return;

            if (m_Serialized == null)
                Bind();

            if (m_Mode == ETrackEditMode.Objects)
                HandleObjectInput();

            if (m_Path != null)
                DrawRouteGizmos();

            DrawPropGizmos(m_Mode == ETrackEditMode.Props);
            DrawLandmarkGizmos(m_Mode == ETrackEditMode.Objects);

            switch (m_Mode)
            {
                case ETrackEditMode.Road:
                    DrawRoadHandles();
                    break;
                case ETrackEditMode.Props:
                    DrawSelectedPropHandles();
                    break;
                default:
                    DrawSelectedLandmarkHandles();
                    break;
            }

            Tools.hidden = (m_Mode == ETrackEditMode.Objects && m_LandmarkList != null && m_LandmarkList.index >= 0) ||
                           (m_Mode == ETrackEditMode.Road && m_StartSelected);

            DrawSceneToolbar();
        }

        private void DrawSceneToolbar()
        {
            float height = m_Mode == ETrackEditMode.Objects ? 150f : 128f;
            m_ToolbarRect = new Rect(10f, 10f, ToolbarWidth, height);

            Handles.BeginGUI();
            GUILayout.BeginArea(m_ToolbarRect, GUI.skin.box);
            GUILayout.Label("Track Editor · " + m_Layout.name, EditorStyles.boldLabel);
            SetMode((ETrackEditMode)GUILayout.Toolbar((int)m_Mode, ModeNames));

            switch (m_Mode)
            {
                case ETrackEditMode.Road:
                    DrawRoadToolbar();
                    break;
                case ETrackEditMode.Props:
                    DrawPropToolbar();
                    break;
                default:
                    DrawObjectToolbar();
                    break;
            }

            GUILayout.EndArea();
            Handles.EndGUI();
        }

        private void DrawRoadToolbar()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+ Straight"))
                AddSegment(TrackSegment.Straight(m_DefaultStraight));
            if (GUILayout.Button("+ Left"))
                AddSegment(TrackSegment.Turn(-45f, m_DefaultRadius));
            if (GUILayout.Button("+ Right"))
                AddSegment(TrackSegment.Turn(45f, m_DefaultRadius));
            GUILayout.EndHorizontal();

            bool hasSelection = m_SegmentList != null && m_SegmentList.index >= 0 && m_SegmentList.index < m_Layout.SegmentCount;
            GUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(!hasSelection || !m_Layout.GetSegment(Mathf.Max(0, m_SegmentList.index)).IsTurn))
            {
                if (GUILayout.Button("Flip Turn"))
                    FlipSelectedTurn();
            }

            using (new EditorGUI.DisabledScope(!hasSelection))
            {
                if (GUILayout.Button("Delete"))
                    DeleteSelectedSegment();
            }

            if (GUILayout.Button("Close Loop"))
                CloseLoop();

            GUILayout.EndHorizontal();

            GUILayout.Label(hasSelection
                ? DescribeSegment(m_SegmentList.index, m_Layout.GetSegment(m_SegmentList.index))
                : "Click a yellow dot to edit a segment, the green cube to move the start.", EditorStyles.wordWrappedMiniLabel);
        }

        private void DrawPropToolbar()
        {
            bool hasSelection = m_PropList != null && m_PropList.index >= 0 && m_PropList.index < m_Layout.PropCount;

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+ " + m_FillProp))
                AddProp(TrackPropPlacement.Create(m_FillProp, m_FillSide, CurrentViewDistance(), m_FillOffset, 1, m_FillSpacing));

            using (new EditorGUI.DisabledScope(!hasSelection))
            {
                if (GUILayout.Button("Stack"))
                    StackSelectedProp();
                if (GUILayout.Button("Delete"))
                    DeleteArrayElement(PropsProperty, m_PropList);
            }

            GUILayout.EndHorizontal();
            GUILayout.Label("Click an orange box to select a row. Sphere slides it along the road, arrow sets its edge offset, green arrow lifts it. Stack puts a copy on top.",
                EditorStyles.wordWrappedMiniLabel);
        }

        private void DrawRouteGizmos()
        {
            float half = RoadHalfWidth;
            float race = RaceLength;
            float total = m_Path.TotalLength;
            bool road = m_Mode == ETrackEditMode.Road;

            DrawBand(0f, total, 0f, road ? CenterlineColor : DimColor, road ? 3f : 2f);
            DrawBand(0f, total, -half, EdgeColor, 2f);
            DrawBand(0f, total, half, EdgeColor, 2f);

            DrawCrossLine(0f, half, StartColor, "START");
            DrawCrossLine(race, half, FinishColor, "FINISH");

            Handles.color = EdgeColor;
            for (float d = 100f; d < race; d += 100f)
                Handles.Label(OffsetPoint(d, -NearSign * (half + 2f)), ((int)d).ToString());

            m_Path.Evaluate(0f, out Vector3 origin, out Vector3 forward);
            Handles.Label(origin + RacePath.RightOf(forward) * (NearSign * (half + 6f)) + Vector3.up, "camera side");
        }

        private void DrawRoadHandles()
        {
            Vector3 start = m_Layout.StartPosition;
            float startSize = HandleUtility.GetHandleSize(start) * 0.14f;
            Handles.color = StartColor;
            if (Handles.Button(start + Vector3.up * 0.5f, Quaternion.identity, startSize, startSize * 1.3f, Handles.CubeHandleCap))
            {
                bool select = !m_StartSelected;
                SelectSegment(-1);
                m_StartSelected = select;
            }

            if (m_StartSelected)
                DrawStartHandles(start);

            Vector3 position = m_Layout.StartPosition;
            float heading = m_Layout.StartHeading;
            int selected = m_SegmentList != null ? m_SegmentList.index : -1;

            for (int i = 0; i < m_Layout.SegmentCount; i++)
            {
                TrackSegment segment = m_Layout.GetSegment(i);
                Vector3 segmentStart = position;
                float segmentHeading = heading;
                TrackLayoutSO.Advance(segment, ref position, ref heading);

                if (i == selected)
                {
                    DrawSelectedSegmentOutline(i, segment);
                    DrawSegmentHandles(i, segment, segmentStart, segmentHeading, position);
                    continue;
                }

                Vector3 mid = TrackLayoutSO.GetSegmentMidpoint(segment, segmentStart, segmentHeading);
                float size = HandleUtility.GetHandleSize(mid) * 0.09f;
                Handles.color = CenterlineColor;
                if (Handles.Button(mid + Vector3.up * 0.5f, Quaternion.identity, size, size * 1.4f, Handles.SphereHandleCap))
                    SelectSegment(i);
            }

            DrawFinishHandle();

            if (m_Layout.ClosedLoop)
                return;

            float endSize = HandleUtility.GetHandleSize(position) * 0.12f;
            Handles.color = Color.white;
            Handles.Label(position + Vector3.up * 3f, "+ straight");
            if (Handles.Button(position + Vector3.up * 0.5f, Quaternion.identity, endSize, endSize * 1.3f, Handles.CubeHandleCap))
            {
                SelectSegment(m_Layout.SegmentCount - 1);
                AddSegment(TrackSegment.Straight(m_DefaultStraight));
            }
        }

        private void DrawFinishHandle()
        {
            if (m_Path == null || m_Config == null)
                return;

            float route = m_Path.TotalLength;
            float race = RaceLength;
            Vector3 finish = OffsetPoint(race, 0f) + Vector3.up * 1.5f;

            Handles.color = FinishColor;
            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.FreeMoveHandle(finish, HandleUtility.GetHandleSize(finish) * 0.13f, Vector3.zero,
                Handles.SphereHandleCap);
            if (!EditorGUI.EndChangeCheck())
                return;

            float distance = m_Path.FindClosestDistance(moved);
            if (m_Layout.ClosedLoop)
            {
                int completedLaps = Mathf.Max(0, Mathf.CeilToInt(race / route) - 1);
                if (distance < 10f || distance > route - 10f)
                    distance = route;
                distance += completedLaps * route;
            }
            else
            {
                distance = Mathf.Round(distance * 2f) * 0.5f;
            }

            SetRaceLength(distance);
        }

        private void DrawStartHandles(Vector3 start)
        {
            float heading = m_Layout.StartHeading;

            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.PositionHandle(start, Quaternion.Euler(0f, heading, 0f));
            if (EditorGUI.EndChangeCheck())
            {
                m_Serialized.Update();
                m_Serialized.FindProperty("m_StartPosition").vector3Value = Snap(moved, 0.5f);
                m_Serialized.ApplyModifiedProperties();
                OnLayoutChanged();
            }

            Handles.color = StartColor;
            EditorGUI.BeginChangeCheck();
            Quaternion rotated = Handles.Disc(Quaternion.Euler(0f, heading, 0f), start, Vector3.up,
                HandleUtility.GetHandleSize(start) * 1.3f, false, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                m_Serialized.Update();
                m_Serialized.FindProperty("m_StartHeading").floatValue = Mathf.Round(rotated.eulerAngles.y);
                m_Serialized.ApplyModifiedProperties();
                OnLayoutChanged();
            }

            Handles.Label(start + Vector3.up * 3f, $"start  heading {heading:0}°");
        }

        private void DrawSelectedSegmentOutline(int index, TrackSegment segment)
        {
            if (m_Path == null)
                return;

            float from = m_Layout.GetSegmentStartDistance(index);
            DrawBand(from, from + segment.PathLength, 0f, SelectedColor, 6f);
            DrawBand(from, from + segment.PathLength, -RoadHalfWidth, SelectedColor, 2f);
            DrawBand(from, from + segment.PathLength, RoadHalfWidth, SelectedColor, 2f);
        }

        private void DrawSegmentHandles(int index, TrackSegment segment, Vector3 start, float heading, Vector3 end)
        {
            float size = HandleUtility.GetHandleSize(end);

            Handles.color = RiseColor;
            EditorGUI.BeginChangeCheck();
            Vector3 lifted = Handles.Slider(end, Vector3.up, size * 0.7f, Handles.ArrowHandleCap, 0f);
            if (EditorGUI.EndChangeCheck())
                SetSegmentFloat(index, "m_Rise", Mathf.Round((lifted.y - start.y) * 10f) * 0.1f);

            if (!segment.IsTurn)
            {
                Vector3 direction = TrackLayoutSO.HeadingToDirection(heading);
                Handles.color = SelectedColor;
                EditorGUI.BeginChangeCheck();
                Vector3 moved = Handles.Slider(end, direction, size * 0.9f, Handles.ArrowHandleCap, 0f);
                if (EditorGUI.EndChangeCheck())
                {
                    float length = Vector3.Dot(Flat(moved - start), direction);
                    SetSegmentFloat(index, "m_Length", Mathf.Max(TrackSegment.MinLength, Mathf.Round(length * 2f) * 0.5f));
                }

                Handles.Label(end + Vector3.up * 3.5f, DescribeSegment(index, segment));
                return;
            }

            float sign = TrackLayoutSO.TurnSign(segment);
            Vector3 center = TrackLayoutSO.GetTurnCenter(start, heading, segment);
            Vector3 flatEnd = new Vector3(end.x, center.y, end.z);

            Handles.color = DimColor;
            Handles.DrawDottedLine(center, start, 4f);
            Handles.DrawDottedLine(center, flatEnd, 4f);
            Handles.DrawWireDisc(center, Vector3.up, 0.6f);

            Handles.color = SelectedColor;
            EditorGUI.BeginChangeCheck();
            Vector3 dragged = Handles.FreeMoveHandle(end, size * 0.11f, Vector3.zero, Handles.SphereHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Vector3 radial = Flat(dragged - center);
                if (radial.sqrMagnitude > 0.01f)
                {
                    float phi = Mathf.Atan2(radial.x, radial.z) * Mathf.Rad2Deg;
                    float startRadial = heading - 90f * sign;
                    float swept = sign > 0f ? Mathf.Repeat(phi - startRadial, 360f) : Mathf.Repeat(startRadial - phi, 360f);
                    SetSegmentFloat(index, "m_Angle", sign * Mathf.Clamp(Mathf.Round(swept), 1f, 359f));
                }
            }

            Vector3 lever = TrackLayoutSO.HeadingToDirection(heading + 90f * sign) +
                            TrackLayoutSO.HeadingToDirection(heading - 90f * sign + segment.Angle * 0.5f);

            if (lever.sqrMagnitude > 0.01f)
            {
                Vector3 mid = start + lever * segment.Radius + Vector3.up * (segment.Rise * 0.5f);
                EditorGUI.BeginChangeCheck();
                Vector3 pulled = Handles.Slider(mid, lever.normalized, HandleUtility.GetHandleSize(mid) * 0.8f,
                    Handles.ArrowHandleCap, 0f);
                if (EditorGUI.EndChangeCheck())
                {
                    float radius = Vector3.Dot(Flat(pulled - start), lever) / lever.sqrMagnitude;
                    SetSegmentFloat(index, "m_Radius", Mathf.Max(TrackSegment.MinRadius, Mathf.Round(radius * 2f) * 0.5f));
                }
            }

            Handles.Label(end + Vector3.up * 3.5f, DescribeSegment(index, segment));
        }

        private void DrawPropGizmos(bool active)
        {
            if (m_Path == null)
                return;

            int selected = active && m_PropList != null ? m_PropList.index : -1;

            for (int i = 0; i < m_Layout.PropCount; i++)
            {
                TrackPropPlacement placement = m_Layout.GetProp(i);
                bool isSelected = i == selected;
                Handles.color = isSelected ? SelectedColor : (active ? PropColor : DimColor);
                float size = isSelected ? 1.2f : 0.7f;

                for (int k = 0; k < placement.Count; k++)
                {
                    float distance = placement.GetDistance(k);
                    foreach (float lateral in Laterals(placement, RoadHalfWidth, NearSign))
                        Handles.DrawWireCube(OffsetPoint(distance, lateral) + Vector3.up * (placement.Height + size * 0.5f),
                            Vector3.one * size);
                }

                if (!active || isSelected)
                    continue;

                Vector3 anchor = OffsetPoint(placement.Distance, FirstLateral(placement)) + Vector3.up * (placement.Height + 0.5f);
                float button = HandleUtility.GetHandleSize(anchor) * 0.08f;
                if (Handles.Button(anchor, Quaternion.identity, button, button * 1.4f, Handles.CubeHandleCap))
                {
                    m_PropList.index = i;
                    Repaint();
                }
            }
        }

        private void DrawSelectedPropHandles()
        {
            if (m_Path == null || m_PropList == null || m_PropList.index < 0 || m_PropList.index >= m_Layout.PropCount)
                return;

            int index = m_PropList.index;
            TrackPropPlacement placement = m_Layout.GetProp(index);
            float lateral = FirstLateral(placement);

            m_Path.Evaluate(placement.Distance, out _, out Vector3 forward);
            Vector3 right = RacePath.RightOf(forward);
            Vector3 lift = Vector3.up * placement.Height;
            Vector3 position = OffsetPoint(placement.Distance, lateral) + lift;
            float size = HandleUtility.GetHandleSize(position);
            Handles.Label(position + Vector3.up * 3f,
                $"{placement.Prop} ×{placement.Count} @ {placement.Distance:0.0} m, up {placement.Height:0.##} m");

            Handles.color = SelectedColor;
            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.FreeMoveHandle(position, size * 0.12f, Vector3.zero, Handles.SphereHandleCap);
            if (EditorGUI.EndChangeCheck())
                SetPropValue(index, "m_Distance", Mathf.Round(m_Path.FindClosestDistance(moved) * 2f) * 0.5f);

            Vector3 outward = placement.Side == ETrackSide.Center ? right : right * Mathf.Sign(lateral == 0f ? 1f : lateral);
            EditorGUI.BeginChangeCheck();
            Vector3 pushed = Handles.Slider(position, outward, size * 0.7f, Handles.ArrowHandleCap, 0f);
            if (EditorGUI.EndChangeCheck())
            {
                float offset = placement.EdgeOffset + Vector3.Dot(pushed - position, outward);
                SetPropValue(index, "m_EdgeOffset", Mathf.Round(offset * 4f) * 0.25f);
            }

            Handles.color = RiseColor;
            EditorGUI.BeginChangeCheck();
            Vector3 raised = Handles.Slider(position, Vector3.up, size * 0.7f, Handles.ArrowHandleCap, 0f);
            if (EditorGUI.EndChangeCheck())
                SetPropValue(index, "m_Height", Mathf.Round((placement.Height + raised.y - position.y) * 20f) * 0.05f);

            if (placement.Count <= 1)
                return;

            Vector3 last = OffsetPoint(placement.GetDistance(placement.Count - 1), lateral) + lift;
            Handles.color = PropColor;
            EditorGUI.BeginChangeCheck();
            Vector3 stretched = Handles.FreeMoveHandle(last, HandleUtility.GetHandleSize(last) * 0.09f, Vector3.zero,
                Handles.CubeHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                float span = m_Path.FindClosestDistance(stretched) - placement.Distance;
                int count = Mathf.Max(1, Mathf.RoundToInt(span / placement.Spacing) + 1);
                m_Serialized.Update();
                PropsProperty.GetArrayElementAtIndex(index).FindPropertyRelative("m_Count").intValue = count;
                m_Serialized.ApplyModifiedProperties();
                OnLayoutChanged();
            }
        }

        private void SetPropValue(int index, string field, float value)
        {
            m_Serialized.Update();
            PropsProperty.GetArrayElementAtIndex(index).FindPropertyRelative(field).floatValue = value;
            m_Serialized.ApplyModifiedProperties();
            OnLayoutChanged();
        }

        private void DeleteArrayElement(SerializedProperty array, UnityEditorInternal.ReorderableList list)
        {
            int index = list.index;
            m_Serialized.Update();
            if (index < 0 || index >= array.arraySize)
                return;

            array.DeleteArrayElementAtIndex(index);
            m_Serialized.ApplyModifiedProperties();
            list.index = Mathf.Min(index, array.arraySize - 1);
            OnLayoutChanged();
        }

        private float FirstLateral(TrackPropPlacement placement)
        {
            foreach (float value in Laterals(placement, RoadHalfWidth, NearSign))
                return value;

            return 0f;
        }

        private float CurrentViewDistance()
        {
            SceneView view = SceneView.lastActiveSceneView;
            if (view == null || m_Path == null)
                return 0f;

            return Mathf.Round(m_Path.FindClosestDistance(view.pivot));
        }

        private float RoadHalfWidth => m_Config != null ? m_Config.RoadWidthMeters * 0.5f : 12f;
        private float NearSign => m_Config != null ? m_Config.ViewSideSign : 1f;

        private void DrawBand(float from, float to, float lateral, Color color, float width)
        {
            m_Line.Clear();
            int steps = Mathf.Max(1, Mathf.CeilToInt((to - from) / GizmoStep));
            for (int i = 0; i <= steps; i++)
                m_Line.Add(OffsetPoint(Mathf.Lerp(from, to, i / (float)steps), lateral));

            Handles.color = color;
            Handles.DrawAAPolyLine(width, m_Line.ToArray());
        }

        private void DrawCrossLine(float distance, float half, Color color, string label)
        {
            Handles.color = color;
            Handles.DrawAAPolyLine(5f, OffsetPoint(distance, -half), OffsetPoint(distance, half));
            Handles.Label(OffsetPoint(distance, 0f) + Vector3.up * 3f, label);
        }

        private Vector3 OffsetPoint(float distance, float lateral)
        {
            m_Path.Evaluate(distance, out Vector3 position, out Vector3 forward);
            return position + RacePath.RightOf(forward) * lateral;
        }

        private static Vector3 Flat(Vector3 value)
        {
            value.y = 0f;
            return value;
        }

        private static Vector3 Snap(Vector3 value, float step)
        {
            return new Vector3(Mathf.Round(value.x / step) * step, Mathf.Round(value.y / step) * step,
                Mathf.Round(value.z / step) * step);
        }

        private static string DescribeSegment(int index, TrackSegment segment)
        {
            if (!segment.IsTurn)
                return $"#{index} straight {segment.Length:0.#} m" + (segment.Rise != 0f ? $"  rise {segment.Rise:0.#}" : "");

            string direction = segment.Angle < 0f ? "left" : "right";
            return $"#{index} {direction} {Mathf.Abs(segment.Angle):0}° R{segment.Radius:0.#}" +
                   (segment.Rise != 0f ? $"  rise {segment.Rise:0.#}" : "");
        }
    }
}
