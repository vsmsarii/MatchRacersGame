using UnityEngine;

namespace CasualKit.Core
{
    [CreateAssetMenu(fileName = "GlobalSettings", menuName = "CasualKit/Global Settings")]
    public sealed class GlobalSettingsSO : ScriptableObject
    {
        [Header("Performance")]
        [Tooltip("Uygulamanın hedef kare hızı. Simülasyon sabit adımlı olduğu için yarış sonucunu değiştirmez, yalnız sunumun akıcılığını belirler.")]
        [SerializeField, Min(30)] private int m_TargetFrameRate = 60;

        public int TargetFrameRate => m_TargetFrameRate < 30 ? 30 : m_TargetFrameRate;
    }
}
