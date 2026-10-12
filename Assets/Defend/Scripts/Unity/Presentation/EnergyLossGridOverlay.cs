using System;
using System.Collections.Generic;
using Defend.Simulation;
using UnityEngine;

namespace Defend.Unity
{
    /// <summary>
    /// 为选中的反应堆或中继器显示逐格能源传输效率。
    /// </summary>
    public sealed class EnergyLossGridOverlay : MonoBehaviour
    {
        [SerializeField]
        private float cellHeight = 0.05f;

        // 使用网格坐标保存生成的 Cell。
        private readonly Dictionary<Vector2Int, Cell> cells =
            new Dictionary<Vector2Int, Cell>();

        private GameWorld world;
        private MaterialPropertyBlock properties;
        private GameObject cellPrefab;
        private GameObject cellsRoot;
        private int width;
        private int depth;

        private long lastSelectedId = -1;
        private int lastPositionRevision = -1;
        private int lastLifeGeneration = -1;
        private bool lastDisplayed;

        private static readonly int ShouldBeDisplayed =
            Shader.PropertyToID("_ShouldBeDisplayed");

        private static readonly int Percentage =
            Shader.PropertyToID("_Percentage");

        private sealed class Cell
        {
            public readonly GameObject Instance;
            public readonly Renderer Renderer;
            public readonly int X;
            public readonly int Z;

            public Cell(
                GameObject instance,
                Renderer renderer,
                int x,
                int z)
            {
                Instance = instance;
                Renderer = renderer;
                X = x;
                Z = z;
            }
        }

        /// <summary>
        /// 供 GameSessionRoot 在创建模拟世界后调用。
        /// </summary>
        public void Initialize(
            GameWorld value,
            GameObject prefab)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (prefab == null)
            {
                throw new ArgumentNullException(nameof(prefab));
            }

            if (prefab.GetComponentInChildren<Renderer>(true) == null)
            {
                throw new InvalidOperationException(
                    "EnergyLossGridOverlay 的格子 Prefab 必须包含 Renderer。");
            }

            world = value;

            bool needsRebuild =
                cells.Count > 0 &&
                (
                    width != value.Settings.Width ||
                    depth != value.Settings.Depth ||
                    cellPrefab != prefab
                );

            if (needsRebuild)
            {
                foreach (Cell cell in cells.Values)
                {
                    Destroy(cell.Instance);
                }

                cells.Clear();

                if (cellsRoot != null)
                {
                    Destroy(cellsRoot);
                    cellsRoot = null;
                }
            }

            cellPrefab = prefab;
            width = value.Settings.Width;
            depth = value.Settings.Depth;

            if (properties == null)
            {
                properties = new MaterialPropertyBlock();
            }

            EnsureCellsRoot();
            EnsureCells();

            HideAll();

            lastSelectedId = -1;
            lastPositionRevision = -1;
            lastLifeGeneration = -1;
            lastDisplayed = false;
        }

        /// <summary>
        /// 只在选择、位置或生命状态变化时重新写入逐格参数。
        /// </summary>
        public void Refresh(
            GameWorld value,
            long selectedId)
        {
            if (value == null ||
                value != world ||
                cells.Count == 0)
            {
                return;
            }

            BuildingState source =
                value.FindBuilding(selectedId);

            bool displayed =
                source != null &&
                source.IsAlive &&
                !source.IsSold &&
                (
                    source.Kind == BuildingKind.Reactor ||
                    source.Kind == BuildingKind.Relay
                );

            int positionRevision =
                source == null
                    ? -1
                    : source.PositionRevision;

            int lifeGeneration =
                source == null
                    ? -1
                    : source.LifeGeneration;

            bool unchanged =
                selectedId == lastSelectedId &&
                displayed == lastDisplayed &&
                positionRevision == lastPositionRevision &&
                lifeGeneration == lastLifeGeneration;

            if (unchanged)
            {
                return;
            }

            lastSelectedId = selectedId;
            lastDisplayed = displayed;
            lastPositionRevision = positionRevision;
            lastLifeGeneration = lifeGeneration;

            // 先隐藏全部格子。
            HideAll();

            if (!displayed)
            {
                return;
            }

            foreach (Cell cell in cells.Values)
            {
                double percentage =
                    value.Energy.DirectEfficiencyAt(
                        source,
                        cell.X + 0.5,
                        cell.Z + 0.5);

                if (percentage <= 0)
                {
                    continue;
                }

                SetCellDisplay(
                    cell,
                    true,
                    (float)percentage);
            }
        }

        public void Hide()
        {
            HideAll();

            lastSelectedId = -1;
            lastPositionRevision = -1;
            lastLifeGeneration = -1;
            lastDisplayed = false;
        }

        private void EnsureCells()
        {
            if (cells.Count == width * depth)
            {
                return;
            }

            EnsureCellsRoot();

            for (int z = 0; z < depth; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    GameObject instance =
                        Instantiate(cellPrefab);

                    instance.name =
                        "EnergyLossCell " + x + "," + z;

                    // 所有 Cell 都挂到统一根对象下。
                    instance.transform.SetParent(
                        cellsRoot.transform,
                        true);

                    instance.SetActive(true);

                    Renderer renderer =
                        instance.GetComponentInChildren<Renderer>(true);

                    if (renderer == null)
                    {
                        Destroy(instance);

                        throw new InvalidOperationException(
                            "EnergyLossGridOverlay 的 cellPrefab 必须包含 Renderer。");
                    }

                    Vector3 cellPosition =
                        new Vector3(
                            x + 0.5f,
                            cellHeight,
                            z + 0.5f);

                    Quaternion cellRotation =
                        Quaternion.Euler(
                            -90f,
                            0f,
                            0f);

                    // 根对象使用世界坐标，避免父级变换影响位置。
                    instance.transform.SetPositionAndRotation(
                        cellPosition,
                        cellRotation);

                    instance.transform.localPosition =
                        cellPosition;

                    renderer.transform.SetPositionAndRotation(
                        cellPosition,
                        cellRotation);

                    Vector2Int coordinate =
                        new Vector2Int(x, z);

                    cells.Add(
                        coordinate,
                        new Cell(
                            instance,
                            renderer,
                            x,
                            z));
                }
            }
        }

        private void EnsureCellsRoot()
        {
            if (cellsRoot != null)
            {
                return;
            }

            cellsRoot =
                new GameObject(
                    "EnergyLossGridOverlayCells");

            cellsRoot.transform.SetPositionAndRotation(
                Vector3.zero,
                Quaternion.identity);

            cellsRoot.transform.localScale =
                Vector3.one;
        }

        private void HideAll()
        {
            foreach (Cell cell in cells.Values)
            {
                SetCellDisplay(
                    cell,
                    false,
                    0f);
            }
        }

        /// <summary>
        /// 同时设置材质参数和 GameObject 的 active 状态。
        /// </summary>
        private void SetCellDisplay(
            Cell cell,
            bool shouldDisplay,
            float percentage)
        {
            cell.Instance.SetActive(
                shouldDisplay);

            properties.Clear();

            properties.SetFloat(
                ShouldBeDisplayed,
                shouldDisplay ? 1f : 0f);

            properties.SetFloat(
                Percentage,
                shouldDisplay ? percentage : 0f);

            cell.Renderer.SetPropertyBlock(
                properties);
        }

        private void OnDestroy()
        {
            cells.Clear();

            if (cellsRoot != null)
            {
                Destroy(cellsRoot);
                cellsRoot = null;
            }
        }
    }
}