using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CasualKit.Core;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;

namespace MatchRacers
{
    public sealed class RacePerformanceProbe : IDisposable
    {
        public static readonly ProfilerMarker SimMarker = new ProfilerMarker("MatchRacers.Sim");
        public static readonly ProfilerMarker ViewsMarker = new ProfilerMarker("MatchRacers.Views");
        public static readonly ProfilerMarker CameraMarker = new ProfilerMarker("MatchRacers.Camera");
        public static readonly ProfilerMarker VfxMarker = new ProfilerMarker("MatchRacers.Vfx");
        public static readonly ProfilerMarker FeedbackMarker = new ProfilerMarker("MatchRacers.Feedback");
        public static readonly ProfilerMarker HudMarker = new ProfilerMarker("MatchRacers.Hud");
        public static readonly ProfilerMarker DebugPanelMarker = new ProfilerMarker("MatchRacers.DebugPanel");
        public static readonly ProfilerMarker TrackRenderMarker = new ProfilerMarker("MatchRacers.TrackRender");
        public static readonly ProfilerMarker RecorderMarker = new ProfilerMarker("MatchRacers.Recorder");

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const float TargetFrameMs = 1000f / 60f;
        private const float HitchFrameMs = 50f;
        private const float SpikeFrameMs = 100f;
        private const double ReportThresholdMs = 0.5;
        private const int MaxSpikes = 64;
        private const string GcAllocatedName = "GC Allocated In Frame";

        private static readonly string[] SourceLabels =
        {
            "mainThread", "waitForGpu", "renderLoop", "scripts", "gcCollect", "shaderCompile", "assetLoad",
            "uiCanvas", "particles", "sim", "views", "camera", "vfx", "feedback", "hud", "debugPanel",
            "trackRender", "recorder"
        };

        private static readonly string[] SourceMarkers =
        {
            "Main Thread", "Gfx.WaitForPresentOnGfxThread", "RenderPipelineManager.DoRenderLoop_Internal",
            "BehaviourUpdate", "GC.Collect", "Shader.CreateGPUProgram", "Loading.ReadObject",
            "Canvas.SendWillRenderCanvases", "ParticleSystem.Update", "MatchRacers.Sim", "MatchRacers.Views",
            "MatchRacers.Camera", "MatchRacers.Vfx", "MatchRacers.Feedback", "MatchRacers.Hud",
            "MatchRacers.DebugPanel", "MatchRacers.TrackRender", "MatchRacers.Recorder"
        };

        private sealed class Source
        {
            public string Label;
            public ProfilerRecorder Recorder;
            public double SumMs;
            public double MaxMs;
            public double LastMs;
        }

        private readonly List<float> m_FrameMs = new List<float>(8192);
        private readonly List<Source> m_Sources = new List<Source>();
        private readonly List<Source> m_SpikeOrder = new List<Source>();
        private readonly List<string> m_Spikes = new List<string>();
        private readonly FrameTiming[] m_Timings = new FrameTiming[1];
        private ProfilerRecorder m_GcAllocated;
        private float m_WarmupRemaining = 0.5f;
        private float m_Elapsed;
        private double m_GpuSumMs;
        private double m_GpuMaxMs;
        private int m_GpuSamples;
        private double m_GcAllocatedSumKb;
        private int m_Hitches;

        public int SampleCount => m_FrameMs.Count;

