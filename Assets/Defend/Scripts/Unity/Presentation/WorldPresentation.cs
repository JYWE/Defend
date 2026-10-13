using System.Collections.Generic;
using Defend.Simulation;
using UnityEngine;
using UnityEngine.Pool;

namespace Defend.Unity
{
    /// <summary>
    /// 统一同步建筑、连线和运输表现，
    /// 防止每个能量物体各自执行 Update。
    /// </summary>
    public sealed class WorldPresentation : MonoBehaviour
    {
        [SerializeField] private BuildingView[] buildingPrefabs;
        [SerializeField] private bool externalTransferEffects;

        [SerializeField] private BuildingView[] authoredBuildings;
        [SerializeField] private EnergyTransferView transferPrefab;
        [SerializeField] private LineRenderer connectionPrefab;

        [SerializeField] private Transform buildingRoot;
        [SerializeField] private Transform energyRoot;
        [SerializeField] private Transform connectionRoot;

        [SerializeField] private Transform placementPreview;
        [SerializeField] private Renderer previewRenderer;
        [SerializeField] private LineRenderer selectionRange;
        [SerializeField] private LineRenderer previewRange;

        public BuildingView FindView(long id)
        {
            return buildingViews.TryGetValue(
                id,
                out BuildingView view)
                    ? view
                    : null;
        }

        public void ConfigureRanges(
            LineRenderer selected,
            LineRenderer preview)
        {
            selectionRange = selected;
            previewRange = preview;
        }

        private readonly Dictionary<long, BuildingView>
            buildingViews =
                new Dictionary<long, BuildingView>();

        private readonly List<long>
            removedBuildings =
                new List<long>();

        private readonly List<EnergyTransferView>
            transferViews =
                new List<EnergyTransferView>();

        private readonly List<LineRenderer>
            connectionViews =
                new List<LineRenderer>();

        private ObjectPool<EnergyTransferView>
            transferPool;

        private readonly Dictionary<
            BuildingKind,
            ObjectPool<BuildingView>>
            buildingPools =
                new Dictionary<
                    BuildingKind,
                    ObjectPool<BuildingView>>();

        private readonly HashSet<BuildingView>
            authoredViews =
                new HashSet<BuildingView>();

        // 与对象池、身份映射同属单次播放会话。
        [System.NonSerialized]
        private Vector3[] authoredPositions;

        private MaterialPropertyBlock previewProperties;
        private MaterialPropertyBlock connectionProperties;

        private int maximumVisibleTransfers;
        private int maximumVisibleConnections;

        private long lastAppliedTick = -1;
        private long lastSelectedId = -1;
<<<<<<< HEAD
=======
        public IReadOnlyList<BuildingView> AuthoredBuildings => authoredBuildings;
        public void Invalidate() { lastAppliedTick = -1; }
        public bool ShowNetwork { get; set; } = true;
        public bool ShowRanges { get; set; } = true;
>>>>>>> upstream/main

        /*
         * 连接线缓存。
         *
         * 每个数组的下标对应 connectionViews 中的下标。
         * connectionViews[index] 对应当前能源连接列表中的第 index 条连接。
         */
        private long[] cachedConnectionSourceIds =
            new long[0];

        private long[] cachedConnectionTargetIds =
            new long[0];

        private Vector3[] cachedConnectionStartPositions =
            new Vector3[0];

        private Vector3[] cachedConnectionEndPositions =
            new Vector3[0];

        private bool[] cachedConnectionRelated =
            new bool[0];

        private bool[] cachedConnectionActive =
            new bool[0];

        public IReadOnlyList<BuildingView>
            AuthoredBuildings =>
                authoredBuildings;

        public void Configure(
            BuildingView[] prefabs,
            BuildingView[] placed,
            EnergyTransferView energyPrefab,
            LineRenderer linePrefab,
            Transform buildings,
            Transform energy,
            Transform connections,
            Transform preview,
            Renderer previewShape)
        {
            buildingPrefabs = prefabs;
            authoredBuildings = placed;
            transferPrefab = energyPrefab;
            connectionPrefab = linePrefab;
            buildingRoot = buildings;
            energyRoot = energy;
            connectionRoot = connections;
            placementPreview = preview;
            previewRenderer = previewShape;
        }

