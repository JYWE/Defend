using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Defend.Simulation;
using Defend.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Defend.Editor
{
    /// <summary>
    /// 首次制作白盒资产的编辑器入口。已有文件一律复用；不会用模板覆盖开发者手工编辑的场景或预制体。
    /// 所有面板、按钮、布局和场景依赖在保存前建立，运行时不执行本生成流程。
    /// </summary>
    public static class WhiteboxSceneBuilder
    {
        public const string ScenePath = "Assets/Defend/Content/Scenes/Dev/EnergyWhitebox.unity";
        public const string ChineseFontPath = "Assets/Defend/Content/Art/Fonts/NotoSansCJKsc-Regular.otf";
        private const string ContentRoot = "Assets/Defend/Content";
        private static readonly Color TextColor = new Color(0.86f, 0.91f, 0.96f);
        private static readonly Color MutedColor = new Color(0.55f, 0.64f, 0.72f);
        private static readonly Color AccentColor = new Color(0.24f, 0.84f, 0.72f);

        [MenuItem("涌现防线/创建或打开能源白盒场景")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请退出播放模式后制作白盒资产。");
            // 已有场景完全保留。开发者可以直接修改其层级和 Prefab，不会被下次运行覆盖。
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                Debug.Log("能源白盒场景已存在，保留全部手工修改：" + ScenePath);
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnsureFolder(ContentRoot + "/Definitions/Buildings");
            EnsureFolder(ContentRoot + "/Definitions/Energy");
            EnsureFolder(ContentRoot + "/Prefabs/Buildings");
            EnsureFolder(ContentRoot + "/Prefabs/UI");
            EnsureFolder(ContentRoot + "/Prefabs/VFX");
            EnsureFolder(ContentRoot + "/Art/Materials");
            EnsureFolder(ContentRoot + "/Art/Fonts");
            EnsureFolder(ContentRoot + "/Scenes/Dev");

            // Single 模式切换场景会清理旧场景及未被持久对象引用的资源。
            // 先完成场景切换，再加载下面需要保持有效的 Prefab、定义和材质对象。
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "EnergyWhitebox";

            BuildingDefinitionAsset[] definitions = CreateDefinitions();
            EnergyExperimentSettings settings = LoadOrCreate<EnergyExperimentSettings>(ContentRoot + "/Definitions/Energy/WhiteboxEnergySettings.asset");
            Material reactorMaterial = MaterialAsset("Reactor", new Color(0.19f, 0.8f, 0.69f));
            Material relayMaterial = MaterialAsset("Relay", new Color(0.3f, 0.58f, 0.98f));
            Material turretMaterial = MaterialAsset("Turret", new Color(0.98f, 0.64f, 0.27f));
            Material coreMaterial = MaterialAsset("Core", new Color(0.66f, 0.46f, 0.98f));
            Material darkMaterial = MaterialAsset("BuildingBase", new Color(0.13f, 0.19f, 0.24f));
            Material lightMaterial = MaterialAsset("EnergyLight", new Color(0.68f, 1f, 0.89f), true);
            Material selectionMaterial = MaterialAsset("Selection", new Color(0.96f, 0.91f, 0.53f), true);
            Material damagedMaterial = MaterialAsset("Damaged", new Color(0.92f, 0.22f, 0.24f));
            Material[] buildingMaterials = { reactorMaterial, relayMaterial, turretMaterial, coreMaterial };
            BuildingView[] prefabs = new BuildingView[definitions.Length];
            for (int index = 0; index < definitions.Length; index++)
                prefabs[index] = CreateBuildingPrefab(definitions[index], buildingMaterials[index], darkMaterial, lightMaterial, selectionMaterial, damagedMaterial);
            EnergyTransferView energyPrefab = CreateTransferPrefab(lightMaterial);
            LineRenderer linePrefab = CreateConnectionPrefab(MaterialAsset("Connection", new Color(0.17f, 0.4f, 0.43f), true));
            Font font = CreateChineseFont();
            EnergyControlPanel panelPrefab = CreatePanelPrefab(font);

            RenderSettings.ambientLight = new Color(0.72f, 0.76f, 0.83f);
            RenderSettings.fog = false;
            Transform mapRoot = new GameObject("地图 · 40 × 40").transform;
            CreateShape("Ground", PrimitiveType.Cube, mapRoot, new Vector3(20, -0.15f, 20), new Vector3(40, 0.2f, 40), MaterialAsset("Ground", new Color(0.065f, 0.095f, 0.125f)));
            CreateGrid(mapRoot, MaterialAsset("Grid", new Color(0.11f, 0.17f, 0.21f), true));
            Camera camera = new GameObject("主摄像机", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(20, 50, 20);
            camera.transform.rotation = Quaternion.Euler(90, 0, 0);
            camera.orthographic = true;
            camera.orthographicSize = 27.5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.041f, 0.061f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 120;
            WhiteboxBackgroundMigration.EnsureBackgroundCamera(scene, camera);
            Light light = new GameObject("白盒定向光", typeof(Light)).GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.6f;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(50, -30, 0);

            Transform buildingRoot = new GameObject("建筑 · 可直接编辑初始布局").transform;
            List<BuildingView> initial = new List<BuildingView>();
            initial.Add(PlaceAuthored(prefabs[0], buildingRoot, 11, 20));
            initial.Add(PlaceAuthored(prefabs[1], buildingRoot, 16, 17));
            initial.Add(PlaceAuthored(prefabs[1], buildingRoot, 16, 23));
            initial.Add(PlaceAuthored(prefabs[1], buildingRoot, 21, 17));
            initial.Add(PlaceAuthored(prefabs[1], buildingRoot, 21, 23));
            initial.Add(PlaceAuthored(prefabs[2], buildingRoot, 26, 17));
            initial.Add(PlaceAuthored(prefabs[2], buildingRoot, 26, 23));
            initial.Add(PlaceAuthored(prefabs[3], buildingRoot, 26, 20));
            Transform energyRoot = new GameObject("运输表现 · 不参与结算").transform;
            Transform connectionRoot = new GameObject("连接表现").transform;
            GameObject preview = CreateShape("放置预览", PrimitiveType.Cube, mapRoot, Vector3.zero,
                new Vector3(0.95f, 0.08f, 0.95f), MaterialAsset("PlacementPreview", AccentColor, true));
            preview.SetActive(false);
            EventSystem eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule)).GetComponent<EventSystem>();
            eventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            // PrefabUtility 的输入使用预制体根 GameObject；不能依赖传入组件时返回该组件。
            GameObject panelInstance = (GameObject)PrefabUtility.InstantiatePrefab(panelPrefab.gameObject);
            if (panelInstance == null) throw new InvalidOperationException("无法实例化能源控制面板预制体。");
            EnergyControlPanel panel = panelInstance.GetComponent<EnergyControlPanel>();
            if (panel == null) throw new InvalidOperationException("能源控制面板预制体根节点缺少 EnergyControlPanel。");
            RectTransform mapViewport = panel.transform.Find("MapViewport").GetComponent<RectTransform>();
            WorldCameraViewport viewportController = camera.gameObject.AddComponent<WorldCameraViewport>();
            viewportController.Configure(camera, mapViewport);
            GameObject sessionObject = new GameObject("GameSessionRoot · 模拟组合入口");
            WorldPresentation presentation = sessionObject.AddComponent<WorldPresentation>();
            presentation.Configure(prefabs, initial.ToArray(), energyPrefab, linePrefab, buildingRoot, energyRoot,
                connectionRoot, preview.transform, preview.GetComponent<Renderer>());
            GridInputController input = sessionObject.AddComponent<GridInputController>();
            input.Configure(camera, eventSystem);
            GameSessionRoot sessionRoot = sessionObject.AddComponent<GameSessionRoot>();
            sessionRoot.Configure(definitions, settings, presentation, input, panel);
            EditorUtility.SetDirty(sessionRoot);
            EditorUtility.SetDirty(presentation);
            EditorUtility.SetDirty(input);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = sessionObject;
            Debug.Log("能源白盒场景已保存。UI、初始建筑、配置和 Prefab 均可直接在编辑器修改：" + ScenePath);
        }

        private static BuildingDefinitionAsset[] CreateDefinitions()
        {
            BuildingKind[] kinds = { BuildingKind.Reactor, BuildingKind.Relay, BuildingKind.Turret, BuildingKind.Core };
            string[] names = { "反应堆", "中继器", "炮塔", "核心" };
            BuildingDefinitionAsset[] definitions = new BuildingDefinitionAsset[4];
            for (int index = 0; index < definitions.Length; index++)
            {
                string path = ContentRoot + "/Definitions/Buildings/" + kinds[index] + ".asset";
                definitions[index] = AssetDatabase.LoadAssetAtPath<BuildingDefinitionAsset>(path);
                if (definitions[index] != null) continue;
                BuildingDefinitionAsset definition = ScriptableObject.CreateInstance<BuildingDefinitionAsset>();
                bool consumer = index >= 2;
                definition.ConfigureWhitebox(kinds[index], names[index], index == 3 ? 160000 : 100000,
                    index == 0 ? 2000 : 0, consumer ? 1000 : 0, consumer ? 0 : 8000, 4,
                    index == 0 ? 200 : index == 3 ? 200 : 100, index == 0 || index == 3 ? 100 : 50, 25);
                AssetDatabase.CreateAsset(definition, path);
                definitions[index] = definition;
            }
            return definitions;
        }

        private static BuildingView CreateBuildingPrefab(BuildingDefinitionAsset definition, Material body,
            Material dark, Material energy, Material selected, Material damaged)
        {
            string path = ContentRoot + "/Prefabs/Buildings/" + definition.Kind + ".prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing.GetComponent<BuildingView>();
            GameObject root = new GameObject(definition.Kind.ToString());
            BuildingView view = root.AddComponent<BuildingView>();
            BoxCollider hitArea = root.AddComponent<BoxCollider>();
            hitArea.center = new Vector3(0, 0.5f, 0);
            hitArea.size = new Vector3(1, 1.4f, 1);
            Transform visual = Child("VisualRoot · 替换美术入口", root.transform);
            CreateShape("Base", PrimitiveType.Cube, visual, new Vector3(0, 0.13f, 0), new Vector3(0.88f, 0.26f, 0.88f), dark);
            if (definition.Kind == BuildingKind.Reactor)
            {
                CreateShape("ReactorBody", PrimitiveType.Cylinder, visual, new Vector3(0, 0.67f, 0), new Vector3(0.65f, 0.43f, 0.65f), body);
                CreateShape("EnergyCore", PrimitiveType.Sphere, visual, new Vector3(0, 1.17f, 0), new Vector3(0.3f, 0.3f, 0.3f), energy);
            }
            else if (definition.Kind == BuildingKind.Relay)
            {
                CreateShape("RelayColumn", PrimitiveType.Cube, visual, new Vector3(0, 0.7f, 0), new Vector3(0.27f, 0.95f, 0.27f), body);
                CreateShape("RelayAntenna", PrimitiveType.Sphere, visual, new Vector3(0, 1.17f, 0), new Vector3(0.68f, 0.18f, 0.68f), body);
            }
            else if (definition.Kind == BuildingKind.Turret)
            {
                Transform weapon = Child("WeaponRoot", visual);
                CreateShape("TurretHead", PrimitiveType.Cube, weapon, new Vector3(0, 0.67f, 0), new Vector3(0.62f, 0.5f, 0.6f), body);
                CreateShape("Barrel", PrimitiveType.Cube, weapon, new Vector3(0, 0.7f, 0.42f), new Vector3(0.16f, 0.16f, 0.55f), dark);
                Child("Muzzle", weapon).localPosition = new Vector3(0, 0.7f, 0.71f);
            }
            else
            {
                GameObject core = CreateShape("CoreBody", PrimitiveType.Cube, visual, new Vector3(0, 0.75f, 0), new Vector3(0.61f, 0.85f, 0.61f), body);
                core.transform.localRotation = Quaternion.Euler(0, 45, 0);
            }
            Child("EnergyPort", root.transform).localPosition = new Vector3(0, 0.85f, 0);
            GameObject selection = CreateShape("SelectionRoot", PrimitiveType.Cube, root.transform, new Vector3(0, 0.025f, 0), new Vector3(1.02f, 0.035f, 1.02f), selected);
            selection.SetActive(false);
            CreateShape("EnergyBackground", PrimitiveType.Cube, root.transform, new Vector3(0, 1.5f, 0), new Vector3(0.82f, 0.07f, 0.14f), dark);
            Transform fill = CreateShape("EnergyFill", PrimitiveType.Cube, root.transform, new Vector3(-0.4f, 1.5f, 0), new Vector3(0.001f, 0.08f, 0.15f), energy).transform;
            GameObject destroyed = CreateShape("DestroyedMarker", PrimitiveType.Cube, root.transform, new Vector3(0, 0.28f, 0), new Vector3(0.8f, 0.16f, 0.8f), damaged);
            destroyed.SetActive(false);
            view.Configure(definition, visual, selection, fill, destroyed);
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved.GetComponent<BuildingView>();
        }

        private static EnergyTransferView CreateTransferPrefab(Material material)
        {
            string path = ContentRoot + "/Prefabs/VFX/EnergyTransfer.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing.GetComponent<EnergyTransferView>();
            GameObject root = CreateShape("EnergyTransfer", PrimitiveType.Sphere, null, Vector3.zero, Vector3.one * 0.14f, material);
            root.AddComponent<EnergyTransferView>();
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved.GetComponent<EnergyTransferView>();
        }

        private static LineRenderer CreateConnectionPrefab(Material material)
        {
            string path = ContentRoot + "/Prefabs/VFX/EnergyConnection.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing.GetComponent<LineRenderer>();
            GameObject root = new GameObject("EnergyConnection", typeof(LineRenderer));
            LineRenderer line = root.GetComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.positionCount = 2;
            line.startWidth = line.endWidth = 0.045f;
            line.useWorldSpace = true;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved.GetComponent<LineRenderer>();
        }

        private static EnergyControlPanel CreatePanelPrefab(Font font)
        {
            string path = ContentRoot + "/Prefabs/UI/EnergyWhiteboxPanel.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing.GetComponent<EnergyControlPanel>();
            GameObject root = new GameObject("EnergyWhiteboxPanel", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(EnergyControlPanel));
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = 0.5f;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            Transform parent = root.transform;
            // 只有透明的布局矩形，不接收点击；摄像机使用它计算实际屏幕区域。
            RectTransform mapViewport = new GameObject("MapViewport", typeof(RectTransform)).GetComponent<RectTransform>();
            mapViewport.SetParent(parent, false);
            PlaceRectangle(mapViewport, 296, 180, 934, 610);
            RectTransform header = Panel("Header", parent, 24, 20, 1552, 78, new Color(0.06f, 0.09f, 0.13f));
            Label("ProductName", header, font, "涌现防线", 20, 12, 230, 31, 28, TextColor, FontStyle.Bold);
            Label("Subtitle", header, font, "能源网络  /  第一阶段白盒", 22, 48, 420, 19, 14, MutedColor);
            Text resource = Label("Resources", header, font, "资金  3000     模拟时间  0.0 秒", 480, 29, 640, 26, 19, TextColor);
            Label("PhaseBadge", header, font, "持续模拟 · 可编辑资产", 1290, 30, 240, 25, 16, AccentColor);

            RectTransform shop = Panel("ConstructionPanel", parent, 24, 114, 256, 676, new Color(0.055f, 0.083f, 0.117f));
            Label("ShopHeading", shop, font, "建造与试验", 20, 20, 216, 30, 23, TextColor, FontStyle.Bold);
            Label("ShopDescription", shop, font, "选择类型，再单击网格放置。", 20, 60, 220, 45, 15, MutedColor);
            Button[] shopButtons = new Button[4];
            Text[] shopLabels = new Text[4];
            string[] names = { "反应堆\n200 资金", "中继器\n100 资金", "炮塔\n100 资金", "核心\n200 资金" };
            Color[] colors = { new Color(0.12f, 0.36f, 0.32f), new Color(0.15f, 0.26f, 0.43f), new Color(0.39f, 0.27f, 0.15f), new Color(0.28f, 0.2f, 0.4f) };
            for (int index = 0; index < 4; index++) shopButtons[index] = Button("Build" + index, shop, font, names[index], 20, 101 + index * 73, 216, 61, colors[index], out shopLabels[index]);

            RectTransform inspector = Panel("BuildingInspector", parent, 1246, 114, 330, 676, new Color(0.055f, 0.083f, 0.117f));
            Text selectionTitle = Label("SelectionTitle", inspector, font, "未选择建筑", 20, 20, 290, 34, 23, TextColor, FontStyle.Bold);
            Image track = Panel("EnergyTrack", inspector, 20, 70, 290, 7, new Color(0.13f, 0.2f, 0.25f)).GetComponent<Image>();
            Image fill = Panel("EnergyFill", track.transform, 0, 0, 290, 7, AccentColor).GetComponent<Image>();
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 0;
            WhiteboxEnergyBarAuthoring.Configure(fill);
            Text selectionText = Label("SelectionDetails", inspector, font, "单击地图中的建筑\n查看储能、供给与优先级。", 20, 96, 290, 297, 16, TextColor);
            Label("PriorityHeading", inspector, font, "终端供给优先级", 20, 407, 290, 26, 16, MutedColor);
            Button[] priority = new Button[3];
            for (int index = 0; index < 3; index++) priority[index] = Button("Priority" + index, inspector, font,
                new[] { "低 · 1", "中 · 2", "高 · 4" }[index], 20 + index * 99, 446, 92, 44, new Color(0.14f, 0.22f, 0.29f), out _);
            Button move = Button("MoveBuilding", inspector, font, "移动", 20, 510, 139, 43, new Color(0.13f, 0.23f, 0.3f), out _);
            Button sell = Button("SellBuilding", inspector, font, "出售", 171, 510, 139, 43, new Color(0.22f, 0.18f, 0.19f), out _);
            Button damage = Button("DamageBuilding", inspector, font, "损坏试验", 20, 566, 139, 43, new Color(0.31f, 0.16f, 0.18f), out _);
            Button repair = Button("RepairBuilding", inspector, font, "修复", 171, 566, 139, 43, new Color(0.14f, 0.3f, 0.25f), out _);
            Label("InspectorHelp", inspector, font, "移动前会提示在途损失风险。", 20, 630, 290, 24, 14, MutedColor);

            Label("MapTitle", parent, font, "试验网络", 310, 115, 400, 28, 21, TextColor, FontStyle.Bold);
            Label("MapSubtitle", parent, font, "40 × 40  网格  /  自动规划  /  禁止回流", 310, 150, 650, 24, 15, MutedColor);
            RectTransform diagnostics = Panel("DeveloperDiagnostics", shop, 12, 409, 232, 252, new Color(0.035f, 0.06f, 0.086f, 0.96f));
            Label("DiagnosticHeading", diagnostics, font, "开发诊断 · 能量账目", 10, 12, 212, 24, 15, AccentColor, FontStyle.Bold);
            Text diagnosticText = Label("DiagnosticValues", diagnostics, font, "进入播放模式后显示账目。", 10, 43, 212, 202, 13, MutedColor);

            RectTransform footer = Panel("SimulationControls", parent, 24, 806, 1552, 74, new Color(0.06f, 0.09f, 0.13f));
            Button pause = Button("Pause", footer, font, "暂停模拟", 16, 14, 115, 44, new Color(0.14f, 0.3f, 0.28f), out Text pauseText);
            Button step = Button("SingleStep", footer, font, "单步", 143, 14, 80, 44, new Color(0.14f, 0.22f, 0.29f), out _);
            Button speed = Button("Speed", footer, font, "速度  1 倍", 235, 14, 118, 44, new Color(0.14f, 0.22f, 0.29f), out Text speedText);
            Button load = Button("TestLoad", footer, font, "测试消耗：开启", 365, 14, 174, 44, new Color(0.14f, 0.22f, 0.29f), out Text loadText);
            Button cancel = Button("CancelPlacement", footer, font, "取消放置", 551, 14, 119, 44, new Color(0.14f, 0.22f, 0.29f), out _);
            Text modeText = Label("InteractionMode", footer, font, "浏览 / 选择", 692, 13, 285, 25, 16, AccentColor);
            Label("KeyboardHelp", footer, font, "滚轮缩放 · 中键拖动 · Esc 取消", 692, 41, 285, 22, 13, MutedColor);
            Text feedback = Label("Feedback", footer, font, "选择建筑查看供给，或从左侧购买建筑。", 997, 12, 535, 52, 15, TextColor);

            EnergyControlPanel controller = root.GetComponent<EnergyControlPanel>();
            SerializedObject serialized = new SerializedObject(controller);
            SetReference(serialized, "resourceText", resource);
            SetReference(serialized, "selectionTitle", selectionTitle);
            SetReference(serialized, "selectionText", selectionText);
            SetReference(serialized, "diagnosticsText", diagnosticText);
            SetReference(serialized, "feedbackText", feedback);
            SetReference(serialized, "modeText", modeText);
            SetReference(serialized, "pauseLabel", pauseText);
            SetReference(serialized, "speedLabel", speedText);
            SetReference(serialized, "loadLabel", loadText);
            SetReference(serialized, "energyFill", fill);
            SetArray(serialized, "placementButtons", shopButtons);
            SetArray(serialized, "placementLabels", shopLabels);
            SetArray(serialized, "priorityButtons", priority);
            SetReference(serialized, "moveButton", move);
            SetReference(serialized, "sellButton", sell);
            SetReference(serialized, "damageButton", damage);
            SetReference(serialized, "repairButton", repair);
            SetReference(serialized, "pauseButton", pause);
            SetReference(serialized, "stepButton", step);
            SetReference(serialized, "speedButton", speed);
            SetReference(serialized, "loadButton", load);
            SetReference(serialized, "cancelButton", cancel);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved.GetComponent<EnergyControlPanel>();
        }

        private static Font CreateChineseFont()
        {
            // 使用真实字体文件，让编辑器、域重载和 Player 都拥有可恢复的字形与材质。
            // 原始 Noto Sans CJK 文件及 SIL Open Font License 一并随项目分发。
            AssetDatabase.ImportAsset(ChineseFontPath, ImportAssetOptions.ForceSynchronousImport);
            Font font = AssetDatabase.LoadAssetAtPath<Font>(ChineseFontPath);
            if (font == null) throw new InvalidOperationException("缺少开源中文字体资源：" + ChineseFontPath);
            return font;
        }

        internal static RectTransform Panel(string name, Transform parent, float x, float y, float width, float height, Color color)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            PlaceRectangle(panel.GetComponent<RectTransform>(), x, y, width, height);
            panel.GetComponent<Image>().color = color;
            return panel.GetComponent<RectTransform>();
        }

        internal static Text Label(string name, Transform parent, Font font, string content,
            float x, float y, float width, float height, int size, Color color, FontStyle style = FontStyle.Normal)
        {
            GameObject label = new GameObject(name, typeof(RectTransform), typeof(Text));
            label.transform.SetParent(parent, false);
            PlaceRectangle(label.GetComponent<RectTransform>(), x, y, width, height);
            Text text = label.GetComponent<Text>();
            text.font = font;
            text.text = content;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAnchor.UpperLeft;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.supportRichText = false;
            WhiteboxTextLayout.Apply(text);
            return text;
        }

        internal static Button Button(string name, Transform parent, Font font, string content,
            float x, float y, float width, float height, Color color, out Text label)
        {
            RectTransform rectangle = Panel(name, parent, x, y, width, height, color);
            Button button = rectangle.gameObject.AddComponent<Button>();
            button.targetGraphic = rectangle.GetComponent<Image>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.18f, 1.18f, 1.18f);
            colors.pressedColor = new Color(0.72f, 0.9f, 0.9f);
            colors.selectedColor = new Color(1.1f, 1.1f, 1.1f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            button.colors = colors;
            label = Label("Label", rectangle, font, content, 8, 2, width - 16, height - 4, 17, TextColor);
            label.alignment = TextAnchor.MiddleCenter;
            return button;
        }

        internal static void PlaceRectangle(RectTransform rectangle, float x, float y, float width, float height)
        {
            rectangle.anchorMin = rectangle.anchorMax = new Vector2(0, 1);
            rectangle.pivot = new Vector2(0, 1);
            rectangle.anchoredPosition = new Vector2(x, -y);
            rectangle.sizeDelta = new Vector2(width, height);
        }

        internal static void SetReference(SerializedObject serialized, string property, Object value)
        {
            SerializedProperty field = serialized.FindProperty(property);
            if (field == null) throw new InvalidOperationException("UI 引用字段不存在：" + property);
            field.objectReferenceValue = value;
        }

        internal static void SetArray<T>(SerializedObject serialized, string property, T[] values) where T : Object
        {
            SerializedProperty field = serialized.FindProperty(property);
            field.arraySize = values.Length;
            for (int index = 0; index < values.Length; index++) field.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
        }

        internal static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            T created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        internal static Material MaterialAsset(string name, Color color, bool unlit = false)
        {
            string path = ContentRoot + "/Art/Materials/" + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            Shader shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("没有找到项目使用的 URP Shader。");
            Material material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.22f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        internal static Transform Child(string name, Transform parent)
        {
            Transform child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        internal static GameObject CreateShape(string name, PrimitiveType type, Transform parent,
            Vector3 position, Vector3 scale, Material material)
        {
            GameObject shape = GameObject.CreatePrimitive(type);
            shape.name = name;
            shape.transform.SetParent(parent, false);
            shape.transform.localPosition = position;
            shape.transform.localScale = scale;
            shape.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(shape.GetComponent<Collider>());
            return shape;
        }

        internal static BuildingView PlaceAuthored(BuildingView prefab, Transform parent, int x, int z)
        {
            if (prefab == null)
                throw new InvalidOperationException("初始建筑预制体组件已失效，位置：" + x + ", " + z);
            if (prefab.Definition == null)
                throw new InvalidOperationException("建筑定义引用为空：" + AssetDatabase.GetAssetPath(prefab));
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject, parent);
            if (instance == null) throw new InvalidOperationException("无法实例化初始建筑预制体：" + prefab.name);
            BuildingView view = instance.GetComponent<BuildingView>();
            if (view == null) throw new InvalidOperationException("建筑预制体根节点缺少 BuildingView：" + prefab.name);
            view.name = prefab.Definition.DisplayName + " [" + x + ", " + z + "]";
            view.transform.position = new Vector3(x + 0.5f, 0, z + 0.5f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(view.transform);
            return view;
        }

        /// <summary>只读诊断：直接报告资产、组件与定义的状态，不校验哈希，不修改资产。</summary>
        public static string DescribeBuildingAssets()
        {
            StringBuilder report = new StringBuilder();
            foreach (BuildingKind kind in new[] { BuildingKind.Reactor, BuildingKind.Relay, BuildingKind.Turret, BuildingKind.Core })
            {
                string path = ContentRoot + "/Prefabs/Buildings/" + kind + ".prefab";
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                BuildingView view = prefab == null ? null : prefab.GetComponent<BuildingView>();
                BuildingDefinitionAsset definition = view == null ? null : view.Definition;
                report.Append(kind).Append(": GameObject=").Append(prefab != null)
                    .Append(", BuildingView=").Append(view != null).Append(", Definition=").Append(definition != null);
                if (prefab != null) report.Append(", PrefabPersistent=").Append(EditorUtility.IsPersistent(prefab));
                if (view != null) report.Append(", ComponentPersistent=").Append(EditorUtility.IsPersistent(view));
                if (definition != null) report.Append(", DefinitionPath=").Append(AssetDatabase.GetAssetPath(definition));
                report.AppendLine();
            }
            return report.ToString();
        }

        internal static void CreateGrid(Transform parent, Material material)
        {
            Transform grid = Child("网格线 · 编辑器资产", parent);
            for (int index = 0; index <= 40; index++)
            {
                GridLine("Column " + index, grid, material, new Vector3(index, 0.006f, 0), new Vector3(index, 0.006f, 40));
                GridLine("Row " + index, grid, material, new Vector3(0, 0.006f, index), new Vector3(40, 0.006f, index));
            }
        }

        private static void GridLine(string name, Transform parent, Material material, Vector3 from, Vector3 to)
        {
            LineRenderer line = Child(name, parent).gameObject.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.positionCount = 2;
            line.startWidth = line.endWidth = 0.075f;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
        }

        internal static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
