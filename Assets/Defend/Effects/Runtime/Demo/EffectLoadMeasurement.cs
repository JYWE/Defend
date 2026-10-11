using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace Defend.PhaseFiveEffects
{
    /// <summary>Short opt-in Editor Play Mode workload, not a visible-player frame-rate certification.</summary>
    public sealed class EffectLoadMeasurement : MonoBehaviour
    {
        [Serializable] public sealed class Result
        {
            public string context = "Unity Editor Play Mode; offscreen/unfocused timing is not visible 60 FPS evidence";
            public int frames, created, reused, peakActive, peakParticles, dropped, finalActive, finalParticles;
            public int[] peakBudget;
            public double meanFrameMilliseconds, p95FrameMilliseconds, maximumFrameMilliseconds;
            public double meanPresentationCpuMilliseconds, maximumPresentationCpuMilliseconds;
            public long presentationAllocatedBytes;
            public bool cleanupPassed;
        }
        public EffectShowcase Demo;
        public bool Running;
        public Result LastResult;
        double[] frameTimes = new double[600], cpuTimes = new double[600];
        int frame, warmup;
        long allocated;
        readonly Stopwatch stopwatch = new Stopwatch();
        public void Begin()
        {
            Demo.Stop(); Demo.ExternalClock = true; Demo.Pool.Paused = false;
            frame = 0; allocated = 0; warmup = 8;
            Burst(); Demo.Pool.StopAll(); Demo.Pool.ResetMetrics();
            Running = true;
        }
        void Burst()
        {
            var pool = Demo.Pool;
            for (int i = 0; i < 80; i++)
            {
                Vector3 position = new Vector3((i % 10 - 5) * .35f, .15f, (i / 10 - 4) * .35f);
                var request = new EffectRequest { Origin = position, Destination = position, Rotation = Quaternion.identity, Intensity = 1, Seed = i + 1, ConfirmedTransfer = true };
                pool.TryPlay("AreaImpact", request);
            }
            for (int i = 0; i < 20; i++)
            {
                var request = new EffectRequest { Origin = Vector3.zero, Destination = Vector3.zero, Rotation = Quaternion.identity, Intensity = 1, Seed = i + 1 };
                pool.TryPlay("EnemyDeath", request);
            }
            for (int i = 0; i < 144; i++)
            {
                var request = new EffectRequest { Origin = new Vector3(-1,.6f,0), Destination = new Vector3(1,.6f,0), Rotation = Quaternion.identity, Intensity = 1, Seed = i + 1, ConfirmedTransfer = true };
                pool.TryPlay("EnergyTransfer", request);
            }
        }
        void Update()
        {
            if (!Running) return;
            if (warmup-- > 0) return;
            frameTimes[frame] = Time.unscaledDeltaTime * 1000d;
            long before = GC.GetAllocatedBytesForCurrentThread(); stopwatch.Restart();
            if (frame % 30 == 0) { Demo.Pool.StopAll(); Burst(); }
            Demo.Pool.Advance(Time.deltaTime);
            stopwatch.Stop(); allocated += GC.GetAllocatedBytesForCurrentThread() - before;
            cpuTimes[frame] = stopwatch.Elapsed.TotalMilliseconds;
            if (++frame < frameTimes.Length) return;
            Running = false; Demo.Pool.StopAll();
            Array.Sort(frameTimes); double frameSum = 0, cpuSum = 0, cpuMax = 0;
            for (int i=0;i<frame;i++) { frameSum+=frameTimes[i]; cpuSum+=cpuTimes[i]; cpuMax=Math.Max(cpuMax,cpuTimes[i]); }
            var pool=Demo.Pool;
            LastResult=new Result { frames=frame, created=pool.Created, reused=pool.Reused, peakActive=pool.PeakActive, peakParticles=pool.PeakParticles, dropped=pool.Dropped, peakBudget=(int[])pool.PeakBudget.Clone(), meanFrameMilliseconds=frameSum/frame, p95FrameMilliseconds=frameTimes[(int)(frame*.95)-1], maximumFrameMilliseconds=frameTimes[frame-1], meanPresentationCpuMilliseconds=cpuSum/frame, maximumPresentationCpuMilliseconds=cpuMax, presentationAllocatedBytes=allocated, finalActive=pool.ActiveCount, finalParticles=pool.ParticleCount, cleanupPassed=pool.ActiveCount==0&&pool.ParticleCount==0 };
            Directory.CreateDirectory("Docs/Art/PhaseFiveEffects/Checks");
            File.WriteAllText("Docs/Art/PhaseFiveEffects/Checks/PlayModeLoad.json",JsonUtility.ToJson(LastResult,true));
            Demo.ExternalClock=false; Demo.Restart();
        }
    }
}