        public void Initialize(
            GameWorld world,
            EnergyExperimentSettings settings)
        {
            if (authoredBuildings == null ||
                authoredBuildings.Length == 0)
            {
                throw new System.InvalidOperationException(
                    "初始建筑列表为空，请在编辑模式配置场景中的建筑引用。");
            }

            if (authoredPositions != null &&
                authoredPositions.Length != authoredBuildings.Length)
            {
                throw new System.InvalidOperationException(
                    "播放期间初始建筑列表发生变化。请退出播放后编辑布局，再重新进入播放；不能用运行中的位置替换初始布局。");
            }

            if (authoredPositions == null)
            {
                authoredPositions =
                    new Vector3[authoredBuildings.Length];

                for (int index = 0;
                     index < authoredBuildings.Length;
                     index++)
                {
                    if (authoredBuildings[index] == null)
                    {
                        throw new System.InvalidOperationException(
                            "初始建筑引用丢失，请检查 authoredBuildings 第 " +
                            (index + 1) +
                            " 项。");
                    }

                    authoredPositions[index] =
                        authoredBuildings[index].transform.position;

                    authoredViews.Add(
                        authoredBuildings[index]);
                }

                foreach (BuildingView prefab
                         in buildingPrefabs)
                {
                    BuildingView captured =
                        prefab;

                    buildingPools.Add(
                        prefab.Definition.Kind,
                        new ObjectPool<BuildingView>(
                            () =>
                                Instantiate(
                                    captured,
                                    buildingRoot),

                            view =>
                            {
                                view.ResetForReuse();
                                view.gameObject.SetActive(true);
                            },

                            view =>
                            {
                                view.gameObject.SetActive(false);
                            },

                            view =>
                            {
                                if (view != null)
                                {
                                    Destroy(view.gameObject);
                                }
                            },

                            false,
                            4,
                            256));
                }
            }

            // 初始场景实例保留，动态实例归还对象池。
            foreach (BuildingView view
                     in buildingViews.Values)
            {
                if (!authoredViews.Contains(view))
                {
                    buildingPools[
                        view.Definition.Kind].Release(view);
                }
            }

            buildingViews.Clear();

            foreach (EnergyTransferView view
                     in transferViews)
            {
                transferPool.Release(view);
            }

            transferViews.Clear();

            lastAppliedTick = -1;
            lastSelectedId = -1;

            maximumVisibleTransfers =
                settings.MaximumVisibleTransfers;

            maximumVisibleConnections =
                settings.MaximumVisibleConnections;

            if (previewProperties == null)
            {
                previewProperties =
                    new MaterialPropertyBlock();
            }

            if (connectionProperties == null)
            {
                connectionProperties =
                    new MaterialPropertyBlock();
            }

            if (transferPool == null)
            {
                transferPool =
                    new ObjectPool<EnergyTransferView>(
                        () =>
                            Instantiate(
                                transferPrefab,
                                energyRoot),

                        view =>
                        {
                            view.gameObject.SetActive(true);
                        },

                        view =>
                        {
                            view.gameObject.SetActive(false);
                        },

                        view =>
                        {
                            if (view != null)
                            {
                                Destroy(view.gameObject);
                            }
                        },

                        false,
                        16,
                        maximumVisibleTransfers);
            }

            ResetConnectionCaches();

            for (int index = 0;
                 index < authoredBuildings.Length;
                 index++)
            {
                BuildingView view =
                    authoredBuildings[index];

                view.ResetForReuse();

                view.transform.position =
                    authoredPositions[index];

                view.gameObject.SetActive(true);

                int x =
                    Mathf.FloorToInt(
                        view.transform.position.x);

                int z =
                    Mathf.FloorToInt(
                        view.transform.position.z);

                BuildingState state =
                    world.FindBuildingAt(x, z);

                if (state == null)
                {
                    throw new System.InvalidOperationException(
                        "初始建筑未能放置，请检查场景位置与初始资金：" +
                        view.name);
                }

                buildingViews.Add(
                    state.Id,
                    view);
            }
        }

        public void Refresh(
            GameWorld world,
            long selectedId,
            double displayTick)
        {
            // 静止建筑与连接只随逻辑状态或选择变化刷新，
            // 运输插值仍然每帧执行。
            if (lastAppliedTick != world.CurrentTick ||
                lastSelectedId != selectedId)
            {
                lastSelectedId =
                    selectedId;

                RefreshBuildings(
                    world,
                    selectedId);

                RefreshConnections(world);
<<<<<<< HEAD

                BuildingState selected =
                    world.FindBuilding(selectedId);

                DrawRange(
                    selectionRange,
                    selected == null
                        ? 0
                        : selected.CenterX,
                    selected == null
                        ? 0
                        : selected.CenterZ,
                    selected == null
                        ? 0
                        : Range(
                            world,
                            selected.Definition),
                    selected != null &&
                    !selected.IsSold);

                lastAppliedTick =
                    world.CurrentTick;

                lastSelectedId =
                    selectedId;
            }

            if (!externalTransferEffects)
            {
                RefreshTransfers(
                    world,
                    displayTick);
=======
                var selected = world.FindBuilding(selectedId);
                DrawRange(selectionRange, selected == null ? 0 : selected.CenterX, selected == null ? 0 : selected.CenterZ,
                    selected == null ? 0 : Range(world, selected.Definition), ShowRanges && selected != null && selected.IsAlive && !selected.IsSold);
                lastAppliedTick = world.CurrentTick;
                lastSelectedId = selectedId;
>>>>>>> upstream/main
            }
        }

