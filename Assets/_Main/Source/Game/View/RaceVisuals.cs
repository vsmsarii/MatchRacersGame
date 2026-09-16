using CasualKit.Core;
using UnityEngine;

namespace MatchRacers
{
    public static class RaceVisuals
    {
        private static readonly Color[] BuffColors =
        {
            new Color(0.70f, 0.75f, 0.78f),
            new Color(0.35f, 0.80f, 0.92f),
            new Color(0.40f, 0.88f, 0.52f),
            new Color(0.98f, 0.75f, 0.25f),
            new Color(1.00f, 0.42f, 0.22f),
        };

        public static Color GetBuffColor(int key)
        {
            int index = Mathf.Clamp(key - BuffTableSO.MinKey, 0, BuffColors.Length - 1);
            return BuffColors[index];
        }

        public static float GetBuffWidth(int key)
        {
            return 0.18f + 0.11f * Mathf.Max(0, key - 1);
        }

        public static Material CreateUnlitMaterial(Shader shader, Color color)
        {
            if (shader == null)
            {
                EditorLog.Error("URP/Unlit shader not assigned on RaceConfig.");
                return null;
            }

            Material material = new Material(shader);
            material.color = color;

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);

            return material;
        }
    }
}
