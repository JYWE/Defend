using System;

namespace Defend.Simulation
{
    /// <summary>
    /// 将渲染帧经过的时间转换成固定逻辑步数。追赶达到上限后保留积压，暂停时既不追加也不处理时间。
    /// 时钟只决定执行次数；世界状态与实际能量由 GameWorld 和 EnergySystem 拥有。
    /// </summary>
    public sealed class SimulationClock
    {
        private readonly double secondsPerStep;
        private readonly int maximumStepsPerFrame;

        public double PendingSeconds { get; private set; }

        public SimulationClock(int ticksPerSecond = 20, int maximumStepsPerFrame = 8)
        {
            if (ticksPerSecond <= 0 || ticksPerSecond > 1000)
                throw new ArgumentOutOfRangeException(nameof(ticksPerSecond));
            if (maximumStepsPerFrame <= 0)
                throw new ArgumentOutOfRangeException(nameof(maximumStepsPerFrame));
            secondsPerStep = 1.0 / ticksPerSecond;
            this.maximumStepsPerFrame = maximumStepsPerFrame;
        }

        public int Advance(double elapsedSeconds, bool paused, double speed, Action step)
        {
            if (step == null)
                throw new ArgumentNullException(nameof(step));
            if (double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds) || elapsedSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            if (double.IsNaN(speed) || double.IsInfinity(speed) || speed < 0)
                throw new ArgumentOutOfRangeException(nameof(speed));
            if (paused || speed == 0)
                return 0;

            double accumulated = PendingSeconds + elapsedSeconds * speed;
            if (double.IsInfinity(accumulated))
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds), "累计模拟时间超出有效范围。");
            PendingSeconds = accumulated;
            int executed = 0;
            // 仅对时间累计的浮点尾差留容差；能量账本全程使用整数，不使用此容差。
            while (executed < maximumStepsPerFrame && PendingSeconds + 1e-10 >= secondsPerStep)
            {
                step();
                PendingSeconds = Math.Max(0, PendingSeconds - secondsPerStep);
                executed++;
            }
            return executed;
        }
    }
}
