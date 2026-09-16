using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace MatchRacers.Editor
{
    public sealed partial class TrackEditorWindow : EditorWindow
    {
        private const string ConfigPath = "Assets/_Main/SO/Race/RaceConfig.asset";
        private const string CatalogPath = "Assets/_Main/SO/Race/TrackPropCatalog.asset";
        private const string TrackCatalogPath = "Assets/_Main/SO/Race/TrackCatalog.asset";
        private const string TrackParentFolder = "Assets/_Main/SO/Race";
        private const string TrackFolderName = "Tracks";
        private const string PreviewName = "TrackPreview";

        private static readonly string[] ModeNames = { "Road", "Props", "Objects" };

        [Tooltip("Düzenlenen pist varlığı. Pencere bütün değişiklikleri bu asset'e yazar.")]
        [SerializeField] private TrackLayoutSO m_Layout;
        [Tooltip("Yol genişliği ve şerit sayısı gibi ölçüleri okumak için kullanılan yarış ayarı.")]
        [SerializeField] private RaceConfigSO m_Config;
        [Tooltip("Aktif düzenleme kipi: yol, dekor ya da tekil objeler. Sahnedeki tutamaçlar buna göre değişir.")]
        [SerializeField] private ETrackEditMode m_Mode = ETrackEditMode.Road;
        [Tooltip("Hızlı ekle düğmesinin yeni düz parça için kullanacağı uzunluk (metre).")]
        [SerializeField] private float m_DefaultStraight = 100f;
        [Tooltip("Hızlı ekle düğmesinin yeni viraj için kullanacağı yarıçap (metre).")]
        [SerializeField] private float m_DefaultRadius = 50f;
        [Tooltip("Açıkken her değişiklikte pist sahnede yeniden kurulur. Çok dekorlu pistlerde kapatmak düzenlemeyi hızlandırır.")]
        [SerializeField] private bool m_LivePreview = true;
        [Tooltip("Pencerede pist ayarları bölümünün açık olup olmadığı.")]
        [SerializeField] private bool m_ShowSettings;
        [Tooltip("Yol boyunca doldur komutunun kullanacağı dekor türü.")]
        [SerializeField] private ETrackProp m_FillProp = ETrackProp.Barrier;
        [Tooltip("Doldur komutunun dekoru yolun hangi tarafına koyacağı.")]
        [SerializeField] private ETrackSide m_FillSide = ETrackSide.Far;
        [Tooltip("Doldur komutunda iki dekor arasındaki mesafe (metre).")]
        [SerializeField] private float m_FillSpacing = 6f;
        [Tooltip("Doldur komutunda yol kenarından kayma (metre).")]
        [SerializeField] private float m_FillOffset = 1.5f;
        [Tooltip("Finish At Start komutunun kaç tur sonra bitiş çizgisi koyacağı. Kapalı pistlerde yarış uzunluğunu tur sayısından hesaplar.")]
        [SerializeField] private int m_Laps = 1;

        private SerializedObject m_Serialized;
        private ReorderableList m_SegmentList;
        private ReorderableList m_PropList;
        private ReorderableList m_LandmarkList;
        private RacePath m_Path;
        private TrackBuilder m_Preview;
        private Vector2 m_Scroll;

        [MenuItem("MatchRacers/Track Editor")]
        public static void Open()
        {
            GetWindow<TrackEditorWindow>("Track Editor").minSize = new Vector2(440f, 560f);
        }

        private void OnEnable()
        {
            if (m_Config == null)
                m_Config = AssetDatabase.LoadAssetAtPath<RaceConfigSO>(ConfigPath);

            if (m_Layout == null && m_Config != null)
                m_Layout = m_Config.TrackLayout;

            DestroyStalePreviews();
            SceneView.duringSceneGui += OnSceneGUI;
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            Bind();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.delayCall -= FlushPreview;
            Tools.hidden = false;
            DestroyPreview();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Track & map editor", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Road: pick a segment in the Scene and drag its handles. Arrow = length or radius, sphere = turn angle, green arrow = rise.\n" +
                "Props: repeated track-side decor. Objects: unique map pieces such as buildings; drag prefabs into the Scene or use Place.\n" +
                "Angle < 0 turns left, > 0 turns right. Far = opposite the camera, Near = camera side.",
                MessageType.None);

            EditorGUI.BeginChangeCheck();
            m_Layout = (TrackLayoutSO)EditorGUILayout.ObjectField("Layout", m_Layout, typeof(TrackLayoutSO), false);
            m_Config = (RaceConfigSO)EditorGUILayout.ObjectField("Race Config", m_Config, typeof(RaceConfigSO), false);
            if (EditorGUI.EndChangeCheck())
                Bind();

            if (m_Layout == null)
            {
                EditorGUILayout.Space(6f);
                if (GUILayout.Button("Create New Layout", GUILayout.Height(28f)))
                    CreateLayout();
                return;
            }

            if (m_Serialized == null)
                Bind();

            SetMode((ETrackEditMode)GUILayout.Toolbar((int)m_Mode, ModeNames, GUILayout.Height(24f)));

            m_Serialized.Update();
            EditorGUILayout.PropertyField(m_Serialized.FindProperty("m_DisplayName"), new GUIContent("Track Name"));
            m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);

            DrawSettings();
            EditorGUILayout.Space(6f);

            switch (m_Mode)
            {
                case ETrackEditMode.Road:
                    DrawQuickAdd();
                    m_SegmentList.DoLayoutList();
                    break;
                case ETrackEditMode.Props:
                    DrawPropFill();
                    m_PropList.DoLayoutList();
                    break;
                default:
                    DrawLandmarkTools();
                    m_LandmarkList.DoLayoutList();
                    break;
            }

            if (m_Serialized.ApplyModifiedProperties())
                OnLayoutChanged();

            EditorGUILayout.Space(6f);
            DrawStatus();
            DrawActions();

            EditorGUILayout.EndScrollView();
        }

        private void SetMode(ETrackEditMode mode)
        {
            if (mode == m_Mode)
                return;

            m_Mode = mode;
            m_Placing = false;
            m_StartSelected = false;
            Tools.hidden = false;
            SceneView.RepaintAll();
            Repaint();
        }

        private void Bind()
        {
            m_Serialized = m_Layout != null ? new SerializedObject(m_Layout) : null;
            m_SegmentList = null;
            m_PropList = null;
            m_LandmarkList = null;

            if (m_Serialized != null)
            {
                m_SegmentList = new ReorderableList(m_Serialized, m_Serialized.FindProperty("m_Segments"), true, true, true, true)
                {
                    drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Segments"),
                    elementHeightCallback = _ => EditorGUIUtility.singleLineHeight * 2f + 8f,
                    drawElementCallback = DrawSegment,
                    onSelectCallback = _ => SelectSegment(m_SegmentList.index),
                    onAddCallback = _ => AddSegment(TrackSegment.Straight(m_DefaultStraight))
                };

                m_PropList = new ReorderableList(m_Serialized, m_Serialized.FindProperty("m_Props"), true, true, true, true)
                {
                    drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Props"),
                    elementHeightCallback = _ => EditorGUIUtility.singleLineHeight * 3f + 10f,
                    drawElementCallback = DrawProp,
                    onSelectCallback = _ => SceneView.RepaintAll(),
                    onAddCallback = _ => AddProp(TrackPropPlacement.Create(m_FillProp, m_FillSide, 0f, m_FillOffset, 1, m_FillSpacing))
                };

                m_LandmarkList = new ReorderableList(m_Serialized, m_Serialized.FindProperty("m_Landmarks"), true, true, true, true)
                {
                    drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Objects"),
                    elementHeightCallback = _ => EditorGUIUtility.singleLineHeight * 4f + 12f,
                    drawElementCallback = DrawLandmark,
                    onSelectCallback = _ => SceneView.RepaintAll(),
                    onAddCallback = _ => AddLandmarkAtView(m_Brush)
                };
            }

            OnLayoutChanged();
        }

        private void DrawSettings()
        {
            m_ShowSettings = EditorGUILayout.Foldout(m_ShowSettings, "Layout settings", true);
            if (!m_ShowSettings)
                return;

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(m_Serialized.FindProperty("m_Environment"));
            EditorGUILayout.PropertyField(m_Serialized.FindProperty("m_LobbyMusic"));
            EditorGUILayout.PropertyField(m_Serialized.FindProperty("m_LobbyMusicVolume"));
            EditorGUILayout.PropertyField(m_Serialized.FindProperty("m_RaceMusic"));
            EditorGUILayout.PropertyField(m_Serialized.FindProperty("m_RaceMusicVolume"));
            EditorGUILayout.PropertyField(m_Serialized.FindProperty("m_StartPosition"));
            EditorGUILayout.PropertyField(m_Serialized.FindProperty("m_StartHeading"));
            EditorGUILayout.PropertyField(m_Serialized.FindProperty("m_SampleSpacing"));
            EditorGUILayout.PropertyField(m_Serialized.FindProperty("m_ClosedLoop"));
            EditorGUILayout.PropertyField(m_Serialized.FindProperty("m_PropCatalog"));
            EditorGUILayout.PropertyField(m_Serialized.FindProperty("m_RoadMaterial"));
            EditorGUILayout.PropertyField(m_Serialized.FindProperty("m_StripeMaterial"));
            EditorGUILayout.PropertyField(m_Serialized.FindProperty("m_DistanceMarkers"));
            EditorGUILayout.PropertyField(m_Serialized.FindProperty("m_FinishGate"));
            EditorGUILayout.PropertyField(m_Serialized.FindProperty("m_Ground"));
            EditorGUILayout.PropertyField(m_Serialized.FindProperty("m_GroundMaterial"));
            EditorGUILayout.PropertyField(m_Serialized.FindProperty("m_GroundMargin"));
            EditorGUILayout.PropertyField(m_Serialized.FindProperty("m_GroundHeight"));
            EditorGUI.indentLevel--;
        }

        private void DrawQuickAdd()
        {
            EditorGUILayout.BeginHorizontal();
            m_DefaultStraight = Mathf.Max(TrackSegment.MinLength, EditorGUILayout.FloatField("Straight", m_DefaultStraight));
            m_DefaultRadius = Mathf.Max(TrackSegment.MinRadius, EditorGUILayout.FloatField("Radius", m_DefaultRadius));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ Straight"))
                AddSegment(TrackSegment.Straight(m_DefaultStraight));
            if (GUILayout.Button("+ Left 45"))
                AddSegment(TrackSegment.Turn(-45f, m_DefaultRadius));
            if (GUILayout.Button("+ Left 90"))
                AddSegment(TrackSegment.Turn(-90f, m_DefaultRadius));
            if (GUILayout.Button("+ Right 45"))
                AddSegment(TrackSegment.Turn(45f, m_DefaultRadius));
            if (GUILayout.Button("+ Right 90"))
                AddSegment(TrackSegment.Turn(90f, m_DefaultRadius));
            EditorGUILayout.EndHorizontal();
        }

        private void DrawPropFill()
        {
            EditorGUILayout.LabelField("Prop fill", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            m_FillProp = (ETrackProp)EditorGUILayout.EnumPopup(m_FillProp);
            m_FillSide = (ETrackSide)EditorGUILayout.EnumPopup(m_FillSide);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            m_FillSpacing = Mathf.Max(0.5f, EditorGUILayout.FloatField("Spacing", m_FillSpacing));
            m_FillOffset = EditorGUILayout.FloatField("Edge Offset", m_FillOffset);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(m_Config == null))
            {
                if (GUILayout.Button("Line Whole Race"))
                    FillWholeRace();
            }

            if (GUILayout.Button("Line Turn Outsides"))
                FillTurnOutsides();
            EditorGUILayout.EndHorizontal();

            using (new EditorGUI.DisabledScope(!HasPropSelection))
            {
                if (GUILayout.Button("Stack Copy On Top"))
                    StackSelectedProp();
            }
        }

        private bool HasPropSelection =>
            m_PropList != null && m_PropList.index >= 0 && m_PropList.index < m_Layout.PropCount;

        private void StackSelectedProp()
        {
            if (!HasPropSelection)
                return;

            int index = m_PropList.index;
            TrackPropPlacement placement = m_Layout.GetProp(index);
            float height = 1f;

            if (m_Layout.PropCatalog != null && m_Layout.PropCatalog.TryGet(placement.Prop, out TrackPropEntry entry))
                height = MeasureHeight(entry.Prefab, Quaternion.Euler(entry.LocalEuler),
                    entry.Prefab.transform.localScale * (entry.LocalScale * placement.Scale));

            m_Serialized.Update();
            SerializedProperty array = PropsProperty;
            array.InsertArrayElementAtIndex(index);
            array.GetArrayElementAtIndex(index + 1).FindPropertyRelative("m_Height").floatValue = placement.Height + height;
            m_Serialized.ApplyModifiedProperties();

            m_PropList.index = index + 1;
            OnLayoutChanged();
        }

        private static float MeasureHeight(GameObject prefab, Quaternion rotation, Vector3 scale)
        {
            if (prefab == null)
                return 1f;

            GameObject probe = Instantiate(prefab);
            probe.hideFlags = HideFlags.HideAndDontSave;
            probe.transform.SetPositionAndRotation(Vector3.zero, rotation);
            probe.transform.localScale = scale;

            Renderer[] renderers = probe.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = default;
            bool found = false;

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] is ParticleSystemRenderer)
                    continue;

                if (!found)
                {
                    bounds = renderers[i].bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
            }

            DestroyImmediate(probe);
            return found && bounds.size.y > 0.01f ? bounds.size.y : 1f;
        }

        private void DrawStatus()
        {
            if (m_Path == null)
            {
                EditorGUILayout.HelpBox("Add at least one segment.", MessageType.Info);
                return;
            }

            if (m_Config == null)
            {
                EditorGUILayout.HelpBox("Assign a Race Config to validate lengths and preview the road.", MessageType.Warning);
                return;
            }

            float route = m_Path.TotalLength;
            float race = RaceLength;
            float runout = m_Config.RunoutMeters;
            float half = m_Config.RoadWidthMeters * 0.5f;
            TrackCatalogSO catalog = ResolveTrackCatalog(false);
            bool listed = catalog != null && catalog.IndexOf(m_Layout) >= 0;
            bool isDefault = m_Config.TrackLayout == m_Layout;

            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Route {route:0.0} m    Race {race:0.#} m    Runout {runout:0} m");
            EditorGUILayout.LabelField($"In track list: {(listed ? "yes" : "no")}    Default track: {(isDefault ? "yes" : "no")}");
            DrawFinishControls(route, race);

            if (m_Layout.ClosedLoop)
            {
                float gap = m_Layout.GetLoopGap();
                float headingError = m_Layout.GetLoopHeadingError();
                if (gap > 0.5f || headingError > 1f)
                    EditorGUILayout.HelpBox(
                        $"Closed loop does not meet the start: {gap:0.0} m apart, {headingError:0}° off. Press Close Loop to replace the last segment with an exact connection.",
                        MessageType.Error);
            }
            else if (route < race + runout)
                EditorGUILayout.HelpBox(
                    $"The route ends {race + runout - route:0} m before the end of the runout. The road continues straight past the last segment; use Extend To Finish to make that explicit.",
                    MessageType.Warning);

            for (int i = 0; i < m_Layout.SegmentCount; i++)
            {
                TrackSegment segment = m_Layout.GetSegment(i);
                if (!segment.IsTurn)
                    continue;

                if (segment.Radius <= half + 1f)
                {
                    EditorGUILayout.HelpBox(
                        $"Segment #{i}: radius {segment.Radius:0} m is tighter than half the road ({half:0.0} m); the inner edge folds over itself.",
                        MessageType.Error);
                    continue;
                }

                bool towardCamera = Mathf.Sign(segment.Angle) == Mathf.Sign(m_Config.ViewSideSign);
                if (towardCamera && segment.Radius < m_Config.CameraSideDistance + half)
                    EditorGUILayout.HelpBox(
                        $"Segment #{i} bends toward the camera tighter than the camera distance; the camera will swing across the turn centre.",
                        MessageType.Warning);
            }
        }

        private void DrawActions()
        {
            EditorGUILayout.Space(4f);
            TrackCatalogSO catalog = ResolveTrackCatalog(false);
            bool listed = catalog != null && catalog.IndexOf(m_Layout) >= 0;

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(listed ? "Update In Track List" : "Submit To Track List", GUILayout.Height(26f)))
                SubmitToTrackList();

            using (new EditorGUI.DisabledScope(!listed))
            {
                if (GUILayout.Button("Remove From List", GUILayout.Height(26f)))
                    RemoveFromTrackList();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(m_Config == null))
            {
                if (GUILayout.Button("Set As Default"))
                    AssignToConfig(m_Layout);
                if (GUILayout.Button("Back To Straight"))
                    AssignToConfig(null);
                if (GUILayout.Button("Extend To Finish"))
                    ExtendToFinish();
            }

            if (GUILayout.Button("Frame In Scene"))
                FrameInScene();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            m_LivePreview = EditorGUILayout.ToggleLeft("Live scene preview", m_LivePreview);
            if (EditorGUI.EndChangeCheck())
                SchedulePreview();

            if (GUILayout.Button("Rebuild Preview"))
                SchedulePreview();
            if (GUILayout.Button("Clear Preview"))
                DestroyPreview();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSegment(Rect rect, int index, bool isActive, bool isFocused)
        {
            SerializedProperty element = m_SegmentList.serializedProperty.GetArrayElementAtIndex(index);
            SerializedProperty type = element.FindPropertyRelative("m_Type");
            float line = EditorGUIUtility.singleLineHeight;
            float start = m_Layout.GetSegmentStartDistance(index);
            float end = index < m_Layout.SegmentCount ? start + m_Layout.GetSegment(index).PathLength : start;

            Rect row = new Rect(rect.x, rect.y + 3f, rect.width, line);
            EditorGUI.LabelField(new Rect(row.x, row.y, 32f, line), "#" + index);
            EditorGUI.PropertyField(new Rect(row.x + 34f, row.y, 90f, line), type, GUIContent.none);
            EditorGUI.LabelField(new Rect(row.x + 130f, row.y, row.width - 130f, line), $"{start:0} m  →  {end:0} m",
                EditorStyles.miniLabel);

            row.y += line + 3f;

            if (type.enumValueIndex == (int)ETrackSegmentType.Straight)
            {
                float halfWidth = row.width * 0.5f;
                DrawField(new Rect(row.x, row.y, halfWidth - 4f, line), element.FindPropertyRelative("m_Length"), "Length");
                DrawField(new Rect(row.x + halfWidth, row.y, halfWidth, line), element.FindPropertyRelative("m_Rise"), "Rise");
                return;
            }

            float third = row.width / 3f;
            DrawField(new Rect(row.x, row.y, third - 4f, line), element.FindPropertyRelative("m_Angle"), "Angle");
            DrawField(new Rect(row.x + third, row.y, third - 4f, line), element.FindPropertyRelative("m_Radius"), "Radius");
            DrawField(new Rect(row.x + third * 2f, row.y, third, line), element.FindPropertyRelative("m_Rise"), "Rise");
        }

        private void DrawProp(Rect rect, int index, bool isActive, bool isFocused)
        {
            SerializedProperty element = m_PropList.serializedProperty.GetArrayElementAtIndex(index);
            float line = EditorGUIUtility.singleLineHeight;
            Rect row = new Rect(rect.x, rect.y + 3f, rect.width, line);

            float half = row.width * 0.5f;
            EditorGUI.PropertyField(new Rect(row.x, row.y, half - 4f, line), element.FindPropertyRelative("m_Prop"), GUIContent.none);
            EditorGUI.PropertyField(new Rect(row.x + half, row.y, half, line), element.FindPropertyRelative("m_Side"), GUIContent.none);

            float third = row.width / 3f;
            row.y += line + 2f;
            DrawField(new Rect(row.x, row.y, third - 4f, line), element.FindPropertyRelative("m_Distance"), "At");
            DrawField(new Rect(row.x + third, row.y, third - 4f, line), element.FindPropertyRelative("m_Count"), "Count");
            DrawField(new Rect(row.x + third * 2f, row.y, third, line), element.FindPropertyRelative("m_Spacing"), "Every");

            float quarter = row.width / 4f;
            row.y += line + 2f;
            DrawField(new Rect(row.x, row.y, quarter - 4f, line), element.FindPropertyRelative("m_EdgeOffset"), "Offset");
            DrawField(new Rect(row.x + quarter, row.y, quarter - 4f, line), element.FindPropertyRelative("m_Height"), "Up");
            DrawField(new Rect(row.x + quarter * 2f, row.y, quarter - 4f, line), element.FindPropertyRelative("m_Yaw"), "Yaw");
            DrawField(new Rect(row.x + quarter * 3f, row.y, quarter, line), element.FindPropertyRelative("m_Scale"), "Scale");
        }

        private static void DrawField(Rect rect, SerializedProperty property, string label)
        {
            float previous = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 44f;
            EditorGUI.PropertyField(rect, property, new GUIContent(label));
            EditorGUIUtility.labelWidth = previous;
        }

        private SerializedProperty SegmentsProperty => m_Serialized.FindProperty("m_Segments");
        private SerializedProperty PropsProperty => m_Serialized.FindProperty("m_Props");
        private SerializedProperty LandmarksProperty => m_Serialized.FindProperty("m_Landmarks");

        private void SelectSegment(int index)
        {
            if (m_SegmentList != null)
                m_SegmentList.index = index;

            m_StartSelected = false;
            SceneView.RepaintAll();
            Repaint();
        }

        private void AddSegment(TrackSegment segment)
        {
            m_Serialized.Update();
            SerializedProperty array = SegmentsProperty;
            int index = m_SegmentList.index >= 0 && m_SegmentList.index < array.arraySize
                ? m_SegmentList.index + 1
                : array.arraySize;

            array.InsertArrayElementAtIndex(Mathf.Min(index, array.arraySize));
            if (index >= array.arraySize)
                index = array.arraySize - 1;

            WriteSegment(array.GetArrayElementAtIndex(index), segment);
            m_Serialized.ApplyModifiedProperties();
            SelectSegment(index);
            OnLayoutChanged();
        }

        private void DeleteSelectedSegment()
        {
            int index = m_SegmentList.index;
            m_Serialized.Update();
            SerializedProperty array = SegmentsProperty;
            if (index < 0 || index >= array.arraySize)
                return;

            array.DeleteArrayElementAtIndex(index);
            m_Serialized.ApplyModifiedProperties();
            SelectSegment(Mathf.Min(index, array.arraySize - 1));
            OnLayoutChanged();
        }

        private void FlipSelectedTurn()
        {
            int index = m_SegmentList.index;
            if (index < 0 || index >= m_Layout.SegmentCount || !m_Layout.GetSegment(index).IsTurn)
                return;

            SetSegmentFloat(index, "m_Angle", -m_Layout.GetSegment(index).Angle);
        }

        private void SetSegmentFloat(int index, string field, float value)
        {
            m_Serialized.Update();
            SegmentsProperty.GetArrayElementAtIndex(index).FindPropertyRelative(field).floatValue = value;
            m_Serialized.ApplyModifiedProperties();
            OnLayoutChanged();
        }

        private void AddProp(TrackPropPlacement placement)
        {
            m_Serialized.Update();
            SerializedProperty array = PropsProperty;
            int index = array.arraySize;
            array.arraySize = index + 1;
            WriteProp(array.GetArrayElementAtIndex(index), placement);
            m_Serialized.ApplyModifiedProperties();
            m_PropList.index = index;
            OnLayoutChanged();
        }

        private void FillWholeRace()
        {
            int count = Mathf.FloorToInt(RaceLength / m_FillSpacing) + 1;
            AddProp(TrackPropPlacement.Create(m_FillProp, m_FillSide, 0f, m_FillOffset, count, m_FillSpacing));
        }

        private void FillTurnOutsides()
        {
            float nearSign = m_Config != null ? m_Config.ViewSideSign : 1f;

            for (int i = 0; i < m_Layout.SegmentCount; i++)
            {
                TrackSegment segment = m_Layout.GetSegment(i);
                if (!segment.IsTurn)
                    continue;

                float outsideSign = -TrackLayoutSO.TurnSign(segment);
                ETrackSide side = Mathf.Approximately(outsideSign, Mathf.Sign(nearSign)) ? ETrackSide.Near : ETrackSide.Far;
                int count = Mathf.Max(1, Mathf.FloorToInt(segment.PathLength / m_FillSpacing) + 1);

                AddProp(TrackPropPlacement.Create(m_FillProp, side, m_Layout.GetSegmentStartDistance(i), m_FillOffset,
                    count, m_FillSpacing));
            }
        }

        private void ExtendToFinish()
        {
            if (m_Path == null)
                return;

            float missing = RaceLength + m_Config.RunoutMeters - m_Path.TotalLength;
            if (missing <= 0.5f)
                return;

            m_SegmentList.index = m_Layout.SegmentCount - 1;
            AddSegment(TrackSegment.Straight(Mathf.Ceil(missing)));
        }

        private void DrawFinishControls(float route, float race)
        {
            EditorGUILayout.BeginHorizontal();
            if (m_Layout.ClosedLoop)
            {
                EditorGUILayout.LabelField($"Finish is {race / route:0.##} laps in", GUILayout.Width(150f));
                m_Laps = Mathf.Max(1, EditorGUILayout.IntField("Laps", m_Laps));
                if (GUILayout.Button("Finish At Start"))
                    SetRaceLength(route * m_Laps);
            }
            else if (GUILayout.Button("Finish At Route End"))
            {
                SetRaceLength(route);
            }

            if (GUILayout.Button("Close Loop"))
                CloseLoop();
            EditorGUILayout.EndHorizontal();
        }

        private float RaceLength
        {
            get
            {
                if (m_Layout != null && m_Layout.RaceLengthMeters >= 1f)
                    return m_Layout.RaceLengthMeters;

                if (m_Config != null)
                    return m_Config.DefaultRaceLengthMeters;

                return m_Path != null ? m_Path.TotalLength : 0f;
            }
        }

        private void SetRaceLength(float length)
        {
            if (m_Serialized == null)
                return;

            m_Serialized.Update();
            m_Serialized.FindProperty("m_RaceLengthMeters").floatValue = Mathf.Max(TrackSegment.MinLength * 10f, length);
            m_Serialized.ApplyModifiedProperties();
            OnLayoutChanged();
        }

        private TrackCatalogSO ResolveTrackCatalog(bool create)
        {
            if (m_Config != null && m_Config.TrackCatalog != null)
                return m_Config.TrackCatalog;

            TrackCatalogSO catalog = AssetDatabase.LoadAssetAtPath<TrackCatalogSO>(TrackCatalogPath);
            if (!create)
                return catalog;

            if (catalog == null)
            {
                catalog = CreateInstance<TrackCatalogSO>();
                AssetDatabase.CreateAsset(catalog, TrackCatalogPath);
            }

            if (m_Config != null)
            {
                SerializedObject config = new SerializedObject(m_Config);
                config.FindProperty("m_TrackCatalog").objectReferenceValue = catalog;
                config.ApplyModifiedProperties();
            }

            return catalog;
        }

        private void SubmitToTrackList()
        {
            TrackCatalogSO catalog = ResolveTrackCatalog(true);
            if (catalog == null || m_Serialized == null)
                return;

            m_Serialized.Update();
            SerializedProperty length = m_Serialized.FindProperty("m_RaceLengthMeters");
            if (length.floatValue < 1f)
                length.floatValue = RaceLength;

            SerializedProperty displayName = m_Serialized.FindProperty("m_DisplayName");
            if (string.IsNullOrWhiteSpace(displayName.stringValue))
                displayName.stringValue = m_Layout.name;

            m_Serialized.ApplyModifiedProperties();

            if (catalog.IndexOf(m_Layout) < 0)
            {
                SerializedObject serialized = new SerializedObject(catalog);
                SerializedProperty tracks = serialized.FindProperty("m_Tracks");
                tracks.arraySize++;
                tracks.GetArrayElementAtIndex(tracks.arraySize - 1).objectReferenceValue = m_Layout;
                serialized.ApplyModifiedProperties();
            }

            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(catalog);
        }

        private void RemoveFromTrackList()
        {
            TrackCatalogSO catalog = ResolveTrackCatalog(false);
            int index = catalog != null ? catalog.IndexOf(m_Layout) : -1;
            if (index < 0)
                return;

            SerializedObject serialized = new SerializedObject(catalog);
            SerializedProperty tracks = serialized.FindProperty("m_Tracks");
            tracks.GetArrayElementAtIndex(index).objectReferenceValue = null;
            tracks.DeleteArrayElementAtIndex(index);
            serialized.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
        }

        private void CloseLoop()
        {
            int count = m_Layout.SegmentCount;
            if (count < 2)
                return;

            int replace = count - 1;
            TrackSegment last = m_Layout.GetSegment(replace);
            float radius = last.IsTurn ? last.Radius : m_DefaultRadius;
            m_Layout.GetSegmentStart(replace, out Vector3 from, out float fromHeading);

            List<TrackSegment> bridge = new List<TrackSegment>();
            if (!TrackLoopSolver.TrySolve(from, fromHeading, m_Layout.StartPosition, m_Layout.StartHeading, radius, bridge))
            {
                EditorUtility.DisplayDialog("Close Loop", "No connection found with this radius. Try a smaller Radius.", "OK");
                return;
            }

            m_Serialized.Update();
            SerializedProperty array = SegmentsProperty;
            array.arraySize = replace + bridge.Count;
            for (int i = 0; i < bridge.Count; i++)
                WriteSegment(array.GetArrayElementAtIndex(replace + i), bridge[i]);

            m_Serialized.FindProperty("m_ClosedLoop").boolValue = true;
            m_Serialized.ApplyModifiedProperties();
            SelectSegment(-1);
            OnLayoutChanged();
        }

        private void AssignToConfig(TrackLayoutSO layout)
        {
            SerializedObject config = new SerializedObject(m_Config);
            config.FindProperty("m_TrackLayout").objectReferenceValue = layout;
            config.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
        }

        private void CreateLayout()
        {
            string folder = TrackParentFolder + "/" + TrackFolderName;
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder(TrackParentFolder, TrackFolderName);

            TrackLayoutSO layout = CreateInstance<TrackLayoutSO>();
            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/TrackLayout.asset");
            AssetDatabase.CreateAsset(layout, path);

            SerializedObject serialized = new SerializedObject(layout);
            serialized.FindProperty("m_PropCatalog").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<TrackPropCatalogSO>(CatalogPath);
            serialized.FindProperty("m_DisplayName").stringValue = layout.name;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();

            m_Layout = layout;
            EditorGUIUtility.PingObject(layout);
            Bind();
        }

        private static void WriteSegment(SerializedProperty element, TrackSegment segment)
        {
            element.FindPropertyRelative("m_Type").enumValueIndex = (int)segment.Type;
            element.FindPropertyRelative("m_Length").floatValue = segment.Length;
            element.FindPropertyRelative("m_Angle").floatValue = segment.Angle;
            element.FindPropertyRelative("m_Radius").floatValue = segment.Radius;
            element.FindPropertyRelative("m_Rise").floatValue = segment.Rise;
        }

        private static void WriteProp(SerializedProperty element, TrackPropPlacement placement)
        {
            element.FindPropertyRelative("m_Prop").intValue = (int)placement.Prop;
            element.FindPropertyRelative("m_Side").intValue = (int)placement.Side;
            element.FindPropertyRelative("m_Distance").floatValue = placement.Distance;
            element.FindPropertyRelative("m_EdgeOffset").floatValue = placement.EdgeOffset;
            element.FindPropertyRelative("m_Height").floatValue = placement.Height;
            element.FindPropertyRelative("m_Count").intValue = placement.Count;
            element.FindPropertyRelative("m_Spacing").floatValue = placement.Spacing;
            element.FindPropertyRelative("m_Yaw").floatValue = placement.Yaw;
            element.FindPropertyRelative("m_Scale").floatValue = placement.Scale;
        }

        private void OnLayoutChanged()
        {
            m_Path = m_Layout != null && m_Layout.SegmentCount > 0 ? RacePath.FromLayout(m_Layout) : null;
            SchedulePreview();
            SceneView.RepaintAll();
            Repaint();
        }

        private void OnUndoRedo()
        {
            if (m_Serialized != null)
                m_Serialized.Update();

            OnLayoutChanged();
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                Tools.hidden = false;
                DestroyPreview();
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                SchedulePreview();
            }
        }

        private void SchedulePreview()
        {
            EditorApplication.delayCall -= FlushPreview;
            EditorApplication.delayCall += FlushPreview;
        }

        private void FlushPreview()
        {
            if (GUIUtility.hotControl != 0)
            {
                SchedulePreview();
                return;
            }

            DestroyPreview();

            if (!m_LivePreview || m_Path == null || m_Config == null || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            m_Preview = new TrackBuilder(m_Config, m_Path, m_Layout);
            m_Preview.Build();

            Transform root = m_Preview.Root;
            root.name = PreviewName;

            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
                all[i].gameObject.hideFlags = HideFlags.DontSave;

            SceneVisibilityManager.instance.DisablePicking(root.gameObject, true);
        }

        private void DestroyPreview()
        {
            if (m_Preview == null)
                return;

            m_Preview.Dispose();
            m_Preview = null;
        }

        private static void DestroyStalePreviews()
        {
            GameObject[] all = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = 0; i < all.Length; i++)
            {
                GameObject candidate = all[i];
                if (candidate == null || candidate.name != PreviewName || candidate.transform.parent != null)
                    continue;

                if ((candidate.hideFlags & HideFlags.DontSave) == HideFlags.DontSave && !EditorUtility.IsPersistent(candidate))
                    DestroyImmediate(candidate);
            }
        }

        private void FrameInScene()
        {
            SceneView view = SceneView.lastActiveSceneView;
            if (view == null || m_Path == null)
                return;

            float runout = m_Config != null ? m_Config.RunoutMeters : 0f;
            Bounds bounds = new Bounds(m_Path.GetPosition(0f), Vector3.one);
            for (float d = -runout; d <= m_Path.TotalLength + runout; d += 10f)
                bounds.Encapsulate(m_Path.GetPosition(d));

            for (int i = 0; i < m_Layout.LandmarkCount; i++)
            {
                m_Layout.GetLandmark(i).Resolve(m_Path, out Vector3 position, out _);
                bounds.Encapsulate(position);
            }

            view.Frame(bounds, false);
        }

        private static IEnumerable<float> Laterals(TrackPropPlacement placement, float half, float nearSign)
        {
            float edge = half + placement.EdgeOffset;
            switch (placement.Side)
            {
                case ETrackSide.Far:
                    yield return -nearSign * edge;
                    break;
                case ETrackSide.Near:
                    yield return nearSign * edge;
                    break;
                case ETrackSide.Both:
                    yield return -nearSign * edge;
                    yield return nearSign * edge;
                    break;
                default:
                    yield return placement.EdgeOffset;
                    break;
            }
        }
    }
}
