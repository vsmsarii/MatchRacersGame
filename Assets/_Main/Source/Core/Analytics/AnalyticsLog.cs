using UnityEngine;

namespace CasualKit.Core
{
    public sealed class AnalyticsLog : IAnalytics
    {
        public void MatchStart(int levelIndex)
        {
            Debug.Log("[Analytics] MatchStart level_index=" + levelIndex);
        }

        public void MatchWin(int levelIndex, float time)
        {
            Debug.Log("[Analytics] MatchWin level_index=" + levelIndex + " time=" + time.ToString("0.###"));
        }

        public void MatchLose(int levelIndex, float resultTime, int attemptCount)
        {
            Debug.Log("[Analytics] MatchLose level_index=" + levelIndex + " result_time=" + resultTime.ToString("0.###") + " attempt_count=" + attemptCount);
        }
    }
}