        private void RefreshBuildings(
            GameWorld world,
            long selectedId)
        {
            removedBuildings.Clear();

            foreach (KeyValuePair<long, BuildingView> item
                     in buildingViews)
            {
<<<<<<< HEAD
                BuildingState state =
                    world.FindBuilding(item.Key);

                if (state == null ||
                    state.IsSold)
=======
                BuildingState state = world.FindBuilding(item.Key);
                if (state == null || state.IsSold || !state.IsAlive)
>>>>>>> upstream/main
                {
                    if (authoredViews.Contains(item.Value))
                    {
                        item.Value.gameObject.SetActive(false);
                    }
                    else
                    {
                        buildingPools[
                            item.Value.Definition.Kind].Release(
                                item.Value);
                    }

                    removedBuildings.Add(item.Key);
                }
            }

            foreach (long id in removedBuildings)
            {
<<<<<<< HEAD
                buildingViews.Remove(id);
            }

            for (int index = 0;
                 index < world.Buildings.Count;
                 index++)
            {
                BuildingState state =
                    world.Buildings[index];

                if (state.IsSold)
=======
                BuildingState state = world.Buildings[index];
                if (state.IsSold || !state.IsAlive) continue;
                if (!buildingViews.TryGetValue(state.Id, out BuildingView view))
>>>>>>> upstream/main
                {
                    continue;
                }

                if (!buildingViews.TryGetValue(
                        state.Id,
                        out BuildingView view))
                {
                    view =
                        buildingPools[
                            state.Kind].Get();

                    view.name =
                        state.Definition.DisplayName +
                        " " +
                        state.Id;

                    buildingViews.Add(
                        state.Id,
                        view);
                }

                view.Apply(
                    state,
                    state.Id == selectedId,
                    world.CurrentTick);
            }
        }

