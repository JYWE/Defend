using System;

namespace Defend.Simulation
{
    /// <summary>所有权威数量使用整数；每一跳只计算一次取整损失。</summary>
    internal static class EnergyArithmetic
    {
        public const long EfficiencyScale = 1000000;
        public const long MaximumAmount = long.MaxValue / EfficiencyScale;

        public static long Receive(long sent, long efficiency)
        {
            if (sent < 0 || sent > MaximumAmount) throw new ArgumentOutOfRangeException(nameof(sent));
            return checked(sent * efficiency) / EfficiencyScale;
        }

        public static long Predict(long sent, EnergyRoute route, int firstEdge = 0)
        {
            for (int index = firstEdge; index < route.Edges.Length; index++)
                sent = Receive(sent, route.Edges[index].Efficiency);
            return sent;
        }

        // 先找到能够提供的有效量，再倒推最少发出量，避免在取整平台上白白多扣库存。
        public static long LimitSend(long available, long effectiveLimit, EnergyRoute route, int firstEdge = 0)
        {
            if (available <= 0 || effectiveLimit <= 0) return 0;
            long lower = 0;
            long upper = Math.Min(available, MaximumAmount);
            while (lower < upper)
            {
                long middle = lower + (upper - lower + 1) / 2;
                if (Predict(middle, route, firstEdge) <= effectiveLimit) lower = middle;
                else upper = middle - 1;
            }
            long required = Predict(lower, route, firstEdge);
            if (required <= 0) return 0;
            for (int index = route.Edges.Length - 1; index >= firstEdge; index--)
            {
                long efficiency = route.Edges[index].Efficiency;
                long product = checked(required * EfficiencyScale);
                required = product / efficiency + (product % efficiency == 0 ? 0 : 1);
            }
            return required;
        }
    }
}
