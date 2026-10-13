using System.Collections.Generic;

namespace Defend.Simulation
{
    internal sealed class EnemyStrategicPath
    {
        internal long BuildingId, Revision;
        internal int X, Z, Width, Depth;
        internal int[] Distances;
    }

    /// <summary>同一已知占地共享反向距离场；隐藏移动后的新坐标不参与旧位置寻路。</summary>
    internal sealed class EnemyStrategicNavigation
    {
        private const int MaximumCachedPaths = 128;
        private static readonly int[] OffsetX = { 0, 1, 0, -1 };
        private static readonly int[] OffsetZ = { 1, 0, -1, 0 };
        private readonly BuildingGrid grid;
        private readonly EnemyBehaviorSettings settings;
        private readonly EnemyBehaviorStatistics statistics;
        private readonly List<EnemyStrategicPath> paths = new List<EnemyStrategicPath>();
        private readonly int[] queue;
        private long revision = -1;

        internal EnemyStrategicNavigation(BuildingGrid grid, EnemyBehaviorSettings settings, EnemyBehaviorStatistics statistics)
        {
            this.grid = grid;
            this.settings = settings;
            this.statistics = statistics;
            queue = new int[grid.Width * grid.Depth];
        }

        internal void BeginStep()
        {
            if (revision != grid.Revision)
            {
                paths.Clear();
                revision = grid.Revision;
            }
            statistics.CachedPathCount = paths.Count;
        }

        internal EnemyStrategicPath Get(EnemyBuildingObservation observation)
        {
            foreach (var path in paths)
                if (path.BuildingId == observation.Id && path.X == observation.X && path.Z == observation.Z &&
                    path.Width == observation.Width && path.Depth == observation.Depth) return path;
            if (statistics.PathBuildsThisTick >= settings.MaximumPathBuildsPerTick) return null;
            var result = new EnemyStrategicPath
            {
                BuildingId = observation.Id, Revision = grid.Revision,
                X = observation.X, Z = observation.Z, Width = observation.Width, Depth = observation.Depth,
                Distances = new int[queue.Length]
            };
            Build(result);
            if (paths.Count == MaximumCachedPaths) paths.RemoveAt(0);
            paths.Add(result);
            statistics.PathBuildsThisTick++;
            statistics.TotalPathBuilds++;
            statistics.CachedPathCount = paths.Count;
            return result;
        }

        internal bool IsCurrent(EnemyStrategicPath path) => path != null && path.Revision == grid.Revision;

        internal int Distance(EnemyStrategicPath path, int x, int z) =>
            IsCurrent(path) && Inside(x, z) ? path.Distances[Index(x, z)] : -1;

        internal bool TryNext(EnemyStrategicPath path, int x, int z, out int nextX, out int nextZ)
        {
            nextX = x;
            nextZ = z;
            int best = Distance(path, x, z);
            if (best <= 0) return false;
            for (int direction = 0; direction < 4; direction++)
            {
                int candidateX = x + OffsetX[direction], candidateZ = z + OffsetZ[direction];
                int candidate = Distance(path, candidateX, candidateZ);
                if (candidate < 0 || candidate >= best || grid.Find(candidateX, candidateZ) != 0) continue;
                best = candidate;
                nextX = candidateX;
                nextZ = candidateZ;
            }
            return nextX != x || nextZ != z;
        }

        private void Build(EnemyStrategicPath path)
        {
            for (int index = 0; index < path.Distances.Length; index++) path.Distances[index] = -1;
            int count = 0;
            // 目标仍在时落点为占地外相邻格；旧位置已经腾空时允许进入旧占地完成搜索。
            for (int z = path.Z - 1; z <= path.Z + path.Depth; z++)
            for (int x = path.X - 1; x <= path.X + path.Width; x++)
            {
                if (!Inside(x, z) || grid.Find(x, z) != 0) continue;
                bool withinX = x >= path.X && x < path.X + path.Width;
                bool withinZ = z >= path.Z && z < path.Z + path.Depth;
                if (!withinX && !withinZ) continue;
                int cell = Index(x, z);
                path.Distances[cell] = 0;
                queue[count++] = cell;
            }
            for (int head = 0; head < count; head++)
            {
                int current = queue[head], x = current % grid.Width, z = current / grid.Width;
                for (int direction = 0; direction < 4; direction++)
                {
                    int adjacentX = x + OffsetX[direction], adjacentZ = z + OffsetZ[direction];
                    if (!Inside(adjacentX, adjacentZ) || grid.Find(adjacentX, adjacentZ) != 0) continue;
                    int adjacent = Index(adjacentX, adjacentZ);
                    if (path.Distances[adjacent] >= 0) continue;
                    path.Distances[adjacent] = path.Distances[current] + 1;
                    queue[count++] = adjacent;
                }
            }
        }

        private bool Inside(int x, int z) => x >= 0 && z >= 0 && x < grid.Width && z < grid.Depth;
        private int Index(int x, int z) => z * grid.Width + x;
    }
}
