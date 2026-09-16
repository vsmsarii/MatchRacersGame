using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace MatchRacers
{
    public sealed class TrackPropInstancer : IDisposable
    {
        private const int ChunkSize = 128;

        private readonly struct BatchKey : IEquatable<BatchKey>
        {
            public readonly Mesh Mesh;
            public readonly int SubMesh;
            public readonly Material Material;
            public readonly ShadowCastingMode Shadows;
            public readonly bool ReceiveShadows;
            public readonly int Layer;

            public BatchKey(Mesh mesh, int subMesh, Material material, ShadowCastingMode shadows, bool receiveShadows, int layer)
            {
                Mesh = mesh;
                SubMesh = subMesh;
                Material = material;
                Shadows = shadows;
                ReceiveShadows = receiveShadows;
                Layer = layer;
            }

            public bool Equals(BatchKey other)
            {
                return Mesh == other.Mesh && SubMesh == other.SubMesh && Material == other.Material &&
                       Shadows == other.Shadows && ReceiveShadows == other.ReceiveShadows && Layer == other.Layer;
            }

            public override bool Equals(object obj)
            {
                return obj is BatchKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Mesh, SubMesh, Material, (int)Shadows, ReceiveShadows, Layer);
            }
        }

        private sealed class Batch
        {
            public BatchKey Key;
            public RenderParams Params;
            public readonly List<Matrix4x4> Pending = new List<Matrix4x4>();
            public Matrix4x4[] Matrices;
            public NativeArray<Matrix4x4> Instances;
            public int Count;
            public Bounds[] ChunkBounds;
        }

        private readonly Dictionary<BatchKey, Batch> m_Batches = new Dictionary<BatchKey, Batch>();
        private readonly List<Batch> m_Order = new List<Batch>();
        private readonly Dictionary<Material, Material> m_InstancedMaterials = new Dictionary<Material, Material>();
        private readonly List<MeshFilter> m_Filters = new List<MeshFilter>();
        private bool m_Built;

        public int InstanceCount { get; private set; }
        public int ChunkCount { get; private set; }

        public void Add(GameObject prefab, Matrix4x4 rootMatrix)
        {
            if (prefab == null || m_Built)
                return;

            Transform prefabRoot = prefab.transform;
            Matrix4x4 toPrefab = prefabRoot.worldToLocalMatrix;
            prefab.GetComponentsInChildren(true, m_Filters);

            for (int i = 0; i < m_Filters.Count; i++)
            {
                MeshFilter filter = m_Filters[i];
                Mesh mesh = filter.sharedMesh;
                if (mesh == null || !IsActive(filter.transform, prefabRoot))
                    continue;

                MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null || !renderer.enabled)
                    continue;

                Matrix4x4 matrix = rootMatrix * (toPrefab * filter.transform.localToWorldMatrix);
                Material[] materials = renderer.sharedMaterials;

                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    Material material = materials.Length == 0 ? null : materials[Mathf.Min(sub, materials.Length - 1)];
                    if (material == null)
                        continue;

                    BatchKey key = new BatchKey(mesh, sub, material, renderer.shadowCastingMode, renderer.receiveShadows,
                        filter.gameObject.layer);

                    if (!m_Batches.TryGetValue(key, out Batch batch))
                    {
                        batch = new Batch { Key = key };
                        m_Batches.Add(key, batch);
                        m_Order.Add(batch);
                    }

                    batch.Pending.Add(matrix);
                }
            }

            InstanceCount++;
        }

        public void Build()
        {
            if (m_Built)
                return;

            m_Built = true;
            ChunkCount = 0;

            for (int b = 0; b < m_Order.Count; b++)
            {
                Batch batch = m_Order[b];
                batch.Matrices = batch.Pending.ToArray();
                batch.Pending.Clear();

                int count = batch.Matrices.Length;
                int chunks = (count + ChunkSize - 1) / ChunkSize;
                batch.ChunkBounds = new Bounds[chunks];
                Bounds local = batch.Key.Mesh.bounds;

                for (int c = 0; c < chunks; c++)
                {
                    int start = c * ChunkSize;
                    int end = Mathf.Min(count, start + ChunkSize);
                    Bounds bounds = TransformBounds(local, batch.Matrices[start]);

                    for (int i = start + 1; i < end; i++)
                        bounds.Encapsulate(TransformBounds(local, batch.Matrices[i]));

                    batch.ChunkBounds[c] = bounds;
                }

                batch.Params = new RenderParams(GetInstancedMaterial(batch.Key.Material))
                {
                    shadowCastingMode = batch.Key.Shadows,
                    receiveShadows = batch.Key.ReceiveShadows,
                    layer = batch.Key.Layer
                };

                batch.Count = count;
                batch.Instances = new NativeArray<Matrix4x4>(batch.Matrices, Allocator.Persistent);
                batch.Matrices = null;

                ChunkCount += chunks;
            }
        }

        public void Render()
        {
            if (!m_Built)
                return;

            for (int b = 0; b < m_Order.Count; b++)
            {
                Batch batch = m_Order[b];
                if (!batch.Instances.IsCreated)
                    continue;

                for (int c = 0; c < batch.ChunkBounds.Length; c++)
                {
                    RenderParams parameters = batch.Params;
                    parameters.worldBounds = batch.ChunkBounds[c];

                    int start = c * ChunkSize;
                    Graphics.RenderMeshInstanced(parameters, batch.Key.Mesh, batch.Key.SubMesh, batch.Instances,
                        Mathf.Min(ChunkSize, batch.Count - start), start);
                }
            }
        }

        public void Dispose()
        {
            for (int b = 0; b < m_Order.Count; b++)
            {
                if (m_Order[b].Instances.IsCreated)
                    m_Order[b].Instances.Dispose();
            }

            foreach (KeyValuePair<Material, Material> pair in m_InstancedMaterials)
            {
                if (pair.Value != null && pair.Value != pair.Key)
                    Object.Destroy(pair.Value);
            }

            m_InstancedMaterials.Clear();
            m_Batches.Clear();
            m_Order.Clear();
            m_Built = false;
        }

        private Material GetInstancedMaterial(Material source)
        {
            if (m_InstancedMaterials.TryGetValue(source, out Material cached))
                return cached;

            Material instanced = source.enableInstancing
                ? source
                : new Material(source) { name = source.name + " (Instanced)", enableInstancing = true };

            m_InstancedMaterials.Add(source, instanced);
            return instanced;
        }

        private static bool IsActive(Transform node, Transform root)
        {
            for (Transform current = node; current != null; current = current.parent)
            {
                if (!current.gameObject.activeSelf)
                    return false;

                if (current == root)
                    return true;
            }

            return true;
        }

        private static Bounds TransformBounds(Bounds local, Matrix4x4 matrix)
        {
            Vector3 center = matrix.MultiplyPoint3x4(local.center);
            Vector3 extents = local.extents;
            Vector3 axisX = matrix.MultiplyVector(new Vector3(extents.x, 0f, 0f));
            Vector3 axisY = matrix.MultiplyVector(new Vector3(0f, extents.y, 0f));
            Vector3 axisZ = matrix.MultiplyVector(new Vector3(0f, 0f, extents.z));

            Vector3 size = new Vector3(
                Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x),
                Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y),
                Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z));

            return new Bounds(center, size * 2f);
        }
    }
}
