using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace MatchRacers
{
    public sealed class RacePerformanceProbe
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const float TargetFrameMs = 1000f / 60f;

        private readonly List<float> m_FrameMs = new List<float>(8192);
        private float m_WarmupRemaining = 0.5f;

        public int SampleCount => m_FrameMs.Count;

        public void Tick(float unscaledDeltaTime)
        {
            if (unscaledDeltaTime <= 0f)
                return;

            if (m_WarmupRemaining > 0f)
            {
                m_WarmupRemaining -= unscaledDeltaTime;
                return;
            }

            m_FrameMs.Add(unscaledDeltaTime * 1000f);
        }

        public void Reset()
        {
            m_FrameMs.Clear();
            m_WarmupRemaining = 0.5f;
        }

        public string BuildCsv()
        {
            StringBuilder csv = new StringBuilder(1024);
            csv.AppendLine("metric,value");
            csv.Append("device,").AppendLine(Sanitize(SystemInfo.deviceModel));
            csv.Append("cpu,").AppendLine(Sanitize(SystemInfo.processorType));
            csv.Append("cpuThreads,").AppendLine(SystemInfo.processorCount.ToString(Inv));
            csv.Append("gpu,").AppendLine(Sanitize(SystemInfo.graphicsDeviceName));
            csv.Append("graphicsApi,").AppendLine(Sanitize(SystemInfo.graphicsDeviceType.ToString()));
            csv.Append("systemMemoryMb,").AppendLine(SystemInfo.systemMemorySize.ToString(Inv));
            csv.Append("resolution,").Append(Screen.width.ToString(Inv)).Append('x')
                .AppendLine(Screen.height.ToString(Inv));
            csv.Append("fullscreen,").AppendLine(Screen.fullScreenMode.ToString());
            csv.Append("qualityLevel,").AppendLine(Sanitize(QualitySettings.names[QualitySettings.GetQualityLevel()]));
            csv.Append("vSyncCount,").AppendLine(QualitySettings.vSyncCount.ToString(Inv));
            csv.Append("targetFrameRate,").AppendLine(Application.targetFrameRate.ToString(Inv));
            csv.Append("carCount,").AppendLine(RaceConfigSO.CarCount.ToString(Inv));
            csv.Append("frameSamples,").AppendLine(m_FrameMs.Count.ToString(Inv));

            if (m_FrameMs.Count == 0)
                return csv.ToString();

            float[] sorted = m_FrameMs.ToArray();
            System.Array.Sort(sorted);

            float sum = 0f;
            int overBudget = 0;
            for (int i = 0; i < sorted.Length; i++)
            {
                sum += sorted[i];
                if (sorted[i] > TargetFrameMs)
                    overBudget++;
            }

            float mean = sum / sorted.Length;

            csv.Append("averageFps,").AppendLine((1000f / mean).ToString("0.0", Inv));
            csv.Append("meanFrameMs,").AppendLine(mean.ToString("0.000", Inv));
            csv.Append("medianFrameMs,").AppendLine(Percentile(sorted, 0.50f).ToString("0.000", Inv));
            csv.Append("p95FrameMs,").AppendLine(Percentile(sorted, 0.95f).ToString("0.000", Inv));
            csv.Append("p99FrameMs,").AppendLine(Percentile(sorted, 0.99f).ToString("0.000", Inv));
            csv.Append("worstFrameMs,").AppendLine(sorted[sorted.Length - 1].ToString("0.000", Inv));
            csv.Append("framesOver16_67ms,").AppendLine(overBudget.ToString(Inv));
            csv.Append("percentOverBudget,")
                .AppendLine((100f * overBudget / sorted.Length).ToString("0.00", Inv));

            return csv.ToString();
        }

        private static float Percentile(float[] sorted, float fraction)
        {
            int index = Mathf.Clamp(Mathf.RoundToInt(fraction * (sorted.Length - 1)), 0, sorted.Length - 1);
            return sorted[index];
        }

        private static string Sanitize(string value)
        {
            return string.IsNullOrEmpty(value) ? "unknown" : value.Replace(',', ' ');
        }
    }
}