        public RacePerformanceProbe()
        {
            Dictionary<string, ProfilerRecorderHandle> handles = FindHandles();

            for (int i = 0; i < SourceLabels.Length; i++)
            {
                Source source = new Source { Label = SourceLabels[i] };
                if (handles.TryGetValue(SourceMarkers[i], out ProfilerRecorderHandle handle))
                {
                    source.Recorder = new ProfilerRecorder(handle, 1, ProfilerRecorderOptions.Default);
                    source.Recorder.Start();
                }

                m_Sources.Add(source);
            }

            if (handles.TryGetValue(GcAllocatedName, out ProfilerRecorderHandle allocated))
            {
                m_GcAllocated = new ProfilerRecorder(allocated, 1, ProfilerRecorderOptions.Default);
                m_GcAllocated.Start();
            }
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (unscaledDeltaTime <= 0f)
                return;

            FrameTimingManager.CaptureFrameTimings();

            if (m_WarmupRemaining > 0f)
            {
                m_WarmupRemaining -= unscaledDeltaTime;
                return;
            }

            float frameMs = unscaledDeltaTime * 1000f;
            m_FrameMs.Add(frameMs);
            m_Elapsed += unscaledDeltaTime;

            if (frameMs >= HitchFrameMs)
                m_Hitches++;

            for (int i = 0; i < m_Sources.Count; i++)
            {
                Source source = m_Sources[i];
                source.LastMs = source.Recorder.Valid ? source.Recorder.LastValue * 1e-6 : 0.0;
                source.SumMs += source.LastMs;
                if (source.LastMs > source.MaxMs)
                    source.MaxMs = source.LastMs;
            }

            double gpuMs = ReadGpuMs();
            if (gpuMs > 0.0)
            {
                m_GpuSumMs += gpuMs;
                m_GpuSamples++;
                if (gpuMs > m_GpuMaxMs)
                    m_GpuMaxMs = gpuMs;
            }

            double allocatedKb = m_GcAllocated.Valid ? m_GcAllocated.LastValue / 1024.0 : 0.0;
            m_GcAllocatedSumKb += allocatedKb;

            if (frameMs >= SpikeFrameMs)
                RecordSpike(frameMs, gpuMs, allocatedKb);
        }

        public void Reset()
        {
            m_FrameMs.Clear();
            m_Spikes.Clear();
            m_WarmupRemaining = 0.5f;
            m_Elapsed = 0f;
            m_GpuSumMs = 0.0;
            m_GpuMaxMs = 0.0;
            m_GpuSamples = 0;
            m_GcAllocatedSumKb = 0.0;
            m_Hitches = 0;

            for (int i = 0; i < m_Sources.Count; i++)
            {
                m_Sources[i].SumMs = 0.0;
                m_Sources[i].MaxMs = 0.0;
                m_Sources[i].LastMs = 0.0;
            }
        }

        public void Dispose()
        {
            for (int i = 0; i < m_Sources.Count; i++)
                m_Sources[i].Recorder.Dispose();

            m_GcAllocated.Dispose();
        }

