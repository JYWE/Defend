using System;

namespace Defend.Simulation
{
    public sealed class WorldSettings
    {
        public int Width { get; }
        public int Depth { get; }
        public int TicksPerSecond { get; }
        public int StartingCredits { get; }

        public WorldSettings(int width = 40, int depth = 40, int ticksPerSecond = 20, int startingCredits = 2000)
        {
            if (width <= 0 || width > 512 || depth <= 0 || depth > 512)
                throw new ArgumentOutOfRangeException(nameof(width), "白盒地图边长必须在 1 到 512 格之间。");
            if (ticksPerSecond <= 0 || ticksPerSecond > 1000)
                throw new ArgumentOutOfRangeException(nameof(ticksPerSecond));
            if (startingCredits < 0)
                throw new ArgumentOutOfRangeException(nameof(startingCredits));
            Width = width;
            Depth = depth;
            TicksPerSecond = ticksPerSecond;
            StartingCredits = startingCredits;
        }
    }
}
