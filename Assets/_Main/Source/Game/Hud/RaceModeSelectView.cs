using System;
using UnityEngine;
using UnityEngine.UI;

namespace MatchRacers
{
    public sealed class RaceModeSelectView : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button m_FreeButton;
        [SerializeField] private Button[] m_TargetButtons = new Button[RaceConfigSO.CarCount];

        private Action<ERaceMode, int> m_OnConfirm;
        private bool m_Wired;

        public void Show(Action<ERaceMode, int> onConfirm)
        {
            m_OnConfirm = onConfirm;
            Wire();
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            m_OnConfirm = null;
            gameObject.SetActive(false);
        }

        private void Wire()
        {
            if (m_Wired)
                return;

            m_Wired = true;

            if (m_FreeButton != null)
                m_FreeButton.onClick.AddListener(() => Confirm(ERaceMode.Free, 0));

            for (int i = 0; i < m_TargetButtons.Length; i++)
            {
                if (m_TargetButtons[i] == null)
                    continue;

                int position = i + 1;
                m_TargetButtons[i].onClick.AddListener(() => Confirm(ERaceMode.TargetOrder, position));
            }
        }

        private void Confirm(ERaceMode mode, int targetPosition)
        {
            Action<ERaceMode, int> callback = m_OnConfirm;
            if (callback != null)
                callback(mode, targetPosition);
        }
    }
}
