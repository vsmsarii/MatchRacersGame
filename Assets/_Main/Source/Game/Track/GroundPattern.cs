using UnityEngine;

namespace MatchRacers
{
    public static class GroundPattern
    {
        private const int Size = 256;

        public static Texture2D Create(Color baseColor, Color patchColor, Color detailColor, int seed)
        {
            Texture2D texture = new Texture2D(Size, Size, TextureFormat.RGBA32, true)
            {
                name = "GroundPattern",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4
            };

            Color32[] pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                float v = y / (float)Size;
                for (int x = 0; x < Size; x++)
                {
                    float u = x / (float)Size;

                    float patches = Tile(u, v, 4, seed) * 0.6f + Tile(u, v, 9, seed + 1) * 0.3f + Tile(u, v, 20, seed + 2) * 0.1f;
                    Color color = Color.Lerp(baseColor, patchColor, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.65f, patches)));

                    float grain = Tile(u, v, 48, seed + 3) * 0.55f + Tile(u, v, 128, seed + 4) * 0.45f;
                    color *= Mathf.Lerp(0.88f, 1.12f, grain);

                    float speck = Tile(u, v, 80, seed + 5);
                    color = Color.Lerp(color, detailColor, Mathf.InverseLerp(0.7f, 0.92f, speck) * 0.75f);
                    color.a = 1f;

                    pixels[y * Size + x] = color;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }

        private static float Tile(float u, float v, int period, int seed)
        {
            float x = u * period;
            float y = v * period;
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            float fx = x - x0;
            float fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);

            int x1 = (x0 + 1) % period;
            int y1 = (y0 + 1) % period;
            x0 %= period;
            y0 %= period;

            float bottom = Mathf.Lerp(Lattice(x0, y0, seed), Lattice(x1, y0, seed), fx);
            float top = Mathf.Lerp(Lattice(x0, y1, seed), Lattice(x1, y1, seed), fx);
            return Mathf.Lerp(bottom, top, fy);
        }

        private static float Lattice(int x, int y, int seed)
        {
            unchecked
            {
                uint hash = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                hash = (hash ^ (hash >> 13)) * 1274126177u;
                hash ^= hash >> 16;
                return (hash & 0xFFFFFF) / 16777215f;
            }
        }
    }
}