        public string BuildCsv()
        {
            StringBuilder csv = new StringBuilder(4096);
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
            csv.Append("editor,").AppendLine(Application.isEditor ? "1" : "0");
            csv.Append("frameSamples,").AppendLine(m_FrameMs.Count.ToString(Inv));

            if (m_FrameMs.Count == 0)
                return csv.ToString();

            float[] sorted = m_FrameMs.ToArray();
            Array.Sort(sorted);

            float sum = 0f;
            int overBudget = 0;
            for (int i = 0; i < sorted.Length; i++)
            {
                sum += sorted[i];
                if (sorted[i] > TargetFrameMs)
                    overBudget++;
            }

            float mean = sum / sorted.Length;
            int frames = sorted.Length;

            csv.Append("averageFps,").AppendLine((1000f / mean).ToString("0.0", Inv));
            csv.Append("meanFrameMs,").AppendLine(mean.ToString("0.000", Inv));
            csv.Append("medianFrameMs,").AppendLine(Percentile(sorted, 0.50f).ToString("0.000", Inv));
            csv.Append("p95FrameMs,").AppendLine(Percentile(sorted, 0.95f).ToString("0.000", Inv));
            csv.Append("p99FrameMs,").AppendLine(Percentile(sorted, 0.99f).ToString("0.000", Inv));
            csv.Append("worstFrameMs,").AppendLine(sorted[sorted.Length - 1].ToString("0.000", Inv));
            csv.Append("framesOver16_67ms,").AppendLine(overBudget.ToString(Inv));
            csv.Append("percentOverBudget,").AppendLine((100f * overBudget / frames).ToString("0.00", Inv));
            csv.Append("framesOver50ms,").AppendLine(m_Hitches.ToString(Inv));
            csv.Append("gcAllocatedKbPerFrame,").AppendLine((m_GcAllocatedSumKb / frames).ToString("0.00", Inv));

            if (m_GpuSamples > 0)
            {
                csv.Append("gpuMeanMs,").AppendLine((m_GpuSumMs / m_GpuSamples).ToString("0.000", Inv));
                csv.Append("gpuMaxMs,").AppendLine(m_GpuMaxMs.ToString("0.000", Inv));
            }

            for (int i = 0; i < m_Sources.Count; i++)
            {
                Source source = m_Sources[i];
                if (!source.Recorder.Valid)
                {
                    csv.Append("mean_").Append(source.Label).AppendLine("Ms,unavailable");
                    continue;
                }

                csv.Append("mean_").Append(source.Label).Append("Ms,").AppendLine((source.SumMs / frames).ToString("0.000", Inv));
                csv.Append("max_").Append(source.Label).Append("Ms,").AppendLine(source.MaxMs.ToString("0.000", Inv));
            }

            csv.Append("gcIncremental,").AppendLine(UnityEngine.Scripting.GarbageCollector.isIncremental ? "1" : "0");

            for (int i = 0; i < m_Spikes.Count; i++)
                csv.Append("spike").Append((i + 1).ToString(Inv)).Append(',').AppendLine(Sanitize(m_Spikes[i]));

            return csv.ToString();
        }

        private void RecordSpike(float frameMs, double gpuMs, double allocatedKb)
        {
            StringBuilder line = new StringBuilder(256);
            line.Append("t ").Append(m_Elapsed.ToString("0.00", Inv)).Append(" s; frame ")
                .Append(frameMs.ToString("0", Inv)).Append(" ms");

            if (gpuMs > 0.0)
                line.Append("; gpu ").Append(gpuMs.ToString("0.0", Inv));

            line.Append("; gcAlloc ").Append(allocatedKb.ToString("0", Inv)).Append(" KB");

            m_SpikeOrder.Clear();
            for (int i = 0; i < m_Sources.Count; i++)
            {
                if (m_Sources[i].LastMs >= ReportThresholdMs)
                    m_SpikeOrder.Add(m_Sources[i]);
            }

            m_SpikeOrder.Sort((a, b) => b.LastMs.CompareTo(a.LastMs));

            for (int i = 0; i < m_SpikeOrder.Count; i++)
                line.Append("; ").Append(m_SpikeOrder[i].Label).Append(' ').Append(m_SpikeOrder[i].LastMs.ToString("0.0", Inv));

            string text = line.ToString();
            if (m_Spikes.Count < MaxSpikes)
                m_Spikes.Add(text);

            EditorLog.Warning("Frame spike: " + text);
        }

        private double ReadGpuMs()
        {
            if (FrameTimingManager.GetLatestTimings(1, m_Timings) == 0)
                return 0.0;

            return m_Timings[0].gpuFrameTime;
        }

        private static Dictionary<string, ProfilerRecorderHandle> FindHandles()
        {
            List<ProfilerRecorderHandle> available = new List<ProfilerRecorderHandle>(8192);
            ProfilerRecorderHandle.GetAvailable(available);

            Dictionary<string, ProfilerRecorderHandle> handles = new Dictionary<string, ProfilerRecorderHandle>(available.Count);
            for (int i = 0; i < available.Count; i++)
            {
                string name = ProfilerRecorderHandle.GetDescription(available[i]).Name;
                if (!string.IsNullOrEmpty(name) && !handles.ContainsKey(name))
                    handles.Add(name, available[i]);
            }

            return handles;
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
