using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MatchRacers
{
    public sealed class RaceTrackSelectView : MonoBehaviour
    {
        [Header("List")]
        [SerializeField] private Button m_ItemTemplate;
        [SerializeField, Min(0f)] private float m_ItemSpacing = 10f;
        [SerializeField, Min(0f)] private float m_BottomPadding = 60f;

        [Header("Colors")]
        [SerializeField] private Color m_SelectedColor = new Color(0.16f, 0.35f, 0.3f, 1f);

        private readonly List<Button> m_Items = new List<Button>();
        private Action<int> m_OnSelect;
        private Color m_IdleColor = Color.white;
        private bool m_IdleColorCaptured;

        public void Show(TrackCatalogSO catalog, int selectedIndex, Action<int> onSelect)
        {
            m_OnSelect = onSelect;
            Rebuild(catalog);
            SetSelected(selectedIndex);
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            m_OnSelect = null;
            gameObject.SetActive(false);
        }

        public void SetSelected(int index)
        {
            for (int i = 0; i < m_Items.Count; i++)
            {
                Graphic graphic = m_Items[i].targetGraphic;
                if (graphic != null)
                    graphic.color = i == index ? m_SelectedColor : m_IdleColor;
            }
        }

        private void Rebuild(TrackCatalogSO catalog)
        {
            if (m_ItemTemplate == null)
                return;

            if (!m_IdleColorCaptured)
            {
                if (m_ItemTemplate.targetGraphic != null)
                    m_IdleColor = m_ItemTemplate.targetGraphic.color;

                m_IdleColorCaptured = true;
            }

            m_ItemTemplate.gameObject.SetActive(false);

            for (int i = 0; i < m_Items.Count; i++)
            {
                if (m_Items[i] != null)
                    Destroy(m_Items[i].gameObject);
            }

            m_Items.Clear();

            int count = catalog != null ? catalog.Count : 0;
            RectTransform template = (RectTransform)m_ItemTemplate.transform;
            float step = template.sizeDelta.y + m_ItemSpacing;

            for (int i = 0; i < count; i++)
            {
                TrackLayoutSO track = catalog.GetTrack(i);
                Button item = Instantiate(m_ItemTemplate, template.parent, false);
                item.gameObject.name = "Track_" + i;
                ((RectTransform)item.transform).anchoredPosition = template.anchoredPosition - new Vector2(0f, step * i);

                TMP_Text label = item.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                    label.text = track != null ? track.DisplayName : "-";

                int index = i;
                item.onClick.AddListener(() => Select(index));
                item.gameObject.SetActive(true);
                m_Items.Add(item);
            }

            RectTransform root = (RectTransform)transform;
            float needed = -template.anchoredPosition.y + template.sizeDelta.y * 0.5f + step * Mathf.Max(0, count - 1) + m_BottomPadding;
            if (needed > root.sizeDelta.y)
                root.sizeDelta = new Vector2(root.sizeDelta.x, needed);
        }

        private void Select(int index)
        {
            Action<int> callback = m_OnSelect;
            if (callback != null)
                callback(index);
        }
    }
}
