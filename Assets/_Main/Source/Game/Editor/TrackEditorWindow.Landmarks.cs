using UnityEditor;
using UnityEngine;

namespace MatchRacers.Editor
{
    public sealed partial class TrackEditorWindow
    {
        private static readonly Color LandmarkColor = new Color(0.75f, 0.5f, 1f, 1f);

        [Tooltip("Tıklayarak yerleştirilecek prefab. Project penceresinden sürüklenir.")]
        [SerializeField] private GameObject m_Brush;
        [Tooltip("Yerleştirilecek objenin dünya konumuna mı yoksa yola mı bağlanacağı.")]
        [SerializeField] private ELandmarkAnchor m_BrushAnchor = ELandmarkAnchor.World;
        [Tooltip("Yerleştirme kipi açık mı. Açıkken sahneye her tıklama yeni bir obje koyar.")]
        [SerializeField] private bool m_Placing;

        private void DrawLandmarkTools()
        {
            EditorGUILayout.LabelField("Unique objects", EditorStyles.boldLabel);
            m_Brush = (GameObject)EditorGUILayout.ObjectField("Prefab", m_Brush, typeof(GameObject), false);
            m_BrushAnchor = (ELandmarkAnchor)EditorGUILayout.EnumPopup("New Anchor", m_BrushAnchor);
            EditorGUILayout.HelpBox(
                "World: stays where you put it. Track: keeps its distance and offset from the road, so it follows when you reshape the route.\n" +
                "Drag prefabs from the Project window into the Scene, or press Place and click. W / E / R switch move, rotate and scale.",
                MessageType.None);

            bool hasSelection = HasLandmarkSelection;
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(m_Brush == null))
            {
                m_Placing = GUILayout.Toggle(m_Placing, m_Placing ? "Placing… (Esc)" : "Place", "Button");
            }

            using (new EditorGUI.DisabledScope(!hasSelection))
            {
                if (GUILayout.Button("Duplicate"))
                    DuplicateSelectedLandmark();
                if (GUILayout.Button("Stack Copy"))
                    StackSelectedLandmark();
                if (GUILayout.Button("Switch Anchor"))
                    SwitchSelectedAnchor();
                if (GUILayout.Button("Frame"))
                    FrameSelectedLandmark();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawObjectToolbar()
        {
            m_Brush = (GameObject)EditorGUILayout.ObjectField(m_Brush, typeof(GameObject), false);

            GUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(m_Brush == null))
            {
                m_Placing = GUILayout.Toggle(m_Placing, m_Placing ? "Placing… (Esc)" : "Place", "Button");
            }

            m_BrushAnchor = (ELandmarkAnchor)EditorGUILayout.EnumPopup(m_BrushAnchor, GUILayout.Width(70f));
            GUILayout.EndHorizontal();

            bool hasSelection = HasLandmarkSelection;
            GUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(!hasSelection))
            {
                if (GUILayout.Button("Duplicate"))
                    DuplicateSelectedLandmark();
                if (GUILayout.Button("Stack"))
                    StackSelectedLandmark();
                if (GUILayout.Button("Delete"))
                    DeleteArrayElement(LandmarksProperty, m_LandmarkList);
                if (GUILayout.Button("Deselect"))
                {
                    m_LandmarkList.index = -1;
                    Repaint();
                }
            }

            GUILayout.EndHorizontal();
            GUILayout.Label("Drop prefabs into the Scene. Click a purple box to select; W / E / R to move, rotate, scale.",
                EditorStyles.wordWrappedMiniLabel);
        }

        private void DrawLandmark(Rect rect, int index, bool isActive, bool isFocused)
        {
            SerializedProperty element = m_LandmarkList.serializedProperty.GetArrayElementAtIndex(index);
            SerializedProperty anchor = element.FindPropertyRelative("m_Anchor");
            float line = EditorGUIUtility.singleLineHeight;
            Rect row = new Rect(rect.x, rect.y + 3f, rect.width, line);

            float half = row.width * 0.5f;
            DrawField(new Rect(row.x, row.y, half - 4f, line), element.FindPropertyRelative("m_Label"), "Name");
            EditorGUI.PropertyField(new Rect(row.x + half, row.y, half, line), element.FindPropertyRelative("m_Prefab"), GUIContent.none);

            row.y += line + 2f;
            EditorGUI.PropertyField(new Rect(row.x, row.y, 70f, line), anchor, GUIContent.none);
            Rect rest = new Rect(row.x + 74f, row.y, row.width - 74f, line);
            float third = rest.width / 3f;

            if (anchor.enumValueIndex == (int)ELandmarkAnchor.World)
            {
                SerializedProperty position = element.FindPropertyRelative("m_Position");
                DrawField(new Rect(rest.x, rest.y, third - 4f, line), position.FindPropertyRelative("x"), "X");
                DrawField(new Rect(rest.x + third, rest.y, third - 4f, line), position.FindPropertyRelative("y"), "Up");
                DrawField(new Rect(rest.x + third * 2f, rest.y, third, line), position.FindPropertyRelative("z"), "Z");
            }
            else
            {
                DrawField(new Rect(rest.x, rest.y, third - 4f, line), element.FindPropertyRelative("m_Distance"), "At");
                DrawField(new Rect(rest.x + third, rest.y, third - 4f, line), element.FindPropertyRelative("m_Lateral"), "Side");
                DrawField(new Rect(rest.x + third * 2f, rest.y, third, line), element.FindPropertyRelative("m_Height"), "Up");
            }

            row.y += line + 2f;
            DrawField(row, element.FindPropertyRelative("m_Euler"), "Rot");
            row.y += line + 2f;
            DrawField(row, element.FindPropertyRelative("m_Scale"), "Scale");
        }

