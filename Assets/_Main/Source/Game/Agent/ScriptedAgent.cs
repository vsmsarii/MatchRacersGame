namespace MatchRacers
{
    public sealed class ScriptedAgent : IRaceAgent
    {
        private readonly int m_CarIndex;
        private readonly RaceScenarioSO m_Scenario;

        private int m_TimelineIndex;
        private float m_NextRepeatTime;
        private float m_RaceStartTime;
        private bool m_Started;

        public int CarIndex => m_CarIndex;

        public ScriptedAgent(int carIndex, RaceScenarioSO scenario)
        {
            m_CarIndex = carIndex;
            m_Scenario = scenario;
        }

        public void Reset(uint seed)
        {
            m_TimelineIndex = 0;
            m_RaceStartTime = 0f;
            m_Started = false;
            m_NextRepeatTime = m_Scenario != null ? m_Scenario.RepeatStartSeconds : 0f;
        }

        public int PollBuffKey(IRaceView view, float stepTime, float stepDelta)
        {
            if (m_Scenario == null || view.State != ERaceState.Racing)
                return 0;

            if (!m_Started)
            {
                m_Started = true;
                m_RaceStartTime = stepTime;
            }

            float raceTime = stepTime - m_RaceStartTime;

            if (m_Scenario.InputMode == EScenarioInputMode.Timeline)
                return PollTimeline(raceTime);

            if (m_Scenario.InputMode == EScenarioInputMode.RepeatingKey)
                return PollRepeating(raceTime);

            return 0;
        }

        private int PollTimeline(float stepTime)
        {
            ScenarioInputEntry[] timeline = m_Scenario.Timeline;
            if (timeline == null || m_TimelineIndex >= timeline.Length)
                return 0;

            if (timeline[m_TimelineIndex].TimeSeconds > stepTime)
                return 0;

            return timeline[m_TimelineIndex++].Key;
        }

        private int PollRepeating(float stepTime)
        {
            if (stepTime < m_Scenario.RepeatStartSeconds || stepTime > m_Scenario.RepeatEndSeconds)
                return 0;

            if (stepTime < m_NextRepeatTime)
                return 0;

            m_NextRepeatTime = stepTime + m_Scenario.RepeatIntervalSeconds;
            return m_Scenario.RepeatKey;
        }
    }
}
