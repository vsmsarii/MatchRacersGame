using System.Collections.Generic;
using CasualKit.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace MatchRacers
{
    public sealed class TrackBuilder
    {
        private const float StripeWidth = 0.12f;
        private const float LineHeight = 0.02f;
        private const float RibbonStep = 1f;
        private const float UvTileMeters = 4f;
        private const float PostHeight = 6f;
        private const float GroundUvMeters = 10f;
        private const float VergeHeight = -0.02f;
        private const int GroundPatternSeed = 11;

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");

        private readonly RaceConfigSO m_Config;
        private readonly RacePath m_Path;
        private readonly TrackLayoutSO m_Layout;
        private readonly RaceEnvironmentSO m_Environment;
        private readonly List<Object> m_Owned = new List<Object>();

        private Transform m_Root;
        private Transform m_LandmarkRoot;
        private TrackPropInstancer m_Instancer;

        public Transform Root => m_Root;
        public int InstancedPropCount => m_Instancer != null ? m_Instancer.InstanceCount : 0;

        public void Render()
        {
            if (m_Instancer != null)
                m_Instancer.Render();
        }

        public TrackBuilder(RaceConfigSO config, RacePath path) : this(config, path, null)
        {
        }

        public TrackBuilder(RaceConfigSO config, RacePath path, TrackLayoutSO layout)
        {
            m_Config = config;
            m_Path = path;
            m_Layout = layout;
            m_Environment = layout != null && layout.Environment != null ? layout.Environment : config.Environment;
        }

        public void Build()
        {
            if (m_Root != null)
                return;

            m_Root = new GameObject("RaceTrack").transform;

            float length = m_Layout == null
                ? m_Config.RaceLengthMeters
                : m_Layout.RaceLengthMeters >= 1f ? m_Layout.RaceLengthMeters : m_Config.DefaultRaceLengthMeters;
            float runout = m_Config.RunoutMeters;
            float width = m_Config.RoadWidthMeters;
            bool closed = m_Path.IsClosed;
            float route = m_Path.TotalLength;
            float from = closed ? 0f : -runout;
            float to = closed ? route : Mathf.Max(route, length + runout);

            Material asphalt = m_Layout != null && m_Layout.RoadMaterial != null
                ? m_Layout.RoadMaterial
                : CreateMaterial(new Color(0.22f, 0.23f, 0.25f));
            Material stripe = m_Layout != null && m_Layout.StripeMaterial != null
                ? m_Layout.StripeMaterial
                : CreateMaterial(new Color(0.78f, 0.78f, 0.74f));
            Material startPaint = CreateMaterial(new Color(0.35f, 0.65f, 0.9f));
            Material finishPaint = CreateMaterial(new Color(0.95f, 0.95f, 0.95f));

            CreateRibbon("Road", from, to, -width * 0.5f, width * 0.5f, 0f, asphalt);

            for (int lane = 1; lane < RaceConfigSO.CarCount; lane++)
            {
                float lateral = (lane - RaceConfigSO.CarCount * 0.5f) * m_Config.LaneWidthMeters;
                CreateRibbon("LaneStripe_" + lane, from, to, lateral - StripeWidth * 0.5f, lateral + StripeWidth * 0.5f,
                    LineHeight, stripe);
            }

            CreateRibbon("StartLine", -0.25f, 0.25f, -width * 0.5f, width * 0.5f, LineHeight * 1.5f, startPaint);
            CreateRibbon("FinishLine", length - 0.35f, length + 0.35f, -width * 0.5f, width * 0.5f, LineHeight * 2f,
                finishPaint);

            if (m_Layout == null || m_Layout.DistanceMarkers)
                CreateDistanceMarkers(closed ? Mathf.Min(length, route) : length, width);

            if (m_Layout == null || m_Layout.FinishGate)
                CreateFinishGate(length, width, finishPaint);

            PlaceProps(width);
            PlaceLandmarks();
            CreateGround(from, to);
            CreateVerges(from, to, width);
            CreateBackdrop(from, to, width);
            StripPhysics(m_Root.gameObject);

            if (Application.isPlaying)
                CombineStatic();
        }

        public Vector3 GetGridPosition(int laneIndex)
        {
            m_Path.Evaluate(0f, out Vector3 position, out Vector3 forward);
            return position + RacePath.RightOf(forward) * m_Config.GetLaneOffset(laneIndex);
        }

        public void Dispose()
        {
            if (m_Instancer != null)
            {
                m_Instancer.Dispose();
                m_Instancer = null;
            }

            m_LandmarkRoot = null;

            if (m_Root != null)
                DestroyObject(m_Root.gameObject);

            for (int i = 0; i < m_Owned.Count; i++)
            {
                if (m_Owned[i] != null)
                    DestroyObject(m_Owned[i]);
            }

            m_Owned.Clear();
            m_Root = null;
        }

        private void CreateFinishGate(float length, float width, Material accent)
        {
            Material postMaterial = CreateMaterial(new Color(0.22f, 0.24f, 0.27f));
            Material checkDark = CreateMaterial(new Color(0.11f, 0.12f, 0.13f));
            float half = width * 0.5f + 0.6f;

            CreateBox("FinishPostLeft", length, -half, PostHeight * 0.5f, new Vector3(0.5f, PostHeight, 0.5f), postMaterial);
            CreateBox("FinishPostRight", length, half, PostHeight * 0.5f, new Vector3(0.5f, PostHeight, 0.5f), postMaterial);
            CreateBox("FinishBanner", length, 0f, PostHeight - 0.7f, new Vector3(width + 1.2f, 1.4f, 0.6f), accent);

            int squares = 10;
            float squareSize = (width + 1.2f) / squares;

            for (int i = 0; i < squares; i += 2)
            {
                float lateral = -half - 0.6f + squareSize * (i + 0.5f);
                CreateBox("Check_" + i, length, lateral, PostHeight - 0.7f, new Vector3(squareSize, 1.4f, 0.62f), checkDark);
            }
        }

        private void CreateDistanceMarkers(float length, float width)
        {
            Material markerMaterial = CreateMaterial(new Color(0.55f, 0.55f, 0.58f));
            float half = width * 0.5f + 1.2f;

            for (float d = 100f; d < length; d += 100f)
            {
                CreateBox("Marker_" + (int)d, d, half, 0.6f, new Vector3(0.3f, 1.2f, 0.3f), markerMaterial);
                CreateBox("Marker_" + (int)d + "_L", d, -half, 0.6f, new Vector3(0.3f, 1.2f, 0.3f), markerMaterial);
            }
        }

        private void PlaceProps(float width)
        {
            if (m_Layout == null || m_Layout.PropCatalog == null || m_Layout.PropCount == 0)
                return;

            Transform propRoot = null;
            if (Application.isPlaying)
            {
                m_Instancer = new TrackPropInstancer();
            }
            else
            {
                propRoot = new GameObject("Props").transform;
                propRoot.SetParent(m_Root, false);
            }

            float half = width * 0.5f;
            float nearSign = m_Config.ViewSideSign;

            for (int i = 0; i < m_Layout.PropCount; i++)
            {
                TrackPropPlacement placement = m_Layout.GetProp(i);
                if (!m_Layout.PropCatalog.TryGet(placement.Prop, out TrackPropEntry entry))
                    continue;

                for (int k = 0; k < placement.Count; k++)
                {
                    float distance = placement.GetDistance(k);

                    switch (placement.Side)
                    {
                        case ETrackSide.Far:
                            SpawnProp(propRoot, entry, placement, distance, -nearSign * (half + placement.EdgeOffset), true);
                            break;
                        case ETrackSide.Near:
                            SpawnProp(propRoot, entry, placement, distance, nearSign * (half + placement.EdgeOffset), false);
                            break;
                        case ETrackSide.Both:
                            SpawnProp(propRoot, entry, placement, distance, -nearSign * (half + placement.EdgeOffset), true);
                            SpawnProp(propRoot, entry, placement, distance, nearSign * (half + placement.EdgeOffset), false);
                            break;
                        default:
                            SpawnProp(propRoot, entry, placement, distance, placement.EdgeOffset, false);
                            break;
                    }
                }
            }

            if (m_Instancer != null)
                m_Instancer.Build();
        }

        private void SpawnProp(Transform parent, TrackPropEntry entry, TrackPropPlacement placement, float distance,
            float lateral, bool farSide)
        {
            m_Path.Evaluate(distance, out Vector3 position, out Vector3 forward);
            Vector3 right = RacePath.RightOf(forward);

            float yaw = placement.Yaw + (farSide && entry.MirrorOnFarSide ? 180f : 0f);
            Quaternion rotation = Quaternion.LookRotation(RacePath.Flatten(forward), Vector3.up)
                                  * Quaternion.Euler(0f, yaw, 0f)
                                  * Quaternion.Euler(entry.LocalEuler);

            Vector3 world = position + right * lateral + Vector3.up * (entry.HeightOffset + placement.Height);
            Vector3 scale = entry.Prefab.transform.localScale * (entry.LocalScale * placement.Scale);

            if (m_Instancer != null)
            {
                m_Instancer.Add(entry.Prefab, Matrix4x4.TRS(world, rotation, scale));
                return;
            }

            GameObject instance = Object.Instantiate(entry.Prefab, world, rotation, parent);
            instance.name = entry.Prefab.name;
            instance.transform.localScale = scale;
        }

        private static void StripPhysics(GameObject root)
        {
            Rigidbody[] bodies = root.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < bodies.Length; i++)
                DestroyObject(bodies[i]);

            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
                DestroyObject(colliders[i]);
        }

        private void CombineStatic()
        {
            MeshFilter[] filters = m_Root.GetComponentsInChildren<MeshFilter>(true);
            List<GameObject> combinable = new List<GameObject>(filters.Length);
            string skipped = string.Empty;

            for (int i = 0; i < filters.Length; i++)
            {
                Mesh mesh = filters[i].sharedMesh;
                if (mesh == null)
                    continue;

                if (mesh.isReadable)
                {
                    combinable.Add(filters[i].gameObject);
                    continue;
                }

                if (m_LandmarkRoot != null && filters[i].transform.IsChildOf(m_LandmarkRoot) && !skipped.Contains(mesh.name))
                    skipped += (skipped.Length > 0 ? ", " : string.Empty) + mesh.name;
            }

            if (combinable.Count > 1)
                StaticBatchingUtility.Combine(combinable.ToArray(), m_Root.gameObject);

            if (skipped.Length > 0)
                EditorLog.Warning("Track objects left out of static batching (enable Read/Write on their meshes): " + skipped);
        }

        private void PlaceLandmarks()
        {
            if (m_Layout == null || m_Layout.LandmarkCount == 0)
                return;

            Transform landmarkRoot = new GameObject("Landmarks").transform;
            landmarkRoot.SetParent(m_Root, false);
            m_LandmarkRoot = landmarkRoot;

            for (int i = 0; i < m_Layout.LandmarkCount; i++)
            {
                TrackLandmark landmark = m_Layout.GetLandmark(i);
                if (landmark.Prefab == null)
                    continue;

                landmark.Resolve(m_Path, out Vector3 position, out Quaternion rotation);
                GameObject instance = Object.Instantiate(landmark.Prefab, position, rotation, landmarkRoot);
                instance.name = landmark.DisplayName;
                instance.transform.localScale = Vector3.Scale(landmark.Prefab.transform.localScale, landmark.Scale);
            }
        }

        private void CreateGround(float from, float to)
        {
            if (m_Layout == null || !m_Layout.Ground)
                return;

            Bounds bounds = new Bounds(m_Path.GetPosition(from), Vector3.zero);
            for (float d = from; d < to; d += 10f)
                bounds.Encapsulate(m_Path.GetPosition(d));
            bounds.Encapsulate(m_Path.GetPosition(to));

            for (int i = 0; i < m_Layout.LandmarkCount; i++)
            {
                m_Layout.GetLandmark(i).Resolve(m_Path, out Vector3 position, out _);
                bounds.Encapsulate(position);
            }

            float margin = m_Layout.GroundMargin;
            float y = bounds.min.y + m_Layout.GroundHeight;
            Vector3 min = new Vector3(bounds.min.x - margin, y, bounds.min.z - margin);
            Vector3 max = new Vector3(bounds.max.x + margin, y, bounds.max.z + margin);

            Mesh mesh = new Mesh { name = "Ground" };
            mesh.vertices = new[]
            {
                new Vector3(min.x, y, min.z), new Vector3(min.x, y, max.z),
                new Vector3(max.x, y, max.z), new Vector3(max.x, y, min.z)
            };
            mesh.uv = new[]
            {
                new Vector2(min.x, min.z) / GroundUvMeters, new Vector2(min.x, max.z) / GroundUvMeters,
                new Vector2(max.x, max.z) / GroundUvMeters, new Vector2(max.x, min.z) / GroundUvMeters
            };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            m_Owned.Add(mesh);

            GameObject host = new GameObject("Ground");
            host.transform.SetParent(m_Root, false);
            host.AddComponent<MeshFilter>().sharedMesh = mesh;
            host.AddComponent<MeshRenderer>().sharedMaterial = CreateGroundMaterial();
        }

        private Material CreateGroundMaterial()
        {
            Material source = m_Layout.GroundMaterial;
            if (m_Environment == null || !m_Environment.GroundPattern)
                return source != null ? source : CreateMaterial(new Color(0.23f, 0.27f, 0.22f));

            Material material;
            if (source != null)
            {
                material = new Material(source) { name = source.name + " (Pattern)" };
                m_Owned.Add(material);
            }
            else
            {
                material = CreateMaterial(Color.white);
                if (material == null)
                    return null;
            }

            Texture2D texture = GroundPattern.Create(m_Environment.GroundColor, m_Environment.GroundPatchColor,
                m_Environment.GroundDetailColor, GroundPatternSeed);
            m_Owned.Add(texture);

            Vector2 tiling = Vector2.one * (GroundUvMeters / m_Environment.GroundTileMeters);
            if (material.HasProperty(BaseMapId))
            {
                material.SetTexture(BaseMapId, texture);
                material.SetTextureScale(BaseMapId, tiling);
            }
            else
            {
                material.mainTexture = texture;
                material.mainTextureScale = tiling;
            }

            if (material.HasProperty(BaseColorId))
                material.SetColor(BaseColorId, Color.white);

            if (material.HasProperty(SmoothnessId))
                material.SetFloat(SmoothnessId, m_Environment.GroundSmoothness);

            return material;
        }

        private void CreateVerges(float from, float to, float width)
        {
            if (m_Environment == null || m_Environment.VergeWidth <= 0f)
                return;

            Material verge = CreateMaterial(m_Environment.VergeColor);
            if (verge == null)
                return;

            if (verge.HasProperty(SmoothnessId))
                verge.SetFloat(SmoothnessId, m_Environment.GroundSmoothness);

            float half = width * 0.5f;
            float outer = half + m_Environment.VergeWidth;
            CreateRibbon("VergeLeft", from, to, -outer, -half, VergeHeight, verge);
            CreateRibbon("VergeRight", from, to, half, outer, VergeHeight, verge);
        }

        private void CreateBackdrop(float from, float to, float width)
        {
            if (m_Environment == null || m_Environment.BackdropStyle == EBackdropStyle.None)
                return;

            Material material = m_Environment.BackdropMaterial;
            if (material == null)
            {
                EditorLog.Warning("Race environment '" + m_Environment.name + "' has a backdrop style but no backdrop material.");
                return;
            }

            float groundOffset = m_Layout != null ? m_Layout.GroundHeight : 0f;
            List<Mesh> meshes = TrackBackdrop.Build(m_Path, m_Environment, width * 0.5f, -m_Config.ViewSideSign, from, to,
                groundOffset);
            if (meshes.Count == 0)
                return;

            Transform backdropRoot = new GameObject("Backdrop").transform;
            backdropRoot.SetParent(m_Root, false);

            for (int i = 0; i < meshes.Count; i++)
            {
                m_Owned.Add(meshes[i]);

                GameObject host = new GameObject(meshes[i].name);
                host.transform.SetParent(backdropRoot, false);
                host.AddComponent<MeshFilter>().sharedMesh = meshes[i];

                MeshRenderer renderer = host.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
        }

        private void CreateRibbon(string ribbonName, float from, float to, float left, float right, float height,
            Material material)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt((to - from) / RibbonStep));
            Vector3[] vertices = new Vector3[(steps + 1) * 2];
            Vector2[] uvs = new Vector2[vertices.Length];
            int[] triangles = new int[steps * 6];

            for (int i = 0; i <= steps; i++)
            {
                float distance = Mathf.Lerp(from, to, i / (float)steps);
                m_Path.Evaluate(distance, out Vector3 position, out Vector3 forward);
                Vector3 side = RacePath.RightOf(forward);
                Vector3 lift = Vector3.up * height;

                vertices[i * 2] = position + side * left + lift;
                vertices[i * 2 + 1] = position + side * right + lift;
                uvs[i * 2] = new Vector2(0f, distance / UvTileMeters);
                uvs[i * 2 + 1] = new Vector2(1f, distance / UvTileMeters);
            }

            for (int i = 0; i < steps; i++)
            {
                int v = i * 2;
                int t = i * 6;
                triangles[t] = v;
                triangles[t + 1] = v + 2;
                triangles[t + 2] = v + 3;
                triangles[t + 3] = v;
                triangles[t + 4] = v + 3;
                triangles[t + 5] = v + 1;
            }

            Mesh mesh = new Mesh { name = ribbonName };
            if (vertices.Length > 65000)
                mesh.indexFormat = IndexFormat.UInt32;

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            m_Owned.Add(mesh);

            GameObject host = new GameObject(ribbonName);
            host.transform.SetParent(m_Root, false);
            host.AddComponent<MeshFilter>().sharedMesh = mesh;

            MeshRenderer renderer = host.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        private void CreateBox(string boxName, float distance, float lateral, float height, Vector3 size, Material material)
        {
            m_Path.Evaluate(distance, out Vector3 position, out Vector3 forward);

            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = boxName;
            box.transform.SetParent(m_Root, false);
            box.transform.SetPositionAndRotation(
                position + RacePath.RightOf(forward) * lateral + Vector3.up * height,
                Quaternion.LookRotation(RacePath.Flatten(forward), Vector3.up));
            box.transform.localScale = size;

            Collider collider = box.GetComponent<Collider>();
            if (collider != null)
                DestroyObject(collider);

            MeshRenderer renderer = box.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.sharedMaterial = material;
        }

        private Material CreateMaterial(Color color)
        {
            Shader shader = m_Config != null ? m_Config.LitShader : null;
            if (shader == null)
            {
                EditorLog.Error("URP/Lit shader not assigned on RaceConfig.");
                return null;
            }

            Material material = new Material(shader) { color = color };
            m_Owned.Add(material);
            return material;
        }

        private static void DestroyObject(Object target)
        {
            if (Application.isPlaying)
                Object.Destroy(target);
            else
                Object.DestroyImmediate(target);
        }
    }
}