        private bool HasLandmarkSelection =>
            m_LandmarkList != null && m_LandmarkList.index >= 0 && m_LandmarkList.index < m_Layout.LandmarkCount;

        private void HandleObjectInput()
        {
            Event current = Event.current;

            if (current.type == EventType.DragUpdated || current.type == EventType.DragPerform)
            {
                GameObject prefab = DraggedPrefab();
                if (prefab == null || m_ToolbarRect.Contains(current.mousePosition))
                    return;

                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                if (current.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    m_Brush = prefab;
                    if (TryGroundPoint(current.mousePosition, out Vector3 point))
                        AddLandmark(prefab, point);
                }

                current.Use();
                return;
            }

            if (!m_Placing || m_Brush == null)
                return;

            int control = GUIUtility.GetControlID(FocusType.Passive);
            if (current.type == EventType.Layout)
                HandleUtility.AddDefaultControl(control);

            if (current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape)
            {
                m_Placing = false;
                current.Use();
                Repaint();
                return;
            }

            if (current.type != EventType.MouseDown || current.button != 0 || current.alt ||
                m_ToolbarRect.Contains(current.mousePosition))
                return;

            if (TryGroundPoint(current.mousePosition, out Vector3 placed))
                AddLandmark(m_Brush, placed);

            current.Use();
        }

        private void DrawLandmarkGizmos(bool active)
        {
            int selected = active && m_LandmarkList != null ? m_LandmarkList.index : -1;

            for (int i = 0; i < m_Layout.LandmarkCount; i++)
            {
                TrackLandmark landmark = m_Layout.GetLandmark(i);
                landmark.Resolve(m_Path, out Vector3 position, out _);
                float size = HandleUtility.GetHandleSize(position) * 0.1f;

                Handles.color = i == selected ? SelectedColor : (active ? LandmarkColor : DimColor);
                Handles.DrawLine(position, position + Vector3.up * 4f);

                if (active)
                    Handles.Label(position + Vector3.up * 4.5f,
                        landmark.DisplayName + (landmark.Anchor == ELandmarkAnchor.Track ? " (track)" : ""));

                if (!active || i == selected)
                    continue;

                if (Handles.Button(position + Vector3.up * 4f, Quaternion.identity, size, size * 1.4f, Handles.CubeHandleCap))
                {
                    m_LandmarkList.index = i;
                    m_Placing = false;
                    Repaint();
                }
            }
        }

        private void DrawSelectedLandmarkHandles()
        {
            if (!HasLandmarkSelection)
                return;

            int index = m_LandmarkList.index;
            TrackLandmark landmark = m_Layout.GetLandmark(index);
            landmark.Resolve(m_Path, out Vector3 position, out Quaternion rotation);
            Vector3 scale = landmark.Scale;

            EditorGUI.BeginChangeCheck();
            switch (Tools.current)
            {
                case Tool.Rotate:
                    rotation = Handles.RotationHandle(rotation, position);
                    break;
                case Tool.Scale:
                    scale = Handles.ScaleHandle(scale, position, rotation, HandleUtility.GetHandleSize(position));
                    break;
                case Tool.Transform:
                    Handles.TransformHandle(ref position, ref rotation, ref scale);
                    break;
                default:
                    position = Handles.PositionHandle(position,
                        Tools.pivotRotation == PivotRotation.Local ? rotation : Quaternion.identity);
                    break;
            }

            if (EditorGUI.EndChangeCheck())
                WriteLandmark(index, Rebuild(landmark, landmark.Anchor, position, rotation, scale));
        }

        private void AddLandmark(GameObject prefab, Vector3 point)
        {
            Quaternion rotation = Quaternion.identity;
            if (m_Path != null)
            {
                m_Path.Evaluate(m_Path.FindClosestDistance(point), out _, out Vector3 forward);
                rotation = Quaternion.LookRotation(RacePath.Flatten(forward), Vector3.up);
            }

            TrackLandmark landmark = m_BrushAnchor == ELandmarkAnchor.Track && m_Path != null
                ? TrackLandmark.OnTrack(string.Empty, prefab, m_Path, point, rotation, Vector3.one)
                : TrackLandmark.AtWorld(string.Empty, prefab, point, rotation, Vector3.one);

            m_Serialized.Update();
            SerializedProperty array = LandmarksProperty;
            int index = array.arraySize;
            array.arraySize = index + 1;
            WriteLandmark(array.GetArrayElementAtIndex(index), landmark);
            m_Serialized.ApplyModifiedProperties();

            m_LandmarkList.index = index;
            OnLayoutChanged();
        }