        public void SetPreview(
            bool visible,
            int x,
            int z,
            int width,
            int depth,
            bool valid)
        {
            placementPreview.gameObject.SetActive(
                visible);

<<<<<<< HEAD
            if (!visible &&
                previewRange != null)
=======
        public void ShowPreviewRange(GameWorld world, BuildingDefinition definition, int x, int z, bool visible)
            => DrawRange(previewRange, x + definition.Width * 0.5, z + definition.Depth * 0.5, Range(world, definition), visible);

        private static double Range(GameWorld world, BuildingDefinition definition) =>
            definition.Kind == BuildingKind.Reactor || definition.Kind == BuildingKind.Relay || definition.Kind == BuildingKind.Battery ? definition.TransmissionRange :
            world.Battle != null && definition.Kind != BuildingKind.Core ? world.Battle.Settings.GetTurret(definition.Kind).Range : 0;

        private static void DrawRange(LineRenderer line, double x, double z, double radius, bool visible)
        {
            if (line == null) return;
            line.gameObject.SetActive(visible && radius > 0);
            if (!visible || radius <= 0) return;
            for (int index = 0; index < line.positionCount; index++)
>>>>>>> upstream/main
            {
                previewRange.gameObject.SetActive(false);
            }

            if (!visible)
            {
                return;
            }

            placementPreview.position =
                new Vector3(
                    x + width * 0.5f,
                    0.06f,
                    z + depth * 0.5f);

            placementPreview.localScale =
                new Vector3(
                    width * 0.95f,
                    0.08f,
                    depth * 0.95f);

            Color color =
                valid
                    ? new Color(
                        0.18f,
                        0.8f,
                        0.65f)
                    : new Color(
                        0.95f,
                        0.3f,
                        0.26f);

            previewProperties.SetColor(
                "_BaseColor",
                color);

            previewProperties.SetColor(
                "_Color",
                valid
                    ? Color.green
                    : Color.red);

            previewRenderer.SetPropertyBlock(
                previewProperties);
        }

        public void ShowPreviewRange(
            GameWorld world,
            BuildingDefinition definition,
            int x,
            int z,
            bool visible)
        {
            DrawRange(
                previewRange,
                x + definition.Width * 0.5,
                z + definition.Depth * 0.5,
                Range(world, definition),
                visible);
        }

        private static double Range(
            GameWorld world,
            BuildingDefinition definition)
        {
            if (definition.Kind ==
                    BuildingKind.Reactor ||
                definition.Kind ==
                    BuildingKind.Relay)
            {
                return definition.TransmissionRange;
            }

            if (world.Battle != null &&
                definition.Kind != BuildingKind.Core)
            {
                return world.Battle.Settings.GetTurret(
                    definition.Kind).Range;
            }

            return 0;
        }

        private static void DrawRange(
            LineRenderer line,
            double x,
            double z,
            double radius,
            bool visible)
        {
            if (line == null)
            {
                return;
            }

            bool shouldBeActive =
                visible &&
                radius > 0;

            if (line.gameObject.activeSelf !=
                shouldBeActive)
            {
                line.gameObject.SetActive(
                    shouldBeActive);
            }

            if (!shouldBeActive)
            {
                return;
            }

            for (int index = 0;
                 index < line.positionCount;
                 index++)
            {
                double angle =
                    index *
                    System.Math.PI *
                    2 /
                    (line.positionCount - 1);

                line.SetPosition(
                    index,
                    new Vector3(
                        (float)(
                            x +
                            System.Math.Cos(angle) *
                            radius),
                        0.12f,
                        (float)(
                            z +
                            System.Math.Sin(angle) *
                            radius)));
            }
        }

        private void RefreshTransfers(
            GameWorld world,
            double displayTick)
        {
            int count =
                Mathf.Min(
                    world.Energy.Transfers.Count,
                    maximumVisibleTransfers);

            while (transferViews.Count < count)
            {
                transferViews.Add(
                    transferPool.Get());
            }

            while (transferViews.Count > count)
            {
                int last =
                    transferViews.Count - 1;

                transferPool.Release(
                    transferViews[last]);

                transferViews.RemoveAt(last);
            }

            for (int index = 0;
                 index < count;
                 index++)
            {
                transferViews[index].Apply(
                    world.Energy.Transfers[index],
                    displayTick);
            }
        }

        private void RefreshConnections(
            GameWorld world)
        {
            int count =
                Mathf.Min(
                    world.Energy.Connections.Count,
                    maximumVisibleConnections);

            while (connectionViews.Count < count)
            {
<<<<<<< HEAD
                connectionViews.Add(
                    Instantiate(
                        connectionPrefab,
                        connectionRoot));
=======
                LineRenderer line = connectionViews[index];
                line.gameObject.SetActive(ShowNetwork && index < count);
                if (index >= count) continue;
                EnergyConnection connection = world.Energy.Connections[index];
                BuildingState source = world.FindBuilding(connection.SourceId);
                BuildingState target = world.FindBuilding(connection.TargetId);
                if (source == null || target == null) { line.gameObject.SetActive(false); continue; }
                bool related = connection.SourceId == lastSelectedId || connection.TargetId == lastSelectedId;
                Color color = related ? new Color(0.4f, 1f, 0.8f, 1) : new Color(0.12f, 0.22f, 0.26f, 1);
                connectionProperties.SetColor("_BaseColor", color); connectionProperties.SetColor("_Color", color);
                line.SetPropertyBlock(connectionProperties);
                line.startWidth = line.endWidth = related ? 0.075f : 0.018f;
                line.SetPosition(0, new Vector3((float)source.CenterX, 0.11f, (float)source.CenterZ));
                line.SetPosition(1, new Vector3((float)target.CenterX, 0.11f, (float)target.CenterZ));
>>>>>>> upstream/main
            }

            EnsureConnectionCacheCapacity(
                connectionViews.Count);

            for (int index = 0;
                 index < connectionViews.Count;
                 index++)
            {
                LineRenderer line =
                    connectionViews[index];

                bool shouldBeActive =
                    index < count;

                if (cachedConnectionActive[index] !=
                    shouldBeActive)
                {
                    line.gameObject.SetActive(
                        shouldBeActive);

                    cachedConnectionActive[index] =
                        shouldBeActive;
                }

                // 连接已经超出当前数量。
                // 清空缓存，确保未来重新出现时强制刷新。
                if (!shouldBeActive)
                {
                    ClearConnectionCache(index);
                    continue;
                }

                EnergyConnection connection =
                    world.Energy.Connections[index];

                BuildingState source =
                    world.FindBuilding(
                        connection.SourceId);

                BuildingState target =
                    world.FindBuilding(
                        connection.TargetId);

                // 理论上能源连接不应该引用不存在的建筑。
                // 如果出现暂时不一致，隐藏该线并清空缓存。
                if (source == null ||
                    target == null)
                {
                    if (cachedConnectionActive[index])
                    {
                        line.gameObject.SetActive(false);
                        cachedConnectionActive[index] =
                            false;
                    }

                    ClearConnectionCache(index);
                    continue;
                }

                long sourceId =
                    connection.SourceId;

                long targetId =
                    connection.TargetId;

                bool endpointChanged =
                    cachedConnectionSourceIds[index] !=
                        sourceId ||
                    cachedConnectionTargetIds[index] !=
                        targetId;

                Vector3 startPosition =
                    new Vector3(
                        (float)source.CenterX,
                        0.11f,
                        (float)source.CenterZ);

                Vector3 endPosition =
                    new Vector3(
                        (float)target.CenterX,
                        0.11f,
                        (float)target.CenterZ);

                bool startPositionChanged =
                    endpointChanged ||
                    cachedConnectionStartPositions[index] !=
                        startPosition;

                bool endPositionChanged =
                    endpointChanged ||
                    cachedConnectionEndPositions[index] !=
                        endPosition;

                // 端点变化时，必须强制更新位置。
                if (startPositionChanged)
                {
                    line.SetPosition(
                        0,
                        startPosition);

                    cachedConnectionStartPositions[index] =
                        startPosition;
                }

                if (endPositionChanged)
                {
                    line.SetPosition(
                        1,
                        endPosition);

                    cachedConnectionEndPositions[index] =
                        endPosition;
                }

                bool related =
                    sourceId == lastSelectedId ||
                    targetId == lastSelectedId;

                // 端点变化时不使用上一条连接的 related 缓存。
                // endpointChanged 会强制进入这里。
                if (endpointChanged ||
                    cachedConnectionRelated[index] !=
                        related)
                {
                    Color color =
                        related
                            ? new Color(
                                0.4f,
                                1f,
                                0.8f,
                                1f)
                            : new Color(
                                0.12f,
                                0.22f,
                                0.26f,
                                1f);

                    connectionProperties.SetColor(
                        "_BaseColor",
                        color);

                    connectionProperties.SetColor(
                        "_Color",
                        color);

                    line.SetPropertyBlock(
                        connectionProperties);

                    float width =
                        related
                            ? 0.075f
                            : 0.018f;

                    line.startWidth =
                        width;

                    line.endWidth =
                        width;

                    cachedConnectionRelated[index] =
                        related;
                }

                // 最后保存端点身份。
                cachedConnectionSourceIds[index] =
                    sourceId;

                cachedConnectionTargetIds[index] =
                    targetId;
            }
        }

        private void EnsureConnectionCacheCapacity(
            int capacity)
        {
            if (cachedConnectionSourceIds.Length >=
                capacity)
            {
                return;
            }

            System.Array.Resize(
                ref cachedConnectionSourceIds,
                capacity);

            System.Array.Resize(
                ref cachedConnectionTargetIds,
                capacity);

            System.Array.Resize(
                ref cachedConnectionStartPositions,
                capacity);

            System.Array.Resize(
                ref cachedConnectionEndPositions,
                capacity);

            System.Array.Resize(
                ref cachedConnectionRelated,
                capacity);

            System.Array.Resize(
                ref cachedConnectionActive,
                capacity);
        }

        private void ClearConnectionCache(
            int index)
        {
            cachedConnectionSourceIds[index] =
                0;

            cachedConnectionTargetIds[index] =
                0;

            cachedConnectionStartPositions[index] =
                Vector3.zero;

            cachedConnectionEndPositions[index] =
                Vector3.zero;

            cachedConnectionRelated[index] =
                false;
        }

        private void ResetConnectionCaches()
        {
            System.Array.Clear(
                cachedConnectionSourceIds,
                0,
                cachedConnectionSourceIds.Length);

            System.Array.Clear(
                cachedConnectionTargetIds,
                0,
                cachedConnectionTargetIds.Length);

            System.Array.Clear(
                cachedConnectionStartPositions,
                0,
                cachedConnectionStartPositions.Length);

            System.Array.Clear(
                cachedConnectionEndPositions,
                0,
                cachedConnectionEndPositions.Length);

            System.Array.Clear(
                cachedConnectionRelated,
                0,
                cachedConnectionRelated.Length);

            System.Array.Clear(
                cachedConnectionActive,
                0,
                cachedConnectionActive.Length);
        }

        private void OnDestroy()
        {
            // 场景退出时子物体可能先被销毁；
            // 销毁回调必须检查 Unity 对象是否仍然存在。
            transferPool?.Clear();

            foreach (
                ObjectPool<BuildingView> pool
                in buildingPools.Values)
            {
                pool.Clear();
            }
        }
    }
}