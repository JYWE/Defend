using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.Profiling;
using UnityEditorInternal;

/// <summary>离线读取独立开发包的 CPU 采样；与无 Profiler 的帧率验收分开。</summary>
public static class ReadCombatProfile
{
    public static object Read(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("没有找到 CPU 采样文件。", path);
        if (ProfilerDriver.enabled) throw new InvalidOperationException("编辑器正在录制 Profiler；请先结束本次录制，避免混合数据。");
        string previous = Path.Combine(Path.GetDirectoryName(path), "editor-profile-before-inspection.data");
        bool saved = ProfilerDriver.firstFrameIndex >= 0 && ProfilerDriver.SaveProfile(previous);
        try
        {
            if (!ProfilerDriver.LoadProfile(path, false)) throw new InvalidOperationException("Profiler 文件加载失败。");
            int first = ProfilerDriver.firstFrameIndex, last = ProfilerDriver.lastFrameIndex;
            var steps = new List<double>();
            var frames = new List<double>();
            var markers = new Dictionary<string, Marker>();
            string thread = "";
            for (int frame = first; frame <= last; frame++)
            {
                using (RawFrameDataView data = ProfilerDriver.GetRawFrameDataView(frame, 0))
                {
                    if (!data.valid) continue;
                    thread = data.threadName;
                    frames.Add(data.frameTimeMs);
                    for (int sample = 0; sample < data.sampleCount; sample++)
                    {
                        string name = data.GetSampleName(sample);
                        double milliseconds = data.GetSampleTimeMs(sample);
                        if (!markers.TryGetValue(name, out Marker marker)) markers[name] = marker = new Marker { name = name };
                        marker.calls++;
                        marker.totalInclusiveMilliseconds += milliseconds;
                        marker.maximumMilliseconds = Math.Max(marker.maximumMilliseconds, milliseconds);
                        if (name == "Defend.Simulation.Step") steps.Add(milliseconds);
                    }
                }
            }
            steps.Sort(); frames.Sort();
            return new { path, firstFrame = first, lastFrame = last, validFrames = frames.Count, thread,
                simulationSamples = steps.Count, simulationP50 = Percentile(steps, 0.5),
                simulationP95 = Percentile(steps, 0.95), simulationP99 = Percentile(steps, 0.99),
                capturedFrameP95 = Percentile(frames, 0.95),
                markers = markers.Values.OrderByDescending(item => item.totalInclusiveMilliseconds).Take(25).ToArray(),
                garbageCollectionMarkers = markers.Values.Where(item => item.name.Contains("GC.")).ToArray(),
                note = "标记时间包含子标记，不能相加。Profiler 录制本身有开销；不作为常规帧率验收结果。" };
        }
        finally { if (saved) ProfilerDriver.LoadProfile(previous, false); }
    }

    private static double Percentile(List<double> values, double p)
        => values.Count == 0 ? 0 : values[Math.Min(values.Count - 1, (int)Math.Ceiling(values.Count * p) - 1)];
    public sealed class Marker
    {
        public string name;
        public int calls;
        public double totalInclusiveMilliseconds, maximumMilliseconds;
    }
}