        private void AddLandmarkAtView(GameObject prefab)
        {
            SceneView view = SceneView.lastActiveSceneView;
            Vector3 point = view != null ? view.pivot : m_Layout.StartPosition;
            point.y = m_Layout.StartPosition.y;
            AddLandmark(prefab, point);
        }

        private void DuplicateSelectedLandmark()
        {
            if (!HasLandmarkSelection)
                return;

            TrackLandmark source = m_Layout.GetLandmark(m_LandmarkList.index);
            source.Resolve(m_Path, out Vector3 position, out Quaternion rotation);
            AppendLandmark(Rebuild(source, source.Anchor, position + rotation * Vector3.right * 10f, rotation, source.Scale));
        }

        private void StackSelectedLandmark()
        {
            if (!HasLandmarkSelection)
                return;

            TrackLandmark source = m_Layout.GetLandmark(m_LandmarkList.index);
            source.Resolve(m_Path, out Vector3 position, out Quaternion rotation);
            Vector3 prefabScale = source.Prefab != null ? source.Prefab.transform.localScale : Vector3.one;
            float height = MeasureHeight(source.Prefab, rotation, Vector3.Scale(prefabScale, source.Scale));
            AppendLandmark(Rebuild(source, source.Anchor, position + Vector3.up * height, rotation, source.Scale));
        }

        private void AppendLandmark(TrackLandmark copy)
        {
            m_Serialized.Update();
            SerializedProperty array = LandmarksProperty;
            int index = array.arraySize;
            array.arraySize = index + 1;
            WriteLandmark(array.GetArrayElementAtIndex(index), copy);
            m_Serialized.ApplyModifiedProperties();

            m_LandmarkList.index = index;
            OnLayoutChanged();
        }

        private void SwitchSelectedAnchor()
        {
            if (!HasLandmarkSelection || m_Path == null)
                return;

            TrackLandmark landmark = m_Layout.GetLandmark(m_LandmarkList.index);
            landmark.Resolve(m_Path, out Vector3 position, out Quaternion rotation);
            ELandmarkAnchor next = landmark.Anchor == ELandmarkAnchor.World ? ELandmarkAnchor.Track : ELandmarkAnchor.World;
            WriteLandmark(m_LandmarkList.index, Rebuild(landmark, next, position, rotation, landmark.Scale));
        }

        private void FrameSelectedLandmark()
        {
            SceneView view = SceneView.lastActiveSceneView;
            if (view == null || !HasLandmarkSelection)
                return;

            m_Layout.GetLandmark(m_LandmarkList.index).Resolve(m_Path, out Vector3 position, out _);
            view.Frame(new Bounds(position, Vector3.one * 30f), false);
        }

        private TrackLandmark Rebuild(TrackLandmark source, ELandmarkAnchor anchor, Vector3 position, Quaternion rotation,
            Vector3 scale)
        {
            return anchor == ELandmarkAnchor.Track && m_Path != null
                ? TrackLandmark.OnTrack(source.Label, source.Prefab, m_Path, position, rotation, scale)
                : TrackLandmark.AtWorld(source.Label, source.Prefab, position, rotation, scale);
        }

        private void WriteLandmark(int index, TrackLandmark landmark)
        {
            m_Serialized.Update();
            WriteLandmark(LandmarksProperty.GetArrayElementAtIndex(index), landmark);
            m_Serialized.ApplyModifiedProperties();
            OnLayoutChanged();
        }

        private static void WriteLandmark(SerializedProperty element, TrackLandmark landmark)
        {
            element.FindPropertyRelative("m_Label").stringValue = landmark.Label ?? string.Empty;
            element.FindPropertyRelative("m_Prefab").objectReferenceValue = landmark.Prefab;
            element.FindPropertyRelative("m_Anchor").intValue = (int)landmark.Anchor;
            element.FindPropertyRelative("m_Position").vector3Value = landmark.Position;
            element.FindPropertyRelative("m_Distance").floatValue = landmark.Distance;
            element.FindPropertyRelative("m_Lateral").floatValue = landmark.Lateral;
            element.FindPropertyRelative("m_Height").floatValue = landmark.Height;
            element.FindPropertyRelative("m_Euler").vector3Value = landmark.Euler;
            element.FindPropertyRelative("m_Scale").vector3Value = landmark.Scale;
        }

        private bool TryGroundPoint(Vector2 mousePosition, out Vector3 point)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
            Plane ground = new Plane(Vector3.up, new Vector3(0f, m_Layout.StartPosition.y, 0f));

            if (ground.Raycast(ray, out float enter))
            {
                point = ray.GetPoint(enter);
                return true;
            }

            point = Vector3.zero;
            return false;
        }

        private static GameObject DraggedPrefab()
        {
            Object[] dragged = DragAndDrop.objectReferences;
            for (int i = 0; i < dragged.Length; i++)
            {
                if (dragged[i] is GameObject gameObject && EditorUtility.IsPersistent(gameObject))
                    return gameObject;
            }

            return null;
        }
    }
}
