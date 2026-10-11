using System;

namespace Defend.Simulation
{
    /// <summary>
    /// 所有敌人共用按建筑占用版本失效的四方向距离场。
    /// 可通行区域优先走向核心；完全隔绝的区域攻击可接近且靠近核心的阻挡建筑。
    /// 拆障目标按整栋建筑选择，这是一种启发式，不声称得到最小拆除代价路线。
    /// </summary>
    internal sealed class EnemyNavigation
    {
        private static readonly int[] OffsetX = { 0, 1, 0, -1 };
        private static readonly int[] OffsetZ = { 1, 0, -1, 0 };
        private readonly GameWorld world;
        private readonly BuildingGrid grid;
        private readonly int[] component;
        private readonly int[] distance;
        private readonly long[] target;
        private readonly int[] regionQueue;
        private readonly int[] distanceQueue;
        private long revision = -1;
        private long coreIdentifier;

        public int RebuildCount { get; private set; }

        public EnemyNavigation(GameWorld world, BuildingGrid grid)
        {
            this.world = world;
            this.grid = grid;
            int cellCount = grid.Width * grid.Depth;
            component = new int[cellCount];
            distance = new int[cellCount];
            target = new long[cellCount];
            regionQueue = new int[cellCount];
            distanceQueue = new int[cellCount];
        }

        public long GetTarget(int x, int z, BuildingState core)
        {
            EnsureCurrent(core);
            return target[Index(x, z)];
        }

        public int GetDistance(int x, int z, BuildingState core)
        {
            EnsureCurrent(core);
            return Inside(x, z) && target[Index(x, z)] == core.Id ? distance[Index(x, z)] : -1;
        }

        public bool TryNextCell(int x, int z, BuildingState core, out int nextX, out int nextZ)
        {
            EnsureCurrent(core);
            nextX = x;
            nextZ = z;
            int current = Index(x, z);
            int bestDistance = distance[current];
            if (bestDistance <= 0) return false;
            for (int direction = 0; direction < 4; direction++)
            {
                int candidateX = x + OffsetX[direction];
                int candidateZ = z + OffsetZ[direction];
                if (!Inside(candidateX, candidateZ)) continue;
                int candidate = Index(candidateX, candidateZ);
                if (component[candidate] != component[current] || distance[candidate] < 0 || distance[candidate] >= bestDistance)
                    continue;
                bestDistance = distance[candidate];
                nextX = candidateX;
                nextZ = candidateZ;
            }
            return nextX != x || nextZ != z;
        }

        private void EnsureCurrent(BuildingState core)
        {
            if (revision == grid.Revision && coreIdentifier == core.Id) return;
            revision = grid.Revision;
            coreIdentifier = core.Id;
            RebuildCount++;
            for (int index = 0; index < component.Length; index++)
            {
                component[index] = -1;
                distance[index] = -1;
                target[index] = 0;
            }
            int componentIdentifier = 0;
            for (int z = 0; z < grid.Depth; z++)
            for (int x = 0; x < grid.Width; x++)
            {
                int first = Index(x, z);
                if (component[first] >= 0 || grid.Find(x, z) != 0) continue;
                BuildRegion(first, componentIdentifier++, core);
            }
        }

        private void BuildRegion(int first, int componentIdentifier, BuildingState core)
        {
            int regionCount = 1;
            regionQueue[0] = first;
            component[first] = componentIdentifier;
            long selectedTarget = 0;
            double selectedScore = double.MaxValue;
            for (int head = 0; head < regionCount; head++)
            {
                int current = regionQueue[head];
                int x = current % grid.Width;
                int z = current / grid.Width;
                for (int direction = 0; direction < 4; direction++)
                {
                    int adjacentX = x + OffsetX[direction];
                    int adjacentZ = z + OffsetZ[direction];
                    if (!Inside(adjacentX, adjacentZ)) continue;
                    long occupant = grid.Find(adjacentX, adjacentZ);
                    if (occupant < 0) continue; // 岩体不可拆除，也不能作为攻击目标。
                    if (occupant != 0)
                    {
                        BuildingState building = world.FindBuilding(occupant);
                        double score = occupant == core.Id ? -1 :
                            Math.Abs(building.CenterX - core.CenterX) + Math.Abs(building.CenterZ - core.CenterZ);
                        if (score < selectedScore || (score == selectedScore && occupant < selectedTarget))
                        {
                            selectedScore = score;
                            selectedTarget = occupant;
                        }
                        continue;
                    }
                    int adjacent = Index(adjacentX, adjacentZ);
                    if (component[adjacent] >= 0) continue;
                    component[adjacent] = componentIdentifier;
                    regionQueue[regionCount++] = adjacent;
                }
            }

            int count = 0;
            for (int index = 0; index < regionCount; index++)
            {
                int cell = regionQueue[index];
                target[cell] = selectedTarget;
                if (selectedTarget == 0) continue;
                int x = cell % grid.Width;
                int z = cell / grid.Width;
                for (int direction = 0; direction < 4; direction++)
                {
                    int adjacentX = x + OffsetX[direction];
                    int adjacentZ = z + OffsetZ[direction];
                    if (!Inside(adjacentX, adjacentZ) || grid.Find(adjacentX, adjacentZ) != selectedTarget) continue;
                    distance[cell] = 0;
                    distanceQueue[count++] = cell;
                    break;
                }
            }
            for (int head = 0; head < count; head++)
            {
                int current = distanceQueue[head];
                int x = current % grid.Width;
                int z = current / grid.Width;
                for (int direction = 0; direction < 4; direction++)
                {
                    int adjacentX = x + OffsetX[direction];
                    int adjacentZ = z + OffsetZ[direction];
                    if (!Inside(adjacentX, adjacentZ)) continue;
                    int adjacent = Index(adjacentX, adjacentZ);
                    if (component[adjacent] != componentIdentifier || distance[adjacent] >= 0) continue;
                    distance[adjacent] = distance[current] + 1;
                    distanceQueue[count++] = adjacent;
                }
            }
        }

        private bool Inside(int x, int z) => x >= 0 && z >= 0 && x < grid.Width && z < grid.Depth;
        private int Index(int x, int z) => z * grid.Width + x;
    }
}
