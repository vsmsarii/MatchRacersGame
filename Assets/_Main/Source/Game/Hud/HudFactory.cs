using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MatchRacers
{
    public static class HudFactory
    {
        public static RectTransform CreateRect(string rectName, Transform parent,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject host = new GameObject(rectName, typeof(RectTransform));
            RectTransform rect = (RectTransform)host.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.localScale = Vector3.one;
            return rect;
        }

        public static Image CreateImage(string imageName, Transform parent, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            RectTransform rect = CreateRect(imageName, parent, anchorMin, anchorMax, offsetMin, offsetMax);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static TextMeshProUGUI CreateText(string textName, Transform parent, string content,
            float fontSize, Color color, TextAlignmentOptions alignment,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            RectTransform rect = CreateRect(textName, parent, anchorMin, anchorMax, offsetMin, offsetMax);
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;

            if (TMP_Settings.defaultFontAsset != null)
                text.font = TMP_Settings.defaultFontAsset;

            return text;
        }

        public static Button CreateButton(string buttonName, Transform parent, string label,
            float fontSize, Color background, Color textColor,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax,
            System.Action onClick)
        {
            RectTransform rect = CreateRect(buttonName, parent, anchorMin, anchorMax, offsetMin, offsetMax);

            Image image = rect.gameObject.AddComponent<Image>();
            image.color = background;
            image.raycastTarget = true;

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            if (onClick != null)
                button.onClick.AddListener(() => onClick());

            CreateText(buttonName + "Label", rect, label, fontSize, textColor,
                TextAlignmentOptions.Center, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            return button;
        }

        public static Image CreateBar(string barName, Transform parent, Color fillColor,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, out Image background)
        {
            background = CreateImage(barName + "Bg", parent, new Color(0f, 0f, 0f, 0.55f),
                anchorMin, anchorMax, offsetMin, offsetMax);

            Image fill = CreateImage(barName + "Fill", background.transform, fillColor,
                Vector2.zero, Vector2.one, new Vector2(2f, 2f), new Vector2(-2f, -2f));

            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
            return fill;
        }
    }
}
