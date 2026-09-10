using CasualKit.Core;
using UnityEngine;

namespace MatchRacers
{
    public sealed class TrackBuilder
    {
        private const float RoadThickness = 0.4f;
        private const float StripeWidth = 0.12f;
        private const float LineHeight = 0.02f;

        private readonly RaceConfigSO m_Config;
        private readonly RacePath m_Path;

        private Transform m_Root;

        public Transform Root => m_Root;

        public TrackBuilder(RaceConfigSO config, RacePath path)
        {
            m_Config = config;
            m_Path = path;
        }

        public void Build()
        {
            if (m_Root != null)
                return;

            m_Root = new GameObject("RaceTrack").transform;

            float length = m_Config.RaceLengthMeters;
            float runout = m_Config.RunoutMeters;
            float width = m_Config.RoadWidthMeters;

            Material asphalt = CreateMaterial(new Color(0.16f, 0.17f, 0.19f));
            Material stripe = CreateMaterial(new Color(0.78f, 0.78f, 0.74f));
            Material startPaint = CreateMaterial(new Color(0.35f, 0.65f, 0.9f));
            Material finishPaint = CreateMaterial(new Color(0.95f, 0.95f, 0.95f));

            CreateBox("Road",
                new Vector3((length + 2f * runout) * 0.5f - runout, -RoadThickness * 0.5f, 0f),
                new Vector3(length + 2f * runout, RoadThickness, width),
                asphalt);

            for (int lane = 1; lane < RaceConfigSO.CarCount; lane++)
            {
                float z = m_Config.GetLaneOffset(lane) - m_Config.LaneWidthMeters * 0.5f;
                CreateBox("LaneStripe_" + lane,
                    new Vector3((length + 2f * runout) * 0.5f - runout, LineHeight, z),
                    new Vector3(length + 2f * runout, LineHeight, StripeWidth),
                    stripe);
            }

            CreateBox("StartLine", new Vector3(0f, LineHeight * 1.5f, 0f),
                new Vector3(0.5f, LineHeight, width), startPaint);

            CreateBox("FinishLine", new Vector3(length, LineHeight * 1.5f, 0f),
                new Vector3(0.7f, LineHeight, width), finishPaint);

            CreateDistanceMarkers(length, width);
            CreateFinishGate(length, width, finishPaint);
        }

        private void CreateFinishGate(float length, float width, Material accent)
        {
            Material postMaterial = CreateMaterial(new Color(0.22f, 0.24f, 0.27f));
            float half = width * 0.5f + 0.6f;
            const float postHeight = 6f;

            CreateBox("FinishPostLeft", new Vector3(length, postHeight * 0.5f, -half),
                new Vector3(0.5f, postHeight, 0.5f), postMaterial);
            CreateBox("FinishPostRight", new Vector3(length, postHeight * 0.5f, half),
                new Vector3(0.5f, postHeight, 0.5f), postMaterial);
            CreateBox("FinishBanner", new Vector3(length, postHeight - 0.7f, 0f),
                new Vector3(0.6f, 1.4f, width + 1.2f), accent);

            Material checkDark = CreateMaterial(new Color(0.11f, 0.12f, 0.13f));
            int squares = 10;
            float squareSize = (width + 1.2f) / squares;

            for (int i = 0; i < squares; i += 2)
            {
                float z = -half - 0.6f + squareSize * (i + 0.5f);
                CreateBox("Check_" + i, new Vector3(length, postHeight - 0.7f, z),
                    new Vector3(0.62f, 1.4f, squareSize), checkDark);
            }
        }

        public Vector3 GetGridPosition(int laneIndex)
        {
            m_Path.Evaluate(0f, out Vector3 position, out Vector3 forward);
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            return position + right * m_Config.GetLaneOffset(laneIndex);
        }

        public void Dispose()
        {
            if (m_Root == null)
                return;

            Object.Destroy(m_Root.gameObject);
            m_Root = null;
        }

        private void CreateDistanceMarkers(float length, float width)
        {
            Material markerMaterial = CreateMaterial(new Color(0.55f, 0.55f, 0.58f));
            float half = width * 0.5f + 1.2f;

            for (float d = 100f; d < length; d += 100f)
            {
                CreateBox("Marker_" + (int)d, new Vector3(d, 0.6f, half),
                    new Vector3(0.3f, 1.2f, 0.3f), markerMaterial);
                CreateBox("Marker_" + (int)d + "_L", new Vector3(d, 0.6f, -half),
                    new Vector3(0.3f, 1.2f, 0.3f), markerMaterial);
            }
        }

        private void CreateBox(string boxName, Vector3 center, Vector3 size, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = boxName;
            box.transform.SetParent(m_Root, false);
            box.transform.localPosition = center;
            box.transform.localScale = size;

            Collider collider = box.GetComponent<Collider>();
            if (collider != null)
                Object.Destroy(collider);

            MeshRenderer renderer = box.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.sharedMaterial = material;
        }

        private static Material CreateMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                EditorLog.Error("URP/Lit shader not found. Assign the URP pipeline asset in Graphics and Quality settings.");
                return null;
            }

            Material material = new Material(shader);
            material.color = color;
            return material;
        }
    }
}
