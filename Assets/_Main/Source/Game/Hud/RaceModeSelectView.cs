using System;
using TMPro;
using UnityEngine;

namespace MatchRacers
{
    public sealed class RaceModeSelectView
    {
        private static readonly Color Panel = new Color(0.04f, 0.05f, 0.06f, 0.92f);
        private static readonly Color Ink = new Color(0.95f, 0.96f, 0.94f);
        private static readonly Color Muted = new Color(0.68f, 0.71f, 0.69f);
        private static readonly Color FreeColor = new Color(0.16f, 0.42f, 0.36f, 0.95f);
        private static readonly Color TargetColor = new Color(0.42f, 0.24f, 0.14f, 0.95f);

        private readonly GameObject m_Root;

        public RaceModeSelectView(Transform parent, int carCount, Action<ERaceMode, int> onConfirm)
        {
            RectTransform root = HudFactory.CreateRect("RaceModeSelect", parent,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-320f, -220f), new Vector2(320f, 220f));
            m_Root = root.gameObject;

            HudFactory.CreateImage("Bg", root, Panel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            HudFactory.CreateText("Title", root, "YARIŞ MODU", 34f, Ink,
                TextAlignmentOptions.Center, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(24f, -70f), new Vector2(-24f, -18f));

            HudFactory.CreateButton("FreeMode", root, "SERBEST MOD", 24f, FreeColor, Ink,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, -140f), new Vector2(-40f, -80f),
                () => onConfirm(ERaceMode.Free, 0));

            HudFactory.CreateText("FreeHint", root, "Sonucu senin hamlelerin belirler", 17f, Muted,
                TextAlignmentOptions.Center, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(24f, -168f), new Vector2(-24f, -142f));

            HudFactory.CreateText("TargetTitle", root, "HEDEF SIRA MODU", 24f, Ink,
                TextAlignmentOptions.Center, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(24f, -212f), new Vector2(-24f, -178f));

            HudFactory.CreateText("TargetHint", root, "Bitirmek istediğin sırayı seç", 17f, Muted,
                TextAlignmentOptions.Center, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(24f, -238f), new Vector2(-24f, -214f));

            float slotWidth = 600f / carCount;
            for (int position = 1; position <= carCount; position++)
            {
                int captured = position;
                float x = 20f + (position - 1) * slotWidth;

                HudFactory.CreateButton("Target" + position, root, position.ToString(), 26f, TargetColor, Ink,
                    new Vector2(0f, 0f), new Vector2(0f, 0f),
                    new Vector2(x, 46f), new Vector2(x + slotWidth - 8f, 46f + slotWidth - 8f),
                    () => onConfirm(ERaceMode.TargetOrder, captured));
            }

            HudFactory.CreateText("Footer", root, "1-5 tuşları yarış boyunca nitro verir", 16f, Muted,
                TextAlignmentOptions.Center, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(24f, 14f), new Vector2(-24f, 40f));
        }

        public void Dispose()
        {
            if (m_Root != null)
                UnityEngine.Object.Destroy(m_Root);
        }
    }
}
