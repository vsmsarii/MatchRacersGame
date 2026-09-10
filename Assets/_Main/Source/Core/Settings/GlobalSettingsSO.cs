using UnityEngine;

namespace CasualKit.Core
{
    [CreateAssetMenu(fileName = "GlobalSettings", menuName = "CasualKit/Global Settings")]
    public sealed class GlobalSettingsSO : ScriptableObject
    {
        [Header("Hearts")]
        [SerializeField, Min(0)] private int m_DefaultHeartCount = 5;
        [SerializeField, Min(0f)] private float m_HeartRefillMinutes = 30f;

        [Header("Performance")]
        [SerializeField, Min(30)] private int m_TargetFrameRate = 60;

        public int DefaultHeartCount => m_DefaultHeartCount < 0 ? 0 : m_DefaultHeartCount;
        public float HeartRefillMinutes => m_HeartRefillMinutes < 0f ? 0f : m_HeartRefillMinutes;
        public int TargetFrameRate => m_TargetFrameRate < 30 ? 30 : m_TargetFrameRate;
    }
}
